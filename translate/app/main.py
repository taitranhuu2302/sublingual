import logging
import time

from fastapi import FastAPI, HTTPException

from app.config import get_settings
from app.schemas import (
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


settings = get_settings()
configure_logging(settings.log_level)
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
        translator = model_manager.get_translator(
            settings.default_source_lang,
            settings.default_target_lang,
            mode="fast",
        )
        translator.translate("hello", source_lang="en", target_lang="vi")
        logger.info("warmed up NLLB-200 model on startup")
    except HTTPException as exc:
        logger.warning("default model warmup skipped: %s", exc.detail)


def _prepare_text(text: str) -> str:
    prepared = truncate_text(normalize_text(text), settings.max_text_chars)
    if not prepared:
        raise HTTPException(status_code=400, detail="Text must not be empty.")
    return prepared


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
