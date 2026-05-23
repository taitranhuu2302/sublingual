## TODO

### Phase 1: API Contract

- [ ] extend `RealtimeTranslateRequest` with `segment_id`, `sequence_id`, `kind`, and optional `force`
- [ ] extend `RealtimeTranslateResponse` with `model`, `segment_id`, `sequence_id`, `kind`, `was_skipped`, `skip_reason`, `normalized_text`, and `cache_hit`
- [ ] add `POST /translate/realtime/reset`
- [ ] update `openapi.json` and README examples
- [ ] add tests for draft skip reasons and final translation behavior

### Phase 2: App Settings And Provider Registration

- [ ] add `TranslateServiceLocal` provider constant
- [ ] add settings model for local provider base URL and behavior flags
- [ ] add settings UI fields for the local provider
- [ ] register the provider in DI and design-time construction paths

### Phase 3: Realtime Context In App

- [ ] define app-side realtime translation context
- [ ] carry `session_id`, `segment_id`, `sequence_id`, `target`, and `is_final` through scheduler and execution layers
- [ ] ensure generic providers can ignore realtime-only fields safely

### Phase 4: Local Provider Implementation

- [ ] implement local provider HTTP client
- [ ] map draft requests to `/translate/realtime`
- [ ] map final requests to chosen final path
- [ ] parse and surface skip reasons and latency metadata
- [ ] reset translation session on capture lifecycle boundaries

### Phase 5: Verification

- [ ] validate near-realtime partial translation behavior
- [ ] validate final segment authority over draft state
- [ ] validate out-of-order protection with stale sequence ids
- [ ] validate provider-down fallback behavior
- [ ] validate no audio pipeline blocking under slow translation responses
