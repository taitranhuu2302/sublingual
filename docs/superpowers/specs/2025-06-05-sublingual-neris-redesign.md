# Sublingual NERIS UI Redesign — Design Spec

**Date:** 2025-06-05
**Status:** Approved

## Overview

Complete visual redesign of the Sublingual Electron app under the NERIS brand identity. Replaces the generic shadcn neutral theme with the NERIS Ocean color palette, restructures navigation to a macOS-style sidebar using shadcn's sidebar primitives, and transforms the HomePage into a transcript-first live caption experience.

---

## 1. CSS Token Layer — NERIS Ocean Palette

Map the NERIS Ocean colors to shadcn's CSS variable system. Replace the entire `:root`/`.dark` block in `src/index.css`.

### Color Mapping

| shadcn Token | NERIS Name | Hex | Usage |
|---|---|---|---|
| `--background` | Deep Ocean | `#001A33` | Window body background |
| `--foreground` | Near-white | `#E5EDF5` | Primary text |
| `--card` | Ocean Navy | `#003366` | Cards, panels |
| `--card-foreground` | Near-white | `#E5EDF5` | Text on cards |
| `--primary` | Aurora Blue | `#0066CC` | CTAs, active indicators |
| `--primary-foreground` | White | `#FFFFFF` | Text on primary |
| `--secondary` | Marine Blue | `#004080` | Secondary buttons, hover bg |
| `--secondary-foreground` | Near-white | `#D0DFF0` | Text on secondary |
| `--muted` | Ocean Navy 80% | `#002952` | Subtle surfaces |
| `--muted-foreground` | Horizon Blue faded | `#5A8ABF` | Dimmed/secondary text |
| `--accent` | Horizon Blue | `#0059B3` | Interactive hover states |
| `--accent-foreground` | White | `#FFFFFF` | Text on accent |
| `--border` | Marine Blue 40% | `#00408066` | Subtle borders/dividers |
| `--ring` | Aurora Blue | `#0066CC` | Focus rings |
| `--destructive` | Warm red | `#E05555` | Danger/destructive actions |
| `--destructive-foreground` | White | `#FFFFFF` | Text on destructive |

### Sidebar Tokens

| shadcn Token | NERIS Name | Hex | Usage |
|---|---|---|---|
| `--sidebar` | Ocean Navy | `#003366` | Sidebar background |
| `--sidebar-foreground` | Near-white | `#D0DFF0` | Sidebar text |
| `--sidebar-primary` | Aurora Blue | `#0066CC` | Active sidebar item |
| `--sidebar-primary-foreground` | White | `#FFFFFF` | Text on active item |
| `--sidebar-accent` | Marine Blue | `#004080` | Sidebar hover state |
| `--sidebar-accent-foreground` | Near-white | `#D0DFF0` | Text on hover |
| `--sidebar-border` | Marine Blue 30% | `#0040804D` | Sidebar separator |
| `--sidebar-ring` | Aurora Blue | `#0066CC` | Sidebar focus ring |

### Radii
- `--radius: 0.75rem` (12px, macOS-native corner radius)

### Typography
- **All UI:** Inter variable font (400–700 weights)
- **Fallback:** `-apple-system`, `BlinkMacSystemFont`, `Segoe UI`, sans-serif
- **Monospace (timestamps):** JetBrains Mono
- **Scale:** Tailwind defaults (12/14/16/18/20/24/32px)

### Shadows (custom tokens)
- `--shadow-sm`: 0 1px 4px hsla(0,0%,0%,0.08)
- `--shadow-md`: 0 2px 8px hsla(0,0%,0%,0.12)
- `--shadow-lg`: 0 4px 16px hsla(0,0%,0%,0.16)
- `--shadow-xl`: 0 8px 24px hsla(0,0%,0%,0.20)

### Dark-Only
No light mode. The app is dark-only per design principles. Remove `:root` light theme block entirely.

### Glassmorphism Tokens (optional accent)
- Glass bg: `hsla(210, 100%, 20%, 0.6)` (Ocean Navy at 60% opacity)
- Glass blur: `backdrop-blur-xl` (24px)
- Glass border: `hsla(0, 0%, 100%, 0.08)` (1px)
- Used on: overlay window, active state panels (sparingly)

---

## 2. Layout Structure — macOS-Style Sidebar

