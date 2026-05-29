## Translate Service Provider Plan

### Purpose

This folder contains the implementation plan for integrating the local `translate` as a first-class realtime translation provider for Sublingual.

The goal is not only to add another HTTP provider. The goal is to add a provider that preserves realtime subtitle semantics:

- partial input should be translated early enough to feel live
- final input should remain authoritative and stable
- the app should avoid waiting for a full sentence before showing useful translated text
- the system should not depend on streaming responses from third-party providers that do not support them

### Why This Provider Needs A Separate Plan

The existing providers in Sublingual are request/response translators:

- `GoogleTranslateFreeApi`
- `LibreTranslate`

They can translate text, but they do not provide provider-specific realtime semantics for partial and final subtitle updates.

The local `translate` is different:

- it runs locally
- it can maintain short-lived realtime session state
- it can apply partial/final heuristics close to the translation engine
- it can be tuned specifically for Vosk-driven subtitle updates

Because of that, this provider should not be treated as just another generic `POST /translate` integration.

### Design Principles

1. Semantics before transport.
   The priority is to preserve `draft` versus `stable/final` meaning. WebSocket or streaming should only be introduced if they serve that goal.

2. Local-first assumptions.
   This provider runs on the same machine or local network. Low transport overhead matters less than avoiding unnecessary translations and protecting the UI pipeline.

3. Partial translation must be useful, not noisy.
   Partial translation should appear quickly, but it must avoid flicker, repeated near-identical translations, and out-of-order overwrites.

4. Final translation remains authoritative.
   Final segments should always be translated and should replace any older draft meaning for the same segment.

5. Do not require token streaming from the model.
   The current translation runtime does not expose incremental token output. The plan should improve perceived realtime behavior without depending on token-by-token streaming.

### Recommended Direction

The recommended architecture is:

- local provider specialized for `translate`
- draft partials use a realtime-aware API contract
- stable/final segments use a final translation path with stronger delivery guarantees
- the app passes session and sequencing metadata so stale responses can be dropped safely

### Document Map

- `API-UPDATE-PLAN.md`: changes required in `translate`
- `APP-INTEGRATION-PLAN.md`: changes required in Sublingual app
- `TODO.md`: phased implementation checklist

### Non-Goals For The First Iteration

- generic provider abstraction redesign for every future provider
- token-by-token translation streaming from the model runtime
- replacing all existing providers
- remote multi-tenant translation service concerns

### Success Criteria

The local provider is successful when:

1. partial transcript updates can trigger near-realtime translation without blocking the audio pipeline
2. final transcript updates always produce stable translations
3. stale draft responses never overwrite newer draft or final state
4. the overlay feels more live than the current full-sentence wait behavior
5. the app still supports generic providers as fallback for non-local translation
