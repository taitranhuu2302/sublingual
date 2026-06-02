# Speaker Diarization with Vosk — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add automatic speaker diarization using `vosk-model-spk-0.4` — extract x-vector embeddings from audio segments and cluster by cosine similarity to label speakers.

**Architecture:** Audio captured at 16kHz mono PCM16 is written to a 5-second ring buffer alongside being fed to the Vosk ASR recognizer. When ASR produces a final result with timestamps, the corresponding audio segment is extracted from the buffer, fed to the Vosk SPK recognizer for x-vector extraction, and compared against existing speaker clusters via cosine similarity. Matching segments get `speakerId`/`speakerLabel`/`speakerColor`.

**Tech Stack:** Electron 42 + TypeScript + Vosk (koffi FFI) + React 19 + Tailwind CSS 4

---

### Task 1: FFI Bindings for Vosk SPK C API

**Files:**
- Create: `desktop/src/main/asr/vosk-spk-bindings.ts`
- Modify: `desktop/src/main/asr/vosk-bindings.ts` — add `getLibPath()` export

**Step 1: Export `getLibPath` from vosk-bindings.ts**

```typescript
// In desktop/src/main/asr/vosk-bindings.ts, change this function from
// function getLibPath(): string { ... }
// to:
export function getLibPath(): string {
  if (app.isPackaged) {
    return path.join(process.resourcesPath, "bin", "vosk", "libvosk.dylib");
  }
  return path.join(app.getAppPath(), "bin", "vosk", "libvosk.dylib");
}
```

**Step 2: Create `desktop/src/main/asr/vosk-spk-bindings.ts`**

```typescript
import koffi from "koffi";
import { getLibPath } from "./vosk-bindings";

const isVoskSupported = process.platform === "darwin";

let lib: any = null;
let VoskSpkModel: any = null;
let VoskSpkModelPtr: any = null;
let VoskSpkRecognizer: any = null;
let VoskSpkRecognizerPtr: any = null;

let vosk_spk_model_new: (...args: any[]) => any = () => null;
let vosk_spk_model_free: (...args: any[]) => void = () => {};
let vosk_spk_recognizer_new: (...args: any[]) => any = () => null;
let vosk_spk_recognizer_free: (...args: any[]) => void = () => {};
let vosk_spk_recognizer_accept_waveform: (...args: any[]) => any = () => false;
// Result returns JSON with "spk" array of float64 embedding values
let vosk_recognizer_result: (...args: any[]) => any = () => null;

let initialized = false;

function init(): void {
  if (initialized) return;
  initialized = true;

  if (!isVoskSupported) return;

  try {
    lib = koffi.load(getLibPath());

    VoskSpkModel = koffi.opaque("VoskSpkModel");
    VoskSpkModelPtr = koffi.pointer(VoskSpkModel);
    VoskSpkRecognizer = koffi.opaque("VoskSpkRecognizer");
    VoskSpkRecognizerPtr = koffi.pointer(VoskSpkRecognizer);

    vosk_spk_model_new = lib.func("vosk_spk_model_new", VoskSpkModelPtr, ["string"]);
    vosk_spk_model_free = lib.func("vosk_spk_model_free", "void", [VoskSpkModelPtr]);
    vosk_spk_recognizer_new = lib.func(
      "vosk_spk_recognizer_new",
      VoskSpkRecognizerPtr,
      [VoskSpkModelPtr, "float"]
    );
    vosk_spk_recognizer_free = lib.func(
      "vosk_spk_recognizer_free",
      "void",
      [VoskSpkRecognizerPtr]
    );
    vosk_spk_recognizer_accept_waveform = lib.func(
      "vosk_spk_recognizer_accept_waveform",
      "bool",
      [VoskSpkRecognizerPtr, koffi.pointer("void"), "int"]
    );
    // Note: Vosk SPK recognizer uses the same result function name "vosk_recognizer_result"
    // but with a different recognizer type (VoskSpkRecognizer instead of VoskRecognizer).
    // We need to use a different symbol approach - the function is the same C symbol
    // "vosk_recognizer_result" that takes a generic pointer, so we can't register it
    // twice with different opaque types. We call it with the spk pointer casted.
    vosk_recognizer_result = lib.func(
      "vosk_recognizer_result",
      "string",
      [koffi.pointer("void")]
    );

    console.log("[vosk-spk] bindings initialized");
  } catch (err) {
    console.error("[vosk-spk] Failed to initialize bindings:", err);
    initialized = false;
  }
}

export function isSpkSupported(): boolean {
  init();
  return isVoskSupported && lib !== null;
}

export function spkModelNew(modelPath: string): unknown {
  init();
  const ptr = vosk_spk_model_new(modelPath);
  if (!ptr) throw new Error("Failed to create Vosk SPK model");
  return ptr;
}

export function spkModelFree(modelPtr: unknown): void {
  vosk_spk_model_free(modelPtr);
}

export function spkRecognizerNew(modelPtr: unknown, sampleRate: number): unknown {
  init();
  const ptr = vosk_spk_recognizer_new(modelPtr, sampleRate);
  if (!ptr) throw new Error("Failed to create Vosk SPK recognizer");
  return ptr;
}

export function spkRecognizerFree(recPtr: unknown): void {
  vosk_spk_recognizer_free(recPtr);
}

export function spkAcceptWaveform(recPtr: unknown, data: Buffer): boolean {
  return vosk_spk_recognizer_accept_waveform(recPtr, data, data.length);
}

export function spkGetResult(recPtr: unknown): string {
  return vosk_recognizer_result(recPtr) ?? "";
}
```

