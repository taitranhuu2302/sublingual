# Sublingual NERIS UI Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the generic shadcn neutral theme with the NERIS Ocean dark palette, restructure to macOS-style sidebar navigation, transform HomePage into transcript-first layout, and upgrade SessionsPage to 3-column with folder groups.

**Architecture:** CSS-only theme via shadcn token remapping in `index.css` (no shadcn component file changes). New sidebar uses shadcn's `<Sidebar>` primitives. Business logic (hooks, main process) remains untouched. Each page gets a layout refactor only.

**Tech Stack:** React 19, shadcn/ui 4.x, Tailwind CSS 4, Lucide React, react-router-dom v7

---

### Task 0: Install shadcn Sidebar Component

**Files:**
- Create: `src/components/ui/sidebar.tsx`

- [ ] **Step 1: Add shadcn sidebar component**

```bash
npx shadcn add sidebar -y
```
Run from `desktop/` directory. Expected: Creates `src/components/ui/sidebar.tsx` and any needed dependencies.

- [ ] **Step 2: Verify sidebar file exists and compiles**

```bash
npx tsc --noEmit src/components/ui/sidebar.tsx 2>&1 | head -20
```
Expected: No errors (may have existing project errors — only confirm sidebar-specific errors are absent).

---

### Task 1: Rewrite CSS Token Layer — NERIS Ocean Palette

**Files:**
- Modify: `desktop/src/index.css` — complete replacement

- [ ] **Step 1: Replace `src/index.css` with NERIS Ocean dark theme**

```css
@import "tailwindcss";
@import "tw-animate-css";
@import "shadcn/tailwind.css";

@custom-variant dark (&:is(.dark *));

@theme inline {
  --color-background: var(--background);
  --color-foreground: var(--foreground);
  --color-card: var(--card);
  --color-card-foreground: var(--card-foreground);
  --color-popover: var(--popover);
  --color-popover-foreground: var(--popover-foreground);
  --color-primary: var(--primary);
  --color-primary-foreground: var(--primary-foreground);
  --color-secondary: var(--secondary);
  --color-secondary-foreground: var(--secondary-foreground);
  --color-muted: var(--muted);
  --color-muted-foreground: var(--muted-foreground);
  --color-accent: var(--accent);
  --color-accent-foreground: var(--accent-foreground);
  --color-destructive: var(--destructive);
  --color-destructive-foreground: var(--destructive-foreground);
  --color-border: var(--border);
  --color-input: var(--input);
  --color-ring: var(--ring);
  --color-chart-1: var(--chart-1);
  --color-chart-2: var(--chart-2);
  --color-chart-3: var(--chart-3);
  --color-chart-4: var(--chart-4);
  --color-chart-5: var(--chart-5);
  --radius-sm: calc(var(--radius) * 0.6);
  --radius-md: calc(var(--radius) * 0.8);
  --radius-lg: var(--radius);
  --radius-xl: calc(var(--radius) * 1.4);
  --radius-2xl: calc(var(--radius) * 1.8);
  --radius-3xl: calc(var(--radius) * 2.2);
  --radius-4xl: calc(var(--radius) * 2.6);
  --color-sidebar: var(--sidebar);
  --color-sidebar-foreground: var(--sidebar-foreground);
  --color-sidebar-primary: var(--sidebar-primary);
  --color-sidebar-primary-foreground: var(--sidebar-primary-foreground);
  --color-sidebar-accent: var(--sidebar-accent);
  --color-sidebar-accent-foreground: var(--sidebar-accent-foreground);
  --color-sidebar-border: var(--sidebar-border);
  --color-sidebar-ring: var(--sidebar-ring);
}

:root {
  --radius: 0.75rem;
  /* Deep Ocean — darkest bg */
  --background: oklch(0.121 0.045 251.86);
  /* Near-white text */
  --foreground: oklch(0.91 0.015 243.93);
  /* Ocean Navy — cards/panels */
  --card: oklch(0.185 0.055 251.86);
  --card-foreground: oklch(0.91 0.015 243.93);
  /* Ocean Navy — popovers */
  --popover: oklch(0.185 0.055 251.86);
  --popover-foreground: oklch(0.91 0.015 243.93);
  /* Aurora Blue — CTAs */
  --primary: oklch(0.52 0.19 251.86);
  --primary-foreground: oklch(1 0 0);
  /* Marine Blue — secondary */
  --secondary: oklch(0.25 0.07 251.86);
  --secondary-foreground: oklch(0.88 0.02 243.93);
  /* Ocean Navy 80% — muted */
  --muted: oklch(0.165 0.05 251.86);
  --muted-foreground: oklch(0.55 0.06 251.86);
  /* Horizon Blue — accent/hover */
  --accent: oklch(0.32 0.12 251.86);
  --accent-foreground: oklch(1 0 0);
  /* Warm red — destructive */
  --destructive: oklch(0.58 0.19 19.29);
  --destructive-foreground: oklch(1 0 0);
  /* Marine Blue 40% — borders */
  --border: oklch(0.25 0.07 251.86 / 0.4);
  --input: oklch(0.25 0.07 251.86 / 0.5);
  --ring: oklch(0.52 0.19 251.86);
  /* Chart colors (ocean variants) */
  --chart-1: oklch(0.52 0.19 251.86);
  --chart-2: oklch(0.62 0.15 210);
  --chart-3: oklch(0.55 0.12 230);
  --chart-4: oklch(0.48 0.16 220);
  --chart-5: oklch(0.45 0.18 240);
  /* Sidebar — Ocean Navy */
  --sidebar: oklch(0.185 0.055 251.86);
  --sidebar-foreground: oklch(0.88 0.02 243.93);
  --sidebar-primary: oklch(0.52 0.19 251.86);
  --sidebar-primary-foreground: oklch(1 0 0);
  --sidebar-accent: oklch(0.25 0.07 251.86);
  --sidebar-accent-foreground: oklch(0.88 0.02 243.93);
  --sidebar-border: oklch(0.25 0.07 251.86 / 0.3);
  --sidebar-ring: oklch(0.52 0.19 251.86);
}

@layer base {
  * {
    @apply border-border outline-ring/50;
  }
  body {
    @apply bg-background text-foreground antialiased;
    font-family: "Inter", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    font-feature-settings: "cv02", "cv03", "cv04", "cv11";
  }
}
```

