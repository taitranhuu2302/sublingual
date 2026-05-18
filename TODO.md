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

- [ ] Set up dependency injection and application bootstrap
  - Choose a simple DI mechanism for the Avalonia application
  - Register services for audio capture, STT, translation, and overlay state
  - Separate startup flow for development and production modes

## 2. UI Shell

- [ ] Complete the Avalonia application shell
  - Main window with a clear lifecycle
  - Separate overlay window lifecycle
  - Optional settings/debug window

- [ ] Create the overlay subtitle window
  - Borderless
  - Transparent
  - Always on top
  - Click-through when needed
  - Draggable / positionable

- [ ] Create the overlay view model
  - Show partial text
  - Show final subtitles
  - Update in real time when new transcripts arrive

- [ ] Create a debug/status panel
  - Show the currently selected source
  - Show capture status
  - Show partial/final transcript output for debugging
  - Show runtime errors and guidance

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

- [ ] Create a chunking strategy
  - Fixed windows
  - Ability to move to a sliding-window strategy later if needed

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

- [ ] Render subtitles in real time
  - Separate style for partial text
  - Separate style for final text
  - Show original and translated text together

- [ ] Improve readability
  - Font size
  - Stroke / shadow / background
  - Spacing
  - Optional fade in/out

- [ ] Add overlay options
  - Auto-hide
  - Position persistence
  - Display mode: bilingual / original only / translated only

## 10. Settings and Configuration

- [ ] Create the settings model
  - STT provider
  - Translation provider
  - API keys
  - Overlay settings
  - Audio capture preferences

- [ ] Persist settings
  - Local file or user config directory
  - Safe load/save behavior

- [ ] Create the settings UI
  - API key form
  - Overlay preferences
  - Audio source preferences

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
- [ ] 6. Implement the macOS `ScreenCaptureKit + P/Invoke` bridge
- [ ] 7. Add settings, persistence, and history
- [ ] 8. Finish packaging and documentation
