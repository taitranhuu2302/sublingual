# Translate Service

Standalone self-hosted translation microservice for a Vosk-based live subtitle pipeline.

Powered by **NLLB-200 (600M distilled)** via **CTranslate2** for low-latency CPU inference.

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