**Step 3: Commit**

```bash
git add desktop/src/main/asr/vosk-bindings.ts desktop/src/main/asr/vosk-spk-bindings.ts
git commit -m "feat: add Vosk SPK FFI bindings (vosk_spk_model/recognizer)"
```

---

### Task 2: Speaker Cluster Module

**Files:**
- Create: `desktop/src/main/asr/speaker-cluster.ts`

**Step 1: Create `desktop/src/main/asr/speaker-cluster.ts`**

```typescript
export interface SpeakerIdentity {
  id: string;
  label: string;
  color: string;
  centroid: Float64Array;
  lastSeenAt: number;
}

const SIMILARITY_THRESHOLD = 0.7;

const SPEAKER_COLORS = [
  "#FF6B6B", "#4ECDC4", "#FFE66D", "#6C5CE7",
  "#A8E6CF", "#FF8B94", "#B8B8D1", "#FFB347",
];

function cosineSimilarity(a: Float64Array, b: Float64Array): number {
  let dotProduct = 0;
  let normA = 0;
  let normB = 0;
  for (let i = 0; i < a.length; i++) {
    dotProduct += a[i] * b[i];
    normA += a[i] * a[i];
    normB += b[i] * b[i];
  }
  if (normA === 0 || normB === 0) return 0;
  return dotProduct / (Math.sqrt(normA) * Math.sqrt(normB));
}

function updateCentroid(cluster: SpeakerIdentity, embedding: Float64Array): void {
  const count = (cluster as any)._count ?? 1;
  const newCount = count + 1;
  (cluster as any)._count = newCount;
  for (let i = 0; i < cluster.centroid.length; i++) {
    cluster.centroid[i] = (cluster.centroid[i] * count + embedding[i]) / newCount;
  }
}

/**
 * Assign a speaker identity based on cosine similarity to existing clusters.
 * Returns a new or updated SpeakerIdentity.
 */
export function assignSpeaker(
  clusters: SpeakerIdentity[],
  embedding: Float64Array,
  maxSpeakers: number,
  now: number,
): SpeakerIdentity {
  let bestMatch: SpeakerIdentity | null = null;
  let bestScore = 0;

  for (const cluster of clusters) {
    const score = cosineSimilarity(embedding, cluster.centroid);
    if (score > bestScore && score >= SIMILARITY_THRESHOLD) {
      bestScore = score;
      bestMatch = cluster;
    }
  }

  if (bestMatch) {
    updateCentroid(bestMatch, embedding);
    bestMatch.lastSeenAt = now;
    return bestMatch;
  }

  if (clusters.length >= maxSpeakers) {
    let oldest = clusters[0];
    for (const c of clusters) {
      if (c.lastSeenAt < oldest.lastSeenAt) {
        oldest = c;
      }
    }
    const idx = clusters.indexOf(oldest);
    const newSpeaker: SpeakerIdentity = {
      id: oldest.id,
      label: oldest.label,
      color: oldest.color,
      centroid: new Float64Array(embedding),
      lastSeenAt: now,
    };
    (newSpeaker as any)._count = 1;
    clusters[idx] = newSpeaker;
    return newSpeaker;
  }

  const idx = clusters.length;
  const newSpeaker: SpeakerIdentity = {
    id: `spk_${idx + 1}`,
    label: `Speaker ${idx + 1}`,
    color: SPEAKER_COLORS[idx % SPEAKER_COLORS.length],
    centroid: new Float64Array(embedding),
    lastSeenAt: now,
  };
  (newSpeaker as any)._count = 1;
  clusters.push(newSpeaker);
  return newSpeaker;
}
```

