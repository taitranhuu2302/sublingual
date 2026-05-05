# US-004: Zustand Store for Application State

### US-004: Zustand Store for Application State

**Description:** As a developer, I need a centralized Zustand store to manage session state, subtitle data, settings, and UI state so all components stay in sync.

**Acceptance Criteria:**
- [ ] Create `src/renderer/src/stores/session-store.ts` with the following state shape:
  ```
  {
    status: 'idle' | 'connecting' | 'streaming' | 'error',
    selectedDeviceId: string | null,
    sttEngine: 'vosk' | 'whisper',
    translationEnabled: boolean,
    currentPartial: string,
    subtitles: Array<{ id, original, translated, timestamp }>,
    error: string | null,
  }
  ```
- [ ] Actions: `setDevice`, `setSTTEngine`, `startSession`, `stopSession`, `addSubtitle`, `updatePartial`, `setError`, `clearSubtitles`
- [ ] Create `src/renderer/src/stores/overlay-store.ts` for overlay settings:
  ```
  {
    fontSize: number,
    backgroundOpacity: number,
    lineSpacing: number,
    displayMode: 'bilingual' | 'original-only' | 'translated-only',
    position: { x: number, y: number },
    autoHideDelay: number,
  }
  ```
- [ ] Actions: `updateSettings`, `resetDefaults`
- [ ] Typecheck/lint passes

---
