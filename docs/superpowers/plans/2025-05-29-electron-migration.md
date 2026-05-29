# Sublingual Electron Migration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate core functionality (audio capture → ASR → display) from the C#/Avalonia `src/` app to the Electron `desktop/` app.

**Architecture:**
```
Electron App (main process)
├── Audio Capture Module (native addon)
│   ├── Windows: WASAPI loopback + mic (node native addon)
│   └── macOS: ScreenCaptureKit (.dylib via ffi-napi)
├── ASR Engine (whisper.cpp)
│   ├── Runs as child process (stdin/stdout streaming)
│   └── Or wrapper Node native addon
└── IPC (main ↔ renderer)
    └── contextBridge to stream text real-time
```

**Tech Stack:** Electron 42, React 19, TypeScript, Vite, shadcn/ui, Tailwind CSS 4, node-addon-api (WASAPI), ffi-napi (macOS), whisper.cpp (child process)

**Scope exclusions:**
- ❌ Session audio playback (deferred — requires audio file serving from main process)

---

## File Structure

```
desktop/src/
├── main.ts                          — Main process entry, window creation, IPC registration
├── preload.ts                       — contextBridge: expose audio/ASR/translation/overlay APIs to renderer
├── main/
│   ├── ipc-handlers.ts             — IPC handler registration (audio, ASR, settings, translation, overlay)
│   ├── audio/
│   │   ├── audio-capture.ts        — Platform-agnostic audio capture orchestrator
│   │   ├── wasapi-addon.ts         — Windows WASAPI native addon loader
│   │   └── screencapture-mac.ts    — macOS ScreenCaptureKit via ffi-napi
│   ├── asr/
│   │   ├── whisper-process.ts      — whisper.cpp child process manager (spawn, stream, kill)
│   │   └── whisper-types.ts        — Types for whisper output (segments, timestamps)
│   ├── models/
│   │   ├── model-manager.ts        — Download/list/select whisper models
│   │   ├── model-source-catalog.ts — Downloadable model manifest (from JSON config)
│   │   └── model-downloader.ts     — Download model zip, report progress, import
│   ├── translation/
│   │   ├── translation-service.ts  — Simple translation: pick provider, call, return result
│   │   ├── providers/
│   │   │   ├── types.ts            — ITranslationProvider interface, TranslationRequest/Result types
│   │   │   ├── google-free.ts      — Google Translate free API provider
│   │   │   └── translate-local.ts  — Local TranslateService provider (localhost:3333)
│   ├── overlay/
│   │   └── overlay-window.ts       — Overlay BrowserWindow manager (create, show/hide, position, sync settings)
│   └── settings/
│       └── settings-store.ts       — Persistent settings (all categories)
├── renderer.ts                      — React app entry
├── App.tsx                          — Router setup
├── lib/
│   └── utils.ts                    — cn() helper (existing)
├── hooks/
│   ├── use-audio-capture.ts        — React hook wrapping IPC audio controls
│   ├── use-transcription.ts        — React hook for streaming ASR results
│   ├── use-settings.ts             — React hook for app settings
│   ├── use-translation.ts          — React hook for translation state & results
│   └── use-model-download.ts       — React hook for model download progress
├── components/
│   ├── Layout.tsx                  — App shell (existing, will be updated)
│   ├── ui/                         — shadcn components
│   ├── SubtitleOverlay.tsx         — Real-time subtitle display (inline, for HomePage)
│   ├── AudioSourceSelector.tsx     — Dropdown to pick mic/system audio
│   ├── ModelSelector.tsx           — Dropdown to pick whisper model
│   └── ModelDownloadDialog.tsx     — Dialog to browse & download models with progress
├── pages/
│   ├── HomePage.tsx                — Main capture + subtitle view
│   └── SettingsPage.tsx            — Settings with tabs (General, Speech, Translation, Overlay)
├── overlay/
│   ├── overlay-renderer.ts         — Separate renderer entry for overlay window
│   ├── overlay-preload.ts          — Preload for overlay window
│   └── OverlayApp.tsx              — Overlay React root (subtitle lines + translation + theme)
└── types/
    └── electron-api.d.ts           — Type declarations for window.electronAPI
```

**Native addons (separate build):**
```
desktop/native/
├── wasapi-capture/                  — Windows WASAPI (node-addon-api)
│   ├── binding.gyp
│   ├── src/wasapi_capture.cc
│   └── index.d.ts
├── screencapture-mac/               — macOS ScreenCaptureKit (copy/symlink of existing build)
│   └── libScreenCaptureKitBridge.dylib
└── README.md
```

> **macOS native build exists at:** `native/macos/ScreenCaptureKitBridge/` (repo root).
> Build output: `native/macos/ScreenCaptureKitBridge/build/libScreenCaptureKitBridge.dylib`
> Copy or symlink it to `desktop/native/screencapture-mac/libScreenCaptureKitBridge.dylib` after build.

---

## Task 1: Fix App Scaffold & Router

**Files:**
- Create: `src/App.tsx`
- Modify: `src/renderer.ts`
- Modify: `src/components/Layout.tsx`
- Create: `src/types/electron-api.d.ts`

- [ ] **Step 1: Create App.tsx with router**

```tsx
// src/App.tsx
import { BrowserRouter, Routes, Route } from "react-router-dom";
import { Layout } from "./components/Layout";
import { HomePage } from "./pages/HomePage";
import { SettingsPage } from "./pages/SettingsPage";

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
```

- [ ] **Step 2: Fix renderer.ts**

```tsx
// src/renderer.ts
import "./index.css";
import { createRoot } from "react-dom/client";
import { App } from "./App";

const root = createRoot(document.getElementById("root")!);
root.render(<App />);
```

- [ ] **Step 3: Create electron-api type declarations**

```ts
// src/types/electron-api.d.ts
export interface ElectronAPI {
  audio: {
    getSources: () => Promise<AudioSource[]>;
    startCapture: (sourceId: string) => Promise<void>;
    stopCapture: () => Promise<void>;
    onAudioData: (callback: (data: Float32Array) => void) => () => void;
  };
  asr: {
    getModels: () => Promise<WhisperModel[]>;
    selectModel: (modelId: string) => Promise<void>;
    startTranscription: () => Promise<void>;
    stopTranscription: () => Promise<void>;
    onTranscript: (callback: (segment: TranscriptSegment) => void) => () => void;
  };
  settings: {
    get: () => Promise<AppSettings>;
    set: (settings: Partial<AppSettings>) => Promise<void>;
  };
}

export interface AudioSource {
  id: string;
  name: string;
  type: "microphone" | "system";
}

export interface WhisperModel {
  id: string;
  name: string;
  size: string;
  downloaded: boolean;
}

export interface TranscriptSegment {
  text: string;
  isFinal: boolean;
  timestamp: number;
}

export interface AppSettings {
  language: string;
  modelId: string;
  audioSourceId: string;
}

declare global {
  interface Window {
    electronAPI: ElectronAPI;
  }
}
```

- [ ] **Step 4: Verify app compiles**

Run: `pnpm start` (in desktop/)
Expected: Window opens with Home page

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "fix: scaffold App.tsx, router, and electron API types"
```

---

## Task 2: Preload Bridge & IPC Foundation

**Files:**
- Modify: `src/preload.ts`
- Create: `src/main/ipc-handlers.ts`
- Modify: `src/main.ts`

- [ ] **Step 1: Implement preload.ts with contextBridge**

```ts
// src/preload.ts
import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("electronAPI", {
  audio: {
    getSources: () => ipcRenderer.invoke("audio:get-sources"),
    startCapture: (sourceId: string) => ipcRenderer.invoke("audio:start-capture", sourceId),
    stopCapture: () => ipcRenderer.invoke("audio:stop-capture"),
    onAudioData: (callback: (data: Float32Array) => void) => {
      const handler = (_event: unknown, data: Float32Array) => callback(data);
      ipcRenderer.on("audio:data", handler);
      return () => ipcRenderer.removeListener("audio:data", handler);
    },
  },
  asr: {
    getModels: () => ipcRenderer.invoke("asr:get-models"),
    selectModel: (modelId: string) => ipcRenderer.invoke("asr:select-model", modelId),
    startTranscription: () => ipcRenderer.invoke("asr:start-transcription"),
    stopTranscription: () => ipcRenderer.invoke("asr:stop-transcription"),
    onTranscript: (callback: (segment: { text: string; isFinal: boolean; timestamp: number }) => void) => {
      const handler = (_event: unknown, segment: { text: string; isFinal: boolean; timestamp: number }) => callback(segment);
      ipcRenderer.on("asr:transcript", handler);
      return () => ipcRenderer.removeListener("asr:transcript", handler);
    },
  },
  settings: {
    get: () => ipcRenderer.invoke("settings:get"),
    set: (settings: Record<string, unknown>) => ipcRenderer.invoke("settings:set", settings),
  },
});
```

- [ ] **Step 2: Create IPC handlers skeleton**

```ts
// src/main/ipc-handlers.ts
import { ipcMain, BrowserWindow } from "electron";

