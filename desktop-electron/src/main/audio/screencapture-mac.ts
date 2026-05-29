import koffi from "koffi";
import path from "path";

const LIB_PATH = path.join(
  __dirname,
  "../../native/screencapture-mac/libScreenCaptureKitBridge.dylib"
);

let sessionCreated = false;

// Load the library
const lib = koffi.load(LIB_PATH);

// Define callback type: void(float*, int, int, double, void*)
const AudioCallbackType = koffi.proto("void AudioCallbackProto(_Out_ float *samples, int frameCount, int channels, double timestamp, _Out_ void *context)");

let callbackPtr: any = null;

export function initMacCapture(onAudio: (samples: Float32Array, frameCount: number, channels: number, timestamp: number) => void): boolean {
  if (sessionCreated) return true;

  // Create callback
  const callback = koffi.register((samples: koffi.IKoffiCType, frameCount: number, channels: number, timestamp: number, _context: koffi.IKoffiCType) => {
    try {
      // Read the float array from memory  
      const totalSamples = frameCount * channels;
      const floatArray = koffi.decode(samples, koffi.types.float, totalSamples);
      const typedArray = new Float32Array(floatArray);
      onAudio(typedArray, frameCount, channels, timestamp);
    } catch (err) {
      console.error("[screencapture-mac] Callback error:", err);
    }
  }, AudioCallbackType);

  callbackPtr = callback;

  // Define C function signatures
  const sc_create_session = lib.func("sc_create_session", "int", [koffi.pointer(AudioCallbackType), "void *"]);
  const sc_get_last_error_message = lib.func("sc_get_last_error_message", "string", []);

  // Create session with callback
  const status = sc_create_session(callback, null);
  if (status !== 0) {
    console.error("[screencapture-mac] sc_create_session failed:", sc_get_last_error_message());
    return false;
  }
  sessionCreated = true;
  return true;
}

export function startMacCapture(): boolean {
  if (!sessionCreated) return false;
  const sc_start_capture = lib.func("sc_start_capture", "int", []);
  const sc_get_last_error_message = lib.func("sc_get_last_error_message", "string", []);
  
  const status = sc_start_capture();
  if (status !== 0) {
    console.error("[screencapture-mac] sc_start_capture failed:", sc_get_last_error_message());
    return false;
  }
  return true;
}

export function stopMacCapture(): boolean {
  const sc_stop_capture = lib.func("sc_stop_capture", "int", []);
  const status = sc_stop_capture();
  return status === 0;
}

export function destroyMacCapture(): void {
  const sc_destroy_session = lib.func("sc_destroy_session", "int", []);
  sc_destroy_session();
  sessionCreated = false;
  if (callbackPtr) {
    koffi.unregister(callbackPtr);
    callbackPtr = null;
  }
}
