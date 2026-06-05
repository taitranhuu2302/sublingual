import { ipcMain, BrowserWindow } from "electron";
import { getModelManager } from "../models/model-manager";
import { getSettings } from "../settings/settings-store";
import { startVosk, stopVosk, isVoskRunning, isVoskLoading } from "../asr/vosk-process";
import { getSessionStorage } from "../sessions/session-storage";
import { getOverlayManager } from "../overlay/overlay-window";
import { IncrementalTranslationManager } from "../translation/incremental-translation-manager";
import { startSpk, stopSpk, isSpkRunning, extractSpeakerEmbedding, classifySpeaker } from "../asr/speaker-process";
import { getRingBuffer, getCaptureStartTime } from "../audio/audio-capture";

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
  let flushTimeoutMs = getSettings().speechToText.flushTimeoutMs || 3000;
  const speakerById: Map<string, { speakerId: string; speakerLabel: string; speakerColor: string }> = new Map();

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

    const speakerInfo = speakerById.get(pendingLineId);
    if (speakerInfo) {
      (line as any).speakerId = speakerInfo.speakerId;
      (line as any).speakerLabel = speakerInfo.speakerLabel;
      (line as any).speakerColor = speakerInfo.speakerColor;
      speakerById.delete(pendingLineId);
    }

    pendingText = "";
    pendingLineId = "";

    getSessionStorage().appendLine(line);

    const overlay = getOverlayManager();

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

    mainWindow.webContents.send("asr:model-status", { status: "loading" });
    try {
      await startVosk(model.path, mainWindow);
    } catch (err) {
      mainWindow.webContents.send("asr:model-status", { status: "error", message: String(err) });
      throw err;
    }
    mainWindow.webContents.send("asr:model-status", { status: "loaded" });

    const spkModel = mm.getSpkModel();
    const spkModelPath = spkModel?.path ?? settings.speechToText.speakerModel;
    const maxSpeakers = settings.speechToText.maxSpeakers ?? 4;
    if (spkModelPath) {
      startSpk(spkModelPath, maxSpeakers);
    }
  });

  ipcMain.handle("asr:get-state", async () => ({
    running: isVoskRunning(),
    loading: isVoskLoading(),
  }));

  ipcMain.handle("asr:stop-transcription", async () => {
    flushPending();
    incrementalMgr.reset();
    await stopVosk();
    stopSpk();
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
        return;
      }

      if (segment.isFinal) {
        const lineId = `seg-${segmentCounter++}`;
        segment.id = lineId;

        if (isSpkRunning()) {
          const rb = getRingBuffer();
          const startTime = getCaptureStartTime();
          if (rb && startTime > 0) {
            const endMs = Date.now() - startTime;
            const startMs = Math.max(0, endMs - 2000);
            const audioSegment = rb.extractSegment(startMs, endMs);
            const embedding = extractSpeakerEmbedding(audioSegment);
            if (embedding) {
              const speaker = classifySpeaker(embedding);
              if (speaker) {
                speakerById.set(lineId, speaker);
              }
            }
          }
        }

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
          }, flushTimeoutMs);
        }
      } else {
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

        if (overlay.isVisible() && segment.text.trim()) {
          overlay.sendToOverlay("overlay:partial-update", {
            text: segment.text,
          });
        }
      }
    }
    originalSend(channel, ...args);
  };
}
