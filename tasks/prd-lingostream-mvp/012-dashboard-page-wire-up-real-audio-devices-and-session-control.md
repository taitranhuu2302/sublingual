# US-012: Dashboard Page — Wire Up Real Audio Devices and Session Control

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
