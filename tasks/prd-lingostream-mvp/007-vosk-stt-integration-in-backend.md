# US-007: Vosk STT Integration in Backend

### US-007: Vosk STT Integration in Backend

**Description:** As a user, I want the Vosk engine to transcribe my speech locally in real-time so I can use the app without internet.

**Acceptance Criteria:**
- [x] Add `vosk` to `requirements.txt`
- [x] Create `backend/engines/stt_vosk.py` that wraps Vosk `KaldiRecognizer`
- [x] Accepts 16kHz mono PCM Int16 audio chunks
- [x] Returns partial results (`{"type": "partial", "text": "..."}`) after each chunk
- [x] Returns final results (`{"type": "final", "text": "..."}`) when Vosk detects end of utterance
- [ ] Vosk model is downloaded to `backend/models/vosk-model-small-en-us/` (document download URL in README)
- [x] Model path is configurable via environment variable `VOSK_MODEL_PATH`
- [ ] Benchmark: processes a 1-second chunk in < 200ms on a 4-core CPU
- [x] Typecheck/lint passes

---
