# Incremental Translation with Stable Prefix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace end-of-segment-only batch translation with incremental translation using stable-prefix detection, reducing perceived latency from ~3s to <1s for live captions.

**Architecture:** A `StablePrefixTracker` detects which parts of partial ASR output are stable across successive Vosk partials. An `IncrementalTranslationManager` sends only stable word chunks for translation, tracks committed state per utterance, and reconciles the final segment at sentence flush. The overlay displays committed translation alongside source partials.

**Tech Stack:** TypeScript, Electron IPC, Vosk streaming ASR, existing TranslationService (Google/Local)

---

## File Structure

```
NEW:
desktop/src/main/translation/stable-prefix-tracker.ts
desktop/src/main/translation/incremental-translation-manager.ts

MODIFY:
desktop/src/types/electron-api.d.ts              — new incremental translation types
desktop/src/main/ipc/asr-handlers.ts             — wire incremental translation for partials
desktop/src/overlay/overlay-preload.ts           — add overlay IPC bridge for committed
desktop/src/overlay/OverlayApp.tsx               — show committed translation on partials
```

---

### Task 1: Create StablePrefixTracker

**Files:**
- Create: `desktop/src/main/translation/stable-prefix-tracker.ts`

Pure logic class. No Electron dependencies, no side effects. Given a stream of partial text strings, identifies the longest common prefix across recent partials and determines how many words are "stable" (safe to translate).

- [ ] **Step 1: Create StablePrefixTracker**

```typescript
export interface StablePrefixTrackerConfig {
  stablePartialCount: number;  // how many consecutive partials must agree (default 2)
  holdBackWords: number;       // words to keep at end as "may change" (default 2)
  minChunkWords: number;       // minimum stable words before committing (default 3)
  maxWaitMs: number;           // force commit if enough time passed (default 600)
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
  private lastCommitAt = 0;

  constructor(config: Partial<StablePrefixTrackerConfig> = {}) {
    this.config = { ...DEFAULTS, ...config };
  }

  processPartial(text: string, now = Date.now()): ProcessPartialResult {
    const words = text.trim().split(/\s+/).filter(Boolean);
    if (words.length === 0) {
      return { words: [], committedWordCount: this.lastCommitAt > 0 ? 0 : 0, isReady: false };
    }

    this.history.push(words);
    if (this.history.length > this.config.stablePartialCount) {
      this.history.shift();
    }

    // Need minimum history to detect stability
    if (this.history.length < this.config.stablePartialCount) {
      return { words, committedWordCount: 0, isReady: false };
    }

    const stable = this.commonPrefix();
    const stableEnd = Math.max(0, stable.length - this.config.holdBackWords);

    // Check if we have enough new stable words to commit
    const hasMinWords = stableEnd >= this.config.minChunkWords;
    const hasTimedOut = now - this.lastCommitAt >= this.config.maxWaitMs;

    return {
      words,
      committedWordCount: stableEnd,
      isReady: hasMinWords || hasTimedOut,
    };
  }

  private commonPrefix(): string[] {
    const first = this.history[0];
    if (!first) return [];

    const result: string[] = [];
    for (let i = 0; i < first.length; i++) {
      if (this.history.every((arr) => arr[i] === first[i])) {
        result.push(first[i]);
      } else {
        break;
      }
    }
    return result;
  }

  reset(): void {
    this.history = [];
    this.lastCommitAt = 0;
  }

  markCommitted(now = Date.now()): void {
    this.lastCommitAt = now;
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add desktop/src/main/translation/stable-prefix-tracker.ts
git commit -m "feat: add StablePrefixTracker for detecting stable ASR text"
```

---

### Task 2: Create IncrementalTranslationManager

**Files:**
- Create: `desktop/src/main/translation/incremental-translation-manager.ts`

Orchestrates the incremental translation flow for a single utterance. Owns the `StablePrefixTracker`, manages revision IDs for race condition handling, sends translation requests to the existing `TranslationService`, and emits callbacks for committed/draft/final events.

- [ ] **Step 1: Create IncrementalTranslationManager**

