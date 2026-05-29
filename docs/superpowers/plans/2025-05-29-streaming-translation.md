# Streaming Translation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement true token-by-token streaming translation (NLLB-600M + SSE) across FastAPI service and Electron app.

**Architecture:** FastAPI loads NLLB model, exposes SSE endpoint. Electron main process schedules translation requests with debounce/abort, fetches SSE, forwards tokens via IPC. Renderer accumulates tokens and renders streaming effect.

**Tech Stack:** Python (FastAPI, transformers, torch CPU, sentencepiece), TypeScript (Electron IPC, fetch ReadableStream), React (hooks, shadcn UI)

---

## File Structure

```
translate-service/
├── app/
│   ├── main.py                          — Add /translate/stream endpoint
│   ├── schemas.py                       — Add StreamTranslateRequest
│   └── translator/
│       └── nllb_streaming.py            — NEW: NLLB model loader + token generator
├── requirements.txt                     — Add torch, sentencepiece

desktop-electron/src/
├── main/
│   └── translation/
│       └── translation-scheduler.ts     — NEW: debounce/abort/SSE fetch logic
├── preload.ts                           — Add translation bridge
├── main/ipc-handlers.ts                 — Wire translation IPC
├── hooks/
│   └── use-streaming-translation.ts     — NEW: accumulate tokens hook
├── components/
│   └── SubtitleOverlay.tsx              — Update: show original + streaming translated
└── types/
    └── electron-api.d.ts                — Add translation types
```

---

### Task 1: NLLB Streaming Generator (FastAPI)

**Files:**
- Create: `translate-service/app/translator/nllb_streaming.py`

- [ ] **Step 1: Create nllb_streaming.py with model loader**

```python
# app/translator/nllb_streaming.py
import threading
from typing import Iterator
import torch
from transformers import AutoModelForSeq2SeqLM, AutoTokenizer, TextIteratorStreamer

_model = None
_tokenizer = None
_lock = threading.Lock()

LANG_MAP = {
    "en": "eng_Latn",
    "vi": "vie_Latn",
}


def _load_model():
    global _model, _tokenizer
    if _model is not None:
        return
    with _lock:
        if _model is not None:
            return
        _tokenizer = AutoTokenizer.from_pretrained("facebook/nllb-200-distilled-600M")
        _model = AutoModelForSeq2SeqLM.from_pretrained("facebook/nllb-200-distilled-600M")
        _model.eval()


def get_model():
    _load_model()
    return _model


def get_tokenizer():
    _load_model()
    return _tokenizer


def stream_translate(text: str, source_lang: str, target_lang: str) -> Iterator[str]:
    """
    True token-by-token translation using TextIteratorStreamer.
    Yields decoded token strings as they are generated.
    """
    model = get_model()
    tokenizer = get_tokenizer()

    src_code = LANG_MAP.get(source_lang, source_lang)
    tgt_code = LANG_MAP.get(target_lang, target_lang)

    tokenizer.src_lang = src_code
    inputs = tokenizer(text, return_tensors="pt", truncation=True, max_length=512)
    forced_bos = tokenizer.convert_tokens_to_ids(tgt_code)

    streamer = TextIteratorStreamer(tokenizer, skip_special_tokens=True, skip_prompt=True)

    generate_kwargs = {
        **inputs,
        "forced_bos_token_id": forced_bos,
        "max_new_tokens": 256,
        "streamer": streamer,
    }

    thread = threading.Thread(target=_generate, args=(model, generate_kwargs))
    thread.start()

    for token_text in streamer:
        if token_text:
            yield token_text

    thread.join()


@torch.no_grad()
def _generate(model, kwargs):
    model.generate(**kwargs)
```

- [ ] **Step 2: Verify module imports work**

Run (from translate-service/):
```bash
python -c "from app.translator.nllb_streaming import stream_translate; print('OK')"
```
Expected: `OK` (model download may take time on first run)

- [ ] **Step 3: Commit**

```bash
git add app/translator/nllb_streaming.py && git commit -m "feat: NLLB streaming token generator"
```

