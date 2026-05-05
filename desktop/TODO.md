# UI Migration TODO

## Source Scan (`stitch/`)

- [x] `stitch/history.html` scanned
- [x] `stitch/overlay.html` scanned
- [x] `stitch/settings.html` scanned
- [x] `stitch/dashboard.html` currently missing in folder; use `history.html`/`overlay.html` structure as source-equivalent dashboard shell

## Pages To Build (React)

- [x] `DashboardPage` (`/dashboard`)
- [x] `HistoryPage` (`/history`)
- [x] `OverlayPage` (`/captions`)
- [x] `SettingsPage` (`/settings`)

## Shared Components (Reusable Pattern)

- [x] `AppShell` (sidebar + top bar + content canvas)
- [x] `SideNav`
- [x] `SectionCard`
- [x] `Dashboard` telemetry card section
- [x] `Dashboard` live monitor card section
- [x] `History` session list section
- [x] `Captions` preview and controls sections
- [x] Shared `PageHeader` component
- [x] Shared `StatusPill` component
- [x] Shared `MetricBars` component
- [x] Shared `SectionCard` wrapper component

## Routing / App Wiring

- [x] Router base redirect (`/` -> `/dashboard`)
- [x] Nav links wired to routes
- [x] All stitch routes implemented with real pages
- [x] Real settings route wired
- [x] 404 route

## State / Data Follow-ups

- [x] Move dashboard form values into zustand store
- [x] Add mock session data model for history
- [x] Add mock hotkey and engine config model for settings
