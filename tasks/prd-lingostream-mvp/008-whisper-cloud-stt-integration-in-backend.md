# US-008: Whisper Cloud STT Integration in Backend

### US-008: Whisper Cloud STT Integration in Backend

**Description:** As a user, I want the option to use OpenAI Whisper API for higher-accuracy transcription when I have internet.

**Priority:** Lowest implementation priority in the current MVP backlog. Complete the Vosk-first end-to-end flow before starting this task.

**Acceptance Criteria:**
- [ ] Add `openai` to `requirements.txt`
- [ ] Create `backend/engines/stt_whisper.py`
- [ ] Buffers incoming audio chunks and sends to OpenAI Whisper API every ~3 seconds by default, with early flush on silence detection or session end
- [ ] API key read from environment variable `OPENAI_API_KEY`
- [ ] Returns final text results in the same format as Vosk: `{"type": "final", "text": "..."}`
- [ ] Partial results are synthesized from accumulated buffer text (optional: send `{"type": "partial", "text": "buffering..."}` while waiting)
- [ ] If API key is missing or API call fails, return `{"type": "error", "message": "..."}` and fall back gracefully
- [ ] Typecheck/lint passes

---
