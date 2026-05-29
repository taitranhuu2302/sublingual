# Realtime Translation Enhancement Plan (Vosk + Google Free / LibreTranslate)

## Context (current implementation)

- STT: `VoskTranscriptionService` feeds chunks into `VoskRecognizer.AcceptWaveform(...)` and returns either `PartialResult()` or `Result()`.
- Capture pipeline: `AudioCaptureDebugSession.ProcessChunkAsync()` holds `_pipelineGate` while it iterates processed chunks and calls `PublishTranscriptPreviewAsync(chunk)`.
- Translation: `PublishTranscriptPreviewAsync(...)` currently awaits `ITranslationExecutionService.TranslateWithDiagnosticsAsync(...)` inline.
- Overlay UI: `OverlayWindowViewModel` adds final lines to `TranscriptLines` and shows only final translation via `FinalTranslatedText`. Partial translation is produced in debug session but not rendered in overlay.

Key risk: awaiting translation inside `_pipelineGate` couples network latency to the audio pipeline. With partial translation enabled, this can create an HTTP flood + backlog + CPU/GC pressure, leading to UI jitter and even process exit.

## Goals

1. Do not block audio processing/capture on translation.
2. Partial translation should feel realtime without spamming requests.
3. Avoid content jitter: stable text should not be rewritten repeatedly.
4. Provide clear UX feedback when translation is in progress (e.g., `...`).
5. Prevent out-of-order translation responses from overwriting newer text.
6. Keep changes minimal and aligned with existing code style.

## Proposed UX Model: Stable + Draft

- **Stable**: segments that have been committed (Vosk final results or explicit commit rule). Stable segments never change.
- **Draft**: the current in-progress segment driven by Vosk partial results. Draft may change frequently.

Overlay rendering:

- Stable segments are appended to `TranscriptLines` as they are today.
- Draft is displayed separately (not part of the stable list), so only one line can “move”.
- Draft translation can show a placeholder `...` while a translation request is in flight.

This avoids the “whole overlay keeps changing” effect.

## Core Architecture Change: Decouple Translation from Capture Pipeline

### Current issue

`PublishTranscriptPreviewAsync(...)` does STT + translation and awaits translation within the capture pipeline semaphore. Any translate delay slows the pipeline.

### Target design

1. Capture/STT pipeline emits transcript events quickly (partial/final text only).
2. A dedicated translation worker performs translation asynchronously.
3. UI observes transcript updates and translation updates separately.

Minimal implementation approach:

- Introduce a small in-process translation scheduler/worker that accepts “draft updates” and “final segment commits”.
- Enforce backpressure and cancellation:
  - Draft translation: keep only the latest draft update (drop older ones).
  - Final translation: queue sequentially (can be bounded), but do not run concurrently.
  - Always cancel any in-flight draft translation when a newer draft arrives.

## Translation Scheduling Rules

### 1) Debounce draft translation

Only request draft translation when the draft is stable enough.

Recommended rule (tunable):

- Debounce: 250–400ms since last draft change.
- Additional guard: require length to increase by >= 8–12 chars (or >= 2–3 words) since last translated draft.
- Hard cap: translate draft at most 2–4 times per second.

### 2) Commit rule for stable segments

Stable segment is created when:

- Vosk returns a final result (`AcceptWaveform(...) == true` -> `Result()`), OR
- silence/pause heuristic triggers (if available from your capture pipeline), OR
- the draft grows beyond a configured size threshold and you decide to “cut”.

For stable segments:

- Translate immediately (queued), do not debounce.
- Optionally add a short “loading translation” placeholder in the stable line until translation arrives.

### 3) Out-of-order protection

For both draft and stable segments:

- Assign a monotonically increasing `sequenceId` (or `segmentId`).
- When a translation response arrives, apply it only if it matches the current draft id or the stable segment id.
- Drop stale responses silently.

## Backpressure Strategy (prevents freezes/crashes)

- Draft queue capacity: 1 (latest-only). New draft replaces pending draft.
- Stable queue capacity: bounded (e.g., 50–200). If exceeded, drop oldest or stop translating until backlog clears (choose one behavior).
- Translation concurrency: 1 worker (avoids out-of-order and reduces CPU/memory thrash).

## Caching

Current cache key: `sourceLang|targetLang|sourceText`.

Improvements (optional):

- Normalize whitespace before caching (trim + collapse multiple spaces) to increase hit rate for partials.
- Cache both:
  - stable segment translation
  - recently translated drafts (small LRU), since partial text often oscillates.

