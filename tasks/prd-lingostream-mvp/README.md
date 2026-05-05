# LingoStream MVP PRD Split

PRD da duoc tach thanh cac file nho theo tung chuc nang.

## File Index

- `00-overview-goals-architecture.md`
- `001-python-backend-scaffold-with-fastapi-websocket.md`
- `002-electron-main-process-spawns-python-backend.md`
- `003-preload-script-with-ipc-api.md`
- `004-zustand-store-for-application-state.md`
- `005-audio-capture-via-getusermedia-audioworklet.md`
- `006-websocket-client-in-renderer.md`
- `007-vosk-stt-integration-in-backend.md`
- `008-whisper-cloud-stt-integration-in-backend.md`
- `009-stt-engine-selection-and-routing.md`
- `010-libretranslate-integration-in-backend.md`
- `011-full-websocket-pipeline-audio-stt-translate-response.md`
- `012-dashboard-page-wire-up-real-audio-devices-and-session-control.md`
- `013-overlay-window-always-on-top-subtitle-display.md`
- `014-overlay-window-ipc-communication.md`
- `015-captions-page-wire-up-live-preview-with-real-settings.md`
- `016-settings-page-wire-up-engine-selection-and-api-key-input.md`
- `017-sqlite-session-persistence-in-backend.md`
- `018-history-page-display-real-session-data.md`
- `019-global-keyboard-shortcuts.md`
- `020-error-handling-and-backend-status-indicator.md`
- `90-functional-requirements.md`
- `91-non-goals.md`
- `92-design-considerations.md`
- `93-technical-considerations.md`
- `94-success-metrics.md`
- `95-open-questions.md`

## TODO List (MVP)

### 1) Core Architecture and App Foundation

- [ ] US-001: Python backend scaffold (FastAPI + WebSocket + `/health`)
- [ ] US-002: Electron main process auto-spawn/kill backend, health polling, logs
- [ ] US-003: Preload IPC API + secure BrowserWindow config
- [ ] US-004: Zustand stores for session/overlay state

### 2) Realtime Audio and Streaming Pipeline

- [ ] US-005: Audio capture (`getUserMedia` + `AudioWorklet` PCM 16kHz)
- [ ] US-006: Renderer WebSocket client + reconnect + status updates
- [ ] US-007: Vosk STT engine integration
- [ ] US-008: Whisper cloud STT integration
- [ ] US-009: STT engine selection/routing + persistence + API key warning flow
- [ ] US-010: LibreTranslate integration + fallback behavior
- [ ] US-011: End-to-end pipeline (Audio -> STT -> Translate -> Response)

### 3) UI Wiring and Overlay Experience

- [ ] US-012: Dashboard wire-up (devices, session control, live transcription)
- [ ] US-013: Always-on-top overlay window behavior and rendering
- [ ] US-014: Overlay IPC bridge (main renderer <-> main process <-> overlay)
- [ ] US-015: Captions page real settings + live preview
- [ ] US-016: Settings page engine selection + secure API key storage

### 4) Data, History, and Productivity

- [ ] US-017: SQLite session/transcript persistence in backend
- [ ] US-018: History page with real session data and replay detail
- [ ] US-019: Global keyboard shortcuts
- [ ] US-020: Error handling and backend status indicator

### 5) Release Readiness Checklist

- [ ] Verify all UI tasks marked **[UI]** in browser
- [ ] Confirm FR-01 -> FR-20 are satisfied
- [ ] Validate non-goals are not accidentally implemented in MVP
- [ ] Smoke test macOS and Windows flows
- [ ] Confirm success metrics baseline (latency, stability, CPU)