export function registerIpcHandlers(mainWindow: BrowserWindow) {
  // Audio
  ipcMain.handle("audio:get-sources", async () => {
    // TODO: Task 3
    return [];
  });
  ipcMain.handle("audio:start-capture", async (_event, sourceId: string) => {
    // TODO: Task 3
  });
  ipcMain.handle("audio:stop-capture", async () => {
    // TODO: Task 3
  });

  // ASR
  ipcMain.handle("asr:get-models", async () => {
    // TODO: Task 4
    return [];
  });
  ipcMain.handle("asr:select-model", async (_event, modelId: string) => {
    // TODO: Task 4
  });
  ipcMain.handle("asr:start-transcription", async () => {
    // TODO: Task 4
  });
  ipcMain.handle("asr:stop-transcription", async () => {
    // TODO: Task 4
  });

  // Settings
  ipcMain.handle("settings:get", async () => {
    // TODO: Task 5
    return { language: "en", modelId: "", audioSourceId: "" };
  });
  ipcMain.handle("settings:set", async (_event, settings: Record<string, unknown>) => {
    // TODO: Task 5
  });
}
```

- [ ] **Step 3: Wire IPC handlers in main.ts**

Add to `main.ts` after window creation:

```ts
import { registerIpcHandlers } from "./main/ipc-handlers";

// Inside createWindow(), after mainWindow is created:
registerIpcHandlers(mainWindow);
```

Also ensure `webPreferences` includes:
```ts
webPreferences: {
  preload: path.join(__dirname, "preload.js"),
  contextIsolation: true,
  nodeIntegration: false,
}
```

- [ ] **Step 4: Verify app starts with IPC bridge active**

Run: `pnpm start`
Expected: No errors in console, `window.electronAPI` available in renderer devtools

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: IPC bridge with contextBridge for audio, ASR, settings"
```

---

## Task 3: Audio Capture Module (Windows WASAPI)

**Files:**
- Create: `src/main/audio/audio-capture.ts`
- Create: `src/main/audio/wasapi-addon.ts`
- Create: `native/wasapi-capture/binding.gyp`
- Create: `native/wasapi-capture/src/wasapi_capture.cc`
- Create: `native/wasapi-capture/index.d.ts`
- Modify: `src/main/ipc-handlers.ts`

- [ ] **Step 1: Design native addon interface**

```ts
// src/main/audio/wasapi-addon.ts
import path from "path";

interface WasapiAddon {
  getDevices(): Array<{ id: string; name: string; isLoopback: boolean }>;
  startCapture(deviceId: string, sampleRate: number, callback: (pcmData: Buffer) => void): void;
  stopCapture(): void;
}

let addon: WasapiAddon | null = null;

export function getWasapiAddon(): WasapiAddon {
  if (!addon) {
    // eslint-disable-next-line @typescript-eslint/no-var-requires
    addon = require(path.join(__dirname, "../../native/wasapi-capture/build/Release/wasapi_capture.node"));
  }
  return addon!;
}
```

- [ ] **Step 2: Create binding.gyp for native addon**

```json
{
  "targets": [
    {
      "target_name": "wasapi_capture",
      "sources": ["src/wasapi_capture.cc"],
      "include_dirs": [
        "<!@(node -p \"require('node-addon-api').include\")"
      ],
      "defines": ["NAPI_DISABLE_CPP_EXCEPTIONS"],
      "libraries": ["-lole32", "-lmmdevapi"],
      "cflags!": ["-fno-exceptions"],
      "cflags_cc!": ["-fno-exceptions"]
    }
  ]
}
```

- [ ] **Step 3: Implement wasapi_capture.cc (WASAPI loopback + mic)**

Create `native/wasapi-capture/src/wasapi_capture.cc` — a Node N-API addon that:
1. Enumerates audio endpoints (render for loopback, capture for mic)
2. Opens a WASAPI capture client on the selected device (loopback mode for system audio)
3. In a background thread, reads PCM packets and calls back into JS with Buffer data
4. Exposes: `getDevices()`, `startCapture(deviceId, sampleRate, callback)`, `stopCapture()`

Key WASAPI calls: `IMMDeviceEnumerator`, `IAudioClient::Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_LOOPBACK)`, `IAudioCaptureClient::GetBuffer`.

> **Note:** This is a non-trivial C++ file (~300 lines). The implementing agent should reference the existing C# WASAPI implementation in `src/Sublingual.Infrastructure/Audio/Capture/Windows/WasapiLoopbackCaptureService.cs` for the capture logic pattern.

- [ ] **Step 4: Create audio-capture orchestrator**

```ts
// src/main/audio/audio-capture.ts
import { BrowserWindow } from "electron";
import { getWasapiAddon } from "./wasapi-addon";
import type { AudioSource } from "../../types/electron-api";

let capturing = false;

export function getAudioSources(): AudioSource[] {
  if (process.platform === "win32") {
    const addon = getWasapiAddon();
    return addon.getDevices().map((d) => ({
      id: d.id,
      name: d.name,
      type: d.isLoopback ? "system" as const : "microphone" as const,
    }));
  }
  if (process.platform === "darwin") {
    // The dylib captures default system output — no device enumeration
    return [{ id: "system-default", name: "System Audio", type: "system" }];
  }
  return [];
}

export function startAudioCapture(sourceId: string, mainWindow: BrowserWindow) {
  if (capturing) return;
  capturing = true;

  if (process.platform === "win32") {
    const addon = getWasapiAddon();
    addon.startCapture(sourceId, 16000, (pcmData: Buffer) => {
      mainWindow.webContents.send("audio:data", new Float32Array(pcmData.buffer));
    });
  }
}

export function stopAudioCapture() {
  if (!capturing) return;
  capturing = false;

  if (process.platform === "win32") {
    const addon = getWasapiAddon();
    addon.stopCapture();
  }
}
```

- [ ] **Step 5: Wire audio handlers in ipc-handlers.ts**

Replace the audio TODOs:
```ts
import { getAudioSources, startAudioCapture, stopAudioCapture } from "./audio/audio-capture";

ipcMain.handle("audio:get-sources", async () => getAudioSources());
ipcMain.handle("audio:start-capture", async (_event, sourceId: string) => {
  startAudioCapture(sourceId, mainWindow);
});
ipcMain.handle("audio:stop-capture", async () => stopAudioCapture());
```

- [ ] **Step 6: Add node-addon-api and node-gyp to devDependencies**

```bash
pnpm add -D node-addon-api node-gyp
```

- [ ] **Step 7: Build native addon and test**

```bash
cd native/wasapi-capture && npx node-gyp rebuild
```

Run: `pnpm start`, open devtools, call `window.electronAPI.audio.getSources()`
Expected: Returns list of audio devices

- [ ] **Step 8: Commit**

```bash
git add -A && git commit -m "feat: WASAPI native addon for audio capture (Windows)"
```

---

## Task 3b: Audio Capture (macOS — ScreenCaptureKit via ffi-napi)

**Files:**
- Create: `src/main/audio/screencapture-mac.ts`
- Modify: `src/main/audio/audio-capture.ts`

**Existing native build:** `native/macos/ScreenCaptureKitBridge/` — already built at repo root.
You do **not** need to build anything; the `.dylib` is ready at:
`native/macos/ScreenCaptureKitBridge/build/libScreenCaptureKitBridge.dylib`

**C API exposed by the dylib:**
```c
typedef void (*audio_callback_t)(const float* samples, int frame_count, int channels, double timestamp, void* context);

int sc_create_session(audio_callback_t callback, void* context);
int sc_start_capture(void);
int sc_stop_capture(void);
int sc_destroy_session(void);
const char* sc_get_last_error_message(void);
```

No device enumeration — it captures the default system audio output via ScreenCaptureKit.
Audio format: **48kHz stereo float** (callback-based, not polling).

- [ ] **Step 1: Implement ScreenCaptureKit bridge**

