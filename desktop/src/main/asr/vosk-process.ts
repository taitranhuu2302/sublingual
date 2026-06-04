import { BrowserWindow } from "electron";
import * as bindings from "./vosk-bindings";

let model: unknown = null;
let recognizer: unknown = null;
let mainWindowRef: BrowserWindow | null = null;

bindings.setLogLevel(-1);

export function startVosk(modelPath: string, mainWindow: BrowserWindow) {
  mainWindowRef = mainWindow;

  try {
    model = bindings.modelNew(modelPath);
    recognizer = bindings.recognizerNew(model, 16000);
    bindings.recognizerSetWords(recognizer, true);
    bindings.recognizerSetPartialWords(recognizer, true);
  } catch (err) {
    console.error("[vosk] Failed to initialize:", err);
    throw err;
  }
}

export function isVoskRunning(): boolean {
  return recognizer !== null;
}

export function feedAudio(pcmData: Buffer) {
  if (!recognizer || !mainWindowRef || mainWindowRef.isDestroyed()) return;

  try {
    const isFinal = bindings.acceptWaveform(recognizer, pcmData);

    if (isFinal) {
      const raw = bindings.getResult(recognizer);
      const parsed = tryParseJson(raw);
      if (parsed?.text?.trim()) {
        mainWindowRef.webContents.send("asr:transcript", {
          text: parsed.text.trim(),
          isFinal: true,
          timestamp: Date.now(),
        });
      }
    } else {
      const raw = bindings.getPartialResult(recognizer);
      const parsed = tryParseJson(raw);
      if (parsed?.partial?.trim()) {
        mainWindowRef.webContents.send("asr:transcript", {
          text: parsed.partial.trim(),
          isFinal: false,
          timestamp: Date.now(),
        });
      }
    }
  } catch (err) {
    console.error("[vosk] acceptWaveform error:", err);
  }
}

export const feedPcm = feedAudio;

export function stopVosk() {
  if (recognizer) {
    try {
      const raw = bindings.getFinalResult(recognizer);
      const parsed = tryParseJson(raw);
      if (parsed?.text?.trim() && mainWindowRef && !mainWindowRef.isDestroyed()) {
        mainWindowRef.webContents.send("asr:transcript", {
          text: parsed.text.trim(),
          isFinal: true,
          timestamp: Date.now(),
        });
      }
    } catch (err) {
      console.error("[vosk] finalResult error:", err);
    }
    bindings.recognizerFree(recognizer);
    recognizer = null;
  }

  if (model) {
    bindings.modelFree(model);
    model = null;
  }

  mainWindowRef = null;
}

function tryParseJson(s: string): Record<string, unknown> | null {
  if (!s) return null;
  try {
    return JSON.parse(s);
  } catch {
    return null;
  }
}
