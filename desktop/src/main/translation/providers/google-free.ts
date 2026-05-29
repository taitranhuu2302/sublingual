import { ITranslationProvider, TranslationRequest } from "./types";

function isTranslationSegment(value: unknown): value is [string, ...unknown[]] {
  return Array.isArray(value) && typeof value[0] === "string";
}

export class GoogleFreeTranslationProvider implements ITranslationProvider {
  readonly name = "GoogleTranslateFreeApi";

  constructor(private endpoint: string = "https://translate.googleapis.com/translate_a/single") {}

  async translate(request: TranslationRequest): Promise<string> {
    const url = `${this.endpoint}?client=gtx&sl=${encodeURIComponent(request.sourceLanguage)}&tl=${encodeURIComponent(request.targetLanguage)}&dt=t&q=${encodeURIComponent(request.sourceText)}`;

    const response = await fetch(url);
    if (!response.ok) {
      throw new Error(`Google Translate API error: ${response.status} ${response.statusText}`);
    }

    const data: unknown = await response.json();
    if (!Array.isArray(data) || !Array.isArray(data[0])) {
      throw new Error("Unexpected Google Translate response format");
    }

    return data[0]
      .filter(isTranslationSegment)
      .map((segment) => segment[0])
      .join("");
  }
}
