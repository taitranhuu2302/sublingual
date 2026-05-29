import { app } from "electron";
import fs from "fs";
import path from "path";
import os from "os";

export interface AppSettings {
  language: string;
  modelId: string;
  audioSourceId: string;
}

const DEFAULTS: AppSettings = {
  language: "en",
  modelId: "",
  audioSourceId: "",
};

const SETTINGS_DIR = path.join(os.homedir(), ".sublingual");
const settingsPath = path.join(SETTINGS_DIR, "settings.json");

let cache: AppSettings | null = null;

export function getSettings(): AppSettings {
  if (cache) return cache;
  
  // Ensure directory exists
  if (!fs.existsSync(SETTINGS_DIR)) {
    fs.mkdirSync(SETTINGS_DIR, { recursive: true });
  }
  
  try {
    const raw = fs.readFileSync(settingsPath, "utf-8");
    cache = { ...DEFAULTS, ...JSON.parse(raw) };
  } catch {
    cache = { ...DEFAULTS };
  }
  return cache!;
}

export function setSettings(partial: Partial<AppSettings>): void {
  cache = { ...getSettings(), ...partial };
  
  // Ensure directory exists
  if (!fs.existsSync(SETTINGS_DIR)) {
    fs.mkdirSync(SETTINGS_DIR, { recursive: true });
  }
  
  fs.writeFileSync(settingsPath, JSON.stringify(cache, null, 2));
}
