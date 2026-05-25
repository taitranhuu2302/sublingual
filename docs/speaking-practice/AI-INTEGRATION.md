# Speaking Practice - AI Integration

This document describes the current AI orchestration for room-based speaking practice.

## 1. Provider Selection

Provider is selected from settings:

- `Groq` (`GroqSpeakingTutorService`)
- `Gemini` (`GeminiSpeakingTutorService`)

Runtime uses the provider configured in `SpeakingPracticeSettings.AiProvider`.

## 2. Required Configuration

Before user can send/choose suggestion/start speaking in room detail, runtime validates provider config:

- Groq requires:
  - `GroqApiKey`
  - `GroqModel`
- Gemini requires:
  - `GeminiApiKey`
  - `GeminiModel`

If missing, actions are blocked and UI status explains what is missing.

## 3. Prompt Contract

`IAiTutorService.GetResponseAsync(...)` receives:

- `instructions` (from room),
- `languageLevel`,
- `history` (room conversation history).

Prompt behavior:

1. Treat room instructions as primary contract.
2. If instructions are broad/empty, apply warm daily-conversation fallback.
3. Return strict JSON:
   - `tutor_reply`
   - `english_enhancement`
   - `suggestions[3]`

## 4. Response Handling

`SpeakingSessionManager`:

- appends user message,
- calls provider,
- patches enhancement back into latest user turn,
- appends AI reply,
- publishes suggestions,
- triggers TTS for tutor reply.

## 5. Current State Machine

Room detail AI loop now uses these visible states:

- `Listening`
- `Transcribing`
- `AiThinking`
- `AiSpeaking`

UI binds these states to status text and busy overlays.
