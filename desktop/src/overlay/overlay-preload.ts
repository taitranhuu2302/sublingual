import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("overlayAPI", {
  getSettings: () => ipcRenderer.invoke("settings:get").then((s: Record<string, unknown>) => (s as Record<string, unknown>).overlay),
  onTranscriptLine: (callback: (line: { id: string; text: string; translatedText?: string; timestamp: number }) => void) => {
    const handler = (_event: unknown, line: { id: string; text: string; translatedText?: string; timestamp: number }) => callback(line);
    ipcRenderer.on("overlay:transcript-line", handler);
    return () => ipcRenderer.removeListener("overlay:transcript-line", handler);
  },
  onPartialUpdate: (callback: (data: { text: string; translatedText?: string }) => void) => {
    const handler = (_event: unknown, data: { text: string; translatedText?: string }) => callback(data);
    ipcRenderer.on("overlay:partial-update", handler);
    return () => ipcRenderer.removeListener("overlay:partial-update", handler);
  },
  onSettingsUpdate: (callback: (settings: Record<string, unknown>) => void) => {
    const handler = (_event: unknown, settings: Record<string, unknown>) => callback(settings);
    ipcRenderer.on("overlay:settings-update", handler);
    return () => ipcRenderer.removeListener("overlay:settings-update", handler);
  },
  onTranslationUpdate: (callback: (data: { id: string; translatedText: string }) => void) => {
    const handler = (_event: unknown, data: { id: string; translatedText: string }) => callback(data);
    ipcRenderer.on("overlay:translation-update", handler);
    return () => ipcRenderer.removeListener("overlay:translation-update", handler);
  },
  onClear: (callback: () => void) => {
    const handler = () => callback();
    ipcRenderer.on("overlay:clear", handler);
    return () => ipcRenderer.removeListener("overlay:clear", handler);
  },
  close: () => ipcRenderer.invoke("overlay:hide"),
});
