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

### US-001: Python Backend Scaffold with FastAPI + WebSocket

**Description:** As a developer, I need a Python backend service that runs a FastAPI server with WebSocket support so that the Electron app can send audio and receive transcription results.

**Acceptance Criteria:**
- [ ] Create `backend/` directory at project root with `main.py`, `requirements.txt`
- [ ] `requirements.txt` includes `fastapi`, `uvicorn[standard]`, `websockets`
- [ ] `main.py` starts a FastAPI app on `localhost:8765`
- [ ] WebSocket endpoint at `/ws/audio` accepts binary messages and echoes back a JSON acknowledgment `{"type": "ack"}`
- [ ] REST endpoint `GET /health` returns `{"status": "ok"}`
- [ ] Server can be started with `python main.py` or `uvicorn main:app`
- [ ] Typecheck/lint passes

---

### US-002: Electron Main Process Spawns Python Backend

**Description:** As a user, I want the Python backend to start automatically when I launch the app so I don't need to run separate processes.

**Acceptance Criteria:**
- [ ] Main process spawns Python backend as a child process on app `ready` event
- [ ] Backend process is killed on app `before-quit` event
- [ ] If backend fails to start (Python not found, port in use), show an error dialog to the user
- [ ] Backend stdout/stderr is logged to a file in the app's userData directory
- [ ] Health check: main process polls `GET /health` until backend is ready (timeout 15 seconds)
- [ ] Typecheck/lint passes

---

### US-003: Preload Script with IPC API

**Description:** As a developer, I need a secure preload script that exposes IPC methods to the renderer so it can communicate with the main process without direct Node.js access.

**Acceptance Criteria:**
- [ ] Preload script uses `contextBridge.exposeInMainWorld` to expose an `electronAPI` object
- [ ] Exposed methods include:
  - `getAudioDevices(): Promise<MediaDeviceInfo[]>` — lists available audio input devices
  - `startSession(config: { deviceId: string, sttEngine: string }): void` — tells main process to begin a session
  - `stopSession(): void` — tells main process to end the current session
  - `onSubtitleUpdate(callback: (data) => void): void` — listens for subtitle updates from main
  - `onBackendStatus(callback: (status) => void): void` — listens for backend health changes
  - `getSessionHistory(): Promise<Session[]>` — fetches saved sessions
  - `updateOverlaySettings(settings): void` — sends overlay config to main
- [ ] `contextIsolation: true` and `nodeIntegration: false` in BrowserWindow webPreferences
- [ ] TypeScript type declarations for `window.electronAPI` in a `.d.ts` file
- [ ] Typecheck/lint passes

---

### US-004: Zustand Store for Application State

**Description:** As a developer, I need a centralized Zustand store to manage session state, subtitle data, settings, and UI state so all components stay in sync.

**Acceptance Criteria:**
- [ ] Create `src/renderer/src/stores/session-store.ts` with the following state shape:
  ```
  {
    status: 'idle' | 'connecting' | 'streaming' | 'error',
    selectedDeviceId: string | null,
    sttEngine: 'vosk' | 'whisper',
    translationEnabled: boolean,
    currentPartial: string,
    subtitles: Array<{ id, original, translated, timestamp }>,
    error: string | null,
  }
  ```
- [ ] Actions: `setDevice`, `setSTTEngine`, `startSession`, `stopSession`, `addSubtitle`, `updatePartial`, `setError`, `clearSubtitles`
- [ ] Create `src/renderer/src/stores/overlay-store.ts` for overlay settings:
  ```
  {
    fontSize: number,
    backgroundOpacity: number,
    lineSpacing: number,
    displayMode: 'bilingual' | 'original-only' | 'translated-only',
    position: { x: number, y: number },
    autoHideDelay: number,
  }
  ```
- [ ] Actions: `updateSettings`, `resetDefaults`
- [ ] Typecheck/lint passes

---

### US-005: Audio Capture via getUserMedia + AudioWorklet

**Description:** As a user, I want the app to capture audio from my selected microphone and convert it into PCM format suitable for STT processing.

