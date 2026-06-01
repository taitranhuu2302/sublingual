import { ipcMain, BrowserWindow } from "electron";
import { getAudioSources, startAudioCapture, stopAudioCapture, isAudioCapturing } from "../audio/audio-capture";

export function registerAudioHandlers(mainWindow: BrowserWindow) {
  ipcMain.handle("audio:get-sources", async () => getAudioSources());
  ipcMain.handle("audio:start-capture", async (_event, sourceId: string) => {
    startAudioCapture(sourceId, mainWindow);
  });
  ipcMain.handle("audio:stop-capture", async () => stopAudioCapture());
  ipcMain.handle("audio:get-state", async () => ({
    capturing: isAudioCapturing(),
  }));
}