```ts
// src/main/audio/screencapture-mac.ts
// Uses ffi-napi to call the pre-built libScreenCaptureKitBridge.dylib
// The dylib uses a callback-based API — no polling needed.
import ffi from "ffi-napi";
import path from "path";

const LIB_PATH = path.join(
  __dirname,
  "../../../native/screencapture-mac/libScreenCaptureKitBridge.dylib"
);

type AudioCallback = (
  samples: Buffer,
  frameCount: number,
  channels: number,
  timestamp: number,
  context: Buffer
) => void;

let sessionCreated = false;

// ffi-napi callback type: void(float*, int, int, double, void*)
const AudioCallbackPtr = ffi.Callback(
  "void",
  ["pointer", "int", "int", "double", "pointer"],
  (samples: Buffer, frameCount: number, channels: number, timestamp: number, _context: Buffer) => {
    // Reinterpret the float* pointer as Float32Array
    const floatArray = new Float32Array(samples.buffer, samples.byteOffset, frameCount * channels);
    // Forward to the registered JS handler
    globalThis.__macAudioCallback?.(floatArray, frameCount, channels, timestamp);
  }
);

const lib = ffi.Library(LIB_PATH, {
  sc_create_session: ["int", ["pointer", "pointer"]],
  sc_start_capture: ["int", []],
  sc_stop_capture: ["int", []],
  sc_destroy_session: ["int", []],
  sc_get_last_error_message: ["string", []],
});

export function initMacCapture(onAudio: (samples: Float32Array, frameCount: number, channels: number, timestamp: number) => void): boolean {
  if (sessionCreated) return true;

  // Store callback globally so ffi-napi can call it
  globalThis.__macAudioCallback = onAudio;

  // We pass null for context (not needed)
  const status = lib.sc_create_session(AudioCallbackPtr, ffi.NULL);
  if (status !== 0) {
    console.error("[screencapture-mac] sc_create_session failed:", lib.sc_get_last_error_message());
    return false;
  }
  sessionCreated = true;
  return true;
}

export function startMacCapture(): boolean {
  if (!sessionCreated) return false;
  const status = lib.sc_start_capture();
  if (status !== 0) {
    console.error("[screencapture-mac] sc_start_capture failed:", lib.sc_get_last_error_message());
    return false;
  }
  return true;
}

export function stopMacCapture(): boolean {
  const status = lib.sc_stop_capture();
  return status === 0;
}

export function destroyMacCapture(): void {
  lib.sc_destroy_session();
  sessionCreated = false;
  globalThis.__macAudioCallback = undefined;
}
```

> **Note:** The dylib delivers 48kHz stereo float audio. Whisper expects 16kHz mono.
> You will need to **downmix stereo→mono and resample 48k→16k** before feeding to ASR.
> Consider using a simple resampling utility (e.g. `speex-resampler` WASM or a lightweight inline sinc resampler). Alternatively, the dylib could be modified to do this — but for now, handle it in JS/TS.

- [ ] **Step 2: Update audio-capture.ts for macOS path**

```ts
// Inside audio-capture.ts, macOS branch:

import { initMacCapture, startMacCapture, stopMacCapture, destroyMacCapture } from "./screencapture-mac";

// In startAudioCapture, macOS case:
if (process.platform === "darwin") {
  const audioQueue: Float32Array[] = [];
  let sampleRate = 48000; // native rate from dylib

  // Convert 48k stereo float → 16k mono float
  function downmixAndResample(samples: Float32Array, frameCount: number, channels: number, timestamp: number) {
    // Simple downmix: average channels
    let mono: Float32Array;
    if (channels === 2) {
      mono = new Float32Array(frameCount);
      for (let i = 0; i < frameCount; i++) {
        mono[i] = (samples[i * 2] + samples[i * 2 + 1]) / 2;
      }
    } else {
      mono = samples;
    }

    // Simple linear resample 48k → 16k (every 3rd sample)
    const ratio = 48000 / 16000; // 3
    const outLen = Math.floor(mono.length / ratio);
    const resampled = new Float32Array(outLen);
    for (let i = 0; i < outLen; i++) {
      resampled[i] = mono[Math.floor(i * ratio)];
    }

    // Send to renderer and feed to ASR
    mainWindow.webContents.send("audio:data", resampled);
    feedAudio(Buffer.from(resampled.buffer));
  }

  const ok = initMacCapture(downmixAndResample);
  if (ok) startMacCapture();
}
```

In `stopAudioCapture`, macOS case:
```ts
if (process.platform === "darwin") {
  stopMacCapture();
  destroyMacCapture();
}
```

Remove the `getMacAudioSources` / `readMacBuffer` polling approach from the previous plan — the existing dylib is callback-based and does not enumerate devices.

- [ ] **Step 3: Add ffi-napi dependency**

`ref-napi` is **no longer needed** (no polling buffer to allocate). Just `ffi-napi`:

```bash
pnpm add ffi-napi
```

> **Note:** `ffi-napi` requires native compilation (node-gyp). On macOS you need Xcode Command Line Tools.

- [ ] **Step 4: Copy the dylib to desktop**

```bash
mkdir -p desktop/native/screencapture-mac
cp native/macos/ScreenCaptureKitBridge/build/libScreenCaptureKitBridge.dylib desktop/native/screencapture-mac/libScreenCaptureKitBridge.dylib
```

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: macOS ScreenCaptureKit audio capture via callback-based dylib"
```

> **Note:** The existing dylib was already built. You only need to rebuild if modifying the native code at `native/macos/ScreenCaptureKitBridge/` (run `./build.sh` there).

---

## Task 4: ASR Engine (whisper.cpp child process)

**Files:**
- Create: `src/main/asr/whisper-process.ts`
- Create: `src/main/asr/whisper-types.ts`
- Modify: `src/main/ipc-handlers.ts`

- [ ] **Step 1: Define whisper types**

```ts
// src/main/asr/whisper-types.ts
export interface WhisperSegment {
  text: string;
  t0: number; // start ms
  t1: number; // end ms
  isFinal: boolean;
}

export interface WhisperConfig {
  modelPath: string;
  language: string;
  threads?: number;
}
```

- [ ] **Step 2: Implement whisper child process manager**

```ts
// src/main/asr/whisper-process.ts
import { spawn, ChildProcess } from "child_process";
import path from "path";
import { BrowserWindow } from "electron";
import { WhisperConfig, WhisperSegment } from "./whisper-types";

let whisperProcess: ChildProcess | null = null;

/**
 * Spawns whisper.cpp's `main` binary in streaming mode.
 * Expects raw 16kHz mono PCM s16le on stdin.
 * Outputs JSON segments on stdout.
 */
export function startWhisper(config: WhisperConfig, mainWindow: BrowserWindow) {
  if (whisperProcess) return;

  const binaryPath = getWhisperBinaryPath();

  whisperProcess = spawn(binaryPath, [
    "--model", config.modelPath,
    "--language", config.language,
    "--threads", String(config.threads ?? 4),
    "--output-json",
    "--no-timestamps", "false",
    "-", // read from stdin
  ]);

  let buffer = "";

  whisperProcess.stdout?.on("data", (chunk: Buffer) => {
    buffer += chunk.toString();
    const lines = buffer.split("\n");
    buffer = lines.pop() ?? "";

    for (const line of lines) {
      if (!line.trim()) continue;
      try {
        const segment: WhisperSegment = JSON.parse(line);
        mainWindow.webContents.send("asr:transcript", {
          text: segment.text,
          isFinal: segment.isFinal,
          timestamp: segment.t0,
        });
      } catch {
        // skip non-JSON lines
      }
    }
  });

  whisperProcess.on("exit", () => {
    whisperProcess = null;
  });
}

export function feedAudio(pcmData: Buffer) {
  if (whisperProcess?.stdin?.writable) {
    whisperProcess.stdin.write(pcmData);
  }
}

export function stopWhisper() {
  if (whisperProcess) {
    whisperProcess.stdin?.end();
    whisperProcess.kill();
    whisperProcess = null;
  }
}

function getWhisperBinaryPath(): string {
  // Platform-specific binary location
  const binName = process.platform === "win32" ? "whisper-cli.exe" : "whisper-cli";
  return path.join(__dirname, "../../bin", binName);
}
```

- [ ] **Step 3: Wire ASR handlers in ipc-handlers.ts**

```ts
import { startWhisper, stopWhisper, feedAudio } from "./asr/whisper-process";
import { getModelManager } from "./models/model-manager";

