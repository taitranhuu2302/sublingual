# Speaking Practice - Vocal Synthesis (TTS) Engine

This document details how Sublingual converts the AI's textual responses into spoken voice using local system vocal engines and cloud high-fidelity fallbacks.

---

## 1. Local System TTS (Default / Zero-Cost)

To maintain a zero-cost baseline and ensure near-instantaneous vocal playback, the application defaults to local operating system text-to-speech synthesisers.

### A. Windows Synthesis (`System.Speech.Synthesis`)
*   **API**: `System.Speech.Synthesis.SpeechSynthesizer`
*   **Implementation**:
    ```csharp
    using System.Speech.Synthesis;

    var synth = new SpeechSynthesizer();
    synth.SetOutputToDefaultAudioDevice();
    synth.SpeakAsync(text);
    ```
*   **Benefits**: Zero installation required, runs completely offline, supports standard Windows voice packages.

### B. macOS Synthesis (`AVSpeechSynthesizer` / `say` processes)
*   **Standard Method**: Launching the native Unix `/usr/bin/say` process asynchronously:
    ```csharp
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "say",
            Arguments = $"-v Samantha \"{sanitizedText}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        }
    };
    process.Start();
    ```
*   **Premium Method**: Wrapping Objective-C's `AVSpeechSynthesizer` in the `.dylib` native bridge and calling it via P/Invoke. This avoids the overhead of creating sub-processes and enables fine-grained control over the playback speed, pitch, and voice selection.

---

## 2. Cloud TTS Integrations (Optional / High-Quality)

For users who want hyper-realistic vocal tutors, we offer an optional cloud-synthesizer plugin:

*   **Gemini built-in Audio Response**: If the Gemini API is utilized, we can request the model to return its output directly in voice format, ensuring conversational pitch changes and emotional inflections are preserved.
*   **Google Cloud Text-to-Speech API**: Integrates high-fidelity Wavenet / Neural voices.
*   **ElevenLabs / OpenAI TTS API**: Optional integrations allowing premium natural voices.

---

## 3. Playback Synchronization & Audio Queues

To avoid overlapping audio playback (for example, if the user starts speaking or clicks "Skip" while the AI is still speaking):

*   **Playback Interrupts**: The `SpeakingSessionManager` maintains a cancelable playback queue. If the user clicks **Mute**, **Skip**, or begins speaking into the microphone, a `CancellationToken` triggers an immediate stoppage:
    *   Windows: `synth.SpeakAsyncCancelAll()`
    *   macOS: Kill the current `/usr/bin/say` process or call `[synthesizer stopSpeakingAtBoundary:AVSpeechBoundaryImmediate]` via interop.
*   **Session Focus**: When TTS is active, the VAD (Voice Activity Detection) system temporarily suspends listening to prevent the app from recording its own output speakers (preventing acoustic feedback loop issues).
