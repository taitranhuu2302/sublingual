# Speaking Practice - AI Orchestration & API Integrations

This document describes how Sublingual integrates with **Groq** and **Gemini** APIs to manage text-based chat completions, strict conversational prompts, constructive feedback (enhancements), and structured hint generation.

---

## 1. Speech-to-Text (STT) Integration

To maintain privacy, maximize speed, and optimize API costs, the Speaking Practice system utilizes the application's existing local **Vosk STT** engine. 

*   **Local Vosk STT Processing**: The raw microphone input is continuously fed into `VoskTranscriptionService`.
*   **Transcribing State**: While the speech is being processed, the UI displays a subtle `"Processing speech..."` indicator using SukiUI `BusyArea` overlays.
*   **Plain Text Forwarding**: Upon detecting user silence (VAD pause), the system commits the final transcription. The text string is extracted and forwarded to the orchestrator.

---

## 2. LLM Orchestrator & Strict Prompt Design

The `SpeakingSessionManager` coordinates the LLM chat completions. To ensure absolute compliance with constraints, we enforce a strict prompt format.

### A. Strict System Prompt Template
```
[System Instruction]
You are a highly professional, encouraging, and strict English Language Tutor. 
The user is practicing speaking on the topic: '{Topic}' at language level: '{LanguageLevel}'.

You must strictly adhere to the following rules:
1. TOPIC ADHERENCE: Stay 100% focused on the topic. Do not deviate under any circumstances.
2. SPOKEN STYLE: Keep your response short and natural, exactly 2 to 3 sentences.
3. CONSTRUCTIVE FEEDBACK: Analyze the user's latest sentence. If they made a grammar, vocabulary, or collocation error, provide a gentle, polite correction and suggest a better, more natural way to express it (Enhancement). If their sentence was flawless, give a small encouraging praise and leave the feedback brief.
4. RESPONSE FORMAT: You must return your response in the exact JSON schema requested.
```

---

## 3. Single-Call Structured JSON Output

To minimize network overhead, API costs, and latency, the conversational response, grammar feedback, and user hints are retrieved in **a single structured JSON call**:

### JSON Schema
```json
{
  "type": "object",
  "properties": {
    "tutor_reply": {
      "type": "string",
      "description": "The natural, encouraging conversational reply of the AI tutor (2-3 sentences max)."
    },
    "english_enhancement": {
      "type": "string",
      "description": "Constructive, polite feedback suggesting how the user could improve their grammar, spelling, or vocabulary phrasing in their last turn. Keep it extremely brief and supportive."
    },
    "suggestions": {
      "type": "array",
      "description": "3 distinct sentences of varying difficulty representing options the user can speak next.",
      "items": {
        "type": "object",
        "properties": {
          "label": { "type": "string", "description": "Category label, e.g., 'Direct Reply', 'Ask Elaborate', 'Shift Topic'" },
          "text": { "type": "string", "description": "The exact sentence the user could choose to say." }
        },
        "required": ["label", "text"]
      }
    }
  },
  "required": ["tutor_reply", "english_enhancement", "suggestions"]
}
```

---

## 4. Latency Mitigation & Thinking State Architecture

Large language models (especially reasoning models or deep-thinking models) can introduce a brief delay before returning the final text packet. To prevent the user from feeling lost:

*   **Cancelable Tasks**: Any active LLM request is fully tied to a `CancellationToken`. If the user leaves the session or restarts, the request aborts instantly.
*   **State Machine Transitions**:
    ```
    [User Stops Speaking] 
             │
             ▼
    (State: Transcribing)  --> SukiUI BusyArea overlays chat.
             │
             ▼
    (State: AiThinking)    --> Displays "Tutor is thinking..." pulsing text.
             │                 Enables a looping, smooth SukiUI Loading circle.
             ▼
    (State: AiSpeaking)    --> Reveals Tutor Bubble and starts TTS voice output.
    ```
*   **Parallel Streaming**: The UI binds to state-level observables, disabling input buttons while `IsThinking` is active, keeping the app interactive but protecting it from input double-clicks.
