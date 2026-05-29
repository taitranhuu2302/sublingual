import { useState, useEffect, useCallback } from "react";
import type { ModelDownloadProgress, InstallableModel } from "../types/electron-api";

export function useModelDownload() {
  const [models, setModels] = useState<InstallableModel[]>([]);
  const [activeDownload, setActiveDownload] = useState<ModelDownloadProgress | null>(null);
  const [loading, setLoading] = useState(false);

  const loadModels = useCallback(async () => {
    setLoading(true);
    try {
      const list = await window.electronAPI.models.getInstallable();
      setModels(list);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadModels();
  }, [loadModels]);

  useEffect(() => {
    const unsub = window.electronAPI.models.onDownloadProgress((progress: ModelDownloadProgress) => {
      setActiveDownload(progress);
      if (progress.status === "completed" || progress.status === "error" || progress.status === "cancelled") {
        loadModels();
        if (progress.status === "completed") {
          setTimeout(() => setActiveDownload(null), 1500);
        }
      }
    });
    return unsub;
  }, [loadModels]);

  const startDownload = useCallback(async (modelId: string) => {
    setActiveDownload({ modelId, percent: 0, status: "downloading" });
    await window.electronAPI.models.download(modelId);
  }, []);

  const cancelDownload = useCallback(async () => {
    await window.electronAPI.models.cancelDownload();
  }, []);

  const openFolder = useCallback(async () => {
    await window.electronAPI.models.openFolder();
  }, []);

  return { models, activeDownload, loading, startDownload, cancelDownload, openFolder, refresh: loadModels };
}
