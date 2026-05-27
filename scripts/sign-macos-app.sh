#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
RUNTIME_IDENTIFIER="${1:-}"
SIGNING_IDENTITY="${2:-}"

if [[ -z "$RUNTIME_IDENTIFIER" ]]; then
  ARCH="$(uname -m)"
  if [[ "$ARCH" == "arm64" ]]; then
    RUNTIME_IDENTIFIER="osx-arm64"
  else
    RUNTIME_IDENTIFIER="osx-x64"
  fi
fi

if [[ -z "$SIGNING_IDENTITY" ]]; then
  echo "Usage: bash ./scripts/sign-macos-app.sh [runtime-identifier] \"Developer ID Application: Your Name (TEAMID)\"" >&2
  exit 1
fi

APP_BUNDLE_PATH="$ROOT_DIR/artifacts/macos/$RUNTIME_IDENTIFIER/Sublingual.app"
ENTITLEMENTS_PATH="$ROOT_DIR/packaging/macos/entitlements.plist"

if [[ ! -d "$APP_BUNDLE_PATH" ]]; then
  echo "App bundle not found: $APP_BUNDLE_PATH" >&2
  echo "Build it first with: bash ./scripts/package-macos-app.sh ${RUNTIME_IDENTIFIER}" >&2
  exit 1
fi

codesign \
  --force \
  --deep \
  --options runtime \
  --entitlements "$ENTITLEMENTS_PATH" \
  --sign "$SIGNING_IDENTITY" \
  "$APP_BUNDLE_PATH"

codesign --verify --deep --strict --verbose=2 "$APP_BUNDLE_PATH"
spctl --assess --type execute --verbose=4 "$APP_BUNDLE_PATH"

printf 'Signed macOS app bundle:\n- app: %s\n- identity: %s\n- entitlements: %s\n' "$APP_BUNDLE_PATH" "$SIGNING_IDENTITY" "$ENTITLEMENTS_PATH"
