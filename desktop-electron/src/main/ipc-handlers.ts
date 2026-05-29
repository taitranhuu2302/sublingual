import { ipcMain, BrowserWindow } from "electron";
import { getSettings, setSettings } from "./settings/settings-store";
import { getModelManager } from "./models/model-manager";
import { startWhisper, stopWhisper } from "./asr/whisper-process";

export function registerIpcHandlers(mainWindow: BrowserWindow) {
  // Audio
  ipcMain.handle("audio:get-sources", async () => {
    // TODO: Task 3
    return [];
  });
  ipcMain.handle("audio:start-capture", async (_event, sourceId: string) => {
    // TODO: Task 3
  });
  ipcMain.handle("audio:stop-capture", async () => {
    // TODO: Task 3
  });

  // ASR
  ipcMain.handle("asr:get-models", async () => {
    return getModelManager().listModels();
  });
  ipcMain.handle("asr:select-model", async (_event, modelId: string) => {
    getModelManager().selectModel(modelId);
  });
  ipcMain.handle("asr:start-transcription", async () => {
    const mm = getModelManager();
    const model = mm.getSelectedModel();
    if (!model) throw new Error("No model selected");
    const settings = getSettings();
    startWhisper({ modelPath: model.path, language: settings.language }, mainWindow);
  });
  ipcMain.handle("asr:stop-transcription", async () => {
    stopWhisper();
  });

  // Settings
  ipcMain.handle("settings:get", async () => {
    return getSettings();
  });
  ipcMain.handle("settings:set", async (_event, settings: Record<string, unknown>) => {
    setSettings(settings);
  });
}