ipcMain.handle("asr:get-models", async () => getModelManager().listModels());
ipcMain.handle("asr:select-model", async (_event, modelId: string) => {
  getModelManager().selectModel(modelId);
});
ipcMain.handle("asr:start-transcription", async () => {
  const mm = getModelManager();
  const model = mm.getSelectedModel();
  if (!model) throw new Error("No model selected");
  startWhisper({ modelPath: model.path, language: "en" }, mainWindow);
});
ipcMain.handle("asr:stop-transcription", async () => stopWhisper());
```

- [ ] **Step 4: Connect audio pipeline to ASR**

In `audio-capture.ts`, when audio data arrives from native addon, also feed it to whisper:

```ts
import { feedAudio } from "../asr/whisper-process";

// Inside startCapture callback:
addon.startCapture(sourceId, 16000, (pcmData: Buffer) => {
  mainWindow.webContents.send("audio:data", new Float32Array(pcmData.buffer));
  feedAudio(pcmData); // feed to whisper stdin
});
```

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: whisper.cpp child process ASR with stdin streaming"
```

---

## Task 5: Model Manager & Settings Store

**Files:**
- Create: `src/main/models/model-manager.ts`
- Create: `src/main/settings/settings-store.ts`
- Modify: `src/main/ipc-handlers.ts`

- [ ] **Step 1: Implement settings store**

```ts
// src/main/settings/settings-store.ts
import { app } from "electron";
import fs from "fs";
import path from "path";

export interface AppSettings {
  storage: {
    sessionsRoot: string;
    speechToTextModelsRoot: string;
  };
  overlay: {
    fontSize: number;
    lineHeight: number;
    width: number;
    height: number;
    theme: "Dark" | "Light";
    opacity: number;
    showTranslation: boolean;
    positionX: number | null;
    positionY: number | null;
  };
  speechToText: {
    selectedModel: string;
    realtimeChunkPreset: "Fast" | "Balanced" | "Accurate";
    sourceLanguage: string;
  };
  translation: {
    enabled: boolean;
    provider: "google-free" | "translate-local";
    targetLanguage: string;
    google: { endpoint: string };
    local: { baseUrl: string };
  };
}

const DEFAULTS: AppSettings = {
  storage: {
    sessionsRoot: "sessions",
    speechToTextModelsRoot: "speech-to-text-models",
  },
  overlay: {
    fontSize: 26,
    lineHeight: 1.35,
    width: 720,
    height: 200,
    theme: "Dark",
    opacity: 0.88,
    showTranslation: true,
    positionX: null,
    positionY: null,
  },
  speechToText: {
    selectedModel: "",
    realtimeChunkPreset: "Balanced",
    sourceLanguage: "en",
  },
  translation: {
    enabled: true,
    provider: "google-free",
    targetLanguage: "vi",
    google: { endpoint: "https://translate.googleapis.com/translate_a/single" },
    local: { baseUrl: "http://127.0.0.1:3333" },
  },
};

const settingsPath = path.join(app.getPath("userData"), "settings.json");

let cache: AppSettings | null = null;

export function getSettings(): AppSettings {
  if (cache) return cache;
  try {
    const raw = fs.readFileSync(settingsPath, "utf-8");
    cache = { ...DEFAULTS, ...JSON.parse(raw) };
  } catch {
    cache = { ...DEFAULTS };
  }
  return cache!;
}

export function setSettings(partial: Partial<AppSettings>): void {
  cache = { ...getSettings(), ...partial };
  fs.writeFileSync(settingsPath, JSON.stringify(cache, null, 2));
}
```

- [ ] **Step 2: Implement model manager**

```ts
// src/main/models/model-manager.ts
import { app } from "electron";
import fs from "fs";
import path from "path";
import { getSettings, setSettings } from "../settings/settings-store";

export interface WhisperModel {
  id: string;
  name: string;
  size: string;
  path: string;
  downloaded: boolean;
}

const MODELS_DIR = path.join(app.getPath("userData"), "models");

// Known whisper.cpp model catalog
const MODEL_CATALOG: Array<{ id: string; name: string; size: string; filename: string }> = [
  { id: "tiny", name: "Tiny (75MB)", size: "75MB", filename: "ggml-tiny.bin" },
  { id: "base", name: "Base (142MB)", size: "142MB", filename: "ggml-base.bin" },
  { id: "small", name: "Small (466MB)", size: "466MB", filename: "ggml-small.bin" },
  { id: "medium", name: "Medium (1.5GB)", size: "1.5GB", filename: "ggml-medium.bin" },
  { id: "large", name: "Large (3.1GB)", size: "3.1GB", filename: "ggml-large-v3.bin" },
];

class ModelManager {
  constructor() {
    if (!fs.existsSync(MODELS_DIR)) {
      fs.mkdirSync(MODELS_DIR, { recursive: true });
    }
  }

  listModels(): WhisperModel[] {
    return MODEL_CATALOG.map((m) => ({
      id: m.id,
      name: m.name,
      size: m.size,
      path: path.join(MODELS_DIR, m.filename),
      downloaded: fs.existsSync(path.join(MODELS_DIR, m.filename)),
    }));
  }

  selectModel(modelId: string): void {
    setSettings({ modelId });
  }

  getSelectedModel(): WhisperModel | null {
    const settings = getSettings();
    return this.listModels().find((m) => m.id === settings.modelId) ?? null;
  }
}

let instance: ModelManager | null = null;
export function getModelManager(): ModelManager {
  if (!instance) instance = new ModelManager();
  return instance;
}
```

- [ ] **Step 3: Wire settings handlers in ipc-handlers.ts**

```ts
import { getSettings, setSettings } from "./settings/settings-store";

ipcMain.handle("settings:get", async () => getSettings());
ipcMain.handle("settings:set", async (_event, partial) => setSettings(partial));
```

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: model manager and persistent settings store"
```

---

## Task 6: Renderer Hooks

**Files:**
- Create: `src/hooks/use-audio-capture.ts`
- Create: `src/hooks/use-transcription.ts`
- Create: `src/hooks/use-settings.ts`

- [ ] **Step 1: Implement use-audio-capture hook**

```tsx
// src/hooks/use-audio-capture.ts
import { useState, useEffect, useCallback } from "react";
import type { AudioSource } from "../types/electron-api";

export function useAudioCapture() {
  const [sources, setSources] = useState<AudioSource[]>([]);
  const [capturing, setCapturing] = useState(false);
  const [activeSource, setActiveSource] = useState<string>("");

  useEffect(() => {
    window.electronAPI.audio.getSources().then(setSources);
  }, []);

  const start = useCallback(async (sourceId: string) => {
    await window.electronAPI.audio.startCapture(sourceId);
    setCapturing(true);
    setActiveSource(sourceId);
  }, []);

  const stop = useCallback(async () => {
    await window.electronAPI.audio.stopCapture();
    setCapturing(false);
    setActiveSource("");
  }, []);

  return { sources, capturing, activeSource, start, stop };
}
```

- [ ] **Step 2: Implement use-transcription hook**

```tsx
// src/hooks/use-transcription.ts
import { useState, useEffect, useCallback } from "react";

export interface TranscriptEntry {
  text: string;
  isFinal: boolean;
  timestamp: number;
}

export function useTranscription() {
  const [segments, setSegments] = useState<TranscriptEntry[]>([]);
  const [running, setRunning] = useState(false);

  useEffect(() => {
    const unsub = window.electronAPI.asr.onTranscript((segment) => {
      setSegments((prev) => {
        if (segment.isFinal) {
          // Replace last partial with final
          const withoutPartials = prev.filter((s) => s.isFinal);
          return [...withoutPartials, segment];
        }
        // Replace current partial
        const finals = prev.filter((s) => s.isFinal);
        return [...finals, segment];
      });
    });
    return unsub;
  }, []);

  const start = useCallback(async () => {
    await window.electronAPI.asr.startTranscription();
    setRunning(true);
  }, []);

  const stop = useCallback(async () => {
    await window.electronAPI.asr.stopTranscription();
    setRunning(false);
  }, []);

  const clear = useCallback(() => setSegments([]), []);

  return { segments, running, start, stop, clear };
}
```

- [ ] **Step 3: Implement use-settings hook**

```tsx
// src/hooks/use-settings.ts
import { useState, useEffect, useCallback } from "react";
import type { AppSettings } from "../types/electron-api";

export function useSettings() {
  const [settings, setSettingsState] = useState<AppSettings>({
    language: "en",
    modelId: "",
    audioSourceId: "",
  });

  useEffect(() => {
    window.electronAPI.settings.get().then(setSettingsState);
  }, []);

  const update = useCallback(async (partial: Partial<AppSettings>) => {
    await window.electronAPI.settings.set(partial);
    setSettingsState((prev) => ({ ...prev, ...partial }));
  }, []);

  return { settings, update };
}
```

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: React hooks for audio capture, transcription, settings"
```

---

