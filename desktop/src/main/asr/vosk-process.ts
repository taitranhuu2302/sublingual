import { BrowserWindow, app } from "electron";
import { fork } from "node:child_process";
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

let worker: any = null;
let mainWindowRef: BrowserWindow | null = null;
let ready = false;

function killWorker(): void {
  if (!worker) return;
  try {
    worker.kill();
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
      worker = fork(workerPath, [], {
        env: {
          ...process.env,
          APP_PATH: app.getAppPath(),
          RESOURCES_PATH: app.isPackaged ? process.resourcesPath : "",
        },
        silent: true,
      });
    } catch (err) {
      console.error("[vosk] Failed to fork worker:", err);
      reject(new Error("Failed to fork Vosk worker: " + String(err)));
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
      }
    });

    worker.on("error", (err: Error) => {
      console.error("[vosk] Worker error:", err);
    });

    worker.on("exit", (code: number | null) => {
      if (!settled) {
        settled = true;
        reject(new Error(`Vosk worker exited unexpectedly with code ${code}`));
      }
    });

    worker.stdout?.on("data", (data: Buffer) => {
      console.log("[vosk:worker] " + data.toString().trim());
    });

    worker.stderr?.on("data", (data: Buffer) => {
      const text = data.toString().trim();
      if (text.includes("Discarding word-ids")) return;
      console.error("[vosk:worker:err] " + text);
    });

    worker.send({ type: "start", modelPath, puncModelPath });

    setTimeout(() => {
      if (!settled) {
        settled = true;
        console.error("[vosk] Model loading timed out after 120s");
        if (worker) {
          worker.kill();
          worker = null;
        }
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
  worker.send({ type: "audio", data: pcmData });
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

    targetWorker.send({ type: "stop" });

    setTimeout(() => {
      targetWorker.kill();
      worker = null;
      ready = false;
      resolve();
    }, 5000);
  });
}