Delete the `.dark { }` block entirely — `:root` IS the dark theme. No light mode.

- [ ] **Step 2: Verify CSS is syntactically valid**

No lint command for CSS. Review manually — ensure all closing braces are present and `@layer` syntax is correct.

---

### Task 2: Rewrite Layout.tsx with macOS-Style Sidebar

**Files:**
- Modify: `desktop/src/components/Layout.tsx`
- Modify: `desktop/src/App.tsx`

- [ ] **Step 1: Rewrite Layout.tsx with shadcn Sidebar**

Replace the entire contents of `src/components/Layout.tsx` with:

```tsx
import { Outlet, useLocation, NavLink } from "react-router-dom";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarMenu,
  SidebarMenuItem,
  SidebarMenuButton,
  SidebarProvider,
  SidebarInset,
} from "@/components/ui/sidebar";
import { Home, Archive, Settings, Waves } from "lucide-react";

const navItems = [
  { to: "/", label: "Home", icon: Home },
  { to: "/sessions", label: "Sessions", icon: Archive },
  { to: "/settings", label: "Settings", icon: Settings },
];

function AppSidebar() {
  const location = useLocation();

  return (
    <Sidebar collapsible="none" className="border-r border-sidebar-border">
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <div className="flex items-center gap-2 px-2 py-3 mb-2">
              <Waves className="h-6 w-6 text-sidebar-primary" />
              <span className="text-base font-semibold text-sidebar-foreground">
                Sublingual
              </span>
            </div>
            <SidebarMenu>
              {navItems.map(({ to, label, icon: Icon }) => {
                const isActive = location.pathname === to || (to !== "/" && location.pathname.startsWith(to));
                return (
                  <SidebarMenuItem key={to}>
                    <SidebarMenuButton asChild isActive={isActive}>
                      <NavLink to={to}>
                        <Icon className="h-4 w-4" />
                        <span>{label}</span>
                      </NavLink>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                );
              })}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
      <SidebarFooter>
        <p className="text-[11px] text-sidebar-foreground/40 px-2 py-1">
          NERIS &middot; Sublingual
        </p>
      </SidebarFooter>
    </Sidebar>
  );
}

export function Layout() {
  return (
    <SidebarProvider>
      <div className="flex h-screen w-screen overflow-hidden">
        <AppSidebar />
        <SidebarInset className="flex flex-col min-h-0">
          <main className="flex-1 overflow-y-auto flex flex-col min-h-0">
            <Outlet />
          </main>
        </SidebarInset>
      </div>
    </SidebarProvider>
  );
}
```

- [ ] **Step 2: Update App.tsx — ensure SidebarProvider wraps correctly**

Update `src/App.tsx`:

```tsx
import { BrowserRouter, Routes, Route } from "react-router-dom";
import { Layout } from "./components/Layout";
import { HomePage } from "./pages/HomePage";
import { SettingsPage } from "./pages/SettingsPage";
import { SessionsPage } from "./pages/SessionsPage";

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/sessions" element={<SessionsPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
```

The `Layout` component now contains `<SidebarProvider>` internally, so App.tsx no longer needs to wrap it.

- [ ] **Step 3: Verify TypeScript compilation**

```bash
npx tsc --noEmit 2>&1 | head -30
```

Expected: No new errors from Layout.tsx or App.tsx.

---

### Task 3: Redesign CaptureToolbar and HomePage — Transcript-First

**Files:**
- Modify: `desktop/src/components/CaptureToolbar.tsx`
- Modify: `desktop/src/pages/HomePage.tsx`

- [ ] **Step 1: Rewrite CaptureToolbar.tsx — compact variant**

Replace `src/components/CaptureToolbar.tsx` with:

```tsx
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { AudioSourceSelector } from "./AudioSourceSelector";
import { Mic, MicOff, Trash2, Monitor, Circle } from "lucide-react";
import type { AudioSource } from "@/types/electron-api";

interface CaptureToolbarProps {
  sources: AudioSource[];
  selectedSource: string;
  capturing: boolean;
  starting: boolean;
  hasModel: boolean;
  overlayVisible: boolean;
  onSourceChange: (id: string) => void;
  onStart: () => void;
  onStop: () => void;
  onClear: () => void;
  onToggleOverlay: () => void;
}

export function CaptureToolbar({
  sources,
  selectedSource,
  capturing,
  starting,
  hasModel,
  overlayVisible,
  onSourceChange,
  onStart,
  onStop,
  onClear,
  onToggleOverlay,
}: CaptureToolbarProps) {
  const canStart = selectedSource && hasModel && !capturing && !starting;

  return (
    <TooltipProvider>
      <div className="flex items-center gap-2 px-4 py-2 border-b border-border/50 bg-card/50">
        <AudioSourceSelector
          sources={sources}
          value={selectedSource}
          onChange={onSourceChange}
          disabled={capturing}
        />

        <div className="w-px h-5 bg-border/50" />

        {starting ? (
          <Button disabled className="bg-primary/70 text-primary-foreground min-w-[100px]">
            <svg className="animate-spin h-4 w-4 mr-2" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            Starting...
          </Button>
        ) : !capturing ? (
          <Button onClick={onStart} disabled={!canStart} className="bg-primary hover:bg-primary/90 text-primary-foreground min-w-[90px]">
            <Mic className="h-4 w-4 mr-2" />
            Start
          </Button>
        ) : (
          <Button variant="destructive" onClick={onStop} className="min-w-[90px]">
            <MicOff className="h-4 w-4 mr-2" />
            Stop
          </Button>
        )}

        <Tooltip>
          <TooltipTrigger asChild>
            <Button variant="ghost" size="icon" onClick={onClear} className="h-8 w-8">
              <Trash2 className="h-4 w-4" />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Clear transcript</TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant={overlayVisible ? "secondary" : "ghost"}
              size="icon"
              onClick={onToggleOverlay}
              className="h-8 w-8"
            >
              <Monitor className="h-4 w-4" />
            </Button>
          </TooltipTrigger>
          <TooltipContent>{overlayVisible ? "Hide overlay" : "Show overlay"}</TooltipContent>
        </Tooltip>

        <div className="ml-auto flex items-center gap-2">
          {capturing && (
            <span className="flex items-center gap-1.5 text-xs font-medium">
              <Circle className="h-2 w-2 fill-red-500 text-red-500 animate-pulse" />
              <span className="text-red-400">REC</span>
            </span>
          )}
          <Badge variant={capturing ? "default" : "secondary"} className={capturing ? "animate-pulse" : ""}>
            {capturing ? "Live" : hasModel ? "Ready" : "No model"}
          </Badge>
        </div>
      </div>
    </TooltipProvider>
  );
}
```

