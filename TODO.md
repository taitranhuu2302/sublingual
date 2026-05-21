# TODO

This file tracks the remaining work for Sublingual, with a focus on the native desktop architecture based on `Avalonia + .NET + native audio capture`.

## 1. Foundation

- [ ] Finalize the official stack and repository boundaries
  - Confirm that the repository will move forward with the native `.NET/Avalonia` direction
  - Confirm the roles of `src/`, `native/`, `docs/`, and `scripts/`
  - Remove or isolate experimental legacy parts that are no longer part of the plan

- [ ] Complete the solution structure
  - `Domain`, `Application`, `Infrastructure`, `Interop`, `Desktop`, and `App` projects now exist and build together
  - Some project boundaries are still blurred, especially inside `Sublingual.App`
  - Placeholder / low-value artifacts still exist, for example `src/Sublingual.UI/Class1.cs`

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

- [x] Define audio contracts in `Sublingual.Domain`
  - `AudioSourceType`
  - `AudioChunk`
  - `IAudioCaptureService`
  - `IAudioChunkProcessor`
  - `ITranscriptionService`
  - `ITranslationService`

- [ ] Define the session model
  - Core session types exist (`SessionInfo`, `SessionState`, capture metadata, saved transcript entries)
  - Runtime lifecycle is still spread across `AudioCaptureState`, `AudioCaptureDebugSession`, and persisted session records
  - Session title strategy is still missing
  - Session folder semantics still need to be formalized: user-defined tree path, folder display name, sanitized persisted path, and the fallback `global` bucket when no folder is chosen

## 4. Windows Audio Capture

- [x] Integrate `NAudio`
  - Required packages are referenced
  - `WasapiLoopbackCaptureService` exists
  - Playback devices can be enumerated
  - The default output or a specific output device can be selected

- [x] Save captured audio to a file for pipeline verification
  - Captured audio is saved to session `.wav` output
  - Session file output is already used in the capture debug pipeline
  - Further verification / inspection tooling could still improve diagnostics

- [x] Normalize audio data
  - Shared `AudioFormatNormalizer` now converts to mono when needed
  - Shared resampling path now targets `16kHz`
  - `VoskInputVerifier` now guards the STT pipeline input as `16kHz mono PCM16`

## 5. macOS Native Audio Plugin

- [ ] Complete the C / Objective-C++ bridge for `ScreenCaptureKit`
  - Implement `screen_capture_bridge.mm`
  - Manage capture session lifecycle
  - Export audio buffer callbacks through a C ABI
  - Verify that capture follows the current active system output device, including built-in speakers, wired headphones, and Bluetooth audio outputs

- [ ] Complete the native build script
  - `scripts/build-macos-native.sh` and `native/macos/ScreenCaptureKitBridge/build.sh` exist
  - Output copy and prerequisites need better end-to-end validation and documentation

- [ ] Create the `P/Invoke` interop layer in `Sublingual.Interop`
  - Basic interop files and callback delegates already exist
  - Native structs / error mapping still need tightening and validation

- [ ] Create `ScreenCaptureKitCaptureService`
  - Service skeleton exists and is wired into DI for macOS
  - Start/stop and callback forwarding paths exist at code level
  - Unsupported-version handling, native validation, and route-switch behavior still need real platform testing

## 6. Audio Processing Pipeline

- [ ] Create an audio buffer manager
  - `FixedWindowAudioChunkProcessor` currently acts as the effective rolling buffer for chunk emission
  - A clearer dedicated buffer manager abstraction still does not exist

- [x] Create a resampler
  - `AudioFormatNormalizer` now converts incoming chunks to `16kHz mono PCM16`
  - The implementation currently uses a simple linear resampling path and may need further quality tuning later

- [x] Create a chunking strategy
  - `FixedWindowAudioChunkProcessor` (750ms default) implemented in `Sublingual.Infrastructure`
  - Sliding-window strategy deferred

