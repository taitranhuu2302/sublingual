# TODO

This file tracks the remaining work for Sublingual, with a focus on the native desktop architecture based on `Avalonia + .NET + native audio capture`.

## 1. Foundation

- [ ] Finalize the official stack and repository boundaries
  - Confirm that the repository will move forward with the native `.NET/Avalonia` direction
  - Confirm the roles of `src/`, `native/`, `docs/`, and `scripts/`
  - Remove or isolate experimental legacy parts that are no longer part of the plan

- [ ] Complete the solution structure
  - Replace placeholders with real files and folders in each project
  - Define consistent namespace, folder, and file naming conventions
  - Normalize project references according to the architecture layers

- [x] Set up dependency injection and application bootstrap
  - `Microsoft.Extensions.DependencyInjection` used as DI mechanism
  - Services registered: audio capture, STT, translation, overlay state (`AudioCaptureDebugSession`)
  - Design-time / platform-switch startup flow implemented in `AppBootstrapper`

## 2. UI Shell

- [x] Complete the Avalonia application shell
  - `MainWindow` now uses a sidebar layout with `Capture` and `Overlay` tabs
  - `OverlayWindow` lifecycle is managed via toggle, not auto-shown on startup
  - Both are wired/disposed via `App.axaml.cs` using `AppBootstrapper`

- [x] Create the overlay subtitle window
  - Borderless, transparent, always on top
  - Draggable via pointer drag anywhere on the window
  - Manual close is intercepted and converted into hide to keep the shared instance alive
  - Click-through not yet implemented

- [x] Create the overlay view model
  - Shows placeholder text, partial/final captions, and translated text
  - Supports overlay font size, width, height, theme, and opacity state
  - Updates in real time via `AudioCaptureDebugSession.TranscriptPreviewUpdated` event

- [x] Create a debug/status panel
  - Device picker, capture controls, and runtime log exist in `Capture` tab
  - Capture status, audio level meter, peak level, chunk count, and transcript preview are surfaced
  - Empty/placeholder-heavy blocks were reduced or hidden when they have no value
  - Still needs better runtime guidance / structured error presentation

## 3. Audio Domain Model

- [ ] Define audio contracts in `Sublingual.Domain`
  - `AudioSourceType`
  - `AudioChunk`
  - `IAudioCaptureService`
  - `IAudioChunkProcessor`
  - `ITranscriptionService`
  - `ITranslationService`

- [ ] Define the session model
  - Session lifecycle: idle, starting, capturing, processing, error, stopped
  - Session metadata: source type, language pair, start time, end time
  - Session title strategy

## 4. Windows Audio Capture

- [ ] Integrate `NAudio`
  - Add required packages
  - Create `WasapiLoopbackCaptureService`
  - Enumerate playback devices
  - Select the default output or a specific output device

- [ ] Save captured audio to a file for pipeline verification
  - Capture system audio to `.wav`
  - Verify sample rate, channel count, and format

- [ ] Normalize audio data
  - Convert to mono when needed
  - Downsample to `16kHz`
  - Convert into the format expected by the STT pipeline

## 5. macOS Native Audio Plugin

- [ ] Complete the C / Objective-C++ bridge for `ScreenCaptureKit`
  - Implement `screen_capture_bridge.mm`
  - Manage capture session lifecycle
  - Export audio buffer callbacks through a C ABI
  - Verify that capture follows the current active system output device, including built-in speakers, wired headphones, and Bluetooth audio outputs

- [ ] Complete the native build script
  - Build the `.dylib`
  - Place the output where the .NET application can load it
  - Document the prerequisites for macOS native builds

- [ ] Create the `P/Invoke` interop layer in `Sublingual.Interop`
  - `[DllImport]` signatures
  - Native structs
  - Callback delegates
  - Error code mapping

- [ ] Create `ScreenCaptureKitCaptureService`
  - Load the native library
  - Start/stop capture
  - Forward native audio callbacks into the .NET pipeline
  - Handle unsupported macOS versions
  - Validate behavior when the active output route changes between speakers, wired headphones, and Bluetooth headphones

## 6. Audio Processing Pipeline

- [ ] Create an audio buffer manager
  - Receive raw PCM / Float32 data from platform capture services
  - Keep buffering stable without leaking memory

- [ ] Create a resampler
  - Convert to `16kHz mono`
  - Handle different sample rates across Windows and macOS

- [x] Create a chunking strategy
  - `FixedWindowAudioChunkProcessor` (750ms default) implemented in `Sublingual.Infrastructure`
  - Sliding-window strategy deferred

