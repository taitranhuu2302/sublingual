import { create } from 'zustand';

export type AudioInputDevice = {
  id: string;
  label: string;
};

export type TelemetryLine = {
  id: string;
  message: string;
};

type DashboardState = {
  selectedDeviceId: string | null;
  devices: AudioInputDevice[];
  isStreaming: boolean;
  telemetryLines: TelemetryLine[];
  meterLevel: number;
  setDevices: (devices: AudioInputDevice[]) => void;
  setSelectedDeviceId: (value: string) => void;
  setStreaming: (value: boolean) => void;
  appendTelemetry: (line: string) => void;
  setMeterLevel: (value: number) => void;
  resetTelemetry: () => void;
};

const initialTelemetry = ['> Dashboard ready. Waiting for microphone access.'];
const toTelemetryLine = (message: string): TelemetryLine => ({
  id: `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`,
  message,
});

export const useDashboardStore = create<DashboardState>((set) => ({
  selectedDeviceId: null,
  devices: [],
  isStreaming: false,
  telemetryLines: initialTelemetry.map(toTelemetryLine),
  meterLevel: 0,
  setDevices: (devices) =>
    set((state) => ({
      devices,
      selectedDeviceId:
        state.selectedDeviceId ?? (devices.length > 0 ? devices[0].id : null),
    })),
  setSelectedDeviceId: (value) => set({ selectedDeviceId: value }),
  setStreaming: (value) => set({ isStreaming: value }),
  appendTelemetry: (line) =>
    set((state) => ({
      telemetryLines: [...state.telemetryLines.slice(-6), toTelemetryLine(line)],
    })),
  setMeterLevel: (value) => set({ meterLevel: value }),
  resetTelemetry: () =>
    set({ telemetryLines: initialTelemetry.map(toTelemetryLine) }),
}));