- [ ] Add Voice Activity Detection when needed
  - Split chunks more intelligently
  - Reduce latency and avoid sending long silent regions

## 7. STT Integration

- [x] Create an abstraction for the STT provider
  - `ITranscriptionService` is in place and the UI consumes the abstraction
  - Current direction is intentionally `Vosk` only, not provider-swappable cloud STT

- [ ] Keep `Vosk` as the STT provider
  - Local Vosk transcription is the primary STT path now
  - Model management and selection UX exist, but can still be improved
  - Input normalization / verification now exists, but model-language validation is still heuristic

- [x] Define a partial/final transcript strategy
  - Partial and final transcript flow is already implemented in the realtime session pipeline
  - Translation is now final-only by default, with optional partial translation toggle

## 8. Translation Integration

- [x] Create an abstraction for the translation provider
  - `ITranslationService` exists in `Sublingual.Domain`
  - Runtime execution now also uses `ITranslationExecutionService` for provider diagnostics and cache metadata

- [x] Integrate translation providers via factory
  - Provider selection from settings is implemented
  - `GoogleTranslateFreeApi` is implemented
  - `LibreTranslate` is implemented
  - Ordered fallback across multiple providers is implemented

- [ ] Optimize the transcript -> translation pipeline
  - UI-thread blocking is already avoided in the current async pipeline
  - Repeated translation is reduced through bounded cache and final-only default behavior
  - The processing pipeline is still serialized and can still add latency under slow providers

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
  - Position persistence implemented
  - Auto-hide not implemented
  - Display mode: bilingual / original only / translated only not implemented

## 10. Settings and Configuration

- [x] Create the settings model
  - STT model selection exists
  - Translation provider / factory settings exist
  - API key / endpoint settings exist for translation providers
  - Overlay settings exist: size, theme, opacity, font size, line height, position
  - Audio capture preferences are still minimal

- [x] Persist settings
  - Settings persist to local user config storage via `AppSettingsStore`
  - Safe load/save fallback behavior exists

- [ ] Create the settings UI
  - Sidebar/tab shell is in place
  - Speech, translation, and overlay settings UI now exist
  - Translation provider testing UI exists
  - Audio source preferences still need deeper settings UI
  - Storage folder settings still use free-text input and should switch to OS explorer/folder-picker interaction

## 11. Session History and Persistence

- [x] Choose a persistence mechanism
  - Local JSON / file-based persistence is already being used for the MVP

- [ ] Save session metadata
  - Model, device, language, duration, and created-at are already persisted
  - Session title, provider details, and fuller session metadata are still incomplete
  - Folder identity metadata still needs to move from `TreePath` semantics to stable flat-folder ownership (`FolderId`, display name, storage slug)

- [x] Save transcript segments
  - Original text, translated text, and timestamp are persisted for session playback/export

- [x] Create the history view
  - Session list exists
  - Session transcript detail view exists
  - The list UX still needs refinement

- [ ] Refactor session storage into flat group folders
  - Replace nested `tree path` ownership with single-level `SessionFolder` groups
  - Introduce a persistent folder store (`folders.json` or equivalent) with a protected default folder `Global`
  - Persist stable folder ownership on each capture record using `FolderId` semantics instead of raw path strings
  - Add migration rules for existing path-based sessions so old data still loads safely

- [ ] Refactor the capture start flow around folder selection only
  - Remove all folder creation and path-entry behavior from the `Capture` tab
  - Let users choose only an existing folder before starting capture
  - Fallback to `Global` when no valid folder is selected
  - Persist the last selected folder by stable folder identity, not path text

- [ ] Redesign the sessions page around folder-first management
  - Left pane: flat folder browser with capture counts and `Global` badge
  - Right pane: capture records for the selected folder, with stable columns and readable bulk actions
  - Keep folder management and capture-record management visually separate
  - Remove nested-folder and tree-path mental models from the UI