---

### Task 2: SSE Endpoint (FastAPI)

**Files:**
- Modify: `translate-service/app/schemas.py`
- Modify: `translate-service/app/main.py`
- Modify: `translate-service/requirements.txt`

- [ ] **Step 1: Add StreamTranslateRequest schema**

Add to `app/schemas.py`:

```python
class StreamTranslateRequest(BaseModel):
    text: str = Field(..., min_length=1, max_length=1000)
    source_lang: str = Field(default="en")
    target_lang: str = Field(default="vi")
```

- [ ] **Step 2: Add /translate/stream endpoint**

Add to `app/main.py`:

```python
import json
import asyncio
from fastapi.responses import StreamingResponse
from app.translator.nllb_streaming import stream_translate

@app.post("/translate/stream")
async def translate_stream(req: StreamTranslateRequest):
    return StreamingResponse(
        _sse_generator(req.text, req.source_lang, req.target_lang),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no",
        },
    )


async def _sse_generator(text: str, source_lang: str, target_lang: str):
    full_text = ""
    loop = asyncio.get_event_loop()

    # Run blocking iterator in thread
    for token_text in await loop.run_in_executor(None, lambda: list(_consume_sync(text, source_lang, target_lang))):
        full_text += token_text
        yield f"data: {json.dumps({'token': token_text, 'done': False})}\n\n"

    yield f"data: {json.dumps({'token': '', 'done': True, 'full_text': full_text})}\n\n"


def _consume_sync(text, source_lang, target_lang):
    """Wrapper to collect from generator in executor thread."""
    from app.translator.nllb_streaming import stream_translate
    return list(stream_translate(text, source_lang, target_lang))
```

**Wait** — the above collects all tokens first then yields, defeating streaming. Fix:

```python
import json
import asyncio
import queue
import threading
from fastapi.responses import StreamingResponse
from app.translator.nllb_streaming import stream_translate


@app.post("/translate/stream")
async def translate_stream(req: StreamTranslateRequest):
    return StreamingResponse(
        _sse_generator(req.text, req.source_lang, req.target_lang),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no",
        },
    )


async def _sse_generator(text: str, source_lang: str, target_lang: str):
    token_queue: queue.Queue = queue.Queue()
    sentinel = object()

    def _produce():
        for token_text in stream_translate(text, source_lang, target_lang):
            token_queue.put(token_text)
        token_queue.put(sentinel)

    thread = threading.Thread(target=_produce, daemon=True)
    thread.start()

    full_text = ""
    while True:
        try:
            item = token_queue.get(timeout=0.01)
        except queue.Empty:
            await asyncio.sleep(0.01)
            continue

        if item is sentinel:
            break

        full_text += item
        yield f"data: {json.dumps({'token': item, 'done': False})}\n\n"

    yield f"data: {json.dumps({'token': '', 'done': True, 'full_text': full_text})}\n\n"
```

- [ ] **Step 3: Update requirements.txt**

Add:
```
torch --index-url https://download.pytorch.org/whl/cpu
transformers>=4.36.0
sentencepiece>=0.1.99
```

- [ ] **Step 4: Test endpoint with curl**

```bash
curl -X POST http://localhost:8000/translate/stream \
  -H "Content-Type: application/json" \
  -d '{"text": "Hello, how are you today?", "source_lang": "en", "target_lang": "vi"}'
```

Expected: SSE events stream in one by one:
```
data: {"token": "Xin", "done": false}
data: {"token": " ch\u00e0o", "done": false}
...
data: {"token": "", "done": true, "full_text": "Xin ch\u00e0o, b\u1ea1n kh\u1ecfe kh\u00f4ng h\u00f4m nay?"}
```

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: POST /translate/stream SSE endpoint with true token streaming"
```

---

### Task 3: Electron — Translation Types

**Files:**
- Modify: `desktop-electron/src/types/electron-api.d.ts`

- [ ] **Step 1: Add translation types**

Add to `electron-api.d.ts`:

```typescript
export interface TranslationTokenEvent {
  token: string;
  done: boolean;
  segmentId: string;
  fullText?: string;
}

