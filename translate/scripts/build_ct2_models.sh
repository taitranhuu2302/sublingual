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

OUTPUT_DIR="$PROJECT_DIR/models/ct2/nllb-200-600M"

echo "Building NLLB-200 600M model with quantization=$QUANTIZATION"

"$PYTHON_EXEC" "$PROJECT_DIR/scripts/convert_nllb_to_ct2.py" \
  --hf_model facebook/nllb-200-distilled-600M \
  --output_dir "$OUTPUT_DIR" \
  --quantization "$QUANTIZATION" \
  "${FORCE_FLAG[@]}"

printf 'NLLB-200 model created successfully at %s with quantization=%s\n' "$OUTPUT_DIR" "$QUANTIZATION"
