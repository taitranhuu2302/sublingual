import { app } from "electron";
import fs from "fs";
import path from "path";
import os from "os";

export interface StorageSettings {
  sessionsRoot: string;
  speechToTextModelsRoot: string;
}

export interface OverlaySettings {
  fontSize: number;
  lineHeight: number;
  width: number;
  height: number;
  theme: "Dark" | "Light";
  opacity: number;
  showTranslation: boolean;
  positionX: number | null;
  positionY: number | null;
}

export interface SpeechToTextSettings {
  selectedModel: string;
  sourceLanguage: string;
  speakerModel?: string;
  maxSpeakers?: number;
}

export interface TranslationProviderGoogle {
  endpoint: string;
}

export interface TranslationProviderLocal {
  baseUrl: string;
}

export interface TranslationSettings {
  enabled: boolean;
  provider: "google-free" | "translate-local";
  targetLanguage: string;
  google: TranslationProviderGoogle;
  local: TranslationProviderLocal;
}

export interface AppSettings {
  storage: StorageSettings;
  overlay: OverlaySettings;
  speechToText: SpeechToTextSettings;
  translation: TranslationSettings;
}

const SETTINGS_DIR = path.join(os.homedir(), ".sublingual");

const DEFAULTS: AppSettings = {
  storage: {
    sessionsRoot: path.join(SETTINGS_DIR, "sessions"),
    speechToTextModelsRoot: path.join(SETTINGS_DIR, "models"),
  },
  overlay: {
    fontSize: 26,
    lineHeight: 1.35,
    width: 720,
    height: 200,
    theme: "Dark",
    opacity: 0.88,
    showTranslation: true,
    positionX: null,
    positionY: null,
  },
  speechToText: {
    selectedModel: "",
    sourceLanguage: "en",
    speakerModel: "",
    maxSpeakers: 4,
  },
  translation: {
    enabled: true,
    provider: "google-free",
    targetLanguage: "vi",
    google: { endpoint: "https://translate.googleapis.com/translate_a/single" },
    local: { baseUrl: "http://127.0.0.1:3333" },
  },
};

const settingsPath = path.join(SETTINGS_DIR, "settings.json");

let cache: AppSettings | null = null;

function ensureDir() {
  if (!fs.existsSync(SETTINGS_DIR)) {
    fs.mkdirSync(SETTINGS_DIR, { recursive: true });
  }
}

function deepMerge(target: Record<string, unknown>, source: Record<string, unknown>): Record<string, unknown> {
  const result = { ...target };
  for (const key of Object.keys(source)) {
    if (
      source[key] &&
      typeof source[key] === "object" &&
      !Array.isArray(source[key]) &&
      target[key] &&
      typeof target[key] === "object" &&
      !Array.isArray(target[key])
    ) {
      result[key] = deepMerge(target[key] as Record<string, unknown>, source[key] as Record<string, unknown>);
    } else {
      result[key] = source[key];
    }
  }
  return result;
}

export function getSettings(): AppSettings {
  if (cache) return cache;
  ensureDir();
  try {
    const raw = fs.readFileSync(settingsPath, "utf-8");
    cache = deepMerge(DEFAULTS as unknown as Record<string, unknown>, JSON.parse(raw)) as unknown as AppSettings;
  } catch {
    cache = { ...DEFAULTS };
  }
  return cache!;
}

export function setSettings(partial: Partial<AppSettings>): void {
  const current = getSettings();
  cache = deepMerge(current as unknown as Record<string, unknown>, partial as unknown as Record<string, unknown>) as unknown as AppSettings;
  ensureDir();
  fs.writeFileSync(settingsPath, JSON.stringify(cache, null, 2));
}

export function getSettingsDir(): string {
  return SETTINGS_DIR;
}
