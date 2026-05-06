# US-026: Dual Mode Dashboard and Guardrails

### US-026: Dual Mode Dashboard and Guardrails

**Description:** As a user, I want Dashboard controls to work for both source modes with clear start/stop behavior and error guidance.

**Acceptance Criteria:**
- [ ] Add `Input Source Type` selector: `Microphone` and `System Audio`.
- [ ] Show source-specific selector based on active mode.
- [ ] Disable Start Session when source selection is invalid.
- [ ] Show actionable guidance for unsupported/unconfigured/missing source states.
- [ ] Keep existing telemetry and status behavior, but include active source mode in messages.
- [ ] Typecheck/lint passes.
- [ ] **[UI]** Verify in browser using dev-browser skill.

---