**Step 2: Commit**

```bash
git add desktop/src/main/asr/speaker-cluster.ts
git commit -m "feat: add speaker cluster module with cosine similarity"
```

---

### Task 3: Speaker Process Module (SPK Model Lifecycle)

**Files:**
- Create: `desktop/src/main/asr/speaker-process.ts`

**Step 1: Create `desktop/src/main/asr/speaker-process.ts`**

```typescript
import * as spkBindings from "./vosk-spk-bindings";
import { assignSpeaker, type SpeakerIdentity } from "./speaker-cluster";

let spkModel: unknown = null;
let spkRecognizer: unknown = null;
let clusters: SpeakerIdentity[] = [];
let maxSpeakers = 4;

export function startSpk(modelPath: string, _maxSpeakers: number): void {
  if (!spkBindings.isSpkSupported()) {
    console.log("[spk] Not supported on this platform, skipping");
    return;
  }

  maxSpeakers = _maxSpeakers;
  clusters = [];

  try {
    spkModel = spkBindings.spkModelNew(modelPath);
    spkRecognizer = spkBindings.spkRecognizerNew(spkModel, 16000);
    console.log("[spk] Speaker model loaded successfully");
  } catch (err) {
    console.error("[spk] Failed to load speaker model:", err);
    spkModel = null;
    spkRecognizer = null;
  }
}

export function isSpkRunning(): boolean {
  return spkRecognizer !== null;
}

/**
 * Extract speaker embedding from a PCM16 audio buffer.
 * Returns null if SPK is not running or extraction fails.
 */
export function extractSpeakerEmbedding(pcmBuffer: Buffer): Float64Array | null {
  if (!spkRecognizer) return null;

  try {
    spkBindings.spkAcceptWaveform(spkRecognizer, pcmBuffer);
    const raw = spkBindings.spkGetResult(spkRecognizer);
    if (!raw) return null;

    const parsed = JSON.parse(raw);
    if (!parsed || !Array.isArray(parsed.spk)) return null;

    return new Float64Array(parsed.spk);
  } catch (err) {
    console.error("[spk] Failed to extract embedding:", err);
    return null;
  }
}

/**
 * Assign a speaker label for an embedding. Returns identity info for the
 * transcript line, or null if no embedding was provided.
 */
export function classifySpeaker(embedding: Float64Array | null): {
  speakerId: string;
  speakerLabel: string;
  speakerColor: string;
} | null {
  if (!embedding) return null;

  const identity = assignSpeaker(clusters, embedding, maxSpeakers, Date.now());
  return {
    speakerId: identity.id,
    speakerLabel: identity.label,
    speakerColor: identity.color,
  };
}

export function stopSpk(): void {
  if (spkRecognizer) {
    spkBindings.spkRecognizerFree(spkRecognizer);
    spkRecognizer = null;
  }
  if (spkModel) {
    spkBindings.spkModelFree(spkModel);
    spkModel = null;
  }
  clusters = [];
}
```

