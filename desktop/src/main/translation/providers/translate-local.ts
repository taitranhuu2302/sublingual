import { ITranslationProvider, TranslationRequest } from "./types";

interface LocalTranslationResponse {
  translated_text: string;
  latency_ms: number;
}

export class LocalTranslationProvider implements ITranslationProvider {
  readonly name = "TranslateServiceLocal";

  constructor(private baseUrl: string = "http://127.0.0.1:3333") {}

  async translate(request: TranslationRequest): Promise<string> {
    const url = `${this.baseUrl.replace(/\/+$/, "")}/translate`;

    const response = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        text: request.sourceText,
        source_lang: request.sourceLanguage,
        target_lang: request.targetLanguage,
      }),
    });

    if (!response.ok) {
      throw new Error(`Local translation service error: ${response.status} ${response.statusText}`);
    }

    const data: LocalTranslationResponse = await response.json();
    return data.translated_text ?? "";
  }
}