```typescript
import { StablePrefixTracker } from "./stable-prefix-tracker";
import { getTranslationService } from "./translation-service";
import { getSettings } from "../settings/settings-store";

export interface CommittedChunkEvent {
  text: string;         // translated text for this chunk
  fullSource: string;   // full partial source (for context)
  revision: number;
}

export type CommitCallback = (event: CommittedChunkEvent) => void;
export type FinalizeCallback = (event: FinalizedEvent) => void;

export class IncrementalTranslationManager {
  private tracker = new StablePrefixTracker();
  private revision = 0;
  private committedWordCount = 0;
  private committedTranslation = "";    // accumulated committed translation
  utteranceId: string;

  onCommit: CommitCallback | null = null;
  onFinalize: FinalizeCallback | null = null;

  constructor(utteranceId: string) {
    this.utteranceId = utteranceId;
  }

  handlePartial(text: string): void {
    const result = this.tracker.processPartial(text);
    if (!result.isReady || result.committedWordCount <= this.committedWordCount) {
      return;
    }

    const newWords = result.words.slice(this.committedWordCount, result.committedWordCount);
    if (newWords.length === 0) return;

    const chunk = newWords.join(" ");
    const revision = ++this.revision;
    this.committedWordCount = result.committedWordCount;
    this.tracker.markCommitted();

    // Translate the new chunk
    this.translateChunk(chunk, text, revision);
  }

  private async translateChunk(chunk: string, fullSource: string, revision: number): Promise<void> {
    const settings = getSettings();
    if (!settings.translation.enabled) return;

    const srcLang = settings.speechToText.sourceLanguage || "auto";
    const tgtLang = settings.translation.targetLanguage || "vi";

    try {
      const result = await getTranslationService().translate(chunk, srcLang, tgtLang, {
        previousTarget: this.committedTranslation,
      });

      // Race: skip if a newer revision already completed
      if (revision < this.revision) return;

      const translated = result.translatedText || chunk;
      this.committedTranslation += (this.committedTranslation ? " " : "") + translated;

      this.onCommit?.({
        text: translated,
        fullSource,
        revision,
      });
    } catch (err) {
      console.error("[incremental] translate chunk failed:", err);
    }
  }

  async handleFinal(text: string): Promise<void> {
    const words = text.trim().split(/\s+/).filter(Boolean);
    const remainingWords = words.slice(this.committedWordCount);
    const revision = ++this.revision;

    if (remainingWords.length > 0) {
      const remaining = remainingWords.join(" ");
      const srcLang = getSettings().speechToText.sourceLanguage || "auto";
      const tgtLang = getSettings().translation.targetLanguage || "vi";

      try {
        const result = await getTranslationService().translate(remaining, srcLang, tgtLang);
        if (revision >= this.revision) {
          this.committedTranslation += (this.committedTranslation ? " " : "") + (result.translatedText || remaining);
        }
      } catch (err) {
        console.error("[incremental] final translate failed:", err);
      }
    }

    this.onFinalize?.({
      fullSource: text,
      fullTranslation: this.committedTranslation,
      revision,
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
    // revision intentionally NOT reset — monotonic across utterances
  }
}
```

- [ ] **Step 2: Update TranslationService.translate to accept context**

Modify `desktop/src/main/translation/translation-service.ts` to accept an optional context object. The `ITranslationProvider` interface is unchanged — context is for logging/debugging only at this layer.

```typescript
export interface TranslationContext {
  previousSource?: string;
  previousTarget?: string;
}

export class TranslationService {
  async translate(
    sourceText: string,
    sourceLanguage: string,
    targetLanguage: string,
    context?: TranslationContext,
  ): Promise<TranslationResult> {
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

    // Context is accepted but not passed to standard providers
    // Streaming providers (future) can use it for consistency

    return { translatedText, providerName: provider.name, durationMs };
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add desktop/src/main/translation/incremental-translation-manager.ts desktop/src/main/translation/translation-service.ts
git commit -m "feat: add IncrementalTranslationManager with stable-prefix translation"
```

---

### Task 3: Add Incremental Translation IPC Types

**Files:**
- Modify: `desktop/src/types/electron-api.d.ts`

Add new event types for incremental translation events flowing from main process to overlay.