**Step 2: Commit**

```bash
git add desktop/src/main/asr/speaker-process.ts
git commit -m "feat: add speaker process module (model lifecycle + embedding + classify)"
```

---

### Task 4: Audio Ring Buffer

**Files:**
- Modify: `desktop/src/main/audio/audio-capture.ts`

**Step 1: Add RingBuffer class and wire it into audio-capture.ts**

In `desktop/src/main/audio/audio-capture.ts`, after the existing imports and before the `capturing` variable:

Add the RingBuffer class:

```typescript
class RingBuffer {
  private buffer: Buffer;
  private writePos: number = 0;
  private totalWritten: number = 0;
  private readonly capacity: number;
  private readonly sampleRate: number;
  private readonly bytesPerSample: number = 2;

  constructor(sampleRate: number, durationMs: number) {
    this.sampleRate = sampleRate;
    const samples = Math.ceil((sampleRate * durationMs) / 1000);
    this.capacity = samples * this.bytesPerSample;
    this.buffer = Buffer.alloc(this.capacity);
  }

  write(data: Buffer): void {
    for (let i = 0; i < data.length; i++) {
      this.buffer[this.writePos] = data[i];
      this.writePos = (this.writePos + 1) % this.capacity;
    }
    this.totalWritten += data.length;
  }

  /**
   * Extract audio segment between startMs and endMs (relative to capture start).
   * Returns Buffer containing PCM16 data, or empty buffer if out of range.
   */
  extractSegment(startMs: number, endMs: number): Buffer {
    const startByte = Math.floor((startMs / 1000) * this.sampleRate) * this.bytesPerSample;
    const endByte = Math.floor((endMs / 1000) * this.sampleRate) * this.bytesPerSample;
    const length = endByte - startByte;

    if (length <= 0) return Buffer.alloc(0);

    const result = Buffer.alloc(length);
    const totalBytes = this.totalWritten;

    for (let i = 0; i < length; i++) {
      const globalOffset = startByte + i;
      if (globalOffset < 0 || globalOffset >= totalBytes) {
        result[i] = 0;
        continue;
      }
      const wrappedOffset = (globalOffset - Math.max(0, totalBytes - this.capacity));
      if (wrappedOffset < 0) continue;
      const idx = wrappedOffset % this.capacity;
      result[i] = this.buffer[idx];
    }

    return result;
  }

  reset(): void {
    this.writePos = 0;
    this.totalWritten = 0;
    this.buffer.fill(0);
  }
}
```

After the `RingBuffer` class, add:

```typescript
let ringBuffer: RingBuffer | null = null;
let captureStartTime: number = 0;
```

**Step 2: Export ring buffer accessors**

Add exports before `stopAudioCapture`:

```typescript
export function getRingBuffer(): RingBuffer | null {
  return ringBuffer;
}

export function getCaptureStartTime(): number {
  return captureStartTime;
}
```

**Step 3: Initialize ring buffer in `startAudioCapture`**

Inside `startAudioCapture`, right after `capturing = true;`, add:

```typescript
  ringBuffer = new RingBuffer(16000, 5000);
  captureStartTime = Date.now();
```

**Step 4: Feed ring buffer inside `downmixAndResample`**

After the line `feedAudio(pcmBuffer);`, add:

```typescript
      ringBuffer?.write(pcmBuffer);
```

**Step 5: Clean up ring buffer in `stopAudioCapture`**

Inside `stopAudioCapture`, inside the `if (!capturing) return;` block but before setting `capturing = false`, or at the end alongside other cleanup, add:

