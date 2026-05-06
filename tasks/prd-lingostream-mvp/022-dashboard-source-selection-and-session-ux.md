# US-022: Dashboard Source Selection and Session UX

### US-022: Dashboard Source Selection and Session UX

**Description:** As a user, I want the Dashboard to clearly show which audio source type I am using and what I need to select before starting a session.

**Goal:** Define the future Dashboard UX for microphone and system/desktop audio source selection without implementing it yet.

**Requirements:**
- [ ] The Dashboard must expose an `Input Source Type` selector with `Microphone` and `System Audio` options.
- [ ] When `Microphone` is selected, the Dashboard must show a microphone device selector.
- [ ] When `System Audio` is selected, the Dashboard must show a desktop/system source selector.
- [ ] The Start Session control must stay shared across both source types.
- [ ] The Dashboard must disable Start Session when the required source selection is incomplete.
- [ ] The Dashboard must show short operational guidance when system audio is unavailable, unsupported, or not yet configured.
- [ ] Telemetry copy must identify the active source type during start, stream, and failure states.
- [ ] If a previously saved desktop source is missing, the Dashboard must require explicit reselection before session start.
- [ ] The Dashboard must not silently switch the user from system audio to microphone.

**Acceptance Criteria:**
- [ ] The UX requirements are specific enough that a frontend coder can implement the flow without redefining product behavior.
- [ ] Empty, loading, and unavailable states are documented for both source types.
- [ ] The document states that only one source type is active per session in the initial rollout.
- [ ] The document avoids redesigning unrelated Dashboard features.
- [ ] The documented invalid states include unsupported platform, not configured, source unavailable, and previously saved source missing.

**Out of Scope:**
- [ ] Final visual design exploration beyond the current Dashboard layout.
- [ ] Implementation of desktop source enumeration.
- [ ] Native permission onboarding flows.

**Dependencies:**
- [ ] `021-dual-audio-input-scope-and-rollout.md`
- [ ] Existing Dashboard session flow in `012-dashboard-page-wire-up-real-audio-devices-and-session-control.md`

**Risks / Notes:**
- [ ] Desktop source names may be long or unclear depending on platform APIs.
- [ ] System audio may need additional guidance if the OS does not expose a usable audio path by default.
- [ ] macOS guidance should explicitly mention a virtual audio device requirement such as BlackHole for MVP planning.
- [ ] Windows guidance should assume a loopback-first strategy and only mention virtual cable setup as a fallback or later option.

---
