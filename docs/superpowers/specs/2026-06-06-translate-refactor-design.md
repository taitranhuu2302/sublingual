# Translate Service Refactor — Design Spec

**Date**: 2026-06-06
**Status**: approved

## Overview

Refactor the `/translate/` microservice: simplify API endpoints, replace the MarianMT model with NLLB-200 600M for better Vietnamese translation quality, and add a post-processing pipeline for Vietnamese text normalization.

### Motivations

- Current MarianMT (opus-mt) has poor translation quality for en-vi/vi-en, especially for domain terminology and Vietnamese grammar
- API has too many endpoints with overlapping responsibilities
- Response schemas are unnecessarily verbose
- CPU-only deployment constraints require a model that balances quality and latency

## Target Architecture

```
NLLB-200 600M (CTranslate2 int8)
├── Tokenizer: NLLB SentencePiece (SPM)
├── Inference: CTranslate2 Translator
│   ├── /translate/fast → beam_size=1, greedy decode
│   └── /translate      → beam_size=4, beam search
└── Post-processing Pipeline
    ├── Whitespace normalize
    ├── Diacritics fix (Vietnamese tone marks)
    ├── Word boundary merge
    └── Glossary override (configurable .json)
```

## API Endpoints

### Endpoints (6 → 3)

| Keep | Remove | Reason |
|------|--------|--------|
| `GET /health` | | Expanded to include model info |
| | `GET /models` | Merged into `/health` |
| | `POST /translate` (old) | Replaced by new `/translate` |
| | `POST /translate/batch` | Merged into `/translate` (array detection) |
| `POST /translate/fast` | | New: low-latency realtime, replaces `/translate/realtime` |
| | `POST /translate/realtime` | Renamed to `/translate/fast` |
| | `POST /translate/realtime/reset` | Removed; client manages session lifecycle |

### `GET /health`

Response:
```json
{
  "status": "ok",
  "device": "cpu",
  "compute_type": "int8",
  "loaded_models": ["en-vi", "vi-en"],
  "available_pairs": ["en-vi", "vi-en"],
  "model": "nllb-200-distilled-600M"
}
```

### `POST /translate/fast`

Low-latency translation for Vosk realtime subtitles. Uses greedy decoding (beam_size=1), session-based skip logic, target <100ms.

Request:
```json
{
  "text": "hello everyone welcome to",
  "source_lang": "en",
  "target_lang": "vi",
  "session_id": "abc123",
  "is_final": false
}
```

Response:
```json
{
  "translated_text": "xin chào mọi người chào mừng đến",
  "should_display": true,
  "latency_ms": 18.0
}
```

### `POST /translate`

Quality translation for general text. Uses beam search (beam_size=4), runs through full post-processing pipeline, target <500ms. Supports both single string and batch array — auto-detected by input type.

Request (single):
```json
{
  "text": "Hello everyone, welcome to today's meeting.",
  "source_lang": "en",
  "target_lang": "vi"
}
```

Request (batch):
```json
{
  "text": ["Hello everyone.", "Welcome to today's meeting."],
  "source_lang": "en",
  "target_lang": "vi"
}
```

Response:
```json
{
  "translated_text": "Xin chào mọi người, chào mừng đến với cuộc họp hôm nay.",
  "latency_ms": 250.0
}
```

## Directory Structure

```
translate/
  app/
    main.py              # FastAPI app — 3 endpoints
    config.py            # Settings (add fast/quality model configs)
    schemas.py           # Request/response models (simplified, removed unused)
    translator/
      nllb_ct2.py        # NLLB-200 CTranslate2 wrapper (new, replaces marian_ct2.py)
      model_manager.py   # Lazy loading, cache per pair, dual config (fast/quality)
      session_cache.py   # Session cache extracted from main.py (new file)
    postprocess/
      __init__.py
      vi_normalizer.py   # Vietnamese text normalization (new)
      glossary.py        # Configurable terminology glossary (new)
    utils/
      text.py            # normalize, truncate, boundary checks (updated)
      logger.py          # (unchanged)
  scripts/
    convert_nllb_to_ct2.py  # NLLB → CTranslate2 converter (new)
    build_ct2_models.sh     # Model build script (updated)
    test_translate.py       # Test script (updated)
    benchmark.py            # Benchmark for both modes (updated)
  models/
    ct2/
      nllb-200-600M/        # Converted model directory
  docker/
    Dockerfile              # Updated for NLLB
    docker-compose.yml      # (unchanged)
  requirements.txt          # Updated (remove marian-specific deps, add NLLB deps)
  .env.example              # Updated config vars
  README.md                 # Updated docs
```

