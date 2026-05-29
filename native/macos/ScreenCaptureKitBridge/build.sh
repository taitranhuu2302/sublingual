#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUTPUT_DIR="$ROOT_DIR/build"

mkdir -p "$OUTPUT_DIR"

clang++ \
  -dynamiclib \
  -std=c++17 \
  -fobjc-arc \
  -framework Foundation \
  -framework CoreAudio \
  -framework CoreMedia \
  -framework AudioToolbox \
  -framework ScreenCaptureKit \
  "$ROOT_DIR/src/screen_capture_bridge.mm" \
  "$ROOT_DIR/src/screen_capture_session.mm" \
  "$ROOT_DIR/src/audio_buffer_adapter.mm" \
  -I"$ROOT_DIR/include" \
  -o "$OUTPUT_DIR/libScreenCaptureKitBridge.dylib"

echo "Built $OUTPUT_DIR/libScreenCaptureKitBridge.dylib"
