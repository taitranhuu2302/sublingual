import { getSettings } from "../settings/settings-store";
import { GoogleFreeTranslationProvider } from "./providers/google-free";
import { LocalTranslationProvider } from "./providers/translate-local";
import type { ITranslationProvider, TranslationResult } from "./providers/types";

export class TranslationService {
  private getProvider(): ITranslationProvider {
    const settings = getSettings().translation;
    if (settings.provider === "translate-local") {
      return new LocalTranslationProvider(settings.local.baseUrl);
    }

    return new GoogleFreeTranslationProvider(settings.google.endpoint);
  }

  async translate(sourceText: string, sourceLanguage: string, targetLanguage: string): Promise<TranslationResult> {
    if (!sourceText.trim()) {
      return { translatedText: "", providerName: "none", durationMs: 0 };
    }

    if (sourceLanguage === targetLanguage) {
      return { translatedText: "", providerName: "skipped", durationMs: 0 };
    }

    const provider = this.getProvider();
    const start = Date.now();
    const translatedText = await provider.translate({ sourceText, sourceLanguage, targetLanguage });
    const durationMs = Date.now() - start;

    return { translatedText, providerName: provider.name, durationMs };
  }
}

let instance: TranslationService | null = null;

export function getTranslationService(): TranslationService {
  if (!instance) instance = new TranslationService();
  return instance;
}
