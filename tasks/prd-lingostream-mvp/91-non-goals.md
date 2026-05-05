# Non-Goals

## Non-Goals (Out of Scope for MVP)

- **System/loopback audio capture** — MVP supports microphone only. Loopback (capturing system audio for movies) will be added later with virtual audio device drivers (BlackHole on macOS, VB-Cable on Windows).
- **Argos Translate** — Only LibreTranslate is supported for MVP. Argos Translate is shown in Settings but disabled.
- **Multiple language pairs** — MVP supports English → Vietnamese only. Additional pairs are post-MVP.
- **Auto-detect source language** — MVP assumes English as source language.
- **Vocabulary lookup / dictionary** — Clicking on words to see definitions is deferred.
- **Export transcripts** — Exporting to `.txt` / `.csv` is deferred.
- **Custom keyboard shortcut binding** — Shortcuts are hardcoded for MVP.
- **Linux support** — macOS and Windows only.
- **Local Whisper (whisper.cpp)** — Only cloud Whisper API is supported; local Whisper model inference is post-MVP.
- **User accounts / cloud sync** — All data is local.
- **Automatic session titling via AI** — Session titles are derived from the first sentence of the transcript.

---
