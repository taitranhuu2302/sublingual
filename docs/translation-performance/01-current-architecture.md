# Current Architecture

## Pipeline Flow

```
Audio Source (System/Mic loopback)
  │
  ▼
[IAudioCaptureService]
  │  Fires AudioChunkCaptured(raw bytes)
  ▼
[AudioCaptureDebugSession.OnAudioChunkCaptured]
  │  Fire-and-forget → ProcessChunkAsync
  │  ⏳ _pipelineGate (SemaphoreSlim 1,1)
  ▼
[FixedWindowAudioChunkProcessor]
  │  Accumulates raw audio, emits fixed-size windows
  │  Default: 350ms (Fast=275ms, Accurate=500ms)
  ▼
[AudioFormatNormalizer]
  │  → 16kHz mono PCM16
  ▼
[VoskTranscriptionService.TranscribeAsync]
  │  VoskRecognizer.AcceptWaveform(chunk.Data)
  │  → PartialResult() or Result()
  ▼
[PublishTranscriptEvents]
  │  Partial → DraftTranscriptChanged event
  │  Final  → StableTranscriptCommitted event
  ▼
[ScheduleTranslation]
  │  (Now OUTSIDE _pipelineGate via RealtimeTranslationScheduler)
  ▼
[RealtimeTranslationScheduler]
  │
  ├── Draft: debounce 300ms, latest-only, cancellable
  │   → ConfigurableTranslationService
  │     → TranslateServiceLocalTranslationProvider
  │       → POST /translate/realtime (Python)
  │         → RealtimeSessionCache.should_translate (skip logic)
  │         → MarianCT2Translator.translate
  │           → CTranslate2 inference
  │
  └── Stable: sequential ConcurrentQueue, single worker
      → ConfigurableTranslationService
        → TranslateServiceLocalTranslationProvider
          → POST /translate or /translate/realtime
            → MarianCT2Translator.translate
              → CTranslate2 inference
  │
  ▼
[OnTranslationCompleted]
  │  UI thread via dispatcher
  ▼
[OverlayWindowViewModel]
  │  PartialTranslatedText (draft)
  │  TranscriptLines (stable)
```

## Component Map

### C# (.NET 10, Avalonia)

| Component | File | Vai trò |
|---|---|---|
| `AudioCaptureDebugSession` | `src/Sublingual.App/Services/AudioCaptureDebugSession.cs` | Orchestrator: capture → STT → translation |
| `RealtimeTranslationScheduler` | `src/Sublingual.App/Services/RealtimeTranslationScheduler.cs` | Draft debounce + stable queue |
| `ConfigurableTranslationService` | `src/Sublingual.App/Services/Translation/ConfigurableTranslationService.cs` | Cache + provider fallback |
| `TranslateServiceLocalTranslationProvider` | `src/Sublingual.App/Services/Translation/TranslateServiceLocalTranslationProvider.cs` | HTTP client gọi Python service |
| `VoskTranscriptionService` | `src/Sublingual.Infrastructure/SpeechRecognition/VoskTranscriptionService.cs` | STT via Vosk |
| `FixedWindowAudioChunkProcessor` | `src/Sublingual.Infrastructure/Audio/Processing/FixedWindowAudioChunkProcessor.cs` | Chunking audio |

### Python (FastAPI + CTranslate2)

| Component | File | Vai trò |
|---|---|---|
| FastAPI app | `translate-service/app/main.py` | HTTP endpoints |
| Settings | `translate-service/app/config.py` | Config từ .env |
| Schemas | `translate-service/app/schemas.py` | Request/Response models |
| `MarianCT2Translator` | `translate-service/app/translator/marian_ct2.py` | Tokenize + CTranslate2 inference |
| `TranslationModelManager` | `translate-service/app/translator/model_manager.py` | Lazy-load + cache model per pair |
| Text utils | `translate-service/app/utils/text.py` | Normalize, boundary detection, similarity |
| `RealtimeSessionCache` | `translate-service/app/main.py` (inline class) | Skip logic cho partial text |

## Data Flow Details

### Chunking
- `FixedWindowAudioChunkProcessor` accumulate bytes → emit chunk khi đủ duration
- 3 presets: Fast (275ms), Balanced (350ms default), Accurate (500ms)
- **Không có VAD** → silence vẫn được gửi vào Vosk

### Vosk STT
- Model loaded lazy + cached
- `AcceptWaveform()` incremental
- Khi return `true` → `Result()` (final); nếu không → `PartialResult()`
- Partial final text được emit mỗi chunk window

### Translation Scheduling (`RealtimeTranslationScheduler.cs`)
- **Draft**: `_latestDraft` (1 slot), debounce 300ms, cancel in-flight khi có draft mới
- **Stable**: `ConcurrentQueue` + single worker `ProcessStableQueueAsync()`
- **Không có capacity limit** cho stable queue

### Python Skip Logic (`RealtimeSessionCache.should_translate`)
- `is_final=True` → luôn translate nếu text không empty
- Partial: skip nếu:
  - Text < `MIN_REALTIME_CHARS` (8)
  - Weak boundary (không kết thúc bằng space/punctuation và < 24 chars)
  - Too similar (delta < `MIN_REALTIME_CHARS` chars so với previous)

### CTranslate2 Inference (`marian_ct2.py`)
- `MarianTokenizer.from_pretrained()` load tokenizer từ model dir
- `ctranslate2.Translator.translate_batch(tokens, beam_size=1)`
- `device="cpu"`, `compute_type="int8"`, `inter_threads=1`, `intra_threads=4`
- Sync (blocking) — không async
