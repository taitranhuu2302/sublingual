# Translate Service

Standalone self-hosted translation microservice for a Vosk-based live subtitle pipeline.

Powered by **NLLB-200 (600M distilled)** via **CTranslate2** for low-latency CPU inference.

## Quick Start

```bash
# 1. Setup
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt torch
cp .env.example .env

# 2. Build model (one-time, ~5 min)
bash scripts/build_ct2_models.sh int8

# 3. Run
# macOS: export DYLD_LIBRARY_PATH=/opt/homebrew/opt/expat/lib
uvicorn app.main:app --host 0.0.0.0 --port 8000

# 4. Test
curl -s http://localhost:8000/health | python3 -m json.tool
curl -s -X POST http://localhost:8000/translate \
  -H "Content-Type: application/json" \
  -d '{"text":"Hello world","source_lang":"en","target_lang":"vi"}' | python3 -m json.tool
```

## Features

- `GET /health`
- `POST /translate` — quality translation with beam search + Vietnamese post-processing
- `POST /translate/fast` — low-latency greedy translation for realtime subtitles
- Lazy model loading with in-memory cache
- Session-based partial text deduplication for Vosk realtime
- Vietnamese diacritic normalization and glossary support

## Endpoints

### `GET /health`

```json
{
  "status": "ok",
  "device": "cpu",
  "compute_type": "int8",
  "loaded_models": ["nllb-fast"],
  "available_pairs": ["en-vi", "vi-en", "en-zh", "zh-en", "vi-zh", "zh-vi"],
  "model": "nllb-200-distilled-600M"
}
```

### `POST /translate`

Quality translation (beam_size=4, Vietnamese post-processing). Accepts single string or array.

```json
// Request
{ "text": "Hello everyone, welcome.", "source_lang": "en", "target_lang": "vi" }

// Response
{ "translated_text": "Xin chào mọi người, chào mừng.", "latency_ms": 250.0 }
```

### `POST /translate/fast`

Low-latency greedy translation for Vosk realtime subtitles (<100ms target).

```json
// Request
{ "text": "hello everyone", "source_lang": "en", "target_lang": "vi", "session_id": "abc123", "is_final": false }

// Response
{ "translated_text": "xin chào mọi người", "should_display": true, "latency_ms": 18.1 }
```

## Architecture

```text
NLLB-200 600M (CTranslate2 int8)
├── Tokenizer: NLLB SentencePiece
├── Inference: CTranslate2 Translator
│   ├── /translate/fast → beam_size=1, greedy decode
│   └── /translate      → beam_size=4, beam search
└── Post-processing Pipeline
    ├── Whitespace normalize
    ├── Diacritics fix (Vietnamese tone marks)
    ├── Word boundary merge
    └── Glossary override (configurable .json)
```

## Project Structure

```text
translate/
  app/
    __init__.py
    main.py
    config.py
    schemas.py
    translator/
      __init__.py
      nllb_ct2.py
      model_manager.py
      session_cache.py
    postprocess/
      __init__.py
      vi_normalizer.py
      glossary.py
    utils/
      __init__.py
      logger.py
      text.py
  scripts/
    convert_nllb_to_ct2.py
    build_ct2_models.sh
    test_translate.py
    benchmark.py
  models/
    ct2/
      nllb-200-600M/
  docker/
    Dockerfile
    docker-compose.yml
  requirements.txt
  .env.example
```

## Installation

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
cp .env.example .env
```

**macOS note:** If you encounter `pyexpat` errors during pip/venv setup, set `DYLD_LIBRARY_PATH`:
```bash
DYLD_LIBRARY_PATH=/opt/homebrew/opt/expat/lib pip install -r requirements.txt
```

## Convert NLLB-200 to CTranslate2

Requires `torch` for conversion only (not needed at runtime):

```bash
pip install torch
python scripts/convert_nllb_to_ct2.py \
  --hf_model facebook/nllb-200-distilled-600M \
  --output_dir models/ct2/nllb-200-600M \
  --quantization int8
```

Or build with the convenience script:

```bash
bash scripts/build_ct2_models.sh int8
```

Overwrite existing output:

```bash
bash scripts/build_ct2_models.sh int8 --force
```

## Run

```bash
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

**macOS note:** If you hit `pyexpat` errors at startup, prepend the expat library path:
```bash
DYLD_LIBRARY_PATH=/opt/homebrew/opt/expat/lib uvicorn app.main:app --host 0.0.0.0 --port 8000
```

The service preloads both fast and quality models on startup (~5s warmup). Once warmed up, typical latency:
- `/translate/fast`: <500ms (greedy, beam_size=1)
- `/translate`: <1s after warmup (beam search, beam_size=4, post-processing)

## Test

```bash
# Quality mode (single)
python scripts/test_translate.py --url http://localhost:8000 --text "Hello world" --mode quality

# Quality mode (batch)
python scripts/test_translate.py --url http://localhost:8000 --text "Hello|Goodbye" --mode quality --batch

# Fast mode
python scripts/test_translate.py --url http://localhost:8000 --text "Hello world" --mode fast
```

## Benchmark

```bash
# Both modes
python scripts/benchmark.py --url http://localhost:8000 --mode both --iterations 100

# Fast only
python scripts/benchmark.py --url http://localhost:8000 --mode fast --iterations 200

# Quality only
python scripts/benchmark.py --url http://localhost:8000 --mode quality --iterations 50
```

## Docker

```bash
docker compose -f docker/docker-compose.yml up --build
```

## CPU Tuning

Default configuration is CPU-first:

- `TRANSLATION_DEVICE=cpu`
- `TRANSLATION_COMPUTE_TYPE=int8`
- `INTER_THREADS=1`
- `INTRA_THREADS=4`

Use shorter subtitle segments for lower perceived latency. Keep `MAX_TEXT_CHARS` bounded to avoid expensive requests.

## Troubleshooting

### `pyexpat` / `Symbol not found: _XML_SetAllocTrackerActivationThreshold` (macOS)

This is a known conflict between Homebrew Python and macOS's bundled `libexpat`. Workaround:

```bash
export DYLD_LIBRARY_PATH=/opt/homebrew/opt/expat/lib
```

Add to your `~/.zshrc` for a permanent fix. This affects `pip`, `uvicorn`, and any Python process that imports XML-related modules.

### Model not found

If you see "Model directory not found", convert the NLLB-200 model first:

```bash
bash scripts/build_ct2_models.sh int8
```

Make sure `models/ct2/nllb-200-600M/` exists and contains the tokenizer and CTranslate2 model files.

### Slow CPU performance

- Use `int8` quantization
- Reduce thread contention (`INTER_THREADS=1`)
- Keep requests short