### Architecture
```
┌──────────────────────────────────────────────────────────┐
│ ┌──────────┐ ┌─────────────────────────────────────────┐ │
│ │ Sidebar  │ │  Title Bar (draggable, traffic lights)   │ │
│ │ 240px    │ ├─────────────────────────────────────────┤ │
│ │          │ │                                        │ │
│ │ Logo     │ │        <Outlet />                      │ │
│ │ ─────    │ │        (page content)                  │ │
│ │ ● Home   │ │                                        │ │
│ │ ● Sess.  │ │                                        │ │
│ │ ● Sett.  │ │                                        │ │
│ │          │ │                                        │ │
│ └──────────┘ └─────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
```

### Implementation
- Use shadcn `<SidebarProvider>`, `<Sidebar>`, `<SidebarContent>`, `<SidebarMenu>`, `<SidebarMenuItem>`, `<SidebarMenuButton>`
- Sidebar = `w-[240px] shrink-0`, `--sidebar` background
- Move routing structure so sidebar persists across all routes
- Wrap `<Layout>` with `<SidebarProvider>` in `App.tsx`

### Sidebar Content
1. **Brand header:** NERIS/Sublingual logo + "Sublingual" name
2. **Separator**
3. **Nav items:**
   - Home (`Layout` icon) → `/`
   - Sessions (`Archive` icon) → `/sessions`
   - Settings (`Settings` icon) → `/settings`
4. Active item: `--sidebar-primary` background + subtle left border accent
5. Hover item: `--sidebar-accent` background
6. **Footer:** Version/app info or status indicator

### Layout.tsx Changes
- Remove horizontal `<nav>` bar
- Add `<Sidebar>` component as the left panel
- Main content area = `<SidebarInset>` containing `<main>` + `<Outlet />`
- Title bar area at top of content (draggable region for macOS traffic lights)

---

## 3. HomePage — Transcript-First

### Idle State
- Center-aligned with large icon + "No Speech Model Installed" message
- "Go to Settings" / "Download Model" CTA button
- Source selector shown as compact dropdown

### Active State (capturing)
```
┌─────────────────────────────────────────────────────────┐
│ [Source: System Audio ▼]    ● REC 00:03:24    [Stop]    │
│ ─────────────────────────────────────────────────────── │
│                                                         │
│  00:01:12  This is the first recognized sentence.        │
│            Đây là câu đầu tiên được nhận diện.           │
│                                                         │
│  00:01:18  And this is the second one.                   │
│            Và đây là câu thứ hai.                        │
│                                                         │
│  00:01:25  Now we're getting a stream of... (draft)     │
│                                                         │
│ ─────────────────────────────────────────────────────── │
│  Model: vosk-small-en  │  15 segments  │  42 words      │
│  en → vi              │                                 │
└─────────────────────────────────────────────────────────┘
```

### Components
- **CaptureToolbar (compact):** ~48px height. Source selector left, record indicator + timer center, Stop button right. Overlay toggle + Clear as icon buttons.
- **Transcript feed:** Flex-1 scrollable container. Each segment = timestamp (JetBrains Mono, `text-xs`, muted) + original text + translation (muted, `text-sm`). Partial/draft lines in italic + opacity-70. Auto-scroll via `useEffect` + `scrollIntoView({ behavior: 'smooth' })`.
- **Stats Footer:** Slim bar (~32px). Shows model name, segment count, word count, language pair.
- **Empty transcript:** Centered "Listening..." message.

### Behavior
- Existing hooks (`useAudioCapture`, `useTranscription`, `useSettings`) unchanged.
- Start/stop/clear logic from current `HomePage` preserved.
- Overlay toggle preserved.

---

## 4. SessionsPage — 3-Column Layout

```
┌──────────┬────────────────────┬─────────────────────────────┐
│ Sidebar  │  All Sessions      │  Zoom Meeting                │
│ (nav)    │  ────────────────  │  Mar 14, 2025 · 14:32       │
│ 240px    │  • Work (5)        │  00:42:18 · vosk-en-us-0.22 │
│          │  • Study (3)       │  EN → VI · 128 lines         │
│          │  • Podcasts (2)    │                              │
│          │  • Global (2)      │  [Filter transcript...]      │
│          │                    │  ───────────────────────     │
│          │  RECENT            │                              │
│          │  • Mar 14 - Zoom   │  00:00:04  Good morning...  │
│          │    EN→VI  00:42:18 │           Chào buổi sáng... │
│          │  • Mar 13 - YouTube│                              │
│          │    EN→VI  01:05:44 │  00:00:11  Let's start...   │
│          │  • Mar 12 - Spotify│           Hãy bắt đầu...    │
│          │    JA→VI  00:58:02 │                              │
│          │  • Mar 10 - Teams  │                              │
│          │    EN→VI  00:27:31 │                              │
│          │                    │                              │
└──────────┴────────────────────┴─────────────────────────────┘
```

