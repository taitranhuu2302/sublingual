# Speaking Practice - Backlog

This file tracks the remaining work for the `practice speak with AI` feature.
It is intentionally scoped to `docs/speaking-practice/` so it does not overlap with the root `TODO.md`, which tracks repository-level work.

## 1. Product Flow Update

- [x] Restructure the `Practice` tab into a room list entry page
  - When the user opens the `Practice` tab, show a list of practice rooms instead of opening a live conversation immediately.
  - The list should be the default entry point for the feature.
  - The room list view should be treated as a separate page/state from room detail.

- [x] Add practice room creation flow
  - The user can create a new practice room from the room list page.
  - Creating a room should open a dialog that asks the user for `instructions`.
  - `Instructions` are the core room prompt and should guide how the AI and user talk in that room.
  - The room can optionally derive its display name from those instructions or ask for a short room name if needed.
  - Keep the create flow simple and consistent with the current app patterns.

- [x] Redirect to room detail immediately after room creation
  - After the user submits the create-room dialog, open that room's detail page right away.
  - The user should not need to click the new room again after creating it.
  - Confirm whether canceling the dialog returns to the room list without side effects.

- [x] Add practice room deletion flow
  - The user can delete a single room from the list.
  - The user can also select and delete multiple rooms in one action.
  - Confirm whether deletion should remove only room metadata or also the message history attached to that room.

- [x] Add navigation from room list to room detail page
  - Clicking a room item should open a separate room detail page/state.
  - Room detail is where the actual conversation with AI happens.
  - Define the minimum back-navigation behavior so the user can return to the room list without losing structure.

- [x] Split room detail into two input modes: chat and speak
  - In room detail, the user should be able to either type a chat message or speak to the AI.
  - Both input modes should append into the same conversation history for that room.
  - Keep one shared message timeline instead of separate chat and voice threads.
  - Both modes should follow the room's saved `instructions` when talking to the AI.

- [x] Replace the current auto-send speaking flow with explicit start/stop speaking
  - Add a toggle/button for speaking in room detail.
  - The user starts speaking manually, talks, then stops manually.
  - Only after the user stops should the captured speech be sent to STT and then forwarded to the AI.
  - Remove the assumption that speech is auto-committed by silence for this room-detail flow unless there is a clear reason to keep it.

- [x] Render spoken user text on a single line
  - After STT completes, the rendered text for that spoken turn should stay on one line.
  - Confirm whether this means truncation, horizontal scroll avoidance, or plain single-line visual style with wrapping disabled.
  - Apply the rule specifically to the spoken-input rendering path unless product wants all messages constrained to one line.

## 2. Conversation Orchestration

- [x] Make room instructions part of the AI conversation contract
  - Each room should carry a persistent `instructions` field.
  - The AI prompt must always include those instructions so the conversation stays aligned with the room purpose.
  - Examples include a topic-focused room, a room limited to a vocabulary list, or a room for a specific style of advice/practice.

- [x] Add default fallback AI behavior when room instructions are empty or skipped
  - If the user skips the instructions field or leaves it blank, the room should still work.
  - Define a default conversational persona and fallback topics for the AI, such as greeting the user, asking about work, daily life, family-style conversation, sharing feelings, or giving advice on everyday issues.
  - Keep this fallback behavior explicit in prompt construction instead of relying on whatever the model does by default.

- [x] Fix duplicated user turn in AI request context
  - `SpeakingSessionManager` currently adds the latest user message into `_history` before calling the AI service.
  - `GroqSpeakingTutorService` and `GeminiSpeakingTutorService` then append the same `userText` again when building the outbound request.
  - Update the request-building flow so the latest user utterance is sent exactly once.
  - Re-check both providers to ensure they build equivalent conversation context.

- [x] Align runtime state transitions with the intended speaking flow
  - `SpeakingSessionState.Transcribing` exists in the domain and UI, but the runtime never transitions into it.
  - Add or remove this state based on the actual intended UX, but make code, UI text, and docs match.
  - Ensure the user-visible status changes correctly between `Listening`, `Transcribing`, `AiThinking`, and `AiSpeaking`.

