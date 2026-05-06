# US-006: WebSocket Client in Renderer

### US-006: WebSocket Client in Renderer

**Description:** As a developer, I need a WebSocket client in the renderer that sends PCM audio chunks to the Python backend and receives STT/translation results.

**Acceptance Criteria:**
- [x] Create `src/renderer/src/services/websocket-client.ts`
- [x] Connects to `ws://localhost:8765/ws/audio` when session starts
- [x] Sends binary PCM chunks as ArrayBuffer messages
- [x] Receives JSON messages and dispatches to Zustand store:
  - `{type: "partial", text}` → `updatePartial(text)`
  - `{type: "final", original, translated, timestamp}` → `addSubtitle({...})`
  - `{type: "error", message}` → `setError(message)`
- [x] Handles reconnection: if WebSocket disconnects during a session, attempt reconnect up to 3 times with exponential backoff (1s, 2s, 4s)
- [x] On session stop, sends a `{type: "end_session"}` text message and closes the WebSocket
- [x] Zustand store `status` reflects connection state (`connecting`, `streaming`, `error`)
- [x] Typecheck/lint passes

---