## Task 7: UI — HomePage (Live Capture + Transcript View)

**Goal:** Build the main capture screen with a clean, focused layout. No Avalonia patterns — design from scratch for Electron/React.

**Files:**
- Modify: `src/pages/HomePage.tsx`
- Create: `src/components/TranscriptPanel.tsx`
- Create: `src/components/CaptureToolbar.tsx`
- Modify: `src/components/SubtitleOverlay.tsx` (delete — replaced by TranscriptPanel)

> **UI toolkit:** shadcn/ui + Tailwind CSS 4. Install additional shadcn components as needed: `npx shadcn@latest add badge separator tooltip switch scroll-area`

- [ ] **Step 1: Create CaptureToolbar component**

```tsx
// src/components/CaptureToolbar.tsx
// ┌─────────────────────────────────────────────────────────────────────────┐
// │ [🎙 Audio Source ▼]  [🧠 Model ▼]  [▶ Start]  [🗑 Clear]  [📺 Overlay] │
// └─────────────────────────────────────────────────────────────────────────┘
//
// Layout: horizontal bar, items-center, gap-3, border-b, px-4 py-3
// - AudioSourceSelector (existing) — compact w-[200px]
// - ModelSelector (existing) — compact w-[200px]
// - Separator (vertical)
// - Start/Stop button: primary green when idle, destructive red when capturing
//   - Start disabled if no source or no model selected
//   - Shows pulsing dot animation when capturing
// - Clear button: ghost variant, icon-only with tooltip "Clear transcript"
// - Separator (vertical)
// - Overlay toggle: ghost button with Monitor icon, tooltip "Show overlay"
//   - Active state: highlighted bg when overlay is visible
// - Right-aligned: status badge
//   - "Ready" (muted) / "Capturing" (green pulse) / "No model" (yellow warning)
```

- [ ] **Step 2: Create TranscriptPanel component**

```tsx
// src/components/TranscriptPanel.tsx
// The main content area showing live transcript lines.
//
// ┌─────────────────────────────────────────────────────────────────┐
// │                                                                 │
// │  10:32:05  Hello, welcome to the presentation today.           │
// │            Xin chào, chào mừng đến với bài thuyết trình hôm nay│
// │                                                                 │
// │  10:32:12  We'll be discussing the new architecture.            │
// │            Chúng ta sẽ thảo luận về kiến trúc mới.             │
// │                                                                 │
// │  10:32:18  Let me share my screen...                  ← partial│
// │            Để tôi chia sẻ màn hình...                  (italic) │
// │                                                                 │
// └─────────────────────────────────────────────────────────────────┘
//
// Design:
// - Uses shadcn ScrollArea, full height of remaining space
// - Each transcript entry is a row:
//   - Left gutter: timestamp in text-xs text-muted-foreground, mono font
//   - Main content:
//     - Original text: text-base font-medium
//     - Translated text (if exists): text-sm text-muted-foreground, mt-0.5
//   - Partial (non-final) entries: italic, opacity-70, no bottom border
//   - Final entries: border-b border-border/30
// - Auto-scroll to bottom on new entries (useRef + scrollIntoView)
// - "Jump to bottom" FAB button when scrolled up (absolute bottom-4 right-4)
// - Empty state: centered illustration-free message
//   "Press Start to begin capturing audio"
//   with subtle microphone icon above, text-muted-foreground
// - Max 200 entries displayed, older ones removed from DOM (keep in state for export)
```

- [ ] **Step 3: Redesign HomePage layout**

```tsx
// src/pages/HomePage.tsx
// Full-height flex layout:
//
// ┌───────────────────────────────────────────────────┐
// │  CaptureToolbar                                    │  ← fixed height
// ├───────────────────────────────────────────────────┤
// │                                                    │
// │  TranscriptPanel                                   │  ← flex-1, scrollable
// │                                                    │
// ├───────────────────────────────────────────────────┤
// │  Status: Capturing · 00:02:34 · 12 segments       │  ← fixed height
// └───────────────────────────────────────────────────┘
//
// Bottom status bar: flex items-center gap-4, h-8, px-4, border-t, text-xs text-muted-foreground
// Shows:
//   - Capture duration (live timer when capturing)
//   - Segment count
//   - Active model name
//   - Translation provider status (if enabled)
```

- [ ] **Step 4: Delete old SubtitleOverlay.tsx**

Remove `src/components/SubtitleOverlay.tsx` — replaced by TranscriptPanel.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: clean HomePage with CaptureToolbar, TranscriptPanel, status bar"
```

---

## Task 8: Settings Page (Full Tabbed Layout)

**Goal:** Build a complete settings page with sidebar navigation and 4 sections. Clean design, not copying Avalonia layout.

**Files:**
- Rewrite: `src/pages/SettingsPage.tsx`
- Create: `src/components/settings/GeneralSettings.tsx`
- Create: `src/components/settings/SpeechSettings.tsx`
- Create: `src/components/settings/TranslationSettings.tsx`
- Create: `src/components/settings/OverlaySettings.tsx`
- Create: `src/components/settings/SettingsSection.tsx`
- Create: `src/components/settings/SettingsField.tsx`

> Install: `npx shadcn@latest add tabs input label slider switch card separator`

- [ ] **Step 1: Create reusable SettingsSection and SettingsField**

```tsx
// src/components/settings/SettingsSection.tsx
// Reusable section wrapper for settings groups.
//
// ┌─ Section Title ──────────────────────────────────┐
// │  Optional description text in muted color         │
// │                                                    │
// │  [children — the settings fields]                 │
// └───────────────────────────────────────────────────┘
//
// - title: text-lg font-semibold
// - description: text-sm text-muted-foreground mb-4
// - children: space-y-4
// - Wrapper: bg-card rounded-lg border p-6

