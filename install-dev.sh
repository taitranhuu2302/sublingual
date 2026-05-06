#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="${ROOT_DIR}/backend"
VENV_DIR="${BACKEND_DIR}/.venv"

echo "Installing Node dependencies with pnpm..."
cd "${ROOT_DIR}"
pnpm install

echo "Setting up Python virtual environment in backend/.venv..."
cd "${BACKEND_DIR}"
if [[ ! -d "${VENV_DIR}" ]]; then
  python3 -m venv .venv
fi

if [[ -x "${VENV_DIR}/bin/python" ]]; then
  PYTHON_BIN="${VENV_DIR}/bin/python"
else
  PYTHON_BIN="python3"
fi

echo "Installing backend Python dependencies..."
"${PYTHON_BIN}" -m pip install --upgrade pip
"${PYTHON_BIN}" -m pip install -r requirements.txt

echo "Done."
echo "Run backend with: pnpm backend"

