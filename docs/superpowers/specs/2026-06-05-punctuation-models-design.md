# Punctuation & Recasing Models

## Summary

Integrate Vosk recasepunc models (`vosk-recasepunc-en-0.22`) into Sublingual to add automatic punctuation restoration and text recasing to transcribed speech. The model is managed through the existing download/install mechanism and activated automatically when installed.

## Architecture

### Flow

```
ASR Worker (fork)
  ├── Load ASR model (existing)
  ├── If recasepunc model is available:
  │     └── vosk_model_set_add_punc(model, puncModelPath)
  │         → Vosk output includes punctuation & casing natively
  └── Recognizer → punctuated text → main process → overlay/session
```

No changes to the main-process pipeline are needed — the worker output is already forwarded as-is. With recasepunc attached, `isSentenceComplete()` in `asr-handlers.ts` works more accurately because sentences have proper punctuation endings.

### Scope

This feature covers English only (`vosk-recasepunc-en-0.22`). Russian and German recasepunc models exist but are not part of this spec.

## Changes

### 1. Model Catalog (`model-source-catalog.ts`)

Add entry:

```typescript
{
  id: "vosk-recasepunc-en-0.22",
  name: "English Punctuation & Recasing",
  description:
    "Restores punctuation (.,!?) and corrects capitalization in transcribed English text. ~1.6GB.",
  size: "1.6 GB",
  sizeBytes: 1_600_000_000,
  language: "en",
  filename: "vosk-recasepunc-en-0.22.zip",
  url: "https://alphacephei.com/vosk/models/vosk-recasepunc-en-0.22.zip",
}
```

**Installation detection**: The model contains `am/final.mdl` (same structure as ASR models), so existing `fs.existsSync(path.join(localPath, "am", "final.mdl"))` will detect it. No detection changes needed.

### 2. Worker Bindings (`vosk-worker.ts`)

Add FFI binding:

```typescript
let modelSetAddPunc: (...args: any[]) => void;
modelSetAddPunc = lib.func("vosk_model_set_add_punc", "void", [VoskModelPtr, "string"]);
```

Wrap in try-catch for graceful fallback if the DLL doesn't export this function (older Vosk versions).

### 3. Worker Start Handler (`vosk-worker.ts`)

Accept optional `puncModelPath` in the "start" message. After loading the main model, attach the recasepunc model before creating the recognizer:

```
case "start":
  model = modelNew(msg.modelPath)

  if (msg.puncModelPath):
    try:
      modelSetAddPunc(model, msg.puncModelPath)
    catch:
      log error, continue without punctuation

  recognizer = recognizerNew(model, ...)
  ...
```

### 4. Vosk Process (`vosk-process.ts`)

Extend `startVosk()` signature to accept `puncModelPath: string | null`. Pass it to the worker in the start message:

```typescript
worker.send({ type: "start", modelPath, puncModelPath });
```

### 5. Model Manager (`model-manager.ts`)

Add `getPunctuationModel()` following `getSpkModel()` pattern:

```typescript
getPunctuationModel(): VoskModel | null {
  return this.listModels()
    .find((m) => m.id === "vosk-recasepunc-en-0.22" && m.downloaded)
    ?? null;
}
```

### 6. ASR Handlers (`asr-handlers.ts`)

In `asr:start-transcription`, auto-detect and pass recasepunc model path:

```typescript
const puncModel = mm.getPunctuationModel();
const puncModelPath = puncModel?.path ?? null;

await startVosk(model.path, puncModelPath, mainWindow);
```

## Behavior

| State | Behavior |
|-------|----------|
| Recasepunc model not installed | No change — raw Vosk output as today |
| Recasepunc model installed | Automatically used — output has punctuation & casing |
| Vosk DLL doesn't support `vosk_model_set_add_punc` | Graceful fallback — output unchanged, logged error |
| Source language is not English | Recasepunc model is English-only, no effect (text unchanged) |

## File Changes Summary

| File | Change |
|------|--------|
| `model-source-catalog.ts` | Add recasepunc model entry |
| `vosk-worker.ts` | Add `modelSetAddPunc` binding, apply in start |
| `vosk-process.ts` | Add `puncModelPath` param, pass to worker |
| `model-manager.ts` | Add `getPunctuationModel()` |
| `asr-handlers.ts` | Auto-detect and pass punctuation path |

No UI changes, no settings changes, no new IPC channels.