### Column 1 (240px) — Global Sidebar
- Shared sidebar nav (Home/Sessions/Settings) — same as Layout
- Sessions item highlighted as active

### Column 2 (~280px) — Sessions Browser
- **Header:** "All Sessions" title
- **Folder groups:** Work, Study, Podcasts, Global (count badges)
  - Each folder is a collapsible section or button
  - Active folder highlighted
- **Recent sessions list:**
  - Date, title, language pair, duration
  - Click to view transcript
  - Right-click or checkmark for multi-select
- **Footer actions:** Select All / Delete Selected

### Column 3 (flex-1) — Transcript Detail
- **Header:** Session title, date/time, duration, model, line count
- **Filter input:** "Filter transcript by keyword" — filters lines by text content
- **Transcript lines:** Each line = timestamp (JetBrains Mono, muted) + original + translation
- **Action bar below:**
  - Export TXT (`FileText`), Export JSON (`FileJson`), Open Folder (`FolderOpen`)
  - Delete (`Trash2`) with confirm dialog
- **Empty state:** Centered "Select a session to view its transcript"

### Integration
- Existing `useSessions` hook preserved
- `groupByDate` helper preserved
- Delete confirmation via existing `ConfirmDialog` (restyled)
- Multi-select logic intact

---

## 5. SettingsPage

### Structure
```
┌──────────┬──────────────┬──────────────────────────────────┐
│ Sidebar  │  General     │  General Settings                 │
│ (nav)    │  Speech      │  ────────────────────             │
│ 240px    │  Translation │                                   │
│          │  Overlay     │  Storage Path                     │
│          │              │  [input field........] [Browse]   │
│          │              │                                   │
│          │              │  Models Path                      │
│          │              │  [input field........] [Browse]   │
│          │              │                                   │
│          │              │  [Open Settings Folder]           │
└──────────┴──────────────┴──────────────────────────────────┘
```

### Column 1 (240px) — Global Sidebar
- Shared nav, Settings item highlighted

### Column 2 (160px) — Sub-tab Navigation
- Vertical tab list: General, Speech, Translation, Overlay
- Icon + label per tab
- Active tab highlighted with `--sidebar-accent` bg + left border accent

### Column 3 (flex-1) — Settings Content
- Scrollable content area
- Max-width container (~640px) for readability
- Each sub-tab renders existing settings panel component:
  - `GeneralSettings` — storage paths with folder picker
  - `SpeechSettings` — model selection, language, timeout
  - `TranslationSettings` — enable/disable, provider, language, test tool
  - `OverlaySettings` — theme, font size, opacity, dimensions
- Form elements restyled with NERIS colors:
  - Input bg = `#001A33` (Deep Ocean)
  - Border = `--border` (Marine Blue 40%)
  - Focus ring = `--ring` (Aurora Blue)
  - Labels = `--foreground`
  - Helper text = `--muted-foreground`

### Integration
- Existing `useSettings` hook + settings panel components preserved
- Sub-tab state management unchanged (local state in `SettingsPage`)

---

## 6. Overlay Window

### Window Properties
- Separate Electron `BrowserWindow`
- Always-on-top, borderless (`frame: false`), transparent (`transparent: true`)
- Dimensions from settings (default 720×200)
- Draggable anywhere on screen (`-webkit-app-region: drag`)

### Visual (Glassmorphism)
- `background: hsla(0, 0%, 0%, var(--overlay-opacity))` — opacity from settings (0.5–1.0)
- `backdrop-filter: blur(24px)` — frosted glass effect
- `border: 1px solid hsla(0, 0%, 100%, 0.08)` — frosted edge highlight
- `border-radius: 12px` (macOS-native)
- `box-shadow: --shadow-lg` — depth

