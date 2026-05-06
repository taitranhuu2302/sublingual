# Backend Setup

## Vosk model

US-007 uses Vosk local STT. Download model:

- URL: https://alphacephei.com/vosk/models
- Recommended model: `vosk-model-small-en-us-0.15`

Extract to:

- `backend/models/vosk-model-small-en-us/`

Or set a custom path:

- Environment variable: `VOSK_MODEL_PATH`

Example:

```bash
VOSK_MODEL_PATH=/absolute/path/to/vosk-model-small-en-us pnpm backend
```

## Performance note

Target benchmark for US-007 is processing a 1-second audio chunk in under 200ms on a 4-core CPU. Verify on your machine with a representative PCM sample.

Benchmark helper script:

```bash
python backend/scripts/benchmark_vosk.py --sample /path/to/sample_1s_16khz_mono_int16.pcm --runs 10
```

Or with package script:

```bash
pnpm backend:benchmark-vosk -- --sample /path/to/sample_1s_16khz_mono_int16.pcm --runs 10
```