- [ ] Add folder CRUD in the sessions page
  - Create folder via dialog with realtime validation and no path input
  - Rename non-default folders via dialog with duplicate-name and invalid-character validation
  - Delete non-default folders directly when empty
  - Delete non-default folders by moving existing captures to `Global` first when they are not empty

- [ ] Add capture record move management between folders
  - Allow single-record move via folder picker UI
  - Allow multi-select move via the same picker pattern
  - Move physical capture directories and metadata together so history reload remains correct
  - Do not expose manual filesystem path entry in this flow

- [ ] Remove capture log concepts from the end-user UI
  - Remove the capture log panel from the capture screen because users do not understand or need it
  - Replace user-facing log-heavy feedback with clearer capture status/progress messaging where still necessary
  - Keep any developer diagnostics only if they are hidden from the normal UX and still serve debugging needs

## 12. Error Handling and UX Guidance

- [ ] Create a unified error model
  - Errors are currently surfaced ad hoc through status text, runtime log, and transcript preview diagnostics
  - A formal shared error model is still missing

- [ ] Show platform-specific guidance
  - Windows capture issues
  - macOS `ScreenCaptureKit` issues
  - Unsupported OS version
  - Missing native library

- [ ] Add diagnostic logging
  - Runtime log, translation diagnostics, provider/fallback details, and Vosk input verification summaries now exist in-app
  - Formal application/native log sinks are still missing

## 13. Packaging and Dev Tooling

- [ ] Complete development scripts
  - `scripts/run-dev.sh`, `scripts/build-all.sh`, and `scripts/build-macos-native.sh` already exist
  - They still need stronger end-to-end validation and documentation

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

- [x] 1. Complete `Domain + Application + Infrastructure` contracts
- [x] 2. Implement Windows audio capture end to end first
- [x] 3. Build the audio processing pipeline and save-to-file verification
- [x] 4. Keep `Vosk` as STT and integrate translation providers
- [x] 5. Render the real overlay subtitle flow
  - Overlay now uses real local Vosk transcription and live translation providers, not only mock data
- [ ] 6. Implement the macOS `ScreenCaptureKit + P/Invoke` bridge
- [ ] 7. Refactor session folder/domain model and persistence semantics
  - Replace `TreePath` with flat folder groups and a protected default `Global` folder
- [ ] 8. Migrate existing session data and settings to flat folders
  - Preserve old captures while moving ownership away from path strings
- [ ] 9. Refactor capture flow to choose existing folders only
  - Use `Global` fallback and remember last selected folder identity
- [ ] 10. Build folder CRUD in the sessions page
  - Create, rename, and delete same-level folders without exposing paths
- [ ] 11. Build capture record move/delete management inside folders
  - Support single and bulk operations with picker-based UX
- [ ] 12. Redesign the sessions page around folder browser + record list
  - Make folder grouping the primary organizational model in the UI
- [ ] 13. Replace storage path text inputs with OS folder pickers
  - Keep settings storage browsing simple and path-free for end users where applicable
- [ ] 14. Remove capture logs from the end-user experience
  - Simplify capture UX after the flat-folder refactor is stable
- [ ] 15. Finish packaging and documentation

## 16. Translate Service MVP

This section tracks the new self-hosted translation microservice for the existing Vosk-based realtime subtitle pipeline. Scope here is translation only: no ASR, no Vosk changes, no audio capture, and no microphone work.

### Goals

- [ ] Build a local/self-hosted translation API based on `FastAPI + MarianTokenizer + CTranslate2`
  - Keep inference on `ctranslate2.Translator`
  - Use `transformers` only for tokenizer loading and decode/encode flow
  - Optimize for low latency partial/final subtitle translation from the current Vosk pipeline

### Priority 1. Service Skeleton

- [ ] Create `translate-service/` project structure
  - Add `app/`, `scripts/`, `models/`, `docker/`, `requirements.txt`, `README.md`, and `.env.example`
  - Keep the service isolated from the current desktop app so the Vosk pipeline can call it over HTTP

- [ ] Implement configuration loading in `app/config.py`
  - Read all required runtime settings from `.env`
  - Expose defaults for CPU-first local deployment

