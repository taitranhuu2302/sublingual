import fs from "fs";
import path from "path";
import { getSettings, setSettings } from "../settings/settings-store";
import { getInstallableModels, getModelsDir } from "./model-source-catalog";

export interface WhisperModel {
  id: string;
  name: string;
  size: string;
  path: string;
  downloaded: boolean;
}

class ModelManager {
  listModels(): WhisperModel[] {
    const modelsDir = getModelsDir();
    if (!fs.existsSync(modelsDir)) {
      fs.mkdirSync(modelsDir, { recursive: true });
    }

    return getInstallableModels().map((m) => ({
      id: m.id,
      name: `${m.name} (${m.size})`,
      size: m.size,
      path: m.localPath,
      downloaded: m.isInstalled,
    }));
  }

  selectModel(modelId: string): void {
    const settings = getSettings();
    setSettings({ speechToText: { ...settings.speechToText, selectedModel: modelId } });
  }

  getSelectedModel(): WhisperModel | null {
    const settings = getSettings();
    const modelId = settings.speechToText.selectedModel;
    if (!modelId) return null;
    return this.listModels().find((m) => m.id === modelId) ?? null;
  }
}

let instance: ModelManager | null = null;
export function getModelManager(): ModelManager {
  if (!instance) instance = new ModelManager();
  return instance;
}
