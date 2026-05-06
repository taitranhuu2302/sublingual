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
- `021-dual-audio-input-scope-and-rollout.md`
- `022-dashboard-source-selection-and-session-ux.md`
- `023-system-audio-capture-platform-adapter-plan.md`
- `024-dual-mode-state-and-session-contract.md`
- `025-system-audio-enumeration-and-selection.md`
- `026-dual-mode-dashboard-and-guardrails.md`
- `027-system-audio-capture-to-pcm-pipeline.md`
- `028-system-audio-platform-fallback-and-errors.md`
- `90-functional-requirements.md`
- `91-non-goals.md`
- `92-design-considerations.md`
- `93-technical-considerations.md`
- `94-success-metrics.md`
- `95-open-questions.md`

## Current Priority

### 1) Do First: Dual Audio Planning Docs

- [x] US-021: Dual audio input scope and rollout
- [x] US-022: Dashboard source selection and session UX
- [x] US-023: System audio capture platform adapter plan

### 2) Next: Dual Mode Implementation Tasks

- [x] US-024: Dual mode state and session contract
- [x] US-025: System audio enumeration and selection
- [x] US-026: Dual mode Dashboard and guardrails
- [ ] US-027: System audio capture to PCM pipeline
- [ ] US-028: System audio platform fallback and errors

### Priority Rules

- [ ] Documentation first for dual audio input support (`microphone` + `system/desktop audio`)
- [x] `US-021`, `US-022`, and `US-023` reviewed and approved for implementation kickoff
- [ ] Keep microphone capture as the current implementation baseline while dual-audio support remains in planning

## TODO List (MVP)

### 3) Core Architecture and App Foundation

- [x] US-001: Python backend scaffold (FastAPI + WebSocket + `/health`)
- [x] US-002: Electron main process auto-spawn/kill backend, health polling, logs
- [x] US-003: Preload IPC API + secure BrowserWindow config
- [x] US-004: Zustand stores for session/overlay state

### 4) Realtime Audio and Streaming Pipeline

- [x] US-005: Audio capture (`getUserMedia` + `AudioWorklet` PCM 16kHz)
- [x] US-006: Renderer WebSocket client + reconnect + status updates
- [ ] US-007: Vosk STT engine integration
- [ ] US-009: STT engine selection/routing + persistence + API key warning flow
- [ ] US-010: LibreTranslate integration + fallback behavior
- [ ] US-011: End-to-end pipeline (Audio -> STT -> Translate -> Response)

### 5) UI Wiring and Overlay Experience

- [ ] US-012: Dashboard wire-up (devices, session control, live transcription)
- [ ] US-013: Always-on-top overlay window behavior and rendering
- [ ] US-014: Overlay IPC bridge (main renderer <-> main process <-> overlay)
- [ ] US-015: Captions page real settings + live preview
- [ ] US-016: Settings page engine selection + secure API key storage

### 6) Data, History, and Productivity

- [ ] US-017: SQLite session/transcript persistence in backend
- [ ] US-018: History page with real session data and replay detail
- [ ] US-019: Global keyboard shortcuts
- [ ] US-020: Error handling and backend status indicator

### 7) Lowest Priority for MVP

- [ ] US-008: Whisper cloud STT integration

### 8) Release Readiness Checklist

- [ ] Verify all UI tasks marked **[UI]** in browser
- [ ] Confirm FR-01 -> FR-30 are satisfied
- [ ] Validate non-goals are not accidentally implemented in MVP
- [ ] Smoke test macOS and Windows flows
- [ ] Confirm success metrics baseline (latency, stability, CPU)

## TODO List (Post-MVP)

- [ ] PS-001: Mixed-source capture, per-app capture, and advanced system/loopback workflows beyond the initial dual-audio scope

## Priority Notes

- [ ] Prioritize the Vosk-based path and end-to-end local MVP flow before starting Whisper work.
- [ ] Treat `US-008` as the last implementation item in the current MVP backlog unless product priorities change.
