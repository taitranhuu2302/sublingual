import { useState, useEffect, useCallback } from "react";
import type { AppSettings } from "../types/electron-api";

export function useSettings() {
  const [settings, setSettingsState] = useState<AppSettings>({
    language: "en",
    modelId: "",
    audioSourceId: "",
  });

  useEffect(() => {
    window.electronAPI.settings.get().then(setSettingsState);
  }, []);

  const update = useCallback(async (partial: Partial<AppSettings>) => {
    await window.electronAPI.settings.set(partial);
    setSettingsState((prev) => ({ ...prev, ...partial }));
  }, []);

  return { settings, update };
}
