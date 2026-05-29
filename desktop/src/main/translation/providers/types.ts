export interface TranslationRequest {
  sourceText: string;
  sourceLanguage: string;
  targetLanguage: string;
}

export interface TranslationResult {
  translatedText: string;
  providerName: string;
  durationMs: number;
}

export interface ITranslationProvider {
  readonly name: string;
  translate(request: TranslationRequest): Promise<string>;
}
