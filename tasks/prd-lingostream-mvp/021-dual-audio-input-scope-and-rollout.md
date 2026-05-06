# US-021: Dual Audio Input Scope and Rollout

### US-021: Dual Audio Input Scope and Rollout

**Description:** As a product and engineering team, we want to define the scope, rollout order, and constraints for supporting both microphone and system/desktop audio input so future implementation can proceed without re-planning the feature.

**Goal:** Document the product scope for dual audio input before any implementation work begins.

**Requirements:**
- [ ] The PRD must define two source types: `microphone` and `system`.
- [ ] A session must use one primary audio source at a time in the initial rollout.
- [ ] The existing microphone pipeline remains the current implementation baseline.
- [ ] System/desktop audio support is documented as planned work, not active implementation in this phase.
- [ ] The PRD must describe where the capture strategy differs between microphone input and system/desktop audio input.
- [ ] The PRD must note that system/desktop audio capture is platform-sensitive on macOS and Windows.
- [ ] The PRD must describe a phased rollout order: documentation first, implementation later.
- [ ] The PRD must state that automatic fallback from a missing or invalid system source to microphone is out of scope.

**Acceptance Criteria:**
- [ ] Overview, requirements, and open-questions documents all reference the same dual-audio scope.
- [ ] Documentation makes it clear that implementation has not started yet.
- [ ] The one-source-per-session rule is stated explicitly.
- [ ] Dependencies, risks, and deferred work are captured clearly enough for a coding agent to start implementation later without guessing scope.

**Out of Scope:**
- [ ] Mixing microphone and system audio in the same session.
- [ ] Per-application audio capture.
- [ ] Source-aware backend processing changes.
- [ ] Automatic source failover.
- [ ] Silent fallback from system audio to microphone when a saved source is missing.

**Dependencies:**
- [ ] Update `00-overview-goals-architecture.md`.
- [ ] Update `90-functional-requirements.md`.
- [ ] Update `95-open-questions.md`.

**Risks / Notes:**
- [ ] Desktop audio capture may require different platform-specific strategies on macOS and Windows.
- [ ] The user experience for system audio may depend on OS permissions, desktop source selection, or virtual audio device setup.

---
