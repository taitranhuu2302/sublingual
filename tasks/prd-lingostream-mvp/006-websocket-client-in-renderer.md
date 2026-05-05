# US-006: WebSocket Client in Renderer

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
