# US-023: System Audio Capture Platform Adapter Plan

### US-023: System Audio Capture Platform Adapter Plan

**Description:** As an engineering team, we want a platform-specific capture plan for system/desktop audio so implementation can be scoped without ambiguity.

**Goal:** Document the future capture strategy for system/desktop audio on supported desktop platforms.

**Requirements:**
- [ ] The plan must distinguish microphone capture from system/desktop audio capture.
- [ ] The plan must describe the expected capture path on macOS.
- [ ] The plan must describe the expected capture path on Windows.
- [ ] The plan must call out any likely dependency on virtual audio devices, loopback strategy, or desktop capture APIs.
- [ ] The plan must document fallback behavior when system audio capture is unavailable.
- [ ] The plan must preserve the current downstream assumption that the backend receives PCM 16kHz mono 16-bit audio chunks.
- [ ] The plan must remain implementation-ready without requiring backend redesign in the first pass.
- [ ] The plan must document macOS as a virtual-audio-device-first path for MVP planning.
- [ ] The plan must document Windows as a loopback-first path for MVP planning.
- [ ] The plan must state that saved source failure requires explicit reselection and must not silently fall back to microphone.

**Acceptance Criteria:**
- [ ] The document clearly separates platform assumptions, risks, and deferred decisions.
- [ ] The expected renderer/main/preload/backend responsibilities are described at a high level.
- [ ] Failure modes are listed for unsupported platform, denied permission, missing virtual device, and silent stream scenarios.
- [ ] The document explicitly states that this is a planning artifact and not an active implementation task yet.

**Out of Scope:**
- [ ] Shipping a macOS or Windows adapter in this phase.
- [ ] Automated integration tests for OS media permissions.
- [ ] Mixed-source capture or source-aware STT tuning.

**Dependencies:**
- [ ] `021-dual-audio-input-scope-and-rollout.md`
- [ ] Existing audio pipeline tasks `005-audio-capture-via-getusermedia-audioworklet.md` and `006-websocket-client-in-renderer.md`

**Risks / Notes:**
- [ ] macOS may require a virtual audio device such as BlackHole to make system audio available to the app.
- [ ] Windows may require a loopback-specific strategy or a virtual cable depending on the chosen implementation path.
- [ ] Desktop audio sample rates may differ from microphone defaults and must still fit the existing PCM pipeline.
- [ ] Any source-specific adapter must still normalize output to PCM 16kHz mono 16-bit before WebSocket transmission.

---
