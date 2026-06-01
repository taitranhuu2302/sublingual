# Vosk Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Whisper CLI batch processing with Vosk in-process streaming ASR for real-time transcription.

**Architecture:** Vosk runs in-process via native Node.js bindings. Audio PCM16 data is fed directly into `acceptWaveform()`, returning partial results immediately and final results when silence is detected — no batching, no temp files, no subprocess spawning.

**Tech Stack:** Electron + Vite + TypeScript, `vosk` npm package (native bindings), `adm-zip` for model extraction

---

### Task 1: Install Dependencies

**Files:**
- Modify: `desktop/package.json`
- Run: `pnpm install` (or npm)

- [ ] **Step 1: Add vosk and adm-zip to package.json**

Edit `desktop/package.json` — add to `dependencies`:
```json
"adm-zip": "^0.5.16",
"vosk": "^0.3.39"
```

- [ ] **Step 2: Install**

Run: `pnpm install` or `npm install`
Expected: packages install successfully, native binaries downloaded for platform

- [ ] **Step 3: Commit**

```bash
git add desktop/package.json desktop/pnpm-lock.yaml
git commit -m "chore: add vosk and adm-zip dependencies"
```

---

### Task 2: Create vosk-process.ts (new Vosk ASR engine)

**Files:**
- Create: `desktop/src/main/asr/vosk-process.ts`
- Delete: `desktop/src/main/asr/whisper-process.ts`
- Delete: `desktop/src/main/asr/whisper-types.ts`

- [ ] **Step 1: Create vosk-process.ts**

```typescript
import { BrowserWindow } from "electron";
import * as vosk from "vosk";

let model: vosk.Model | null = null;
let recognizer: vosk.Recognizer | null = null;
let mainWindowRef: BrowserWindow | null = null;

vosk.setLogLevel(-1);

export function startVosk(modelPath: string, mainWindow: BrowserWindow) {
  mainWindowRef = mainWindow;

  try {
    model = new vosk.Model(modelPath);
    recognizer = new vosk.Recognizer({ model, sampleRate: 16000 });
  } catch (err) {
    console.error("[vosk] Failed to initialize:", err);
    throw err;
  }
}

export function feedAudio(pcmData: Buffer) {
  if (!recognizer || !mainWindowRef || mainWindowRef.isDestroyed()) return;

  try {
    const isFinal = recognizer.acceptWaveform(pcmData);

    if (isFinal) {
      const result = recognizer.result();
      if (result.text && result.text.trim()) {
        mainWindowRef.webContents.send("asr:transcript", {
          text: result.text.trim(),
          isFinal: true,
          timestamp: Date.now(),
        });
      }
    } else {
      const partial = recognizer.partialResult();
      if (partial.partial && partial.partial.trim()) {
        mainWindowRef.webContents.send("asr:transcript", {
          text: partial.partial.trim(),
          isFinal: false,
          timestamp: Date.now(),
        });
      }
    }
  } catch (err) {
    console.error("[vosk] acceptWaveform error:", err);
  }
}

export function stopVosk() {
  if (recognizer) {
    try {
      const final = recognizer.finalResult();
      if (final.text && final.text.trim() && mainWindowRef && !mainWindowRef.isDestroyed()) {
        mainWindowRef.webContents.send("asr:transcript", {
          text: final.text.trim(),
          isFinal: true,
          timestamp: Date.now(),
        });
      }
    } catch (err) {
      console.error("[vosk] finalResult error:", err);
    }
    recognizer.free();
    recognizer = null;
  }

  if (model) {
    model.free();
    model = null;
  }

  mainWindowRef = null;
}
```

- [ ] **Step 2: Delete old whisper files**

Delete `desktop/src/main/asr/whisper-process.ts` and `desktop/src/main/asr/whisper-types.ts`.

- [ ] **Step 3: Commit**

```bash
git add desktop/src/main/asr/vosk-process.ts
git rm desktop/src/main/asr/whisper-process.ts desktop/src/main/asr/whisper-types.ts
git commit -m "feat: add vosk-process.ts, remove whisper-process"
```

---

### Task 3: Update audio-capture.ts import

**Files:**
- Modify: `desktop/src/main/audio/audio-capture.ts`

- [ ] **Step 1: Change import path**

Edit `desktop/src/main/audio/audio-capture.ts` line 3:
```typescript
// Before:
import { feedAudio } from "../asr/whisper-process";
// After:
import { feedAudio } from "../asr/vosk-process";
```

- [ ] **Step 2: Commit**

