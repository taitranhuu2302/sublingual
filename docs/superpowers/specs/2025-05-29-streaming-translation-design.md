# Realtime Streaming Translation Design

## Goal

True token-by-token streaming translation cho Electron app. ASR text (partial/final) → FastAPI SSE → renderer hiển thị từng từ real-time (ChatGPT effect).

## Scope

- Engine: NLLB-200-distilled-600M via HuggingFace transformers + TextIteratorStreamer
- Language pairs (MVP): en→vi, vi→en
- Protocol: SSE (Server-Sent Events)
- Only applies to LocalTranslateService

## Architecture

```
Whisper ASR (partial/final)
       │
       ▼
┌─ Electron Main: TranslationScheduler ─┐
│  partial → debounce 300ms, abort prev  │
│  final   → queue FIFO, send immediately│
└────────────────────────────────────────┘
       │
       ▼ fetch SSE
┌─ FastAPI: POST /translate/stream ──────┐
│  Input: {text, source_lang, target_lang}│
│  HF model.generate() + TextIteratorStreamer │
│  Yield token-by-token as SSE events    │
└────────────────────────────────────────┘
       │
       ▼ SSE events via IPC
┌─ Renderer: useStreamingTranslation ────┐
│  Append token → current translated line│
│  On done → finalize, next segment      │
│  On cancel → clear buffer              │
└────────────────────────────────────────┘
       │
       ▼
┌─ SubtitleOverlay ──────────────────────┐
│  Line 1: [Original ASR text]           │
│  Line 2: [Translated, streaming in]    │
└────────────────────────────────────────┘
```

## Component Details

### 1. FastAPI — translate refactor

**New endpoint:** `POST /translate/stream`

**Request:**
```json
{"text": "Hello world", "source_lang": "eng_Latn", "target_lang": "vie_Latn"}
```

**Response:** SSE stream
```
data: {"token": "Xin", "done": false}
data: {"token": " chào", "done": false}
data: {"token": " thế", "done": false}
data: {"token": " giới", "done": false}
data: {"token": "", "done": true, "full_text": "Xin chào thế giới"}
```

**Implementation:**
```python
from transformers import AutoModelForSeq2SeqLM, AutoTokenizer, TextIteratorStreamer
from fastapi.responses import StreamingResponse
import threading, json, asyncio

# Loaded once at startup
model = AutoModelForSeq2SeqLM.from_pretrained("facebook/nllb-200-distilled-600M")
tokenizer = AutoTokenizer.from_pretrained("facebook/nllb-200-distilled-600M")

@app.post("/translate/stream")
async def translate_stream(req: StreamTranslateRequest):
    return StreamingResponse(
        generate_sse(req.text, req.source_lang, req.target_lang),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"}
    )

async def generate_sse(text: str, src_lang: str, tgt_lang: str):
    tokenizer.src_lang = src_lang
    inputs = tokenizer(text, return_tensors="pt")
    forced_bos = tokenizer.convert_tokens_to_ids(tgt_lang)

    streamer = TextIteratorStreamer(tokenizer, skip_special_tokens=True)

    thread = threading.Thread(
        target=model.generate,
        kwargs={
            **inputs,
            "forced_bos_token_id": forced_bos,
            "max_new_tokens": 256,
            "streamer": streamer,
        }
    )
    thread.start()

    full_text = ""
    for token_text in streamer:
        if token_text:
            full_text += token_text
            yield f"data: {json.dumps({'token': token_text, 'done': False})}\n\n"
            await asyncio.sleep(0)  # yield control

    yield f"data: {json.dumps({'token': '', 'done': True, 'full_text': full_text})}\n\n"
```

**Model loading:**
- Load at startup, keep in memory
- `model.eval()` + `torch.no_grad()` context in generate thread
- Optional: `model.half()` for FP16 on supported CPUs

**Existing endpoints giữ nguyên** — `/translate`, `/translate/batch`, `/translate/realtime` vẫn dùng CT2 MarianMT cho non-streaming use cases.

### 2. Electron Main — TranslationScheduler

**File:** `src/main/translation/translation-scheduler.ts`

**Responsibilities:**
- Receive transcript segments (partial/final) from whisper process
- For partials: debounce 300ms, abort previous SSE fetch
- For finals: enqueue, process FIFO, no abort
- Fetch SSE from translate, parse events
- Forward each token to renderer via IPC

