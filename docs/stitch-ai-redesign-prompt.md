# Stitch AI — Sublingual UI/UX Redesign Prompt

## PROJECT NAME
**Sublingual** — Real-time Speech-to-Text & Translation Desktop App

---

## APP OVERVIEW
Sublingual is an Electron desktop app (macOS + Windows) that captures system audio, runs local speech-to-text via Vosk (offline, no cloud), auto-translates each recognized segment, and displays live bilingual subtitles in a transparent floating overlay window.

**Primary use cases:**
- Online meetings (Google Meet, Teams, Zoom) — see live subtitles in your language
- Video & media playback — real-time captions from system audio
- Language learning — bilingual subtitles during study or entertainment

---

## TARGET USERS
- Professionals in multilingual meetings who need live translated captions
- Language learners consuming foreign media
- Anyone needing real-time transcription + translation from system audio

---

## DESIGN STYLE & TONE
- **Aesthetic:** Modern, clean, professional desktop UI — tech-forward but warm
- **Mode:** Dark-first (primary), with full light mode support
- **Depth:** Subtle layering — soft shadows, transparent/blur panels, distinct card surfaces
- **Accent color:** Emerald/Green spectrum (#10B981) — signals "live" and "active"
- **Component style:** shadcn/ui philosophy — rounded corners (--radius), soft borders, clean readable typography
- **Spacing:** 8px grid system
- **Typography:** System font stack (Inter/SF Pro); monospace for timestamps and code fields

---

## COLOR SYSTEM
| Token | Light | Dark |
|-------|-------|------|
| Background | #FFFFFF | #0B1120 |
| Surface / Card | #F8FAFC | #111827 |
| Border | #E2E8F0 | #1F2937 |
| Text Primary | #0F172A | #F1F5F9 |
| Text Muted | #64748B | #94A3B8 |
| Accent (Emerald) | #10B981 | #10B981 |
| Destructive | #EF4444 | #EF4444 |
| Warning | #F59E0B | #F59E0B |
| Overlay Background | rgba(245,247,250, opacity) | rgba(14,19,28, opacity) |

---

## TECH STACK (for design reference)
- **Framework:** Electron 42 + React 19 + TypeScript
- **UI Library:** shadcn/ui (Radix primitives) + Tailwind CSS 4
- **Icons:** Lucide React
- **Router:** React Router 7
- **Window:** Borderless floating overlay window (separate BrowserWindow in Electron)

---

# SCREEN 1: HOME — Live Transcript Hub

> **Concept:** The dashboard IS the transcript viewer. Users see recognized speech in real-time on the main screen without needing the overlay open. The overlay becomes a secondary/supplementary display (e.g., for screen sharing or floating over fullscreen apps).

## Layout (3-column when capturing, 1-column when idle)

```
┌─────────────────────────────────────────────────────────────┐
│ [Sublingual]   Home | Sessions | Settings          [Status] │  ← Top Nav Bar
├─────────────────────────────────────────────────────────────┤
│ Capturing   00:12:45                                       │  ← Compact Timer Bar
├──────────────┬──────────────────────────┬───────────────────┤
│              │                          │                   │
│  Audio       │   LIVE TRANSCRIPT        │   Translation     │
│  Source      │                          │   Panel           │
│  Selector    │   [Speaker A] Hello,     │                   │
│              │   everyone. Today        │   Hôm nay tôi     │
│  [Start/Stop]│   I'd like to discuss... │   muốn thảo luận  │
│              │                          │   về kiến trúc    │
│  Controls    │   [Speaker B] Thanks     │   mới...          │
│  • Clear     │   for having me.         │                   │
│  • Overlay   │                          │   Cảm ơn đã mời   │
│  • Settings  │   ▂▃▅▆█▆▅▃▂  (waveform) │   tôi...          │
│              │                          │                   │
│  Stats       │   --- typing... (partial)│                   │
│  142 segs    │                          │                   │
│  1,247 words │                          │                   │
│              │                          │                   │
├──────────────┴──────────────────────────┴───────────────────┤
│  Session Info:  Model: vosk-small-en  |  en → vi  |  Google │  ← Info Footer
└─────────────────────────────────────────────────────────────┘
```

### Idle State (before capturing)
- Center: large app icon + "Ready to Capture" heading + subtitle "Select an audio source and press Start"
- Bottom: audio source chip selector + green Start button
- No columns visible yet — clean, focused launch state

### Capturing State (3 columns appear)
- **Left Sidebar (200px):** Audio source chip, Start/Stop button (contextual), Clear + Overlay toggle icon buttons, live stats (segments count, words count)
- **Center (flex-1):** Live scrolling transcript — newest at bottom, auto-scroll. Each line shows: timestamp (left, small, mono), optional speaker badge (colored), original text. Partial/draft line at bottom with subtle typing animation. Thin waveform bar at top of transcript area for audio level feedback.
- **Right Panel (280px, collapsible):** Live translation mirror — same lines, translated text. Editable panel: user can copy translation. Toggle to hide translation column.
- **Footer:** Compact session info bar — model name, language pair, translation provider, session duration

### Micro-interactions
- New transcript line: slide-up + fade-in (200ms ease-out)
- Partial text: subtle pulse/typing cursor (not distracting)
- Waveform: smooth bar animation reflecting audio input level
- Start → columns expand with staggered reveal (left → center → right, 150ms each)
- Stop → transcript stays visible for review; 3-column collapses to 1-column "review mode" with export CTA

---

# SCREEN 2: SESSIONS — Folder-first History Browser

> **Concept:** Organize capture sessions by flat folders (not nested trees). Users create folders like "Work Meetings", "Language Study", "Podcast Transcriptions". A default "Global" folder catches uncategorized sessions.

## Layout (2 panels: folder browser + transcript viewer)

```
┌──────────────────────────────────────────────────────────────┐
│ [Sublingual]   Home | Sessions | Settings                   │
├──────────────────┬───────────────────────────────────────────┤
│                  │                                           │
│ FOLDERS          │  GLOBAL  >  Session: Jun 4, 2026 09:15   │
│                  │  Duration: 45m  ·  312 segments          │
│  ○ Global (17)   │                                           │
│  ○ Work (8)      │  09:15:02 │ John │ Good morning team     │
│  ○ Study (5)     │           │       │ Chào buổi sáng cả nhà │
│  ○ Podcasts (3)  │  09:15:08 │ Anna │ Let's start with the  │
│                  │           │       │ Q2 review...          │
│  [+ New Folder]  │           │       │ Bắt đầu với đánh giá  │
│                  │  09:15:22 │ John │ The numbers are in    │
│  SEARCH          │           │       │ Số liệu đã có rồi    │
│  [___________]   │  ...                                     │
│                  │                                           │
│  Select: All     │  [TXT] [JSON] [Open] [Move]  [🗑 Delete] │
│  5 selected      │                                           │
│  [Delete (5)]    │                                           │
│                  │                                           │
└──────────────────┴───────────────────────────────────────────┘
```

### Left Panel (260px): Folder Browser
- **Default "Global" folder** — always present, cannot be deleted, has a globe/badge icon
- **User-created folders** — each shows folder name + capture count badge
- **Active folder** — highlighted with accent left-border
- **[+ New Folder] button** — opens inline creation dialog (name input only, no path)
- **Delete folder** — available only for non-empty user folders (moves captures to Global first)
- **Search bar** — filters sessions within selected folder by transcript content
- **Selection bar** — Select All / Deselect All + Delete Selected (N) when items checked

### Right Panel (flex-1): Session List + Transcript Detail
- **TOP HALF: Session list** (for selected folder) — compact table/list rows, each showing:
  - Checkbox (for batch operations)
  - Session time (HH:MM)
  - Duration
  - Preview text (truncated first line)
  - Segment count badge
  - Click to select → bottom half shows full transcript
- **BOTTOM HALF: Transcript viewer** — scrollable, each line:
  - Timestamp (left, mono, small)
  - Speaker badge (optional, colored pill)
  - Original text (primary)
  - Translated text (below, muted, smaller)

### Toolbar (bottom of right panel)
- Export TXT button
- Export JSON button
- Open Folder button
- Move to Folder button (folder picker dropdown)
- Delete Session button (red, with confirmation dialog)

### Empty States
- No folders: "Create a folder to organize your sessions" + [Create] CTA
- No sessions in folder: "No sessions yet. Start capturing to see them here." + [Go to Home] CTA
- No search results: "No sessions match your search" + [Clear Search] CTA

### Folder CRUD Dialogs
- **Create:** Modal with name input, realtime validation (no special chars, no duplicates), [Create] / [Cancel]
- **Rename:** Modal pre-filled with current name, same validation, [Save] / [Cancel]
- **Delete (empty):** Confirm: "Delete folder 'X'? This cannot be undone." [Delete] / [Cancel]
- **Delete (non-empty):** Confirm: "Folder 'X' has N sessions. They will be moved to Global. Delete anyway?" [Move & Delete] / [Cancel]

---

# SCREEN 3: SETTINGS

> **Layout:** Tabbed sidebar navigation (left) + content area (right). Clean hierarchical layout matching shadcn/ui Settings pattern.

## Layout

```
┌──────────────────────────────────────────────────────────────┐
│ [Sublingual]   Home | Sessions | Settings                   │
├──────────┬───────────────────────────────────────────────────┤
│          │                                                   │
│  GENERAL │  General Settings                                 │
│          │                                                   │
│  SPEECH  │  ▸ Storage                                       │
│          │    Sessions folder    [/Users/.../sessions] [...] │
│  TRANS.  │    Models folder      [/Users/.../models]   [...] │
│          │                                                   │
│  OVERLAY │                                                   │
│          │                                                   │
│          │                                                   │
└──────────┴───────────────────────────────────────────────────┘
```

### Settings Tabs

#### Tab 1: General
- **Storage section:**
  - Sessions folder path (readonly input + Browse + Open buttons)
  - Speech models folder path (readonly input + Browse + Open buttons)
- **About section:**
  - App version, license info, links to docs

#### Tab 2: Speech
- **Model section:**
  - Active model dropdown (shows only downloaded models; empty state: "No models installed")
  - [Install Models] button → opens Model Download Dialog
  - [Open Models Folder] button
- **Recognition section:**
  - Source language dropdown (en, vi, ja, ko, zh, fr, de, es)
  - Max speakers selector (2-8, disabled with hint if speaker model not installed)
  - Flush timeout selector (500ms / 1s / 2s / 3s / 5s / 10s) — when to auto-finalize a partial sentence

#### Tab 3: Translation
- **Toggle:** Enable translation (switch)
- **Provider:** Google Translate / Local TranslateService (dropdown)
- **Target language:** dropdown (same 8 languages)
- **Provider-specific fields:**
  - Google: Endpoint URL input
  - Local: Base URL input
- **Test panel:**
  - Source text textarea
  - [Translate] button (with spinner during request)
  - Result display card with provider name + latency
  - Error message (red) on failure

#### Tab 4: Overlay
- **Appearance section:**
  - Theme: Dark / Light (dropdown)
  - Font size: slider (14-48px) with current value label
  - Line spacing: Compact / Default / Wide (segmented button group)
  - Background opacity: slider (30%-100%) with percentage label
  - Show translation: toggle switch
- **Size section:**
  - Width input (px) + Height input (px) — side by side
- **Position:** (future: auto-hide, display mode: bilingual/original only/translated only)
- **Live Preview panel:**
  - Rounded card with current theme/opacity/font/line-height showing sample bilingual text
  - Updates in real-time as sliders/selects change

#### Model Download Dialog (modal, triggered from Speech tab)
- Search/filter for models
- Model list with: name, size, estimated download time, accuracy rating (stars or label)
- [Download] button per model with progress bar
- Downloaded models show checkmark + "Installed" badge
- Close button, closes on backdrop click

---

# SCREEN 4: OVERLAY — Floating Subtitle Window

> **Separate Electron BrowserWindow** — borderless, transparent, always-on-top. Draggable, resizable. Shows live bilingual subtitles.

## Layout

```
┌────────────────────────────────────────────────────┐
│ Sublingual Overlay                        [✕]     │  ← Drag Handle (top bar)
├────────────────────────────────────────────────────┤
│                                                    │
│  ┌──────────────────────────────────────────────┐  │
│  │ [John] Good morning, everyone. Today I'd...  │  │
│  │ Chào buổi sáng mọi người. Hôm nay tôi muốn... │  │
│  └──────────────────────────────────────────────┘  │
│                                                    │
│  ┌──────────────────────────────────────────────┐  │
│  │ [Anna] Thanks for having me. Let's start...  │  │
│  │ Cảm ơn đã mời tôi. Hãy bắt đầu với...        │  │
│  └──────────────────────────────────────────────┘  │
│                                                    │
│  ┌─ partial ────────────────────────────────────┐  │
│  │ The numbers are looking really...            │  │
│  │ Số liệu đang trông rất...                    │  │
│  └──────────────────────────────────────────────┘  │
│                                                    │
│  ... (scrollable)                                  │
│                                               [↓] │  ← Jump to Bottom (conditional)
└────────────────────────────────────────────────────┘
```

### Design Details
- **Drag Handle:** 28px height, top bar with "Sublingual Overlay" label (left, tiny text) + close ✕ button (right). Entire bar is a drag region (WebkitAppRegion: drag). Close button zone is no-drag.
- **Transcript Lines:** each has:
  - Speaker badge (optional): colored pill with speaker name/ID, left-aligned inline
  - Original text: larger font (configurable, default 26px), primary text color, medium weight
  - Translated text: smaller font (original - 4px), muted color, below original, with 0.5 margin-top
  - Pending translation indicator: three pulsing dots "···" while waiting for translation
- **Partial (draft) line:** currently-being-spoken text, italic or slightly transparent to distinguish from finalized lines. Updates in real-time.
- **Auto-scroll:** scrolls to bottom as new content arrives. If user has scrolled up manually, a "↓" floating button appears at bottom-right to jump back.
- **Empty state:** "Waiting for speech..." centered, muted text.
- **Max lines:** 50 visible lines, older lines removed (circular buffer).

### Overlay Window Behavior (for dev context)
- Always on top (level: screen-saver in Electron)
- Click-through not yet implemented (future)
- Close button hides window (doesn't destroy)
- Position persists between show/hide
- Configurable from Settings: theme (Dark/Light), font size, line height, opacity, width, height, show/hide translation

---

# COMMON COMPONENT LIBRARY

All components follow shadcn/ui conventions. Must be designed for both light and dark themes.

| Component | Usage | Key States |
|-----------|-------|------------|
| **Button** | Primary (green), Secondary (ghost/outline), Destructive (red), Icon-only | Default, Hover, Active, Disabled, Loading (spinner) |
| **Card** | Content containers | Default, hoverable, selected (accent border) |
| **Select/Dropdown** | Model selection, language, provider, timeout | Default, Open, Disabled, Empty ("No items") |
| **Switch/Toggle** | Enable translation, show translation | On, Off, Disabled |
| **Slider** | Font size, opacity | Default, with value label |
| **Dialog/Modal** | Confirm delete, model download, folder CRUD | Open, with backdrop, responsive |
| **Badge** | Status indicator, folder counts, speaker labels | Default (gray), Accent (green), Destructive (red), with/without dot |
| **Input** | Search, URLs, folder names | Default, Focus, Error, Disabled, Readonly |
| **Textarea** | Translation test input | Default, Focus |
| **Checkbox** | Session selection, batch actions | Unchecked, Checked, Indeterminate |
| **Tooltip** | Icon button labels | On hover, short delay |
| **Progress** | Model download progress | Determinate (%), Indeterminate (spinning) |
| **Separator** | Visual division between sections | Horizontal, Vertical |
| **ScrollArea** | Transcript panels, session lists | With custom scrollbar styling |

---

# INTERACTION FLOWS

## Flow A: First-time User Experience
1. App opens → Home (empty state: "No Speech Model Installed")
2. User clicks "Go to Settings" → Settings > Speech tab
3. User clicks "Install Models" → Model Download Dialog opens
4. User downloads a model → progress bar → "Installed" checkmark
5. User selects downloaded model from dropdown
6. User goes back to Home → sees "Ready to Capture" with Start button enabled

## Flow B: Capture & Review Session
1. User selects audio source → Start button becomes clickable
2. User clicks Start → columns expand (left sidebar + transcript center + translation right)
3. Live transcript streams in center panel; translation appears in right panel
4. User clicks Stop → columns collapse; transcript stays visible in review mode
5. "Session saved" toast/notification appears with link: "View in Sessions"
6. Review mode shows export options (TXT, JSON)

## Flow C: Overlay Usage
1. During capture, user toggles overlay on → floating window appears
2. Overlay mirrors the live transcript + translation
3. User drags overlay to preferred screen position
4. User minimizes main window; overlay stays on top
5. User shares screen in meeting; overlay subtitles visible to audience
6. User closes overlay (or toggles off) → window hides

## Flow D: Session Management
1. User navigates to Sessions → sees folder browser + Global folder selected
2. User creates folder "Work Meetings" → folder appears in list
3. User selects sessions in Global → clicks "Move" → selects "Work Meetings"
4. Sessions appear under Work Meetings folder
5. User can export, delete, or review any session

---

# STATES COVERAGE

Every interactive surface must be designed for:

### Loading States
- Model download progress (determinate + indeterminate)
- Translation test request (spinner on button, result area empty)
- Session list loading (skeleton cards, 3-4 placeholder rows)
- Audio source enumeration (spinner in selector)

### Empty States
- No model installed (Home) — icon + message + CTA to Settings
- No sessions yet (Sessions) — icon + message + CTA to Home
- No sessions in folder — icon + folder-specific message
- No search results — "No matches" + clear search button
- No translation for line yet — "···" pulsing dots

### Error States
- Translation failure — inline error message with provider name
- Audio capture error — toast notification with guidance
- Model download failure — retry button, error details
- Missing model file — warning card with re-download CTA
- Platform permission needed (macOS Screen Recording) — guidance dialog

### Edge Cases
- Very long transcript text (truncation + tooltip)
- Rapid-fire partial updates (debounce rendering, max 60fps)
- Overlay window dragged to edge of screen (snap behavior?)
- Multiple rapid start/stop cycles (debounce, show "Please wait...")

---

# DELIVERABLE

1. **High-fidelity mockups** for all 4 screens in both Dark and Light themes:
   - Home (idle + capturing + review mode)
   - Sessions (folder browser + transcript view + empty states)
   - Settings (all 4 tabs with all fields)
   - Overlay (with transcript + translation + partial text)

2. **Complete component kit** — all 16 component types with all states

3. **Interaction flow diagrams** — 4 key flows (A, B, C, D above) showing page transitions and state changes

4. **Overlay design spec** — detailed treatment of the floating window including drag behavior, scroll, and bilingual text rendering

5. **Design tokens export** — color palette (light + dark), spacing scale, typography scale, shadow definitions, border-radius tokens

---

## APPENDIX: Vietnamese Language Context

The app was built by a Vietnamese developer. The default settings are Source: en → Target: vi. The translation service supports en↔vi as the primary pair. The README and translate service docs are in Vietnamese. Consider this when designing bilingual text rendering — Vietnamese uses Latin script with diacritics (ắ, ệ, ỏ, etc.), so font rendering must handle diacritic stacking correctly at all sizes.
