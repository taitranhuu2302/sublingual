#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
RUNTIME_IDENTIFIER="${1:-}"

if [[ -z "$RUNTIME_IDENTIFIER" ]]; then
  ARCH="$(uname -m)"
  if [[ "$ARCH" == "arm64" ]]; then
    RUNTIME_IDENTIFIER="osx-arm64"
  else
    RUNTIME_IDENTIFIER="osx-x64"
  fi
fi

APP_PROJECT="$ROOT_DIR/src/Sublingual.App/Sublingual.App.csproj"
ARTIFACTS_DIR="$ROOT_DIR/artifacts/macos/$RUNTIME_IDENTIFIER"
PUBLISH_DIR="$ARTIFACTS_DIR/publish"
ZIP_PATH="$ARTIFACTS_DIR/sublingual-$RUNTIME_IDENTIFIER.zip"

rm -rf "$ARTIFACTS_DIR"
mkdir -p "$PUBLISH_DIR"

bash "$ROOT_DIR/scripts/build-macos-native.sh"

dotnet publish "$APP_PROJECT" \
  -c Release \
  -r "$RUNTIME_IDENTIFIER" \
  --self-contained true \
  -o "$PUBLISH_DIR"

rm -f "$ZIP_PATH"
ditto -c -k --sequesterRsrc --keepParent "$PUBLISH_DIR" "$ZIP_PATH"

printf 'Created macOS package:\n- publish: %s\n- zip: %s\n' "$PUBLISH_DIR" "$ZIP_PATH"
