#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

if [[ -n "${PYTHON_BIN:-}" ]]; then
  PYTHON_EXEC="$PYTHON_BIN"
elif [[ -x "$PROJECT_DIR/.venv/bin/python" ]]; then
  PYTHON_EXEC="$PROJECT_DIR/.venv/bin/python"
elif [[ -x "$PROJECT_DIR/venv/bin/python" ]]; then
  PYTHON_EXEC="$PROJECT_DIR/venv/bin/python"
else
  PYTHON_EXEC="python3"
fi

QUANTIZATION="int8"
QUALITY_QUANTIZATION="int8_float16"
FORCE_FLAG=()

for arg in "$@"; do
  if [[ "$arg" == "--force" ]]; then
    FORCE_FLAG=(--force)
  elif [[ "$arg" == "--quality-only" ]]; then
    QUALITY_ONLY=1
    QUALITY_ONLY_SET=1
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

convert_pair() {
  local hf_model="$1"
  local pair="$2"
  local quantization="$3"
  local suffix="${4:-}"

  "$PYTHON_EXEC" "$PROJECT_DIR/scripts/convert_marian_to_ct2.py" \
    --hf_model "$hf_model" \
    --output_dir "$PROJECT_DIR/models/ct2/${pair}${suffix}" \
    --quantization "$quantization" \
    "${FORCE_FLAG[@]}"
}

if [[ -z "${QUALITY_ONLY_SET:-}" ]]; then
  convert_pair Helsinki-NLP/opus-mt-en-vi en-vi "$QUANTIZATION"
  convert_pair Helsinki-NLP/opus-mt-vi-en vi-en "$QUANTIZATION"
  convert_pair Helsinki-NLP/opus-mt-zh-vi zh-vi "$QUANTIZATION"
fi

convert_pair Helsinki-NLP/opus-mt-en-vi en-vi "$QUALITY_QUANTIZATION" "-quality"
convert_pair Helsinki-NLP/opus-mt-vi-en vi-en "$QUALITY_QUANTIZATION" "-quality"
convert_pair Helsinki-NLP/opus-mt-zh-vi zh-vi "$QUALITY_QUANTIZATION" "-quality"

if [[ -n "${QUALITY_ONLY_SET:-}" ]]; then
  printf 'Quality CT2 models created successfully with quantization=%s\n' "$QUALITY_QUANTIZATION"
else
  printf 'All CT2 models created successfully (fast=%s, quality=%s)\n' "$QUANTIZATION" "$QUALITY_QUANTIZATION"
fi
