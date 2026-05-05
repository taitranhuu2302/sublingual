import { create } from "zustand";

type DashboardState = {
  primarySource: string;
  secondarySource: string;
  isStreaming: boolean;
  telemetryLines: string[];
  setPrimarySource: (value: string) => void;
  setSecondarySource: (value: string) => void;
  toggleStreaming: () => void;
};

const initialTelemetry = [
  "> Initializing connection pool...",
  "> Audio context established (48kHz, 24-bit)",
  "> Awaiting stream token validation...",
  "> Ready for payload transmission.",
];

export const useDashboardStore = create<DashboardState>((set, get) => ({
  primarySource: "studio-mic",
  secondarySource: "system-audio",
  isStreaming: false,
  telemetryLines: initialTelemetry,
  setPrimarySource: (value) => set({ primarySource: value }),
  setSecondarySource: (value) => set({ secondarySource: value }),
  toggleStreaming: () => {
    const nextStreaming = !get().isStreaming;
    set({
      isStreaming: nextStreaming,
      telemetryLines: nextStreaming
        ? [...initialTelemetry, "> Streaming session started."]
        : [...initialTelemetry, "> Session ended by user."],
    });
  },
}));