```typescript
  ringBuffer?.reset();
  ringBuffer = null;
  captureStartTime = 0;
```

**Step 6: Commit**

```bash
git add desktop/src/main/audio/audio-capture.ts
git commit -m "feat: add PCM16 ring buffer for speaker diarization audio segments"
```

---

### Task 5: Integrate Speaker Diarization into ASR Handlers

**Files:**
- Modify: `desktop/src/main/ipc/asr-handlers.ts`

**Step 1: Add imports at top of asr-handlers.ts**

```typescript
import { startSpk, stopSpk, isSpkRunning, extractSpeakerEmbedding, classifySpeaker } from "../asr/speaker-process";
import { getRingBuffer, getCaptureStartTime } from "../audio/audio-capture";
import { getModelManager } from "../models/model-manager";
```

(Note: `getModelManager` may already be imported — check.)

**Step 2: Inside `registerAsrHandlers`, add speaker model initialization**

Inside the `ipcMain.handle("asr:start-transcription", ...)` handler, after the line `startVosk(model.path, mainWindow);`, add:

```typescript
    const spkModelPath = settings.speechToText.speakerModel;
    const maxSpeakers = settings.speechToText.maxSpeakers ?? 4;
    if (spkModelPath) {
      startSpk(spkModelPath, maxSpeakers);
    }
```

**Step 3: Stop SPK on transcription stop**

Inside `ipcMain.handle("asr:stop-transcription", ...)`, after `stopVosk();`, add:

```typescript
    stopSpk();
```

**Step 4: Add speaker extraction in the final-segment handler (inside `mainWindow.webContents.send =` override)**

Inside the `if (segment.isFinal) { ... }` block, after we have the `lineId` and before we start building `pendingText`, add speaker extraction logic. The key is that we need the timestamp range for this segment. Since `feedAudio` in `vosk-process.ts` sends `Date.now()` as timestamp, we can use the capture start time from the ring buffer.

Modify the final-segment handling. After:
```typescript
        const lineId = `seg-${segmentCounter++}`;
        segment.id = lineId;
```

But BEFORE:
```typescript
        if (pendingText) {
```

Add:

```typescript
        // Extract speaker embedding from ring buffer
        let speakerId: string | undefined;
        let speakerLabel: string | undefined;
        let speakerColor: string | undefined;
        if (isSpkRunning()) {
          const rb = getRingBuffer();
          const startTime = getCaptureStartTime();
          if (rb && startTime > 0) {
            // Use a sliding window: extract the last 2s of audio before this final result
            const endMs = Date.now() - startTime;
            const startMs = Math.max(0, endMs - 2000);
            const audioSegment = rb.extractSegment(startMs, endMs);
            const embedding = extractSpeakerEmbedding(audioSegment);
            if (embedding) {
              const speaker = classifySpeaker(embedding);
              if (speaker) {
                speakerId = speaker.speakerId;
                speakerLabel = speaker.speakerLabel;
                speakerColor = speaker.speakerColor;
              }
            }
          }
        }
```

**Step 5: Attach speaker info to the transcript line when flushing**

Inside `flushPending`, modify the `line` object to include speaker info. Replace:

```typescript
    const line = {
      id: pendingLineId,
      text: pendingText.trim(),
      isFinal: true,
      timestamp: Date.now(),
    };
```

With the line needing to carry the speaker info that was captured during the segment arrival. Since `flushPending` runs asynchronously (via timeout), we need to store the speaker info per-line.

Better approach: keep a `Map<string, {speakerId, speakerLabel, speakerColor}>` per lineId to decouple speaker extraction from flush.

Add at the top of `registerAsrHandlers` (near other state variables):

```typescript
  const speakerById: Map<string, { speakerId: string; speakerLabel: string; speakerColor: string }> = new Map();
```

Then in the final-segment handler, after extracting speaker info, store it:

```typescript
        if (speakerId) {
          speakerById.set(lineId, { speakerId, speakerLabel: speakerLabel!, speakerColor: speakerColor! });
        }
```

