import fs from "fs";
import { getSettings, setSettings } from "../settings/settings-store";
import { getInstallableModels } from "./model-source-catalog";

export interface VoskModel {
  id: string;
  name: string;
  size: string;
  path: string;
  language: string;
  downloaded: boolean;
}

class ModelManager {
  listModels(): VoskModel[] {
    return getInstallableModels().map((m) => ({
      id: m.id,
      name: m.name,
      size: m.size,
      language: m.language,
      path: m.localPath,
      downloaded: m.isInstalled,
    }));
  }

  selectModel(modelId: string): void {
    const settings = getSettings();
    setSettings({ speechToText: { ...settings.speechToText, selectedModel: modelId } });
  }

  getSelectedModel(): VoskModel | null {
    const settings = getSettings();
    return this.listModels().find((m) => m.id === settings.speechToText.selectedModel) ?? null;
  }

  removeModel(modelId: string): void {
    const model = this.listModels().find((m) => m.id === modelId);
    if (!model || !model.downloaded) return;

    fs.rmSync(model.path, { recursive: true, force: true });

    const settings = getSettings();
    if (settings.speechToText.selectedModel === modelId) {
      setSettings({ speechToText: { ...settings.speechToText, selectedModel: "" } });
    }
  }

  getSpkModel(): VoskModel | null {
    return this.listModels().find((m) => m.id === "vosk-model-spk-0.4" && m.downloaded) ?? null;
  }
}

let instance: ModelManager | null = null;
export function getModelManager(): ModelManager {
  if (!instance) instance = new ModelManager();
  return instance;
}
