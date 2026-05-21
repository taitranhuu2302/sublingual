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

"$PYTHON_EXEC" -m pip install --upgrade pip
"$PYTHON_EXEC" -m pip install -r "$PROJECT_DIR/requirements.txt"
"$PYTHON_EXEC" -m pip install ctranslate2
"$PYTHON_EXEC" -m pip install torch

printf 'Conversion dependencies installed successfully with %s\n' "$PYTHON_EXEC"
