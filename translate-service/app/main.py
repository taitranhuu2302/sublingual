import logging
from threading import Lock
import time

from fastapi import FastAPI, HTTPException

from app.config import get_settings
from app.schemas import (
    BatchTranslateRequest,
    BatchTranslateResponse,
    BatchTranslationItem,
    ErrorResponse,
    HealthResponse,
    ModelsResponse,
    RealtimeTranslateRequest,
    RealtimeTranslateResponse,
    TranslateRequest,
    TranslateResponse,
    ValidationErrorResponse,
)
from app.translator.model_manager import TranslationModelManager
from app.utils.logger import configure_logging
from app.utils.text import (
    is_good_realtime_boundary,
    is_too_similar,
    normalize_text,
    truncate_text,
)


settings = get_settings()
configure_logging(settings.log_level)
logger = logging.getLogger("translate-service")

BAD_REQUEST_RESPONSE = {
    "model": ErrorResponse,
    "description": "Bad request or requested translation model is not available.",
}

UNPROCESSABLE_ENTITY_RESPONSE = {
    "model": ValidationErrorResponse,
    "description": "Validation error in request payload.",
}


class RealtimeSessionCache:
    def __init__(self, ttl_sec: int = 300, min_realtime_chars: int = 8):
        self.ttl_sec = ttl_sec
        self.min_realtime_chars = min_realtime_chars
        self.sessions: dict[str, dict[str, object]] = {}
        self._lock = Lock()

    def should_translate(self, session_id: str, text: str, is_final: bool) -> bool:
        normalized = normalize_text(text)
        if is_final:
            return bool(normalized)

        if len(normalized) < self.min_realtime_chars:
            return False

        if not is_good_realtime_boundary(text):
            return False

        with self._lock:
            session = self.sessions.get(session_id)
            previous = str(session.get("last_text", "")) if session else ""

        if is_too_similar(previous, normalized, min_delta_chars=self.min_realtime_chars):
            return False

        return True

    def update(self, session_id: str, text: str, translated_text: str) -> None:
        now = time.monotonic()
        with self._lock:
            self.sessions[session_id] = {
                "last_text": normalize_text(text),
                "last_translated_text": translated_text,
                "updated_at": now,
            }

    def get(self, session_id: str) -> dict[str, object] | None:
        with self._lock:
            session = self.sessions.get(session_id)
            if session is None:
                return None

            return dict(session)

    def cleanup_expired(self) -> None:
        now = time.monotonic()
        with self._lock:
            expired_keys = [
                session_id
                for session_id, session in self.sessions.items()
                if now - float(session.get("updated_at", 0)) > self.ttl_sec
            ]
            for session_id in expired_keys:
                del self.sessions[session_id]

model_manager = TranslationModelManager(
    base_model_dir=settings.model_base_dir,
    device=settings.translation_device,
    compute_type=settings.translation_compute_type,
    inter_threads=settings.inter_threads,
    intra_threads=settings.intra_threads,
)
realtime_session_cache = RealtimeSessionCache(
    ttl_sec=settings.session_cache_ttl_sec,
    min_realtime_chars=settings.min_realtime_chars,
)

app = FastAPI(
    title="Translate Service",
    version="0.1.0",
    description=(
        "Standalone self-hosted translation API for a Vosk-based subtitle pipeline. "
        "This service uses MarianTokenizer for tokenization and CTranslate2 for inference."
    ),
    docs_url="/docs",
    redoc_url="/redoc",
    openapi_tags=[
        {
            "name": "system",
            "description": "Health and model discovery endpoints.",
        },
        {
            "name": "translation",
            "description": "Single and batch translation endpoints.",
        },
        {
            "name": "realtime",
            "description": "Realtime translation endpoint for Vosk partial/final text.",
        },
    ],
)


@app.on_event("startup")
def warmup_default_model() -> None:
    try:
        translator = model_manager.get_translator(
            settings.default_source_lang,
            settings.default_target_lang,
        )
        translator.translate("hello")
        logger.info(
            "warmed up default model pair=%s",
            model_manager.get_pair(
                settings.default_source_lang,
                settings.default_target_lang,
            ),
        )
    except HTTPException as exc:
        logger.warning("default model warmup skipped: %s", exc.detail)


def _prepare_text(text: str) -> str:
    prepared = truncate_text(normalize_text(text), settings.max_text_chars)
    if not prepared:
        raise HTTPException(status_code=400, detail="Text must not be empty.")

    return prepared


def _prepare_texts(texts: list[str]) -> list[str]:
    prepared = [_prepare_text(text) for text in texts]
    if not prepared:
        raise HTTPException(status_code=400, detail="Texts must not be empty.")

    return prepared


def _prepare_realtime_text(text: str) -> str:
    return truncate_text(normalize_text(text), settings.max_text_chars)


