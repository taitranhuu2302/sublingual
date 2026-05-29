import koffi from "koffi";
import path from "path";

const LIB_PATH = path.join(
  __dirname,
  "../../native/screencapture-mac/libScreenCaptureKitBridge.dylib"
);

let sessionCreated = false;

// Load the library
const lib = koffi.load(LIB_PATH);

// Define callback type using koffi.types
// Signature: void callback(const float* samples, int frameCount, int channels, double timestamp, void* context)
const AudioCallbackType = koffi.types("void AudioCallback(float *, int, int, double, void *)");

// Define C function signatures
const sc_create_session = lib.func("sc_create_session", "int", [AudioCallbackType, "void *"]);
const sc_start_capture = lib.func("sc_start_capture", "int", []);
const sc_stop_capture = lib.func("sc_stop_capture", "int", []);
const sc_destroy_session = lib.func("sc_destroy_session", "int", []);
const sc_get_last_error_message = lib.func("sc_get_last_error_message", "string", []);

let registeredCallback: any = null;

export function initMacCapture(onAudio: (samples: Float32Array, frameCount: number, channels: number, timestamp: number) => void): boolean {
  if (sessionCreated) return true;

  try {
    // Create the JavaScript callback function
    const jsCallback = (samples: any, frameCount: number, channels: number, timestamp: number, _context: any) => {
      try {
        // Read the float array from memory
        const totalSamples = frameCount * channels;
        const floatArray = koffi.decode(samples, "float", totalSamples);
        const typedArray = new Float32Array(floatArray);
        onAudio(typedArray, frameCount, channels, timestamp);
      } catch (err) {
        console.error("[screencapture-mac] Callback error:", err);
      }
    };

    // Store the callback to keep it alive
    registeredCallback = jsCallback;

    // Create session with callback
    const status = sc_create_session(jsCallback, null);
    if (status !== 0) {
      console.error("[screencapture-mac] sc_create_session failed:", sc_get_last_error_message());
      return false;
    }
    
    sessionCreated = true;
    return true;
  } catch (err) {
    console.error("[screencapture-mac] initMacCapture error:", err);
    return false;
  }
}

export function startMacCapture(): boolean {
  if (!sessionCreated) return false;
  
  const status = sc_start_capture();
  if (status !== 0) {
    console.error("[screencapture-mac] sc_start_capture failed:", sc_get_last_error_message());
    return false;
  }
  return true;
}

export function stopMacCapture(): boolean {
  const status = sc_stop_capture();
  return status === 0;
}

export function destroyMacCapture(): void {
  sc_destroy_session();
  sessionCreated = false;
  registeredCallback = null;
}
