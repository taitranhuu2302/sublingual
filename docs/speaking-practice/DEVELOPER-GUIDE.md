# Speaking Practice - Developer Guide

Goal of this document: help you modify Speaking Practice quickly (UI/flow/code) and iterate on the AI system prompts safely.

## 1. Where Things Live

UI (Avalonia + SukiUI):

- `src/Sublingual.App/Views/SpeakingPractice/PracticeSessionView.axaml`
- `src/Sublingual.App/Views/SpeakingPractice/PracticeSessionView.axaml.cs`

ViewModel (MVVM state + commands):

- `src/Sublingual.App/ViewModels/SpeakingPractice/PracticeSessionViewModel.cs`

Conversation orchestration (state machine + history + AI call + TTS):

- `src/Sublingual.Application/SpeakingPractice/SpeakingSessionManager.cs`

AI provider selection (reads Settings, routes to Groq/Gemini):

- `src/Sublingual.App/Services/SpeakingPracticeDynamicAiTutorService.cs`

AI provider implementations (prompt + HTTP + JSON parse):

- `src/Sublingual.Infrastructure/AI/Groq/GroqSpeakingTutorService.cs`
- `src/Sublingual.Infrastructure/AI/Gemini/GeminiSpeakingTutorService.cs`

Room persistence (local JSON):

- `src/Sublingual.App/Services/SpeakingPracticeRoomStore.cs`
- `src/Sublingual.App/Models/SpeakingPracticeRoomModels.cs`

Domain models:

- `src/Sublingual.Domain/SpeakingPractice/PracticeMessage.cs`
- `src/Sublingual.Domain/SpeakingPractice/TutorResponse.cs`
- `src/Sublingual.Domain/SpeakingPractice/IAiTutorService.cs`

## 2. Runtime Flow (Typed + Spoken)

Typed message:

1. UI `TextBox` binds `TypedMessage`.
2. Send button calls `PracticeSessionViewModel.SendTypedMessageCommand`.
3. `SubmitUserMessageAsync` calls `SpeakingSessionManager.HandleUserTranscriptAsync(text)`.

Spoken message (manual start/stop):

1. `StartSpeakingCommand` starts microphone transcription.
2. `FinalTranscriptReady` emits segments while recording.
3. `StopSpeakingCommand` stops mic, aggregates segments, then calls `SubmitUserMessageAsync(transcript, isSpoken: true)`.

Conversation loop (inside `SpeakingSessionManager.HandleUserTranscriptAsync`):

1. Add user message to `_history` and raise `MessageAdded`.
2. Transition state `AiThinking`.
3. Call `IAiTutorService.GetResponseAsync(instructions, level, history)`.
4. If `TutorReply` is empty, manager logs and returns without publishing an AI bubble.
5. Add AI message to `_history` and raise `MessageAdded`.
6. Raise `SuggestionsUpdated`.
7. Transition to `AiSpeaking` and play TTS.
8. Transition back to `Listening`.

## 3. UI Loading Behavior

Speaking Practice does NOT block the whole message list while thinking.

- `PracticeSessionViewModel.IsThinking` is derived from `SpeakingSessionState`.
- `PracticeSessionView.axaml` renders an inline "Tutor is thinking..." bubble when `IsThinking=true`.

If you want to change loading UX, start in:

- `PracticeSessionView.axaml` (inline bubble)
- `PracticeSessionViewModel.OnSessionStateChanged` (sets `IsThinking`)

## 4. Suggestions UX Contract

- Suggestions are attached to each AI message (`PracticeMessage.Suggestions`).
- The UI always shows a "Suggestions" button under AI messages.
- Suggestions list is hidden by default, toggled by `PracticeMessageViewModel.ShowSuggestions`.

Relevant code:

- UI template: `PracticeSessionView.axaml`
- Toggle command: `PracticeSessionViewModel.ToggleSuggestionsCommand`
- Data: `PracticeMessageViewModel.Suggestions`

There is NO suggestions fallback generation. If the model returns no suggestions, the panel can be empty.

## 5. AI JSON Contract (Important)

Both providers prompt for strict JSON with these keys:

```json
{
  "tutor_reply": "...",
  "suggestions": [
    { "label": "Direct Reply", "text": "..." },
    { "label": "Elaborate", "text": "..." },
    { "label": "Ask Back", "text": "..." }
  ]
}
```

Parsing notes:

- Groq parses `choices[0].message.content`.
- Gemini parses `candidates[0].content.parts[0].text`.
- Both DTOs use `[JsonPropertyName("tutor_reply")]` / `[JsonPropertyName("suggestions")]`.

If you add or rename a JSON field in the prompt, you MUST update the DTO mapping and parsing logic.

## 6. Editing The System Prompt Safely

Where to edit:

- Shared prompt: `src/Sublingual.Infrastructure/AI/SpeakingTutorPrompts.cs` (`BuildTutorSystemPrompt`)

Guidelines:

- Keep "OUTPUT FORMAT" strict: JSON only.
- Keep keys stable (`tutor_reply`, `suggestions`) unless you also update parsing.
- Avoid adding any prose outside JSON; parsing will fail and AI replies will be dropped.
- Keep reply length constraints aligned with the current language-level policy.

See also:

- `docs/speaking-practice/PROMPT-IMPROVEMENT-CHECKLIST.md`

## 6.1 Room Instructions UX Helpers

Room create/edit dialogs include an "Instruction Builder" panel:

- Quick templates (small built-in list) to generate structured instructions.
- A field-based builder (daily vs roleplay) that composes instructions into the existing textbox.
- Best-effort "Load from instructions" parsing for structured lines (`Scenario:`, `Goal:`, etc.).

## 7. Troubleshooting

Symptom: "AI returned empty reply" warning

- Common cause: JSON schema mismatch (keys changed) or model returned non-JSON.
- Check provider raw JSON quickly by temporarily logging the response body in provider service.

Symptom: Suggestions button shows but opens empty

- Provider returned empty/missing `suggestions` or texts are blank.
- Verify the provider prompt still requires 3 suggestions.

Symptom: Send button not enabling while typing

- The binding must use `UpdateSourceTrigger=PropertyChanged` (already set in `PracticeSessionView.axaml`).

## 8. Persistence Notes

Rooms and messages persist to a local JSON file:

- `speaking-practice-rooms.json` under the app root from `AppPathHelper.GetDefaultAppRoot()`.

If you change the persisted model (`SpeakingPracticeRoomModels.cs`), update:

- `SpeakingPracticeRoomStore.ReplaceMessages(...)`
- `SpeakingPracticeRoomStore.ToDomainMessages(...)`
