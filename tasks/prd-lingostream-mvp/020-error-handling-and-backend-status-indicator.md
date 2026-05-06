# US-020: Error Handling and Backend Status Indicator

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
- [ ] If system audio is unsupported, unavailable, unconfigured, or the previously saved source is missing, Dashboard shows actionable guidance and prevents session start
- [ ] The app must not silently fall back from system audio to microphone after a source validation failure
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---

## Functional Requirements

- This task must align with `90-functional-requirements.md`, especially:
- `FR-13` for non-blocking LibreTranslate warnings.
- `FR-27` for blocking invalid source starts with clear warnings.
- `FR-29` for explicit reselection when a saved desktop source is missing.
- `FR-30` for Whisper buffering behavior that may affect user-facing status copy.

---

## Non-Goals (Out of Scope for MVP)

- **Mixed-source capture and automatic source failover** — MVP planning allows microphone and system/desktop audio as separate source types, but does not include mixing them in one session or silently falling back from one source to another.
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