- [ ] **Step 1: Add incremental translation interfaces**

```typescript
// Add after TranslationSegmentResult (line 95):

export interface IncrementalTranslationEvent {
  utteranceId: string;
  revision: number;
  text: string;
}

export interface IncrementalFinalEvent {
  utteranceId: string;
  fullSource: string;
  fullTranslation: string;
  revision: number;
}
```

- [ ] **Step 2: Add overlayAPI types for incremental events**

After line 29 (inside the `overlayAPI` declaration), add:
```typescript
onTranslationCommitted: (cb: (data: { text: string }) => void) => () => void;
```

- [ ] **Step 3: Commit**

```bash
git add desktop/src/types/electron-api.d.ts
git commit -m "feat: add incremental translation IPC types"
```

---

### Task 4: Update Overlay Preload

**Files:**
- Modify: `desktop/src/overlay/overlay-preload.ts`

Add IPC bridges for the new committed/draft translation events.

- [ ] **Step 1: Add new event listener to overlay preload**

Insert after `onTranslationUpdate` (line 20-24):
```typescript
onTranslationCommitted: (callback: (data: { text: string }) => void) => {
  const handler = (_event: unknown, data: { text: string }) => callback(data);
  ipcRenderer.on("overlay:translation-committed", handler);
  return () => ipcRenderer.removeListener("overlay:translation-committed", handler);
},
```

- [ ] **Step 2: Commit**

```bash
git add desktop/src/overlay/overlay-preload.ts
git commit -m "feat: add overlay IPC bridges for incremental translation"
```

---

### Task 5: Integrate IncrementalTranslationManager into ASR Pipeline

**Files:**
- Modify: `desktop/src/main/ipc/asr-handlers.ts`

Wire the `IncrementalTranslationManager` into the existing transcript processing. For partials, feed to the manager and forward committed/draft events to overlay. For sentence flush (final), finalize the incremental translation.

- [ ] **Step 1: Add import and manager instance**

Near top of `registerAsrHandlers()`, after `let flushTimer`, add:
```typescript
import { IncrementalTranslationManager } from "../translation/incremental-translation-manager";

// In registerAsrHandlers, after let flushTimer:
let incrementalMgr = new IncrementalTranslationManager("");
```

- [ ] **Step 2: Wire partial handling with incremental translation**

Replace the `else` branch (partial handling, around line 150-158) with:
```typescript
} else {
  // Partial: feed to incremental translation manager
  const overlay = getOverlayManager();

  // Set utterance ID if this is the beginning of a new utterance
  if (!incrementalMgr.utteranceId) {
    incrementalMgr.resetUtteranceId(`utt-${Date.now()}`);
  }

  incrementalMgr.onCommit = (event) => {
    if (overlay.isVisible()) {
      overlay.sendToOverlay("overlay:translation-committed", {
        text: event.text,
      });
    }
    originalSend("translation:commit-chunk", {
      utteranceId: incrementalMgr.utteranceId,
      text: event.text,
      revision: event.revision,
    });
  };

  incrementalMgr.handlePartial(segment.text);

  if (overlay.isVisible()) {
    overlay.sendToOverlay("overlay:partial-update", {
      text: segment.text,
    });
  }
}
```

- [ ] **Step 3: Wire flushPending for incremental finalization**

In the `flushPending` function, after the translation result comes back (around line 70-80), modify the translation callback to finalize the incremental manager:

```typescript
const flushPending = () => {
  if (flushTimer) {
    clearTimeout(flushTimer);
    flushTimer = null;
  }
  if (!pendingText) return;

  const line = {
    id: pendingLineId,
    text: pendingText.trim(),
    isFinal: true,
    timestamp: Date.now(),
  };

  pendingText = "";
  pendingLineId = "";

  getSessionStorage().appendLine(line);

  const overlay = getOverlayManager();

  // Finalize incremental translation
  incrementalMgr.onFinalize = (event) => {
    if (!mainWindow.isDestroyed()) {
      originalSend("translation:segment-result", {
        segmentId: line.id,
        translatedText: event.fullTranslation,
        providerName: "incremental",
        durationMs: 0,
      });
      if (overlay.isVisible()) {
        overlay.sendToOverlay("overlay:transcript-line", {
          ...line,
          translatedText: event.fullTranslation || undefined,
        });
        overlay.sendToOverlay("overlay:translation-committed", { text: "" });
      }
    }
  };

  incrementalMgr.handleFinal(line.text).then(() => {
    incrementalMgr.reset();
  }).catch((err) => {
    console.error("[incremental] finalization failed:", err);
    // Fallback: send original line without translation
    if (overlay.isVisible()) {
      overlay.sendToOverlay("overlay:transcript-line", line);
    }
    incrementalMgr.reset();
  });
};
```

