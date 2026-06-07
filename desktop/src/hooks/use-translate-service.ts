import { useState, useEffect, useCallback } from "react";
import type { TranslateServiceStatus } from "@/types/electron-api";

export function useTranslateService() {
  const [status, setStatus] = useState<TranslateServiceStatus>({
    status: "stopped",
    pid: null,
    uptime: null,
    loadedModels: [],
    error: null,
  });
  const [logs, setLogs] = useState<string[]>([]);

  useEffect(() => {
    const unsubStatus = window.electronAPI.translation.onServiceStatusChange((s) => {
      setStatus(s);
    });

    const unsubLog = window.electronAPI.translation.onServiceLog((log) => {
      setLogs((prev) => {
        const next = [...prev, log.line];
        return next.length > 50 ? next.slice(-50) : next;
      });
    });

    // Fetch initial status
    window.electronAPI.translation.getServiceStatus().then(setStatus).catch(console.error);

    return () => {
      unsubStatus();
      unsubLog();
    };
  }, []);

  const restart = useCallback(async () => {
    setStatus((prev) => ({ ...prev, status: "starting", pid: null, uptime: null, error: null, loadedModels: [] }));
    await window.electronAPI.translation.restartService();
  }, []);

  const clearLogs = useCallback(() => {
    setLogs([]);
  }, []);

  return { status, logs, restart, clearLogs };
}
