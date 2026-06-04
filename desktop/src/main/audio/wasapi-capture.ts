import { SystemAudioRecorder, listAudioDevices } from "native-audio-node";

let recorder: SystemAudioRecorder | null = null;
let onAudioCallback: ((pcmBuffer: Buffer) => void) | null = null;
let isFloat32 = false;

export function initWinCapture(onAudio: (pcmBuffer: Buffer) => void): boolean {
  if (recorder) return true;

  try {
    onAudioCallback = onAudio;

    const devices = listAudioDevices();
    const outputDevices = devices.filter((d) => d.isOutput);
    console.log("[wasapi-capture] Output devices:", outputDevices.map((d) => `${d.name} (default: ${d.isDefault})`));

    recorder = new SystemAudioRecorder({
      sampleRate: 16000,
      chunkDurationMs: 100,
      stereo: false,
      emitSilence: true,
    });

    recorder.on("metadata", (meta) => {
      console.log("[wasapi-capture] metadata:", meta);
      isFloat32 = meta.isFloat;
    });

    let chunkCount = 0;
    recorder.on("data", (chunk) => {
      if (!onAudioCallback) return;

      let pcmBuffer: Buffer;

      if (isFloat32) {
        const floatSamples = new Float32Array(chunk.data.buffer, chunk.data.byteOffset, chunk.data.length / 4);
        pcmBuffer = Buffer.alloc(floatSamples.length * 2);
        for (let i = 0; i < floatSamples.length; i++) {
          const sample = Math.max(-1, Math.min(1, floatSamples[i]));
          pcmBuffer.writeInt16LE(Math.floor(sample * 32767), i * 2);
        }
      } else {
        pcmBuffer = chunk.data;
      }

      onAudioCallback(pcmBuffer);
    });

    recorder.on("error", (err) => {
      console.error("[wasapi-capture] error:", err);
    });

    recorder.on("start", () => {
      console.log("[wasapi-capture] started");
    });

    recorder.on("stop", () => {
      console.log("[wasapi-capture] stopped");
    });

    return true;
  } catch (err) {
    console.error("[wasapi-capture] initWinCapture error:", err);
    return false;
  }
}

export async function startWinCapture(): Promise<boolean> {
  if (!recorder) return false;

  try {
    await recorder.start();
    console.log("[wasapi-capture] startWinCapture ok, isActive:", recorder.isActive());
    return true;
  } catch (err) {
    console.error("[wasapi-capture] startWinCapture error:", err);
    return false;
  }
}

export async function stopWinCapture(): Promise<boolean> {
  if (!recorder) return false;

  try {
    await recorder.stop();
    return true;
  } catch (err) {
    console.error("[wasapi-capture] stopWinCapture error:", err);
    return false;
  }
}

export function destroyWinCapture(): void {
  if (recorder) {
    recorder.removeAllListeners();
    recorder = null;
  }
  onAudioCallback = null;
}
