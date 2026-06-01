import fs from "fs";
import path from "path";
import { BrowserWindow } from "electron";
import { getModelsDir, getModelSource } from "./model-source-catalog";
import AdmZip from "adm-zip";

export interface DownloadProgress {
  modelId: string;
  percent: number;
  status: "downloading" | "extracting" | "completed" | "error" | "cancelled";
  error?: string;
}

let activeAbortController: AbortController | null = null;
let activeModelId: string | null = null;

export async function downloadModel(modelId: string, mainWindow: BrowserWindow): Promise<void> {
  const source = getModelSource(modelId);
  if (!source) throw new Error(`Unknown model: ${modelId}`);

  const modelsDir = getModelsDir();
  if (!fs.existsSync(modelsDir)) {
    fs.mkdirSync(modelsDir, { recursive: true });
  }

  const extractDir = path.join(modelsDir, source.extractDir);
  const zipPath = path.join(modelsDir, `${modelId}.zip`);
  const tempZipPath = zipPath + ".tmp";

  activeAbortController = new AbortController();
  activeModelId = modelId;

  const sendProgress = (progress: DownloadProgress) => {
    if (!mainWindow.isDestroyed()) {
      mainWindow.webContents.send("models:download-progress", progress);
    }
  };

  try {
    sendProgress({ modelId, percent: 0, status: "downloading" });

    const response = await fetch(source.url, {
      signal: activeAbortController.signal,
    });

    if (!response.ok) {
      throw new Error(`Download failed: ${response.status} ${response.statusText}`);
    }

    const contentLength = Number(response.headers.get("content-length")) || source.sizeBytes;
    const reader = response.body?.getReader();
    if (!reader) throw new Error("No response body");

    const fileStream = fs.createWriteStream(tempZipPath);
    let downloaded = 0;
    let lastReportedPercent = -1;
    let done = false;

    while (!done) {
      const chunk = await reader.read();
      done = chunk.done;
      if (done) break;

      fileStream.write(Buffer.from(chunk.value));
      downloaded += chunk.value.length;

      const percent = Math.min(99, Math.floor((downloaded / contentLength) * 100));
      if (percent !== lastReportedPercent) {
        lastReportedPercent = percent;
        sendProgress({ modelId, percent, status: "downloading" });
      }
    }

    fileStream.end();
    await new Promise<void>((resolve, reject) => {
      fileStream.on("finish", resolve);
      fileStream.on("error", reject);
    });

    fs.renameSync(tempZipPath, zipPath);

    sendProgress({ modelId, percent: 100, status: "extracting" });

    const zip = new AdmZip(zipPath);
    if (fs.existsSync(extractDir)) {
      fs.rmSync(extractDir, { recursive: true, force: true });
    }
    zip.extractAllTo(modelsDir, true);
    fs.unlinkSync(zipPath);

    sendProgress({ modelId, percent: 100, status: "completed" });
  } catch (err: unknown) {
    if (fs.existsSync(tempZipPath)) {
      fs.unlinkSync(tempZipPath);
    }
    if (fs.existsSync(zipPath)) {
      fs.unlinkSync(zipPath);
    }

    if (err instanceof Error && err.name === "AbortError") {
      sendProgress({ modelId, percent: 0, status: "cancelled" });
    } else {
      const message = err instanceof Error ? err.message : String(err);
      sendProgress({ modelId, percent: 0, status: "error", error: message });
      throw err;
    }
  } finally {
    activeAbortController = null;
    activeModelId = null;
  }
}

export function cancelDownload(): void {
  if (activeAbortController) {
    activeAbortController.abort();
  }
}

export function getActiveDownloadModelId(): string | null {
  return activeModelId;
}
