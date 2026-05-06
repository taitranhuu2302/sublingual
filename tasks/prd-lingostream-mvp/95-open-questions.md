# MVP Decisions Log

## Resolved Decisions for MVP Planning

1. **LibreTranslate startup**
   - **Decision:** The app must not spawn or manage a LibreTranslate Docker container in MVP. LibreTranslate is an external prerequisite that the user or developer runs separately.
   - **Rationale:** This keeps the desktop app simpler, avoids Docker lifecycle management, and matches the non-blocking fallback behavior when translation is unavailable.

2. **Session auto-naming**
   - **Decision:** Session titles must be auto-generated from the first 6-10 words of the first final transcript. If no usable transcript exists, fall back to `Session YYYY-MM-DD HH:mm`.
   - **Rationale:** This avoids interrupting the realtime flow with a naming prompt while still producing useful history entries in MVP.

3. **Overlay window on multiple monitors**
   - **Decision:** The overlay must remember its last monitor and position. If that monitor is unavailable on the next launch, the overlay falls back to the primary display.
   - **Rationale:** This gives better multi-monitor usability without adding much implementation risk.

4. **Audio format negotiation**
   - **Decision:** The capture layer must resample and normalize native audio input to PCM 16kHz mono 16-bit before sending it to the backend.
   - **Rationale:** This preserves a single backend audio contract across microphone and future system-audio paths.

5. **Whisper buffering strategy**
   - **Decision:** Whisper integration must use a default buffer of about 3 seconds and may flush earlier on silence or when the session ends.
   - **Rationale:** Three seconds is a pragmatic balance between latency and transcription quality for MVP.

6. **System audio strategy on macOS**
   - **Decision:** MVP system/desktop audio capture on macOS is planned around a virtual audio device such as BlackHole or an equivalent setup.
   - **Rationale:** This is the clearest and lowest-risk planning path for macOS system audio in MVP.

7. **System audio strategy on Windows**
   - **Decision:** MVP system/desktop audio capture on Windows is planned as loopback-first. Virtual cable setup is deferred as a fallback option, not the default strategy.
   - **Rationale:** This keeps the default Windows path closer to native system capabilities and reduces setup burden.

8. **Source model per session**
   - **Decision:** Each MVP session must have exactly one active primary source. Mixing microphone and system audio in the same session is out of scope.
   - **Rationale:** Mixed-source capture adds synchronization, gain, and UX complexity that is unnecessary for the first rollout.

9. **Dashboard fallback UX**
   - **Decision:** When system/desktop audio is unsupported, unavailable, unconfigured, or missing, the Dashboard must disable Start Session and show short, actionable guidance. The app must not silently fall back to microphone.
   - **Rationale:** Explicit guidance is safer than implicit fallback and avoids capturing the wrong source by mistake.

10. **Persistence semantics**
   - **Decision:** If a previously saved desktop source is missing on a later launch, the app must require explicit reselection instead of silently falling back to microphone.
   - **Rationale:** This avoids incorrect capture behavior and stays consistent with the one-source-per-session rule and invalid-start safeguards.
