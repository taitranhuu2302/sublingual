import { BrowserWindow } from "electron";
import { initMacCapture, startMacCapture, stopMacCapture, destroyMacCapture } from "./screencapture-mac";
import { feedAudio } from "../asr/whisper-process";
import type { AudioSource } from "../../types/electron-api";

let capturing = false;
let audioDataCount = 0;

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
  audioDataCount = 0;

  if (process.platform === "darwin") {
    // Convert 48k stereo float → 16k mono s16le PCM for whisper
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

      // Convert float32 [-1.0, 1.0] to int16 [-32768, 32767] (s16le format for whisper)
      const pcmBuffer = Buffer.alloc(resampled.length * 2); // 2 bytes per sample
      for (let i = 0; i < resampled.length; i++) {
        // Clamp to [-1, 1] and convert to int16
        const sample = Math.max(-1, Math.min(1, resampled[i]));
        const int16 = Math.floor(sample * 32767);
        pcmBuffer.writeInt16LE(int16, i * 2);
      }

      // Debug logging (every 100 chunks)
      audioDataCount++;
      if (audioDataCount % 100 === 0) {
        console.log(`[audio] Processed ${audioDataCount} chunks, last chunk: ${resampled.length} samples`);
      }

      // Send to renderer (for visualization if needed)
      mainWindow.webContents.send("audio:data", resampled);
      
      // Feed PCM data to whisper (s16le format)
      feedAudio(pcmBuffer);
    }

    const ok = initMacCapture(downmixAndResample);
    if (ok) {
      console.log("[audio] Mac capture initialized, starting...");
      startMacCapture();
    } else {
      console.error("[audio] Failed to initialize Mac capture");
    }
  }
}

export function stopAudioCapture() {
  if (!capturing) return;
  capturing = false;
  console.log(`[audio] Stopping capture. Total chunks processed: ${audioDataCount}`);

  if (process.platform === "darwin") {
    stopMacCapture();
    destroyMacCapture();
  }
}
