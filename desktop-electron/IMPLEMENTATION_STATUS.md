# Electron Migration Implementation Status

## ✅ Completed Tasks

### Task 1: App Scaffold & Router
- ✅ Created App.tsx with React Router
- ✅ Fixed renderer.ts to properly mount React app
- ✅ Created electron-api type declarations
- ✅ Updated Layout and Pages to use named exports

### Task 2: Preload Bridge & IPC Foundation
- ✅ Implemented preload.ts with contextBridge
- ✅ Created IPC handlers skeleton
- ✅ Wired IPC handlers in main.ts
- ✅ Exposed audio, ASR, and settings APIs to renderer

### Task 5: Model Manager & Settings Store
- ✅ Implemented persistent settings store (JSON file)
- ✅ Created model manager for whisper.cpp models
- ✅ Wired settings handlers in IPC

### Task 6: Renderer Hooks
- ✅ Implemented use-audio-capture hook
- ✅ Implemented use-transcription hook
- ✅ Implemented use-settings hook

### Task 7: UI Components
- ✅ Created SubtitleOverlay component
- ✅ Created AudioSourceSelector component
- ✅ Created ModelSelector component
- ✅ Updated HomePage with audio capture controls
- ✅ Added Select UI component from Radix

### Task 8: Settings Page
- ✅ Implemented SettingsPage with model/audio/language config
- ✅ Integrated with settings hook

### Task 4: ASR Engine
- ✅ Implemented whisper.cpp child process manager
- ✅ Created whisper types
- ✅ Wired ASR handlers in IPC
- ✅ Connected audio pipeline to ASR stdin

### Task 3b: macOS Audio Capture
- ✅ Implemented ScreenCaptureKit bridge using ffi-napi
- ✅ Copied dylib from native build
- ✅ Created audio capture orchestrator
- ✅ Implemented downmixing (stereo→mono) and resampling (48k→16k)
- ✅ Wired audio handlers in IPC

## ⏸️ Deferred Tasks

### Task 3: Windows WASAPI Audio Capture
- ❌ Not implemented (macOS development environment)
- 📋 Native addon structure defined in plan
- 📋 binding.gyp template provided
- 📋 Would require C++ implementation of WASAPI capture

## 📦 Dependencies Added

- `@radix-ui/react-select` - Select component
- `ffi-napi` - FFI bindings for macOS dylib

## 🏗️ File Structure Created

```
desktop-electron/
├── src/
│   ├── App.tsx
│   ├── renderer.ts
│   ├── preload.ts
│   ├── main.ts
│   ├── types/
│   │   └── electron-api.d.ts
│   ├── main/
│   │   ├── ipc-handlers.ts
│   │   ├── audio/
│   │   │   ├── audio-capture.ts
│   │   │   └── screencapture-mac.ts
│   │   ├── asr/
│   │   │   ├── whisper-process.ts
│   │   │   └── whisper-types.ts
│   │   ├── models/
│   │   │   └── model-manager.ts
│   │   └── settings/
│   │       └── settings-store.ts
│   ├── hooks/
│   │   ├── use-audio-capture.ts
│   │   ├── use-transcription.ts
│   │   └── use-settings.ts
│   ├── components/
│   │   ├── SubtitleOverlay.tsx
│   │   ├── AudioSourceSelector.tsx
│   │   ├── ModelSelector.tsx
│   │   └── ui/
│   │       ├── select.tsx
│   │       └── button.tsx
│   └── pages/
│       ├── HomePage.tsx
│       └── SettingsPage.tsx
└── native/
    └── screencapture-mac/
        └── libScreenCaptureKitBridge.dylib
```

## ⚠️ Manual Steps Required

### 1. ✅ Build whisper.cpp Binary (COMPLETED)

The `whisper-cli` binary has been built and placed in `desktop-electron/bin/whisper-cli`.

- **Platform**: macOS ARM64
- **Size**: 844 KB
- **Location**: `desktop-electron/bin/whisper-cli`

For other platforms, see `desktop-electron/bin/README.md` for build instructions.

### 2. Download Whisper Models

Download models from Hugging Face and place in app's userData directory:

```bash
# macOS: ~/.sublingual/models/
# Windows: ~\.sublingual~\models\
# Linux: ~/.sublingual/models/

# Download models (example):
wget https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin
```

Models available:
- `ggml-tiny.bin` (75MB)
- `ggml-base.bin` (142MB)
- `ggml-small.bin` (466MB)
- `ggml-medium.bin` (1.5GB)
- `ggml-large-v3.bin` (3.1GB)

### 3. macOS Permissions

The app needs screen recording permission for ScreenCaptureKit:
- System Settings → Privacy & Security → Screen Recording
- Enable for the Electron app

## 🚀 Running the App

```bash
cd desktop-electron
pnpm install
pnpm start
```

## 🧪 Testing

1. Open Settings page
2. Select an audio source (macOS: "System Audio")
3. Select a whisper model (must be downloaded first)
4. Go to Home page
5. Click "Start" to begin capturing and transcribing
6. Subtitles will appear at bottom of screen

## 📝 Architecture Overview

```
Electron App
├── Main Process
│   ├── Audio Capture (macOS: ScreenCaptureKit dylib)
│   ├── ASR Engine (whisper.cpp child process)
│   ├── Model Manager
│   └── Settings Store
└── Renderer Process
    ├── React App (routing, UI)
    ├── Hooks (state management)
    └── Components (UI elements)

IPC Bridge (contextBridge)
├── audio:get-sources
├── audio:start-capture
├── audio:stop-capture
├── asr:get-models
├── asr:select-model
├── asr:start-transcription
├── asr:stop-transcription
├── settings:get
└── settings:set
```

## 🔄 Data Flow

1. **Audio Capture**: ScreenCaptureKit → dylib callback → downmix/resample → main process
2. **ASR**: Audio data → whisper.cpp stdin → JSON segments → renderer via IPC
3. **Display**: Segments → React state → SubtitleOverlay component
4. **Settings**: Renderer → IPC → JSON file → persisted

## 🎯 Next Steps

1. **Windows Support**: Implement WASAPI native addon (Task 3)
2. **Model Download**: Add in-app model downloader UI
3. **Error Handling**: Add better error messages and recovery
4. **Performance**: Optimize resampling (consider using WASM resampler)
5. **Testing**: Add unit tests for core modules
6. **Packaging**: Configure electron-forge for distribution

## 📚 References

- Electron migration plan: `docs/superpowers/plans/2025-05-29-electron-migration.md`
- Native dylib source: `native/macos/ScreenCaptureKitBridge/`
- Original C# implementation: `src/Sublingual.Infrastructure/Audio/`
