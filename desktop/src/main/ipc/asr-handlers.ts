import { ipcMain, BrowserWindow } from "electron";
import { getModelManager } from "../models/model-manager";
import { getSettings } from "../settings/settings-store";
import { startWhisper, stopWhisper } from "../asr/whisper-process";
import { getSessionStorage } from "../sessions/session-storage";
import { getOverlayManager } from "../overlay/overlay-window";
import { getTranslationService } from "../translation/translation-service";

export function registerAsrHandlers(mainWindow: BrowserWindow) {
  let segmentCounter = 0;

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

    getSessionStorage().startSession();
    getOverlayManager().show(mainWindow);

    startWhisper(
      { modelPath: model.path, language: settings.speechToText.sourceLanguage },
      mainWindow,
    );
  });

  ipcMain.handle("asr:stop-transcription", async () => {
    stopWhisper();
    getSessionStorage().stopSession();
  });

  // Intercept transcript sends to enrich with ID, save to session, translate, and forward to overlay
  const originalSend = mainWindow.webContents.send.bind(mainWindow.webContents);
  mainWindow.webContents.send = (channel: string, ...args: unknown[]) => {
    if (channel === "asr:transcript") {
      const segment = args[0] as {
        text: string;
        isFinal: boolean;
        timestamp: number;
        id?: string;
      };
      if (segment?.text) {
        const lineId = `seg-${segmentCounter++}`;
        segment.id = lineId;

        const line = {
          id: lineId,
          text: segment.text,
          isFinal: segment.isFinal,
          timestamp: segment.timestamp,
        };

        if (segment.isFinal) {
          getSessionStorage().appendLine(line);
        }

        const overlay = getOverlayManager();

        if (!segment.isFinal) {
          if (overlay.isVisible()) {
            overlay.sendToOverlay("overlay:partial-update", { text: segment.text });
          }
        }

        if (segment.isFinal) {
          const settings = getSettings();
          if (settings.translation.enabled) {
            const srcLang = settings.speechToText.sourceLanguage || "auto";
            const tgtLang = settings.translation.targetLanguage || "vi";
            getTranslationService()
              .translate(segment.text, srcLang, tgtLang)
              .then((result) => {
                if (!mainWindow.isDestroyed()) {
                  if (result.translatedText) {
                    originalSend("translation:segment-result", {
                      segmentId: lineId,
                      translatedText: result.translatedText,
                      providerName: result.providerName,
                      durationMs: result.durationMs,
                    });
                  }
                  if (overlay.isVisible()) {
                    overlay.sendToOverlay("overlay:transcript-line", {
                      ...line,
                      translatedText: result.translatedText || undefined,
                    });
                  }
                }
              })
              .catch((err) => {
                console.error("[translation] auto-translate failed:", err);
                if (overlay.isVisible()) {
                  overlay.sendToOverlay("overlay:transcript-line", line);
                }
              });
          } else {
            if (overlay.isVisible()) {
              overlay.sendToOverlay("overlay:transcript-line", line);
            }
          }
        }
      }
    }
    originalSend(channel, ...args);
  };
}
