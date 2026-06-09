import argparse
import logging
import threading
import time

from fastapi import FastAPI, HTTPException
from fastapi.responses import JSONResponse

from app.config import get_settings
from app.schemas import (
    DownloadStatusResponse,
    HealthResponse,
    TranslateFastRequest,
    TranslateFastResponse,
    TranslateRequest,
    TranslateResponse,
)
from app.translator.model_manager import TranslationModelManager
from app.translator.session_cache import RealtimeSessionCache
from app.utils.logger import configure_logging
from app.utils.text import normalize_text, truncate_text


# CLI args override env for port, host, models-dir, log-dir
parser = argparse.ArgumentParser(description="Translate Service")
parser.add_argument("--host", default="127.0.0.1", help="Bind host")
parser.add_argument("--port", type=int, default=3333, help="Bind port")
parser.add_argument("--models-dir", default=None, help="Path to CTranslate2 model directory")
parser.add_argument("--log-dir", default=None, help="Path to log directory")
cli_args = parser.parse_args()

# Override settings with CLI args
import os as _os
if cli_args.models_dir:
    _os.environ["MODEL_BASE_DIR"] = cli_args.models_dir
if cli_args.log_dir:
    _os.environ["LOG_DIR"] = cli_args.log_dir

CLI_HOST = cli_args.host
CLI_PORT = cli_args.port

settings = get_settings()
configure_logging(settings.log_level, log_dir=settings.log_dir if settings.log_dir else None)
logger = logging.getLogger("translate")

model_manager = TranslationModelManager(
    base_model_dir=settings.model_base_dir,
    device=settings.translation_device,
    compute_type=settings.translation_compute_type,
    inter_threads=settings.inter_threads,
    intra_threads=settings.intra_threads,
    fast_beam_size=settings.fast_beam_size,
    quality_beam_size=settings.quality_beam_size,
)
realtime_session_cache = RealtimeSessionCache(
    ttl_sec=settings.session_cache_ttl_sec,
    min_realtime_chars=settings.min_realtime_chars,
)

app = FastAPI(
    title="Translate Service",
    version="0.2.0",
    description=(
        "Standalone self-hosted translation API powered by NLLB-200 and CTranslate2. "
        "Supports fast greedy translation for realtime subtitles and quality beam-search "
        "translation with Vietnamese post-processing."
    ),
    docs_url="/docs",
    redoc_url="/redoc",
    openapi_tags=[
        {
            "name": "system",
            "description": "Health and model discovery.",
        },
        {
            "name": "translation",
            "description": "Quality translation with beam search and post-processing.",
        },
        {
            "name": "fast",
            "description": "Low-latency greedy translation for realtime subtitles.",
        },
    ],
)


@app.on_event("startup")
def warmup_default_model() -> None:
    try:
        if not model_manager.models_available:
            logger.warning("No translation models found in %s — service running in degraded mode", settings.model_base_dir)
            return

        try:
            translator = model_manager.get_translator(
                settings.default_source_lang,
                settings.default_target_lang,
                mode="fast",
            )
            translator.translate("hello", source_lang="en", target_lang="vi")
            logger.info("warmed up NLLB-200 fast model on startup")
        except HTTPException as exc:
            logger.warning("fast model warmup skipped: %s", exc.detail)

        try:
            translator = model_manager.get_translator(
                settings.default_source_lang,
                settings.default_target_lang,
                mode="quality",
            )
            translator.translate("hello", source_lang="en", target_lang="vi")
            logger.info("warmed up NLLB-200 quality model on startup")
        except HTTPException as exc:
            logger.warning("quality model warmup skipped: %s", exc.detail)
    except Exception as exc:
        logger.error("warmup crashed: %s", exc, exc_info=True)


def _prepare_text(text: str) -> str:
    prepared = truncate_text(normalize_text(text), settings.max_text_chars)
    if not prepared:
        raise HTTPException(status_code=400, detail="Text must not be empty.")
    return prepared


# Model download state
_download_state: dict = {
    "status": "idle",  # idle | downloading | completed | error
    "percent": 0,
    "error": None,
}
_download_lock = threading.Lock()


@app.get(
    "/models/download/status",
    response_model=DownloadStatusResponse,
    tags=["system"],
    summary="Check model download status",
)
def download_status() -> DownloadStatusResponse:
    return DownloadStatusResponse(
        status=_download_state["status"],
        percent=_download_state["percent"],
        error=_download_state.get("error"),
    )