```bash
git add desktop/src/main/audio/audio-capture.ts
git commit -m "fix: update audio-capture import to vosk-process"
```

---

### Task 4: Update asr-handlers.ts

**Files:**
- Modify: `desktop/src/main/ipc/asr-handlers.ts`

- [ ] **Step 1: Update imports and start/stop calls**

Edit `desktop/src/main/ipc/asr-handlers.ts`:

Line 4 — change import:
```typescript
// Before:
import { startWhisper, stopWhisper } from "../asr/whisper-process";
// After:
import { startVosk, stopVosk } from "../asr/vosk-process";
```

Lines 103-106 — change start call:
```typescript
// Before:
    startWhisper(
      { modelPath: model.path, language: settings.speechToText.sourceLanguage },
      mainWindow,
    );
// After:
    startVosk(model.path, mainWindow);
```

Line 111 — change stop call:
```typescript
// Before:
    stopWhisper();
// After:
    stopVosk();
```

- [ ] **Step 2: Forward partial transcripts to overlay**

In the `mainWindow.webContents.send` override (line 115), add handling for `isFinal === false`:

After the `if (channel === "asr:transcript")` block, the current code only handles `segment.isFinal` being true. We need to also forward partial segments to the overlay. Find the block starting at line 115 and replace it:

```typescript
  mainWindow.webContents.send = (channel: string, ...args: unknown[]) => {
    if (channel === "asr:transcript") {
      const segment = args[0] as {
        text: string;
        isFinal: boolean;
        timestamp: number;
        id?: string;
      };

      if (!segment?.text) {
        originalSend(channel, ...args);
        return;
      }

      if (segment.isFinal) {
        // Final: sentence merging + translation pipeline
        const lineId = `seg-${segmentCounter++}`;
        segment.id = lineId;

        if (pendingText) {
          pendingText = pendingText + " " + segment.text;
        } else {
          pendingText = segment.text;
          pendingLineId = lineId;
        }

        if (isSentenceComplete(pendingText)) {
          flushPending();
        } else {
          if (flushTimer) clearTimeout(flushTimer);
          flushTimer = setTimeout(() => {
            flushPending();
          }, FLUSH_TIMEOUT_MS);
        }
      } else {
        // Partial: forward directly to renderer and overlay
        const overlay = getOverlayManager();
        if (overlay.isVisible()) {
          overlay.sendToOverlay("overlay:partial-update", {
            text: segment.text,
          });
        }
      }
    }
    originalSend(channel, ...args);
  };
```

- [ ] **Step 3: Commit**

```bash
git add desktop/src/main/ipc/asr-handlers.ts
git commit -m "feat: update asr-handlers for vosk streaming with partial overlays"
```

---

### Task 5: Update model-source-catalog.ts (Vosk models)

**Files:**
- Modify: `desktop/src/main/models/model-source-catalog.ts`

- [ ] **Step 1: Replace whisper model catalog with Vosk models**

Replace the entire `MODEL_CATALOG` array:

```typescript
const MODEL_CATALOG: ModelSource[] = [
  {
    id: "vosk-model-small-en-us-0.15",
    name: "English (Small)",
    description: "Lightweight US English model, ideal for desktop. ~40MB.",
    size: "40 MB",
    sizeBytes: 40_000_000,
    language: "en",
    filename: "vosk-model-small-en-us-0.15.zip",
    url: "https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip",
  },
  {
    id: "vosk-model-en-us-0.22",
    name: "English (Accurate)",
    description: "Accurate US English model for servers and high-end desktops. ~1.8GB.",
    size: "1.8 GB",
    sizeBytes: 1_800_000_000,
    language: "en",
    filename: "vosk-model-en-us-0.22.zip",
    url: "https://alphacephei.com/vosk/models/vosk-model-en-us-0.22.zip",
  },
  {
    id: "vosk-model-en-us-0.22-lgraph",
    name: "English (Dynamic)",
    description: "Big US English model with dynamic graph. ~128MB.",
    size: "128 MB",
    sizeBytes: 128_000_000,
    language: "en",
    filename: "vosk-model-en-us-0.22-lgraph.zip",
    url: "https://alphacephei.com/vosk/models/vosk-model-en-us-0.22-lgraph.zip",
  },
  {
    id: "vosk-model-small-vn-0.4",
    name: "Vietnamese (Small)",
    description: "Lightweight Vietnamese model. ~32MB.",
    size: "32 MB",
    sizeBytes: 32_000_000,
    language: "vi",
    filename: "vosk-model-small-vn-0.4.zip",
    url: "https://alphacephei.com/vosk/models/vosk-model-small-vn-0.4.zip",
  },
  {
    id: "vosk-model-vn-0.4",
    name: "Vietnamese",
    description: "Bigger Vietnamese model for server. ~78MB.",
    size: "78 MB",
    sizeBytes: 78_000_000,
    language: "vi",
    filename: "vosk-model-vn-0.4.zip",
    url: "https://alphacephei.com/vosk/models/vosk-model-vn-0.4.zip",
  },
];
```

