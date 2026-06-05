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
    const [loading, setLoading] = useState(false);

    useEffect(() => {
        if (!window.electronAPI) return;
        window.electronAPI.asr.getState().then((s) => {
            setRunning(s.running);
            setLoading(s.loading ?? false);
        });

        const unsub = window.electronAPI.asr.onTranscript((segment) => {
            setSegments((prev) => {
                if (segment.isFinal) {
                    const withoutPartials = prev.filter((s) => s.isFinal);
                    return [...withoutPartials, segment];
                }
                const finals = prev.filter((s) => s.isFinal);
                return [...finals, segment];
            });
        });
        return unsub;
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

    const clear = useCallback(() => setSegments([]), []);

    return { segments, running, loading, start, stop, clear };
}
