import { BrowserWindow, app } from "electron";
import { Worker } from "node:worker_threads";
import path from "node:path";

interface WorkerMessage {
  type: string;
  text?: string;
  isFinal?: boolean;
  timestamp?: number;
  message?: string;
  modelPath?: string;
  sampleRate?: number;
  puncModelPath?: string;
}

let worker: Worker | null = null;
let mainWindowRef: BrowserWindow | null = null;
let ready = false;

function killWorker(): void {
  if (!worker) return;
  try {
    worker.terminate();
  } catch { /* ignore */ }
  worker = null;
  ready = false;
}

export function startVosk(modelPath: string, puncModelPath: string | null, mainWindow: BrowserWindow): Promise<void> {
  return new Promise((resolve, reject) => {
    killWorker();
    mainWindowRef = mainWindow;
    ready = false;

    const workerPath = path.join(__dirname, "vosk-worker.js");
    let settled = false;

    try {
      worker = new Worker(workerPath, {
        env: {
          ...process.env,
          APP_PATH: app.getAppPath(),
          RESOURCES_PATH: app.isPackaged ? process.resourcesPath : "",
        },
        workerData: { modelPath, puncModelPath },
      });
    } catch (err) {
      console.error("[vosk] Failed to create worker:", err);
      reject(new Error("Failed to create Vosk worker: " + String(err)));
      return;
    }

    worker.on("message", (msg: WorkerMessage) => {
      switch (msg.type) {
        case "ready":
          ready = true;
          settled = true;
          resolve();
          break;
        case "transcript":
          if (mainWindowRef && !mainWindowRef.isDestroyed()) {
            mainWindowRef.webContents.send("asr:transcript", {
              text: msg.text,
              isFinal: msg.isFinal,
              timestamp: msg.timestamp,
            });
          }
          break;
        case "error":
          settled = true;
          reject(new Error(msg.message));
          break;
        case "stopped":
          ready = false;
          break;
        case "log":
          if (mainWindowRef && !mainWindowRef.isDestroyed() && !ready) {
            mainWindowRef.webContents.send("asr:model-status", { status: "loading", message: msg.message });
          }
          break;
      }
    });

    worker.on("error", (err: Error) => {
      console.error("[vosk] Worker error:", err);
      if (!settled) {
        settled = true;
        reject(err);
      }
    });

    worker.on("exit", (code: number) => {
      if (!settled) {
        settled = true;
        killWorker();
        reject(new Error(`Vosk worker exited unexpectedly with code ${code}`));
      }
    });

    worker.postMessage({ type: "start", modelPath, puncModelPath });

    setTimeout(() => {
      if (!settled) {
        settled = true;
        console.error("[vosk] Model loading timed out after 120s");
        killWorker();
        reject(new Error("Model loading timed out after 120s"));
      }
    }, 120000);
  });
}

export function isVoskRunning(): boolean {
  return ready && worker !== null;
}

export function isVoskLoading(): boolean {
  return worker !== null && !ready;
}

export function feedAudio(pcmData: Buffer) {
  if (!worker || !ready) return;
  worker.postMessage({ type: "audio", data: pcmData }, [pcmData.buffer]);
}

export const feedPcm = feedAudio;

export function stopVosk(): Promise<void> {
  return new Promise((resolve) => {
    const targetWorker = worker;
    if (!targetWorker) {
      ready = false;
      resolve();
      return;
    }

    const onMessage = (msg: WorkerMessage) => {
      if (msg.type === "stopped") {
        targetWorker.removeListener("message", onMessage);
        ready = false;
        resolve();
      }
    };
    targetWorker.on("message", onMessage);

    targetWorker.postMessage({ type: "stop" });

    setTimeout(() => {
      killWorker();
      resolve();
    }, 5000);
  });
}
