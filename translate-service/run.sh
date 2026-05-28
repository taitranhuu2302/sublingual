#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR"

# --- Find Python ---
if [[ -n "${PYTHON_BIN:-}" ]]; then
  PYTHON_EXEC="$PYTHON_BIN"
elif [[ -x "$PROJECT_DIR/.venv/bin/python" ]]; then
  PYTHON_EXEC="$PROJECT_DIR/.venv/bin/python"
elif [[ -x "$PROJECT_DIR/venv/bin/python" ]]; then
  PYTHON_EXEC="$PROJECT_DIR/venv/bin/python"
else
  printf 'Error: no virtual environment found.\n' >&2
  printf 'Create one with:\n' >&2
  printf '  python3 -m venv .venv && source .venv/bin/activate && pip install -r requirements.txt\n' >&2
  exit 1
fi

# --- Check dependencies ---
if ! "$PYTHON_EXEC" -c "import fastapi, ctranslate2, transformers" >/dev/null 2>&1; then
  printf 'Error: missing dependencies for %s\n' "$PYTHON_EXEC" >&2
  printf 'Install with: %s -m pip install -r %s/requirements.txt\n' "$PYTHON_EXEC" "$PROJECT_DIR" >&2
  exit 1
fi

# --- Check models ---
MODEL_DIR="$PROJECT_DIR/models/ct2"
if ! ls "$MODEL_DIR"/*/model.bin >/dev/null 2>&1; then
  printf 'Error: no CT2 models found in %s\n' "$MODEL_DIR" >&2
  printf 'Convert models with:\n' >&2
  printf '  PYTHON_BIN=%s bash %s/build_ct2_models.sh\n' "$PYTHON_EXEC" "$PROJECT_DIR/scripts" >&2
  exit 1
fi

# --- Expat workaround for Python 3.14 ---
if "$PYTHON_EXEC" -c "import sys; exit(0 if sys.version_info >= (3, 14) else 1)" 2>/dev/null; then
  EXPAT_LIB=$(brew --prefix expat 2>/dev/null)/lib
  if [[ -n "$EXPAT_LIB" && -f "$EXPAT_LIB/libexpat.1.dylib" ]]; then
    DYLD_LIBRARY_PATH="${DYLD_LIBRARY_PATH:-}:$EXPAT_LIB"
    export DYLD_LIBRARY_PATH
  fi
fi

# --- Load .env ---
if [[ -f "$PROJECT_DIR/.env" ]]; then
  set -a
  source "$PROJECT_DIR/.env"
  set +a
fi

# --- Read UVICORN_WORKERS ---
UVICORN_WORKERS="${UVICORN_WORKERS:-2}"

printf 'Starting Translate Service...\n'
printf '  Python:     %s\n' "$PYTHON_EXEC"
printf '  Workers:    %s\n' "$UVICORN_WORKERS"
printf '  Device:     %s\n' "${TRANSLATION_DEVICE:-cpu}"
printf '  Compute:    %s\n' "${TRANSLATION_COMPUTE_TYPE:-int8}"
printf '  Quality:    beam=%s compute=%s\n' "${QUALITY_BEAM_SIZE:-4}" "${QUALITY_COMPUTE_TYPE:-int8_float16}"
printf '  Port:       3333\n'

cd "$PROJECT_DIR"

exec "$PYTHON_EXEC" -m uvicorn app.main:app \
  --host 0.0.0.0 \
  --port 3333 \
  --workers "$UVICORN_WORKERS"