// src/components/settings/SettingsField.tsx
// Single setting field with label + control layout.
//
// ┌───────────────────────────────────────────────────┐
// │  Label text                              [control]│
// │  Helper text in smaller muted font                │
// └───────────────────────────────────────────────────┘
//
// - Horizontal layout (justify-between) for simple controls (switch, select)
// - Vertical layout for complex controls (text input with helper)
// - label: text-sm font-medium
// - helper: text-xs text-muted-foreground
```

- [ ] **Step 2: Implement SettingsPage with sidebar navigation**

```tsx
// src/pages/SettingsPage.tsx
// Sidebar navigation + content area:
//
// ┌──────────┬───────────────────────────────────────────┐
// │          │                                            │
// │ General  │  General Settings                          │
// │ Speech   │                                            │
// │ Translat │  ┌─ Storage ──────────────────────────┐   │
// │ Overlay  │  │ Sessions folder    [/path...] [📁]  │   │
// │          │  │ Models folder      [/path...] [📁]  │   │
// │          │  └─────────────────────────────────────┘   │
// │          │                                            │
// └──────────┴───────────────────────────────────────────┘
//
// Sidebar: w-48, border-r, py-4, space-y-1
//   Each item: px-3 py-2, rounded-md, text-sm
//   Active: bg-muted font-medium
//   Icons: Cog, Mic, Languages, Monitor
// Content: flex-1, p-6, overflow-y-auto
//
// Use React state for active tab (not router — instant switching)
```

- [ ] **Step 3: Implement GeneralSettings**

```tsx
// src/components/settings/GeneralSettings.tsx
//
// ┌─ Storage ────────────────────────────────────────────┐
// │                                                       │
// │  Sessions folder                                      │
// │  [/Users/taitran/.sublingual/sessions    ] [Browse]   │
// │  Where captured audio sessions are saved              │
// │                                                       │
// │  Speech models folder                                 │
// │  [/Users/taitran/.sublingual/models      ] [Browse]   │
// │  Local speech-to-text model files                     │
// │                                                       │
// └───────────────────────────────────────────────────────┘
//
// - Each path field: flex row, Input (read-only, flex-1) + Button "Browse" (ghost, FolderOpen icon)
// - Clicking Browse opens native folder picker via IPC
// - Below each input: helper text in text-xs text-muted-foreground
```

- [ ] **Step 4: Implement SpeechSettings**

```tsx
// src/components/settings/SpeechSettings.tsx
//
// ┌─ Speech-to-Text Model ──────────────────────────────────┐
// │                                                          │
// │  Active model                                            │
// │  [vosk-model-en-us-0.22  ▼]                             │
// │                                                          │
// │  Chunk preset                                            │
// │  ( ) Fast — 500ms chunks, lower accuracy                 │
// │  (●) Balanced — 1000ms chunks                            │
// │  ( ) Accurate — 2000ms chunks, higher latency            │
// │                                                          │
// │  Source language          [English  ▼]                    │
// │  Language of the audio being captured                     │
// │                                                          │
// └──────────────────────────────────────────────────────────┘
//
// ┌─ Model Management ──────────────────────────────────────┐
// │                                                          │
// │  [📥 Install Models]  [📂 Import]  [📁 Open Folder]     │
// │                                                          │
// │  "Install Models" opens ModelDownloadDialog (Task 10)    │
// │  "Import" opens file picker for zip/directory            │
// │  "Open Folder" opens models dir in file explorer         │
// │                                                          │
// └──────────────────────────────────────────────────────────┘
//
// Chunk preset: use radio group (shadcn RadioGroup)
// Model selector: shadcn Select
```

- [ ] **Step 5: Implement TranslationSettings**

```tsx
// src/components/settings/TranslationSettings.tsx
//
// ┌─ Translation ────────────────────────────────────────────┐
// │                                                           │
// │  Enable translation                              [🔘 on] │
// │                                                           │
// │  Provider                     [Google Translate ▼]        │
// │  Translation backend to use                               │
// │                                                           │
// │  Target language              [Vietnamese ▼]              │
// │  Translate transcripts into this language                  │
// │                                                           │
// └───────────────────────────────────────────────────────────┘
//
// ┌─ Provider: Google Translate ─────────────────────────────┐
// │                                                           │
// │  Endpoint                                                 │
// │  [https://translate.googleapis.com/translate_a/single]    │
// │  Free Google Translate API endpoint                        │
// │                                                           │
// └───────────────────────────────────────────────────────────┘
//
//  — OR if "Local TranslateService" selected: —
//
// ┌─ Provider: Local TranslateService ───────────────────────┐
// │                                                           │
// │  Base URL                                                 │
// │  [http://127.0.0.1:3333                              ]    │
// │  Local translation service address                        │
// │                                                           │
// └───────────────────────────────────────────────────────────┘
//
// ┌─ Test Translation ───────────────────────────────────────┐
// │                                                           │
// │  Source text                                              │
// │  [Hello, how are you today?                          ]    │
// │                                                           │
// │  [🔄 Translate]                                           │
// │                                                           │
// │  Result                                                   │
// │  ┌─────────────────────────────────────────────────────┐ │
// │  │ Xin chào, hôm nay bạn có khỏe không?               │ │
// │  └─────────────────────────────────────────────────────┘ │
// │  Provider: GoogleTranslateFreeApi · 120ms                 │
// │                                                           │
// └───────────────────────────────────────────────────────────┘
//
// Provider options: "Google Translate" | "Local TranslateService"
// (NO LibreTranslate, NO fallback chain)
// Provider config section changes dynamically based on selected provider.
// Test translation: textarea input + translate button + result display
// Show provider name + latency after test completes
// Show error in red text if translation fails
```

- [ ] **Step 6: Implement OverlaySettings**

```tsx
// src/components/settings/OverlaySettings.tsx
//
// ┌─ Appearance ─────────────────────────────────────────────┐
// │                                                           │
// │  Theme                         [Dark ▼]                   │
// │                                                           │
// │  Font size                     [====●======] 26px         │
// │                                     14 ←→ 48              │
// │                                                           │
// │  Line spacing                  [Compact] [Default] [Wide] │
// │                                                           │
// │  Background opacity            [========●==] 88%          │
// │                                     30% ←→ 100%           │
// │                                                           │
// │  Show translation                                [🔘 on]  │
// │  Display translated text below each transcript line       │
// │                                                           │
// └───────────────────────────────────────────────────────────┘
//
// ┌─ Size ───────────────────────────────────────────────────┐
// │                                                           │
// │  Width   [720] px          Height   [200] px              │
// │                                                           │
// └───────────────────────────────────────────────────────────┘
//
// ┌─ Preview ────────────────────────────────────────────────┐
// │  ┌─────────────────────────────────────────────────────┐ │
// │  │ (live preview of overlay with current settings)      │ │
// │  │                                                      │ │
// │  │  Hello, welcome to the presentation.                 │ │
// │  │  Xin chào, chào mừng đến với bài thuyết trình.      │ │
// │  │                                                      │ │
// │  │  We'll discuss the new architecture.                 │ │
// │  │  Chúng ta sẽ thảo luận kiến trúc mới.               │ │
// │  └─────────────────────────────────────────────────────┘ │
// └───────────────────────────────────────────────────────────┘
//
// Theme: Select with "Dark" | "Light"
// Font size: shadcn Slider, shows current value
// Line spacing: 3-button toggle group (Compact=1.15, Default=1.35, Wide=1.6)
// Opacity: shadcn Slider with percentage display
// Show translation: Switch
// Width/Height: number Input fields, side by side
// Preview: a mini overlay simulation box, uses actual settings to render sample text
//   - Background color matches theme + opacity
//   - Font size and line height match settings
//   - Shows/hides translation line based on toggle
```

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: full settings page with General, Speech, Translation, Overlay sections"
```

---

## Task 9: Translation Service (Simple Provider Model)

**Goal:** Simple translation service — pick one active provider, call it, return result. No fallback chain, no cache, no draft/stable scheduler, no LibreTranslate.

**Providers:** Google Translate Free API, Local TranslateService.

**Files:**
- Create: `src/main/translation/providers/types.ts`
- Create: `src/main/translation/providers/google-free.ts`
- Create: `src/main/translation/providers/translate-local.ts`
- Create: `src/main/translation/translation-service.ts`
- Create: `src/hooks/use-translation.ts`
- Modify: `src/main/ipc-handlers.ts`
- Modify: `src/preload.ts`
- Modify: `src/types/electron-api.d.ts`

- [ ] **Step 1: Define translation types**

```ts
// src/main/translation/providers/types.ts
export interface TranslationRequest {
  sourceText: string;
  sourceLanguage: string;
  targetLanguage: string;
}

export interface TranslationResult {
  translatedText: string;
  providerName: string;
  durationMs: number;
}

export interface TranslationSettings {
  enabled: boolean;
  provider: "google-free" | "translate-local"; // active provider
  targetLanguage: string;
  google: { endpoint: string };
  local: { baseUrl: string };
}

export interface ITranslationProvider {
  name: string;
  translate(request: TranslationRequest, config: Record<string, string>): Promise<string>;
}
```

- [ ] **Step 2: Implement Google Translate Free API provider**

```ts
// src/main/translation/providers/google-free.ts
// GET https://translate.googleapis.com/translate_a/single?client=gtx&sl={src}&tl={tgt}&dt=t&q={text}
// Parse nested array response: result[0].map(s => s[0]).join("")
// Config: { endpoint: string }
```

- [ ] **Step 3: Implement Local TranslateService provider**

```ts
// src/main/translation/providers/translate-local.ts
// POST {baseUrl}/translate with JSON body { text, source, target }
// Returns { translatedText: string }
// Config: { baseUrl: string } (default http://127.0.0.1:3333)
```

- [ ] **Step 4: Implement translation service**

```ts
// src/main/translation/translation-service.ts
// Simple service:
//   - Reads settings to pick active provider
//   - Calls provider.translate()
//   - Measures duration
//   - Returns TranslationResult
//   - Skips if source === target language (returns empty)
//   - Skips if source text is empty
//   - On error: throws with provider name + error message
//
// Used in two ways:
//   1. One-shot: settings test translation (IPC handler)
//   2. Inline: called after each final ASR segment, result sent to renderer
//      (translation is triggered from whisper-process.ts when a final segment arrives)
```

- [ ] **Step 5: Wire translation IPC**

Add to `ipc-handlers.ts`:
```ts
ipcMain.handle("translation:translate", async (_e, sourceText, sourceLang, targetLang) => { ... });
ipcMain.handle("translation:test", async (_e, sourceText, sourceLang, targetLang) => { ... });
```

Events pushed from main → renderer:
```ts
// "translation:segment-result" — { segmentId, translatedText, providerName, durationMs }
// Sent after each final ASR segment is translated
```

- [ ] **Step 6: Update preload and types**

```ts
// Add to ElectronAPI:
translation: {
  translate: (sourceText: string, sourceLang: string, targetLang: string) => Promise<TranslationResult>;
  test: (sourceText: string, sourceLang: string, targetLang: string) => Promise<TranslationResult>;
  onSegmentResult: (callback: (result: { segmentId: string; translatedText: string; providerName: string; durationMs: number }) => void) => () => void;
}
```

- [ ] **Step 7: Create use-translation hook**

