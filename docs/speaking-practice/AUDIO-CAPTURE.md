# Speaking Practice - Audio Capture

This document describes the currently implemented microphone flow for speaking practice.

## 1. Capture Backends

- Windows: `WasapiMicrophoneCaptureService`
- macOS: `CoreAudioMicrophoneCaptureService`

Both are used through `IMicrophoneTranscriptionService` in speaking practice.

## 2. Normalization and STT

`MicrophoneTranscriptionService` pipeline:

1. capture raw mic chunks,
2. normalize via `AudioFormatNormalizer` to `16kHz mono PCM16`,
3. send to transcription service,
4. emit final transcript segments through `FinalTranscriptReady`.

## 3. Manual Speaking Flow (Current UX)

Speaking practice room detail uses explicit manual control:

1. user presses `Start Speaking`,
2. transcripts are accumulated while recording,
3. user presses `Stop`,
4. aggregated text is sent to AI.

This flow is intentionally explicit and does not auto-send per silence pause.

## 4. Reliability Behavior

- microphone start/stop failures are surfaced to status text,
- speaking actions are guarded by `IsRoomActionBusy` to avoid double-submit race,
- recording preview is shown before final submit.

## 5. Known Limitation

`SetMuted` behavior during TTS still needs additional validation for edge cases around stale partials and transition timing.
