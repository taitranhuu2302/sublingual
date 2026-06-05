import { useState, useEffect, useCallback, useRef } from "react";

export interface TranscriptEntry {
  id?: string;
  text: string;
  isFinal: boolean;
  timestamp: number;
  translatedText?: string;
  speakerId?: string;
  speakerLabel?: string;
  speakerColor?: string;
}

export function useTranscription() {
  const [segments, setSegments] = useState<TranscriptEntry[]>([]);
  const [running, setRunning] = useState(false);
  const [loading, setLoading] = useState(false);
  const pendingRef = useRef<Map<string, string>>(new Map());

  useEffect(() => {
    if (!window.electronAPI) return;

    window.electronAPI.asr.getState().then((s) => {
      setRunning(s.running);
      setLoading((s as any).loading ?? false);
    });

    const unsubTranscript = window.electronAPI.asr.onTranscript((segment) => {
      setSegments((prev) => {
        const translatedText = segment.id ? pendingRef.current.get(segment.id) : undefined;
        if (translatedText) pendingRef.current.delete(segment.id!);
        const entry = { ...segment, translatedText };

        if (entry.isFinal) {
          const withoutPartials = prev.filter((s) => s.isFinal);
          return [...withoutPartials, entry];
        }
        const finals = prev.filter((s) => s.isFinal);
        return [...finals, entry];
      });
    });

    const unsubTranslation = window.electronAPI.translation.onSegmentResult((result) => {
      setSegments((prev) => {
        const existing = prev.find((s) => s.id === result.segmentId);
        if (existing) {
          return prev.map((seg) =>
            seg.id === result.segmentId ? { ...seg, translatedText: result.translatedText } : seg,
          );
        }
        pendingRef.current.set(result.segmentId, result.translatedText);
        return prev;
      });
    });

    return () => {
      unsubTranscript();
      unsubTranslation();
    };
  }, []);

  const start = useCallback(async () => {
    if (!window.electronAPI) return;
    setLoading(true);
    try {
      await window.electronAPI.asr.startTranscription();
      setRunning(true);
    } finally {
      setLoading(false);
    }
  }, []);

  const stop = useCallback(async () => {
    if (!window.electronAPI) return;
    await window.electronAPI.asr.stopTranscription();
    setRunning(false);
    setLoading(false);
  }, []);

  const clear = useCallback(() => {
    setSegments([]);
    pendingRef.current.clear();
  }, []);

  return { segments, running, loading, start, stop, clear };
}
