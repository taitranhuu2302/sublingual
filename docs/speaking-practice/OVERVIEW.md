# AI Speaking Practice - Architecture Overview

This document describes the currently implemented architecture of the speaking-practice feature.

## 1. Product Flow

The `Practice` tab now uses a room-based flow:

1. Open `Practice` tab -> room list page.
2. Create room with optional title + instructions.
3. Auto-navigate to room detail after creation.
4. In room detail, user can:
   - type chat text, or
   - use manual speaking flow: `Start Speaking -> Stop -> send to AI`.
5. Room messages and room metadata are persisted locally.

Room list supports:

- search by title/instructions,
- duplicate room,
- delete one room,
- multi-select delete with confirm dialog.

## 2. Runtime Components

- `PracticeSessionViewModel`
  - room list/detail state,
  - create/edit/delete/duplicate room flows,
  - manual microphone start/stop,
  - AI config validation guard,
  - message timeline + suggestions.

- `SpeakingSessionManager`
  - conversation history,
  - state machine (`Listening`, `Transcribing`, `AiThinking`, `AiSpeaking`),
  - AI call orchestration,
  - TTS playback lifecycle.

- `SpeakingPracticeRoomStore`
  - local JSON persistence (`speaking-practice-rooms.json`),
  - room metadata and message history,
  - room ordering by recent activity.

## 3. Data Flow

```text
Room Detail
  -> (Chat text OR manual mic transcript)
  -> SpeakingSessionManager
  -> IAiTutorService (Groq/Gemini)
  -> TutorResponse (reply + enhancement + suggestions)
  -> LocalSystemTtsService
  -> UI timeline + persisted room history
```

## 4. Audio Behavior

Current behavior is manual push-to-talk for speaking practice:

- user starts recording explicitly,
- user stops explicitly,
- transcript segments are aggregated,
- final aggregated text is sent to AI only after stop.

This flow intentionally does not rely on automatic VAD commit for room detail conversation turns.

## 5. AI Prompting Behavior

AI providers receive:

- room `instructions`,
- language level,
- recent room history.

Prompt logic uses two modes:

1. Follow room instructions as the main contract.
2. If instructions are broad/empty, use warm daily-conversation fallback behavior.

## 6. Current Gaps

- AI provider config errors are now blocked in speaking runtime with explicit status text, but no dedicated settings shortcut is shown yet.
- Speaking-practice docs are now aligned at high level; detailed sequence diagrams can be expanded later.
