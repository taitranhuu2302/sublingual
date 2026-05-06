# PRD: LingoStream MVP — Real-time Bilingual Subtitle Desktop App

## Introduction

LingoStream is a desktop application that captures live audio, transcribes it to text in real-time using Speech-to-Text engines, translates the text (English to Vietnamese), and displays bilingual subtitles in an always-on-top overlay window. The MVP targets macOS and Windows, supports both offline (Vosk) and cloud (Whisper) STT, uses a separately run LibreTranslate service for translation, and persists session history in SQLite. The current implementation path is microphone-first, while support for system/desktop audio is being documented as the next planned audio-input expansion.

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
 │  ├── Audio capture via microphone or system source   │
 │  ├── Input selection and source-specific guidance    │
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

1. User selects an audio source type and the corresponding source in Dashboard.
2. If the selected source is invalid, unavailable, or not configured, the UI blocks session start and shows actionable guidance instead of falling back silently.
3. Renderer captures audio from the active source, processes it into PCM 16kHz mono 16-bit chunks, and keeps the existing backend audio contract unchanged.
4. Chunks are sent every ~250ms over WebSocket to Python backend.
5. Backend feeds chunks to STT engine (Vosk or Whisper).
6. Whisper may accumulate about 3 seconds of audio before a request and may flush earlier on silence or session end.
7. STT engine returns interim (partial) and final text.
8. On final text, backend calls LibreTranslate API to translate EN → VI.
9. Backend sends `{type: "partial", text}` or `{type: "final", original, translated, timestamp}` back over WebSocket.
10. Renderer updates Zustand store; main window and overlay window both render the latest subtitles.
11. On session end, backend saves the full transcript to SQLite and auto-generates a session title if the user has not named one.

---

## Goals

- Deliver a working end-to-end loop: audio source → STT → translate → overlay display, with < 3 second latency.
- Support both Vosk (offline, CPU) and Whisper (cloud, high accuracy) STT engines, selectable in Settings.
- Translate English → Vietnamese using a self-hosted LibreTranslate instance.
- Display bilingual subtitles in a draggable, always-on-top overlay window that works over fullscreen apps.
- Persist session transcripts in SQLite and display them in the History page.
- Target macOS and Windows with a single codebase.
- Keep CPU usage under 30% on a mid-range machine when using Vosk.
- Define a documentation-first rollout for expanding input support from microphone-only to microphone plus system/desktop audio.

---

## User Stories
