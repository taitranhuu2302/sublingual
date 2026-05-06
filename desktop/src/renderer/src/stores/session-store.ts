import { create } from 'zustand';

export type SessionStatus = 'idle' | 'connecting' | 'streaming' | 'error';
export type STTEngine = 'vosk' | 'whisper';

export type SubtitleItem = {
  id: string;
  original: string;
  translated: string;
  timestamp: string;
};

type SessionState = {
  status: SessionStatus;
  selectedDeviceId: string | null;
  sttEngine: STTEngine;
  translationEnabled: boolean;
  currentPartial: string;
  subtitles: SubtitleItem[];
  error: string | null;
  setStatus: (status: SessionStatus) => void;
  setDevice: (deviceId: string | null) => void;
  setSTTEngine: (engine: STTEngine) => void;
  startSession: () => void;
  stopSession: () => void;
  addSubtitle: (subtitle: SubtitleItem) => void;
  updatePartial: (text: string) => void;
  setError: (message: string | null) => void;
  clearSubtitles: () => void;
};

const initialState: Omit<
  SessionState,
  | 'setDevice'
  | 'setStatus'
  | 'setSTTEngine'
  | 'startSession'
  | 'stopSession'
  | 'addSubtitle'
  | 'updatePartial'
  | 'setError'
  | 'clearSubtitles'
> = {
  status: 'idle',
  selectedDeviceId: null,
  sttEngine: 'vosk',
  translationEnabled: true,
  currentPartial: '',
  subtitles: [],
  error: null,
};

export const useSessionStore = create<SessionState>((set) => ({
  ...initialState,
  setStatus: (status) => {
    set({ status });
  },
  setDevice: (deviceId) => {
    set({ selectedDeviceId: deviceId });
  },
  setSTTEngine: (engine) => {
    set({ sttEngine: engine });
  },
  startSession: () => {
    set({ status: 'connecting', error: null });
  },
  stopSession: () => {
    set({
      status: 'idle',
      currentPartial: '',
      error: null,
    });
  },
  addSubtitle: (subtitle) => {
    set((state) => ({
      subtitles: [...state.subtitles, subtitle],
      currentPartial: '',
      status: 'streaming',
    }));
  },
  updatePartial: (text) => {
    set({
      currentPartial: text,
      status: 'streaming',
    });
  },
  setError: (message) => {
    set({
      error: message,
      status: message ? 'error' : 'idle',
    });
  },
  clearSubtitles: () => {
    set({
      subtitles: [],
      currentPartial: '',
    });
  },
}));

