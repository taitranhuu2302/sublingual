# Speaking Practice - Master Plan & Task Checklist (TODO.md)

This document is the main execution plan and checklist for building the real-time AI Speaking Practice system in Sublingual.

---

## 1. Documentation Index

The technical details and architectural specifications are divided into functional files within this directory:
1.  **[OVERVIEW.md](file:///Users/taitran/Desktop/sublingual/docs/speaking-practice/OVERVIEW.md)**: Architectural design, cross-platform dependencies, and system-wide data flow.
2.  **[AUDIO-CAPTURE.md](file:///Users/taitran/Desktop/sublingual/docs/speaking-practice/AUDIO-CAPTURE.md)**: Technical guide on local microphone capturing (Windows NAudio WASAPI / macOS AVFoundation native C++ bridge), format normalization, and voice activity detection (VAD).
3.  **[AI-INTEGRATION.md](file:///Users/taitran/Desktop/sublingual/docs/speaking-practice/AI-INTEGRATION.md)**: Details on local Speech-to-Text (STT) processing via **Vosk**, strict LLM conversational tutor prompt, structured grammar enhancement response, and JSON output for hint suggestions.
4.  **[TTS-ENGINE.md](file:///Users/taitran/Desktop/sublingual/docs/speaking-practice/TTS-ENGINE.md)**: Details on converting AI response text to spoken audio via local system synthesizers (Windows SpeechSynthesizer, macOS process hooks) and audio playback queues.
5.  **[UI-DESIGN.md](file:///Users/taitran/Desktop/sublingual/docs/speaking-practice/UI-DESIGN.md)**: UI mockup, SukiUI controls hierarchy, glassmorphism guidelines, animated levels, grammar tip cards, thinking state animations, and MVVM properties bindings.

---

## 2. Feature Implementation Checklist

Follow this phase-by-phase task list during development:

### Phase 1: Core Domain Entities (`Sublingual.Domain`)
- [ ] **Define Entities & Models**:
  - `PracticeMessage` structure representing individual conversation dialogue boxes (now including an optional `EnhancementAdvice` text field).
  - `SpeakingSessionState` enum capturing state flow (`Idle`, `Listening`, `Transcribing`, `AiThinking`, `AiSpeaking`, `Paused`).
- [ ] **Define System Contracts**:
  - `IMicrophoneCaptureService` for microphone streaming.
  - `ITtsService` interface for audio synthesis playback.
  - `IAiTutorService` representing the unified LLM backend interface.

### Phase 2: Hardware Integrations & TTS (`Sublingual.Infrastructure`)
- [ ] **Wasapi Input Capturer**: Implement `WasapiMicrophoneCaptureService` utilizing NAudio.
- [ ] **AVFoundation macOS Capturer**: Implement `AvFoundationMicrophoneService` inside `ScreenCaptureKitBridge` wrapper `dylib`.
- [ ] **Voice Activity Detection**: Build C# `SilenceDetectionProcessor` trigger based on root-mean-square (RMS) energy thresholds.
- [ ] **Local Synthesis**: Build `LocalSystemTtsService` for Windows (System.Speech) and macOS (Objective-C AVSpeech / say command process).
- [ ] **Audio Sync Queue**: Implement interruption handling in TTS playback to safely cut off audio whenever the user speaks or clicks skip.

### Phase 3: AI Cloud Engines & Logic (`Sublingual.Application` & `Sublingual.Infrastructure/AI`)
- [ ] **Local STT Pipe**: Wire up the existing `VoskTranscriptionService` to feed the completed voice recording text directly to the conversational orchestrator, triggering a `"Transcribing"` state overlay.
- [ ] **Groq client**: Write text-completion handler supporting the unified JSON response (Tutor Reply, Grammar Enhancement feedback, Suggestion Hints).
- [ ] **Gemini client**: Write text-completion handler supporting JSON schema validation.
- [ ] **Strict System Prompt**: Construct the strict tutoring prompt instructing the LLM to analyze the user's syntax and stay 100% committed to the topic.
- [ ] **Session Coordinator**: Implement `SpeakingSessionManager` state machine holding sliding token context window and orchestrating the audio capture -> Vosk STT -> LLM (Thinking indicators) -> TTS pipeline.

### Phase 4: SukiUI Interactive UI (`Sublingual.UI` & `Sublingual.App`)
- [ ] **Settings panel**: Add text boxes for Groq/Gemini API keys and slider configurations for VAD pause delays.
- [ ] **SpeakingPractice View**:
  - Build `PracticeSessionView` featuring SukiUI `GlassCard` and blur aesthetics.
  - Build a constructive **Grammar Tip Card** nested below user chat bubbles.
  - Implement full-screen or panel-level **`BusyArea`** and **`Loading`** loops to trigger while `IsThinking` is active.
  - Build custom microphone `LevelMeter` representing RMS values dynamically.
  - Render beautiful, clickable suggestion chips at the bottom.
- [ ] **MVVM Bindings**: Complete `PracticeSessionViewModel` registering all reactive buttons and state bindings.

---

## 3. Verification & Validation Milestones

*   **Test 1 (Microphone Loopback)**: Capture microphone data, normalize it to 16kHz mono PCM, save to a temporary `.wav` file, and verify acoustic clarity.
*   **Test 2 (VAD Precision)**: Verify that normal spoken phrases trigger auto-commit seamlessly within 1.2 to 1.5 seconds of pausing, without clipping sentences prematurely.
*   **Test 3 (Suggestion & Tip Verification)**: Ensure parsed JSON response contains constructive grammar tips if an intentional error is introduced, along with context-aware suggestions matching the topic.
*   **Test 4 (Acoustic Feedback Check)**: Test with high volume. Confirm the app does not record its own speakers, preventing infinite loops.
*   **Test 5 (UX Loading Check)**: Verify that during slow internet responses, the `BusyArea` overlay holds properly, locking out buttons and showing a clear "Thinking" state so users are guided appropriately.
