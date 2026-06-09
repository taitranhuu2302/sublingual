import { ipcMain } from "electron";
import { getTranslationService } from "../translation/translation-service";
import { getTranslateStatus, restartTranslate, downloadTranslateModel, pollHealthNow } from "../translation/translate-process";

export function registerTranslationHandlers() {
  ipcMain.handle(
    "translation:translate",
    async (_event, sourceText: string, sourceLang: string, targetLang: string) => {
      return getTranslationService().translate(sourceText, sourceLang, targetLang);
    },
  );

  ipcMain.handle("translate:get-status", async () => {
    pollHealthNow();
    return getTranslateStatus();
  });

  ipcMain.handle("translate:restart", async () => {
    await restartTranslate();
  });

  ipcMain.handle("translate:download-model", async () => {
    await downloadTranslateModel();
  });
}
