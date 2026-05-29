# Sublingual Electron Migration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate core functionality (audio capture → ASR → display) from the C#/Avalonia `src/` app to the Electron `desktop-electron/` app.

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
- ❌ LibreTranslate provider (removed)
- ❌ TranslatedTextPartial logic (will be redefined separately)
- ❌ Translation pipeline (deferred)

---

## File Structure

```
desktop-electron/src/
├── main.ts                          — Main process entry, window creation, IPC registration
├── preload.ts                       — contextBridge: expose audio/ASR APIs to renderer
├── main/
│   ├── ipc-handlers.ts             — IPC handler registration (audio, ASR, settings)
│   ├── audio/
│   │   ├── audio-capture.ts        — Platform-agnostic audio capture orchestrator
│   │   ├── wasapi-addon.ts         — Windows WASAPI native addon loader
│   │   └── screencapture-mac.ts    — macOS ScreenCaptureKit via ffi-napi
│   ├── asr/
│   │   ├── whisper-process.ts      — whisper.cpp child process manager (spawn, stream, kill)
│   │   └── whisper-types.ts        — Types for whisper output (segments, timestamps)
│   ├── models/
│   │   └── model-manager.ts        — Download/list/select whisper models
│   └── settings/
│       └── settings-store.ts       — Persistent settings (electron-store or JSON file)
├── renderer.ts                      — React app entry
├── App.tsx                          — Router setup
├── lib/
│   └── utils.ts                    — cn() helper (existing)
├── hooks/
│   ├── use-audio-capture.ts        — React hook wrapping IPC audio controls
│   ├── use-transcription.ts        — React hook for streaming ASR results
│   └── use-settings.ts            — React hook for app settings
├── components/
│   ├── Layout.tsx                  — App shell (existing, will be updated)
│   ├── ui/                         — shadcn components
│   ├── SubtitleOverlay.tsx         — Real-time subtitle display
│   ├── AudioSourceSelector.tsx     — Dropdown to pick mic/system audio
│   └── ModelSelector.tsx           — Dropdown to pick whisper model
├── pages/
│   ├── HomePage.tsx                — Main capture + subtitle view
│   └── SettingsPage.tsx            — Settings (model, audio source, language)
└── types/
    └── electron-api.d.ts           — Type declarations for window.electronAPI
```

**Native addons (separate build):**
```
desktop-electron/native/
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
> Copy or symlink it to `desktop-electron/native/screencapture-mac/libScreenCaptureKitBridge.dylib` after build.

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

Run: `pnpm start` (in desktop-electron/)
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

- [ ] **Step 4: Copy the dylib to desktop-electron**

```bash
mkdir -p desktop-electron/native/screencapture-mac
cp native/macos/ScreenCaptureKitBridge/build/libScreenCaptureKitBridge.dylib desktop-electron/native/screencapture-mac/libScreenCaptureKitBridge.dylib
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
  language: string;
  modelId: string;
  audioSourceId: string;
}

