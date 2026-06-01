import fs from "fs";
import path from "path";
import { getSettings } from "../settings/settings-store";

export interface ModelSource {
  id: string;
  name: string;
  description: string;
  size: string;
  sizeBytes: number;
  language: string;
  url: string;
  extractDir: string;
}

export interface InstallableModel extends ModelSource {
  isInstalled: boolean;
  localPath: string;
}

const MODEL_CATALOG: ModelSource[] = [
  {
    id: "vosk-small-en-us-0.15",
    name: "English (US) Small",
    description: "Lightweight US English model. Good for real-time.",
    size: "40 MB",
    sizeBytes: 40_000_000,
    language: "en",
    url: "https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip",
    extractDir: "vosk-model-small-en-us-0.15",
  },
  {
    id: "vosk-model-vn-0.4",
    name: "Vietnamese",
    description: "Vietnamese speech model.",
    size: "50 MB",
    sizeBytes: 50_000_000,
    language: "vi",
    url: "https://alphacephei.com/vosk/models/vosk-model-vn-0.4.zip",
    extractDir: "vosk-model-vn-0.4",
  },
];

export function getModelsDir(): string {
  return getSettings().storage.speechToTextModelsRoot;
}

export function getInstallableModels(): InstallableModel[] {
  const modelsDir = getModelsDir();
  return MODEL_CATALOG.map((source) => {
    const localPath = path.join(modelsDir, source.extractDir);
    return {
      ...source,
      isInstalled: fs.existsSync(path.join(localPath, "am", "final.mdl")),
      localPath,
    };
  });
}

export function getModelSource(modelId: string): ModelSource | undefined {
  return MODEL_CATALOG.find((m) => m.id === modelId);
}
