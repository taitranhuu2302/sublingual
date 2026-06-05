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

  const folders = [
    { name: "Work", count: 5 },
    { name: "Study", count: 3 },
    { name: "Podcasts", count: 2 },
    { name: "Global", count: 2 },
  ];

  const allSelected = sessions.length > 0 && selectedIds.size === sessions.length;

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
