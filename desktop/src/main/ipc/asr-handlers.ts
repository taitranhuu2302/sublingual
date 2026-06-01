import { ipcMain, BrowserWindow } from "electron";
import { getModelManager } from "../models/model-manager";
import { getSettings } from "../settings/settings-store";
import { startVosk, stopVosk, isVoskRunning } from "../asr/vosk-process";
import { getSessionStorage } from "../sessions/session-storage";
import { getOverlayManager } from "../overlay/overlay-window";
import { IncrementalTranslationManager } from "../translation/incremental-translation-manager";

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
  const incrementalMgr = new IncrementalTranslationManager("");
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

    // Finalize incremental translation (sends transcript-line via onFinalize callback)
    incrementalMgr.onFinalize = (event) => {
      if (!mainWindow.isDestroyed()) {
        originalSend("translation:segment-result", {
          segmentId: line.id,
          translatedText: event.fullTranslation,
          providerName: "incremental",
          durationMs: 0,
        });

        if (overlay.isVisible()) {
          overlay.sendToOverlay("overlay:transcript-line", {
            ...line,
            translatedText: event.fullTranslation || undefined,
          });
        }
      }
    };

    incrementalMgr.handleFinal(line.text).catch((err) => {
      console.error("[incremental] finalization failed:", err);
      if (overlay.isVisible()) {
        overlay.sendToOverlay("overlay:transcript-line", line);
      }
      incrementalMgr.reset();
    });
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
    incrementalMgr.reset();

    getSessionStorage().startSession();
    getOverlayManager().show(mainWindow);

    startVosk(model.path, mainWindow);
  });

  ipcMain.handle("asr:get-state", async () => ({
    running: isVoskRunning(),
  }));

  ipcMain.handle("asr:stop-transcription", async () => {
    flushPending();
    incrementalMgr.reset();
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
        // Partial: feed to incremental translation manager
        const overlay = getOverlayManager();

        if (!incrementalMgr.utteranceId) {
          incrementalMgr.resetUtteranceId(`utt-${Date.now()}`);
        }

        incrementalMgr.onCommit = (event) => {
          if (overlay.isVisible()) {
            overlay.sendToOverlay("overlay:translation-committed", {
              text: event.text,
            });
          }
        };

        incrementalMgr.handlePartial(segment.text);

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
