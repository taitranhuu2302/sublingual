## API Update Plan

### Objective

Update `translate-service` so it supports Sublingual's realtime subtitle workflow more directly.

The service should help the app translate partial transcript updates with low perceived latency while keeping final segment meaning correct and stable.

### Current Constraints

Current `translate-service` behavior:

- `POST /translate` translates a single text
- `POST /translate/batch` translates multiple texts
- `POST /translate/realtime` applies skip heuristics for Vosk partial/final text
- the runtime still performs full translation before sending a response

Current limitations for the app:

- the realtime response does not return enough metadata for robust client-side reconciliation
- there is no explicit sequence contract between app and service
- session lifecycle is implicit and only expires by TTL
- response semantics are still too close to a generic translator and not rich enough for a realtime subtitle client

### API Direction

Keep REST for the first implementation.

Do not make WebSocket or streaming response a prerequisite for the provider. The first goal is better subtitle semantics, not transport novelty.

### Proposed Contract Changes

#### 1. Extend `POST /translate/realtime`

Keep the current endpoint name, but strengthen its request and response contract.

Request additions:

- `sequence_id`: monotonically increasing client-side sequence number
- `segment_id`: stable identifier for the current subtitle unit
- `kind`: `draft` or `stable`
- `force`: optional override to bypass conservative skipping for selected cases

Recommended request shape:

```json
{
  "text": "hello everyone welcome to",
  "source_lang": "en",
  "target_lang": "vi",
  "session_id": "capture-123",
  "segment_id": "seg-42",
  "sequence_id": 108,
  "kind": "draft",
  "is_final": false,
  "force": false
}
```

Response additions:

- `model`
- `sequence_id`
- `segment_id`
- `kind`
- `was_skipped`
- `skip_reason`
- `normalized_text`
- `cache_hit`

Recommended response shape:

```json
{
  "translated_text": "xin chao moi nguoi chao mung den",
  "should_display": true,
  "is_final": false,
  "latency_ms": 18.1,
  "model": "en-vi",
  "sequence_id": 108,
  "segment_id": "seg-42",
  "kind": "draft",
  "was_skipped": false,
  "skip_reason": null,
  "normalized_text": "hello everyone welcome to",
  "cache_hit": false
}
```

If the request is skipped:

```json
{
  "translated_text": "",
  "should_display": false,
  "is_final": false,
  "latency_ms": 0,
  "model": "en-vi",
  "sequence_id": 108,
  "segment_id": "seg-42",
  "kind": "draft",
  "was_skipped": true,
  "skip_reason": "too_similar",
  "normalized_text": "hello everyone welcome to",
  "cache_hit": false
}
```

#### 2. Add Explicit Session Reset

Add a lightweight reset endpoint so the app can end a capture session cleanly instead of waiting for TTL cleanup.

Recommended endpoint:

- `POST /translate/realtime/reset`

Recommended request shape:

```json
{
  "session_id": "capture-123"
}
```

Reason:

- removes stale session memory when capture stops or restarts
- prevents previous partial state from affecting a new capture session
- keeps service behavior deterministic during testing

#### 3. Keep `POST /translate` For Stable Fallback

Keep `/translate` for these cases:

- final segment translation when the app wants a simple authoritative translation call
- translation testing in settings UI
- non-realtime workflows

This keeps the generic path intact while the realtime contract evolves separately.

#### 4. Optional Session Diagnostics Endpoint

Optional but useful during tuning:

- `GET /translate/realtime/sessions/{session_id}`

Potential fields:

- last normalized text
- last translated text
- updated time
- skip counters by reason

This endpoint should be treated as a debug tool, not part of the main user-facing contract.

### Skip Policy Recommendations

The service should continue to support conservative draft filtering, but skip decisions should become explicit.

Recommended skip reasons:

- `too_short`
- `weak_boundary`
- `too_similar`
- `duplicate_translation`
- `empty_normalized_text`

This makes tuning much easier on the app side.

### Final Semantics Rules

The service should preserve these rules:

1. `is_final=true` should always translate when normalized text is not empty.
2. `kind=stable` should be treated as authoritative.
3. draft requests may be skipped conservatively.
4. final requests should update session state so later draft dedupe stays coherent.

### WebSocket Evaluation

WebSocket is optional for a later phase.

Only add it if one of these becomes true:

- HTTP request overhead is measurable in local profiling
- session state management becomes awkward with REST
- the app needs server-push diagnostics or richer session lifecycle control

If WebSocket is added later, it should mirror the same semantic fields:

- `session_id`
- `segment_id`
- `sequence_id`
- `kind`
- `is_final`

That keeps REST and WebSocket behavior aligned.

### Streaming Response Evaluation

Streaming response should not be part of the first API update.

Reason:

- the current translation runtime does not expose incremental token output
- the response only becomes meaningful after inference completes
- streaming a single final payload would add complexity without meaningful latency benefit

### Implementation Steps

1. Extend realtime request and response schemas.
2. Add sequence and segment metadata pass-through.
3. Add explicit skip reason reporting.
4. Add session reset endpoint.
5. Update OpenAPI examples and README.
6. Add focused tests for draft skip behavior and final delivery.

### Verification

Verify with these cases:

1. same partial text repeated quickly -> skipped with `too_similar`
2. weak partial boundary -> skipped with `weak_boundary`
3. final text for same segment -> always translated
4. reset called, then same partial text sent again -> treated as a fresh session
5. stale `sequence_id` observed by client -> client can safely drop result
