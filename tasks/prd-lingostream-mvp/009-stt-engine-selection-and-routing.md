# US-009: STT Engine Selection and Routing

### US-009: STT Engine Selection and Routing

**Description:** As a user, I want to choose between Vosk and Whisper in the Settings page, and have my choice take effect for the next session.

**Priority Note:** In the current MVP backlog, implementation should work with the Vosk-first path before the Whisper path is completed. Any engine selection UX or routing shipped early must not block the Vosk-only flow.

**Acceptance Criteria:**
- [ ] WebSocket `/ws/audio` accepts an initial text message `{"type": "config", "stt_engine": "vosk" | "whisper"}` before audio streaming begins
- [ ] Backend routes audio to the selected engine based on the config message
- [ ] Settings page radio buttons (already in UI) are wired to the Zustand store `sttEngine` field
- [ ] Selected engine persists across app restarts (saved via IPC to main process, stored in electron-store or a JSON file in userData)
- [ ] If Whisper is selected but `OPENAI_API_KEY` is not set, show a warning badge in Settings and an input field for the API key
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---
