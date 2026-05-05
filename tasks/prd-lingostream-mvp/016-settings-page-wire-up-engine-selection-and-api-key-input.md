# US-016: Settings Page — Wire Up Engine Selection and API Key Input

### US-016: Settings Page — Wire Up Engine Selection and API Key Input

**Description:** As a user, I want the Settings page to persist my STT and translation engine choices, and allow me to enter an API key for Whisper.

**Acceptance Criteria:**
- [ ] STT engine radio buttons update `session-store.sttEngine`
- [ ] When "Whisper (Cloud)" is selected, show a text input for OpenAI API key below the radio group
- [ ] API key is stored securely via Electron's `safeStorage` API (encrypted at rest)
- [ ] API key is sent to the backend via an environment variable when spawning the Python process
- [ ] Translation engine radio buttons are functional (LibreTranslate pre-selected, Argos shown but disabled with "Coming Soon" badge)
- [ ] "Save Configuration" button persists all settings to a JSON file in userData via IPC
- [ ] "Revert Defaults" button resets all settings to defaults
- [ ] Settings are loaded on app startup and applied to Zustand stores
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---
