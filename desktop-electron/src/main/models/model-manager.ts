import { app } from "electron";
import fs from "fs";
import path from "path";
import { getSettings, setSettings } from "../settings/settings-store";

export interface WhisperModel {
  id: string;
  name: string;
  size: string;
  path: string;
  downloaded: boolean;
}

const MODELS_DIR = path.join(app.getPath("userData"), "models");

// Known whisper.cpp model catalog
const MODEL_CATALOG: Array<{ id: string; name: string; size: string; filename: string }> = [
  { id: "tiny", name: "Tiny (75MB)", size: "75MB", filename: "ggml-tiny.bin" },
  { id: "base", name: "Base (142MB)", size: "142MB", filename: "ggml-base.bin" },
  { id: "small", name: "Small (466MB)", size: "466MB", filename: "ggml-small.bin" },
  { id: "medium", name: "Medium (1.5GB)", size: "1.5GB", filename: "ggml-medium.bin" },
  { id: "large", name: "Large (3.1GB)", size: "3.1GB", filename: "ggml-large-v3.bin" },
];

class ModelManager {
  constructor() {
    if (!fs.existsSync(MODELS_DIR)) {
      fs.mkdirSync(MODELS_DIR, { recursive: true });
    }
  }

  listModels(): WhisperModel[] {
    return MODEL_CATALOG.map((m) => ({
      id: m.id,
      name: m.name,
      size: m.size,
      path: path.join(MODELS_DIR, m.filename),
      downloaded: fs.existsSync(path.join(MODELS_DIR, m.filename)),
    }));
  }

  selectModel(modelId: string): void {
    setSettings({ modelId });
  }

  getSelectedModel(): WhisperModel | null {
    const settings = getSettings();
    return this.listModels().find((m) => m.id === settings.modelId) ?? null;
  }
}

let instance: ModelManager | null = null;
export function getModelManager(): ModelManager {
  if (!instance) instance = new ModelManager();
  return instance;
}
