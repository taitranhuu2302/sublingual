# Functional Requirements

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
