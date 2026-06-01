import { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { CaptureToolbar } from "../components/CaptureToolbar";
import { useAudioCapture } from "../hooks/use-audio-capture";
import { useTranscription } from "../hooks/use-transcription";
import { useSettings } from "../hooks/use-settings";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { MessageSquare, Languages, Mic, Settings } from "lucide-react";

export function HomePage() {
  const navigate = useNavigate();
  const { sources, capturing, start, stop } = useAudioCapture();
  const { segments, running, start: startASR, stop: stopASR, clear: clearSegments } = useTranscription();
  const { settings } = useSettings();
  const [selectedSource, setSelectedSource] = useState("");
  const [overlayVisible, setOverlayVisible] = useState(false);
  const [elapsed, setElapsed] = useState(0);
  const [lastText, setLastText] = useState("");
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    if (!selectedSource && sources.length > 0) {
      setSelectedSource(sources[0].id);
    }
  }, [sources, selectedSource]);

  useEffect(() => {
    if (capturing) {
      setElapsed(0);
      timerRef.current = setInterval(() => setElapsed((p) => p + 1), 1000);
    } else {
      if (timerRef.current) clearInterval(timerRef.current);
    }
    return () => { if (timerRef.current) clearInterval(timerRef.current); };
  }, [capturing]);

  // Track last recognized text
  useEffect(() => {
    const finals = segments.filter((s) => s.isFinal);
    if (finals.length > 0) {
      setLastText(finals[finals.length - 1].text);
    }
  }, [segments]);

  const handleStart = async () => {
    if (!selectedSource) return;
    try {
      await start(selectedSource);
      await startASR();
      const visible = await window.electronAPI.overlay.isVisible();
      setOverlayVisible(visible);
    } catch (err) {
      console.error("Failed to start:", err);
      await stop();
    }
  };

  const handleStop = async () => {
    await stopASR();
    await stop();
    setOverlayVisible(false);
  };

  const handleClear = useCallback(() => {
    clearSegments();
    setLastText("");
  }, [clearSegments]);

  const handleToggleOverlay = async () => {
    await window.electronAPI.overlay.toggle();
    const visible = await window.electronAPI.overlay.isVisible();
    setOverlayVisible(visible);
  };

  const formatTime = (s: number) => {
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    const sec = s % 60;
    const mm = m.toString().padStart(2, "0");
    const ss = sec.toString().padStart(2, "0");
    return h > 0 ? `${h}:${mm}:${ss}` : `${mm}:${ss}`;
  };

  const finalCount = segments.filter((s) => s.isFinal).length;
  const wordCount = segments
    .filter((s) => s.isFinal)
    .reduce((sum, s) => sum + s.text.split(/\s+/).filter(Boolean).length, 0);
  const hasModel = !!settings.speechToText.selectedModel;

  const modelName = settings.speechToText.selectedModel
    ? settings.speechToText.selectedModel.replace(/^vosk-model-/, "").replace(/-/g, " ")
    : "None";

  return (
    <div className="flex flex-col flex-1">
      <CaptureToolbar
        sources={sources}
        selectedSource={selectedSource}
        capturing={capturing}
        hasModel={hasModel}
        overlayVisible={overlayVisible}
        onSourceChange={setSelectedSource}
        onStart={handleStart}
        onStop={handleStop}
        onClear={handleClear}
        onToggleOverlay={handleToggleOverlay}
      />

      <div className="flex-1 flex flex-col items-center justify-center p-6 gap-6">
        {!hasModel ? (
          <Card className="w-full max-w-md">
            <CardContent className="flex flex-col items-center py-10 gap-4">
              <Mic className="h-10 w-10 text-muted-foreground/40" />
              <div className="text-center">
                <h2 className="text-lg font-semibold mb-1">No Speech Model Installed</h2>
                <p className="text-sm text-muted-foreground">
                  Install a speech recognition model to start transcribing.
                </p>
              </div>
              <Button onClick={() => navigate("/settings")}>
                <Settings className="h-4 w-4 mr-2" />
                Go to Settings
              </Button>
            </CardContent>
          </Card>
        ) : (
          <>
            {/* Big timer */}
            <div className="text-center">
              <p className={`text-6xl font-mono font-light tracking-wider ${capturing ? "text-foreground" : "text-muted-foreground/40"}`}>
                {formatTime(elapsed)}
              </p>
              <p className="text-sm text-muted-foreground mt-2">
                {capturing ? "Recording in progress" : "Ready to capture"}
              </p>
            </div>

            {/* Stats cards */}
            <div className="grid grid-cols-2 gap-4 w-full max-w-lg">
              <Card>
                <CardContent className="flex flex-col items-center py-4 px-3 gap-1">
                  <MessageSquare className="h-5 w-5 text-muted-foreground" />
                  <span className="text-2xl font-semibold">{finalCount}</span>
                  <span className="text-xs text-muted-foreground">Segments</span>
                </CardContent>
              </Card>
              <Card>
                <CardContent className="flex flex-col items-center py-4 px-3 gap-1">
                  <Languages className="h-5 w-5 text-muted-foreground" />
                  <span className="text-2xl font-semibold">{wordCount}</span>
                  <span className="text-xs text-muted-foreground">Words</span>
                </CardContent>
              </Card>
            </div>

            {/* Last recognized text preview */}
            {lastText && (
              <div className="w-full max-w-lg">
                <Card className="bg-muted/30">
                  <CardContent className="py-3 px-4">
                    <p className="text-xs text-muted-foreground mb-1">Last recognized</p>
                    <p className="text-sm truncate">{lastText}</p>
                  </CardContent>
                </Card>
              </div>
            )}

            {/* Session info bar */}
            <div className="flex items-center gap-4 text-xs text-muted-foreground">
              <span className="flex items-center gap-1">
                <Mic className="h-3 w-3" /> Model: {modelName}
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
