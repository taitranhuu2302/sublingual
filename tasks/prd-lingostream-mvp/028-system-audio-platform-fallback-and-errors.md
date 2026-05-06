# US-028: System Audio Platform Fallback and Errors

### US-028: System Audio Platform Fallback and Errors

**Description:** As a user, I want understandable error states and fallback guidance when system audio capture is not available on my machine.

**Acceptance Criteria:**
- [ ] Handle unsupported platform/build with explicit message.
- [ ] Handle permission-denied and unavailable source states with actionable guidance.
- [ ] Handle missing persisted source by forcing reselection.
- [ ] Do not silently fall back to microphone.
- [ ] Keep session safe: invalid state prevents Start Session.
- [ ] Typecheck/lint passes.

---
