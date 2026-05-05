# US-005: Audio Capture via getUserMedia + AudioWorklet

### US-005: Audio Capture via getUserMedia + AudioWorklet

**Description:** As a user, I want the app to capture audio from my selected microphone and convert it into PCM format suitable for STT processing.

**Acceptance Criteria:**
- [ ] Renderer enumerates audio input devices using `navigator.mediaDevices.enumerateDevices()` and populates the Dashboard source dropdown with real device names
- [ ] On "Start Session", renderer calls `getUserMedia({ audio: { deviceId, sampleRate: 16000, channelCount: 1 } })`
- [ ] An `AudioWorkletProcessor` (in a separate file `src/renderer/src/workers/pcm-processor.worklet.ts`) downsamples audio to 16kHz mono and outputs PCM Int16 ArrayBuffers
- [ ] Audio chunks are emitted every ~250ms (4096 samples at 16kHz)
- [ ] A VU meter on the Dashboard shows real-time audio levels (RMS amplitude) from the AudioWorklet
- [ ] When session is stopped, the MediaStream tracks are stopped and AudioContext is closed
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---
