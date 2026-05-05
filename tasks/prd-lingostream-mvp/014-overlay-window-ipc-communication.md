# US-014: Overlay Window — IPC Communication

### US-014: Overlay Window — IPC Communication

**Description:** As a developer, I need the overlay window to receive subtitle updates and settings changes from the main renderer via the main process IPC bridge.

**Acceptance Criteria:**
- [ ] Main process listens for `subtitle-update` IPC messages from main renderer
- [ ] Main process forwards subtitle data to overlay window via `overlayWindow.webContents.send('subtitle-update', data)`
- [ ] Main process listens for `overlay-settings-update` IPC messages and forwards to overlay window
- [ ] Overlay window's preload script exposes `onSubtitleUpdate` and `onSettingsUpdate` listeners
- [ ] When overlay window is closed by user, main process can recreate it via a "Show Overlay" button in the main window
- [ ] Typecheck/lint passes

---
