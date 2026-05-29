import { ipcMain, BrowserWindow, dialog, shell } from "electron";
import { getSettings, setSettings, type AppSettings } from "../settings/settings-store";
import { getOverlayManager } from "../overlay/overlay-window";

export function registerSettingsHandlers(mainWindow: BrowserWindow) {
  ipcMain.handle("settings:get", async () => getSettings());
  ipcMain.handle("settings:set", async (_event, partial: Partial<AppSettings>) => {
    setSettings(partial);
    const overlay = getOverlayManager();
    if (overlay.isVisible()) {
      overlay.sendToOverlay("overlay:settings-update", getSettings().overlay);
    }
  });
  ipcMain.handle("settings:browse-directory", async (_event, title: string) => {
    const result = await dialog.showOpenDialog(mainWindow, {
      title,
      properties: ["openDirectory", "createDirectory"],
    });
    return result.canceled ? null : result.filePaths[0] ?? null;
  });
  ipcMain.handle("settings:open-directory", async (_event, dirPath: string) => {
    await shell.openPath(dirPath);
  });
}
