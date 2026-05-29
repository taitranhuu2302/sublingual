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
    id: "tiny",
    name: "Tiny",
    description: "Fastest, lower accuracy. Good for testing.",
    size: "75 MB",
    sizeBytes: 75_000_000,
    language: "Multilingual",
    filename: "ggml-tiny.bin",
    url: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
  },
  {
    id: "base",
    name: "Base",
    description: "Good balance of speed and accuracy.",
    size: "142 MB",
    sizeBytes: 142_000_000,
    language: "Multilingual",
    filename: "ggml-base.bin",
    url: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
  },
  {
    id: "small",
    name: "Small",
    description: "Better accuracy, moderate speed.",
    size: "466 MB",
    sizeBytes: 466_000_000,
    language: "Multilingual",
    filename: "ggml-small.bin",
    url: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
  },
  {
    id: "medium",
    name: "Medium",
    description: "High accuracy, slower processing.",
    size: "1.5 GB",
    sizeBytes: 1_500_000_000,
    language: "Multilingual",
    filename: "ggml-medium.bin",
    url: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin",
  },
  {
    id: "large-v3",
    name: "Large v3",
    description: "Highest accuracy, requires more resources.",
    size: "3.1 GB",
    sizeBytes: 3_100_000_000,
    language: "Multilingual",
    filename: "ggml-large-v3.bin",
    url: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin",
  },
];

export function getModelsDir(): string {
  return getSettings().storage.speechToTextModelsRoot;
}

export function getInstallableModels(): InstallableModel[] {
  const modelsDir = getModelsDir();
  return MODEL_CATALOG.map((source) => {
    const localPath = path.join(modelsDir, source.filename);
    return {
      ...source,
      isInstalled: fs.existsSync(localPath),
      localPath,
    };
  });
}

export function getModelSource(modelId: string): ModelSource | undefined {
  return MODEL_CATALOG.find((m) => m.id === modelId);
}