- [ ] **Step 2: Rewrite HomePage.tsx — transcript-first**

Replace `src/pages/HomePage.tsx` with:

```tsx
import { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { CaptureToolbar } from "@/components/CaptureToolbar";
import { useAudioCapture } from "@/hooks/use-audio-capture";
import { useTranscription } from "@/hooks/use-transcription";
import { useSettings } from "@/hooks/use-settings";
import { Mic, Settings, Languages, ScrollText, Clock } from "lucide-react";

function formatTime(s: number) {
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;
  const mm = m.toString().padStart(2, "0");
  const ss = sec.toString().padStart(2, "0");
  return h > 0 ? `${h}:${mm}:${ss}` : `${mm}:${ss}`;
}

function formatTimestamp(ts: number) {
  const d = new Date(ts);
  return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

export function HomePage() {
  const navigate = useNavigate();
  const { sources, capturing, start, stop } = useAudioCapture();
  const { segments, running, loading, start: startASR, stop: stopASR, clear: clearSegments } = useTranscription();
  const { settings } = useSettings();
  const [selectedSource, setSelectedSource] = useState("");
  const [overlayVisible, setOverlayVisible] = useState(false);
  const [starting, setStarting] = useState(false);
  const [elapsed, setElapsed] = useState(0);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!selectedSource && sources.length > 0) {
      setSelectedSource(sources[0].id);
    }
  }, [sources, selectedSource]);

  useEffect(() => {
    if (capturing && running) {
      setElapsed(0);
      timerRef.current = setInterval(() => setElapsed((p) => p + 1), 1000);
    } else {
      if (timerRef.current) clearInterval(timerRef.current);
    }
    return () => { if (timerRef.current) clearInterval(timerRef.current); };
  }, [capturing, running]);

  const handleStart = async () => {
    if (!selectedSource || starting || loading) return;
    setStarting(true);
    try {
      await start(selectedSource);
      await startASR();
      await window.electronAPI.overlay.show();
      setOverlayVisible(true);
    } catch (err) {
      console.error("Failed to start:", err);
      await stop();
    } finally {
      setStarting(false);
    }
  };

  const handleStop = async () => {
    await stopASR();
    await stop();
    setOverlayVisible(false);
  };

  const handleClear = useCallback(() => {
    clearSegments();
  }, [clearSegments]);

  const handleToggleOverlay = async () => {
    await window.electronAPI.overlay.toggle();
    const visible = await window.electronAPI.overlay.isVisible();
    setOverlayVisible(visible);
  };

  // Auto-scroll to latest
  const finals = segments.filter((s) => s.isFinal);
  const partials = segments.filter((s) => !s.isFinal);
  const hasModel = !!settings.speechToText.selectedModel;
  const modelName = settings.speechToText.selectedModel
    ? settings.speechToText.selectedModel.replace(/^vosk-model-/, "").replace(/-/g, " ")
    : "None";

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [segments]);

  const isActive = capturing && running;
  const isEmpty = finals.length === 0 && partials.length === 0;

  return (
    <div className="flex flex-col flex-1 min-h-0">
      <CaptureToolbar
        sources={sources}
        selectedSource={selectedSource}
        capturing={capturing}
        starting={starting || loading}
        hasModel={hasModel}
        overlayVisible={overlayVisible}
        onSourceChange={setSelectedSource}
        onStart={handleStart}
        onStop={handleStop}
        onClear={handleClear}
        onToggleOverlay={handleToggleOverlay}
      />

      <div className="flex-1 flex flex-col min-h-0">
        {!hasModel ? (
          <div className="flex-1 flex flex-col items-center justify-center p-6">
            <Card className="w-full max-w-md border-border/50">
              <CardContent className="flex flex-col items-center py-10 gap-4">
                <div className="h-16 w-16 rounded-2xl bg-muted flex items-center justify-center">
                  <Mic className="h-8 w-8 text-muted-foreground" />
                </div>
                <div className="text-center">
                  <h2 className="text-lg font-semibold">No Speech Model Installed</h2>
                  <p className="text-sm text-muted-foreground mt-1">
                    Install a speech recognition model to start transcribing.
                  </p>
                </div>
                <Button onClick={() => navigate("/settings")}>
                  <Settings className="h-4 w-4 mr-2" />
                  Go to Settings
                </Button>
              </CardContent>
            </Card>
          </div>
        ) : (
          <>
            {/* Transcript Feed */}
            <div ref={scrollRef} className="flex-1 overflow-y-auto min-h-0 px-6 py-4">
              {isEmpty && !isActive && (
                <div className="flex flex-col items-center justify-center h-full text-muted-foreground gap-3">
                  <ScrollText className="h-12 w-12 opacity-30" />
                  <p className="text-sm">Ready to capture. Select a source and press Start.</p>
                </div>
              )}
              {isEmpty && isActive && (
                <div className="flex flex-col items-center justify-center h-full text-muted-foreground gap-3">
                  <div className="flex items-center gap-2">
                    <span className="h-2 w-2 rounded-full bg-primary animate-pulse" />
                    <span>Listening...</span>
                  </div>
                </div>
              )}
              <div className="space-y-1 max-w-4xl mx-auto">
                {finals.map((seg) => (
                  <div key={seg.id} className="flex gap-4 py-2 border-b border-border/20 group">
                    <span className="text-xs text-muted-foreground font-mono shrink-0 pt-0.5 w-20 text-right">
                      {formatTimestamp(seg.timestamp)}
                    </span>
                    <div className="flex-1 min-w-0">
                      <p className="text-base leading-relaxed">
                        {"speakerLabel" in seg && seg.speakerLabel && (
                          <span
                            className="inline-flex items-center gap-1 mr-2 text-[11px] font-semibold rounded px-1.5 py-0.5 align-middle"
                            style={{
                              backgroundColor: `${(seg as any).speakerColor}22`,
                              color: (seg as any).speakerColor,
                              border: `1px solid ${(seg as any).speakerColor}44`,
                            }}
                          >
                            {(seg as any).speakerLabel}
                          </span>
                        )}
                        {seg.text}
                      </p>
                      {"translatedText" in seg && (seg as any).translatedText && (
                        <p className="text-sm text-muted-foreground mt-0.5 leading-relaxed">
                          {(seg as any).translatedText}
                        </p>
                      )}
                    </div>
                  </div>
                ))}
                {partials.map((seg) => (
                  <div key={seg.id} className="flex gap-4 py-2 border-b border-border/10">
                    <span className="text-xs text-muted-foreground font-mono shrink-0 pt-0.5 w-20 text-right opacity-50">
                      {formatTimestamp(seg.timestamp)}
                    </span>
                    <div className="flex-1 min-w-0">
                      <p className="text-base leading-relaxed italic opacity-70">{seg.text}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Stats Footer */}
            <div className="flex items-center gap-4 px-4 py-1.5 border-t border-border/30 bg-card/30 text-xs text-muted-foreground shrink-0">
              <span className="flex items-center gap-1">
                <Clock className="h-3 w-3" />
                {isActive ? (
                  <>
                    <span className="h-1.5 w-1.5 rounded-full bg-red-400 animate-pulse mr-1" />
                    {formatTime(elapsed)}
                  </>
                ) : (
                  formatTime(elapsed)
                )}
              </span>
              <span className="flex items-center gap-1">
                <ScrollText className="h-3 w-3" />
                {finals.length} segments
              </span>
              <span className="flex items-center gap-1">
                <Mic className="h-3 w-3" />
                {modelName}
              </span>
              {settings.translation.enabled && (
                <span className="flex items-center gap-1">
                  <Languages className="h-3 w-3" />
                  {settings.speechToText.sourceLanguage} → {settings.translation.targetLanguage}
                </span>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
```

