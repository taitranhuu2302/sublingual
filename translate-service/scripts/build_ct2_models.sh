#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

if [[ -n "${PYTHON_BIN:-}" ]]; then
  PYTHON_EXEC="$PYTHON_BIN"
elif [[ -x "$PROJECT_DIR/venv/bin/python" ]]; then
  PYTHON_EXEC="$PROJECT_DIR/venv/bin/python"
else
  PYTHON_EXEC="python3"
fi

QUANTIZATION="int8"
FORCE_FLAG=()

for arg in "$@"; do
  if [[ "$arg" == "--force" ]]; then
    FORCE_FLAG=(--force)
  elif [[ -z "${QUANTIZATION_SET:-}" && -n "$arg" ]]; then
    QUANTIZATION="$arg"
    QUANTIZATION_SET=1
  fi
done

if ! "$PYTHON_EXEC" -c "import transformers" >/dev/null 2>&1; then
  printf 'Error: transformers is not installed for %s\n' "$PYTHON_EXEC" >&2
  printf 'Install dependencies in the project environment or set PYTHON_BIN explicitly.\n' >&2
  exit 1
fi

"$PYTHON_EXEC" "$PROJECT_DIR/scripts/convert_marian_to_ct2.py" \
  --hf_model Helsinki-NLP/opus-mt-en-vi \
  --output_dir "$PROJECT_DIR/models/ct2/en-vi" \
  --quantization "$QUANTIZATION" \
  "${FORCE_FLAG[@]}"

"$PYTHON_EXEC" "$PROJECT_DIR/scripts/convert_marian_to_ct2.py" \
  --hf_model Helsinki-NLP/opus-mt-vi-en \
  --output_dir "$PROJECT_DIR/models/ct2/vi-en" \
  --quantization "$QUANTIZATION" \
  "${FORCE_FLAG[@]}"

"$PYTHON_EXEC" "$PROJECT_DIR/scripts/convert_marian_to_ct2.py" \
  --hf_model Helsinki-NLP/opus-mt-zh-vi \
  --output_dir "$PROJECT_DIR/models/ct2/zh-vi" \
  --quantization "$QUANTIZATION" \
  "${FORCE_FLAG[@]}"

printf 'CT2 models created successfully with quantization=%s\n' "$QUANTIZATION"
