export interface ElectronAPI {
  audio: {
    getSources: () => Promise<AudioSource[]>;
    startCapture: (sourceId: string) => Promise<void>;
    stopCapture: () => Promise<void>;
    onAudioData: (callback: (data: Float32Array) => void) => () => void;
  };
  asr: {
    getModels: () => Promise<WhisperModel[]>;
    selectModel: (modelId: string) => Promise<void>;
    startTranscription: () => Promise<void>;
    stopTranscription: () => Promise<void>;
    onTranscript: (callback: (segment: TranscriptSegment) => void) => () => void;
  };
  settings: {
    get: () => Promise<AppSettings>;
    set: (settings: Partial<AppSettings>) => Promise<void>;
  };
}

export interface AudioSource {
  id: string;
  name: string;
  type: "microphone" | "system";
}

export interface WhisperModel {
  id: string;
  name: string;
  size: string;
  downloaded: boolean;
}

export interface TranscriptSegment {
  text: string;
  isFinal: boolean;
  timestamp: number;
}

export interface AppSettings {
  language: string;
  modelId: string;
  audioSourceId: string;
}

declare global {
  interface Window {
    electronAPI: ElectronAPI;
  }
}
