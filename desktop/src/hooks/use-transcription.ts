import { useState, useEffect, useCallback } from "react";

export interface TranscriptEntry {
  id?: string;
  text: string;
  isFinal: boolean;
  timestamp: number;
}

export function useTranscription() {
  const [segments, setSegments] = useState<TranscriptEntry[]>([]);
  const [running, setRunning] = useState(false);

  useEffect(() => {
    const unsub = window.electronAPI.asr.onTranscript((segment) => {
      setSegments((prev) => {
        if (segment.isFinal) {
          // Replace last partial with final
          const withoutPartials = prev.filter((s) => s.isFinal);
          return [...withoutPartials, segment];
        }
        // Replace current partial
        const finals = prev.filter((s) => s.isFinal);
        return [...finals, segment];
      });
    });
    return unsub;
  }, []);

  const start = useCallback(async () => {
    await window.electronAPI.asr.startTranscription();
    setRunning(true);
  }, []);

  const stop = useCallback(async () => {
    await window.electronAPI.asr.stopTranscription();
    setRunning(false);
  }, []);

  const clear = useCallback(() => setSegments([]), []);

  return { segments, running, start, stop, clear };
}
