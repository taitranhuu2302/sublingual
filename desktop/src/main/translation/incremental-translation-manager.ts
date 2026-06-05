import { StablePrefixTracker } from "./stable-prefix-tracker";
import { getTranslationService } from "./translation-service";
import { getSettings } from "../settings/settings-store";

export interface CommittedChunkEvent {
  text: string;
  fullSource: string;
  revision: number;
}

export interface FinalizedEvent {
  fullSource: string;
  fullTranslation: string;
  revision: number;
}

export type CommitCallback = (event: CommittedChunkEvent) => void;
export type FinalizeCallback = (event: FinalizedEvent) => void;

export class IncrementalTranslationManager {
  private tracker = new StablePrefixTracker();
  private revision = 0;
  private finalGeneration = 0;
  private committedWordCount = 0;
  private committedTranslation = "";
  utteranceId: string;

  onCommit: CommitCallback | null = null;
  onFinalize: FinalizeCallback | null = null;

  constructor(utteranceId: string) {
    this.utteranceId = utteranceId;
  }

  handlePartial(text: string): void {
    const settings = getSettings();

    const result = this.tracker.processPartial(text);

    if (!result.isReady) return;
    if (result.committedWordCount <= this.committedWordCount) return;

    const newChunk = result.words.slice(this.committedWordCount, result.committedWordCount).join(" ");
    if (!newChunk) return;

    const currentRevision = ++this.revision;
    this.tracker.markCommitted();
    this.committedWordCount = result.committedWordCount;

    if (!settings.translation.enabled) return;

    const srcLang = settings.speechToText.sourceLanguage || "auto";
    const tgtLang = settings.translation.targetLanguage || "vi";

    getTranslationService()
      .translate(newChunk, srcLang, tgtLang, { previousTarget: this.committedTranslation })
      .then((translationResult) => {
        if (currentRevision < this.revision) return;
        this.committedTranslation += (this.committedTranslation ? " " : "") + translationResult.translatedText;
        this.onCommit?.({
          text: this.committedTranslation,
          fullSource: text,
          revision: currentRevision,
        });
      })
      .catch((err) => {
        console.error("[IncrementalTranslationManager] Translation error:", err);
      });
  }

  async handleFinal(text: string): Promise<void> {
    const settings = getSettings();

    const myGen = ++this.finalGeneration;
    ++this.revision;

    const words = text.trim() ? text.trim().split(/\s+/) : [];
    const remainingWords = words.slice(this.committedWordCount);
    const remaining = remainingWords.join(" ");

    let fullTranslation = this.committedTranslation;

    if (settings.translation.enabled && remaining.trim()) {
      const srcLang = settings.speechToText.sourceLanguage || "auto";
      const tgtLang = settings.translation.targetLanguage || "vi";

      try {
        const result = await getTranslationService().translate(remaining, srcLang, tgtLang);
        if (myGen === this.finalGeneration) {
          fullTranslation = this.committedTranslation + (this.committedTranslation ? " " : "") + result.translatedText;
        }
      } catch (err) {
        console.error("[IncrementalTranslationManager] Final translation error:", err);
      }
    }

    this.onFinalize?.({
      fullSource: text,
      fullTranslation,
      revision: myGen,
    });

    this.reset();
  }

  resetUtteranceId(id: string): void {
    this.utteranceId = id;
  }

  reset(): void {
    this.tracker.reset();
    this.committedWordCount = 0;
    this.committedTranslation = "";
    this.revision = 0;
    this.finalGeneration = 0;
  }
}