**Acceptance Criteria:**
- [ ] Renderer enumerates audio input devices using `navigator.mediaDevices.enumerateDevices()` and populates the Dashboard source dropdown with real device names
- [ ] On "Start Session", renderer calls `getUserMedia({ audio: { deviceId, sampleRate: 16000, channelCount: 1 } })`
- [ ] An `AudioWorkletProcessor` (in a separate file `src/renderer/src/workers/pcm-processor.worklet.ts`) downsamples audio to 16kHz mono and outputs PCM Int16 ArrayBuffers
- [ ] Audio chunks are emitted every ~250ms (4096 samples at 16kHz)
- [ ] A VU meter on the Dashboard shows real-time audio levels (RMS amplitude) from the AudioWorklet
- [ ] When session is stopped, the MediaStream tracks are stopped and AudioContext is closed
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---

### US-006: WebSocket Client in Renderer

**Description:** As a developer, I need a WebSocket client in the renderer that sends PCM audio chunks to the Python backend and receives STT/translation results.

**Acceptance Criteria:**
- [ ] Create `src/renderer/src/services/websocket-client.ts`
- [ ] Connects to `ws://localhost:8765/ws/audio` when session starts
- [ ] Sends binary PCM chunks as ArrayBuffer messages
- [ ] Receives JSON messages and dispatches to Zustand store:
  - `{type: "partial", text}` → `updatePartial(text)`
  - `{type: "final", original, translated, timestamp}` → `addSubtitle({...})`
  - `{type: "error", message}` → `setError(message)`
- [ ] Handles reconnection: if WebSocket disconnects during a session, attempt reconnect up to 3 times with exponential backoff (1s, 2s, 4s)
- [ ] On session stop, sends a `{type: "end_session"}` text message and closes the WebSocket
- [ ] Zustand store `status` reflects connection state (`connecting`, `streaming`, `error`)
- [ ] Typecheck/lint passes

---

### US-007: Vosk STT Integration in Backend

**Description:** As a user, I want the Vosk engine to transcribe my speech locally in real-time so I can use the app without internet.

**Acceptance Criteria:**
- [ ] Add `vosk` to `requirements.txt`
- [ ] Create `backend/engines/stt_vosk.py` that wraps Vosk `KaldiRecognizer`
- [ ] Accepts 16kHz mono PCM Int16 audio chunks
- [ ] Returns partial results (`{"type": "partial", "text": "..."}`) after each chunk
- [ ] Returns final results (`{"type": "final", "text": "..."}`) when Vosk detects end of utterance
- [ ] Vosk model is downloaded to `backend/models/vosk-model-small-en-us/` (document download URL in README)
- [ ] Model path is configurable via environment variable `VOSK_MODEL_PATH`
- [ ] Benchmark: processes a 1-second chunk in < 200ms on a 4-core CPU
- [ ] Typecheck/lint passes

---

### US-008: Whisper Cloud STT Integration in Backend

**Description:** As a user, I want the option to use OpenAI Whisper API for higher-accuracy transcription when I have internet.

**Acceptance Criteria:**
- [ ] Add `openai` to `requirements.txt`
- [ ] Create `backend/engines/stt_whisper.py`
- [ ] Buffers incoming audio chunks and sends to OpenAI Whisper API every ~3 seconds (or on silence detection)
- [ ] API key read from environment variable `OPENAI_API_KEY`
- [ ] Returns final text results in the same format as Vosk: `{"type": "final", "text": "..."}`
- [ ] Partial results are synthesized from accumulated buffer text (optional: send `{"type": "partial", "text": "buffering..."}` while waiting)
- [ ] If API key is missing or API call fails, return `{"type": "error", "message": "..."}` and fall back gracefully
- [ ] Typecheck/lint passes

---

### US-009: STT Engine Selection and Routing

**Description:** As a user, I want to choose between Vosk and Whisper in the Settings page, and have my choice take effect for the next session.

**Acceptance Criteria:**
- [ ] WebSocket `/ws/audio` accepts an initial text message `{"type": "config", "stt_engine": "vosk" | "whisper"}` before audio streaming begins
- [ ] Backend routes audio to the selected engine based on the config message
- [ ] Settings page radio buttons (already in UI) are wired to the Zustand store `sttEngine` field
- [ ] Selected engine persists across app restarts (saved via IPC to main process, stored in electron-store or a JSON file in userData)
- [ ] If Whisper is selected but `OPENAI_API_KEY` is not set, show a warning badge in Settings and an input field for the API key
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---

### US-010: LibreTranslate Integration in Backend

**Description:** As a developer, I need the backend to translate final English text to Vietnamese using LibreTranslate.

