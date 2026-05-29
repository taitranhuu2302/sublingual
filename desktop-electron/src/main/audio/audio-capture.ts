import { BrowserWindow } from "electron";
import { initMacCapture, startMacCapture, stopMacCapture, destroyMacCapture } from "./screencapture-mac";
import { feedAudio } from "../asr/whisper-process";
import type { AudioSource } from "../../types/electron-api";

let capturing = false;

export function getAudioSources(): AudioSource[] {
  if (process.platform === "win32") {
    // TODO: WASAPI - Task 3
    return [];
  }
  if (process.platform === "darwin") {
    // The dylib captures default system output — no device enumeration
    return [{ id: "system-default", name: "System Audio", type: "system" }];
  }
  return [];
}

export function startAudioCapture(sourceId: string, mainWindow: BrowserWindow) {
  if (capturing) return;
  capturing = true;

  if (process.platform === "darwin") {
    // Convert 48k stereo float → 16k mono float
    function downmixAndResample(samples: Float32Array, frameCount: number, channels: number, timestamp: number) {
      // Simple downmix: average channels
      let mono: Float32Array;
      if (channels === 2) {
        mono = new Float32Array(frameCount);
        for (let i = 0; i < frameCount; i++) {
          mono[i] = (samples[i * 2] + samples[i * 2 + 1]) / 2;
        }
      } else {
        mono = samples;
      }

      // Simple linear resample 48k → 16k (every 3rd sample)
      const ratio = 48000 / 16000; // 3
      const outLen = Math.floor(mono.length / ratio);
      const resampled = new Float32Array(outLen);
      for (let i = 0; i < outLen; i++) {
        resampled[i] = mono[Math.floor(i * ratio)];
      }

      // Send to renderer and feed to ASR
      mainWindow.webContents.send("audio:data", resampled);
      feedAudio(Buffer.from(resampled.buffer));
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
