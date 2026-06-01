export interface StablePrefixTrackerConfig {
  stablePartialCount: number; // how many consecutive partials must agree (default 2)
  holdBackWords: number;      // words to keep at end as "may change" (default 2)
  minChunkWords: number;      // minimum stable words before committing (default 3)
  maxWaitMs: number;          // force commit if enough time passed (default 600)
}

export interface ProcessPartialResult {
  words: string[];
  committedWordCount: number;
  isReady: boolean;
}

const DEFAULTS: StablePrefixTrackerConfig = {
  stablePartialCount: 2,
  holdBackWords: 2,
  minChunkWords: 3,
  maxWaitMs: 600,
};

export class StablePrefixTracker {
  private history: string[][] = [];
  private config: StablePrefixTrackerConfig;
  private lastCommitAt: number;

  constructor(config: Partial<StablePrefixTrackerConfig> = {}) {
    this.config = { ...DEFAULTS, ...config };
    this.lastCommitAt = 0;
  }

  processPartial(text: string, now?: number): ProcessPartialResult {
    const trimmed = text.trim();
    const words = trimmed ? trimmed.split(/\s+/) : [];
    const effectiveNow = now ?? Date.now();

    if (words.length === 0) {
      return { words: [], committedWordCount: 0, isReady: false };
    }

    this.history.push(words);

    // Keep only the last stablePartialCount entries
    while (this.history.length > this.config.stablePartialCount) {
      this.history.shift();
    }

    // Not enough history yet
    if (this.history.length < this.config.stablePartialCount) {
      return { words, committedWordCount: 0, isReady: false };
    }

    // Find longest common prefix across all history entries
    const prefixLength = this.findLongestCommonPrefixLength();

    // Subtract holdBackWords
    const committedWordCount = Math.max(0, prefixLength - this.config.holdBackWords);

    // isReady if we have enough stable words OR enough time has passed since last commit
    const hasTimedOut = this.lastCommitAt > 0 && (effectiveNow - this.lastCommitAt) >= this.config.maxWaitMs;
    const isReady = committedWordCount >= this.config.minChunkWords || hasTimedOut;

    return { words, committedWordCount, isReady };
  }

  reset(): void {
    this.history = [];
    this.lastCommitAt = 0;
  }

  markCommitted(now?: number): void {
    this.lastCommitAt = now ?? Date.now();
  }

  private findLongestCommonPrefixLength(): number {
    if (this.history.length === 0) return 0;

    const first = this.history[0];
    for (let i = 0; i < first.length; i++) {
      const word = first[i];
      for (let j = 1; j < this.history.length; j++) {
        if (i >= this.history[j].length || this.history[j][i] !== word) {
          return i;
        }
      }
    }
    return first.length;
  }
}
