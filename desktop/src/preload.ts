import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("electronAPI", {
  audio: {
    getSources: () => ipcRenderer.invoke("audio:get-sources"),
    getState: () => ipcRenderer.invoke("audio:get-state"),
    startCapture: (sourceId: string) =>
      ipcRenderer.invoke("audio:start-capture", sourceId),
    stopCapture: () => ipcRenderer.invoke("audio:stop-capture"),
    onAudioData: (callback: (data: Float32Array) => void) => {
      const handler = (_event: unknown, data: Float32Array) => callback(data);
      ipcRenderer.on("audio:data", handler);
      return () => ipcRenderer.removeListener("audio:data", handler);
    },
  },
  asr: {
    getModels: () => ipcRenderer.invoke("asr:get-models"),
    getState: () => ipcRenderer.invoke("asr:get-state"),
    selectModel: (modelId: string) =>
      ipcRenderer.invoke("asr:select-model", modelId),
    startTranscription: () => ipcRenderer.invoke("asr:start-transcription"),
    stopTranscription: () => ipcRenderer.invoke("asr:stop-transcription"),
    onModelStatus: (
      callback: (status: { status: string; message?: string }) => void
    ) => {
      const handler = (
        _event: unknown,
        status: { status: string; message?: string }
      ) => callback(status);
      ipcRenderer.on("asr:model-status", handler);
      return () => ipcRenderer.removeListener("asr:model-status", handler);
    },
    onTranscript: (
      callback: (segment: {
        id: string;
        text: string;
        isFinal: boolean;
        timestamp: number;
      }) => void
    ) => {
      const handler = (
        _event: unknown,
        segment: {
          id: string;
          text: string;
          isFinal: boolean;
          timestamp: number;
        }
      ) => callback(segment);
      ipcRenderer.on("asr:transcript", handler);
      return () => ipcRenderer.removeListener("asr:transcript", handler);
    },
  },
  settings: {
    get: () => ipcRenderer.invoke("settings:get"),
    set: (settings: Record<string, unknown>) =>
      ipcRenderer.invoke("settings:set", settings),
    browseDirectory: (title: string) =>
      ipcRenderer.invoke("settings:browse-directory", title),
    openDirectory: (dirPath: string) =>
      ipcRenderer.invoke("settings:open-directory", dirPath),
  },
  translation: {
    translate: (sourceText: string, sourceLang: string, targetLang: string) =>
      ipcRenderer.invoke("translation:translate", sourceText, sourceLang, targetLang),
    onSegmentResult: (
      callback: (result: {
        segmentId: string;
        translatedText: string;
        providerName: string;
        durationMs: number;
      }) => void
    ) => {
      const handler = (
        _event: unknown,
        result: {
          segmentId: string;
          translatedText: string;
          providerName: string;
          durationMs: number;
        }
      ) => callback(result);
      ipcRenderer.on("translation:segment-result", handler);
      return () =>
        ipcRenderer.removeListener("translation:segment-result", handler);
    },
  },
  models: {
    getInstallable: () => ipcRenderer.invoke("models:get-installable"),
    download: (modelId: string) =>
      ipcRenderer.invoke("models:download", modelId),
    cancelDownload: () => ipcRenderer.invoke("models:cancel-download"),
    remove: (modelId: string) => ipcRenderer.invoke("models:remove", modelId),
    openFolder: () => ipcRenderer.invoke("models:open-folder"),
    onDownloadProgress: (
      callback: (progress: {
        modelId: string;
        percent: number;
        status: string;
        error?: string;
      }) => void
    ) => {
      const handler = (
        _event: unknown,
        progress: {
          modelId: string;
          percent: number;
          status: string;
          error?: string;
        }
      ) => callback(progress);
      ipcRenderer.on("models:download-progress", handler);
      return () =>
        ipcRenderer.removeListener("models:download-progress", handler);
    },
  },
  overlay: {
    show: () => ipcRenderer.invoke("overlay:show"),
    hide: () => ipcRenderer.invoke("overlay:hide"),
    toggle: () => ipcRenderer.invoke("overlay:toggle"),
    isVisible: () => ipcRenderer.invoke("overlay:is-visible"),
  },
  sessions: {
    list: (search?: string) => ipcRenderer.invoke("sessions:list", search),
    getTranscript: (sessionId: string) =>
      ipcRenderer.invoke("sessions:get-transcript", sessionId),
    delete: (sessionIds: string[]) =>
      ipcRenderer.invoke("sessions:delete", sessionIds),
    clearAll: () => ipcRenderer.invoke("sessions:clear-all"),
    exportTxt: (sessionId: string) =>
      ipcRenderer.invoke("sessions:export-txt", sessionId),
    exportJson: (sessionId: string) =>
      ipcRenderer.invoke("sessions:export-json", sessionId),
    openFolder: (sessionId: string) =>
      ipcRenderer.invoke("sessions:open-folder", sessionId),
  },
});