This replaces the dashboard-centric idle state with a transcript-first layout. The toolbar is compact, the main area is a scrollable transcript feed, and a slim stats footer sits at the bottom.

- [ ] **Step 3: Verify TypeScript compilation**

```bash
npx tsc --noEmit 2>&1 | head -30
```

Expected: No new errors from HomePage.tsx or CaptureToolbar.tsx.

---

### Task 4: Redesign SessionsPage — 3-Column Layout

**Files:**
- Modify: `desktop/src/pages/SessionsPage.tsx`

- [ ] **Step 1: Rewrite SessionsPage.tsx with 3-column folder-aware layout**

Replace `src/pages/SessionsPage.tsx` with:

```tsx
import { useState } from "react";
import { cn } from "@/lib/utils";
import { useSessions } from "@/hooks/use-sessions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import {
  Search,
  Archive,
  FileText,
  FileJson,
  FolderOpen,
  Trash2,
  Folder,
  ChevronRight,
} from "lucide-react";

function formatDuration(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  if (m === 0) return `${s}s`;
  return `${m}m ${s.toString().padStart(2, "0")}s`;
}

function formatTimestamp(ts: number) {
  return new Date(ts).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

interface SessionItem {
  id: string;
  date: string;
  duration: number;
  segmentCount: number;
  preview: string;
}

export function SessionsPage() {
  const {
    sessions,
    selectedIds,
    activeSession,
    search,
    setSearch,
    selectSession,
    toggleSelect,
    selectAll,
    deselectAll,
    deleteSelected,
    deleteSession,
    exportTxt,
    exportJson,
    openFolder,
  } = useSessions();

  const [deleteConfirm, setDeleteConfirm] = useState<{ type: "selected" } | { type: "single"; id: string } | null>(null);
  const [filterText, setFilterText] = useState("");
  const [activeFolder, setActiveFolder] = useState<string | null>(null);

  // Mock folder structure — in production this comes from session storage
  const folders = [
    { name: "Work", count: 5 },
    { name: "Study", count: 3 },
    { name: "Podcasts", count: 2 },
    { name: "Global", count: 2 },
  ];

  const allSelected = sessions.length > 0 && selectedIds.size === sessions.length;

  // Filter transcript lines by keyword
  const filteredTranscript = activeSession?.transcript.filter((line) => {
    if (!filterText) return true;
    const q = filterText.toLowerCase();
    return line.text.toLowerCase().includes(q) || (line.translatedText?.toLowerCase().includes(q));
  }) ?? [];

  const handleDeleteSelected = () => {
    deleteSelected();
    setDeleteConfirm(null);
  };

  const handleDeleteSession = () => {
    if (deleteConfirm?.type === "single") {
      deleteSession(deleteConfirm.id);
    }
    setDeleteConfirm(null);
  };

  return (
    <div className="flex flex-1 min-h-0">
      {/* Column 2: Sessions Browser */}
      <div className="w-72 border-r border-border/50 flex flex-col shrink-0 min-h-0 bg-card/30">
        <div className="p-3 border-b border-border/30">
          <h2 className="text-sm font-semibold">All Sessions</h2>
        </div>

        {/* Folder groups */}
        <div className="px-2 py-2 border-b border-border/20">
          {folders.map((f) => (
            <button
              key={f.name}
              onClick={() => setActiveFolder(activeFolder === f.name ? null : f.name)}
              className={cn(
                "w-full flex items-center gap-2 px-2 py-1.5 rounded-md text-sm transition-colors",
                activeFolder === f.name
                  ? "bg-accent text-accent-foreground"
                  : "text-muted-foreground hover:text-foreground hover:bg-muted/50"
              )}
            >
              <ChevronRight
                className={cn("h-3 w-3 transition-transform", activeFolder === f.name && "rotate-90")}
              />
              <Folder className="h-3.5 w-3.5" />
              <span className="flex-1 text-left">{f.name}</span>
              <span className="text-[11px] text-muted-foreground">{f.count}</span>
            </button>
          ))}
        </div>

        {/* Search */}
        <div className="p-2">
          <div className="relative">
            <Search className="absolute left-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground" />
            <Input
              placeholder="Search..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pl-8 h-8 text-xs"
            />
          </div>
        </div>

        {/* Recent sessions */}
        <div className="flex-1 overflow-y-auto min-h-0">
          <div className="px-3 py-1">
            <p className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider">Recent</p>
          </div>
          <div className="px-2 pb-2">
            {sessions.length === 0 && (
              <div className="flex flex-col items-center justify-center py-8 text-muted-foreground text-xs gap-2">
                <Archive className="h-6 w-6 opacity-30" />
                No sessions
              </div>
            )}
            {sessions.map((s) => (
              <div
                key={s.id}
                role="button"
                tabIndex={0}
                className={cn(
                  "w-full text-left flex items-start gap-2 px-2 py-2 rounded-md transition-colors cursor-pointer",
                  activeSession?.info.id === s.id ? "bg-accent" : "hover:bg-muted/50"
                )}
                onClick={() => selectSession(s)}
                onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); selectSession(s); } }}
              >
                <Checkbox
                  checked={selectedIds.has(s.id)}
                  onCheckedChange={() => toggleSelect(s.id)}
                  onClick={(e) => e.stopPropagation()}
                  className="mt-0.5"
                />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-1.5">
                    <span className="text-xs font-medium truncate">
                      {new Date(s.date).toLocaleDateString([], { month: "short", day: "numeric" })} &middot;{" "}
                      {new Date(s.date).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                    </span>
                  </div>
                  <div className="flex items-center gap-1.5 mt-0.5">
                    <span className="text-[11px] text-muted-foreground">{formatDuration(s.duration)}</span>
                  </div>
                  <p className="text-[11px] text-muted-foreground truncate mt-0.5">{s.preview || "No preview"}</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Footer actions */}
        <div className="flex items-center gap-1 p-2 border-t border-border/30">
          <Button variant="ghost" size="sm" className="text-xs h-7" onClick={allSelected ? deselectAll : selectAll}>
            {allSelected ? "Deselect" : "Select All"}
          </Button>
          {selectedIds.size > 0 && (
            <Button variant="ghost" size="sm" className="text-xs text-destructive h-7" onClick={() => setDeleteConfirm({ type: "selected" })}>
              <Trash2 className="h-3 w-3 mr-1" />
              Delete ({selectedIds.size})
            </Button>
          )}
        </div>
      </div>

      {/* Column 3: Transcript Detail */}
      <div className="flex-1 flex flex-col min-w-0 min-h-0">
        {!activeSession ? (
          <div className="flex flex-col items-center justify-center h-full text-muted-foreground gap-3">
            <Archive className="h-12 w-12 opacity-30" />
            <p className="text-sm">Select a session to view its transcript</p>
          </div>
        ) : (
          <>
            {/* Session header */}
            <div className="px-6 py-3 border-b border-border/30">
              <h2 className="text-base font-semibold">
                {new Date(activeSession.info.date).toLocaleString()}
              </h2>
              <div className="flex gap-3 text-xs text-muted-foreground mt-1">
                <span>{formatDuration(activeSession.info.duration)}</span>
                <span>&middot;</span>
                <span>{activeSession.info.segmentCount} segments</span>
              </div>
            </div>

            {/* Filter */}
            <div className="px-4 py-2 border-b border-border/20">
              <Input
                placeholder="Filter transcript by keyword..."
                value={filterText}
                onChange={(e) => setFilterText(e.target.value)}
                className="h-8 text-xs border-border/30"
              />
            </div>

            {/* Transcript */}
            <div className="flex-1 overflow-y-auto min-h-0">
              <div className="px-6 py-3 space-y-1">
                {filteredTranscript.length === 0 && filterText && (
                  <div className="flex flex-col items-center justify-center py-12 text-muted-foreground text-sm gap-2">
                    <Search className="h-8 w-8 opacity-30" />
                    No lines matching "{filterText}"
                  </div>
                )}
                {filteredTranscript.map((line) => (
                  <div key={line.id} className="flex gap-4 py-2 border-b border-border/10">
                    <span className="text-[11px] text-muted-foreground font-mono shrink-0 pt-0.5 w-[4.5rem] text-right">
                      {formatTimestamp(line.timestamp)}
                    </span>
                    <div className="flex-1 min-w-0">
                      <p className="text-sm leading-relaxed">
                        {"speakerLabel" in line && line.speakerLabel && (
                          <span
                            className="inline-flex items-center gap-1 mr-2 text-[11px] font-semibold rounded px-1.5 py-0.5 align-middle"
                            style={{
                              backgroundColor: `${(line as any).speakerColor}22`,
                              color: (line as any).speakerColor,
                              border: `1px solid ${(line as any).speakerColor}44`,
                            }}
                          >
                            {(line as any).speakerLabel}
                          </span>
                        )}
                        {line.text}
                      </p>
                      {line.translatedText && (
                        <p className="text-xs text-muted-foreground mt-0.5 leading-relaxed">{line.translatedText}</p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Action bar */}
            <div className="flex items-center gap-2 px-4 py-2 border-t border-border/30">
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Button variant="ghost" size="icon" className="h-8 w-8" onClick={() => exportTxt(activeSession.info.id)}>
                      <FileText className="h-3.5 w-3.5" />
                    </Button>
                  </TooltipTrigger>
                  <TooltipContent>Export TXT</TooltipContent>
                </Tooltip>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Button variant="ghost" size="icon" className="h-8 w-8" onClick={() => exportJson(activeSession.info.id)}>
                      <FileJson className="h-3.5 w-3.5" />
                    </Button>
                  </TooltipTrigger>
                  <TooltipContent>Export JSON</TooltipContent>
                </Tooltip>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Button variant="ghost" size="icon" className="h-8 w-8" onClick={() => openFolder(activeSession.info.id)}>
                      <FolderOpen className="h-3.5 w-3.5" />
                    </Button>
                  </TooltipTrigger>
                  <TooltipContent>Open Folder</TooltipContent>
                </Tooltip>
              </TooltipProvider>
              <div className="ml-auto">
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-8 w-8 text-destructive hover:text-destructive"
                  onClick={() => setDeleteConfirm({ type: "single", id: activeSession.info.id })}
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </Button>
              </div>
            </div>
          </>
        )}
      </div>

      <ConfirmDialog
        open={deleteConfirm !== null}
        onOpenChange={(open) => { if (!open) setDeleteConfirm(null); }}
        title={deleteConfirm?.type === "selected" ? "Delete Selected Sessions" : "Delete Session"}
        description={
          deleteConfirm?.type === "selected"
            ? `Are you sure you want to delete ${selectedIds.size} selected session(s)? This action cannot be undone.`
            : "Are you sure you want to delete this session? This action cannot be undone."
        }
        confirmLabel="Delete"
        onConfirm={deleteConfirm?.type === "selected" ? handleDeleteSelected : handleDeleteSession}
      />
    </div>
  );
}
```

