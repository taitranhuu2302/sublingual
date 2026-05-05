# PRD: LingoStream MVP — Real-time Bilingual Subtitle Desktop App

## Introduction

LingoStream is a desktop application that captures live audio (microphone), transcribes it to text in real-time using Speech-to-Text engines, translates the text (English to Vietnamese), and displays bilingual subtitles in an always-on-top overlay window. The MVP targets macOS and Windows, supports both offline (Vosk) and cloud (Whisper) STT, uses LibreTranslate for translation, and persists session history in SQLite.

The Electron frontend (React + Tailwind + Zustand) UI shell already exists with 4 static pages (Dashboard, History, Captions, Settings), a full shadcn component library, and the LingoStream dark glassmorphism design system. **All functional logic — audio capture, backend processing, WebSocket communication, overlay window, IPC, and database — needs to be built.**

### Architecture Overview

```
┌──────────────────── ELECTRON ────────────────────────┐
│  Main Process                                        │
│  ├── Spawns Python backend as child process          │
│  ├── Creates Main Window (UI shell)                  │
│  ├── Creates Overlay Window (always-on-top, frameless)│
│  └── IPC bridge (main ↔ renderer, main ↔ overlay)    │
│                                                      │
│  Renderer (Main Window)                              │
│  ├── Audio capture via getUserMedia + AudioWorklet   │
│  ├── WebSocket client → sends PCM chunks to backend  │
│  ├── Receives STT + translation results              │
│  ├── Zustand store for app state                     │
│  └── Routes: Dashboard, History, Captions, Settings  │
│                                                      │
│  Renderer (Overlay Window)                           │
│  ├── Frameless, transparent, always-on-top           │
│  ├── Receives subtitle data via IPC                  │
│  └── Draggable, resizable, auto-hide                 │
└──────────────────────────────────────────────────────┘
                    │ WebSocket (ws://localhost:8765)
                    ▼
┌──────────────────── PYTHON BACKEND ──────────────────┐
│  FastAPI + WebSocket server                          │
│  ├── STT Engine: Vosk (local) or Whisper (cloud)     │
│  ├── Translation: LibreTranslate (self-hosted)       │
│  ├── SQLite: session persistence                     │
│  └── REST endpoints: history, settings, health       │
└──────────────────────────────────────────────────────┘
```

### Data Flow

1. User selects mic device and presses "Start Session" in Dashboard.
2. Renderer captures mic audio via `getUserMedia`, processes via `AudioWorklet` into PCM 16kHz mono 16-bit chunks.
3. Chunks are sent every ~250ms over WebSocket to Python backend.
4. Backend feeds chunks to STT engine (Vosk or Whisper).
5. STT engine returns interim (partial) and final text.
6. On final text, backend calls LibreTranslate API to translate EN → VI.
7. Backend sends `{type: "partial", text}` or `{type: "final", original, translated, timestamp}` back over WebSocket.
8. Renderer updates Zustand store; main window and overlay window both render the latest subtitles.
9. On session end, backend saves the full transcript to SQLite.

---

## Goals

- Deliver a working end-to-end loop: mic → STT → translate → overlay display, with < 3 second latency.
- Support both Vosk (offline, CPU) and Whisper (cloud, high accuracy) STT engines, selectable in Settings.
- Translate English → Vietnamese using a self-hosted LibreTranslate instance.
- Display bilingual subtitles in a draggable, always-on-top overlay window that works over fullscreen apps.
- Persist session transcripts in SQLite and display them in the History page.
- Target macOS and Windows with a single codebase.
- Keep CPU usage under 30% on a mid-range machine when using Vosk.

---

## User Stories