const DEFAULTS: AppSettings = {
  language: "en",
  modelId: "",
  audioSourceId: "",
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

## Task 7: UI — Subtitle Overlay & Home Page

**Files:**
- Create: `src/components/SubtitleOverlay.tsx`
- Create: `src/components/AudioSourceSelector.tsx`
- Create: `src/components/ModelSelector.tsx`
- Modify: `src/pages/HomePage.tsx`

> **UI Reference:** Use shadcn/ui components (Button, Select, Card, etc.) per `desktop-electron/.agents/skills/shadcn/`.

- [ ] **Step 1: Create SubtitleOverlay component**

```tsx
// src/components/SubtitleOverlay.tsx
import type { TranscriptEntry } from "../hooks/use-transcription";

interface Props {
  segments: TranscriptEntry[];
}

export function SubtitleOverlay({ segments }: Props) {
  const recent = segments.slice(-5); // show last 5 segments

  return (
    <div className="fixed bottom-8 left-1/2 -translate-x-1/2 w-[80%] max-w-2xl">
      <div className="bg-black/80 rounded-lg px-6 py-4 space-y-1">
        {recent.length === 0 && (
          <p className="text-white/50 text-center text-sm">Waiting for speech...</p>
        )}
        {recent.map((seg, i) => (
          <p
            key={i}
            className={`text-white text-lg text-center ${!seg.isFinal ? "opacity-60 italic" : ""}`}
          >
            {seg.text}
          </p>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Create AudioSourceSelector**

```tsx
// src/components/AudioSourceSelector.tsx
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "./ui/select";
import type { AudioSource } from "../types/electron-api";

interface Props {
  sources: AudioSource[];
  value: string;
  onChange: (sourceId: string) => void;
  disabled?: boolean;
}

export function AudioSourceSelector({ sources, value, onChange, disabled }: Props) {
  return (
    <Select value={value} onValueChange={onChange} disabled={disabled}>
      <SelectTrigger className="w-[250px]">
        <SelectValue placeholder="Select audio source" />
      </SelectTrigger>
      <SelectContent>
        {sources.map((s) => (
          <SelectItem key={s.id} value={s.id}>
            {s.name} ({s.type})
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
```

- [ ] **Step 3: Create ModelSelector**

```tsx
// src/components/ModelSelector.tsx
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "./ui/select";
import type { WhisperModel } from "../types/electron-api";

interface Props {
  models: WhisperModel[];
  value: string;
  onChange: (modelId: string) => void;
  disabled?: boolean;
}

export function ModelSelector({ models, value, onChange, disabled }: Props) {
  return (
    <Select value={value} onValueChange={onChange} disabled={disabled}>
      <SelectTrigger className="w-[250px]">
        <SelectValue placeholder="Select model" />
      </SelectTrigger>
      <SelectContent>
        {models.map((m) => (
          <SelectItem key={m.id} value={m.id} disabled={!m.downloaded}>
            {m.name} {!m.downloaded && "(not downloaded)"}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
```

- [ ] **Step 4: Implement HomePage**

```tsx
// src/pages/HomePage.tsx
import { useState, useEffect } from "react";
import { Button } from "../components/ui/button";
import { AudioSourceSelector } from "../components/AudioSourceSelector";
import { ModelSelector } from "../components/ModelSelector";
import { SubtitleOverlay } from "../components/SubtitleOverlay";
import { useAudioCapture } from "../hooks/use-audio-capture";
import { useTranscription } from "../hooks/use-transcription";
import type { WhisperModel } from "../types/electron-api";

export function HomePage() {
  const { sources, capturing, activeSource, start, stop } = useAudioCapture();
  const { segments, running, start: startASR, stop: stopASR, clear } = useTranscription();
  const [selectedSource, setSelectedSource] = useState("");
  const [selectedModel, setSelectedModel] = useState("");
  const [models, setModels] = useState<WhisperModel[]>([]);

  useEffect(() => {
    window.electronAPI.asr.getModels().then(setModels);
  }, []);

  const handleStart = async () => {
    if (!selectedSource || !selectedModel) return;
    await window.electronAPI.asr.selectModel(selectedModel);
    await start(selectedSource);
    await startASR();
  };

  const handleStop = async () => {
    await stopASR();
    await stop();
  };

  return (
    <div className="flex flex-col h-full p-6 gap-4">
      <div className="flex items-center gap-4">
        <AudioSourceSelector
          sources={sources}
          value={selectedSource}
          onChange={setSelectedSource}
          disabled={capturing}
        />
        <ModelSelector
          models={models}
          value={selectedModel}
          onChange={setSelectedModel}
          disabled={capturing}
        />
        {!capturing ? (
          <Button onClick={handleStart} disabled={!selectedSource || !selectedModel}>
            Start
          </Button>
        ) : (
          <Button variant="destructive" onClick={handleStop}>
            Stop
          </Button>
        )}
        <Button variant="outline" onClick={clear}>
          Clear
        </Button>
      </div>

      <div className="flex-1 relative">
        <SubtitleOverlay segments={segments} />
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Add shadcn Select component if not present**

```bash
npx shadcn@latest add select
```

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: subtitle overlay UI, audio/model selectors, home page"
```

---

## Task 8: Settings Page

**Files:**
- Modify: `src/pages/SettingsPage.tsx`

- [ ] **Step 1: Implement SettingsPage**

```tsx
// src/pages/SettingsPage.tsx
import { useEffect, useState } from "react";
import { Button } from "../components/ui/button";
import { AudioSourceSelector } from "../components/AudioSourceSelector";
import { ModelSelector } from "../components/ModelSelector";
import { useSettings } from "../hooks/use-settings";
import type { AudioSource, WhisperModel } from "../types/electron-api";

export function SettingsPage() {
  const { settings, update } = useSettings();
  const [sources, setSources] = useState<AudioSource[]>([]);
  const [models, setModels] = useState<WhisperModel[]>([]);

  useEffect(() => {
    window.electronAPI.audio.getSources().then(setSources);
    window.electronAPI.asr.getModels().then(setModels);
  }, []);

  return (
    <div className="p-6 space-y-6">
      <h1 className="text-2xl font-bold">Settings</h1>

      <div className="space-y-4">
        <div>
          <label className="text-sm font-medium mb-2 block">Default Audio Source</label>
          <AudioSourceSelector
            sources={sources}
            value={settings.audioSourceId}
            onChange={(id) => update({ audioSourceId: id })}
          />
        </div>

        <div>
          <label className="text-sm font-medium mb-2 block">ASR Model</label>
          <ModelSelector
            models={models}
            value={settings.modelId}
            onChange={(id) => update({ modelId: id })}
          />
        </div>

        <div>
          <label className="text-sm font-medium mb-2 block">Language</label>
          <select
            className="border rounded px-3 py-2"
            value={settings.language}
            onChange={(e) => update({ language: e.target.value })}
          >
            <option value="en">English</option>
            <option value="vi">Vietnamese</option>
            <option value="ja">Japanese</option>
            <option value="ko">Korean</option>
            <option value="zh">Chinese</option>
          </select>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add -A && git commit -m "feat: settings page with model/source/language config"
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

1. **whisper.cpp binary** — Place in `desktop-electron/bin/`:
   - macOS: `whisper-cli` (compiled from [whisper.cpp](https://github.com/ggerganov/whisper.cpp))
   - Windows: `whisper-cli.exe`
   - Compile it yourself or download a pre-built release

2. **Whisper model files** — Place in the app's `userData/models/` directory (or configure path via settings):
   - Download from Hugging Face: https://huggingface.co/ggerganov/whisper.cpp
   - Models: `ggml-tiny.bin`, `ggml-base.bin`, `ggml-small.bin`, `ggml-medium.bin`, `ggml-large-v3.bin`
   - They are **not** auto-downloaded by the app

3. **macOS dylib** — Already built at `native/macos/ScreenCaptureKitBridge/build/libScreenCaptureKitBridge.dylib`
   - Copy to `desktop-electron/native/screencapture-mac/` (see Task 3b)
   - Only rebuild if modifying native code: run `./build.sh` in that directory

---

## Execution Order

Tasks 1 → 2 → (3, 3b parallel) → 4 → 5 → 6 → 7 → 8

Tasks 3 and 3b are platform-specific and can be done in parallel. Task 4 depends on Task 3 being wired (audio → stdin). Tasks 6-8 are renderer-side and depend on IPC being in place (Task 2).
