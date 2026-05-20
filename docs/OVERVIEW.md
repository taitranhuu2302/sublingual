# Technical Project Overview: Cross-Platform Live Caption & Translation

## 1. Project Description
A real-time desktop application designed to provide live captions and translations for online meetings (Google Meet, Microsoft Teams, Zoom) and system audio (YouTube, Videos). The application operates as a floating, transparent overlay on the screen. 

## 2. Core Technology Stack
The project adopts a fully native desktop architecture using the .NET ecosystem, avoiding web-based wrappers (like Electron) to ensure high performance, low memory footprint, and deep system integration.

### 2.1. Frontend (User Interface)
*   **Framework:** **Avalonia UI**
*   **Language:** C# & XAML
*   **Why Avalonia?** 
    *   Pixel-perfect, native rendering on both Windows and macOS.
    *   Excellent support for transparent, borderless, "always-on-top" windows (essential for floating subtitles).
    *   No WebView/Chromium overhead.

### 2.2. Backend & Core Logic
*   **Runtime:** .NET 8 (or later)
*   **Language:** C#
*   **Architecture Pattern:** MVVM (Model-View-ViewModel) using ReactiveUI or CommunityToolkit.Mvvm.

### 2.3. Audio Capture Layer (System Loopback)
Capturing system audio natively is the biggest technical challenge across different OS sandboxes.
*   **Windows:** 
    *   **Library:** `NAudio` (NuGet package)
    *   **API:** Windows Audio Session API (WASAPI) Loopback Capture.
*   **macOS:**
    *   **Library:** Custom Shared Library (`.dylib`) written in C++/Objective-C++.
    *   **API:** Apple `ScreenCaptureKit` (macOS 13.0+).
    *   **Bridge:** .NET `P/Invoke` (`[DllImport]`) is used to call the C++ audio buffer callback directly into C#.

### 2.4. AI Processing Layer
*   **Speech-to-Text (STT):** **Vosk** local speech recognition.
    *   *Implementation:* Audio is chunked in-process and transcribed locally without a hosted STT dependency.
*   **Translation Engine:** Settings-driven provider factory.
    *   *Initial providers:* `GoogleTranslateFreeApi` and `LibreTranslate`.
    *   *Implementation:* The app resolves one provider or an ordered fallback chain from settings, then translates the transcript text asynchronously.

---

## 3. System Architecture & Data Flow

```mermaid
Graph TD
    SystemAudio[System Audio Speaker] --> WinAudio[Windows: NAudio WASAPI]
    SystemAudio --> MacAudio[macOS: C++ ScreenCaptureKit dylib]
    
    WinAudio --> AudioBuffer[C# Audio Buffer Manager]
    MacAudio -- P/Invoke --> AudioBuffer
    
    AudioBuffer -- "Chunking" --> STT_API[Vosk]
    
    STT_API -- Original Text --> Translator[Translation Provider Factory]
    
    Translator -- Translated Text --> ViewModel[Avalonia MVVM]
    ViewModel --> UI[Avalonia Transparent Overlay]
```

### Step-by-Step Execution:
1. **Init:** The Avalonia app launches a borderless, transparent window overlay.
2. **Capture:** The OS-specific audio module hooks into the system output and streams raw PCM Float32 audio data into the C# buffer.
3. **Process:** C# logic downsamples the audio to 16kHz Mono (standard AI format) and splits it into small chunks using Voice Activity Detection (VAD) or fixed time-windows.
4. **Recognize:** Chunks are dispatched to the local Vosk recognizer.
5. **Translate:** Recognized text is piped to the configured translation provider chain.
6. **Render:** The Avalonia `ViewModel` updates the `Text` property, and the XAML UI re-renders the subtitle on the user's screen instantly.

---

## 4. Key Technical Challenges & Solutions

### Challenge 1: macOS System Audio Sandboxing
*   **Issue:** macOS does not allow third-party apps to record system output directly without installing virtual drivers (like BlackHole).
*   **Solution:** Utilize Apple's modern `ScreenCaptureKit` framework. Since .NET cannot call this Swift/Objective-C framework directly, a lightweight native C++ wrapper is compiled into a `.dylib`. C# loads this library at runtime to bypass sandbox restrictions natively.
*   **Requirement Clarification:** The capture path must follow the current active output device, including built-in speakers, wired headphones, and Bluetooth audio outputs.

### Challenge 2: "Always-on-top" Transparent UI across OS
*   **Issue:** Window transparency behaves differently on DWM (Windows) and Quartz Compositor (macOS).
*   **Solution:** Configure the Avalonia `Window` specifically:
    *   `Background="{x:Null}"`
    *   `TransparencyLevelHint="Transparent"`
    *   `SystemDecorations="None"`
    *   `Topmost="True"`
    *   Ensure the window is set to "Click-through" (HitTestVisible = false on the main grid) so users can interact with the meeting app underneath the subtitles.

### Challenge 3: Real-time Audio Chunking Latency
*   **Issue:** Waiting too long to send audio creates subtitle lag; sending chunks too short destroys the context for the AI.
*   **Solution:** Implement a sliding window algorithm in C# or use a lightweight local VAD (Voice Activity Detection) library (like `Silero VAD` via ONNX runtime in C#) to split audio precisely when there is a pause in speech.

---

## 5. Development Setup & Milestones

### Pre-requisites (No Admin Rights required for core .NET):
*   .NET 8 SDK (Portable/ZIP version).
*   JetBrains Rider or Visual Studio Code with C# Dev Kit.
*   (For Mac Native Audio): LLVM/Clang portable to compile the `.dylib`.

### Milestones:
*   **Phase 1:** Setup Avalonia project, create the transparent overlay UI, and implement mock text data binding.
*   **Phase 2:** Implement Windows audio loopback (`NAudio`) and test saving system audio to a `.wav` file.
*   **Phase 3:** Write the C++/Objective-C++ wrapper for macOS `ScreenCaptureKit` and integrate via `P/Invoke`.
*   **Phase 4:** Keep Vosk as the STT engine and integrate real translation providers through the factory. Connect the audio buffer to the translation pipeline.
*   **Phase 5:** Finalize UI polish (stroke, fonts, drag-to-move) and compile final binaries (`.exe` for Windows, `.app` for macOS).
