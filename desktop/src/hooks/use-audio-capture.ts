import { useState, useEffect, useCallback } from "react";
import type { AudioSource } from "../types/electron-api";

export function useAudioCapture() {
  const [sources, setSources] = useState<AudioSource[]>([]);
  const [capturing, setCapturing] = useState(false);
  const [activeSource, setActiveSource] = useState<string>("");

  useEffect(() => {
    if (!window.electronAPI?.audio) return;
    window.electronAPI.audio.getSources().then(setSources);
    window.electronAPI.audio.getState().then((s) => setCapturing(s.capturing));
  }, []);

  const start = useCallback(async (sourceId: string) => {
    await window.electronAPI.audio.startCapture(sourceId);
    setCapturing(true);
    setActiveSource(sourceId);
  }, []);

  const stop = useCallback(async () => {
    await window.electronAPI.audio.stopCapture();
    setCapturing(false);
    setActiveSource("");
  }, []);

  return { sources, capturing, activeSource, start, stop };
}