- [ ] Add Voice Activity Detection when needed
  - Split chunks more intelligently
  - Reduce latency and avoid sending long silent regions

## 7. STT Integration

- [ ] Create an abstraction for the STT provider
  - Common transcription interface
  - Allow provider replacement without impacting the UI

- [ ] Integrate the Groq API
  - Manage API keys
  - Send audio chunks in the correct format
  - Parse transcript responses
  - Handle rate limits, timeouts, and retries

- [ ] Define a partial/final transcript strategy
  - Show partial results if the provider supports them
  - If true partials are unavailable, provide a reasonable debug/placeholder strategy

## 8. Translation Integration

- [ ] Create an abstraction for the translation provider
  - Translation service interface
  - Request/response DTOs

- [ ] Integrate Cloudflare Workers AI or Gemini
  - Manage API keys and configuration
  - Send original transcript text to the translation service
  - Return translated text for overlay rendering

- [ ] Optimize the transcript -> translation pipeline
  - Do not block the UI thread
  - Avoid retranslating identical text repeatedly

## 9. Overlay Rendering

- [x] Render subtitles in real time
  - Overlay now renders placeholder, current caption text, and translated text in real time
  - Partial/final text are merged into a single main caption display for a cleaner live-caption feel
  - Original and translated text can both be shown together

- [x] Improve readability
  - Font size is configurable from the `Overlay` tab
  - Overlay has themed card backgrounds, shadow, close button, and compact caption layout
  - Spacing was tightened and non-essential footer chrome removed
  - Fade in/out not implemented yet

- [ ] Add overlay options
  - Theme: `Dark` / `Light` implemented
  - Opacity implemented
  - Width / height implemented
  - Auto-hide not implemented
  - Position persistence not implemented
  - Display mode: bilingual / original only / translated only not implemented

## 10. Settings and Configuration

- [ ] Create the settings model
  - STT provider
  - Translation provider
  - API keys
  - Overlay settings: size, theme, opacity, font size, position
  - Audio capture preferences

- [ ] Persist settings
  - Local file or user config directory
  - Safe load/save behavior

- [ ] Create the settings UI
  - Sidebar/tab shell is now in place and can host future settings pages
  - Overlay preferences currently live inside the `Overlay` tab
  - API key form and audio source preferences still need dedicated settings UI

## 11. Session History and Persistence

- [ ] Choose a persistence mechanism
  - SQLite or local JSON for MVP

- [ ] Save session metadata
  - Session title
  - Start/end time
  - Provider used
  - Source type

- [ ] Save transcript segments
  - Original text
  - Translated text
  - Timestamp

- [ ] Create the history view
  - Session list
  - Session transcript detail view

## 12. Error Handling and UX Guidance

- [ ] Create a unified error model
  - Audio capture error
  - Native plugin load error
  - STT API error
  - Translation API error
  - Permission error

- [ ] Show platform-specific guidance
  - Windows capture issues
  - macOS `ScreenCaptureKit` issues
  - Unsupported OS version
  - Missing native library

- [ ] Add diagnostic logging
  - Application logs
  - Native plugin logs when needed
  - API request/response summaries for debugging

## 13. Packaging and Dev Tooling

- [ ] Complete development scripts
  - `scripts/run-dev.sh`
  - `scripts/build-all.sh`
  - `scripts/build-macos-native.sh`

- [ ] Complete packaging
  - Build macOS app bundle
  - Build Windows executable/package

- [ ] Document development setup
  - How to build the native bridge on macOS
  - How to run the app locally
  - How to configure API keys

## 14. Documentation Cleanup

- [ ] Sync `README.md`, `docs/OVERVIEW.md`, and the new project structure
- [ ] Add documentation for architecture decisions
- [ ] Clearly define MVP vs post-MVP roadmap

## 15. Suggested Execution Order

- [ ] 1. Complete `Domain + Application + Infrastructure` contracts
- [ ] 2. Implement Windows audio capture end to end first
- [ ] 3. Build the audio processing pipeline and save-to-file verification
- [ ] 4. Integrate STT and translation APIs
- [ ] 5. Render the real overlay subtitle flow
  - Current overlay is now interactive and visually closer to a live-caption panel, but still uses mock transcript/translation data
- [ ] 6. Implement the macOS `ScreenCaptureKit + P/Invoke` bridge
- [ ] 7. Add settings, persistence, and history
- [ ] 8. Finish packaging and documentation
