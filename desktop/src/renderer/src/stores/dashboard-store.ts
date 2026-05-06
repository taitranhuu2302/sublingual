import { create } from 'zustand';

export type AudioInputDevice = {
  id: string;
  label: string;
};

export type DesktopAudioSource = {
  id: string;
  label: string;
};

export type AudioSourceMode = 'microphone' | 'system';

export type TelemetryLine = {
  id: string;
  message: string;
};

type DashboardState = {
  sourceMode: AudioSourceMode;
  selectedDeviceId: string | null;
  selectedDesktopSourceId: string | null;
  devices: AudioInputDevice[];
  desktopSources: DesktopAudioSource[];
  isStreaming: boolean;
  telemetryLines: TelemetryLine[];
  meterLevel: number;
  systemSourceStatus: 'idle' | 'loading' | 'ready' | 'unsupported' | 'missing';
  setSourceMode: (mode: AudioSourceMode) => void;
  setDevices: (devices: AudioInputDevice[]) => void;
  setDesktopSources: (sources: DesktopAudioSource[]) => void;
  setSelectedDeviceId: (value: string) => void;
  setSelectedDesktopSourceId: (value: string) => void;
  setStreaming: (value: boolean) => void;
  appendTelemetry: (line: string) => void;
  setMeterLevel: (value: number) => void;
  setSystemSourceStatus: (
    status: DashboardState['systemSourceStatus'],
  ) => void;
  resetTelemetry: () => void;
};

const initialTelemetry = ['> Dashboard ready. Waiting for microphone access.'];
const toTelemetryLine = (message: string): TelemetryLine => ({
  id: `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`,
  message,
});

export const useDashboardStore = create<DashboardState>((set) => ({
  sourceMode: 'microphone',
  selectedDeviceId: null,
  selectedDesktopSourceId: null,
  devices: [],
  desktopSources: [],
  isStreaming: false,
  telemetryLines: initialTelemetry.map(toTelemetryLine),
  meterLevel: 0,
  systemSourceStatus: 'idle',
  setSourceMode: (mode) => set({ sourceMode: mode }),
  setDevices: (devices) =>
    set((state) => ({
      devices,
      selectedDeviceId:
        state.selectedDeviceId ?? (devices.length > 0 ? devices[0].id : null),
    })),
  setDesktopSources: (desktopSources) =>
    set((state) => ({
      desktopSources,
      selectedDesktopSourceId:
        state.selectedDesktopSourceId ??
        (desktopSources.length > 0 ? desktopSources[0].id : null),
      systemSourceStatus:
        desktopSources.length > 0 ? 'ready' : state.systemSourceStatus,
    })),
  setSelectedDeviceId: (value) => set({ selectedDeviceId: value }),
  setSelectedDesktopSourceId: (value) => set({ selectedDesktopSourceId: value }),
  setStreaming: (value) => set({ isStreaming: value }),
  appendTelemetry: (line) =>
    set((state) => ({
      telemetryLines: [...state.telemetryLines.slice(-6), toTelemetryLine(line)],
    })),
  setMeterLevel: (value) => set({ meterLevel: value }),
  setSystemSourceStatus: (status) => set({ systemSourceStatus: status }),
  resetTelemetry: () =>
    set({ telemetryLines: initialTelemetry.map(toTelemetryLine) }),
}));