```ts
// src/hooks/use-translation.ts
// - Listens to translation:segment-result events
// - Maintains a Map<segmentId, translatedText> for the current session
// - Exposes: translations map, testTranslation(text, src, tgt) → result
// - TranscriptPanel uses this to show translated text under each segment
```

- [ ] **Step 8: Integrate with ASR pipeline**

In `whisper-process.ts`, after emitting a final segment to renderer:
```ts
// If translation is enabled in settings:
//   1. Call translationService.translate(segment.text, sourceLang, targetLang)
//   2. Send result to renderer via mainWindow.webContents.send("translation:segment-result", ...)
// This keeps translation inline and simple — no scheduler needed
```

- [ ] **Step 9: Commit**

```bash
git add -A && git commit -m "feat: simple translation service with Google Free + Local providers"
```

---

## Task 10: Model Download & Install

**Goal:** Let users browse and download speech models from within the app.

**Files:**
- Create: `src/main/models/model-source-catalog.ts`
- Create: `src/main/models/model-downloader.ts`
- Create: `src/hooks/use-model-download.ts`
- Create: `src/components/ModelDownloadDialog.tsx`
- Modify: `src/main/ipc-handlers.ts`
- Modify: `src/preload.ts`
- Modify: `src/types/electron-api.d.ts`

- [ ] **Step 1: Create model source catalog**

```ts
// src/main/models/model-source-catalog.ts
// Embedded catalog of downloadable models:
// [
//   { id: "tiny", name: "Tiny", size: "75 MB", lang: "Multi", url: "https://huggingface.co/..." },
//   { id: "base", name: "Base", size: "142 MB", lang: "Multi", url: "..." },
//   { id: "small", name: "Small", size: "466 MB", lang: "Multi", url: "..." },
//   { id: "medium", name: "Medium", size: "1.5 GB", lang: "Multi", url: "..." },
//   { id: "large", name: "Large v3", size: "3.1 GB", lang: "Multi", url: "..." },
// ]
// Returns list with isInstalled flag from model-manager
```

- [ ] **Step 2: Implement model downloader**

```ts
// src/main/models/model-downloader.ts
// - download(modelId): downloads model file with progress reporting
// - Streams response to temp file, then moves to models dir
// - Reports progress via IPC: models:download-progress { modelId, percent, status, error }
// - Supports cancellation via AbortController
```

- [ ] **Step 3: Wire IPC handlers**

```ts
// models:get-installable — list all models with installed status
// models:download — start download for modelId
// models:cancel-download — abort active download
// models:open-folder — shell.openPath(modelsDir)
```

- [ ] **Step 4: Create ModelDownloadDialog component**

```tsx
// src/components/ModelDownloadDialog.tsx
// Modal dialog showing available models as cards:
//
// ┌─ Install Speech Models ──────────────────── [✕] ┐
// │                                                   │
// │  ┌────────────────────────────────────────────┐  │
// │  │ 🟢 Tiny · 75 MB · Multi-language           │  │
// │  │ Fast, lower accuracy            [Installed] │  │
// │  └────────────────────────────────────────────┘  │
// │                                                   │
// │  ┌────────────────────────────────────────────┐  │
// │  │ ○  Base · 142 MB · Multi-language           │  │
// │  │ Good balance of speed and accuracy          │  │
// │  │                                [⬇ Download] │  │
// │  └────────────────────────────────────────────┘  │
// │                                                   │
// │  ┌────────────────────────────────────────────┐  │
// │  │ ○  Small · 466 MB · Multi-language         │  │
// │  │ Better accuracy, slower                     │  │
// │  │ [████████████░░░░░░] 67%        [Cancel]    │  │
// │  └────────────────────────────────────────────┘  │
// │                                                   │
// │  ┌────────────────────────────────────────────┐  │
// │  │ ○  Medium · 1.5 GB · Multi-language        │  │
// │  │ High accuracy                  [⬇ Download] │  │
// │  └────────────────────────────────────────────┘  │
// │                                                   │
// └───────────────────────────────────────────────────┘
//
// Use shadcn Dialog, Card components
// Each model card:
//   - Left: model name (font-medium), size + language badge
//   - Below: short description
//   - Right: status — "Installed" badge (green) / "Download" button / progress bar + Cancel
// Progress bar: shadcn Progress or native HTML progress
// Error state: red text below card with retry button
// Dialog footer: "Open Models Folder" link button
```

- [ ] **Step 5: Create use-model-download hook**

```ts
// src/hooks/use-model-download.ts
// State: { activeDownload: { modelId, percent, status } | null, error: string | null }
// Actions: startDownload(modelId), cancelDownload()
// Listens to models:download-progress events
```

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: model download dialog with progress and install management"
```

---

## Task 11: Overlay Window (Separate BrowserWindow)

**Goal:** Floating always-on-top overlay window showing live transcript + translation. Separate from main window. Clean minimal design.

**Files:**
- Create: `src/main/overlay/overlay-window.ts`
- Create: `src/overlay/overlay-renderer.ts`
- Create: `src/overlay/overlay-preload.ts`
- Create: `src/overlay/OverlayApp.tsx`
- Create: `src/overlay/overlay.html`
- Modify: `src/main/ipc-handlers.ts`
- Modify: `src/preload.ts`
- Modify: `vite.config.ts` (add overlay entry)
- Modify: `src/types/electron-api.d.ts`

- [ ] **Step 1: Create overlay window manager (main process)**

```ts
// src/main/overlay/overlay-window.ts
// Creates a second BrowserWindow:
//   transparent: true, frame: false, alwaysOnTop: true,
//   skipTaskbar: true, hasShadow: false
//   Width/height/position from OverlaySettings
//   Loads overlay.html (separate Vite entry)
//
// API:
//   createOverlayWindow() — creates window if not exists
//   showOverlay() / hideOverlay() / toggleOverlay()
//   isOverlayVisible() → boolean
//   sendToOverlay(channel, ...args) — forward events to overlay renderer
//   On window move/resize → save position/size to settings
//   On close → hide (not destroy), can be shown again
```

- [ ] **Step 2: Create overlay preload**

```ts
// src/overlay/overlay-preload.ts
// Exposes overlayAPI via contextBridge:
//   getSettings() → OverlaySettings
//   onTranscriptLine(cb) — { id, text, translatedText, timestamp }
//   onPartialUpdate(cb) — { text, translatedText }
//   onSettingsUpdate(cb) — partial OverlaySettings changes
//   onClear(cb) — clear all lines
//   close() — hide overlay
```

- [ ] **Step 3: Create OverlayApp React component**

```tsx
// src/overlay/OverlayApp.tsx
// Minimal floating subtitle UI — NOT copying Avalonia layout.
//
// ┌─────────────────────────────────────────────────────────┐
// │ ═══ drag handle ═══                              [✕]    │ ← 24px, transparent bar
// │                                                          │
// │   Hello, welcome to the presentation today.              │ ← original text
// │   Xin chào, chào mừng đến với bài thuyết trình.         │ ← translation (if enabled)
// │                                                          │
// │   We'll discuss the new architecture.                    │
// │   Chúng ta sẽ thảo luận kiến trúc mới.                  │
// │                                                          │
// │   Let me share my screen...                    (partial) │ ← italic, faded
// │   Để tôi chia sẻ màn hình...                             │
// │                                                          │
// └─────────────────────────────────────────────────────────┘
//
// Design:
// - Rounded-lg container with theme-dependent background
//   - Dark: rgba(14, 19, 28, opacity)
//   - Light: rgba(245, 247, 250, opacity)
// - Drag handle: top bar, -webkit-app-region: drag, cursor: grab
// - Close button: absolute top-right, small ✕, opacity on hover
// - Content area: overflow-y auto, scrolls to bottom
// - Each line:
//   - Original: text-base, white (dark) or text-gray-900 (light)
//   - Translation: text-sm, white/60 (dark) or text-gray-500 (light), mt-0.5
//   - Spacing between lines: mb-3
// - Partial line: italic + reduced opacity
// - Empty state: "Waiting for speech..." in muted text, centered
// - Max 50 visible lines (older removed)
// - Auto-scroll to bottom, "↓" button when scrolled up
// - All sizes/colors driven by OverlaySettings (fontSize, lineHeight, theme, opacity, showTranslation)
// - Settings update in real-time via onSettingsUpdate listener
```

- [ ] **Step 4: Create overlay HTML and renderer entry**

```ts
// src/overlay/overlay-renderer.ts — mount OverlayApp
// src/overlay/overlay.html — minimal HTML shell for overlay window
```

- [ ] **Step 5: Update Vite config for multi-entry**

Add overlay as a second renderer entry in `vite.config.ts`.

- [ ] **Step 6: Wire overlay IPC in main window**

Add to main preload:
```ts
overlay: {
  show: () => ipcRenderer.invoke("overlay:show"),
  hide: () => ipcRenderer.invoke("overlay:hide"),
  toggle: () => ipcRenderer.invoke("overlay:toggle"),
  isVisible: () => ipcRenderer.invoke("overlay:is-visible"),
}
```

Forward ASR + translation events to overlay window in `ipc-handlers.ts`:
```ts
// When asr:transcript arrives → sendToOverlay("overlay:transcript-line", segment)
// When translation:segment-result arrives → sendToOverlay("overlay:translation", result)
// When overlay settings change → sendToOverlay("overlay:settings-update", settings)
```

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: floating overlay window with live transcript and translation"
```

