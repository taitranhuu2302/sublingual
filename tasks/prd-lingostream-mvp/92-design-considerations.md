# Design Considerations

## Design Considerations

- **Reuse existing UI:** All 4 pages (Dashboard, History, Captions, Settings) and the full shadcn component library are already built. Wire them up to real data rather than rewriting.
- **Design system:** Follow `DESIGN.md` — dark glassmorphism theme, Inter font, 8px spacing grid, electric blue primary, violet secondary, deep charcoal backgrounds.
- **Overlay styling:** The overlay must be readable over any background. Use a semi-transparent dark backdrop with high-contrast white text for original and the tertiary color (`#4cd7f6`) for translated text, matching the existing preview in the Captions page.
- **Responsive overlay:** The overlay should work at various screen resolutions. Default position: bottom-center, 80% of screen width, 120px tall.
- **Animations:** Use smooth opacity transitions for overlay auto-hide/show (300ms ease). VU meter bars should animate smoothly with `requestAnimationFrame`.

---