Note: The folder groups are currently hardcoded UI placeholders. The `useSessions` hook and main process session storage will need folder support in a future task. For now, the folder list renders as static UI elements that match the design spec's 3-column layout.

- [ ] **Step 2: Verify TypeScript compilation**

```bash
npx tsc --noEmit 2>&1 | head -30
```

Expected: No new errors from SessionsPage.tsx.

---

### Task 5: Update SettingsPage for Global Sidebar

**Files:**
- Modify: `desktop/src/pages/SettingsPage.tsx`

- [ ] **Step 1: Rewrite SettingsPage.tsx**

Replace `src/pages/SettingsPage.tsx` with:

```tsx
import { useState } from "react";
import { cn } from "@/lib/utils";
import { useSettings } from "../hooks/use-settings";
import { GeneralSettings } from "../components/settings/GeneralSettings";
import { SpeechSettings } from "../components/settings/SpeechSettings";
import { TranslationSettings } from "../components/settings/TranslationSettings";
import { OverlaySettingsPanel } from "../components/settings/OverlaySettings";
import { Settings, Mic, Languages, Monitor } from "lucide-react";

const TABS = [
  { id: "general", label: "General", icon: Settings },
  { id: "speech", label: "Speech", icon: Mic },
  { id: "translation", label: "Translation", icon: Languages },
  { id: "overlay", label: "Overlay", icon: Monitor },
] as const;

type TabId = (typeof TABS)[number]["id"];

export function SettingsPage() {
  const { settings, update, loaded } = useSettings();
  const [activeTab, setActiveTab] = useState<TabId>("general");

  if (!loaded) return null;

  return (
    <div className="flex flex-1 min-h-0">
      {/* Sub-tab navigation */}
      <nav className="w-44 border-r border-border/50 py-4 space-y-0.5 px-2 shrink-0 bg-card/30">
        {TABS.map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            onClick={() => setActiveTab(id)}
            className={cn(
              "w-full flex items-center gap-2.5 px-3 py-2 rounded-md text-sm transition-colors",
              activeTab === id
                ? "bg-accent font-medium text-accent-foreground"
                : "text-muted-foreground hover:text-foreground hover:bg-muted/50"
            )}
          >
            <Icon className="h-4 w-4" />
            {label}
          </button>
        ))}
      </nav>

      {/* Content area */}
      <div className="flex-1 overflow-y-auto min-h-0">
        <div className="p-6 max-w-2xl mx-auto">
          <h1 className="text-2xl font-bold mb-8">
            {TABS.find((t) => t.id === activeTab)?.label} Settings
          </h1>

          {activeTab === "general" && <GeneralSettings settings={settings} onUpdate={update} />}
          {activeTab === "speech" && <SpeechSettings settings={settings} onUpdate={update} />}
          {activeTab === "translation" && <TranslationSettings settings={settings} onUpdate={update} />}
          {activeTab === "overlay" && <OverlaySettingsPanel settings={settings} onUpdate={update} />}
        </div>
      </div>
    </div>
  );
}
```