- [ ] **Step 2: Update `getInstallableModels` to detect installed Vosk models**

Vosk models are zip files that extract to directories. Detection checks for the directory existing (without `.zip` extension):

```typescript
export function getInstallableModels(): InstallableModel[] {
  const modelsDir = getModelsDir();
  return MODEL_CATALOG.map((source) => {
    // Vosk models are extracted to directories (filename without .zip)
    const dirName = source.filename.replace(/\.zip$/, "");
    const localPath = path.join(modelsDir, dirName);
    // Detect by checking for the model marker file
    const isInstalled = fs.existsSync(path.join(localPath, "am", "final.mdl"));
    return {
      ...source,
      isInstalled,
      localPath,
    };
  });
}
```

- [ ] **Step 3: Commit**

```bash
git add desktop/src/main/models/model-source-catalog.ts
git commit -m "feat: replace whisper model catalog with vosk models"
```

---

### Task 6: Update model-manager.ts (Vosk model detection)

**Files:**
- Modify: `desktop/src/main/models/model-manager.ts`

- [ ] **Step 1: Replace WhisperModel interface and logic**

Replace the entire file content:

```typescript
import { app } from "electron";
import fs from "fs";
import path from "path";
import os from "os";
import { getSettings, setSettings } from "../settings/settings-store";
import { getInstallableModels } from "./model-source-catalog";

export interface VoskModel {
  id: string;
  name: string;
  size: string;
  path: string;
  language: string;
  downloaded: boolean;
}

const MODELS_DIR = path.join(os.homedir(), ".sublingual", "models");

class ModelManager {
  listModels(): VoskModel[] {
    return getInstallableModels().map((m) => ({
      id: m.id,
      name: m.name,
      size: m.size,
      language: m.language,
      path: m.localPath,
      downloaded: m.isInstalled,
    }));
  }

  selectModel(modelId: string): void {
    const settings = getSettings();
    setSettings({ speechToText: { ...settings.speechToText, selectedModel: modelId } });
  }

  getSelectedModel(): VoskModel | null {
    const settings = getSettings();
    return this.listModels().find((m) => m.id === settings.speechToText.selectedModel) ?? null;
  }
}

let instance: ModelManager | null = null;
export function getModelManager(): ModelManager {
  if (!instance) instance = new ModelManager();
  return instance;
}
```

- [ ] **Step 2: Commit**

```bash
git add desktop/src/main/models/model-manager.ts
git commit -m "feat: update model-manager for vosk models"
```

---

### Task 7: Update model-downloader.ts (zip extraction)

**Files:**
- Modify: `desktop/src/main/models/model-downloader.ts`

- [ ] **Step 1: Update downloadModel to handle zip extraction**

Import `adm-zip` at top:
```typescript
import AdmZip from "adm-zip";
```

Replace the download logic (after the file is downloaded, extract and clean up).

Key change: after `fs.renameSync(tempPath, destPath)`, extract the zip and delete it:

Replace the file content:

