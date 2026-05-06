# US-027: System Audio Capture to PCM Pipeline

### US-027: System Audio Capture to PCM Pipeline

**Description:** As a developer, I need the system-audio path to feed the same PCM contract as microphone so backend STT pipeline can remain unchanged.

**Acceptance Criteria:**
- [ ] Implement system-audio capture path and route it through the same PCM pipeline contract.
- [ ] Normalize output to PCM 16kHz mono 16-bit before WebSocket transmission.
- [ ] Ensure stop/cleanup behavior matches microphone path.
- [ ] Keep one WebSocket audio contract (`/ws/audio`, binary chunks).
- [ ] Typecheck/lint passes.

---