The SettingsPage now uses the shared sidebar from Layout (col 1), then splits into sub-tab nav (col 2, 176px) and content (col 3). The content area has a centered `max-w-2xl` container for readability.

- [ ] **Step 2: Verify TypeScript compilation**

```bash
npx tsc --noEmit 2>&1 | head -30
```

Expected: No new errors from SettingsPage.tsx.

---

### Task 6: Redesign Overlay Window — Glassmorphism

**Files:**
- Modify: `desktop/src/overlay/overlay.css`
- Modify: `desktop/src/overlay/OverlayApp.tsx`

- [ ] **Step 1: Replace overlay.css with glassmorphism + NERIS dark**

Replace `src/overlay/overlay.css`:

```css
@import "tailwindcss";

* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

body {
  font-family: "Inter", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  overflow: hidden;
  background: transparent;
}
```

- [ ] **Step 2: Update OverlayApp.tsx with glassmorphism styling**

Replace `src/overlay/OverlayApp.tsx` with:

```tsx
import { useState, useEffect, useRef, useCallback } from "react";

interface OverlaySettings {
  fontSize: number;
  lineHeight: number;
  theme: "Dark" | "Light";
  opacity: number;
  showTranslation: boolean;
}

interface TranscriptLine {
  id: string;
  text: string;
  translatedText?: string;
  timestamp: number;
  speakerLabel?: string;
  speakerColor?: string;
}

const MAX_LINES = 50;
const PARTIAL_ID = "__partial__";

let partialSeq = 0;

export function OverlayApp() {
  const [settings, setSettings] = useState<OverlaySettings>({
    fontSize: 26,
    lineHeight: 1.35,
    theme: "Dark",
    opacity: 0.88,
    showTranslation: true,
  });
  const [lines, setLines] = useState<TranscriptLine[]>([]);
  const [partialTranslation, setPartialTranslation] = useState<string | null>(null);
  const [pendingTranslation, setPendingTranslation] = useState<Set<string>>(new Set());
  const [isAtBottom, setIsAtBottom] = useState(true);
  const scrollRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  const addCommittedLine = useCallback((line: TranscriptLine) => {
    setLines((prev) => {
      const next = prev.filter((l) => l.id !== PARTIAL_ID);
      next.push(line);
      return next.length > MAX_LINES ? next.slice(-MAX_LINES) : next;
    });
    if (!line.translatedText) {
      setPendingTranslation((prev) => new Set(prev).add(line.id));
    }
  }, []);

  const updatePartial = useCallback((text: string) => {
    setLines((prev) => {
      const next = prev.filter((l) => l.id !== PARTIAL_ID);
      next.push({ id: PARTIAL_ID, text, timestamp: Date.now() });
      return next.length > MAX_LINES ? next.slice(-MAX_LINES) : next;
    });
  }, []);

  useEffect(() => {
    window.overlayAPI.getSettings().then((s) => setSettings(s));
  }, []);

  useEffect(() => {
    const unsubs = [
      window.overlayAPI.onTranscriptLine((line) => {
        addCommittedLine(line);
        setPartialTranslation(null);
      }),
      window.overlayAPI.onPartialUpdate((data) => {
        if (data.text) updatePartial(data.text);
      }),
      window.overlayAPI.onTranslationUpdate((data) => {
        setLines((prev) =>
          prev.map((line) =>
            line.id === data.id ? { ...line, translatedText: data.translatedText } : line,
          ),
        );
        setPendingTranslation((prev) => {
          const next = new Set(prev);
          next.delete(data.id);
          return next;
        });
      }),
      window.overlayAPI.onTranslationCommitted((data) => {
        setPartialTranslation(data.text || null);
      }),
      window.overlayAPI.onSettingsUpdate((s) => {
        setSettings((prev) => ({ ...prev, ...s }));
      }),
      window.overlayAPI.onClear(() => {
        setLines([]);
        setPartialTranslation(null);
        setPendingTranslation(new Set());
      }),
    ];
    return () => unsubs.forEach((fn) => fn());
  }, [addCommittedLine, updatePartial]);

  useEffect(() => {
    if (isAtBottom) {
      bottomRef.current?.scrollIntoView({ behavior: "auto" });
    }
  }, [lines, isAtBottom]);

  const handleScroll = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return;
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 40;
    setIsAtBottom(atBottom);
  }, []);

  const jumpToBottom = () => {
    setIsAtBottom(true);
    bottomRef.current?.scrollIntoView({ behavior: "auto" });
  };

  // NERIS glassmorphism theme (dark only)
  const bgOpacity = settings.opacity;
  const isEmpty = lines.length === 0;

  return (
    <div
      className="h-screen w-screen flex flex-col rounded-xl overflow-hidden"
      style={{
        background: `hsla(210, 100%, 20%, ${bgOpacity})`,
        backdropFilter: "blur(24px)",
        WebkitBackdropFilter: "blur(24px)",
        border: "1px solid hsla(0, 0%, 100%, 0.08)",
        boxShadow: "0 4px 16px hsla(0, 0%, 0%, 0.16)",
      }}
    >
      {/* Drag handle */}
      <div
        className="flex items-center justify-between px-3 h-7 shrink-0 border-b border-white/8"
        style={{ WebkitAppRegion: "drag" } as React.CSSProperties}
      >
        <div className="text-[10px] text-white/30 select-none">NERIS Sublingual</div>
        <button
          className="text-white/30 hover:text-white text-xs w-5 h-5 flex items-center justify-center rounded hover:bg-white/10 transition-colors"
          style={{ WebkitAppRegion: "no-drag" } as React.CSSProperties}
          onClick={() => window.overlayAPI.close()}
        >
          &#x2715;
        </button>
      </div>

      {/* Content */}
      <div
        ref={scrollRef}
        className="flex-1 overflow-y-auto px-4 py-3"
        onScroll={handleScroll}
      >
        {isEmpty && (
          <div className="flex items-center justify-center h-full text-white/40 text-sm">
            Waiting for speech...
          </div>
        )}

        {lines.map((line) => {
          const isPartial = line.id === PARTIAL_ID;
          return (
            <div
              key={line.id}
              className={`mb-3 border-white/8 ${!isPartial ? "border-b pb-3 last:border-b-0" : ""}`}
            >
              <p
                className="text-white font-medium"
                style={{ fontSize: settings.fontSize, lineHeight: settings.lineHeight }}
              >
                {line.speakerLabel && (
                  <span
                    className="inline-flex items-center gap-1 mr-2 text-xs font-semibold rounded px-1.5 py-0.5 align-middle"
                    style={{
                      backgroundColor: `${line.speakerColor}22`,
                      color: line.speakerColor,
                      border: `1px solid ${line.speakerColor}44`,
                    }}
                  >
                    {line.speakerLabel}
                  </span>
                )}
                {line.text}
              </p>
              {settings.showTranslation && !isPartial && line.translatedText ? (
                <p
                  className="text-white/60 mt-0.5"
                  style={{
                    fontSize: Math.max(14, settings.fontSize - 4),
                    lineHeight: settings.lineHeight,
                  }}
                >
                  {line.translatedText}
                </p>
              ) : settings.showTranslation && !isPartial && pendingTranslation.has(line.id) ? (
                <p
                  className="text-white/40 mt-0.5 animate-pulse"
                  style={{
                    fontSize: Math.max(14, settings.fontSize - 4),
                    lineHeight: settings.lineHeight,
                  }}
                >
                  ···
                </p>
              ) : null}
              {settings.showTranslation && isPartial && partialTranslation ? (
                <p
                  className="text-white/60 mt-0.5"
                  style={{
                    fontSize: Math.max(14, settings.fontSize - 4),
                    lineHeight: settings.lineHeight,
                  }}
                >
                  {partialTranslation}
                </p>
              ) : settings.showTranslation && isPartial ? (
                <p
                  className="text-white/40 mt-0.5 animate-pulse"
                  style={{
                    fontSize: Math.max(14, settings.fontSize - 4),
                    lineHeight: settings.lineHeight,
                  }}
                >
                  ···
                </p>
              ) : null}
            </div>
          );
        })}

        <div ref={bottomRef} />
      </div>

      {/* Jump to bottom */}
      {!isAtBottom && (
        <button
          className="absolute bottom-3 right-3 w-8 h-8 rounded-full flex items-center justify-center text-sm bg-white/15 text-white hover:bg-white/25 transition-colors"
          onClick={jumpToBottom}
        >
          &#x2193;
        </button>
      )}
    </div>
  );
}
```

