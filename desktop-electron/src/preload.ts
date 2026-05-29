import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("electronAPI", {
  audio: {
    getSources: () => ipcRenderer.invoke("audio:get-sources"),
    startCapture: (sourceId: string) => ipcRenderer.invoke("audio:start-capture", sourceId),
    stopCapture: () => ipcRenderer.invoke("audio:stop-capture"),
    onAudioData: (callback: (data: Float32Array) => void) => {
      const handler = (_event: unknown, data: Float32Array) => callback(data);
      ipcRenderer.on("audio:data", handler);
      return () => ipcRenderer.removeListener("audio:data", handler);
    },
  },
  asr: {
    getModels: () => ipcRenderer.invoke("asr:get-models"),
    selectModel: (modelId: string) => ipcRenderer.invoke("asr:select-model", modelId),
    startTranscription: () => ipcRenderer.invoke("asr:start-transcription"),
    stopTranscription: () => ipcRenderer.invoke("asr:stop-transcription"),
    onTranscript: (callback: (segment: { text: string; isFinal: boolean; timestamp: number }) => void) => {
      const handler = (_event: unknown, segment: { text: string; isFinal: boolean; timestamp: number }) => callback(segment);
      ipcRenderer.on("asr:transcript", handler);
      return () => ipcRenderer.removeListener("asr:transcript", handler);
    },
  },
  settings: {
    get: () => ipcRenderer.invoke("settings:get"),
    set: (settings: Record<string, unknown>) => ipcRenderer.invoke("settings:set", settings),
  },
});

