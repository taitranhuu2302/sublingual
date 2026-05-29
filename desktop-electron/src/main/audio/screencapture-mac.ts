import ffi from "ffi-napi";
import path from "path";

const LIB_PATH = path.join(
  __dirname,
  "../../../native/screencapture-mac/libScreenCaptureKitBridge.dylib"
);

type AudioCallback = (
  samples: Buffer,
  frameCount: number,
  channels: number,
  timestamp: number,
  context: Buffer
) => void;

let sessionCreated = false;

// ffi-napi callback type: void(float*, int, int, double, void*)
const AudioCallbackPtr = ffi.Callback(
  "void",
  ["pointer", "int", "int", "double", "pointer"],
  (samples: Buffer, frameCount: number, channels: number, timestamp: number, _context: Buffer) => {
    // Reinterpret the float* pointer as Float32Array
    const floatArray = new Float32Array(samples.buffer, samples.byteOffset, frameCount * channels);
    // Forward to the registered JS handler
    (globalThis as any).__macAudioCallback?.(floatArray, frameCount, channels, timestamp);
  }
);

const lib = ffi.Library(LIB_PATH, {
  sc_create_session: ["int", ["pointer", "pointer"]],
  sc_start_capture: ["int", []],
  sc_stop_capture: ["int", []],
  sc_destroy_session: ["int", []],
  sc_get_last_error_message: ["string", []],
});

export function initMacCapture(onAudio: (samples: Float32Array, frameCount: number, channels: number, timestamp: number) => void): boolean {
  if (sessionCreated) return true;

  // Store callback globally so ffi-napi can call it
  (globalThis as any).__macAudioCallback = onAudio;

  // We pass null for context (not needed)
  const status = lib.sc_create_session(AudioCallbackPtr, ffi.NULL as any);
  if (status !== 0) {
    console.error("[screencapture-mac] sc_create_session failed:", lib.sc_get_last_error_message());
    return false;
  }
  sessionCreated = true;
  return true;
}

export function startMacCapture(): boolean {
  if (!sessionCreated) return false;
  const status = lib.sc_start_capture();
  if (status !== 0) {
    console.error("[screencapture-mac] sc_start_capture failed:", lib.sc_get_last_error_message());
    return false;
  }
  return true;
}

export function stopMacCapture(): boolean {
  const status = lib.sc_stop_capture();
  return status === 0;
}

export function destroyMacCapture(): void {
  lib.sc_destroy_session();
  sessionCreated = false;
  (globalThis as any).__macAudioCallback = undefined;
}
