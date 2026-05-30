# Sentence Merging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Merge fragmented whisper batch outputs into complete sentences before sending to the overlay.

**Architecture:** Add buffering logic in `asr-handlers.ts` to detect incomplete sentences and merge them with subsequent batches. Only send to overlay when a sentence is complete (ends with punctuation) or after a timeout.

**Tech Stack:** TypeScript, Electron IPC

---

### Task 1: Add sentence merging logic to asr-handlers.ts

**Files:**
- Modify: `desktop/src/main/ipc/asr-handlers.ts:38-111`

- [ ] **Step 1: Add sentence buffer state variables**

Add these variables inside `registerAsrHandlers()`, after `let segmentCounter = 0;` (line 10):

```typescript
let segmentCounter = 0;
let pendingText = "";
let pendingLineId = "";
let flushTimer: ReturnType<typeof setTimeout> | null = null;
const FLUSH_TIMEOUT_MS = 3000; // flush incomplete sentence after 3s
```

- [ ] **Step 2: Add helper function to detect sentence boundaries**

Add this function before `registerAsrHandlers()`:

```typescript
function isSentenceComplete(text: string): boolean {
  const trimmed = text.trim();
  if (!trimmed) return false;
  // Ends with sentence-ending punctuation (English + Vietnamese)
  return /[.!?…]$/.test(trimmed) ||
    // Vietnamese sentence endings
    /[.!?]$/.test(trimmed) ||
    // Ends with closing quotes after punctuation
    /[.!?]["'\u201D\u201C\u2018\u2019]$/.test(trimmed);
}
```

- [ ] **Step 3: Add flush function to send buffered text**

Add this function inside `registerAsrHandlers()`, after the helper variables:

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

  // Save to session
  getSessionStorage().appendLine(line);

  // Send to overlay (with or without translation)
  const overlay = getOverlayManager();
  const settings = getSettings();

  if (settings.translation.enabled) {
    const srcLang = settings.speechToText.sourceLanguage || "auto";
    const tgtLang = settings.translation.targetLanguage || "vi";
    getTranslationService()
      .translate(line.text, srcLang, tgtLang)
      .then((result) => {
        if (!mainWindow.isDestroyed()) {
          if (result.translatedText) {
            originalSend("translation:segment-result", {
              segmentId: line.id,
              translatedText: result.translatedText,
              providerName: result.providerName,
              durationMs: result.durationMs,
            });
          }
          if (overlay.isVisible()) {
            overlay.sendToOverlay("overlay:transcript-line", {
              ...line,
              translatedText: result.translatedText || undefined,
            });
          }
        }
      })
      .catch((err) => {
        console.error("[translation] auto-translate failed:", err);
        if (overlay.isVisible()) {
          overlay.sendToOverlay("overlay:transcript-line", line);
        }
      });
  } else {
    if (overlay.isVisible()) {
      overlay.sendToOverlay("overlay:transcript-line", line);
    }
  }
};
```

- [ ] **Step 4: Replace the transcript interception logic**

Replace the entire `if (channel === "asr:transcript")` block (lines 41-108) with:

```typescript
if (channel === "asr:transcript") {
  const segment = args[0] as {
    text: string;
    isFinal: boolean;
    timestamp: number;
    id?: string;
  };
  if (segment?.text && segment.isFinal) {
    const lineId = `seg-${segmentCounter++}`;
    segment.id = lineId;

    // Merge with pending text
    if (pendingText) {
      pendingText = pendingText + " " + segment.text;
    } else {
      pendingText = segment.text;
      pendingLineId = lineId;
    }

    // Check if sentence is complete
    if (isSentenceComplete(pendingText)) {
      flushPending();
    } else {
      // Set timeout to flush incomplete sentence
      if (flushTimer) clearTimeout(flushTimer);
      flushTimer = setTimeout(() => {
        flushPending();
      }, FLUSH_TIMEOUT_MS);
    }
  }
}
```

- [ ] **Step 5: Update stopWhisper call to flush pending text**

In the `asr:stop-transcription` handler (line 33-36), add a flush call:

```typescript
ipcMain.handle("asr:stop-transcription", async () => {
  flushPending(); // Flush any buffered text before stopping
  stopWhisper();
  getSessionStorage().stopSession();
});
```

- [ ] **Step 6: Test the changes**

Run the app and verify:
1. Short sentences appear as single lines in overlay
2. Long sentences that span multiple batches are merged into one line
3. After 3s silence, incomplete sentences are flushed
4. Translation works correctly on merged sentences
5. Session storage saves complete sentences

- [ ] **Step 7: Commit**

```bash
git add desktop/src/main/ipc/asr-handlers.ts
git commit -m "feat: merge fragmented whisper outputs into complete sentences"
```
