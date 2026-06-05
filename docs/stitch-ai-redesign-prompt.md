# Sublingual UI/UX
> **Design philosophy:** This prompt describes WHAT the system needs to do and WHO it serves. Layout, navigation, and visual decisions are entirely yours to design. No mockup to copy — just requirements to solve.

---

## BRAND IDENTITY

**Brand:** NERIS
**Founder:** Ralph
**Studio philosophy:** *Imagination meets precision engineering*

NERIS is an independent software studio dedicated to crafting elegant digital products where imagination meets precision engineering. Inspired by the depth and fluidity of the ocean, NERIS transforms ideas into thoughtful experiences through clean architecture, high-performance systems, and refined design.

**Motto:**
> Dream like an artist.
> Build like an engineer.
> Finish like a craftsman.

**Creative inspiration:**
- The ocean — depth, mystery, endless discovery
- Fluid movement — smooth transitions, natural flow
- Ancient mythology — stories of exploration and wonder
- Intuition — design that feels right

---

## PRODUCT IDENTITY

**Name:** Sublingual (by NERIS)
**Tagline:** Real-time speech-to-text & translation for your desktop
**Elevator pitch:** Sublingual captures your system audio, transcribes speech locally (offline), translates it, and displays live bilingual subtitles — either in a floating overlay window or directly in the app. Think of it as a universal real-time caption layer for any audio playing on your computer.

---

## DESIGN TOKENS

### Color Palette — NERIS Ocean

| Token | Hex | RGB | UI Role |
|-------|-----|-----|---------|
| `--neris-deep-ocean` | #001A33 | (0,26,51) | Primary backgrounds, deepest surface layer |
| `--neris-ocean-navy` | #003366 | (0,51,102) | Logo, headers, navigation bars, elevated surfaces |
| `--neris-marine-blue` | #004080 | (0,64,128) | Secondary UI elements, card surfaces, subtle borders |
| `--neris-horizon-blue` | #0059B3 | (0,89,179) | Interactive elements, hover states, links |
| `--neris-aurora-blue` | #0066CC | (0,102,204) | CTAs, active/live indicators, highlights, recording state |

**Dark theme (only theme):**
- Deep Ocean → deepest background (window body)
- Ocean Navy → elevated surfaces (cards, sidebars, panels)
- Marine Blue → subtle borders, secondary surfaces, dividers
- Horizon Blue → interactive hover, focus rings, secondary buttons
- Aurora Blue → primary CTAs, recording indicator, active state

### Typography
- **Primary UI:** Inter (variable font, all weights 400–700)
- **Fallback stack:** `Inter`, `-apple-system`, `BlinkMacSystemFont`, `Segoe UI`, sans-serif
- **Monospace:** JetBrains Mono or SF Mono for transcript text, timestamps, code-adjacent content
- **Scale:** 12/14/16/18/20/24/32px (Tailwind `text-sm` through `text-4xl`)
- **Line height:** 1.5 for body, 1.3 for headings, 1.6 for bilingual transcript lines (accommodates diacritics)

### Border Radius
| Token | Value | Usage |
|-------|-------|-------|
| `--radius-xs` | 4px | Badges, small indicators, inputs |
| `--radius-sm` | 6px | Buttons, small cards, dropdown items |
| `--radius-md` | 8px | Cards, panels, dialogs, select fields |
| `--radius-lg` | 12px | Modals, sidebar panels, larger surfaces |
| `--radius-xl` | 16px | Main window, overlay background, group containers |
| `--radius-full` | 9999px | Pills, avatars, toggle handles |

> macOS-native feel: prefer `--radius-lg` (12px) for top-level surfaces, consistent with macOS window corner radius.

### Shadows & Depth
- Subtle multi-layer shadows (not flat). Surface elevation via:
  - `sm`: cards, dropdowns (1px offset, 4px blur, `hsla(0,0%,0%,0.08)`)
  - `md`: dialogs, modals (2px offset, 8px blur, `hsla(0,0%,0%,0.12)`)
  - `lg`: overlay window, floating panels (4px offset, 16px blur, `hsla(0,0%,0%,0.16)`)
  - `xl`: top-level popovers, tooltips (8px offset, 24px blur, `hsla(0,0%,0%,0.20)`)
- Translucent surfaces with `backdrop-blur` where appropriate

### Glassmorphism (Frosted Glass) — Optional
Optional visual enhancement for elevated surfaces. Use sparingly for special surfaces; default to solid surfaces for cleaner minimal look:

- **Background:** `hsla(var(--neris-ocean-navy-hsl), 0.6)` → 60% opacity Ocean Navy
- **Blur:** `backdrop-blur-xl` (24px) for depth illusion
- **Border:** subtle `hsla(255,255%,255%,0.08)` 1px top/light border (frosted edge highlight)
- **Shadow:** soft multi-layer shadow beneath (see Shadows & Depth)
- **Consider on:** overlay window, active state panels, subtle surface differentiation
- **Default to:** solid Ocean Navy surfaces for cleaner, minimal look