---

## Task 12: Layout & Navigation Update

**Goal:** Update the app shell to include all pages and improve navigation.

**Files:**
- Modify: `src/App.tsx`
- Modify: `src/components/Layout.tsx`

- [ ] **Step 1: Update Layout with all navigation items**

```tsx
// src/components/Layout.tsx
// Updated navigation:
//
// ┌─────────────────────────────────────────────────────────┐
// │  🎙 Sublingual    [Home] [Sessions] [Settings]    [─][□][✕] │
// └─────────────────────────────────────────────────────────┘
//
// - App title/logo: "Sublingual" with microphone icon, text-base font-semibold
// - Nav items: Home (Mic icon), Sessions (Archive icon), Settings (Cog icon)
// - Style: ghost buttons, active state with bg-muted
// - Right side: window controls (if custom titlebar) or empty
// - Border-b separator
// - Main content: flex-1 overflow-hidden (pages handle their own scroll)
```

- [ ] **Step 2: Update App.tsx routes**

```tsx
// Add /sessions route
<Route path="/sessions" element={<SessionsPage />} />
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: update layout with Sessions navigation"
```

---

## Task 13: Sessions Management

**Goal:** Browse, search, and manage saved capture sessions.

**Files:**
- Create: `src/main/sessions/session-storage.ts`
- Create: `src/pages/SessionsPage.tsx`
- Create: `src/hooks/use-sessions.ts`
- Modify: `src/main/ipc-handlers.ts`
- Modify: `src/preload.ts`
- Modify: `src/types/electron-api.d.ts`

- [ ] **Step 1: Implement session storage (main process)**

```ts
// src/main/sessions/session-storage.ts
// - listSessions(search?, page?, pageSize?) → { sessions, total }
// - getTranscript(sessionId) → TranscriptLine[]
// - deleteSessions(ids: string[]) → number deleted
// - clearAll() → number deleted
// - exportAsTxt(sessionId) → opens save dialog, writes .txt
// - exportAsJson(sessionId) → opens save dialog, writes .json
// - openFolder(sessionId) → shell.openPath
```

- [ ] **Step 2: Wire session IPC handlers**

```ts
// sessions:list, sessions:get-transcript, sessions:delete,
// sessions:clear-all, sessions:export-txt, sessions:export-json,
// sessions:open-folder
```

- [ ] **Step 3: Create SessionsPage with master-detail layout**

```tsx
// src/pages/SessionsPage.tsx
// Master-detail layout:
//
// ┌──────────────────────────┬──────────────────────────────────┐
// │  🔍 Search sessions...   │  Session: 2025-05-29 10:32       │
// │                           │  Duration: 5m 23s · 47 segments  │
// │  ┌─ Today ────────────┐  │                                   │
// │  │                     │  │  ┌────────────────────────────┐  │
// │  │ ● 10:32 AM  5m 23s │  │  │ 10:32:05                    │  │
// │  │   "Hello, welcome..." │  │ Hello, welcome to the...    │  │
// │  │                     │  │  │ Xin chào, chào mừng...      │  │
// │  │ ○ 09:15 AM  12m 07s│  │  │                              │  │
// │  │   "Good morning..." │  │  │ 10:32:12                    │  │
// │  │                     │  │  │ We'll be discussing...      │  │
// │  └────────────────────┘  │  │ Chúng ta sẽ thảo luận...    │  │
// │                           │  │                              │  │
// │  ┌─ Yesterday ────────┐  │  └────────────────────────────┘  │
// │  │                     │  │                                   │
// │  │ ○ 3:45 PM  8m 44s  │  │  ┌────────────────────────────┐  │
// │  │   "Let's review..." │  │  │ [📄 Export TXT] [📋 JSON]   │  │
// │  │                     │  │  │ [📁 Open Folder] [🗑 Delete] │  │
// │  └────────────────────┘  │  └────────────────────────────┘  │
// │                           │                                   │
// │  [Select All] [🗑 Delete] │                                   │
// └──────────────────────────┴──────────────────────────────────┘
//
// Left panel (w-80, border-r):
//   - Search input at top (shadcn Input with Search icon)
//   - Session list grouped by date (Today, Yesterday, older dates)
//   - Each session item:
//     - Checkbox for multi-select
//     - Time + duration
//     - First line preview text (truncated)
//     - Selected state: bg-muted
//   - Bottom toolbar: "Select All" toggle, "Delete Selected" button
//   - ScrollArea for the list
//
// Right panel (flex-1):
//   - Header: session date/time, duration, segment count
//   - Transcript view (ScrollArea):
//     - Same layout as TranscriptPanel (timestamp + original + translation)
//     - Read-only (no live updates)
//   - Action bar at bottom:
//     - Export TXT, Export JSON: ghost buttons with file icons
//     - Open Folder: ghost button
//     - Delete: destructive ghost button
//   - Empty state (no session selected):
//     "Select a session to view its transcript"
//     centered, muted text
```

- [ ] **Step 4: Create use-sessions hook**

```ts
// src/hooks/use-sessions.ts
// State:
//   sessions: SessionSummary[]
//   selectedIds: Set<string>
//   activeSession: { info: SessionSummary, transcript: TranscriptLine[] } | null
//   search: string
//   loading: boolean
// Actions:
//   loadSessions(search?)
//   selectSession(id) — loads transcript
//   toggleSelect(id) / selectAll() / deselectAll()
//   deleteSelected() → confirm dialog → delete → reload
//   exportTxt(id) / exportJson(id)
//   openFolder(id)
```

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: sessions management with master-detail layout"
```

---

## Dependency Summary

### npm packages

Add to `package.json`:

```json
{
  "dependencies": {
    "ffi-napi": "^4.0.3"
  },
  "devDependencies": {
    "node-addon-api": "^7.1.0",
    "node-gyp": "^10.0.0"
  }
}
```

> `ref-napi` was removed in the macOS rewrite — no longer needed (callback-based API, not polling).

### Manual downloads (not auto-installed)

These must be downloaded/placed manually:

1. **whisper.cpp binary** — Place in `desktop/bin/`:
   - macOS: `whisper-cli` (compiled from [whisper.cpp](https://github.com/ggerganov/whisper.cpp))
   - Windows: `whisper-cli.exe`
   - Compile it yourself or download a pre-built release

2. **Whisper model files** — Downloaded via Task 10's model downloader, or place manually in the app's `userData/models/` directory:
   - Download from Hugging Face: https://huggingface.co/ggerganov/whisper.cpp
   - Models: `ggml-tiny.bin`, `ggml-base.bin`, `ggml-small.bin`, `ggml-medium.bin`, `ggml-large-v3.bin`

3. **macOS dylib** — Already built at `native/macos/ScreenCaptureKitBridge/build/libScreenCaptureKitBridge.dylib`
   - Copy to `desktop/native/screencapture-mac/` (see Task 3b)
   - Only rebuild if modifying native code: run `./build.sh` in that directory

---

## Execution Order

```
Phase 1 — Foundation:     Task 1 → Task 2
Phase 2 — Audio/ASR:      (Task 3, Task 3b parallel) → Task 4
Phase 3 — Backend:        Task 5 → Task 9 → Task 10
Phase 4 — UI:             Task 6 → Task 7 → Task 8
Phase 5 — Overlay:        Task 11 (after Task 9)
Phase 6 — Navigation:     Task 12
Phase 7 — Sessions:       Task 13
```

Task dependencies:
- Task 5 (settings store) must come before Task 9 (translation) and Task 10 (model download)
- Task 6 (hooks) must come before Task 7 (HomePage) and Task 8 (SettingsPage)
- Task 9 (translation) must come before Task 11 (overlay — needs translation results)
- Task 8 (SettingsPage) should come after Task 9 + Task 10 (references translation settings + model download dialog)
- Task 12 (layout update) and Task 13 (sessions) can be done after all core features are in place
