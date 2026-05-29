import { ipcMain } from "electron";
import { getTranslationService } from "../translation/translation-service";

export function registerTranslationHandlers() {
  ipcMain.handle(
    "translation:translate",
    async (_event, sourceText: string, sourceLang: string, targetLang: string) => {
      return getTranslationService().translate(sourceText, sourceLang, targetLang);
    },
  );
}