- [x] Review stale or unused speaking-practice entry points
  - `PracticeSessionViewModel.HandleVoskTranscriptAsync()` does not appear to be used in the runtime flow.
  - Confirm whether this method is dead code or whether the intended wiring is missing.
  - Remove the dead path or wire it properly, but do not keep both models of transcript delivery without a reason.

## 3. Conversation And Session Model

- [x] Introduce a practice-room model in the speaking-practice domain/app layer
  - Add a room entity/view model that can represent list items and room detail context.
  - Define the relationship between a room and its message history.
  - Store the room-level `instructions` as part of this model.
  - Decide whether the current `SpeakingSessionManager` becomes per-room state or whether a higher-level room coordinator is needed.

- [x] Persist room list and room conversation history
  - Room list should survive app restarts.
  - Opening a room should restore its existing conversation history.
  - Room instructions should also persist and be restored with the room.
  - Keep persistence format minimal and aligned with the app's existing local-storage approach.

- [x] Scope AI conversation state to the selected room
  - Each room should maintain its own message history and AI context.
  - Each room should also keep its own instructions context.
  - Switching rooms must not leak messages or suggestions across rooms.
  - Confirm how active TTS/mic work should behave if the user leaves a room while a response is in progress.

## 4. Audio Session Reliability

- [x] Stop fire-and-forget microphone lifecycle calls from hiding failures
  - `SpeakingSessionManager.StartSession()` calls `_micTranscription.StartAsync()` without awaiting success.
  - `SpeakingSessionManager.StopSession()` also calls `_micTranscription.StopAsync()` fire-and-forget.
  - Change the flow so startup and shutdown failures can surface into state and status text instead of leaving the UI in a false `Listening` state.

- [x] Reconcile microphone transcription behavior with the new manual speak flow
  - The new UX requires explicit start speaking and stop speaking before sending to AI.
  - Rework `MicrophoneTranscriptionService` and related orchestration so capture/transcription align with this manual push-to-talk style.
  - Remove or demote VAD-driven assumptions if they no longer belong in the primary room-detail flow.

- [ ] Verify mute/unmute behavior around TTS playback
  - Mic muting is toggled around AI speech in `SpeakingSessionManager`.
  - Re-check whether pending partial text or capture timing can leak stale speech into the next turn after unmute.
  - Keep the fix minimal and limited to the actual observed edge case.

## 5. UI Structure

- [x] Create a room list page/view model for speaking practice
  - Add the UI state for listing rooms, creating rooms, selecting rooms, and deleting one or many rooms.
  - Include the create-room dialog flow for entering room instructions.
  - Follow the existing Avalonia + SukiUI patterns already used in the app.

- [x] Create a room detail page/view model for active conversation
  - The detail page should show the room title/context, room instructions, and the message timeline.
  - It should expose both typed chat input and explicit speak controls.
  - It should support navigation back to the room list page.

- [x] Update message rendering rules for room detail
  - Preserve the shared timeline for AI and user messages.
  - Apply the single-line display rule to spoken-text rendering as requested.
  - Re-check whether enhancement advice should still render below the user turn in the new layout.

## 6. AI Provider Configuration

- [x] Honor the configured Groq model from app settings
  - `SpeakingPracticeSettings.GroqModel` exists, but the Groq request currently hardcodes `llama-3.3-70b-versatile`.
  - Add a configuration path equivalent to Gemini so runtime behavior matches saved settings.

- [x] Tighten provider configuration failure behavior
  - `GeminiSpeakingTutorService` can be configured with an empty API key.
  - `GroqSpeakingTutorService` only receives an auth header when a key is present.
  - Decide how the feature should fail when keys are missing: disable start, show a clear error, or surface a session-level failure.
  - Make the UI/runtime behavior explicit instead of failing only at request time.

## 7. Docs Alignment

- [x] Update speaking-practice docs to reflect the implemented architecture
  - `OVERVIEW.md`, `AI-INTEGRATION.md`, and `AUDIO-CAPTURE.md` currently describe behaviors that do not fully match the code.
  - After the runtime fixes are done, revise the docs so state transitions, VAD behavior, and provider configuration are accurate.
  - Remove implementation claims that are still aspirational.

## 8. Out Of Scope

- [x] Do not add tests in this pass
  - This fix backlog intentionally excludes new test work per current request.