## Overlay UX Details

### Draft line

- Show draft original text always.
- If translation enabled:
  - while draft translation is pending: show `...` (or empty) in the translated line.
  - once available: show translated draft.

Avoid layout jumps:

- Keep a fixed height for the translated draft line (even when empty) so the overlay doesn’t resize.

### Stable lines

- Append a new stable line on final segment commit.
- If translation is slow:
  - either show original only and fill translation later,
  - or show a subtle placeholder (e.g., `...`) until translated text arrives.

## Implementation Steps (concrete)

1. **Refactor `AudioCaptureDebugSession`**
   - Stop awaiting translation inside `PublishTranscriptPreviewAsync`.
   - Emit a richer update model that includes:
     - draft text changes
     - stable segment commits
     - ids/timestamps

2. **Add a translation worker/scheduler**
   - Accepts draft updates and stable segment requests.
   - Implements debounce + cancellation for drafts.
   - Serializes stable translation requests.

3. **Update `OverlayWindowViewModel`**
   - Add observable properties for:
     - `PartialTranslatedText`
     - `IsDraftTranslating` (or simply derive from a placeholder state)
   - Render draft translation + loader.
   - Keep stable list behavior unchanged.

4. **Add diagnostic counters** (lightweight)
   - Draft translate requests attempted / canceled.
   - Stable translate queue depth.
   - Average translation latency.
   - Surface in `RuntimeLog` or status text.

5. **Verification**
   - Run the app with `TranslatePartials = true`.
   - Confirm:
     - CPU doesn’t spike abnormally.
     - No UI freeze while translation endpoint is slow.
     - Draft updates feel smooth.
     - Stable lines don’t get rewritten.
     - No out-of-order overwrites.

## Suggested Defaults (initial tuning)

- Draft debounce: 300ms
- Draft min delta: 10 chars
- Draft max rate: 3 requests/sec
- Stable queue cap: 100
- TranscriptLines cap: keep existing 80

## Notes on Vosk “streaming”

Vosk supports incremental recognition by feeding audio chunks continuously and reading `PartialResult()` until `AcceptWaveform` signals a final result and `Result()` returns a committed segment. This is sufficient for realtime captions.

## Notes on translate-service Deployment

### Worker configuration

`uvicorn --workers N` forks **independent processes**, not threads. Each worker has its own isolated copy of:

- `RealtimeSessionCache` — session deduplication state is not shared across workers. The same `session_id` will be treated as a fresh session on each worker, causing duplicate translations and broken "too similar" filtering.
- `model_manager.cache` — each worker loads its own in-memory copy of each model (wasted RAM).
- `/translate/realtime/reset` — resets only on the worker that receives the request.

### Current workaround (2025-05-29)

Set `--workers 1` everywhere:

| File | Change |
|------|--------|
| `translate-service/run.sh` | `UVICORN_WORKERS="${UVICORN_WORKERS:-1}"` |
| `translate-service/docker/Dockerfile` | unchanged (`ARG UVICORN_WORKERS=1`) |
| `translate-service/docker/docker-compose.yml` | `UVICORN_WORKERS=1` |

This sidesteps the shared state problem at the cost of not utilizing multiple CPU cores. For a single-user local desktop app this is perfectly fine — the translation service is I/O-bound (model inference, disk reads) rather than CPU-bound at the HTTP layer.

### Future improvement (shared session cache)

To scale with multiple workers without losing session affinity, the recommended path is:

**Option A — Redis-backed session cache** (production-ready):
1. Add `redis` to the service dependency.
2. Replace `RealtimeSessionCache` with a Redis hash keyed by `session_id`.
3. `TTL`, `cleanup_expired`, and `should_translate` all become Redis ops. Fall back to in-memory on Redis unavailable (dev mode).

**Option B — Single-process async** (simpler, lower RAM):
1. Drop `--workers` entirely.
2. Run uvicorn with `--loop uvloop` for better async performance.
3. Use `httpx.AsyncClient` with connection pooling from the .NET client if you later want async HTTP calls.

**Option C — Sticky sessions via reverse proxy**:
1. Keep `--workers N`.
2. Add nginx or Traefik in front with sticky sessions (`$cookie_rt_sid` or similar).
3. Trade-off: adds infrastructure complexity for container deployments.

**Option D — Redis for session + shared model loading**:
1. Option A + memory-map model files via a shared read-only filesystem, or
2. Use `torch.compile` with shared weights across forked processes (complex, not recommended).
