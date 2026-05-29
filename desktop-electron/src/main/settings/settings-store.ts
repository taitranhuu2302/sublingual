import { app } from "electron";
import fs from "fs";
import path from "path";

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

const settingsPath = path.join(app.getPath("userData"), "settings.json");

let cache: AppSettings | null = null;

export function getSettings(): AppSettings {
  if (cache) return cache;
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
  fs.writeFileSync(settingsPath, JSON.stringify(cache, null, 2));
}
