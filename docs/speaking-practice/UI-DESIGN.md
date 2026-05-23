# Speaking Practice - UI/UX Design & SukiUI Specifications

This document outlines the user interface layout, interactive SukiUI components, constructive feedback (enhancements) cards, and states handlers designed to provide a premium, modern experience.

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
*   When the app transits to the `AiThinking` state, the main input controls are disabled to prevent duplicate triggers.
*   A dedicated **`BusyArea`** wraps the chat session panel.
*   A beautiful looping **`Loading`** spinner (configured with `LoadingStyle.Circle` or `LoadingStyle.Wave`) appears over the tutor's profile panel.
*   A skeleton-pulse card represents the upcoming chat bubble, reflecting a natural "Tutor is writing a reply..." state.

### B. State: "Microphone Active / Listening"
*   A glowing, pulsing outer circle outlines the microphone button.
*   An animated audio wave visualizer (using sample volume RMS amplitudes) reflects real-time mic levels, giving the user reassuring, immediate visual feedback.

---

## 3. UI Layout & Grammar Enhancement Card

The conversational view integrates a **Constructive Language Enhancement** panel. This ensures that constructive tutor feedback is readable, encouraging, and separated from the main chat flow:

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
1.  **`GlassCard` (Enhancement Advice)**: Placed inline directly below the user's message bubble. Styled with a delicate amber or gold glow border and a small lightbulb icon (`💡`) to indicate a friendly tip.
2.  **`SukiSideMenu` / Navigation**: Seamless sidebar layout allowing the user to quickly return to normal live caption tools.
3.  **`WaveProgress`**: Dynamic indicators that visualize the vocal loading queue.

---

## 4. Dynamic MVVM State Bindings

The `PracticeSessionViewModel` coordinates the UI state via CommunityToolkit MVVM properties:

| Property | Type | Binding Purpose |
| :--- | :--- | :--- |
| `Messages` | `ObservableCollection<PracticeMessage>` | Binds directly to the Chat bubble `ListBox`. |
| `SessionState` | `SpeakingSessionState` | Controls active loader visibility and control enabling states. |
| `IsThinking` | `bool` | Derived helper property binding to SukiUI `BusyArea.IsBusy`. |
| `SpeechWaveLevel` | `double` | Feeds the real-time RMS microphone amplitude into the equalizer. |
| `Suggestions` | `List<SuggestionOption>` | Populates the help chips below the chat box. |
| `LatestEnhancement` | `string` | Binds to the visual feedback lightbulb card. |
