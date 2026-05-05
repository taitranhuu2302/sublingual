# US-019: Global Keyboard Shortcuts

### US-019: Global Keyboard Shortcuts

**Description:** As a user, I want system-wide keyboard shortcuts to control the app without switching windows.

**Acceptance Criteria:**
- [ ] Register global shortcuts in the main process using `globalShortcut.register`:
  - `Ctrl+Shift+R` (Windows) / `Cmd+Shift+R` (macOS): Toggle recording (start/stop session)
  - `Alt+T` / `Option+T`: Toggle overlay visibility
  - `Escape`: Clear current subtitles from overlay
- [ ] Shortcuts are unregistered on app quit (`will-quit` event)
- [ ] Shortcuts trigger the same actions as their UI button equivalents
- [ ] Settings page displays the shortcuts (already in UI) — shortcuts are read-only for MVP
- [ ] Typecheck/lint passes

---
