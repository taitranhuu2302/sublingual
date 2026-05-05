# Technical Considerations

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
