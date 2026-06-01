import { BrowserWindow } from "electron";
import * as vosk from "vosk";

let model: vosk.Model | null = null;
let recognizer: vosk.Recognizer | null = null;
let mainWindowRef: BrowserWindow | null = null;

vosk.setLogLevel(-1);

export function startVosk(modelPath: string, mainWindow: BrowserWindow) {
  mainWindowRef = mainWindow;

  try {
    model = new vosk.Model(modelPath);
    recognizer = new vosk.Recognizer({ model, sampleRate: 16000 });
  } catch (err) {
    console.error("[vosk] Failed to initialize:", err);
    throw err;
  }
}

export function feedAudio(pcmData: Buffer) {
  if (!recognizer || !mainWindowRef || mainWindowRef.isDestroyed()) return;

  try {
    const isFinal = recognizer.acceptWaveform(pcmData);

    if (isFinal) {
      const result = recognizer.result();
      if (result.text && result.text.trim()) {
        mainWindowRef.webContents.send("asr:transcript", {
          text: result.text.trim(),
          isFinal: true,
          timestamp: Date.now(),
        });
      }
    } else {
      const partial = recognizer.partialResult();
      if (partial.partial && partial.partial.trim()) {
        mainWindowRef.webContents.send("asr:transcript", {
          text: partial.partial.trim(),
          isFinal: false,
          timestamp: Date.now(),
        });
      }
    }
  } catch (err) {
    console.error("[vosk] acceptWaveform error:", err);
  }
}

export function stopVosk() {
  if (recognizer) {
    try {
      const final = recognizer.finalResult();
      if (final.text && final.text.trim() && mainWindowRef && !mainWindowRef.isDestroyed()) {
        mainWindowRef.webContents.send("asr:transcript", {
          text: final.text.trim(),
          isFinal: true,
          timestamp: Date.now(),
        });
      }
    } catch (err) {
      console.error("[vosk] finalResult error:", err);
    }
    recognizer.free();
    recognizer = null;
  }

  if (model) {
    model.free();
    model = null;
  }

  mainWindowRef = null;
}
