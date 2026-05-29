import { BrowserWindow } from "electron";
import { initMacCapture, startMacCapture, stopMacCapture, destroyMacCapture } from "./screencapture-mac";
import { feedAudio } from "../asr/whisper-process";
import type { AudioSource } from "../../types/electron-api";

let capturing = false;

export function getAudioSources(): AudioSource[] {
  if (process.platform === "win32") {
    return [];
  }
  if (process.platform === "darwin") {
    return [{ id: "system-default", name: "System Audio", type: "system" }];
  }
  return [];
}

export function startAudioCapture(sourceId: string, mainWindow: BrowserWindow) {
  if (capturing) return;
  capturing = true;

  if (process.platform === "darwin") {
    function downmixAndResample(samples: Float32Array, frameCount: number, channels: number, timestamp: number) {
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

      mainWindow.webContents.send("audio:data", resampled);
      feedAudio(pcmBuffer);
    }

    const ok = initMacCapture(downmixAndResample);
    if (ok) startMacCapture();
  }
}

export function stopAudioCapture() {
  if (!capturing) return;
  capturing = false;

  if (process.platform === "darwin") {
    stopMacCapture();
    destroyMacCapture();
  }
}
