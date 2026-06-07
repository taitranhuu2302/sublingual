#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="$SCRIPT_DIR/../../desktop/bin/translate"

mkdir -p "$OUTPUT_DIR"

echo "Building translate-service..."
echo "  Project: $PROJECT_DIR"
echo "  Output:  $OUTPUT_DIR"

if [[ "${1:-}" == "--clean" ]]; then
  rm -rf "$PROJECT_DIR/build" "$PROJECT_DIR/dist"
  echo "  Cleaned build/dist directories"
fi

"$PROJECT_DIR/.venv/bin/pip" install pyinstaller

pushd "$PROJECT_DIR" > /dev/null

"$PROJECT_DIR/.venv/bin/pyinstaller" \
  --onefile \
  --name translate-service \
  --distpath "$OUTPUT_DIR" \
  --workpath "$PROJECT_DIR/build" \
  --specpath "$PROJECT_DIR/build" \
  --add-data ".env.example:." \
  --hidden-import "app.translator" \
  --hidden-import "app.translator.nllb_ct2" \
  --hidden-import "app.translator.model_manager" \
  --hidden-import "app.translator.session_cache" \
  --hidden-import "app.postprocess" \
  --hidden-import "app.postprocess.vi_normalizer" \
  --hidden-import "app.postprocess.glossary" \
  --hidden-import "app.utils" \
  --hidden-import "app.utils.text" \
  --hidden-import "app.utils.logger" \
  --hidden-import "ctranslate2" \
  --hidden-import "transformers" \
  --hidden-import "sentencepiece" \
  app/main.py

popd > /dev/null

echo "Done: $OUTPUT_DIR/translate-service"
