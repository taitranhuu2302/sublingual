# US-007: Vosk STT Integration in Backend

### US-007: Vosk STT Integration in Backend

**Description:** As a user, I want the Vosk engine to transcribe my speech locally in real-time so I can use the app without internet.

**Acceptance Criteria:**
- [ ] Add `vosk` to `requirements.txt`
- [ ] Create `backend/engines/stt_vosk.py` that wraps Vosk `KaldiRecognizer`
- [ ] Accepts 16kHz mono PCM Int16 audio chunks
- [ ] Returns partial results (`{"type": "partial", "text": "..."}`) after each chunk
- [ ] Returns final results (`{"type": "final", "text": "..."}`) when Vosk detects end of utterance
- [ ] Vosk model is downloaded to `backend/models/vosk-model-small-en-us/` (document download URL in README)
- [ ] Model path is configurable via environment variable `VOSK_MODEL_PATH`
- [ ] Benchmark: processes a 1-second chunk in < 200ms on a 4-core CPU
- [ ] Typecheck/lint passes

---
