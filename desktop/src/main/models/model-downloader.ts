import fs from "fs";
import path from "path";
import { BrowserWindow } from "electron";
import { getModelsDir, getModelSource } from "./model-source-catalog";
import AdmZip from "adm-zip";

export interface DownloadProgress {
  modelId: string;
  percent: number;
  status: "downloading" | "completed" | "error" | "cancelled";
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

  // For Vosk, filename is a zip, target is a directory
  const zipPath = path.join(modelsDir, source.filename);
  const extractDir = path.join(modelsDir, source.filename.replace(/\.zip$/, ""));

  // Skip if already installed
  if (fs.existsSync(path.join(extractDir, "am", "final.mdl"))) {
    return;
  }

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

    const fileStream = fs.createWriteStream(zipPath);
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

    // Extract zip — Vosk zips contain a top-level dir,
    // so we extract then flatten if needed
    const zip = new AdmZip(zipPath);
    zip.extractAllTo(extractDir, true);

    // Check if zip had a top-level dir (single subdirectory)
    const entries = fs.readdirSync(extractDir);
    if (entries.length === 1) {
      const nested = path.join(extractDir, entries[0]);
      if (fs.statSync(nested).isDirectory()) {
        const tmpDir = extractDir + "_tmp";
        fs.renameSync(extractDir, tmpDir);
        fs.renameSync(path.join(tmpDir, entries[0]), extractDir);
        fs.rmSync(tmpDir, { recursive: true, force: true });
      }
    }

    // Remove zip file
    fs.unlinkSync(zipPath);

    sendProgress({ modelId, percent: 100, status: "completed" });
  } catch (err: unknown) {
    // Clean up zip if it exists
    if (fs.existsSync(zipPath)) {
      fs.unlinkSync(zipPath);
    }
    // Clean up partial extract
    if (fs.existsSync(extractDir)) {
      fs.rmSync(extractDir, { recursive: true, force: true });
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
