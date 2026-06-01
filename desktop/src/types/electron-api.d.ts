import type {
  AppSettings,
  OverlaySettings,
  TranslationSettings,
} from "../main/settings/settings-store";

export type { AppSettings, OverlaySettings, TranslationSettings };

export interface ElectronAPI {
  audio: {
    getSources: () => Promise<AudioSource[]>;
    getState: () => Promise<{ capturing: boolean }>;
    startCapture: (sourceId: string) => Promise<void>;
    stopCapture: () => Promise<void>;
    onAudioData: (callback: (data: Float32Array) => void) => () => void;
  };
  asr: {
    getModels: () => Promise<VoskModel[]>;
    getState: () => Promise<{ running: boolean }>;
    selectModel: (modelId: string) => Promise<void>;
    startTranscription: () => Promise<void>;
    stopTranscription: () => Promise<void>;
    onTranscript: (callback: (segment: TranscriptSegment) => void) => () => void;
  };
  settings: {
    get: () => Promise<AppSettings>;
    set: (settings: Partial<AppSettings>) => Promise<void>;
    browseDirectory: (title: string) => Promise<string | null>;
    openDirectory: (dirPath: string) => Promise<void>;
  };
  translation: {
    translate: (
      sourceText: string,
      sourceLang: string,
      targetLang: string
    ) => Promise<TranslationResult>;
    onSegmentResult: (
      callback: (result: TranslationSegmentResult) => void
    ) => () => void;
  };
  models: {
    getInstallable: () => Promise<InstallableModel[]>;
    download: (modelId: string) => Promise<void>;
    cancelDownload: () => Promise<void>;
    openFolder: () => Promise<void>;
    onDownloadProgress: (
      callback: (progress: ModelDownloadProgress) => void
    ) => () => void;
  };
  overlay: {
    show: () => Promise<void>;
    hide: () => Promise<void>;
    toggle: () => Promise<void>;
    isVisible: () => Promise<boolean>;
  };
  sessions: {
    list: (search?: string) => Promise<SessionSummary[]>;
    getTranscript: (sessionId: string) => Promise<TranscriptLine[]>;
    delete: (sessionIds: string[]) => Promise<number>;
    clearAll: () => Promise<number>;
    exportTxt: (sessionId: string) => Promise<void>;
    exportJson: (sessionId: string) => Promise<void>;
    openFolder: (sessionId: string) => Promise<void>;
  };
}

export interface AudioSource {
  id: string;
  name: string;
  type: "microphone" | "system";
}

export interface VoskModel {
  id: string;
  name: string;
  size: string;
  path: string;
  language: string;
  downloaded: boolean;
}

export interface TranscriptSegment {
  id: string;
  text: string;
  isFinal: boolean;
  timestamp: number;
}

export interface TranslationResult {
  translatedText: string;
  providerName: string;
  durationMs: number;
}

export interface TranslationSegmentResult {
  segmentId: string;
  translatedText: string;
  providerName: string;
  durationMs: number;
}

export interface InstallableModel {
  id: string;
  name: string;
  description: string;
  size: string;
  language: string;
  isInstalled: boolean;
}

export interface ModelDownloadProgress {
  modelId: string;
  percent: number;
  status: "downloading" | "completed" | "error" | "cancelled";
  error?: string;
}

export interface SessionSummary {
  id: string;
  date: string;
  duration: number;
  segmentCount: number;
  preview: string;
}

export interface TranscriptLine {
  id: string;
  text: string;
  translatedText?: string;
  timestamp: number;
  isFinal: boolean;
}

declare global {
  interface Window {
    electronAPI: ElectronAPI;
  }
}
