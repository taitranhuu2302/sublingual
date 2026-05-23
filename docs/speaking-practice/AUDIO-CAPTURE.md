# Speaking Practice - Microphone Capture & Audio Processing

This document outlines the technical details for capturing the user's voice, normalizing the audio format, and implementing Voice Activity Detection (VAD) to auto-commit voice chunks.

---

## 1. Hardware Microphone Capture

Unlike live translations which record system loopback audio, the Speaking Practice system must capture the user's local microphone input.

### A. Windows Implementation (`NAudio.Wave.WasapiCapture`)
*   We utilize NAudio's WASAPI capture loops targeting input devices.
*   `WasapiCapture` receives PCM audio buffers at the hardware's native sample rate.
*   An asynchronous reader loop listens to the `DataAvailable` event and forwards captured bytes into the C# normalizer.

### B. macOS Implementation (`AVFoundation` / C++ Bridge)
*   Since `ScreenCaptureKit` only records speaker loopback output, we extend the C++/Objective-C++ native bridge (`ScreenCaptureKitBridge`) to support microphone capture using standard Apple AVFoundation.
*   **AVFoundation Setup**:
    ```objectivec
    AVCaptureSession *session = [[AVCaptureSession alloc] init];
    AVCaptureDevice *device = [AVCaptureDevice defaultDeviceWithMediaType:AVMediaTypeAudio];
    AVCaptureDeviceInput *input = [AVCaptureDeviceInput deviceInputWithDevice:device error:nil];
    [session addInput:input];
    
    AVCaptureAudioDataOutput *output = [[AVCaptureAudioDataOutput alloc] init];
    dispatch_queue_t queue = dispatch_queue_create("sublingual.mic.queue", NULL);
    [output setSampleBufferDelegate:self queue:queue];
    [session addOutput:output];
    [session startRunning];
    ```
*   **Data Routing**: Inside `captureOutput:didOutputSampleBuffer:`, the native code extracts raw float samples and routes them to our existing `AudioBufferCallback` delegates in C#, reusing the high-performance memory bridge.

---

## 2. Audio Normalization (`AudioFormatNormalizer`)

To ensure flawless compatibility with STT models (Vosk, Groq, Gemini), the raw captured microphone samples are standardized through the `AudioFormatNormalizer` utility:

*   **Target Sample Rate**: `16,000 Hz`
*   **Target Channels**: `1` (Mono)
*   **Target Bits Per Sample**: `16-bit` (Signed Linear PCM)

The normalizer automatically handles float-to-short conversion, downsampling via linear interpolation, and channel mixing (averaging stereo inputs to mono).

---

## 3. Voice Activity Detection (VAD) & Silence Trigger

To facilitate natural, hands-free conversation, the user should not need to click a button every time they finish speaking. The application integrates a lightweight **Energy-based Silence Detection Processor**:

*   **Averaging Root-Mean-Square (RMS)**: The system computes the RMS power level of every incoming 100ms audio chunk:
    $$\text{RMS} = \sqrt{\frac{1}{N} \sum_{i=1}^{N} x_i^2}$$
*   **Dynamic Thresholding**: During initialization, the system computes the ambient background noise level. If the audio energy drops below a configured threshold (typically $-45\text{dB}$ to $-50\text{dB}$) for a consecutive period of **1.2 to 1.5 seconds**, the speaker is deemed to have finished their thought.
*   **Auto-Commit**: Once silence is verified, the system triggers the `AudioCaptureState.Processing` phase, stops the microphone capture feed momentarily, and routes the accumulated PCM buffers to the STT pipeline.