Key changes in the overlay:
- Dark-only glassmorphism: `hsla(210, 100%, 20%, opacity)` + `backdrop-blur(24px)`
- Frosted edge border: `hsla(0, 0%, 100%, 0.08)`
- Box shadow for depth
- `rounded-xl` (12px) for macOS-native corner radius
- Branding changed to "NERIS Sublingual"
- Removed theme toggle logic — always dark

- [ ] **Step 3: Verify overlay renders correctly**

Build and check:

```bash
npx vite build 2>&1 | tail -10
```

Expected: Overlay chunk should build without errors.

---

### Task 7: Update Splash Screen to Dark Mode

**Files:**
- Modify: `desktop/index.html`

- [ ] **Step 1: Replace the splash screen with NERIS dark style**

In `index.html`, replace the `#app-splash` div and its styles:

```html
<!doctype html>
<html lang="en" class="h-full">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta http-equiv="Content-Security-Policy" content="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self' https://deep-translator-api.azurewebsites.net http://localhost:* http://127.0.0.1:* ws://localhost:* ws://127.0.0.1:* wss://localhost:* wss://127.0.0.1:*" />
    <title>NERIS Sublingual</title>
  </head>
  <body class="h-full min-h-0 overflow-hidden antialiased">
    <div id="root" class="h-full min-h-0">
      <div id="app-splash" style="position:fixed;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;background:#001A33;gap:16px;z-index:9999">
        <div style="display:flex;align-items:center;gap:8px">
          <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="#0066CC" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" style="animation:splash-pulse 1.6s ease-in-out infinite">
            <path d="M2 6c.6.5 1.2 1 2.5 2C7 10 8 12 8 12s1-2 3.5-4c1.3-1 1.9-1.5 2.5-2"/>
            <path d="M2 12c.6.5 1.2 1 2.5 2C7 16 8 18 8 18s1-2 3.5-4c1.3-1 1.9-1.5 2.5-2"/>
            <path d="M2 18c.6.5 1.2 1 2.5 2C7 22 8 24 8 24s1-2 3.5-4c1.3-1 1.9-1.5 2.5-2"/>
            <path d="M19 9c-1.2.6-2.4 1.2-3.5 2.5C14 13 12 14 12 14s2 1 4 3.5c1.3 1.5 1.9 2.3 2.5 3.5"/>
            <path d="M22 6c-1.2.6-2.4 1.2-3.5 2.5C17 10 15 11 15 11s2 1 4 3.5c1.3 1.5 1.9 2.3 2.5 3.5"/>
          </svg>
          <span style="color:#E5EDF5;font-size:20px;font-weight:600;font-family:-apple-system,BlinkMacSystemFont,sans-serif">Sublingual</span>
        </div>
        <div style="display:flex;gap:6px;align-items:center">
          <span style="width:7px;height:7px;border-radius:50%;background:#0066CC;animation:splash-dot 1.2s ease-in-out infinite"></span>
          <span style="width:7px;height:7px;border-radius:50%;background:#0059B3;animation:splash-dot 1.2s ease-in-out .2s infinite"></span>
          <span style="width:7px;height:7px;border-radius:50%;background:#004080;animation:splash-dot 1.2s ease-in-out .4s infinite"></span>
        </div>
        <style>
          @keyframes splash-pulse{0%,100%{opacity:1;transform:scale(1)}50%{opacity:.8;transform:scale(.93)}}
          @keyframes splash-dot{0%,80%,100%{opacity:.2;transform:scale(.8)}40%{opacity:1;transform:scale(1)}}
        </style>
      </div>
    </div>
    <script type="module" src="/src/renderer.tsx"></script>
  </body>
</html>
```

