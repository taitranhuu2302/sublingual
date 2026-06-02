# Speaker Diarization with Vosk — Design Spec

**Date**: 2025-06-02
**Status**: Approved

## Overview

Add automatic speaker diarization to Sublingual using `vosk-model-spk-0.4`. Transcribe who said what by clustering speaker embeddings (x-vectors) extracted from audio segments corresponding to each ASR final result.

Speaker diarization is **best-effort**: if the SPK model is unavailable or fails, ASR and translation continue unaffected.

## Architecture

```
Audio (48kHz stereo)
    ↓
Audio Capture (downmix + resample)
    ↓
PCM16 (16kHz mono) ──→ Vosk ASR Recognizer ──→ Partial/Final Text ──→ IPC ──→ Renderer
    │
    └──→ Audio Ring Buffer (~5s, PCM16)
              ↑
              │ extract segment by timestamp
              │
         (when ASR produces final result)
              │
              ↓
         Vosk SPK Recognizer ──→ x-vector embedding
              │
              ↓
         Speaker Cluster ──→ speakerLabel ("Speaker 1", "Speaker 2"...)
              │
              ↓
         Merge with final text ──→ IPC: transcript + speakerId ──→ Renderer + Overlay
```

## Components

### New Files (3)

| File | Purpose |
|---|---|
| `src/main/asr/vosk-spk-bindings.ts` | `koffi` FFI bindings to `libvosk.dylib` SPK functions: `vosk_spk_model_new`, `vosk_spk_recognizer_new`, `vosk_spk_recognizer_accept_waveform`, `vosk_spk_model_free`, `vosk_spk_recognizer_free`, `vosk_recognizer_result` |
| `src/main/asr/speaker-process.ts` | SPK model lifecycle: `loadSpkModel(modelPath)`, `extractEmbedding(pcmBuffer)`, `freeSpkModel()`. Returns `Float64Array | null` embedding. |
| `src/main/asr/speaker-cluster.ts` | `assignSpeaker(embedding, clusters)` — cosine similarity comparison against existing cluster centroids. Threshold > 0.7 → same speaker, else → new speaker. Returns `{ speakerId, label, color }`. `updateCentroid(cluster, embedding)` — moving average. `evictOldest(clusters, maxSpeakers)` — replace longest-unseen speaker. |

### Modified Files — Main Process (5)

| File | Change |
|---|---|
| `src/main/audio/audio-capture.ts` | Add `RingBuffer` class: fixed-size PCM16 circular buffer storing ~5 seconds of audio. Write to buffer simultaneously with feeding ASR. Expose `getSegment(startMs, endMs)` method. |
| `src/main/ipc/asr-handlers.ts` | On final ASR result: compute audio segment timestamps, extract from ring buffer, call `extractEmbedding()`, call `assignSpeaker()`, merge `speakerId` + `speakerLabel` + `speakerColor` into transcript line before IPC to renderer. |
| `src/main/models/model-source-catalog.ts` | Add `vosk-model-spk-0.4` entry with download URL from alphacephei.com. |
| `src/main/settings/settings-store.ts` | Add `speechToText.speakerModel` (string, path) and `speechToText.maxSpeakers` (number, default 4, range 2-8). |
| `src/types/electron-api.d.ts` | Add `speakerId?: string`, `speakerLabel?: string`, `speakerColor?: string` to `TranscriptLine` type. |

### Modified Files — Renderer (3)

| File | Change |
|---|---|
| `src/components/settings/SpeechSettings.tsx` | Add `maxSpeakers` select dropdown (2-8) with setting description. |
| `src/pages/HomePage.tsx` | Show speaker label + color chip in transcript preview. |
| `src/overlay/OverlayApp.tsx` | Show speaker label + color chip before each transcript line. |

## Data Model

### TranscriptLine (extended)

```typescript
interface TranscriptLine {
  id: string
  text: string
  translatedText?: string
  timestamp: number
  isFinal: boolean
  speakerId?: string      // "spk_1"
  speakerLabel?: string   // "Speaker 1"
  speakerColor?: string   // "#FF6B6B"
}
```

### SpeakerIdentity (memory only)

```typescript
interface SpeakerIdentity {
  id: string           // "spk_1"
  label: string        // "Speaker 1"
  color: string        // "#FF6B6B"
  centroid: Float64Array
  lastSeenAt: number   // timestamp
}
```

### Speaker Color Palette (hardcoded)

```
["#FF6B6B", "#4ECDC4", "#FFE66D", "#6C5CE7", "#A8E6CF", "#FF8B94", "#B8B8D1", "#FFB347"]
```

If more than 8 speakers are detected, colors cycle.

## Settings

```typescript
speechToText: {
  // ...existing
  speakerModel?: string    // path to vosk-model-spk-0.4
  maxSpeakers?: number     // default 4, range 2-8
}
```

## Error Handling

| Scenario | Behavior |
|---|---|
| SPK model not found / not downloaded | Speaker diarization silently disabled, ASR operates normally (no `speakerId`) |
| SPK model load fails (corrupt, wrong arch) | Log error, disable speaker feature, emit warning to renderer |
| Embedding extraction fails (buffer too short, noise) | Return `null`, line gets no `speakerId` |
| Cosine similarity below threshold (ambiguous) | Create new speaker to avoid misattribution |
| `libvosk.dylib` lacks SPK symbols | Catch at bind time, log, disable feature |
| Max speakers exceeded | Evict the speaker with oldest `lastSeenAt` |

**Core principle**: SPK failure must never affect ASR or translation.

## Manual Test Checklist

| # | Scenario | Expected Result |
|---|---|---|
| 1 | Capture audio with 2 alternating speakers | Transcript lines have different `speakerId`, different colors |
| 2 | Capture audio with 1 speaker only | All lines share the same `speakerId` |
| 3 | No SPK model downloaded | ASR runs normally, no `speakerId` in transcript |
| 4 | Corrupt or invalid SPK model | Error logged; ASR runs normally; warning in UI |
| 5 | View session in SessionsPage | Speaker label + color visible in transcript viewer |
| 6 | Overlay during capture | Speaker label + color displayed before each line |

## Model Download

`vosk-model-spk-0.4` is added to `model-source-catalog.ts` alongside existing ASR models. Users download it through the existing `ModelDownloadDialog` UI. Model is approximately 50MB.

## Vosk SPK C API Reference

The libvosk shared library exports these speaker-related functions:

```
vosk_spk_model_new(path)           → VoskSpkModel*
vosk_spk_recognizer_new(model, sample_rate) → VoskSpkRecognizer*
vosk_spk_recognizer_accept_waveform(rec, data, length) → bool
vosk_spk_recognizer_result(rec)    → char* (JSON with embedding)
vosk_spk_recognizer_free(rec)      → void
vosk_spk_model_free(model)        → void
```

The `result` JSON contains a `spk` array of float64 values (the x-vector embedding, typically 256 dimensions).
