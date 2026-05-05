# US-015: Captions Page — Wire Up Live Preview with Real Settings

### US-015: Captions Page — Wire Up Live Preview with Real Settings

**Description:** As a user, I want the Captions page to control the overlay appearance and show a real-time preview of how subtitles will look.

**Acceptance Criteria:**
- [ ] Font Size slider updates `overlay-store.fontSize` and the preview text reflects the change immediately
- [ ] Background Opacity slider updates `overlay-store.backgroundOpacity` and preview reflects it
- [ ] Line Spacing slider updates `overlay-store.lineSpacing` and preview reflects it
- [ ] Add a "Display Mode" selector (bilingual / original only / translated only) that updates `overlay-store.displayMode`
- [ ] "Apply Overlay Preset" button sends current settings to main process via IPC, which forwards to overlay window
- [ ] Preview section shows the most recent subtitle from the session store (or placeholder if no session active)
- [ ] Typecheck/lint passes
- [ ] **[UI]** Verify in browser using dev-browser skill

---
