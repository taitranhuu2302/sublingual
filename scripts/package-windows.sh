#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
RUNTIME_IDENTIFIER="${1:-win-x64}"

APP_PROJECT="$ROOT_DIR/src/Sublingual.App/Sublingual.App.csproj"
ARTIFACTS_DIR="$ROOT_DIR/artifacts/windows/$RUNTIME_IDENTIFIER"
PUBLISH_DIR="$ARTIFACTS_DIR/publish"
ZIP_PATH="$ARTIFACTS_DIR/sublingual-$RUNTIME_IDENTIFIER.zip"

rm -rf "$ARTIFACTS_DIR"
mkdir -p "$PUBLISH_DIR"

dotnet publish "$APP_PROJECT" \
  -c Release \
  -r "$RUNTIME_IDENTIFIER" \
  --self-contained true \
  -o "$PUBLISH_DIR"

rm -f "$ZIP_PATH"
ditto -c -k --sequesterRsrc --keepParent "$PUBLISH_DIR" "$ZIP_PATH"

printf 'Created Windows package:\n- publish: %s\n- zip: %s\n' "$PUBLISH_DIR" "$ZIP_PATH"
