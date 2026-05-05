export type EngineOption = {
  value: string;
  id: string;
  title: string;
  description: string;
  isActive?: boolean;
};

export type HotkeyOption = {
  action: string;
  shortcut: string;
};

export const sttEngineOptions: EngineOption[] = [
  {
    value: "vosk",
    id: "stt-vosk",
    title: "Vosk (Offline)",
    description: "Zero latency, fully local processing.",
    isActive: true,
  },
  {
    value: "whisper",
    id: "stt-whisper",
    title: "Whisper (Cloud)",
    description: "High accuracy, requires internet connection.",
  },
];

export const translationEngineOptions: EngineOption[] = [
  {
    value: "argos",
    id: "trans-argos",
    title: "Argos Translate",
    description: "Open-source offline translation based on OpenNMT.",
  },
  {
    value: "libre",
    id: "trans-libre",
    title: "LibreTranslate",
    description: "Self-hosted API with robust language pair support.",
    isActive: true,
  },
];

export const hotkeyOptions: HotkeyOption[] = [
  { action: "Toggle Recording Stream", shortcut: "Ctrl + Shift + R" },
  { action: "Toggle Translation Display", shortcut: "Alt + T" },
  { action: "Clear Current Subtitles", shortcut: "Esc" },
  { action: "Open Settings Menu", shortcut: "Ctrl + ," },
];
