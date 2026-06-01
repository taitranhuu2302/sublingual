import { ipcMain, BrowserWindow } from "electron";
import { getModelManager } from "../models/model-manager";
import { getSettings } from "../settings/settings-store";
import { startVosk, stopVosk, isVoskRunning } from "../asr/vosk-process";
import { getSessionStorage } from "../sessions/session-storage";
import { getOverlayManager } from "../overlay/overlay-window";
import { getTranslationService } from "../translation/translation-service";

function isSentenceComplete(text: string): boolean {
  const trimmed = text.trim();
  if (!trimmed) return false;
  return /[.!?…]$/.test(trimmed) ||
    /[.!?]["'\u201D\u201C\u2018\u2019]$/.test(trimmed);
}

export function registerAsrHandlers(mainWindow: BrowserWindow) {
  let segmentCounter = 0;
  let pendingText = "";
  let pendingLineId = "";
  let flushTimer: ReturnType<typeof setTimeout> | null = null;
  const FLUSH_TIMEOUT_MS = 3000;

  const originalSend = mainWindow.webContents.send.bind(mainWindow.webContents);

  const flushPending = () => {
    if (flushTimer) {
      clearTimeout(flushTimer);
      flushTimer = null;
    }
    if (!pendingText) return;

    const line = {
      id: pendingLineId,
      text: pendingText.trim(),
      isFinal: true,
      timestamp: Date.now(),
    };

    pendingText = "";
    pendingLineId = "";

    getSessionStorage().appendLine(line);

    const overlay = getOverlayManager();

    // Send original text to overlay immediately
    if (overlay.isVisible()) {
      overlay.sendToOverlay("overlay:transcript-line", line);
    }

    // Send translation update asynchronously
    const settings = getSettings();
    if (settings.translation.enabled) {
      const srcLang = settings.speechToText.sourceLanguage || "auto";
      const tgtLang = settings.translation.targetLanguage || "vi";
      getTranslationService()
        .translate(line.text, srcLang, tgtLang)
        .then((result) => {
          if (!mainWindow.isDestroyed()) {
            if (result.translatedText) {
              originalSend("translation:segment-result", {
                segmentId: line.id,
                translatedText: result.translatedText,
                providerName: result.providerName,
                durationMs: result.durationMs,
              });
              if (overlay.isVisible()) {
                overlay.sendToOverlay("overlay:translation-update", {
                  id: line.id,
                  translatedText: result.translatedText,
                });
              }
            }
          }
        })
        .catch((err) => {
          console.error("[translation] auto-translate failed:", err);
        });
    }
  };

  ipcMain.handle("asr:get-models", async () => getModelManager().listModels());
  ipcMain.handle("asr:select-model", async (_event, modelId: string) => {
    getModelManager().selectModel(modelId);
  });

  ipcMain.handle("asr:start-transcription", async () => {
    const mm = getModelManager();
    const model = mm.getSelectedModel();
    if (!model) throw new Error("No model selected");
    const settings = getSettings();
    segmentCounter = 0;
    pendingText = "";
    pendingLineId = "";
    if (flushTimer) {
      clearTimeout(flushTimer);
      flushTimer = null;
    }

    getSessionStorage().startSession();
    getOverlayManager().show(mainWindow);

    startVosk(model.path, mainWindow);
  });

  ipcMain.handle("asr:get-state", async () => ({
    running: isVoskRunning(),
  }));

  ipcMain.handle("asr:stop-transcription", async () => {
    flushPending();
    stopVosk();
    getSessionStorage().stopSession();
  });

  mainWindow.webContents.send = (channel: string, ...args: unknown[]) => {
    if (channel === "asr:transcript") {
      const segment = args[0] as {
        text: string;
        isFinal: boolean;
        timestamp: number;
        id?: string;
      };

      if (!segment?.text) {
        originalSend(channel, ...args);
        return;
      }

      if (segment.isFinal) {
        // Final: sentence merging + translation pipeline
        const lineId = `seg-${segmentCounter++}`;
        segment.id = lineId;

        if (pendingText) {
          pendingText = pendingText + " " + segment.text;
        } else {
          pendingText = segment.text;
          pendingLineId = lineId;
        }

        if (isSentenceComplete(pendingText)) {
          flushPending();
        } else {
          if (flushTimer) clearTimeout(flushTimer);
          flushTimer = setTimeout(() => {
            flushPending();
          }, FLUSH_TIMEOUT_MS);
        }
      } else {
        // Partial: forward directly to renderer and overlay
        const overlay = getOverlayManager();
        if (overlay.isVisible()) {
          overlay.sendToOverlay("overlay:partial-update", {
            text: segment.text,
          });
        }
      }
    }
    originalSend(channel, ...args);
  };
}