**Acceptance Criteria:**
- [ ] Add `requests` (or `httpx`) to `requirements.txt`
- [ ] Create `backend/engines/translator.py`
- [ ] Calls LibreTranslate API at configurable URL (default `http://localhost:5000/translate`)
- [ ] Sends `{"q": text, "source": "en", "target": "vi", "format": "text"}`
- [ ] Returns translated text string
- [ ] If LibreTranslate is unavailable, return original text with a warning flag `{"translated": text, "translation_failed": true}`
- [ ] Translation timeout: 5 seconds per request
- [ ] Document how to run LibreTranslate locally via Docker: `docker run -d -p 5000:5000 libretranslate/libretranslate`
- [ ] Typecheck/lint passes

---

### US-011: Full WebSocket Pipeline (Audio → STT → Translate → Response)

**Description:** As a user, I want the full pipeline working end-to-end: I speak into my mic, and I see both the original English text and Vietnamese translation appear in real-time.

**Acceptance Criteria:**
- [ ] WebSocket handler in `main.py` orchestrates the full flow:
  1. Receives config message → selects STT engine
  2. Receives binary audio chunks → feeds to STT engine
  3. On partial result → sends `{"type": "partial", "text": "..."}` to client
  4. On final result → translates via LibreTranslate → sends `{"type": "final", "original": "...", "translated": "...", "timestamp": "ISO8601"}` to client
- [ ] Multiple concurrent WebSocket connections are supported (one per session)
- [ ] Each connection maintains its own STT engine instance (no shared state between sessions)
- [ ] Latency from speech to displayed subtitle is < 3 seconds (with Vosk)
- [ ] Typecheck/lint passes

---

### US-012: Dashboard Page — Wire Up Real Audio Devices and Session Control

**Description:** As a user, I want the Dashboard page to show my real audio devices, let me start/stop a session, and display live transcription results.

**Acceptance Criteria:**
- [ ] "Primary Source" dropdown is populated with real audio input devices from `enumerateDevices()`
- [ ] "Start Stream Session" button starts the full pipeline: audio capture → WebSocket → receive results
- [ ] Button text changes to "Stop Session" (with destructive styling) while streaming
- [ ] Live Monitor (VU meter) animates with real-time audio levels during streaming
- [ ] Telemetry Output section shows real connection status messages:
  - "Connecting to backend..." → "Connected. STT engine: Vosk" → "Streaming..." → "Session ended."
- [ ] Current partial text is shown in a new "Live Transcription" section below the telemetry
- [ ] Final subtitles (original + translated) appear as a scrolling list in the Live Transcription section
- [ ] Error states are displayed clearly (backend not running, mic permission denied, etc.)
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---

### US-013: Overlay Window — Always-on-Top Subtitle Display

**Description:** As a user, I want a separate overlay window that shows subtitles on top of all other applications (including fullscreen video) so I can read translations while watching movies or attending meetings.

