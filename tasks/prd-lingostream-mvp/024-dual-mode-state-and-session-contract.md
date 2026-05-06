# US-024: Dual Mode State and Session Contract

### US-024: Dual Mode State and Session Contract

**Description:** As a developer, I need a clear state model and session contract for dual input mode so microphone and system audio can share one stable session lifecycle.

**Acceptance Criteria:**
- [ ] Add source mode model: `microphone | system` in session/dashboard store.
- [ ] Keep one active source per session.
- [ ] Store selected microphone device ID separately from selected system source ID.
- [ ] Add explicit invalid states for: missing selection, unsupported source, missing previously saved source.
- [ ] Block session start when selected source is invalid.
- [ ] No silent fallback from system source to microphone.
- [ ] Typecheck/lint passes.

---