export interface TranslationCancelEvent {
  segmentId: string;
}

// Add to ElectronAPI interface:
export interface ElectronAPI {
  // ... existing audio, asr, settings ...
  translation: {
    onToken: (callback: (data: TranslationTokenEvent) => void) => () => void;
    onCancel: (callback: (data: TranslationCancelEvent) => void) => () => void;
  };
}
```

- [ ] **Step 2: Commit**

```bash
git add -A && git commit -m "feat: translation IPC types"
```

---

### Task 4: Electron — TranslationScheduler

**Files:**
- Create: `desktop-electron/src/main/translation/translation-scheduler.ts`

- [ ] **Step 1: Implement TranslationScheduler**

```typescript
// src/main/translation/translation-scheduler.ts
import { BrowserWindow } from "electron";

interface TranscriptSegment {
  text: string;
  isFinal: boolean;
}

interface SchedulerConfig {
  serviceUrl: string; // e.g. "http://localhost:8000"
  sourceLang: string;
  targetLang: string;
  debounceMs: number; // default 300
}

export class TranslationScheduler {
  private mainWindow: BrowserWindow;
  private config: SchedulerConfig;
  private debounceTimer: NodeJS.Timeout | null = null;
  private currentAbort: AbortController | null = null;
  private segmentCounter = 0;
  private finalQueue: Array<{ text: string; segmentId: string }> = [];
  private processingFinal = false;

  constructor(mainWindow: BrowserWindow, config: SchedulerConfig) {
    this.mainWindow = mainWindow;
    this.config = config;
  }

  handleSegment(segment: TranscriptSegment): void {
    if (segment.isFinal) {
      this.cancelPartial();
      const segmentId = `final-${++this.segmentCounter}`;
      this.finalQueue.push({ text: segment.text, segmentId });
      this.processFinalQueue();
    } else {
      this.schedulePartial(segment.text);
    }
  }

  private schedulePartial(text: string): void {
    // Cancel previous partial
    this.cancelPartial();

    this.debounceTimer = setTimeout(() => {
      const segmentId = `partial-${++this.segmentCounter}`;
      this.fetchStream(text, segmentId, true);
    }, this.config.debounceMs);
  }

  private cancelPartial(): void {
    if (this.debounceTimer) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
    if (this.currentAbort) {
      this.currentAbort.abort();
      this.currentAbort = null;
      // Notify renderer to clear partial
      this.mainWindow.webContents.send("translation:cancel", {
        segmentId: "partial",
      });
    }
  }

  private async processFinalQueue(): Promise<void> {
    if (this.processingFinal) return;
    this.processingFinal = true;

    while (this.finalQueue.length > 0) {
      const item = this.finalQueue.shift()!;
      await this.fetchStream(item.text, item.segmentId, false);
    }

    this.processingFinal = false;
  }

