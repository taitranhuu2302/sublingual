# US-003: Preload Script with IPC API

### US-003: Preload Script with IPC API

**Description:** As a developer, I need a secure preload script that exposes IPC methods to the renderer so it can communicate with the main process without direct Node.js access.

**Acceptance Criteria:**
- [ ] Preload script uses `contextBridge.exposeInMainWorld` to expose an `electronAPI` object
- [ ] Exposed methods include:
  - `getAudioDevices(): Promise<MediaDeviceInfo[]>` — lists available audio input devices
  - `startSession(config: { deviceId: string, sttEngine: string }): void` — tells main process to begin a session
  - `stopSession(): void` — tells main process to end the current session
  - `onSubtitleUpdate(callback: (data) => void): void` — listens for subtitle updates from main
  - `onBackendStatus(callback: (status) => void): void` — listens for backend health changes
  - `getSessionHistory(): Promise<Session[]>` — fetches saved sessions
  - `updateOverlaySettings(settings): void` — sends overlay config to main
- [ ] `contextIsolation: true` and `nodeIntegration: false` in BrowserWindow webPreferences
- [ ] TypeScript type declarations for `window.electronAPI` in a `.d.ts` file
- [ ] Typecheck/lint passes

---
