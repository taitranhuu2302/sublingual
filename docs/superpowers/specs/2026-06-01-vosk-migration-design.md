# Vosk Migration: Replace Whisper CLI with Vosk In-Process ASR

## Problem

Whisper integration uses spawning `whisper-cli` process for every chunk of audio.
Each chunk (500-2000ms) requires writing a temp WAV file, spawning a process, and
parsing stdout. This is fundamentally non-realtime — latency accumulates and
partial results are impossible.

## Solution

Replace Whisper (CLI-based batch processing) with Vosk (in-process streaming ASR
via native bindings). Vosk's `acceptWaveform()` processes PCM16 audio as it
arrives, providing zero-latency partial results and immediate final results when
silence is detected.

## Architecture

### Before (Whisper)

```
Audio Capture → PCM16 buffer → every N ms → write WAV → spawn whisper-cli → parse stdout → transcript
                                                                              └── batch only, no partials
```

### After (Vosk)

```
Audio Capture → PCM16 → acceptWaveform() → partialResult() mỗi chunk → partial transcript (isFinal: false)
                                          → khi silence: result() → final transcript (isFinal: true)
```

No batching, no temp files, no subprocess spawning.

## Files Changed

| File | Action |
|------|--------|
| `desktop/src/main/asr/whisper-process.ts` | DELETE |
| `desktop/src/main/asr/whisper-types.ts` | DELETE |
| `desktop/src/main/asr/vosk-process.ts` | CREATE |
| `desktop/src/main/audio/audio-capture.ts` | MODIFY (import path) |
| `desktop/src/main/ipc/asr-handlers.ts` | MODIFY (use vosk, handle partials) |
| `desktop/src/main/models/model-source-catalog.ts` | MODIFY (Vosk model catalog) |
| `desktop/src/main/models/model-manager.ts` | MODIFY (Vosk model detection) |
| `desktop/src/main/models/model-downloader.ts` | MODIFY (zip extraction) |
| `desktop/src/main/settings/settings-store.ts` | MODIFY (remove chunkPreset) |
| `desktop/src/hooks/use-transcription.ts` | MODIFY (handle partials if needed) |
| `desktop/src/pages/HomePage.tsx` | MODIFY (remove chunkPreset UI) |
| `desktop/src/components/settings/SpeechSettings.tsx` | MODIFY (Vosk model list, no chunkPreset) |
| `desktop/package.json` | MODIFY (add `vosk` dependency) |

## Vosk ASR Module (`vosk-process.ts`)

### State

```typescript
let model: vosk.Model | null = null;
let recognizer: vosk.Recognizer | null = null;
let mainWindowRef: BrowserWindow | null = null;
```

### Functions

- **`startVosk(modelPath: string, mainWindow: BrowserWindow)`**
  - Initialize `new vosk.Model(modelPath)` (modelPath is a directory)
  - Initialize `new vosk.Recognizer({model, sampleRate: 16000})`
  - Configure `recognizer.setWords(true)` for word-level timestamps (optional)

- **`feedAudio(pcmData: Buffer)`**
  - Call `recognizer.acceptWaveform(pcmData)` synchronously
  - If returns `true` (silence detected):
    - `const result = recognizer.result()` → `{text: "..."}`
    - Send `{text: result.text, isFinal: true, timestamp}` via `asr:transcript`
  - If returns `false` (still speaking):
    - `const partial = recognizer.partialResult()` → `{partial: "..."}`
    - Send `{text: partial.partial, isFinal: false, timestamp}` via `asr:transcript`

- **`stopVosk()`**
  - `const final = recognizer.finalResult()`
  - Send any remaining text
  - `recognizer.free()`
  - `model.free()`
  - Reset state

### Error Handling

