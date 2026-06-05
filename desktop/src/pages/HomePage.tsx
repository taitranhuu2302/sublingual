import { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { CaptureToolbar } from "@/components/CaptureToolbar";
import { useAudioCapture } from "@/hooks/use-audio-capture";
import { useTranscription } from "@/hooks/use-transcription";
import { useSettings } from "@/hooks/use-settings";
import { Mic, Settings, Languages, ScrollText, Clock } from "lucide-react";

function formatTimer(s: number) {
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
  const { settings, loaded: settingsLoaded } = useSettings();
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

  if (!settingsLoaded) {
    return (
      <div className="flex flex-col flex-1 min-h-0">
        <div className="flex-1 flex items-center justify-center">
          <div className="flex items-center gap-2 text-muted-foreground text-sm">
            <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            Loading...
          </div>
        </div>
      </div>
    );
  }

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
                    {formatTimer(elapsed)}
                  </>
                ) : (
                  formatTimer(elapsed)
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
