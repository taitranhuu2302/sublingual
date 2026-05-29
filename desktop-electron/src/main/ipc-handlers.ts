import { ipcMain, BrowserWindow } from "electron";

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
    // TODO: Task 4
    return [];
  });
  ipcMain.handle("asr:select-model", async (_event, modelId: string) => {
    // TODO: Task 4
  });
  ipcMain.handle("asr:start-transcription", async () => {
    // TODO: Task 4
  });
  ipcMain.handle("asr:stop-transcription", async () => {
    // TODO: Task 4
  });

  // Settings
  ipcMain.handle("settings:get", async () => {
    // TODO: Task 5
    return { language: "en", modelId: "", audioSourceId: "" };
  });
  ipcMain.handle("settings:set", async (_event, settings: Record<string, unknown>) => {
    // TODO: Task 5
  });
}