```typescript
import fs from "fs";
import path from "path";
import { BrowserWindow } from "electron";
import { getModelsDir, getModelSource } from "./model-source-catalog";
import AdmZip from "adm-zip";

export interface DownloadProgress {
  modelId: string;
  percent: number;
  status: "downloading" | "completed" | "error" | "cancelled";
  error?: string;
}

let activeAbortController: AbortController | null = null;
let activeModelId: string | null = null;

export async function downloadModel(modelId: string, mainWindow: BrowserWindow): Promise<void> {
  const source = getModelSource(modelId);
  if (!source) throw new Error(`Unknown model: ${modelId}`);

  const modelsDir = getModelsDir();
  if (!fs.existsSync(modelsDir)) {
    fs.mkdirSync(modelsDir, { recursive: true });
  }

  // For Vosk, filename is a zip, target is a directory
  const zipPath = path.join(modelsDir, source.filename);
  const extractDir = path.join(modelsDir, source.filename.replace(/\.zip$/, ""));

  // Skip if already installed
  if (fs.existsSync(path.join(extractDir, "am", "final.mdl"))) {
    return;
  }

  activeAbortController = new AbortController();
  activeModelId = modelId;

  const sendProgress = (progress: DownloadProgress) => {
    if (!mainWindow.isDestroyed()) {
      mainWindow.webContents.send("models:download-progress", progress);
    }
  };

  try {
    sendProgress({ modelId, percent: 0, status: "downloading" });

    const response = await fetch(source.url, {
      signal: activeAbortController.signal,
    });

    if (!response.ok) {
      throw new Error(`Download failed: ${response.status} ${response.statusText}`);
    }

    const contentLength = Number(response.headers.get("content-length")) || source.sizeBytes;
    const reader = response.body?.getReader();
    if (!reader) throw new Error("No response body");

    const fileStream = fs.createWriteStream(zipPath);
    let downloaded = 0;
    let lastReportedPercent = -1;
    let done = false;

    while (!done) {
      const chunk = await reader.read();
      done = chunk.done;
      if (done) break;

      fileStream.write(Buffer.from(chunk.value));
      downloaded += chunk.value.length;

      const percent = Math.min(99, Math.floor((downloaded / contentLength) * 100));
      if (percent !== lastReportedPercent) {
        lastReportedPercent = percent;
        sendProgress({ modelId, percent, status: "downloading" });
      }
    }

    fileStream.end();
    await new Promise<void>((resolve, reject) => {
      fileStream.on("finish", resolve);
      fileStream.on("error", reject);
    });

    // Extract zip (AdmZip is synchronous, this completes quickly)

    const zip = new AdmZip(zipPath);
    zip.extractAllTo(extractDir, true);

    // Remove zip file
    fs.unlinkSync(zipPath);

    sendProgress({ modelId, percent: 100, status: "completed" });
  } catch (err: unknown) {
    // Clean up zip if it exists
    if (fs.existsSync(zipPath)) {
      fs.unlinkSync(zipPath);
    }
    // Clean up partial extract
    if (fs.existsSync(extractDir)) {
      fs.rmSync(extractDir, { recursive: true, force: true });
    }

    if (err instanceof Error && err.name === "AbortError") {
      sendProgress({ modelId, percent: 0, status: "cancelled" });
    } else {
      const message = err instanceof Error ? err.message : String(err);
      sendProgress({ modelId, percent: 0, status: "error", error: message });
      throw err;
    }
  } finally {
    activeAbortController = null;
    activeModelId = null;
  }
}

export function cancelDownload(): void {
  if (activeAbortController) {
    activeAbortController.abort();
  }
}

export function getActiveDownloadModelId(): string | null {
  return activeModelId;
}
```

- [ ] **Step 2: Commit**

```bash
git add desktop/src/main/models/model-downloader.ts
git commit -m "feat: update model-downloader for vosk zip extraction"
```

---

### Task 8: Update settings-store.ts (remove chunkPreset)

**Files:**
- Modify: `desktop/src/main/settings/settings-store.ts`

- [ ] **Step 1: Remove realtimeChunkPreset from SpeechToTextSettings**

Edit line 23-27:
```typescript
// Before:
export interface SpeechToTextSettings {
  selectedModel: string;
  realtimeChunkPreset: "Fast" | "Balanced" | "Accurate";
  sourceLanguage: string;
}

// After:
export interface SpeechToTextSettings {
  selectedModel: string;
  sourceLanguage: string;
}
```

Edit line 70-74 (DEFAULTS):
```typescript
// Before:
  speechToText: {
    selectedModel: "",
    realtimeChunkPreset: "Balanced",
    sourceLanguage: "en",
  },

// After:
  speechToText: {
    selectedModel: "",
    sourceLanguage: "en",
  },
```

- [ ] **Step 2: Commit**

```bash
git add desktop/src/main/settings/settings-store.ts
git commit -m "feat: remove realtimeChunkPreset from settings"
```

---

### Task 9: Update types (electron-api.d.ts)

**Files:**
- Modify: `desktop/src/types/electron-api.d.ts`

- [ ] **Step 1: Rename WhisperModel → VoskModel**

Line 71-77:
```typescript
// Before:
export interface WhisperModel {
  id: string;
  name: string;
  size: string;
  path: string;
  downloaded: boolean;
}

// After:
export interface VoskModel {
  id: string;
  name: string;
  size: string;
  path: string;
  language: string;
  downloaded: boolean;
}
```

Line 17 — update reference:
```typescript
// Before:
    getModels: () => Promise<WhisperModel[]>;
// After:
    getModels: () => Promise<VoskModel[]>;
```

