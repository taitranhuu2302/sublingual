import { BrowserWindow } from "electron";
import { VoskConfig } from "./vosk-types";

let mainWindowRef: BrowserWindow | null = null;
let configRef: VoskConfig | null = null;
let model: unknown = null;
let recognizer: unknown = null;
let partialPollTimer: ReturnType<typeof setInterval> | null = null;

const SAMPLE_RATE = 16000;

export function initVosk(config: VoskConfig, mainWindow: BrowserWindow) {
  mainWindowRef = mainWindow;
  configRef = config;

  try {
    const vosk = require("vosk");
    model = new vosk.Model(config.modelPath);
    recognizer = new vosk.Recognizer({ model: model as never, sampleRate: SAMPLE_RATE });

    partialPollTimer = setInterval(pollPartialResult, 100);

    if (!mainWindow.isDestroyed()) {
      mainWindow.webContents.send("asr:status", { status: "ready" });
    }
  } catch (err) {
    console.error("[vosk] initVosk error:", err);
    if (!mainWindow.isDestroyed()) {
      mainWindow.webContents.send("asr:status", { status: "error", error: String(err) });
    }
  }
}

function pollPartialResult() {
  if (!recognizer || !mainWindowRef || mainWindowRef.isDestroyed()) return;
  try {
    const rec = recognizer as { partialResult(): { partial: string } };
    const partial = rec.partialResult();
    if (partial.partial && partial.partial.length > 0) {
      mainWindowRef.webContents.send("asr:partial-result", { text: partial.partial });
    }
  } catch {
    // recognizer may not be ready yet
  }
}

export function feedPcm(pcmBuffer: Buffer) {
  if (!recognizer || !mainWindowRef || mainWindowRef.isDestroyed()) return;
  try {
    const rec = recognizer as { acceptWaveform(buf: Buffer): boolean; result(): { text: string } };
    const isFinal = rec.acceptWaveform(pcmBuffer);
    if (isFinal) {
      const { text } = rec.result();
      if (text && text.trim().length > 0) {
        mainWindowRef.webContents.send("asr:transcript", {
          text: text.trim(),
          isFinal: true,
          timestamp: Date.now(),
        });
      }
    }
  } catch (err) {
    console.error("[vosk] feedPcm error:", err);
  }
}

export function stopVosk() {
  if (partialPollTimer) {
    clearInterval(partialPollTimer);
    partialPollTimer = null;
  }
  try {
    if (recognizer && typeof (recognizer as { free(): void }).free === "function") {
      (recognizer as { free(): void }).free();
    }
  } catch {}
  try {
    if (model && typeof (model as { free(): void }).free === "function") {
      (model as { free(): void }).free();
    }
  } catch {}
  recognizer = null;
  model = null;
  mainWindowRef = null;
  configRef = null;
}

export function isVoskRunning(): boolean {
  return recognizer !== null && partialPollTimer !== null;
}

export function updateVoskConfig(config: VoskConfig) {
  if (!configRef || !mainWindowRef) return;
  const wasRunning = isVoskRunning();
  if (wasRunning) {
    stopVosk();
  }
  configRef = config;
  if (wasRunning && mainWindowRef) {
    initVosk(config, mainWindowRef);
  }
}
