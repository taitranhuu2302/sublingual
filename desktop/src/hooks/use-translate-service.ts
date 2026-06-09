import { useState, useEffect, useCallback } from "react";
import type { TranslateServiceStatus, TranslateDownloadProgress } from "@/types/electron-api";

export function useTranslateService() {
  const [status, setStatus] = useState<TranslateServiceStatus>({
    status: "stopped",
    pid: null,
    uptime: null,
    loadedModels: [],
    error: null,
    modelsAvailable: false,
  });
  const [logs, setLogs] = useState<string[]>([]);
  const [download, setDownload] = useState<TranslateDownloadProgress>({
    status: "idle",
    percent: 0,
    error: null,
  });

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

    const unsubDownload = window.electronAPI.translation.onDownloadProgress((p) => {
      setDownload(p);
    });

    // Fetch initial status
    window.electronAPI.translation.getServiceStatus().then(setStatus).catch(console.error);

    return () => {
      unsubStatus();
      unsubLog();
      unsubDownload();
    };
  }, []);

  const restart = useCallback(async () => {
    setStatus((prev) => ({ ...prev, status: "starting", pid: null, uptime: null, error: null, loadedModels: [], modelsAvailable: false }));
    await window.electronAPI.translation.restartService();
  }, []);

  const downloadModel = useCallback(async () => {
    setDownload({ status: "downloading", percent: 0, error: null });
    await window.electronAPI.translation.downloadModel();
  }, []);

  const clearLogs = useCallback(() => {
    setLogs([]);
  }, []);

  return { status, logs, download, restart, downloadModel, clearLogs };
}
