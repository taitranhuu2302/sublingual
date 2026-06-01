import { BrowserWindow } from "electron";
import { feedPcm } from "../asr/vosk-process";
import type { AudioSource } from "../../types/electron-api";

let capturing = false;

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

  if (process.platform === "win32") {
    const { initWinCapture, startWinCapture } = await import("./wasapi-capture");

    const ok = initWinCapture((pcmBuffer: Buffer) => {
      if (!capturing || mainWindow.isDestroyed()) return;
      feedPcm(pcmBuffer);
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
    }

    const ok = initMacCapture(downmixAndResample);
    if (ok) startMacCapture();
  }
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
}
