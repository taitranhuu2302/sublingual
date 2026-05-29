# React Router Integration

## Goal
Add client-side routing with React Router to the Electron + Vite + React + shadcn app, with Home and Settings pages.

## Architecture
- **Router:** `HashRouter` from `react-router-dom` (required for Electron — file:// in production doesn't support History API)
- **Entry:** `src/renderer.ts` → `src/App.tsx` (replaces `src/app.tsx`)
- **Layout:** `src/components/Layout.tsx` — persistent navbar with `<Outlet>` for page content
- **Pages:** `src/pages/HomePage.tsx`, `src/pages/SettingsPage.tsx`

## Components

### App.tsx
- `HashRouter` → `<Routes>` → `<Route element={<Layout />}>` with nested `/` and `/settings` routes

### Layout.tsx
- `<nav>` with `flex gap-1`, `bg-background`, `border-b`
- Two `Button variant="ghost"` nav links using lucide `Home` and `Settings` icons
- `<main>` with `<Outlet />` for page content

### HomePage.tsx
- Demo landing: welcome heading, description paragraph

### SettingsPage.tsx
- Demo settings: heading, placeholder section with a shadcn `Button`

### renderer.ts
- Change import from `./app` to `./App`

## Dependencies
- Add `react-router-dom` to `package.json`

## Non-goals
- No authentication, nested routes, or dynamic routing
- No data fetching patterns
