import { useState, useEffect } from "react";
import { Button } from "../components/ui/button";
import { AudioSourceSelector } from "../components/AudioSourceSelector";
import { ModelSelector } from "../components/ModelSelector";
import { SubtitleOverlay } from "../components/SubtitleOverlay";
import { useAudioCapture } from "../hooks/use-audio-capture";
import { useTranscription } from "../hooks/use-transcription";
import type { WhisperModel } from "../types/electron-api";

export function HomePage() {
  const { sources, capturing, activeSource, start, stop } = useAudioCapture();
  const { segments, running, start: startASR, stop: stopASR, clear } = useTranscription();
  const [selectedSource, setSelectedSource] = useState("");
  const [selectedModel, setSelectedModel] = useState("");
  const [models, setModels] = useState<WhisperModel[]>([]);

  useEffect(() => {
    window.electronAPI.asr.getModels().then(setModels);
  }, []);

  const handleStart = async () => {
    if (!selectedSource || !selectedModel) return;
    await window.electronAPI.asr.selectModel(selectedModel);
    await start(selectedSource);
    await startASR();
  };

  const handleStop = async () => {
    await stopASR();
    await stop();
  };

  return (
    <div className="flex flex-col h-full p-6 gap-4">
      <div className="flex items-center gap-4">
        <AudioSourceSelector
          sources={sources}
          value={selectedSource}
          onChange={setSelectedSource}
          disabled={capturing}
        />
        <ModelSelector
          models={models}
          value={selectedModel}
          onChange={setSelectedModel}
          disabled={capturing}
        />
        {!capturing ? (
          <Button onClick={handleStart} disabled={!selectedSource || !selectedModel}>
            Start
          </Button>
        ) : (
          <Button variant="destructive" onClick={handleStop}>
            Stop
          </Button>
        )}
        <Button variant="outline" onClick={clear}>
          Clear
        </Button>
      </div>

      <div className="flex-1 relative">
        <SubtitleOverlay segments={segments} />
      </div>
    </div>
  );
}
