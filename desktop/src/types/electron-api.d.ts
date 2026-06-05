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
    remove: (modelId: string) => Promise<void>;
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
    listFolders: () => Promise<SessionFolder[]>;
    createFolder: (name: string) => Promise<SessionFolder>;
    renameFolder: (folderId: string, name: string) => Promise<void>;
    deleteFolder: (folderId: string) => Promise<void>;
    moveSessions: (sessionIds: string[], folderId: string) => Promise<void>;
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
  speakerId?: string;
  speakerLabel?: string;
  speakerColor?: string;
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

export interface IncrementalTranslationEvent {
  utteranceId: string;
  revision: number;
  text: string;
}

export interface IncrementalFinalEvent {
  utteranceId: string;
  fullSource: string;
  fullTranslation: string;
  revision: number;
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

export interface SessionFolder {
  id: string;
  name: string;
  createdAt: string;
  sessionCount: number;
}

export interface SessionSummary {
  id: string;
  date: string;
  duration: number;
  segmentCount: number;
  preview: string;
  folderId: string;
}

export interface TranscriptLine {
  id: string;
  text: string;
  translatedText?: string;
  timestamp: number;
  isFinal: boolean;
  speakerId?: string;
  speakerLabel?: string;
  speakerColor?: string;
}

declare global {
  interface Window {
    electronAPI: ElectronAPI;
    overlayAPI: {
      getSettings: () => Promise<OverlaySettings>;
      onTranscriptLine: (cb: (line: TranscriptLine) => void) => () => void;
      onPartialUpdate: (cb: (data: { text: string; translatedText?: string }) => void) => () => void;
      onSettingsUpdate: (cb: (settings: Partial<OverlaySettings>) => void) => () => void;
      onTranslationUpdate: (cb: (data: { id: string; translatedText: string }) => void) => () => void;
      onTranslationCommitted: (cb: (data: { text: string }) => void) => () => void;
      onClear: (cb: () => void) => () => void;
      close: () => void;
    };
  }
}