And in `flushPending`, after building the `line` object, add speaker info:

```typescript
    const speakerInfo = speakerById.get(pendingLineId);
    if (speakerInfo) {
      line.speakerId = speakerInfo.speakerId;
      line.speakerLabel = speakerInfo.speakerLabel;
      line.speakerColor = speakerInfo.speakerColor;
      speakerById.delete(pendingLineId);
    }
```

**Step 6: Commit**

```bash
git add desktop/src/main/ipc/asr-handlers.ts
git commit -m "feat: integrate speaker diarization into ASR pipeline"
```

---

### Task 6: Add vosk-model-spk-0.4 to Model Catalog

**Files:**
- Modify: `desktop/src/main/models/model-source-catalog.ts`
- Modify: `desktop/src/main/models/model-manager.ts` — add method to find SPK model

**Step 1: Add speaker model to the catalog array in model-source-catalog.ts**

Right after the last entry in `MODEL_CATALOG` (the Vietnamese model), add:

```typescript
  {
    id: "vosk-model-spk-0.4",
    name: "Speaker Identification",
    description: "Speaker diarization model, works for all languages. ~50MB.",
    size: "50 MB",
    sizeBytes: 50_000_000,
    language: "multi",
    filename: "vosk-model-spk-0.4.zip",
    url: "https://alphacephei.com/vosk/models/vosk-model-spk-0.4.zip",
  },
```

**Step 2: Add a method to model-manager.ts to find the SPK model path**

```typescript
  getSpkModel(): VoskModel | null {
    return this.listModels().find((m) => m.id === "vosk-model-spk-0.4") ?? null;
  }
```

**Step 3: Update `asr:start-transcription` in asr-handlers.ts to auto-detect SPK model**

Replace the manual `settings.speechToText.speakerModel` lookup with auto-detection:

```typescript
    // Auto-detect speaker model from installed models
    const spkModel = mm.getSpkModel();
    const spkModelPath = spkModel?.downloaded ? spkModel.path : settings.speechToText.speakerModel;
    const maxSpeakers = settings.speechToText.maxSpeakers ?? 4;
    if (spkModelPath) {
      startSpk(spkModelPath, maxSpeakers);
    }
```

**Step 4: Commit**

```bash
git add desktop/src/main/models/model-source-catalog.ts desktop/src/main/models/model-manager.ts desktop/src/main/ipc/asr-handlers.ts
git commit -m "feat: add vosk-model-spk-0.4 to model catalog"
```

---

### Task 7: Update Settings Types and Defaults

**Files:**
- Modify: `desktop/src/main/settings/settings-store.ts`

**Step 1: Add fields to `SpeechToTextSettings` interface**

```typescript
export interface SpeechToTextSettings {
  selectedModel: string;
  sourceLanguage: string;
  speakerModel?: string;
  maxSpeakers?: number;
}
```

**Step 2: Add defaults in `DEFAULTS`**

```typescript
  speechToText: {
    selectedModel: "",
    sourceLanguage: "en",
    speakerModel: "",
    maxSpeakers: 4,
  },
```

**Step 3: Commit**

```bash
git add desktop/src/main/settings/settings-store.ts
git commit -m "feat: add speakerModel and maxSpeakers settings"
```

---

### Task 8: Update TypeScript Types

**Files:**
- Modify: `desktop/src/types/electron-api.d.ts`

**Step 1: Add speaker fields to `TranscriptSegment`**

```typescript
export interface TranscriptSegment {
  id: string;
  text: string;
  isFinal: boolean;
  timestamp: number;
  speakerId?: string;
  speakerLabel?: string;
  speakerColor?: string;
}
```

**Step 2: Add speaker fields to `TranscriptLine`**

```typescript
export interface TranscriptLine {
  id: string;
  text: string;
  translatedText?: string;
  timestamp: number;
  isFinal: boolean;
  speakerId?: string;
  speakerLabel?: string;
  speakerColor?: string;
}
```