@app.post(
    "/models/download",
    tags=["system"],
    summary="Download and convert NLLB-200 model from HuggingFace",
)
def download_models() -> JSONResponse:
    with _download_lock:
        if _download_state["status"] == "downloading":
            return JSONResponse(
                status_code=409,
                content={"detail": "Download already in progress"},
            )
        _download_state["status"] = "downloading"
        _download_state["percent"] = 0
        _download_state["error"] = None

    def _download_thread() -> None:
        try:
            from pathlib import Path
            import ctranslate2
            from transformers import AutoTokenizer

            hf_model = "facebook/nllb-200-distilled-600M"
            target_dir = Path(settings.model_base_dir)

            if target_dir.exists():
                import shutil
                shutil.rmtree(target_dir)
            target_dir.mkdir(parents=True, exist_ok=True)

            # Step 1: Convert HuggingFace model to CTranslate2 (downloads + converts)
            _download_state["percent"] = 10
            logger.info("downloading & converting %s to CTranslate2...", hf_model)
            converter = ctranslate2.converters.TransformersConverter(
                model_name_or_path=hf_model,
            )
            converter.convert(str(target_dir), quantization=settings.translation_compute_type, force=True)
            _download_state["percent"] = 80

            # Step 2: Save tokenizer
            logger.info("saving tokenizer...")
            tokenizer = AutoTokenizer.from_pretrained(hf_model)
            tokenizer.save_pretrained(str(target_dir))
            _download_state["percent"] = 95

            _download_state["status"] = "completed"
            _download_state["percent"] = 100
            logger.info("model download & conversion completed — restart service to load models")
        except Exception as exc:
            _download_state["status"] = "error"
            _download_state["error"] = str(exc)
            logger.error("model download failed: %s", exc)

    threading.Thread(target=_download_thread, daemon=True).start()

    return JSONResponse(content={"status": "started"})


@app.get(
    "/health",
    response_model=HealthResponse,
    tags=["system"],
    summary="Health check",
)
def health() -> HealthResponse:
    return HealthResponse(
        status="ok",
        device=settings.translation_device,
        compute_type=settings.translation_compute_type,
        loaded_models=model_manager.loaded_models,
        available_pairs=model_manager.list_available_pairs(),
        models_available=model_manager.models_available,
    )


@app.post(
    "/translate/fast",
    response_model=TranslateFastResponse,
    tags=["fast"],
    summary="Fast realtime translation",
    description=(
        "Low-latency greedy translation for Vosk subtitle pipeline. "
        "Skips redundant partials based on session state."
    ),
    responses={400: {"description": "Model not found or text empty"}},
)
def translate_fast(request: TranslateFastRequest) -> TranslateFastResponse:
    realtime_session_cache.cleanup_expired()
    source_text = _prepare_text(request.text)

    should_translate, _ = realtime_session_cache.should_translate(
        request.session_id,
        source_text,
        request.is_final,
    )
    if not should_translate:
        return TranslateFastResponse(
            translated_text="",
            should_display=False,
            latency_ms=0,
        )

    started = time.perf_counter()
    translator = model_manager.get_translator(
        request.source_lang, request.target_lang, mode="fast"
    )
    translated_text = translator.translate(
        source_text,
        source_lang=request.source_lang,
        target_lang=request.target_lang,
    )
    latency_ms = (time.perf_counter() - started) * 1000

    session = realtime_session_cache.get(request.session_id)
    last_translated = str(session.get("last_translated_text", "")) if session else ""
    should_display = bool(translated_text)
    if not request.is_final and translated_text == last_translated:
        should_display = False

    realtime_session_cache.update(request.session_id, source_text, translated_text)

    logger.info(
        "translate_fast chars=%d latency_ms=%.2f final=%s display=%s",
        len(source_text),
        latency_ms,
        request.is_final,
        should_display,
    )

    return TranslateFastResponse(
        translated_text=translated_text if should_display else "",
        should_display=should_display,
        latency_ms=latency_ms,
    )


@app.post(
    "/translate",
    response_model=TranslateResponse,
    tags=["translation"],
    summary="Quality translation",
    description=(
        "Translates text with beam search and Vietnamese post-processing. "
        "Accepts single string or array of strings for batch translation."
    ),
    responses={400: {"description": "Model not found or text empty"}},
)
def translate(request: TranslateRequest) -> TranslateResponse:
    started = time.perf_counter()
    translator = model_manager.get_translator(
        request.source_lang, request.target_lang, mode="quality"
    )

    if isinstance(request.text, str):
        source_text = _prepare_text(request.text)
        translated_text = translator.translate(
            source_text,
            source_lang=request.source_lang,
            target_lang=request.target_lang,
        )
    else:
        source_texts = [_prepare_text(t) for t in request.text]
        translated_text = translator.translate_batch(
            source_texts,
            source_lang=request.source_lang,
            target_lang=request.target_lang,
        )

    latency_ms = (time.perf_counter() - started) * 1000

    logger.info(
        "translate lang=%s->%s latency_ms=%.2f",
        request.source_lang,
        request.target_lang,
        latency_ms,
    )

    return TranslateResponse(
        translated_text=translated_text,
        latency_ms=latency_ms,
    )


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host=CLI_HOST, port=CLI_PORT)
