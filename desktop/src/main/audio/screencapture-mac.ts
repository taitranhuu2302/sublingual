import koffi from "koffi";
import path from "path";

const LIB_PATH = path.join(
  __dirname,
  "../../native/screencapture-mac/libScreenCaptureKitBridge.dylib"
);

let sessionCreated = false;
let pollingInterval: ReturnType<typeof setInterval> | null = null;

let lib: any = null;

// Define C function signatures - using polling-based API (no callbacks)
let sc_create_session_polling: (...args: any[]) => any = () => -1;
let sc_start_capture: (...args: any[]) => any = () => -1;
let sc_stop_capture: (...args: any[]) => any = () => -1;
let sc_destroy_session: (...args: any[]) => any = () => -1;
let sc_get_last_error_message: (...args: any[]) => any = () => "Not supported on this platform";
let sc_read_audio: (...args: any[]) => any = () => -1;
let sc_get_buffer_frames_available: (...args: any[]) => any = () => 0;

if (process.platform === "darwin") {
  lib = koffi.load(LIB_PATH);

  sc_create_session_polling = lib.func("sc_create_session_polling", "int", []);
  sc_start_capture = lib.func("sc_start_capture", "int", []);
  sc_stop_capture = lib.func("sc_stop_capture", "int", []);
  sc_destroy_session = lib.func("sc_destroy_session", "int", []);
  sc_get_last_error_message = lib.func("sc_get_last_error_message", "string", []);
  sc_read_audio = lib.func("sc_read_audio", "int", ["void *", "int", "void *", "void *", "void *"]);
  sc_get_buffer_frames_available = lib.func("sc_get_buffer_frames_available", "int", []);
}

// Status codes
const SC_STATUS_OK = 0;
const SC_STATUS_NO_DATA = 6;

let onAudioCallback: ((samples: Float32Array, frameCount: number, channels: number, timestamp: number) => void) | null = null;

export function initMacCapture(onAudio: (samples: Float32Array, frameCount: number, channels: number, timestamp: number) => void): boolean {
  if (sessionCreated) return true;

  try {
    // Store callback for later use
    onAudioCallback = onAudio;
    
    // Create session in polling mode (no callback to C)
    const status = sc_create_session_polling();
    if (status !== SC_STATUS_OK) {
      console.error("[screencapture-mac] sc_create_session_polling failed:", sc_get_last_error_message());
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
  if (status !== SC_STATUS_OK) {
    console.error("[screencapture-mac] sc_start_capture failed:", sc_get_last_error_message());
    return false;
  }
  
  // Start polling for audio data
  const maxFrames = 4800; // 100ms at 48kHz
  const maxSamples = maxFrames * 2; // stereo
  const sampleBuffer = Buffer.alloc(maxSamples * 4); // 4 bytes per float
  const frameCountBuffer = Buffer.alloc(4);
  const channelsBuffer = Buffer.alloc(4);
  const timestampBuffer = Buffer.alloc(8);
  
  pollingInterval = setInterval(() => {
    const framesAvailable = sc_get_buffer_frames_available();
    if (framesAvailable <= 0) return;
    
    const readStatus = sc_read_audio(
      sampleBuffer,
      maxFrames,
      frameCountBuffer,
      channelsBuffer,
      timestampBuffer
    );
    
    if (readStatus === SC_STATUS_OK) {
      const frameCount = frameCountBuffer.readInt32LE(0);
      const channels = channelsBuffer.readInt32LE(0);
      const timestamp = timestampBuffer.readDoubleLE(0);
      
      if (frameCount > 0 && onAudioCallback) {
        // Copy float data to Float32Array
        const totalSamples = frameCount * channels;
        const samples = new Float32Array(totalSamples);
        for (let i = 0; i < totalSamples; i++) {
          samples[i] = sampleBuffer.readFloatLE(i * 4);
        }
        
        onAudioCallback(samples, frameCount, channels, timestamp);
      }
    }
  }, 50); // Poll every 50ms
  
  return true;
}

export function stopMacCapture(): boolean {
  if (pollingInterval) {
    clearInterval(pollingInterval);
    pollingInterval = null;
  }
  
  const status = sc_stop_capture();
  return status === SC_STATUS_OK;
}

export function destroyMacCapture(): void {
  if (pollingInterval) {
    clearInterval(pollingInterval);
    pollingInterval = null;
  }
  
  sc_destroy_session();
  sessionCreated = false;
  onAudioCallback = null;
}