@app.get(
    "/health",
    response_model=HealthResponse,
    tags=["system"],
    summary="Health check",
    description="Returns service health, runtime device, compute type, and currently loaded model pairs.",
    responses={422: UNPROCESSABLE_ENTITY_RESPONSE},
)
def health() -> HealthResponse:
    return HealthResponse(
        status="ok",
        device=settings.translation_device,
        compute_type=settings.translation_compute_type,
        loaded_models=sorted(model_manager.cache.keys()),
    )


@app.get(
    "/models",
    response_model=ModelsResponse,
    tags=["system"],
    summary="List available models",
    description="Lists language-pair model folders discovered under the configured CT2 model directory.",
    responses={422: UNPROCESSABLE_ENTITY_RESPONSE},
)
def list_models() -> ModelsResponse:
    return ModelsResponse(
        available_pairs=model_manager.list_available_pairs(),
        base_model_dir=settings.model_base_dir,
        device=settings.translation_device,
        compute_type=settings.translation_compute_type,
    )


@app.post(
    "/translate",
    response_model=TranslateResponse,
    tags=["translation"],
    summary="Translate a single text",
    description="Translates one text segment using the requested source and target language pair.",
    responses={400: BAD_REQUEST_RESPONSE, 422: UNPROCESSABLE_ENTITY_RESPONSE},
)
def translate(request: TranslateRequest) -> TranslateResponse:
    started = time.perf_counter()
    source_text = _prepare_text(request.text)
    translator = model_manager.get_translator(request.source_lang, request.target_lang)
    translated_text = translator.translate(source_text)
    latency_ms = (time.perf_counter() - started) * 1000
    model = model_manager.get_pair(request.source_lang, request.target_lang)

    logger.info(
        "translate pair=%s latency_ms=%.2f chars=%d",
        model,
        latency_ms,
        len(source_text),
    )

    return TranslateResponse(
        source_text=source_text,
        translated_text=translated_text,
        source_lang=request.source_lang,
        target_lang=request.target_lang,
        latency_ms=latency_ms,
        model=model,
    )


@app.post(
    "/translate/batch",
    response_model=BatchTranslateResponse,
    tags=["translation"],
    summary="Translate a batch of texts",
    description="Translates multiple text segments in one request to improve throughput.",
    responses={400: BAD_REQUEST_RESPONSE, 422: UNPROCESSABLE_ENTITY_RESPONSE},
)
def translate_batch(request: BatchTranslateRequest) -> BatchTranslateResponse:
    started = time.perf_counter()
    source_texts = _prepare_texts(request.texts)
    translator = model_manager.get_translator(request.source_lang, request.target_lang)
    translated_texts = translator.translate_batch(source_texts)
    latency_ms = (time.perf_counter() - started) * 1000
    model = model_manager.get_pair(request.source_lang, request.target_lang)

    logger.info(
        "translate_batch pair=%s latency_ms=%.2f batch_size=%d",
        model,
        latency_ms,
        len(source_texts),
    )

    translations = [
        BatchTranslationItem(source_text=source_text, translated_text=translated_text)
        for source_text, translated_text in zip(source_texts, translated_texts)
    ]

    return BatchTranslateResponse(
        translations=translations,
        source_lang=request.source_lang,
        target_lang=request.target_lang,
        latency_ms=latency_ms,
        model=model,
    )


@app.post(
    "/translate/realtime",
    response_model=RealtimeTranslateResponse,
    tags=["realtime"],
    summary="Translate realtime Vosk text",
    description=(
        "Handles partial and final text from Vosk. Partial text may be skipped when it is too short, "
        "too similar to the previous request, or does not look like a good boundary."
    ),
    responses={400: BAD_REQUEST_RESPONSE, 422: UNPROCESSABLE_ENTITY_RESPONSE},
)
def translate_realtime(request: RealtimeTranslateRequest) -> RealtimeTranslateResponse:
    realtime_session_cache.cleanup_expired()
    source_text = _prepare_realtime_text(request.text)

    if not realtime_session_cache.should_translate(
        request.session_id,
        source_text,
        request.is_final,
    ):
        return RealtimeTranslateResponse(
            translated_text="",
            should_display=False,
            is_final=request.is_final,
            latency_ms=0,
        )

    started = time.perf_counter()
    translator = model_manager.get_translator(request.source_lang, request.target_lang)
    translated_text = translator.translate(source_text)
    latency_ms = (time.perf_counter() - started) * 1000

    session = realtime_session_cache.get(request.session_id)
    last_translated_text = str(session.get("last_translated_text", "")) if session else ""
    should_display = bool(translated_text)
    if not request.is_final and translated_text == last_translated_text:
        should_display = False

    realtime_session_cache.update(request.session_id, source_text, translated_text)

    logger.info(
        "translate_realtime pair=%s latency_ms=%.2f final=%s chars=%d display=%s",
        model_manager.get_pair(request.source_lang, request.target_lang),
        latency_ms,
        request.is_final,
        len(source_text),
        should_display,
    )

    return RealtimeTranslateResponse(
        translated_text=translated_text if should_display else "",
        should_display=should_display,
        is_final=request.is_final,
        latency_ms=latency_ms,
    )