### Content
- Each segment: original text (white, configurable font size 16–40px) + translated text below (smaller, `--neris-horizon-blue` tint)
- Translation line hidable via settings
- Auto-scroll to latest line
- Monospace for timestamps, Inter for transcript text (both configurable)

### Settings Controls (in SettingsPage > Overlay)
- Font size: slider 16–40px
- Line height: slider 1.0–2.0
- Opacity: slider 0.5–1.0
- Width/Height: number inputs
- Show translation: toggle
- Dark theme only — remove Light option

### Integration
- Existing `overlay-window.ts` (main process) unchanged
- `overlay-renderer.tsx` / `OverlayApp.tsx` updated to reflect new theme and glassmorphism
- `index.html` splash removed (or toned down to dark mode)

---

## 7. shadcn Component Customization

All shadcn UI components get the NERIS treatment purely through CSS variables (defined in `index.css`). No component source code changes needed — the token mapping handles:

- `button`: `--primary` = Aurora Blue for default variant, `--secondary` = Marine Blue for secondary, `--destructive` = Warm Red
- `input` / `select` / `textarea`: bg = Deep Ocean, border = Marine Blue 40%, focus ring = Aurora Blue
- `card`: bg = Ocean Navy, text = Near-white
- `dialog` / `popover`: bg = Ocean Navy, shadow = `--shadow-lg`
- `badge`: bg = Marine Blue for secondary, Aurora Blue for default
- `separator`: color = `--border`
- `switch`: track = Marine Blue, checked = Aurora Blue
- `slider`: track = Marine Blue, range = Aurora Blue
- `scrollbar`: thin overlay scrollbar, track = transparent, thumb = Marine Blue 40%
- `tooltip`: bg = Ocean Navy, shadow = `--shadow-xl`

### Font
- Inter variable loaded via `@import` in `index.css`
- Set as `font-family` on `body`

---

## 8. Files to Change

| File | Change |
|---|---|
| `src/index.css` | Complete rewrite — NERIS token layer, dark-only, Inter font |
| `src/App.tsx` | Wrap with `<SidebarProvider>`, potentially restructure routes |
| `src/components/Layout.tsx` | Replace horizontal nav with shadcn Sidebar |
| `src/pages/HomePage.tsx` | Transcript-first layout, compact toolbar |
| `src/components/CaptureToolbar.tsx` | Redesign as compact capture bar |
| `src/pages/SessionsPage.tsx` | 3-column layout with folders + session list + detail |
| `src/pages/SettingsPage.tsx` | Update layout to use global sidebar |
| `src/components/settings/*.tsx` | Restyle forms with NERIS colors |
| `src/index.html` | Update splash/loading to dark mode |
| `src/overlay/overlay.css` | Glassmorphism + NERIS Dark theme |
| `src/overlay/OverlayApp.tsx` | Update to reflect new visual style |
| `src/overlay/overlay-renderer.tsx` | May need style updates |
| shadcn UI components | No file changes — tokens handle all theming |

### Files NOT Changed
| File | Reason |
|---|---|
| `src/main/**` | Main process, IPC handlers — untouched |
| `src/hooks/**` | Business logic hooks — untouched |
| `src/lib/utils.ts` | `cn()` utility — untouched |
| `src/types/**` | Type definitions — untouched |
| `package.json` | No dependency changes |
| `tailwind.config.*` | Using Tailwind v4 (no config file needed) |

---

## 9. States Coverage

Every view handles:

| State | Implementation |
|---|---|
| **Loading** | Skeleton placeholders or inline spinners |
| **Empty** | Center-aligned icon + message + CTA |
| **Active** | Live transcript streaming, animated indicators |
| **Error** | Toast notification or inline error message |
| **Disabled** | Grayed-out controls with tooltip explanation |
| **Confirmation** | `ConfirmDialog` for destructive actions |

---

## 10. Design Principles Checklist

- [x] Dark-only
- [x] macOS-first visual language (12px radii, minimal chrome, shadows for depth)
- [x] Sidebar navigation (macOS-style)
- [x] Transcript-first HomePage
- [x] Progressive disclosure (idle = clean, active = information-rich)
- [x] Bilingual display throughout (original + translation)
- [x] Consistent layout across all 3 pages
- [x] Glassmorphism reserved for overlay window + active panels
- [x] Obvious affordances (tooltips on icon buttons, confirm on delete)
- [x] Space-efficient multi-column where appropriate