The splash now uses:
- `#001A33` (Deep Ocean) background
- `#0066CC` (Aurora Blue) accent dots
- `#E5EDF5` (Near-white) "Sublingual" text
- Waves SVG icon as the NERIS brand mark

- [ ] **Step 2: Update document title**

Change `<title>NextG Sublingual</title>` to `<title>NERIS Sublingual</title>` (done in step 1).

---

### Task 8: Restyle Settings Components with NERIS Colors

**Files:**
- Modify: `desktop/src/components/settings/SettingsSection.tsx`

- [ ] **Step 1: Update SettingsSection with tighter border styling**

Replace the `className` in `SettingsSection`:

```tsx
import { cn } from "@/lib/utils";

interface SettingsSectionProps {
  title: string;
  description?: string;
  children: React.ReactNode;
  className?: string;
}

export function SettingsSection({ title, description, children, className }: SettingsSectionProps) {
  return (
    <div className={cn("rounded-xl border border-border/50 bg-card/60 p-6", className)}>
      <h3 className="text-base font-semibold">{title}</h3>
      {description && <p className="text-xs text-muted-foreground mt-1">{description}</p>}
      <div className="mt-4 space-y-4">{children}</div>
    </div>
  );
}
```

Changes: `rounded-lg` → `rounded-xl`, `bg-card` → `bg-card/60` (slightly translucent for depth), `text-lg` → `text-base`, `text-sm` helper → `text-xs`.

---

### Task 9: Verify & Build

**Files:**
- (All modified files verified)

- [ ] **Step 1: Run TypeScript compiler across entire project**

```bash
npx tsc --noEmit 2>&1 | tail -30
```

Expected: Only pre-existing errors. No new errors from modified files. Fix any sidebar-related import issues if present.

- [ ] **Step 2: Run Vite build to verify bundling**

```bash
npx vite build 2>&1 | tail -15
```

Expected: Build completes successfully with both `index.html` and `overlay.html` entries.

- [ ] **Step 3: Run ESLint to check for syntax issues**

```bash
npx eslint --ext .ts,.tsx src/components/Layout.tsx src/App.tsx src/pages/HomePage.tsx src/pages/SessionsPage.tsx src/pages/SettingsPage.tsx src/components/CaptureToolbar.tsx 2>&1 | tail -20
```

Expected: No new lint errors.

---

### Task 10: Commit

- [ ] **Step 1: Stage and commit all changes**

```bash
git add desktop/src/index.css \
        desktop/src/App.tsx \
        desktop/src/components/Layout.tsx \
        desktop/src/pages/HomePage.tsx \
        desktop/src/pages/SessionsPage.tsx \
        desktop/src/pages/SettingsPage.tsx \
        desktop/src/components/CaptureToolbar.tsx \
        desktop/src/components/settings/SettingsSection.tsx \
        desktop/src/overlay/overlay.css \
        desktop/src/overlay/OverlayApp.tsx \
        desktop/index.html \
        desktop/src/components/ui/sidebar.tsx
git commit -m "feat: NERIS Ocean dark theme with macOS-style sidebar

- Replace shadcn neutral tokens with NERIS Ocean dark palette
- Add macOS-style sidebar navigation using shadcn Sidebar
- Transform HomePage into transcript-first layout
- Redesign SessionsPage to 3-column layout with folder groups
- Update SettingsPage to use global sidebar + sub-tab navigation
- Apply glassmorphism to overlay window
- Dark-only splash screen with NERIS branding"
```
