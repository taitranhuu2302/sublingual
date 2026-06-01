import { useSessions } from "../hooks/use-sessions";

import { useState } from "react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";
import { cn } from "@/lib/utils";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { ConfirmDialog } from "../components/ConfirmDialog";
import {
    Search,
    FileText,
    FileJson,
    FolderOpen,
    Trash2,
    Archive,
    CheckSquare,
} from "lucide-react";

function formatDuration(seconds: number): string {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    if (m === 0) return `${s}s`;
    return `${m}m ${s.toString().padStart(2, "0")}s`;
}

function groupByDate(
    sessions: Array<{ id: string; date: string; duration: number; segmentCount: number; preview: string }>
) {
    const groups: Map<string, typeof sessions> = new Map();
    const today = new Date().toDateString();
    const yesterday = new Date(Date.now() - 86400000).toDateString();

    for (const s of sessions) {
        const d = new Date(s.date).toDateString();
        let label = d;
        if (d === today) label = "Today";
        else if (d === yesterday) label = "Yesterday";

        if (!groups.has(label)) groups.set(label, []);
        groups.get(label)!.push(s);
    }
    return groups;
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

    const groups = groupByDate(sessions);
    const allSelected = sessions.length > 0 && selectedIds.size === sessions.length;

    const [deleteConfirm, setDeleteConfirm] = useState<{ type: "selected" } | { type: "single"; id: string } | null>(null);

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
            {/* Left panel — session list */}
            <div className="w-80 border-r flex flex-col shrink-0 min-h-0">
                <div className="p-3 border-b">
                    <div className="relative">
                        <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                        <Input
                            placeholder="Search sessions..."
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                            className="pl-9"
                        />
                    </div>
                </div>

                <div className="flex-1 overflow-y-auto min-h-0">
                    <div className="p-2">
                        {sessions.length === 0 && (
                            <div className="flex flex-col items-center justify-center py-12 text-muted-foreground text-sm">
                                <Archive className="h-8 w-8 mb-2 opacity-40" />
                                No sessions found
                            </div>
                        )}

                        {Array.from(groups.entries()).map(([label, items]) => (
                            <div key={label} className="mb-4">
                                <p className="text-xs font-medium text-muted-foreground px-2 mb-1">{label}</p>
                                {items.map((s) => (
                                    <button
                                        key={s.id}
                                        className={cn(
                                            "w-full text-left flex items-start gap-2 px-2 py-2 rounded-md transition-colors",
                                            activeSession?.info.id === s.id ? "bg-muted" : "hover:bg-muted/50"
                                        )}
                                        onClick={() => selectSession(s)}
                                    >
                                        <Checkbox
                                            checked={selectedIds.has(s.id)}
                                            onCheckedChange={() => toggleSelect(s.id)}
                                            onClick={(e) => e.stopPropagation()}
                                            className="mt-0.5"
                                        />
                                        <div className="flex-1 min-w-0">
                                            <div className="flex items-center gap-2 text-sm">
                                                <span className="font-medium">
                                                    {new Date(s.date).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                                                </span>
                                                <span className="text-muted-foreground">{formatDuration(s.duration)}</span>
                                            </div>
                                            <p className="text-xs text-muted-foreground truncate mt-0.5">{s.preview || "No preview"}</p>
                                        </div>
                                    </button>
                                ))}
                            </div>
                        ))}
                    </div>
                </div>

                <div className="flex items-center gap-2 p-2 border-t">
                    <Button variant="ghost" size="sm" onClick={allSelected ? deselectAll : selectAll}>
                        <CheckSquare className="h-3 w-3 mr-1" />
                        {allSelected ? "Deselect" : "Select All"}
                    </Button>
                    {selectedIds.size > 0 && (
                        <Button variant="ghost" size="sm" className="text-destructive" onClick={() => setDeleteConfirm({ type: "selected" })}>
                            <Trash2 className="h-3 w-3 mr-1" />
                            Delete ({selectedIds.size})
                        </Button>
                    )}
                </div>
            </div>

            {/* Right panel — transcript detail */}
            <div className="flex-1 flex flex-col min-w-0 min-h-0">
                {!activeSession ? (
                    <div className="flex flex-col items-center justify-center h-full text-muted-foreground text-sm">
                        <Archive className="h-10 w-10 mb-3 opacity-40" />
                        Select a session to view its transcript
                    </div>
                ) : (
                    <>
                        <div className="px-6 py-4 border-b">
                            <h2 className="text-lg font-semibold">
                                Session: {new Date(activeSession.info.date).toLocaleString()}
                            </h2>
                            <div className="flex gap-3 text-sm text-muted-foreground mt-1">
                                <span>Duration: {formatDuration(activeSession.info.duration)}</span>
                                <span>·</span>
                                <span>{activeSession.info.segmentCount} segments</span>
                            </div>
                        </div>

                        <div className="flex-1 overflow-y-auto min-h-0">
                            <div className="px-6 py-4 space-y-1">
                                {activeSession.transcript.map((line) => {
                                    const time = new Date(line.timestamp).toLocaleTimeString();
                                    return (
                                        <div key={line.id} className="flex gap-4 py-2 border-b border-border/30">
                                            <span className="text-xs text-muted-foreground font-mono shrink-0 pt-0.5 w-16">
                                                {time}
                                            </span>
                                            <div className="flex-1 min-w-0">
                                                <p className="text-base">{line.text}</p>
                                                {line.translatedText && (
                                                    <p className="text-sm text-muted-foreground mt-0.5">{line.translatedText}</p>
                                                )}
                                            </div>
                                        </div>
                                    );
                                })}
                            </div>
                        </div>

                        <div className="flex items-center gap-2 px-6 py-3 border-t">
                            <TooltipProvider>
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <Button variant="ghost" size="icon" onClick={() => exportTxt(activeSession.info.id)}>
                                            <FileText className="h-3 w-3" />
                                        </Button>
                                    </TooltipTrigger>
                                    <TooltipContent>Export TXT</TooltipContent>
                                </Tooltip>
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <Button variant="ghost" size="icon" onClick={() => exportJson(activeSession.info.id)}>
                                            <FileJson className="h-3 w-3" />
                                        </Button>
                                    </TooltipTrigger>
                                    <TooltipContent>Export JSON</TooltipContent>
                                </Tooltip>
                                <Tooltip>
                                    <TooltipTrigger asChild>
                                        <Button variant="ghost" size="icon" onClick={() => openFolder(activeSession.info.id)}>
                                            <FolderOpen className="h-3 w-3" />
                                        </Button>
                                    </TooltipTrigger>
                                    <TooltipContent>Open Folder</TooltipContent>
                                </Tooltip>
                            </TooltipProvider>
                            <div className="ml-auto">
                                <TooltipProvider>
                                    <Tooltip>
                                        <TooltipTrigger asChild>
                                            <Button variant="ghost" size="icon" className="text-destructive" onClick={() => setDeleteConfirm({ type: "single", id: activeSession.info.id })}>
                                                <Trash2 className="h-3 w-3" />
                                            </Button>
                                        </TooltipTrigger>
                                        <TooltipContent>Delete Session</TooltipContent>
                                    </Tooltip>
                                </TooltipProvider>
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
