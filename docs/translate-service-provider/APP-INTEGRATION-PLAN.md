## App Integration Plan

### Objective

Integrate the local `translate` into Sublingual as a dedicated provider for local realtime translation.

The integration should make partial translation feel near-realtime without forcing the app to wait for a full sentence.

### Key Requirement

The provider must preserve subtitle meaning correctly:

- draft partials are provisional
- stable/final segments are authoritative
- stale draft results must never overwrite newer draft or final state

### Current App Constraints

Current provider abstraction in the app only carries:

- `SourceText`
- `SourceLanguage`
- `TargetLanguage`

This is not enough for realtime subtitle semantics because the local provider also needs:

- `session_id`
- `segment_id`
- `sequence_id`
- `kind` or equivalent draft/stable meaning
- `is_final`

### Integration Strategy

Treat the local provider as a specialized realtime provider, not just another generic translator.

The app should support two paths:

1. generic provider path
   - works for Google and LibreTranslate
   - remains simple request/response translation

2. local realtime provider path
   - uses richer request semantics
   - participates in draft/final lifecycle directly

### Proposed App Changes

#### 1. Add A New Provider Name And Settings

Add a dedicated provider entry for the local service.

Recommended provider name:

- `TranslateServiceLocal`

Recommended settings:

- `Enabled`
- `BaseUrl`
- `UseRealtimeEndpointForDrafts`
- `UseRealtimeEndpointForFinals`
- `ResetSessionOnCaptureStart`
- `ResetSessionOnCaptureStop`

Default assumptions:

- service runs local
- `BaseUrl = http://0.0.0.0:3333`
- drafts use realtime endpoint
- finals can use realtime endpoint or plain translate, depending on API decision

#### 2. Extend Translation Request Semantics Inside The App

The app needs a richer translation request model for realtime work.

Recommended internal fields:

- `SessionGeneration`
- `SessionId`
- `SegmentId`
- `SequenceId`
- `Target`
- `IsFinal`

Where:

- `Target` maps to current concepts such as `Draft` and `StableSegment`
- `IsFinal` reflects Vosk finality or committed segment status

This does not mean every provider must use all fields. Generic providers can ignore them.

#### 3. Keep Generic Providers Backward Compatible

Generic HTTP providers should continue to operate with the minimal text-based contract.

The local provider should consume the richer context only when available.

This avoids rewriting Google and LibreTranslate just to support the local provider.

#### 4. Use Draft And Stable Routing Rules

Recommended request routing:

- `Draft` partial updates -> local provider calls `POST /translate/realtime`
- `StableSegment` committed updates -> local provider calls `POST /translate/realtime` with `is_final=true` or `POST /translate`

Preferred first version:

- drafts -> `/translate/realtime`
- finals -> `/translate/realtime` with `is_final=true`

Reason:

- one semantic endpoint handles the session lifecycle consistently
- final responses still update session state coherently

If that becomes awkward, fallback to:

- drafts -> `/translate/realtime`
- finals -> `/translate`

#### 5. Add Local Session Lifecycle Management

The app should treat translation session lifecycle as part of capture lifecycle.

On capture start:

- create a fresh translation `session_id`
- optionally reset service session state

On capture stop:

- stop enqueueing draft work
- optionally call reset endpoint

On capture restart:

- never reuse the old translation session id

This prevents stale draft memory from leaking across sessions.

#### 6. Preserve Out-Of-Order Protection In The App

Even if the service returns `sequence_id`, the app must still enforce ordering locally.

Rules:

1. draft translation result applies only if it matches the latest draft `sequence_id`
2. stable translation result applies only if it matches the stable `segment_id`
3. final translation should override any older draft for the same segment

The client should never trust arrival order.

#### 7. Surface Provider Diagnostics In The UI

At least during development, expose lightweight diagnostics:

- provider name used
- latency
- skip reason for draft requests
- cache hit if present

This is important because partial translation tuning is difficult without visible runtime feedback.

### Suggested Internal Abstraction

The minimal change path is:

1. keep `ITranslationExecutionService` as the public app-facing translation entry point
2. extend the internal request path used by `RealtimeTranslationScheduler`
3. allow providers to receive optional realtime context

One practical approach:

- keep existing `TranslationRequest`
- add a separate optional realtime context object
- pass it only from the realtime scheduler path

This keeps non-realtime and settings test flows simple.

### Proposed Integration Phases

#### Phase 1: Settings And Provider Registration

- add provider constant
- add app settings model
- add settings UI fields for local service
- register provider in bootstrapper and design-time construction paths

#### Phase 2: Realtime Context Plumbing

- extend scheduler requests with `session_id`, `segment_id`, `sequence_id`, `is_final`
- pass realtime context into translation execution
- keep generic providers ignoring the new context

#### Phase 3: Local Provider Implementation

- implement provider request mapping to service API
- map draft calls to realtime endpoint
- map final calls to chosen final path
- parse skip reasons and diagnostics

#### Phase 4: Overlay And Diagnostics

- ensure draft translations update only the draft line
- ensure final translations update only the stable line
- expose runtime diagnostics during testing

#### Phase 5: Hardening

- capture restart/reset behavior
- failure fallback strategy
- service unavailable behavior
- queue pressure validation

### Fallback Strategy

Recommended fallback order when the local provider is enabled:

1. `TranslateServiceLocal`
2. `GoogleTranslateFreeApi`
3. `LibreTranslate`

But fallback must be semantics-aware:

- if the local realtime provider is unavailable for draft translation, the app may choose to skip draft translation entirely instead of sending every partial to a generic provider
- final translations may still fall back to generic providers if needed

This preserves UX quality and avoids flooding providers that are not suited to partial updates.

### Verification

Verify end-to-end behavior with these checks:

1. partials appear translated before a full sentence completes
2. repeated similar partials do not cause visible translation churn
3. final segment locks in a stable translation
4. stopping and restarting capture does not reuse old partial translation state
5. when local provider is down, final fallback still works and draft behavior remains controlled