- [ ] Implement request/response schemas in `app/schemas.py`
  - Cover `/health`, `/models`, `/translate`, `/translate/batch`, and `/translate/realtime`
  - Validate input sizes and language fields conservatively

### Priority 2. Core Translation Runtime

- [ ] Implement `MarianCT2Translator` in `app/translator/marian_ct2.py`
  - Load `ctranslate2.Translator` from converted model directory
  - Load `MarianTokenizer` from the same directory
  - Implement single and batch translation without using `MarianMTModel`
  - Normalize input text before tokenization and decode with `skip_special_tokens=True`

- [ ] Implement `TranslationModelManager` in `app/translator/model_manager.py`
  - Resolve pair names like `en-vi` and `vi-en`
  - Lazy load translators on first request
  - Cache translators per pair and avoid reloading per request
  - Fail with clear HTTP 400 when a pair has not been converted yet

### Priority 3. Realtime Optimization For Vosk

- [ ] Implement text normalization helpers in `app/utils/text.py`
  - Normalize whitespace and strip control characters
  - Truncate oversized requests
  - Detect weak partial boundaries
  - Detect near-duplicate partials using minimum delta heuristics

- [ ] Implement `RealtimeSessionCache`
  - Track per-session last source text and last translated text
  - Skip redundant partial translations
  - Respect `MIN_REALTIME_CHARS` and TTL cleanup
  - Always translate non-empty final text

- [ ] Implement `/translate/realtime` behavior in `app/main.py`
  - Return `should_display=false` when partial text is too short or too similar
  - Deduplicate repeated requests in the same session
  - Clear or refresh cached state on final updates

### Priority 4. API Layer And Observability

- [ ] Implement FastAPI app in `app/main.py`
  - Initialize config, model manager, and realtime cache globally
  - Expose `/health`, `/models`, `/translate`, `/translate/batch`, `/translate/realtime`
  - Measure latency with `time.perf_counter()`
  - Add clear `HTTPException` responses and basic request latency logging

- [ ] Implement logger utilities in `app/utils/logger.py`
  - Centralize log formatting and log level setup
  - Keep logging minimal but useful for latency and model-loading diagnostics

### Priority 5. Model Conversion And Validation Scripts

- [ ] Implement `scripts/convert_marian_to_ct2.py`
  - Convert Hugging Face Marian model to CTranslate2 via `ct2-transformers-converter`
  - Save tokenizer artifacts into the converted output directory
  - Validate output directory and exit with clear errors on failure

- [ ] Implement `scripts/test_translate.py`
  - Call `/translate` and print translation result and latency

- [ ] Implement `scripts/benchmark.py`
  - Send repeated translation requests against the local API
  - Report average latency, `p50`, `p95`, `p99`, and requests/second

### Priority 6. Packaging And Docs

- [ ] Add `docker/Dockerfile`
  - Use `python:3.11-slim`
  - Install dependencies and run `uvicorn app.main:app`

- [ ] Add `docker/docker-compose.yml`
  - Mount `models/` for converted Marian CT2 models
  - Load environment variables from `.env`

- [ ] Add `requirements.txt`
  - Include only required translation-service dependencies
  - Exclude ASR/audio-related packages

- [ ] Write `README.md` in Vietnamese
  - Explain service goal, architecture, install flow, model conversion, API usage, Docker usage, benchmark, and troubleshooting
  - Include Python example for calling `/translate/realtime` from the existing Vosk system

### Suggested Execution Order For This Service

- [ ] 1. Create the isolated `translate-service/` skeleton and config/schema foundation
- [ ] 2. Implement Marian + CTranslate2 runtime and model manager
- [ ] 3. Expose `/health`, `/models`, `/translate`, and `/translate/batch`
- [ ] 4. Add realtime partial/final optimization and session cache
- [ ] 5. Add conversion, test, and benchmark scripts
- [ ] 6. Add Docker packaging and Vietnamese README
