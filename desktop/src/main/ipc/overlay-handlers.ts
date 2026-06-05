import { ipcMain, BrowserWindow } from "electron";
import { getOverlayManager } from "../overlay/overlay-window";

export function registerOverlayHandlers(mainWindow: BrowserWindow) {
  ipcMain.handle("overlay:show", async () => getOverlayManager().show(mainWindow));
  ipcMain.handle("overlay:hide", async () => getOverlayManager().hide());
  ipcMain.handle("overlay:toggle", async () => {
    const overlay = getOverlayManager();
    overlay.isVisible() ? overlay.hide() : overlay.show(mainWindow);
  });
  ipcMain.handle("overlay:is-visible", async () => getOverlayManager().isVisible());
  ipcMain.handle("overlay:clear", async () => {
    getOverlayManager().sendToOverlay("overlay:clear");
  });
}
