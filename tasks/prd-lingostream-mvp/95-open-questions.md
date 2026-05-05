# Open Questions

## Open Questions

1. **LibreTranslate startup:** Should the app also manage (spawn/stop) a LibreTranslate Docker container, or require the user to run it separately? For MVP, requiring the user to run it separately is simpler.
2. **Session auto-naming:** Should session titles be the first N words of the transcript, or should the user be prompted to name sessions?
3. **Overlay window on multiple monitors:** Should the overlay remember which monitor it was on, or always default to the primary display?
4. **Audio format negotiation:** Some browsers/devices may not support 16kHz natively. Should the AudioWorklet handle resampling from 44.1kHz/48kHz to 16kHz?
5. **Whisper buffering strategy:** What is the optimal buffer duration before sending to Whisper API? 3 seconds? 5 seconds? Should silence detection trigger early sends?