Line 104 (InstallableModel) — add `language` field already exists, no change needed.

- [ ] **Step 2: Commit**

```bash
git add desktop/src/types/electron-api.d.ts
git commit -m "feat: rename WhisperModel to VoskModel with language field"
```

---

### Task 10: Update SpeechSettings.tsx (UI)

**Files:**
- Modify: `desktop/src/components/settings/SpeechSettings.tsx`

- [ ] **Step 1: Update import and model list UI**

Line 16 — change import:
```typescript
// Before:
import type { AppSettings, WhisperModel } from "@/types/electron-api";
// After:
import type { AppSettings, VoskModel } from "@/types/electron-api";
```

Line 35 — change model state type:
```typescript
// Before:
  const [models, setModels] = useState<WhisperModel[]>([]);
// After:
  const [models, setModels] = useState<VoskModel[]>([]);
```

Lines 68-94 — remove "Chunk preset" RadioGroup section entirely (the entire `<SettingsField label="Chunk preset">` block from line 68 to line 94).

- [ ] **Step 2: Commit**

```bash
git add desktop/src/components/settings/SpeechSettings.tsx
git commit -m "feat: update SpeechSettings for vosk models, remove chunk preset"
```

---

### Task 11: Update HomePage.tsx (UI)

**Files:**
- Modify: `desktop/src/pages/HomePage.tsx`

- [ ] **Step 1: Remove chunkPreset badge and stats card**

Line 88-90 — update model name display (no more ggml- prefix):
```typescript
// Before:
  const modelName = settings.speechToText.selectedModel
    ? settings.speechToText.selectedModel.replace(/^ggml-/, "").replace(/\.bin$/, "")
    : "None";

// After:
  const modelName = settings.speechToText.selectedModel
    ? settings.speechToText.selectedModel.replace(/^vosk-model-/, "").replace(/-/g, " ")
    : "None";
```

Lines 134-143 — remove the chunk ms stats card (the entire third Card in the grid):
```typescript
// Remove these lines:
          <Card>
            <CardContent className="flex flex-col items-center py-4 px-3 gap-1">
              <Activity className="h-5 w-5 text-muted-foreground" />
              <span className="text-2xl font-semibold">
                {settings.speechToText.realtimeChunkPreset === "Fast" ? "500" :
                 settings.speechToText.realtimeChunkPreset === "Accurate" ? "2000" : "1000"}
              </span>
              <span className="text-xs text-muted-foreground">Chunk ms</span>
            </CardContent>
          </Card>
```

Lines 169-171 — remove chunk preset display from session info bar:
```typescript
// Remove:
          <span className="flex items-center gap-1">
            <Clock className="h-3 w-3" /> {settings.speechToText.realtimeChunkPreset}
          </span>
```

Remove `Clock` and `Activity` from the lucide-react import on line 7:
```typescript
// Before:
import { Clock, MessageSquare, Languages, Mic, Activity } from "lucide-react";
// After:
import { MessageSquare, Languages, Mic } from "lucide-react";
```

Change the grid class from `grid-cols-3` to `grid-cols-2` on line 119:
```typescript
// Before:
        <div className="grid grid-cols-3 gap-4 w-full max-w-lg">
// After:
        <div className="grid grid-cols-2 gap-4 w-full max-w-lg">
```

- [ ] **Step 2: Commit**

```bash
git add desktop/src/pages/HomePage.tsx
git commit -m "feat: update HomePage for vosk, remove chunk preset UI"
```

---

### Task 12: Configure Vite for native modules

**Files:**
- Modify: `desktop/vite.main.config.ts`

- [ ] **Step 1: Externalize vosk and adm-zip from Vite bundle**

```typescript
import { defineConfig } from "vite";
import path from "node:path";

export default defineConfig({
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  build: {
    rollupOptions: {
      external: ["vosk", "adm-zip"],
    },
  },
});
```

- [ ] **Step 2: Commit**

```bash
git add desktop/vite.main.config.ts
git commit -m "fix: externalize vosk and adm-zip from vite bundle"
```

---

### Verification

After all tasks, verify the migration:

1. **Build**: `pnpm start` — app launches without errors
2. **Model install**: Open Settings → Install Models → download a Vosk model
3. **Transcription**: Start capture → speak → verify partial text appears in real-time → verify final text on silence
4. **Overlay**: Verify partial text shows in overlay (italic, opacity-70) and final text appears as normal line
5. **Translation**: Verify translation still works end-to-end
6. **Vietnamese**: Test with `vosk-model-vn-0.4` or `vosk-model-small-vn-0.4`
