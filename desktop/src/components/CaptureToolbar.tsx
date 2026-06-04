import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Badge } from "@/components/ui/badge";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { AudioSourceSelector } from "./AudioSourceSelector";
import { Mic, MicOff, Trash2, Monitor } from "lucide-react";
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

  const statusText = capturing ? "Capturing" : hasModel ? "Ready" : "No model";
  const statusVariant = capturing ? "default" : hasModel ? "secondary" : "outline";

  return (
    <TooltipProvider>
      <div className="flex items-center gap-3 border-b px-4 py-3">
        <AudioSourceSelector
          sources={sources}
          value={selectedSource}
          onChange={onSourceChange}
          disabled={capturing}
        />

        <Separator orientation="vertical" className="h-6" />

        {starting ? (
          <Button disabled className="bg-green-600 text-white opacity-70">
            <svg className="animate-spin h-4 w-4 mr-2" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            Starting...
          </Button>
        ) : !capturing ? (
          <Button onClick={onStart} disabled={!canStart} className="bg-green-600 hover:bg-green-700 text-white">
            <Mic className="h-4 w-4 mr-2" />
            Start
          </Button>
        ) : (
          <Button variant="destructive" onClick={onStop}>
            <MicOff className="h-4 w-4 mr-2" />
            Stop
          </Button>
        )}

        <Tooltip>
          <TooltipTrigger asChild>
            <Button variant="ghost" size="icon" onClick={onClear}>
              <Trash2 className="h-4 w-4" />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Clear transcript</TooltipContent>
        </Tooltip>

        <Separator orientation="vertical" className="h-6" />

        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant={overlayVisible ? "secondary" : "ghost"}
              size="icon"
              onClick={onToggleOverlay}
            >
              <Monitor className="h-4 w-4" />
            </Button>
          </TooltipTrigger>
          <TooltipContent>{overlayVisible ? "Hide overlay" : "Show overlay"}</TooltipContent>
        </Tooltip>

        <div className="ml-auto">
          <Badge variant={statusVariant} className={capturing ? "animate-pulse" : ""}>
            {capturing && <span className="inline-block w-2 h-2 rounded-full bg-green-400 mr-1.5" />}
            {statusText}
          </Badge>
        </div>
      </div>
    </TooltipProvider>
  );
}
