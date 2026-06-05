import { useState } from "react";
import { cn } from "@/lib/utils";
import { useSessions } from "@/hooks/use-sessions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import {
  Search,
  Archive,
  FileText,
  FileJson,
  FolderOpen,
  Trash2,
  Folder,
  FolderPlus,
  Edit3,
  ChevronRight,
  Move,
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
    allSessions,
    folders,
    activeFolder,
    selectedIds,
    activeSession,
    search,
    setSearch,
    setActiveFolder,
    selectSession,
    toggleSelect,
    selectAll,
    deselectAll,
    deleteSelected,
    deleteSession,
    exportTxt,
    exportJson,
    openFolder,
    createFolder,
    renameFolder,
    deleteFolder,
    moveSessions,
  } = useSessions();

  const [deleteConfirm, setDeleteConfirm] = useState<{ type: "selected" } | { type: "single"; id: string } | null>(null);
  const [filterText, setFilterText] = useState("");
  const [expandedFolders, setExpandedFolders] = useState<Set<string>>(new Set(["global"]));

  // Folder dialogs
  const [showNewFolder, setShowNewFolder] = useState(false);
  const [newFolderName, setNewFolderName] = useState("");
  const [editingFolder, setEditingFolder] = useState<{ id: string; name: string } | null>(null);
  const [deleteFolderConfirm, setDeleteFolderConfirm] = useState<string | null>(null);
  const [moveTarget, setMoveTarget] = useState<string | null>(null);

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

  const handleCreateFolder = async () => {
    if (!newFolderName.trim()) return;
    await createFolder(newFolderName.trim());
    setNewFolderName("");
    setShowNewFolder(false);
  };

  const handleRenameFolder = async () => {
    if (!editingFolder || !editingFolder.name.trim()) return;
    await renameFolder(editingFolder.id, editingFolder.name.trim());
    setEditingFolder(null);
  };

  const handleDeleteFolder = async () => {
    if (!deleteFolderConfirm) return;
    await deleteFolder(deleteFolderConfirm);
    setDeleteFolderConfirm(null);
  };

  const handleMoveSessions = async (folderId: string) => {
    if (!moveTarget || selectedIds.size === 0) return;
    await moveSessions(Array.from(selectedIds), folderId);
    setMoveTarget(null);
    setSelectedIds(new Set());
  };

  return (
    <div className="flex flex-1 min-h-0">
      {/* Column 2: Sessions Browser */}
      <div className="w-72 border-r border-border/30 flex flex-col shrink-0 min-h-0 bg-card/30">
        <div className="flex items-center justify-between p-3 border-b border-border/20">
          <h2 className="text-sm font-semibold">All Sessions</h2>
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button variant="ghost" size="icon" className="h-6 w-6" onClick={() => setShowNewFolder(true)}>
                  <FolderPlus className="h-3.5 w-3.5" />
                </Button>
              </TooltipTrigger>
              <TooltipContent>New folder</TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </div>

        {/* Search */}
        <div className="p-2 border-b border-border/15">
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

        {/* Folder tree with sessions */}
        <div className="flex-1 overflow-y-auto min-h-0">
          <div className="px-2 py-1.5">
            {folders.length === 0 && (
              <div className="flex flex-col items-center justify-center py-8 text-muted-foreground text-xs gap-2">
                <Archive className="h-6 w-6 opacity-30" />
                No folders
              </div>
            )}
            {folders.map((f) => {
              const isExpanded = expandedFolders.has(f.id);
              const folderSessions = allSessions.filter((s) => s.folderId === f.id);
              const toggleExpanded = () => {
                const next = new Set(expandedFolders);
                if (next.has(f.id)) next.delete(f.id);
                else next.add(f.id);
                setExpandedFolders(next);
              };

              return (
                <div key={f.id} className="mb-0.5">
                  <div className="group flex items-center">
                    <button
                      onClick={toggleExpanded}
                      className="flex items-center justify-center h-7 w-7 shrink-0 rounded hover:bg-[hsl(234,19%,20%)] text-muted-foreground"
                    >
                      <ChevronRight
                        className={cn("h-3 w-3 transition-transform", isExpanded && "rotate-90")}
                      />
                    </button>
                    <button
                      onClick={() => setActiveFolder(activeFolder === f.id ? null : f.id)}
                      className={cn(
                        "flex-1 flex items-center gap-2 px-1.5 py-1.5 rounded-md text-sm transition-colors",
                        activeFolder === f.id
                          ? "bg-[hsl(220,50%,20%)] text-foreground"
                          : "text-muted-foreground hover:text-foreground hover:bg-[hsl(234,19%,20%)]"
                      )}
                    >
                      <Folder className="h-3.5 w-3.5 shrink-0" />
                      <span className="flex-1 text-left truncate">{f.name}</span>
                      <span className="text-[11px] text-muted-foreground">{f.sessionCount}</span>
                    </button>
                    {f.id !== "global" && (
                      <div className="hidden group-hover:flex items-center gap-0.5 ml-0.5">
                        <button
                          onClick={() => setEditingFolder({ id: f.id, name: f.name })}
                          className="h-6 w-6 flex items-center justify-center rounded hover:bg-muted/50 text-muted-foreground hover:text-foreground"
                        >
                          <Edit3 className="h-3 w-3" />
                        </button>
                        <button
                          onClick={() => setDeleteFolderConfirm(f.id)}
                          className="h-6 w-6 flex items-center justify-center rounded hover:bg-destructive/10 text-muted-foreground hover:text-destructive"
                        >
                          <Trash2 className="h-3 w-3" />
                        </button>
                      </div>
                    )}
                  </div>

                  {isExpanded && (
                    <div className="ml-6 border-l border-border/20 pl-2">
                      {(search && folderSessions.length === 0) || (!search && folderSessions.length === 0) ? (
                        <p className="text-[11px] text-muted-foreground py-2 px-2 italic">Empty</p>
                      ) : (
                        folderSessions.map((s) => (
                          <div
                            key={s.id}
                            role="button"
                            tabIndex={0}
                            className={cn(
                              "w-full text-left flex items-start gap-2 px-2 py-1.5 rounded-md transition-colors cursor-pointer",
                              activeSession?.info.id === s.id ? "bg-[hsl(220,50%,20%)]" : "hover:bg-[hsl(234,19%,20%)]"
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
                        ))
                      )}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* Footer actions */}
        <div className="flex items-center gap-1 p-2 border-t border-border/20">
          <Button variant="ghost" size="sm" className="text-xs h-7" onClick={allSelected ? deselectAll : selectAll}>
            {allSelected ? "Deselect" : "Select All"}
          </Button>
          {selectedIds.size > 0 && (
            <>
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Button variant="ghost" size="sm" className="text-xs h-7" onClick={() => setMoveTarget("prompt")}>
                      <Move className="h-3 w-3" />
                    </Button>
                  </TooltipTrigger>
                  <TooltipContent>Move to folder</TooltipContent>
                </Tooltip>
              </TooltipProvider>
              <Button variant="ghost" size="sm" className="text-xs text-destructive h-7" onClick={() => setDeleteConfirm({ type: "selected" })}>
                <Trash2 className="h-3 w-3 mr-1" />
                {selectedIds.size}
              </Button>
            </>
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
            <div className="px-6 py-3 border-b border-border/20">
              <h2 className="text-base font-semibold">
                {new Date(activeSession.info.date).toLocaleString()}
              </h2>
              <div className="flex gap-3 text-xs text-muted-foreground mt-1">
                <span>{formatDuration(activeSession.info.duration)}</span>
                <span>&middot;</span>
                <span>{activeSession.info.segmentCount} segments</span>
                {activeSession.info.folderId && (
                  <>
                    <span>&middot;</span>
                    <Select
                      value={activeSession.info.folderId}
                      onValueChange={(folderId) => {
                        moveSessions([activeSession.info.id], folderId);
                      }}
                    >
                      <SelectTrigger className="h-5 text-[11px] border-0 bg-transparent px-1 gap-1 text-primary/70 hover:text-primary hover:bg-muted/50 w-auto">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {folders.map((f) => (
                          <SelectItem key={f.id} value={f.id} className="text-xs">
                            <span className="flex items-center gap-2">
                              <Folder className="h-3 w-3" />
                              {f.name}
                            </span>
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </>
                )}
              </div>
            </div>

            {/* Filter */}
            <div className="px-4 py-2 border-b border-border/15">
              <Input
                placeholder="Filter transcript by keyword..."
                value={filterText}
                onChange={(e) => setFilterText(e.target.value)}
                className="h-8 text-xs"
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
                  <div key={line.id} className="flex gap-4 py-2 border-b border-border/5">
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
            <div className="flex items-center gap-2 px-4 py-2 border-t border-border/20">
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

      {/* Delete confirm */}
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

      {/* New Folder dialog */}
      <Dialog open={showNewFolder} onOpenChange={setShowNewFolder}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>New Folder</DialogTitle>
          </DialogHeader>
          <Input
            placeholder="Folder name"
            value={newFolderName}
            onChange={(e) => setNewFolderName(e.target.value)}
            onKeyDown={(e) => { if (e.key === "Enter") handleCreateFolder(); }}
            autoFocus
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowNewFolder(false)}>Cancel</Button>
            <Button onClick={handleCreateFolder} disabled={!newFolderName.trim()}>Create</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Rename Folder dialog */}
      <Dialog open={!!editingFolder} onOpenChange={() => setEditingFolder(null)}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>Rename Folder</DialogTitle>
          </DialogHeader>
          <Input
            placeholder="Folder name"
            value={editingFolder?.name || ""}
            onChange={(e) => setEditingFolder((prev) => prev ? { ...prev, name: e.target.value } : null)}
            onKeyDown={(e) => { if (e.key === "Enter") handleRenameFolder(); }}
            autoFocus
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditingFolder(null)}>Cancel</Button>
            <Button onClick={handleRenameFolder} disabled={!editingFolder?.name.trim()}>Rename</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete Folder confirm */}
      <ConfirmDialog
        open={!!deleteFolderConfirm}
        onOpenChange={(open) => { if (!open) setDeleteFolderConfirm(null); }}
        title="Delete Folder"
        description="Are you sure you want to delete this folder? Sessions inside it will be moved to the Global folder."
        confirmLabel="Delete Folder"
        onConfirm={handleDeleteFolder}
      />

      {/* Move Sessions dialog */}
      <Dialog open={moveTarget !== null} onOpenChange={() => setMoveTarget(null)}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>Move {selectedIds.size} session(s) to...</DialogTitle>
          </DialogHeader>
          <Select value={undefined} onValueChange={(v) => handleMoveSessions(v)}>
            <SelectTrigger>
              <SelectValue placeholder="Select folder" />
            </SelectTrigger>
            <SelectContent>
              {folders.filter((f) => f.id !== activeFolder).map((f) => (
                <SelectItem key={f.id} value={f.id}>
                  <span className="flex items-center gap-2">
                    <Folder className="h-3.5 w-3.5" />
                    {f.name}
                  </span>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <DialogFooter>
            <Button variant="outline" onClick={() => setMoveTarget(null)}>Cancel</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
