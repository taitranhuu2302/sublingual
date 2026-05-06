# US-011: Full WebSocket Pipeline (Audio → STT → Translate → Response)

### US-011: Full WebSocket Pipeline (Audio → STT → Translate → Response)

**Description:** As a user, I want the full pipeline working end-to-end: I speak into my mic, and I see both the original English text and Vietnamese translation appear in real-time.

**Priority Note:** For MVP delivery order, this task should first be completed with the Vosk path. Whisper-specific completion can follow later.

**Acceptance Criteria:**
- [ ] WebSocket handler in `main.py` orchestrates the full flow:
  1. Receives config message → selects STT engine
  2. Receives binary audio chunks → feeds to STT engine
  3. On partial result → sends `{"type": "partial", "text": "..."}` to client
  4. On final result → translates via LibreTranslate → sends `{"type": "final", "original": "...", "translated": "...", "timestamp": "ISO8601"}` to client
- [ ] Multiple concurrent WebSocket connections are supported (one per session)
- [ ] Each connection maintains its own STT engine instance (no shared state between sessions)
- [ ] If Whisper is selected, the pipeline uses a default buffer of about 3 seconds and may flush earlier on silence or session end
- [ ] Latency from speech to displayed subtitle is < 3 seconds (with Vosk)
- [ ] Typecheck/lint passes

---
