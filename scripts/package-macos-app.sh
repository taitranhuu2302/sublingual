#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
RUNTIME_IDENTIFIER="${1:-}"
BUNDLE_IDENTIFIER="${2:-com.sublingual.app}"
APP_VERSION="${3:-0.1.0}"

if [[ -z "$RUNTIME_IDENTIFIER" ]]; then
  ARCH="$(uname -m)"
  if [[ "$ARCH" == "arm64" ]]; then
    RUNTIME_IDENTIFIER="osx-arm64"
  else
    RUNTIME_IDENTIFIER="osx-x64"
  fi
fi

APP_PROJECT="$ROOT_DIR/src/Sublingual.App/Sublingual.App.csproj"
APP_DISPLAY_NAME="Sublingual"
APP_EXECUTABLE="Sublingual.App"
ARTIFACTS_DIR="$ROOT_DIR/artifacts/macos/$RUNTIME_IDENTIFIER"
PUBLISH_DIR="$ARTIFACTS_DIR/publish"
APP_BUNDLE_DIR="$ARTIFACTS_DIR/$APP_DISPLAY_NAME.app"
APP_CONTENTS_DIR="$APP_BUNDLE_DIR/Contents"
APP_MACOS_DIR="$APP_CONTENTS_DIR/MacOS"
APP_RESOURCES_DIR="$APP_CONTENTS_DIR/Resources"
PLIST_TEMPLATE="$ROOT_DIR/packaging/macos/Info.plist.template"
PLIST_PATH="$APP_CONTENTS_DIR/Info.plist"
ENTITLEMENTS_PATH="$ROOT_DIR/packaging/macos/entitlements.plist"

rm -rf "$ARTIFACTS_DIR"
mkdir -p "$PUBLISH_DIR" "$APP_MACOS_DIR" "$APP_RESOURCES_DIR"

bash "$ROOT_DIR/scripts/build-macos-native.sh"

dotnet publish "$APP_PROJECT" \
  -c Release \
  -r "$RUNTIME_IDENTIFIER" \
  --self-contained true \
  -o "$PUBLISH_DIR"

cp -R "$PUBLISH_DIR"/. "$APP_MACOS_DIR/"

if [[ -f "$ROOT_DIR/native/macos/ScreenCaptureKitBridge/build/libScreenCaptureKitBridge.dylib" ]]; then
  mkdir -p "$APP_RESOURCES_DIR/native"
  cp "$ROOT_DIR/native/macos/ScreenCaptureKitBridge/build/libScreenCaptureKitBridge.dylib" "$APP_RESOURCES_DIR/native/libScreenCaptureKitBridge.dylib"
fi

sed \
  -e "s|__APP_DISPLAY_NAME__|$APP_DISPLAY_NAME|g" \
  -e "s|__APP_EXECUTABLE__|$APP_EXECUTABLE|g" \
  -e "s|__BUNDLE_IDENTIFIER__|$BUNDLE_IDENTIFIER|g" \
  -e "s|__APP_VERSION__|$APP_VERSION|g" \
  "$PLIST_TEMPLATE" > "$PLIST_PATH"

printf 'Created macOS app bundle:\n- publish: %s\n- app: %s\n- entitlements template: %s\n' "$PUBLISH_DIR" "$APP_BUNDLE_DIR" "$ENTITLEMENTS_PATH"