- [ ] **Step 4: Clear incremental state on stop**

In `asr:start-transcription`, add reset:
```typescript
incrementalMgr.reset();
```

In `asr:stop-transcription`:
```typescript
incrementalMgr.reset();
```

- [ ] **Step 5: Commit**

```bash
git add desktop/src/main/ipc/asr-handlers.ts
git commit -m "feat: integrate incremental translation into ASR pipeline"
```

---

### Task 6: Update OverlayApp for Committed/Draft Translation

**Files:**
- Modify: `desktop/src/overlay/OverlayApp.tsx`

Extend the overlay `partial` state to include committed and draft translation text, and display them below the partial source.

- [ ] **Step 1: Extend overlayAPI interface**

Update the `overlayAPI` declaration at line 18-28 to add the new methods:

```typescript
onTranslationCommitted: (cb: (data: { text: string }) => void) => () => void;
onTranslationDraft: (cb: (data: { text: string }) => void) => () => void;
```

- [ ] **Step 2: Update state to track incremental translation**

Change the `partial` state (line 43) to include translation:
```typescript
const [partial, setPartial] = useState<{
  text: string;
  committedTranslation?: string;
} | null>(null);
```

- [ ] **Step 3: Add listener for committed events**

In the `useEffect` with overlays (line 53-89), add a new listener:
```typescript
window.overlayAPI.onTranslationCommitted((data) => {
  setPartial((prev) =>
    prev ? { ...prev, committedTranslation: data.text || undefined } : null,
  );
}),
```

- [ ] **Step 4: Update partial render block to show committed translation**

Replace the partial render section (lines 184-203) with:
```tsx
{partial && (
  <div className="mb-3">
    <p
      className={`${textColor} font-medium italic opacity-70`}
      style={{ fontSize: settings.fontSize, lineHeight: settings.lineHeight }}
    >
      {partial.text}
    </p>
    {settings.showTranslation && (
      <>
        {partial.committedTranslation ? (
          <p
            className={`${mutedColor} mt-0.5`}
            style={{
              fontSize: Math.max(14, settings.fontSize - 4),
              lineHeight: settings.lineHeight,
            }}
          >
            {partial.committedTranslation}
          </p>
        ) : (
          <p
            className={`${mutedColor} mt-0.5 animate-pulse`}
            style={{
              fontSize: Math.max(14, settings.fontSize - 4),
              lineHeight: settings.lineHeight,
            }}
          >
            ···
          </p>
        )}
      </>
    )}
  </div>
)}
```

- [ ] **Step 5: Clear partial translations on transcript-line and clear**

In the `onTranscriptLine` handler, reset partial translation state:
```typescript
setPartial(null);
```

In the `onClear` handler, already sets `setPartial(null)`.

- [ ] **Step 6: Commit**

```bash
git add desktop/src/overlay/OverlayApp.tsx desktop/src/overlay/overlay-preload.ts
git commit -m "feat: show committed/draft incremental translation in overlay"
```

---

### Verification

After all tasks, verify the incremental translation pipeline:

1. **Build**: `pnpm start` — app launches without errors
2. **Transcription**: Start capture → speak a multi-word phrase → verify partial text appears in overlay
3. **Incremental translation**: After ~2-3 partial updates with stable prefix, verify translation appears in overlay's partial section (normal text, not faded)
4. **Finalization**: After speaking stops and sentence flushes, verify the final line appears with translation
5. **Race condition**: Speak quickly, verify no stale/duplicate translation text appears
