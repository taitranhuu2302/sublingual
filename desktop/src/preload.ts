import { contextBridge, ipcRenderer } from 'electron';

type STTEngine = 'vosk' | 'whisper';

type StartSessionConfig = {
  deviceId: string;
  sttEngine: STTEngine;
};

type Session = {
  id: string;
  title: string;
  startedAt: string;
  endedAt?: string;
  sttEngine: STTEngine;
  languagePair: string;
};

type OverlaySettings = {
  fontSize: number;
  backgroundOpacity: number;
  lineSpacing: number;
  displayMode: 'bilingual' | 'original-only' | 'translated-only';
  position: { x: number; y: number };
  autoHideDelay: number;
};

type BackendStatus = 'unknown' | 'starting' | 'healthy' | 'unreachable';

export type ElectronAPI = {
  getAudioDevices: () => Promise<MediaDeviceInfo[]>;
  startSession: (config: StartSessionConfig) => void;
  stopSession: () => void;
  onSubtitleUpdate: (callback: (data: unknown) => void) => void;
  onBackendStatus: (callback: (status: BackendStatus) => void) => void;
  getSessionHistory: () => Promise<Session[]>;
  updateOverlaySettings: (settings: OverlaySettings) => void;
};

const electronAPI: ElectronAPI = {
  getAudioDevices: async () => {
    const devices = await ipcRenderer.invoke('get-audio-devices');
    return devices as MediaDeviceInfo[];
  },
  startSession: (config) => {
    ipcRenderer.send('start-session', config);
  },
  stopSession: () => {
    ipcRenderer.send('stop-session');
  },
  onSubtitleUpdate: (callback) => {
    ipcRenderer.on('subtitle-update', (_event, data) => {
      callback(data);
    });
  },
  onBackendStatus: (callback) => {
    ipcRenderer.on('backend-status', (_event, status: BackendStatus) => {
      callback(status);
    });
  },
  getSessionHistory: async () => {
    const sessions = await ipcRenderer.invoke('get-session-history');
    return sessions as Session[];
  },
  updateOverlaySettings: (settings) => {
    ipcRenderer.send('update-overlay-settings', settings);
  },
};

contextBridge.exposeInMainWorld('electronAPI', electronAPI);
