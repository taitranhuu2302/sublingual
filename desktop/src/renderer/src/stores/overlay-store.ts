import { create } from 'zustand';

export type OverlayDisplayMode =
  | 'bilingual'
  | 'original-only'
  | 'translated-only';

export type OverlaySettings = {
  fontSize: number;
  backgroundOpacity: number;
  lineSpacing: number;
  displayMode: OverlayDisplayMode;
  position: { x: number; y: number };
  autoHideDelay: number;
};

type OverlayState = OverlaySettings & {
  updateSettings: (settings: Partial<OverlaySettings>) => void;
  resetDefaults: () => void;
};

const defaultOverlaySettings: OverlaySettings = {
  fontSize: 24,
  backgroundOpacity: 60,
  lineSpacing: 140,
  displayMode: 'bilingual',
  position: { x: 0, y: 0 },
  autoHideDelay: 5,
};

export const useOverlayStore = create<OverlayState>((set) => ({
  ...defaultOverlaySettings,
  updateSettings: (settings) => {
    set((state) => ({
      ...state,
      ...settings,
      position: settings.position ?? state.position,
    }));
  },
  resetDefaults: () => {
    set(defaultOverlaySettings);
  },
}));

