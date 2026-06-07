import { ipcMain } from "electron";
import { getTranslationService } from "../translation/translation-service";
import { getTranslateStatus, restartTranslate } from "../translation/translate-process";

export function registerTranslationHandlers() {
  ipcMain.handle(
    "translation:translate",
    async (_event, sourceText: string, sourceLang: string, targetLang: string) => {
      return getTranslationService().translate(sourceText, sourceLang, targetLang);
    },
  );

  ipcMain.handle("translate:get-status", async () => {
    return getTranslateStatus();
  });

  ipcMain.handle("translate:restart", async () => {
    await restartTranslate();
  });
}