  private async fetchStream(
    text: string,
    segmentId: string,
    isPartial: boolean
  ): Promise<void> {
    const abort = new AbortController();
    if (isPartial) {
      this.currentAbort = abort;
    }

    try {
      const response = await fetch(
        `${this.config.serviceUrl}/translate/stream`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            text,
            source_lang: this.config.sourceLang,
            target_lang: this.config.targetLang,
          }),
          signal: abort.signal,
        }
      );

      if (!response.ok || !response.body) return;

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop() ?? "";

        for (const line of lines) {
          if (!line.startsWith("data: ")) continue;
          const json = line.slice(6);
          try {
            const event = JSON.parse(json);
            this.mainWindow.webContents.send("translation:token", {
              token: event.token,
              done: event.done,
              segmentId,
              fullText: event.full_text,
            });
          } catch {
            // skip malformed
          }
        }
      }
    } catch (err: unknown) {
      if (err instanceof Error && err.name === "AbortError") {
        // Expected when partial is cancelled
        return;
      }
      console.error("Translation stream error:", err);
    } finally {
      if (isPartial && this.currentAbort === abort) {
        this.currentAbort = null;
      }
    }
  }

  updateConfig(partial: Partial<SchedulerConfig>): void {
    Object.assign(this.config, partial);
  }

  destroy(): void {
    this.cancelPartial();
    this.finalQueue = [];
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add -A && git commit -m "feat: TranslationScheduler with debounce, abort, SSE fetch"
```

---

### Task 5: Wire TranslationScheduler into Main Process

**Files:**
- Modify: `desktop-electron/src/main/ipc-handlers.ts`
- Modify: `desktop-electron/src/preload.ts`
- Modify: `desktop-electron/src/main.ts`

- [ ] **Step 1: Instantiate scheduler in main and connect to whisper output**

In `main.ts` or `ipc-handlers.ts`, after window creation:

```typescript
import { TranslationScheduler } from "./translation/translation-scheduler";

// After mainWindow is created:
const translationScheduler = new TranslationScheduler(mainWindow, {
  serviceUrl: "http://localhost:8000",
  sourceLang: "en",
  targetLang: "vi",
  debounceMs: 300,
});

// Connect whisper output to scheduler:
// In the whisper stdout handler (whisper-process.ts), after sending asr:transcript to renderer,
// also feed to scheduler:
// translationScheduler.handleSegment({ text: segment.text, isFinal: segment.isFinal });
```

- [ ] **Step 2: Add translation bridge to preload.ts**

```typescript
// Add to contextBridge.exposeInMainWorld("electronAPI", { ... })
translation: {
  onToken: (callback: (data: { token: string; done: boolean; segmentId: string; fullText?: string }) => void) => {
    const handler = (_event: unknown, data: any) => callback(data);
    ipcRenderer.on("translation:token", handler);
    return () => ipcRenderer.removeListener("translation:token", handler);
  },
  onCancel: (callback: (data: { segmentId: string }) => void) => {
    const handler = (_event: unknown, data: any) => callback(data);
    ipcRenderer.on("translation:cancel", handler);
    return () => ipcRenderer.removeListener("translation:cancel", handler);
  },
},
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: wire translation scheduler to whisper + preload bridge"
```

---

### Task 6: Renderer — useStreamingTranslation Hook

**Files:**
- Create: `desktop-electron/src/hooks/use-streaming-translation.ts`

- [ ] **Step 1: Implement hook**

```typescript
// src/hooks/use-streaming-translation.ts
import { useState, useEffect, useCallback } from "react";

export interface StreamingLine {
  segmentId: string;
  translatedText: string;
  done: boolean;
}

export function useStreamingTranslation() {
  const [lines, setLines] = useState<StreamingLine[]>([]);

  useEffect(() => {
    const unsubToken = window.electronAPI.translation.onToken((data) => {
      setLines((prev) => {
        const existing = prev.find((l) => l.segmentId === data.segmentId);
        if (existing) {
          return prev.map((l) =>
            l.segmentId === data.segmentId
              ? {
                  ...l,
                  translatedText: data.done
                    ? data.fullText ?? l.translatedText
                    : l.translatedText + data.token,
                  done: data.done,
                }
              : l
          );
        }
        // New segment
        return [
          ...prev,
          {
            segmentId: data.segmentId,
            translatedText: data.token,
            done: data.done,
          },
        ];
      });
    });

    const unsubCancel = window.electronAPI.translation.onCancel((data) => {
      setLines((prev) => prev.filter((l) => l.segmentId !== data.segmentId));
    });

    return () => {
      unsubToken();
      unsubCancel();
    };
  }, []);

  const clear = useCallback(() => setLines([]), []);

  return { lines, clear };
}
```

- [ ] **Step 2: Commit**

```bash
git add -A && git commit -m "feat: useStreamingTranslation hook"
```

---

### Task 7: UI — SubtitleOverlay with Streaming Translation

**Files:**
- Modify: `desktop-electron/src/components/SubtitleOverlay.tsx`
- Modify: `desktop-electron/src/pages/HomePage.tsx`

- [ ] **Step 1: Update SubtitleOverlay**

```tsx
// src/components/SubtitleOverlay.tsx
import type { TranscriptEntry } from "../hooks/use-transcription";
import type { StreamingLine } from "../hooks/use-streaming-translation";

interface Props {
  segments: TranscriptEntry[];
  translations: StreamingLine[];
  maxLines?: number;
}

export function SubtitleOverlay({ segments, translations, maxLines = 5 }: Props) {
  const recentSegments = segments.filter((s) => s.isFinal).slice(-maxLines);

  return (
    <div className="fixed bottom-8 left-1/2 -translate-x-1/2 w-[80%] max-w-2xl">
      <div className="bg-black/80 rounded-lg px-6 py-4 space-y-3">
        {recentSegments.length === 0 && (
          <p className="text-white/50 text-center text-sm">Waiting for speech...</p>
        )}
        {recentSegments.map((seg, i) => {
          const translation = translations.find(
            (t) => t.segmentId.includes(String(i)) // simplified matching
          );
          return (
            <div key={i} className="space-y-1">
              <p className="text-white/70 text-sm">{seg.text}</p>
              <p className="text-white text-lg">
                {translation?.translatedText ?? ""}
                {translation && !translation.done && (
                  <span className="animate-pulse">▌</span>
                )}
              </p>
            </div>
          );
        })}

        {/* Current partial (not yet final) */}
        {segments.some((s) => !s.isFinal) && (
          <div className="space-y-1 opacity-60">
            <p className="text-white/50 text-sm italic">
              {segments.filter((s) => !s.isFinal).pop()?.text}
            </p>
            <p className="text-white text-lg italic">
              {translations.find((t) => t.segmentId.startsWith("partial"))?.translatedText ?? ""}
              <span className="animate-pulse">▌</span>
            </p>
          </div>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Update HomePage to pass translations**

```tsx
// In HomePage.tsx, add:
import { useStreamingTranslation } from "../hooks/use-streaming-translation";

// Inside component:
const { lines: translations, clear: clearTranslations } = useStreamingTranslation();

// Pass to SubtitleOverlay:
<SubtitleOverlay segments={segments} translations={translations} />

// On clear button, also clear translations:
const handleClear = () => { clear(); clearTranslations(); };
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: SubtitleOverlay with streaming translation display"
```

---

### Task 8: NLLB Model Download Script

**Files:**
- Create: `translate-service/scripts/download_nllb.py`

- [ ] **Step 1: Create download script**

```python
# scripts/download_nllb.py
"""Download and cache NLLB-200-distilled-600M model."""
from transformers import AutoModelForSeq2SeqLM, AutoTokenizer

MODEL_NAME = "facebook/nllb-200-distilled-600M"

print(f"Downloading {MODEL_NAME}...")
tokenizer = AutoTokenizer.from_pretrained(MODEL_NAME)
model = AutoModelForSeq2SeqLM.from_pretrained(MODEL_NAME)
print(f"Model cached at: {model.config._name_or_path}")
print("Done.")
```

- [ ] **Step 2: Add startup warmup in main.py**

```python
@app.on_event("startup")
async def warmup_nllb():
    """Pre-load NLLB model to avoid cold start on first stream request."""
    from app.translator.nllb_streaming import get_model, get_tokenizer
    get_model()
    get_tokenizer()
    logger.info("NLLB model loaded")
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: NLLB model download script and startup warmup"
```

---

## Execution Order

```
Task 1 → Task 2 → Task 3 → Task 4 → Task 5 → Task 6 → Task 7 → Task 8
  (Python core)     (Electron types)  (Scheduler)  (Wire)  (Hook)  (UI)  (Script)
```

All tasks are sequential — each depends on the previous.

## Verification

After all tasks, end-to-end test:

1. Start translate-service: `cd translate-service && uvicorn app.main:app --port 8000`
2. Test SSE: `curl -N -X POST http://localhost:8000/translate/stream -H "Content-Type: application/json" -d '{"text":"Hello world","source_lang":"en","target_lang":"vi"}'`
3. Start Electron: `cd desktop-electron && pnpm start`
4. Speak into mic → see original text + streaming translation appear word-by-word