```typescript
interface TranslationScheduler {
  handleSegment(segment: { text: string; isFinal: boolean }): void;
  destroy(): void;
}
```

**Abort logic:**
- Each SSE fetch uses `AbortController`
- New partial → abort current partial fetch, start new one
- Final segments are never aborted

**IPC events emitted:**
```typescript
// To renderer:
"translation:token"   → { token: string, done: boolean, segmentId: string, fullText?: string }
"translation:cancel"  → { segmentId: string }  // partial was superseded
```

### 3. Electron Preload — Bridge

Add to existing `electronAPI`:
```typescript
translation: {
  onToken: (cb: (data: TranslationTokenEvent) => void) => () => void;
  onCancel: (cb: (data: { segmentId: string }) => void) => () => void;
}
```

### 4. Renderer — `useStreamingTranslation` hook

**State:**
```typescript
interface StreamingLine {
  segmentId: string;
  originalText: string;
  translatedText: string;  // accumulates as tokens arrive
  done: boolean;
}
```

**Logic:**
- On `translation:token` event → find or create StreamingLine by segmentId, append token
- On `done: true` → mark line as finalized
- On `translation:cancel` → remove incomplete line
- Expose `lines: StreamingLine[]` to UI

### 5. UI — SubtitleOverlay update

Display both original + translated per segment:
```
┌────────────────────────────────────────┐
│ Hello, how are you today?              │  ← original (ASR)
│ Xin chào, bạn khỏe không hôm na▌      │  ← translated (streaming in)
└────────────────────────────────────────┘
```

- Show cursor/blink effect while streaming
- Fade partial lines (opacity 60%)
- Final lines full opacity
- Keep last N lines visible (configurable, default 5)

## Data Flow Timeline

```
t=0ms     Whisper emits partial: "Hello how"
t=0ms     Scheduler: start debounce timer (300ms)
t=150ms   Whisper emits partial: "Hello how are"
t=150ms   Scheduler: reset debounce timer
t=450ms   Debounce fires → POST /translate/stream {text: "Hello how are", ...}
t=470ms   SSE: {"token": "Xin", "done": false}        → IPC → UI appends "Xin"
t=510ms   SSE: {"token": " chào", "done": false}      → IPC → UI appends " chào"
t=550ms   SSE: {"token": " bạn", "done": false}       → IPC → UI appends " bạn"
t=600ms   Whisper emits partial: "Hello how are you"
t=600ms   Scheduler: abort current SSE, reset debounce
t=600ms   UI: cancel event → clear "Xin chào bạn"
t=900ms   Debounce fires → POST /translate/stream {text: "Hello how are you", ...}
...
t=1200ms  Whisper emits FINAL: "Hello, how are you today?"
t=1200ms  Scheduler: abort partial SSE, send final immediately
t=1220ms  SSE tokens stream in...
t=1500ms  SSE: {"done": true, "full_text": "Xin chào, bạn khỏe không hôm nay?"}
t=1500ms  UI: line finalized
```

## Performance Considerations

- NLLB-600M on CPU: ~200-500ms per sentence (10 tokens), first token ~50-100ms
- Acceptable for realtime subtitles (user reads slower than generation)
- If too slow: can switch to `model.half()` or use `optimum` with ONNX Runtime
- Debounce 300ms prevents flooding server with every whisper partial

## Refactor to translate

**Keep existing:**
- CT2 MarianMT for `/translate`, `/translate/batch`, `/translate/realtime` (fast, non-streaming)
- Model manager, config, schemas

**Add:**
- `app/translator/nllb_streaming.py` — NLLB model loader + streaming generator
- `POST /translate/stream` endpoint in `app/main.py`
- New schema: `StreamTranslateRequest`
- Startup: load NLLB model alongside existing CT2 models

**Dependencies to add:**
- `transformers` (already likely present for tokenizer)
- `torch` (CPU-only: `torch --index-url https://download.pytorch.org/whl/cpu`)
- `sentencepiece` (for NLLB tokenizer)

## Language Codes (NLLB format)

| Language | NLLB code |
|----------|-----------|
| English  | `eng_Latn` |
| Vietnamese | `vie_Latn` |