**Acceptance Criteria:**
- [ ] Main process creates a second `BrowserWindow` with:
  - `alwaysOnTop: true` (level `screen-saver` on macOS for fullscreen compat)
  - `frame: false` (no title bar)
  - `transparent: true` (transparent background)
  - `skipTaskbar: true`
  - `resizable: true`
  - `focusable: false` (so it doesn't steal focus from other apps)
  - Default size: 800x120, positioned at bottom-center of primary display
- [ ] Overlay window loads a separate HTML entry point (`overlay.html`) with its own React root
- [ ] Overlay renders:
  - Current partial text (dimmed/italic)
  - Last 2 final subtitles (original on top, translated below, per display mode setting)
- [ ] Overlay is draggable by clicking and dragging anywhere on it
- [ ] Overlay auto-hides (fades to 0 opacity) after configurable delay (default 5 seconds) when no new text arrives
- [ ] Overlay reappears immediately when new text arrives
- [ ] Overlay respects settings from `overlay-store`: font size, background opacity, display mode
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---

### US-014: Overlay Window — IPC Communication

**Description:** As a developer, I need the overlay window to receive subtitle updates and settings changes from the main renderer via the main process IPC bridge.

**Acceptance Criteria:**
- [ ] Main process listens for `subtitle-update` IPC messages from main renderer
- [ ] Main process forwards subtitle data to overlay window via `overlayWindow.webContents.send('subtitle-update', data)`
- [ ] Main process listens for `overlay-settings-update` IPC messages and forwards to overlay window
- [ ] Overlay window's preload script exposes `onSubtitleUpdate` and `onSettingsUpdate` listeners
- [ ] When overlay window is closed by user, main process can recreate it via a "Show Overlay" button in the main window
- [ ] Typecheck/lint passes

---

### US-015: Captions Page — Wire Up Live Preview with Real Settings

**Description:** As a user, I want the Captions page to control the overlay appearance and show a real-time preview of how subtitles will look.

**Acceptance Criteria:**
- [ ] Font Size slider updates `overlay-store.fontSize` and the preview text reflects the change immediately
- [ ] Background Opacity slider updates `overlay-store.backgroundOpacity` and preview reflects it
- [ ] Line Spacing slider updates `overlay-store.lineSpacing` and preview reflects it
- [ ] Add a "Display Mode" selector (bilingual / original only / translated only) that updates `overlay-store.displayMode`
- [ ] "Apply Overlay Preset" button sends current settings to main process via IPC, which forwards to overlay window
- [ ] Preview section shows the most recent subtitle from the session store (or placeholder if no session active)
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---

### US-016: Settings Page — Wire Up Engine Selection and API Key Input

**Description:** As a user, I want the Settings page to persist my STT and translation engine choices, and allow me to enter an API key for Whisper.

**Acceptance Criteria:**
- [ ] STT engine radio buttons update `session-store.sttEngine`
- [ ] When "Whisper (Cloud)" is selected, show a text input for OpenAI API key below the radio group
- [ ] API key is stored securely via Electron's `safeStorage` API (encrypted at rest)
- [ ] API key is sent to the backend via an environment variable when spawning the Python process
- [ ] Translation engine radio buttons are functional (LibreTranslate pre-selected, Argos shown but disabled with "Coming Soon" badge)
- [ ] "Save Configuration" button persists all settings to a JSON file in userData via IPC
- [ ] "Revert Defaults" button resets all settings to defaults
- [ ] Settings are loaded on app startup and applied to Zustand stores
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---

### US-017: SQLite Session Persistence in Backend

**Description:** As a developer, I need the backend to save completed session transcripts to SQLite so they can be displayed in the History page.

**Acceptance Criteria:**
- [ ] Add `aiosqlite` to `requirements.txt`
- [ ] Create `backend/database.py` with schema:
  ```sql
  CREATE TABLE sessions (
    id TEXT PRIMARY KEY,
    title TEXT,
    started_at TEXT NOT NULL,
    ended_at TEXT,
    stt_engine TEXT NOT NULL,
    language_pair TEXT NOT NULL DEFAULT 'en-vi'
  );

  CREATE TABLE transcripts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT NOT NULL REFERENCES sessions(id),
    original TEXT NOT NULL,
    translated TEXT,
    timestamp TEXT NOT NULL
  );
  ```
- [ ] Database file stored at `~/.lingostream/sessions.db`
- [ ] When a WebSocket session starts, insert a row into `sessions` with `started_at = now()`
- [ ] When a final subtitle is produced, insert a row into `transcripts`
- [ ] When WebSocket disconnects or receives `end_session`, update `sessions.ended_at`
- [ ] REST endpoint `GET /api/sessions` returns all sessions ordered by `started_at DESC`
- [ ] REST endpoint `GET /api/sessions/{id}/transcripts` returns all transcripts for a session
- [ ] Typecheck/lint passes

---

### US-018: History Page — Display Real Session Data

**Description:** As a user, I want the History page to show my actual past sessions so I can review what was said and translated.

**Acceptance Criteria:**
- [ ] On page load, fetch sessions from backend via `GET /api/sessions` (through IPC → main process → HTTP request)
- [ ] Display real session data instead of hardcoded mock data
- [ ] Each session card shows: title (auto-generated from first few words of transcript), language pair, duration, timestamp
- [ ] Clicking "Replay" on a session opens a detail view showing the full transcript (original + translated side by side)
- [ ] Search input filters sessions by title text (client-side filtering)
- [ ] Empty state when no sessions exist: "No sessions yet. Start your first session from the Dashboard."
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---

### US-019: Global Keyboard Shortcuts

**Description:** As a user, I want system-wide keyboard shortcuts to control the app without switching windows.

**Acceptance Criteria:**
- [ ] Register global shortcuts in the main process using `globalShortcut.register`:
  - `Ctrl+Shift+R` (Windows) / `Cmd+Shift+R` (macOS): Toggle recording (start/stop session)
  - `Alt+T` / `Option+T`: Toggle overlay visibility
  - `Escape`: Clear current subtitles from overlay
- [ ] Shortcuts are unregistered on app quit (`will-quit` event)
- [ ] Shortcuts trigger the same actions as their UI button equivalents
- [ ] Settings page displays the shortcuts (already in UI) — shortcuts are read-only for MVP
- [ ] Typecheck/lint passes

---

### US-020: Error Handling and Backend Status Indicator

**Description:** As a user, I want clear feedback when something goes wrong (backend not running, mic denied, LibreTranslate down) so I know how to fix it.

**Acceptance Criteria:**
- [ ] Main window header shows a colored status dot:
  - Green: backend healthy, ready to stream
  - Yellow: backend starting or LibreTranslate unreachable (translation will fail)
  - Red: backend unreachable
- [ ] If mic permission is denied, Dashboard shows an inline error with instructions to grant permission in System Preferences (macOS) or Windows Settings
- [ ] If WebSocket disconnects mid-session, show a toast notification: "Connection lost. Attempting to reconnect..."
- [ ] If LibreTranslate is unreachable, subtitles still appear (original only) with a small warning indicator
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---

## Functional Requirements

- **FR-01:** The system must capture audio from a user-selected microphone device using the Web Audio API (`getUserMedia` + `AudioWorkletProcessor`).
- **FR-02:** Audio must be converted to PCM 16kHz mono 16-bit format before sending to the backend.
- **FR-03:** Audio chunks must be sent to the backend over WebSocket every ~250ms.
- **FR-04:** The Python backend must accept WebSocket connections at `ws://localhost:8765/ws/audio`.
- **FR-05:** The backend must support two STT engines: Vosk (local) and OpenAI Whisper API (cloud), selectable per session via a config message.
- **FR-06:** The backend must return partial (interim) transcription results as `{"type": "partial", "text": "..."}`.
- **FR-07:** The backend must return final transcription results with translation as `{"type": "final", "original": "...", "translated": "...", "timestamp": "..."}`.
- **FR-08:** Translation must use LibreTranslate (self-hosted at `http://localhost:5000`) for English → Vietnamese.
- **FR-09:** If LibreTranslate is unreachable, the system must still display original text without translation and surface a non-blocking warning.
- **FR-10:** The overlay window must be always-on-top (including over fullscreen applications on macOS), frameless, transparent-background, draggable, and not steal focus from other apps.
- **FR-11:** The overlay must show the current partial text (styled as in-progress) and the last 2 final subtitles.
- **FR-12:** The overlay must auto-hide (fade out) after a configurable delay when no new text arrives, and reappear when new text is received.
- **FR-13:** The overlay appearance (font size, background opacity, line spacing, display mode) must be configurable from the Captions page.
- **FR-14:** The system must save completed sessions and their transcripts to an SQLite database.
- **FR-15:** The History page must display real session data from the database, including session title, duration, language pair, and timestamp.
- **FR-16:** The main process must spawn the Python backend automatically on app launch and terminate it on quit.
- **FR-17:** Global keyboard shortcuts (`Ctrl/Cmd+Shift+R` to toggle session, `Alt/Option+T` to toggle overlay, `Esc` to clear subtitles) must work system-wide.
- **FR-18:** Settings (STT engine, API key, overlay preferences) must persist across app restarts.
- **FR-19:** The system must support macOS and Windows.
- **FR-20:** End-to-end latency from speech to displayed subtitle must be < 3 seconds when using Vosk.

---

## Non-Goals (Out of Scope for MVP)

- **System/loopback audio capture** — MVP supports microphone only. Loopback (capturing system audio for movies) will be added later with virtual audio device drivers (BlackHole on macOS, VB-Cable on Windows).
- **Argos Translate** — Only LibreTranslate is supported for MVP. Argos Translate is shown in Settings but disabled.
- **Multiple language pairs** — MVP supports English → Vietnamese only. Additional pairs are post-MVP.
- **Auto-detect source language** — MVP assumes English as source language.
- **Vocabulary lookup / dictionary** — Clicking on words to see definitions is deferred.
- **Export transcripts** — Exporting to `.txt` / `.csv` is deferred.
- **Custom keyboard shortcut binding** — Shortcuts are hardcoded for MVP.
- **Linux support** — macOS and Windows only.
- **Local Whisper (whisper.cpp)** — Only cloud Whisper API is supported; local Whisper model inference is post-MVP.
- **User accounts / cloud sync** — All data is local.
- **Automatic session titling via AI** — Session titles are derived from the first sentence of the transcript.

---

## Design Considerations

- **Reuse existing UI:** All 4 pages (Dashboard, History, Captions, Settings) and the full shadcn component library are already built. Wire them up to real data rather than rewriting.
- **Design system:** Follow `DESIGN.md` — dark glassmorphism theme, Inter font, 8px spacing grid, electric blue primary, violet secondary, deep charcoal backgrounds.
- **Overlay styling:** The overlay must be readable over any background. Use a semi-transparent dark backdrop with high-contrast white text for original and the tertiary color (`#4cd7f6`) for translated text, matching the existing preview in the Captions page.
- **Responsive overlay:** The overlay should work at various screen resolutions. Default position: bottom-center, 80% of screen width, 120px tall.
- **Animations:** Use smooth opacity transitions for overlay auto-hide/show (300ms ease). VU meter bars should animate smoothly with `requestAnimationFrame`.

---

## Technical Considerations

### Dependencies & Environment

- **Python >= 3.10** required for backend (asyncio improvements, type annotations).
- **Vosk model:** Download `vosk-model-small-en-us-0.15` (~40MB) from https://alphacephei.com/vosk/models. Stored in `backend/models/`.
- **LibreTranslate:** Run via Docker: `docker run -d -p 5000:5000 libretranslate/libretranslate --load-only en,vi`. This downloads ~1GB of language models on first run.
- **OpenAI API key:** Optional, only needed if user selects Whisper engine.

### Cross-Platform Concerns

- **macOS overlay over fullscreen:** Use `alwaysOnTop` with level `screen-saver` and `visibleOnAllWorkspaces: true`.
- **Windows overlay:** Standard `alwaysOnTop: true` works. May need `setAlwaysOnTop(true, 'screen-saver')` for some fullscreen apps.
- **Mic permissions:** macOS requires explicit microphone permission. Electron handles the system prompt, but the app should detect denial and guide the user.
- **Python path:** On macOS, use `python3`; on Windows, use `python` or `py`. The main process should detect the correct binary.

### Performance

- **Vosk** is designed for real-time on CPU. The small English model uses ~50MB RAM and can process faster than real-time on modern hardware.
- **AudioWorklet** runs in a separate thread, so audio processing doesn't block the UI.
- **WebSocket binary messages** are efficient for streaming PCM data (~64KB/s at 16kHz 16-bit mono).

### Security

- `contextIsolation: true` and `nodeIntegration: false` — renderer has no direct Node.js access.
- API keys encrypted via Electron `safeStorage`.
- WebSocket only on `localhost` — no network exposure.

---

## Success Metrics

- User can go from app launch to seeing live bilingual subtitles in < 60 seconds (including backend startup).
- End-to-end latency (speech → displayed subtitle) < 3 seconds with Vosk, < 5 seconds with Whisper.
- Overlay is readable and usable over fullscreen video players (VLC, browser fullscreen) on both macOS and Windows.
- Session history correctly shows all past sessions with full transcripts.
- CPU usage stays under 30% on a mid-range machine (4-core, 8GB RAM) during active Vosk streaming.
- App does not crash or hang during 30-minute continuous sessions.

---

## Open Questions

1. **LibreTranslate startup:** Should the app also manage (spawn/stop) a LibreTranslate Docker container, or require the user to run it separately? For MVP, requiring the user to run it separately is simpler.
2. **Session auto-naming:** Should session titles be the first N words of the transcript, or should the user be prompted to name sessions?
3. **Overlay window on multiple monitors:** Should the overlay remember which monitor it was on, or always default to the primary display?
4. **Audio format negotiation:** Some browsers/devices may not support 16kHz natively. Should the AudioWorklet handle resampling from 44.1kHz/48kHz to 16kHz?
5. **Whisper buffering strategy:** What is the optimal buffer duration before sending to Whisper API? 3 seconds? 5 seconds? Should silence detection trigger early sends?