- If `vosk.Model` constructor fails (bad path, unsupported platform) → throw descriptive error
- If `acceptWaveform` throws → log and continue (don't crash capture loop)
- Log Vosk log level via `vosk.setLogLevel(-1)` (minimal noise)

## Audio Capture (`audio-capture.ts`)

Change import:
```typescript
// Before
import { feedAudio } from "../asr/whisper-process";
// After
import { feedAudio } from "../asr/vosk-process";
```

No other changes — audio capture already outputs PCM16 16kHz mono, which is
what Vosk expects.

## IPC Handlers (`asr-handlers.ts`)

### Changes

- Replace `startWhisper(config, mainWindow)` with `startVosk(model.path, mainWindow)`
- Replace `stopWhisper()` with `stopVosk()`

### Partial Text Handling (NEW)

Current handler only processes `isFinal: true` segments (sentence merging,
translation). With Vosk, we now receive `isFinal: false` partial results.

Partial flow:
- `asr:transcript` with `isFinal: false` → forward directly to renderer via
  `mainWindow.webContents.send("asr:transcript", segment)` (skip sentence merging)

Final flow (unchanged):
- `asr:transcript` with `isFinal: true` → sentence merging → flush → translation

## Model Catalog

### Vosk Models for English + Vietnamese

| ID | Name | Language | Size | URL |
|----|------|----------|------|-----|
| `vosk-model-small-en-us-0.15` | English (Small) | en | 40MB | `https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip` |
| `vosk-model-en-us-0.22` | English (Accurate) | en | 1.8GB | `https://alphacephei.com/vosk/models/vosk-model-en-us-0.22.zip` |
| `vosk-model-en-us-0.22-lgraph` | English (Dynamic) | en | 128MB | `https://alphacephei.com/vosk/models/vosk-model-en-us-0.22-lgraph.zip` |
| `vosk-model-small-vn-0.4` | Vietnamese (Small) | vi | 32MB | `https://alphacephei.com/vosk/models/vosk-model-small-vn-0.4.zip` |
| `vosk-model-vn-0.4` | Vietnamese | vi | 78MB | `https://alphacephei.com/vosk/models/vosk-model-vn-0.4.zip` |

### Model Storage

- Downloaded as zip files → extracted into `~/.sublingual/models/<model-id>/`
- Directory structure after extraction (Vosk format):
  ```
  models/vosk-model-en-us-0.22/
    ├── am/
    ├── conf/
    ├── graph/
    ├── ivector/
    ├── rescore/
    └── rnnlm/
  ```
- Detection: `fs.existsSync(path.join(modelsDir, modelId, "am", "final.mdl"))`

### Download Changes

- **Before**: Download single `.bin` file directly
- **After**: Download `.zip` → extract using `adm-zip` (add dependency) → delete `.zip`
- Extract in streaming fashion (write zip to temp, extract, remove temp zip)

## Settings

### Remove `realtimeChunkPreset`

```typescript
// Before
export interface SpeechToTextSettings {
  selectedModel: string;
  realtimeChunkPreset: "Fast" | "Balanced" | "Accurate";
  sourceLanguage: string;
}

// After
export interface SpeechToTextSettings {
  selectedModel: string;
  sourceLanguage: string;
}
```

- `sourceLanguage` kept for translation API (separate from STT model)
- `selectedModel` now stores Vosk model ID (e.g., `vosk-model-en-us-0.22`)

## UI Changes

### SpeechSettings.tsx

- Model selector: list installable Vosk models, grouped by language
  - English: [English (Small), English (Accurate), English (Dynamic)]
  - Vietnamese: [Vietnamese (Small), Vietnamese]
- Remove chunk preset dropdown (Fast/Balanced/Accurate)
- Keep source language selector (for translation)

### HomePage.tsx

- Remove chunk preset badge (was showing "500ms" / "1000ms")
- Model name display: use Vosk model name instead of whisper filename parsing

## Dependencies

### Add to `desktop/package.json`

- `"vosk": "^0.3.39"` — Vosk Node.js bindings with native binaries
- `"adm-zip": "^0.5.16"` — or similar, for zip extraction

### Native Module Handling

- `vosk` bundles native binaries in its `lib/` directory (per-platform)
- Electron Forge `@electron-forge/plugin-auto-unpack-natives` should handle this
- For Vite bundling: ensure `vosk` is externalized in Vite config for main process
- Test on macOS first (primary target), verify on Windows

## IPC Channels (Unchanged)

| Channel | Direction | Purpose |
|---------|-----------|---------|
| `asr:transcript` | main → renderer | New transcript segment (partial or final) |
| `audio:data` | main → renderer | Audio visualization data |
| (All others unchanged) | | |

## Testing

- Manual: start transcription, verify partial text appears in real-time, verify
  sentence merging on silence
- Edge cases: continuous speech (no pauses), very short utterances, silence,
  model not found
- Test both English and Vietnamese models
- Verify translation still works end-to-end
- Verify overlay window shows partial and final text correctly
