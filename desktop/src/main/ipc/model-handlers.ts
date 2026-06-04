import { ipcMain, BrowserWindow, shell } from "electron";
import { getInstallableModels, getModelsDir } from "../models/model-source-catalog";
import { downloadModel, cancelDownload } from "../models/model-downloader";
import { getModelManager } from "../models/model-manager";

export function registerModelHandlers(mainWindow: BrowserWindow) {
  ipcMain.handle("models:get-installable", async () => getInstallableModels());
  ipcMain.handle("models:download", async (_event, modelId: string) => {
    await downloadModel(modelId, mainWindow);
  });
  ipcMain.handle("models:cancel-download", async () => cancelDownload());
  ipcMain.handle("models:remove", async (_event, modelId: string) => {
    getModelManager().removeModel(modelId);
  });
  ipcMain.handle("models:open-folder", async () => {
    await shell.openPath(getModelsDir());
  });
}
