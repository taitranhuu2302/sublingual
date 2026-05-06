# US-025: System Audio Enumeration and Selection

### US-025: System Audio Enumeration and Selection

**Description:** As a user, I want the app to list valid system/desktop audio sources so I can choose the correct source before starting a session.

**Acceptance Criteria:**
- [ ] Expose preload/main API for listing system/desktop audio sources safely.
- [ ] Renderer can load source list for `System Audio` mode.
- [ ] Empty/unavailable/unsupported states are represented explicitly in UI state.
- [ ] If a saved source is missing, UI asks for explicit reselection.
- [ ] Typecheck/lint passes.

---
