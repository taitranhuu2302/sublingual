# US-013: Overlay Window — Always-on-Top Subtitle Display

### US-013: Overlay Window — Always-on-Top Subtitle Display

**Description:** As a user, I want a separate overlay window that shows subtitles on top of all other applications (including fullscreen video) so I can read translations while watching movies or attending meetings.

**Acceptance Criteria:**
- [ ] Main process creates a second `BrowserWindow` with:
  - `alwaysOnTop: true` (level `screen-saver` on macOS for fullscreen compat)
  - `frame: false` (no title bar)
  - `transparent: true` (transparent background)
  - `skipTaskbar: true`
  - `resizable: true`
  - `focusable: false` (so it doesn't steal focus from other apps)
  - Default size: 800x120, positioned at bottom-center of primary display on first launch
- [ ] Overlay window loads a separate HTML entry point (`overlay.html`) with its own React root
- [ ] Overlay renders:
  - Current partial text (dimmed/italic)
  - Last 2 final subtitles (original on top, translated below, per display mode setting)
- [ ] Overlay is draggable by clicking and dragging anywhere on it
- [ ] Overlay auto-hides (fades to 0 opacity) after configurable delay (default 5 seconds) when no new text arrives
- [ ] Overlay reappears immediately when new text arrives
- [ ] Overlay respects settings from `overlay-store`: font size, background opacity, display mode
- [ ] Overlay remembers its last monitor and window position, and falls back to the primary display if the saved monitor is no longer available
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---
