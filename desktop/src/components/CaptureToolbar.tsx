import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { AudioSourceSelector } from "./AudioSourceSelector";
import { Mic, MicOff, Trash2, Monitor, Circle, X } from "lucide-react";
import type { AudioSource } from "@/types/electron-api";

interface CaptureToolbarProps {
  sources: AudioSource[];
  selectedSource: string;
  capturing: boolean;
  starting: boolean;
  hasModel: boolean;
  overlayVisible: boolean;
  loadingMessage?: string;
  onSourceChange: (id: string) => void;
  onStart: () => void;
  onStop: () => void;
  onCancelLoading: () => void;
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
  loadingMessage,
  onSourceChange,
  onStart,
  onStop,
  onCancelLoading,
  onClear,
  onToggleOverlay,
}: CaptureToolbarProps) {
  const canStart = selectedSource && hasModel && !capturing && !starting;

  return (
    <TooltipProvider>
      <div className="flex items-center gap-2 px-4 py-2 border-b border-border/30 bg-card/50">
        <AudioSourceSelector
          sources={sources}
          value={selectedSource}
          onChange={onSourceChange}
          disabled={capturing}
        />

        <div className="w-px h-5 bg-border/30" />

        {starting ? (
          <div className="flex items-center gap-2 min-w-[180px]">
            <div className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-primary/10 text-primary text-sm">
              <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
              <span className="text-xs max-w-[120px] truncate">
                {loadingMessage || "Loading..."}
              </span>
            </div>
            <Button
              variant="ghost"
              size="icon"
              onClick={onCancelLoading}
              className="h-8 w-8 text-muted-foreground hover:text-destructive"
              title="Cancel loading"
            >
              <X className="h-4 w-4" />
            </Button>
          </div>
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
