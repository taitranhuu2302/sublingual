# Speaking Practice - UI/UX Design & SukiUI Specifications

This document outlines the user interface layout and interactive SukiUI components.

---

## 1. Design Aesthetics & Visual Identity

Following Sublingual's premium guidelines, the Speaking Practice view adopts a modern **Glassmorphic / Dark-Mode** palette:
*   **Colors**: Sleek dark slate background with harmony HSL overlays (glass card borders, deep teal buttons for users, soft blue accents for AI).
*   **Typography**: Clean, premium fonts with clear styling.
*   **Animations**: Micro-transitions for user suggestions, pulsing glow effects for mic capture states, and fading lists during chat scrolling.

---

## 2. SukiUI Interactive States (Thinking & Loading Handler)

To ensure the user is fully aware when the AI is processing (STT, LLM thinking, or TTS synthesis in flight), the interface leverages SukiUI's high-fidelity progress indicators:

### A. State: "Tutor is Thinking..."
*   When the app transits to the `AiThinking` state, the UI renders a non-blocking inline "thinking" bubble in the message list.
*   The message list remains visible (no full overlay).

### B. State: "Microphone Active / Listening"
*   A glowing, pulsing outer circle outlines the microphone button.
*   An animated audio wave visualizer (using sample volume RMS amplitudes) reflects real-time mic levels, giving the user reassuring, immediate visual feedback.

---

## 3. UI Layout

```
+-------------------------------------------------------------+
| [Back to Dashboard]                   AI Vocal Tutor (Topic)|
+-------------------------------------------------------------+
|                                                             |
|   +-----------------------------------------------------+   |
|   | [GlassCard - Chat History Area]                     |   |
|   |                                                     |   |
|   |   (AI) Hello! Let's talk about coffee.              |   |
|   |                                                     |   |
|   |   (User) I love latte!                              |   |
|   |                                                     |   |
|   |   +---------------------------------------------+   |   |
|   |   | [Tutor Enhancement Advice - GlassCard]     |   |   |
|   |   |  💡 Tip: "Instead of 'I love latte', say    |   |   |
|   |   |  'I'm a big fan of lattes' for natural tone!"|   |   |
|   |   +---------------------------------------------+   |   |
|   +-----------------------------------------------------+   |
|                                                             |
|   +-----------------------------------------------------+   |
|   | [Tutor is Thinking... Loading Circle]               |   |
|   +-----------------------------------------------------+   |
|                                                             |
|   +-----------------------------------------------------+   |
|   | [Suggestion Chips Panel]                            |   |
|   |   [Chip: I'd like a double-espresso]                |   |
|   |   [Chip: Can I have a cold-brew coffee, please?]    |   |
|   +-----------------------------------------------------+   |
|                                                             |
|   +-----------------------------------------------------+   |
|   | [Control Dashboard]                                 |   |
|   |   [Mute Mic]   (( RECORDING ))   [Replay Voice]     |   |
|   +-----------------------------------------------------+   |
+-------------------------------------------------------------+
```

### Main Visual Elements:
1.  **`GlassCard` (Chat bubbles)**: AI on left, user on right.
2.  **Suggestions**: Shown under each AI message via a toggle button. Suggestions panel is hidden by default.

---

## 4. Dynamic MVVM State Bindings

The `PracticeSessionViewModel` coordinates the UI state via CommunityToolkit MVVM properties:

| Property | Type | Binding Purpose |
| :--- | :--- | :--- |
| `Messages` | `ObservableCollection<PracticeMessage>` | Binds directly to the Chat bubble `ListBox`. |
| `SessionState` | `SpeakingSessionState` | Controls active loader visibility and control enabling states. |
| `IsThinking` | `bool` | Derived helper property binding to SukiUI `BusyArea.IsBusy`. |
| `SpeechWaveLevel` | `double` | Feeds the real-time RMS microphone amplitude into the equalizer. |
| `Suggestions` | `List<SuggestionOption>` | Populates the suggestion buttons under an AI message (hidden until toggled). |
