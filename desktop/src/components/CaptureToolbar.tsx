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
      <div className="flex items-center gap-2 px-4 py-2 border-b border-border/30 bg-card/50">
        <AudioSourceSelector
          sources={sources}
          value={selectedSource}
          onChange={onSourceChange}
          disabled={capturing}
        />

        <div className="w-px h-5 bg-border/30" />

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