**Step 3: Commit**

```bash
git add desktop/src/types/electron-api.d.ts
git commit -m "feat: add speakerId/speakerLabel/speakerColor to transcript types"
```

---

### Task 9: Add maxSpeakers Selector to SpeechSettings

**Files:**
- Modify: `desktop/src/components/settings/SpeechSettings.tsx`

**Step 1: Add maxSpeakers dropdown**

After the source language `Select` group (right before the closing `</SettingsSection>` for "Speech-to-Text Model"), add:

```jsx
        <SettingsField label="Max speakers" helper="Maximum number of speakers to detect (2-8)">
          <Select
            value={String(settings.speechToText.maxSpeakers ?? 4)}
            onValueChange={(v) =>
              onUpdate({
                speechToText: { ...settings.speechToText, maxSpeakers: Number(v) },
              })
            }
          >
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {[2, 3, 4, 5, 6, 7, 8].map((n) => (
                <SelectItem key={n} value={String(n)}>{n} speakers</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </SettingsField>
```

**Step 2: Commit**

```bash
git add desktop/src/components/settings/SpeechSettings.tsx
git commit -m "feat: add maxSpeakers selector to Speech Settings"
```

---

### Task 10: Show Speaker Info in Overlay

**Files:**
- Modify: `desktop/src/overlay/OverlayApp.tsx`

**Step 1: Update `TranscriptLine` interface in OverlayApp.tsx**

Add speaker fields to the local interface:

```typescript
interface TranscriptLine {
  id: string;
  text: string;
  translatedText?: string;
  timestamp: number;
  speakerLabel?: string;
  speakerColor?: string;
}
```

**Step 2: Render speaker badge before each line's text**

In the JSX where each line is rendered (`lines.map((line) => ...)`), update the line rendering. Change the text paragraph:

```jsx
            {line.speakerLabel && (
              <span
                className="inline-flex items-center gap-1 mr-2 text-xs font-semibold rounded px-1.5 py-0.5"
                style={{
                  backgroundColor: `${line.speakerColor}22`,
                  color: line.speakerColor,
                }}
              >
                {line.speakerLabel}
              </span>
            )}
```

Insert this `<span>` right before the text `<p>` element content.

So the full line structure becomes:

```jsx
        {lines.map((line) => (
          <div key={line.id} className={`mb-3 ${borderColor} border-b pb-3 last:border-b-0`}>
            <p
              className={`${textColor} font-medium`}
              style={{ fontSize: settings.fontSize, lineHeight: settings.lineHeight }}
            >
              {line.speakerLabel && (
                <span
                  className="inline-flex items-center gap-1 mr-2 text-xs font-semibold rounded px-1.5 py-0.5 align-middle"
                  style={{
                    backgroundColor: `${line.speakerColor}22`,
                    color: line.speakerColor,
                    border: `1px solid ${line.speakerColor}44`,
                  }}
                >
                  {line.speakerLabel}
                </span>
              )}
              {line.text}
            </p>
            ...
          </div>
        ))}
```

**Step 3: Commit**

```bash
git add desktop/src/overlay/OverlayApp.tsx
git commit -m "feat: show speaker label badge in overlay"
```

---

### Task 11: Show Speaker Info in Sessions Page

**Files:**
- Modify: `desktop/src/pages/SessionsPage.tsx`

**Step 1: Add speaker color and label to each transcript line's display**

In the `activeSession.transcript.map((line) => ...)` block, inside the `<p className="text-base">`, add the speaker badge:

```jsx
      {activeSession.transcript.map((line) => {
        const time = new Date(line.timestamp).toLocaleTimeString();
        return (
          <div key={line.id} className="flex gap-4 py-2 border-b border-border/30">
            <span className="text-xs text-muted-foreground font-mono shrink-0 pt-0.5 w-16">
              {time}
            </span>
            <div className="flex-1 min-w-0">
              <p className="text-base">
                {"speakerLabel" in line && line.speakerLabel && (
                  <span
                    className="inline-flex items-center gap-1 mr-2 text-xs font-semibold rounded px-1.5 py-0.5 align-middle"
                    style={{
                      backgroundColor: `${(line as any).speakerColor}22`,
                      color: (line as any).speakerColor,
                      border: `1px solid ${(line as any).speakerColor}44`,
                    }}
                  >
                    {(line as any).speakerLabel}
                  </span>
                )}
                {line.text}
              </p>
              {line.translatedText && (
                <p className="text-sm text-muted-foreground mt-0.5">{line.translatedText}</p>
              )}
            </div>
          </div>
        );
      })}
```

**Step 2: Commit**

```bash
git add desktop/src/pages/SessionsPage.tsx
git commit -m "feat: show speaker label badge in sessions transcript viewer"
```

---

### Task 12: Include Speaker Info in TXT Export

**Files:**
- Modify: `desktop/src/main/sessions/session-storage.ts`

**Step 1: Update `exportAsTxt` to include speaker labels**

```typescript
  exportAsTxt(sessionId: string, destPath: string): void {
    const lines = this.getTranscript(sessionId);
    const text = lines
      .filter((l) => l.isFinal)
      .map((l) => {
        const ts = new Date(l.timestamp).toLocaleTimeString();
        const speaker = (l as any).speakerLabel ? `[${(l as any).speakerLabel}] ` : "";
        let line = `[${ts}] ${speaker}${l.text}`;
        if (l.translatedText) line += `\n         ${l.translatedText}`;
        return line;
      })
      .join("\n\n");
    fs.writeFileSync(destPath, text, "utf-8");
  }
```

**Step 2: Commit**

```bash
git add desktop/src/main/sessions/session-storage.ts
git commit -m "feat: include speaker label in TXT export"
```

---

### Task 13: TypeScript Compilation Check + E2E Verification

**Step 1: Run TypeScript check**

```bash
cd desktop && npx tsc --noEmit
```

Expected: No type errors.

**Step 2: Run ESLint**

```bash
cd desktop && npx eslint src/main/asr/vosk-spk-bindings.ts src/main/asr/speaker-process.ts src/main/asr/speaker-cluster.ts src/main/audio/audio-capture.ts src/main/ipc/asr-handlers.ts
```

**Step 3: Manual Test Checklist**

| # | Scenario | Expected Result |
|---|---|---|
| 1 | Download `vosk-model-spk-0.4` via ModelDownloadDialog | Model appears in installed models list |
| 2 | Capture audio with 2 alternating speakers | Overlay shows "Speaker 1" (red) and "Speaker 2" (teal) badges before text lines |
| 3 | Capture with 1 speaker only | All lines share the same speaker badge |
| 4 | No SPK model installed | ASR works normally, no speaker badge appears |
| 5 | View session in SessionsPage | Speaker labels visible in transcript viewer |
| 6 | Export session as TXT | File includes `[Speaker 1]` prefix before each line |
| 7 | Stop capture, start again | Speaker clusters reset (starts fresh from Speaker 1) |

---

## Self-Review Checklist

1. **Spec coverage**: All spec requirements covered — SPK bindings (Task 1), clustering (Task 2), model lifecycle (Task 3), ring buffer (Task 4), ASR integration (Task 5), model catalog (Task 6), settings (Task 7), types (Task 8), SpeechSettings UI (Task 9), overlay (Task 10), sessions (Task 11), export (Task 12), verification (Task 13).

2. **No placeholders**: Every step has actual code, no TBD/TODO/fill-in-the-blank.

3. **Type consistency**: `SpeakerIdentity` defined in Task 2 matches usage in Task 3. `TranscriptLine` extended in Task 8 matches usage in Tasks 10, 11, 12. All interface names consistent across tasks.

4. **Ambiguity**: All thresholds and defaults are explicit (cosine > 0.7, buffer 5s, maxSpeakers default 4).