> Glassmorphism creates the "frosted glass" effect — content beneath the panel is subtly visible through a blurred, semi-transparent surface. Use intentionally, not universally. The default UI is clean solid surfaces; glassmorphism adds depth on specific surfaces where it adds value.

### macOS Design Language
The app runs on both macOS and Windows but takes strong visual cues from **macOS** as the primary design target:

- **Title bar:** unified toolbar (no separate title bar). Traffic light controls respected on macOS, custom title bar buttons on Windows.
- **Vibrancy (optional):** Reserve `backdrop-blur` surfaces for overlay window and active state panels. Default to solid surfaces for a cleaner minimal look.
- **Large corner radii:** 12px+ for windows and panels (native macOS window radius is ~12px).
- **Minimal chrome:** No heavy borders or bevels. Depth comes from shadows + translucency, not strokes.
- **Spacing:** Generous whitespace (16–24px gutters). macOS-native apps breathe.
- **Scrollbars:** Overlay scrollbars (thin, auto-hide) consistent with platform convention.
- **Animations:** Fluid spring animations (0.4–0.6s ease-out) for panel reveals, mode transitions. 0.2s ease for hover/active micro-interactions.
- **Icons:** Lucide outlined style — thin, consistent stroke width, matching SF Symbols visual weight.

> Design principle: **macOS-first, Windows-respectful.** The visual language originates from macOS conventions, but nothing should feel broken or foreign on Windows.

---

## WHO IS THIS FOR?

| Persona | Scenario | Core Need |
|---------|----------|-----------|
| **Remote Worker** | Joining English meetings as a non-native speaker | See what's being said in real-time, in their language |
| **Language Learner** | Watching foreign movies, lectures, podcasts | Bilingual subtitles — original + translation side by side |
| **Journalist / Researcher** | Recording interviews, transcribing later | Accurate transcripts saved with timestamps, searchable later |
| **Accessibility User** | Hard of hearing during online meetings | Live captions from any application, not just browser extensions |

---

## CORE CAPABILITIES (ALL must be accessible from the UI)

### 1. Audio Capture
- Enumerate audio output devices on the system
- Let user select which device/application to capture
- Visual feedback that audio is being captured (level meter, waveform, or indicator)
- Capture system audio — NOT microphone (loopback / WASAPI / ScreenCaptureKit)

### 2. Speech-to-Text (STT)
- Run entirely offline using local Vosk models
- Support multiple languages: English, Vietnamese, Japanese, Korean, Chinese, French, German, Spanish
- Real-time partial (draft) transcript updates as speech is being recognized
- Finalize sentences automatically after a configurable timeout
- Optional speaker identification (who is speaking) — requires separate model

### 3. Translation
- Auto-translate each finalized transcript segment
- Two providers: Google Translate Free API (default, no key needed) or Local TranslateService (self-hosted)
- Configurable target language
- Show original + translated text together (bilingual mode)
- Translation test tool so users can verify the provider works

### 4. Overlay Window
- A separate, always-on-top, borderless, transparent window
- Displays live subtitles: original text + translation
- Draggable by the user to any screen position
- Theme: Dark or Light
- Configurable: font size, line height, background opacity (via glassmorphism blur intensity), window dimensions
- Can be shown/hidden independently from the main app window
- Auto-scrolls to the latest content

### 5. Session Recording & History
- Every capture session is automatically saved with:
  - Start time, duration, audio source, model used, language pair
  - Full transcript lines with timestamps
  - Translations for each line
- Browse, search, and review past sessions
- Export individual sessions as TXT or JSON
- Delete sessions (single or batch)
- Open session folder in file explorer

### 6. Session Organization
- Users can create folders to organize sessions (e.g., "Work", "Study", "Podcasts")
- A default "Global" folder for uncategorized sessions
- Move sessions between folders
- Rename and delete folders (with safeguards)

### 7. Model Management
- Display available STT models with: name, size, accuracy level
- Download models from within the app with progress indication
- Select which downloaded model to use
- Open models folder in file explorer

### 8. Settings & Configuration
- General: storage paths for sessions and model files (with OS folder picker)
- Speech: model selection, source language, max speakers, flush timeout
- Translation: enable/disable, provider selection, target language, provider URLs, test tool
- Overlay: theme, font size, line spacing, opacity, dimensions, show/hide translation

---

## DESIGN PRINCIPLES (guardrails, not instructions)

1. **Dashboard as transcript viewer** — The main screen should show live transcription during capture, not just stats. Users should feel the app is "alive" while capturing.

