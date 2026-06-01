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
  filename: string;
  url: string;
}

export interface InstallableModel extends ModelSource {
  isInstalled: boolean;
  localPath: string;
}

const MODEL_CATALOG: ModelSource[] = [
  {
    id: "vosk-model-small-en-us-0.15",
    name: "English (Small)",
    description: "Lightweight US English model, ideal for desktop. ~40MB.",
    size: "40 MB",
    sizeBytes: 40_000_000,
    language: "en",
    filename: "vosk-model-small-en-us-0.15.zip",
    url: "https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip",
  },
  {
    id: "vosk-model-en-us-0.22",
    name: "English (Accurate)",
    description: "Accurate US English model for servers and high-end desktops. ~1.8GB.",
    size: "1.8 GB",
    sizeBytes: 1_800_000_000,
    language: "en",
    filename: "vosk-model-en-us-0.22.zip",
    url: "https://alphacephei.com/vosk/models/vosk-model-en-us-0.22.zip",
  },
  {
    id: "vosk-model-en-us-0.22-lgraph",
    name: "English (Dynamic)",
    description: "Big US English model with dynamic graph. ~128MB.",
    size: "128 MB",
    sizeBytes: 128_000_000,
    language: "en",
    filename: "vosk-model-en-us-0.22-lgraph.zip",
    url: "https://alphacephei.com/vosk/models/vosk-model-en-us-0.22-lgraph.zip",
  },
  {
    id: "vosk-model-small-vn-0.4",
    name: "Vietnamese (Small)",
    description: "Lightweight Vietnamese model. ~32MB.",
    size: "32 MB",
    sizeBytes: 32_000_000,
    language: "vi",
    filename: "vosk-model-small-vn-0.4.zip",
    url: "https://alphacephei.com/vosk/models/vosk-model-small-vn-0.4.zip",
  },
  {
    id: "vosk-model-vn-0.4",
    name: "Vietnamese",
    description: "Bigger Vietnamese model for server. ~78MB.",
    size: "78 MB",
    sizeBytes: 78_000_000,
    language: "vi",
    filename: "vosk-model-vn-0.4.zip",
    url: "https://alphacephei.com/vosk/models/vosk-model-vn-0.4.zip",
  },
];

export function getModelsDir(): string {
  return getSettings().storage.speechToTextModelsRoot;
}

export function getInstallableModels(): InstallableModel[] {
  const modelsDir = getModelsDir();
  return MODEL_CATALOG.map((source) => {
    // Vosk models are extracted to directories (filename without .zip)
    const dirName = source.filename.replace(/\.zip$/, "");
    const localPath = path.join(modelsDir, dirName);
    // Detect by checking for the model marker file
    const isInstalled = fs.existsSync(path.join(localPath, "am", "final.mdl"));
    return {
      ...source,
      isInstalled,
      localPath,
    };
  });
}

export function getModelSource(modelId: string): ModelSource | undefined {
  return MODEL_CATALOG.find((m) => m.id === modelId);
}
