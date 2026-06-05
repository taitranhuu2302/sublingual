# Punctuation & Recasing Models Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate Vosk recasepunc model into Sublingual for automatic punctuation restoration and text recasing.

**Architecture:** Add recasepunc model to download catalog; on transcription start, detect if installed and pass its path to the Vosk worker; worker attaches the model via `vosk_model_set_add_punc()` FFI call before creating recognizer; Vosk output natively includes punctuation and proper casing.

**Tech Stack:** TypeScript, Vosk (koffi FFI), Electron

---

### Task 1: Add recasepunc model to catalog

**Files:**
- Modify: `desktop/src/main/models/model-source-catalog.ts`

- [ ] **Add model entry to `MODEL_CATALOG`**

Insert after `vosk-model-spk-0.4` entry (before closing `];`):

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
  },
```

No detection changes needed — the model contains `am/final.mdl` so the existing detection check `fs.existsSync(path.join(localPath, "am", "final.mdl"))` will catch it.

- [ ] **Verify by running build**

Run: `npx tsc --noEmit`

Expected: No type errors.

- [ ] **Commit**

```bash
git add desktop/src/main/models/model-source-catalog.ts
git commit -m "feat: add vosk-recasepunc-en-0.22 to model catalog"
```

---

### Task 2: Add `getPunctuationModel()` to ModelManager

**Files:**
- Modify: `desktop/src/main/models/model-manager.ts`

- [ ] **Add method**

Add after `getSpkModel()` (after line 50):

```typescript
  getPunctuationModel(): VoskModel | null {
    return this.listModels().find((m) => m.id === "vosk-recasepunc-en-0.22" && m.downloaded) ?? null;
  }
```

- [ ] **Verify by running build**

Run: `npx tsc --noEmit`

Expected: No type errors.

- [ ] **Commit**

```bash
git add desktop/src/main/models/model-manager.ts
git commit -m "feat: add getPunctuationModel() to ModelManager"
```

---

### Task 3: Add `modelSetAddPunc` FFI binding in worker

**Files:**
- Modify: `desktop/src/main/asr/vosk-worker.ts`

- [ ] **Add function variable declaration**

Add after `getFinalResult` declaration (after line 31):

```typescript
let modelSetAddPunc: (...args: any[]) => void;
```

- [ ] **Add FFI binding in the try block**

Add after `getFinalResult` binding (after line 55):

```typescript
  modelSetAddPunc = lib.func("vosk_model_set_add_punc", "void", [VoskModelPtr, "string"]);
```

Wrap in try-catch in case older Vosk DLL doesn't export this function:

```typescript
  try {
    modelSetAddPunc = lib.func("vosk_model_set_add_punc", "void", [VoskModelPtr, "string"]);
  } catch {
    modelSetAddPunc = () => {};
    console.log("[vosk-worker] vosk_model_set_add_punc not available in this Vosk version");
  }
```

- [ ] **Verify by running build**

Run: `npx tsc --noEmit`

Expected: No type errors.

- [ ] **Commit**

```bash
git add desktop/src/main/asr/vosk-worker.ts
git commit -m "feat: add vosk_model_set_add_punc FFI binding in worker"
```

---

### Task 4: Apply recasepunc model in worker start handler

**Files:**
- Modify: `desktop/src/main/asr/vosk-worker.ts`

- [ ] **Update "start" case to accept and apply puncModelPath**

Replace the current `case "start":` block (lines 73-93):

Old:
```typescript
    case "start": {
      console.log("[vosk-worker] Loading model...");
      try {
        model = modelNew(msg.modelPath);
        if (!model) throw new Error("Failed to create Vosk model");

        recognizer = recognizerNew(model, msg.sampleRate ?? 16000);
        if (!recognizer) throw new Error("Failed to create Vosk recognizer");

        recognizerSetWords(recognizer, true);
        recognizerSetPartialWords(recognizer, true);

        console.log("[vosk-worker] Model ready");
        process.send?.({ type: "ready" });
      } catch (err) {
        console.error("[vosk-worker] Start error:", err);
        if (recognizer) { recognizerFree(recognizer); recognizer = null; }
        if (model) { modelFree(model); model = null; }
        process.send?.({ type: "error", message: String(err) });
      }
      break;
    }
```

New:
```typescript
    case "start": {
      console.log("[vosk-worker] Loading model...");
      try {
        model = modelNew(msg.modelPath);
        if (!model) throw new Error("Failed to create Vosk model");

        if (msg.puncModelPath) {
          console.log("[vosk-worker] Attaching punctuation model...");
          modelSetAddPunc(model, msg.puncModelPath);
        }

        recognizer = recognizerNew(model, msg.sampleRate ?? 16000);
        if (!recognizer) throw new Error("Failed to create Vosk recognizer");

        recognizerSetWords(recognizer, true);
        recognizerSetPartialWords(recognizer, true);

        console.log("[vosk-worker] Model ready");
        process.send?.({ type: "ready" });
      } catch (err) {
        console.error("[vosk-worker] Start error:", err);
        if (recognizer) { recognizerFree(recognizer); recognizer = null; }
        if (model) { modelFree(model); model = null; }
        process.send?.({ type: "error", message: String(err) });
      }
      break;
    }
```

- [ ] **Verify by running build**

Run: `npx tsc --noEmit`

Expected: No type errors.

- [ ] **Commit**

```bash
git add desktop/src/main/asr/vosk-worker.ts
git commit -m "feat: apply punctuation model in worker start handler"
```

---

### Task 5: Add `puncModelPath` parameter to `startVosk()`

**Files:**
- Modify: `desktop/src/main/asr/vosk-process.ts`

- [ ] **Add `puncModelPath` to `WorkerMessage` interface**

Add `puncModelPath?: string;` to the `WorkerMessage` interface (after line 12):

```typescript
  puncModelPath?: string;
```

- [ ] **Update `startVosk()` signature and send call**

Change function signature (line 28):

```typescript
export function startVosk(modelPath: string, puncModelPath: string | null, mainWindow: BrowserWindow): Promise<void> {
```

Update the `worker.send` call (line 99):

```typescript
    worker.send({ type: "start", modelPath, puncModelPath });
```

- [ ] **Verify by running build**

Run: `npx tsc --noEmit`

Expected: Error because `startVosk` is now called with 3 args at the call site. That's fine — Task 6 fixes it.

- [ ] **Commit**

```bash
git add desktop/src/main/asr/vosk-process.ts
git commit -m "feat: add puncModelPath param to startVosk()"
```

---

### Task 6: Auto-detect and pass punctuation path in ASR handlers

**Files:**
- Modify: `desktop/src/main/ipc/asr-handlers.ts`

- [ ] **Update `asr:start-transcription` handler**

In the handler (around line 90), after `const model = mm.getSelectedModel();` and before `await startVosk(model.path, mainWindow);`, add punctuation model detection:

Old (line 109):
```typescript
      await startVosk(model.path, mainWindow);
```

New:
```typescript
      const puncModel = mm.getPunctuationModel();
      const puncModelPath = puncModel?.path ?? null;
      await startVosk(model.path, puncModelPath, mainWindow);
```

- [ ] **Verify by running build**

Run: `npx tsc --noEmit`

Expected: No type errors.

- [ ] **Commit**

```bash
git add desktop/src/main/ipc/asr-handlers.ts
git commit -m "feat: auto-detect and pass punctuation model on transcription start"
```
