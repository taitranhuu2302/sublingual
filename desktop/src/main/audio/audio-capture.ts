import { BrowserWindow } from "electron";
import { feedPcm } from "../asr/vosk-process";
import type { AudioSource } from "../../types/electron-api";

class RingBuffer {
  private buffer: Buffer;
  private writePos: number = 0;
  private totalWritten: number = 0;
  private readonly capacity: number;
  private readonly sampleRate: number;
  private readonly bytesPerSample: number = 2;

  constructor(sampleRate: number, durationMs: number) {
    this.sampleRate = sampleRate;
    const samples = Math.ceil((sampleRate * durationMs) / 1000);
    this.capacity = samples * this.bytesPerSample;
    this.buffer = Buffer.alloc(this.capacity);
  }

  write(data: Buffer): void {
    for (let i = 0; i < data.length; i++) {
      this.buffer[this.writePos] = data[i];
      this.writePos = (this.writePos + 1) % this.capacity;
    }
    this.totalWritten += data.length;
  }

  extractSegment(startMs: number, endMs: number): Buffer {
    const startByte = Math.floor((startMs / 1000) * this.sampleRate) * this.bytesPerSample;
    const endByte = Math.floor((endMs / 1000) * this.sampleRate) * this.bytesPerSample;
    const length = endByte - startByte;

    if (length <= 0) return Buffer.alloc(0);

    const result = Buffer.alloc(length);
    const totalBytes = this.totalWritten;

    for (let i = 0; i < length; i++) {
      const globalOffset = startByte + i;
      if (globalOffset < 0 || globalOffset >= totalBytes) {
        result[i] = 0;
        continue;
      }
      const wrappedOffset = (globalOffset - Math.max(0, totalBytes - this.capacity));
      if (wrappedOffset < 0) continue;
      const idx = wrappedOffset % this.capacity;
      result[i] = this.buffer[idx];
    }

    return result;
  }

  reset(): void {
    this.writePos = 0;
    this.totalWritten = 0;
    this.buffer.fill(0);
  }
}

let capturing = false;
let ringBuffer: RingBuffer | null = null;
let captureStartTime: number = 0;

export function getAudioSources(): AudioSource[] {
  if (process.platform === "win32") {
    return [{ id: "system-default", name: "System Audio", type: "system" }];
  }
  if (process.platform === "darwin") {
    return [{ id: "system-default", name: "System Audio", type: "system" }];
  }
  return [];
}

export async function startAudioCapture(sourceId: string, mainWindow: BrowserWindow) {
  if (capturing) return;
  capturing = true;
  ringBuffer = new RingBuffer(16000, 5000);
  captureStartTime = Date.now();

  if (process.platform === "win32") {
    const { initWinCapture, startWinCapture } = await import("./wasapi-capture");

    const ok = initWinCapture((pcmBuffer: Buffer) => {
      if (!capturing || mainWindow.isDestroyed()) return;
      feedPcm(pcmBuffer);
      ringBuffer?.write(pcmBuffer);
    });
    if (ok) await startWinCapture();
    return;
  }

  if (process.platform === "darwin") {
    const { initMacCapture, startMacCapture } = await import("./screencapture-mac");

    function downmixAndResample(samples: Float32Array, frameCount: number, channels: number, timestamp: number) {
      if (!capturing || mainWindow.isDestroyed()) return;

      let mono: Float32Array;
      if (channels === 2) {
        mono = new Float32Array(frameCount);
        for (let i = 0; i < frameCount; i++) {
          mono[i] = (samples[i * 2] + samples[i * 2 + 1]) / 2;
        }
      } else {
        mono = samples;
      }

      const ratio = 48000 / 16000;
      const outLen = Math.floor(mono.length / ratio);
      const resampled = new Float32Array(outLen);
      for (let i = 0; i < outLen; i++) {
        resampled[i] = mono[Math.floor(i * ratio)];
      }

      const pcmBuffer = Buffer.alloc(resampled.length * 2);
      for (let i = 0; i < resampled.length; i++) {
        const sample = Math.max(-1, Math.min(1, resampled[i]));
        const int16 = Math.floor(sample * 32767);
        pcmBuffer.writeInt16LE(int16, i * 2);
      }

      if (!mainWindow.isDestroyed()) {
        mainWindow.webContents.send("audio:data", resampled);
      }
        feedPcm(pcmBuffer);
        ringBuffer?.write(pcmBuffer);
    }

    const ok = initMacCapture(downmixAndResample);
    if (ok) startMacCapture();
  }
}

export function isAudioCapturing(): boolean {
  return capturing;
}

export function getRingBuffer(): RingBuffer | null {
  return ringBuffer;
}

export function getCaptureStartTime(): number {
  return captureStartTime;
}

export async function stopAudioCapture() {
  if (!capturing) return;
  capturing = false;

  if (process.platform === "win32") {
    const { stopWinCapture, destroyWinCapture } = await import("./wasapi-capture");
    await stopWinCapture();
    destroyWinCapture();
    return;
  }

  if (process.platform === "darwin") {
    const { stopMacCapture, destroyMacCapture } = await import("./screencapture-mac");
    stopMacCapture();
    destroyMacCapture();
  }
  ringBuffer?.reset();
  ringBuffer = null;
  captureStartTime = 0;
}