2. **Dark-only** — Dark theme is the only theme. No light mode. The app is used during meetings, media consumption, and low-light environments. Deep Ocean (#001A33) backgrounds with Ocean Navy (#003366) elevated surfaces create a calm, focus-friendly environment that reduces eye strain during extended use.

3. **Progressive disclosure** — Show only what's relevant to the current state. Idle = clean and focused. Capturing = information-rich. Don't overwhelm first-time users.

4. **Real-time feel** — Transitions should feel smooth and "live" (subtle animations for new content, fluid state changes). Avoid jarring layout jumps.

5. **Bilingual by default** — Vietnamese is the primary target translation language. The UI must handle Latin + diacritics (ắ, ệ, ỏ, ư, ơ...) at all sizes. Design for bilingual text display as the norm, not an edge case.

6. **Ocean depth, crafted warmth** — Not corporate-sterile. Aurora Blue (#0066CC) signals "live" and "active." Deep Ocean (#001A33) backgrounds meld into Ocean Navy (#003366) elevated surfaces for layered depth. Subtle shadows, fluid gradients. Think modern creative tools, not enterprise software.

7. **Space-efficient** — This is a desktop app, not a mobile app. Use horizontal space well. Multi-column layouts are encouraged where they add value. But don't be afraid of whitespace in idle states.

8. **Obvious affordances** — Every clickable thing should look clickable. Every dangerous action (delete) should have confirmation. Every icon-only button should have a tooltip.

9. **Glassmorphism as optional accent** — Default UI uses clean solid Ocean Navy surfaces. Glassmorphism (`backdrop-blur-xl`, 60% opacity) is reserved for specific surfaces where depth adds value (overlay window, active state panels). Not a universal treatment — use intentionally and sparingly. The default is clean, minimal, and solid.

10. **Consistent layout system across all screens** — Every page shares the same structural DNA: unified spacing scale (16–24px gutters), same surface elevation model, same glassmorphism treatment for panels. The 3 primary pages (Home, Sessions, Settings) use a consistent sidebar/content split pattern. Navigation, typography scale, button styles, and radius tokens are identical everywhere. No page feels like a different app.

---

## TECHNICAL CONSTRAINTS 

- **Platform:** Electron desktop app (macOS-first + Windows)
- **Design target:** macOS as primary visual reference (vibrancy, large radii, minimal chrome). Windows gets same layout with platform-native adaptations (title bar, scrollbars, fonts).
- **Component library:** shadcn/ui (Radix primitives available)
- **Styling:** Tailwind CSS 4
- **Icons:** Lucide React (use these icon names when mocking)
- **Overlay is a separate Electron BrowserWindow** — it can have its own HTML/CSS independent from the main window
- **Minimum window size:** roughly 900×600px (the app should work at this size, with graceful degradation for smaller)
- **Navigation model:** The app has 3 primary pages (Home, Sessions, Settings) — how you structure navigation is up to you

---

## USER FLOWS (the journeys that must be smooth)

### Flow 1: First Launch
User opens app → no model installed → guided to download one → selects model → ready to capture

### Flow 2: Capture Session
Select audio source → press Start → see live transcript appear → optionally open overlay → press Stop → transcript preserved for review

### Flow 3: Review & Export
Navigate to session history → find a session (search/browse) → view full transcript with translations → export as TXT or JSON → or delete

### Flow 4: Configure Overlay
Open Settings → adjust overlay appearance → see live preview update → toggle overlay on → see changes applied immediately

### Flow 5: Organize Sessions
Browse sessions → create folder → select sessions → move to folder → future captures go to that folder (persistent preference)

---

## STATES EVERY SCREEN MUST HANDLE

Design must account for these states across ALL relevant views:

| State | Example |
|-------|---------|
| **Loading** | Fetching model list, loading session data, enumerating audio devices |
| **Empty** | No models installed, no sessions recorded, no transcript yet, no search results |
| **Active** | Capturing in progress, live transcript streaming, translation happening |
| **Error** | Translation failed, model download failed, audio permission missing, file not found |
| **Disabled** | Feature requires a missing dependency (e.g., speaker ID needs speaker model) |
| **Confirmation** | Delete session, delete folder, stop capture (unsaved data warning?) |

---

## WHAT NOT TO DESIGN

- Login / authentication flows (app is local-only)
- Cloud sync or multi-device features
- Microphone input (system audio capture only)
- Video recording or camera features
- Payment or subscription UI
- Onboarding wizards beyond the first-launch model download prompt
- Notification system beyond in-app toasts

---

## DELIVERABLE

A complete visual redesign proposal under the NERIS brand including:

1. **All screens** at high fidelity in dark theme, using the NERIS Ocean color palette with glassmorphism surfaces
2. **All component states** (idle, hover, active, disabled, loading, error, empty)
3. **Key interaction flows** as sequential screen states or diagrams
4. **Overlay window** as a standalone design (separate from main window)
5. **Design tokens** — spacing scale, typography, shadows, radii, animation curves (base palette defined above)

**Design the UI you believe best serves the users and the product. You have full creative freedom on layout, navigation, structure, and visual treatment.**