## Data Flow

### `/translate/fast`
```
Request → normalize → session cache (skip check)
→ greedy decode (beam_size=1) → response (<100ms)
```

### `/translate`
```
Request → normalize → batch detect
→ beam search decode (beam_size=4)
→ post-processing pipeline → response (<500ms)
```

### Post-processing Pipeline
```
Raw translated text
→ Step 1: Whitespace normalize (collapse, strip)
→ Step 2: Diacritics fix (common tone mark errors: òa→oà, qủa→quả, etc.)
→ Step 3: Word boundary merge (fix tokenizer splitting errors)
→ Step 4: Glossary lookup (term substitution from .json config)
→ Final text
```

## Key Code Changes

### Files to DELETE
- `app/translator/marian_ct2.py`

### Files to CREATE
- `app/translator/nllb_ct2.py`
- `app/translator/session_cache.py`
- `app/postprocess/__init__.py`
- `app/postprocess/vi_normalizer.py`
- `app/postprocess/glossary.py`
- `scripts/convert_nllb_to_ct2.py`

### Files to MODIFY
- `app/main.py` — replace 6 endpoints with 3, remove RealtimeSessionCache inline class
- `app/schemas.py` — remove unused schemas, simplify remaining ones
- `app/config.py` — add beam_size, alternative count, glossary path settings
- `app/translator/model_manager.py` — support dual config (fast/quality), NLLB loading
- `app/utils/text.py` — update for NLLB text normalization
- `requirements.txt` — remove sacremoses, add sentencepiece if needed
- `scripts/build_ct2_models.sh` — target NLLB instead of Marian
- `scripts/test_translate.py` — test both /translate and /translate/fast
- `scripts/benchmark.py` — benchmark both modes
- `docker/Dockerfile` — update dependencies
- `.env.example` — add new config vars
- `README.md` — full rewrite

### Schemas to REMOVE
- `ErrorResponse`, `ValidationErrorItem`, `ValidationErrorResponse` (use FastAPI defaults)
- `ModelsResponse`
- `BatchTranslateRequest`, `BatchTranslateResponse`, `BatchTranslationItem`
- `RealtimeTranslateRequest`, `RealtimeTranslateResponse`
- `RealtimeSessionResetRequest`, `RealtimeSessionResetResponse`

### Schemas to ADD/UPDATE
- `HealthResponse` — add `available_pairs`, `model` fields
- `TranslateFastRequest` — new, minimal
- `TranslateFastResponse` — new, minimal
- `TranslateRequest` — new, unified (text: str | list[str])
- `TranslateResponse` — new, simplified

## Error Handling

| Status | Trigger |
|--------|---------|
| 400 | Model not found for pair, empty text after normalization |
| 422 | Pydantic validation failure |
| 500 | Model loading failure, inference error |

## Testing Strategy

1. **Unit tests**: `vi_normalizer.py`, `glossary.py`, `text.py`
2. **Integration tests**: HTTP calls to all 3 endpoints with sample text, verify format
3. **Smoke test**: Warmup model, translate sample sentences en→vi and vi→en
4. **Benchmark**: `benchmark.py` tests both `/translate` and `/translate/fast` for latency

## Rollout Plan

1. Convert NLLB-200 600M to CTranslate2 int8
2. Implement `nllb_ct2.py` and `model_manager.py` changes
3. Implement `postprocess/` module
4. Rewrite `main.py` with 3 endpoints
5. Update schemas, config, scripts, Docker
6. Test with sample data
7. Update README
