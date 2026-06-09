import { app, BrowserWindow } from "electron";
import { ChildProcess, spawn } from "node:child_process";
import path from "node:path";
import http from "node:http";
import os from "node:os";
import { getMainLogger } from "../utils/logger";
import { getSettings } from "../settings/settings-store";

export interface TranslateProcessStatus {
  status: "running" | "starting" | "stopped" | "error";
  pid: number | null;
  uptime: number | null;
  loadedModels: string[];
  error: string | null;
  modelsAvailable: boolean;
}

export interface DownloadStatus {
  status: "idle" | "downloading" | "completed" | "error";
  percent: number;
  error: string | null;
}

const HEALTH_CHECK_INTERVAL = 5_000; // 5s
const HEALTH_START_TIMEOUT = 30_000; // 30s
const KILL_TIMEOUT = 5_000; // 5s

let translateProc: ChildProcess | null = null;
let status: TranslateProcessStatus = {
  status: "stopped",
  pid: null,
  uptime: null,
  loadedModels: [],
  error: null,
  modelsAvailable: false,
};
let startTime: number | null = null;
let healthInterval: ReturnType<typeof setInterval> | null = null;
let mainWindowRef: BrowserWindow | null = null;

function setStatus(update: Partial<TranslateProcessStatus>): void {
  status = { ...status, ...update };
  if (mainWindowRef && !mainWindowRef.isDestroyed()) {
    mainWindowRef.webContents.send("translate:status-change", status);
  }
}

function resolveCommand(): { bin: string; args: string[]; cwd?: string } {
  const settings = getSettings().translation.local;
  const name = process.platform === "win32" ? "translate-service.exe" : "translate-service";

  // Always use PyInstaller exe (dev: desktop/bin/translate/, prod: resourcesPath/bin/translate/)
  const exePath = app.isPackaged
    ? path.join(process.resourcesPath, "bin", "translate", name)
    : path.resolve(__dirname, "..", "..", "bin", "translate", name);

  return {
    bin: exePath,
    args: [
      "--host", "127.0.0.1",
      "--port", "3333",
      "--models-dir", settings.modelsDir,
      "--log-dir", path.join(os.homedir(), ".sublingual", "logs", "translate"),
    ],
  };
}

function pollHealth(): void {
  const settings = getSettings().translation.local;
  const baseUrl = settings.baseUrl.replace(/\/+$/, "");
  const logger = getMainLogger({ tag: "translate" });

  try {
    const req = http.get(`${baseUrl}/health`, (res) => {
      let data = "";
      res.on("data", (chunk: Buffer) => { data += chunk.toString(); });
      res.on("end", () => {
        try {
          const health = JSON.parse(data);
          if (health.status === "ok") {
            if (status.status !== "running") {
              const uptime = startTime ? Math.round((Date.now() - startTime) / 1000) : null;
              setStatus({
                status: "running",
                loadedModels: health.loaded_models ?? [],
                error: null,
                uptime,
                modelsAvailable: (health as Record<string, unknown>).models_available === true,
              });
      logger.info(
        `translate service ready (pid=${translateProc?.pid}, loaded_models=${health.loaded_models?.join(",") || "none"})`
      );
      mainWindowRef?.webContents.send("translate:service-ready", {
        pid: translateProc?.pid,
        loadedModels: health.loaded_models ?? [],
      });
      // Stop continuous polling once running — poll on-demand from UI
      if (healthInterval) {
        clearInterval(healthInterval);
        healthInterval = null;
      }
    }
    // Always update modelsAvailable on each health poll
    if (status.status === "running") {
              const modelsAvail = (health as Record<string, unknown>).models_available === true;
              if (status.modelsAvailable !== modelsAvail) {
                setStatus({ modelsAvailable: modelsAvail });
              }
            }
          }
        } catch {
          // not ready yet, keep polling
        }
      });
    });

    req.on("error", () => {
      // connection refused = service not ready yet
    });

    req.setTimeout(1000, () => req.destroy());
  } catch {
    // malformed baseUrl or other sync error
  }
}

function monitorStdio(child: ChildProcess): void {
  const logger = getMainLogger({ tag: "translate" });

  const isHealthLog = (line: string): boolean =>
    line.includes('"GET /health') || line.includes('"HEAD /health');

  child.stdout?.on("data", (data: Buffer) => {
    const lines = data.toString().trim().split("\n");
    for (const line of lines) {
      if (line && !isHealthLog(line)) {
        logger.info(line);
        mainWindowRef?.webContents.send("translate:log", { line });
      }
    }
  });

  child.stderr?.on("data", (data: Buffer) => {
    const lines = data.toString().trim().split("\n");
    for (const line of lines) {
      if (line && !isHealthLog(line)) {
        logger.warn(line);
        mainWindowRef?.webContents.send("translate:log", { line });
      }
    }
  });
}

export function getTranslateStatus(): TranslateProcessStatus {
  return { ...status };
}

export function pollHealthNow(): void {
  if (status.status === "running") {
    pollHealth();
  }
}

export async function downloadTranslateModel(): Promise<void> {
  const settings = getSettings().translation.local;
  const baseUrl = settings.baseUrl.replace(/\/+$/, "");
  const logger = getMainLogger({ tag: "translate" });

  const resp = await fetch(`${baseUrl}/models/download`, { method: "POST" });
  if (resp.status === 409) {
    logger.warn("model download already in progress");
    return;
  }
  if (!resp.ok) {
    const body = await resp.text();
    throw new Error(`Download request failed: ${resp.status} ${body}`);
  }
  logger.info("model download started");

  // Poll download status until complete
  const pollInterval = setInterval(async () => {
    try {
      const statusResp = await fetch(`${baseUrl}/models/download/status`);
      const dl: DownloadStatus = await statusResp.json();

      mainWindowRef?.webContents.send("translate:download-progress", dl);

      if (dl.status === "completed") {
        clearInterval(pollInterval);
        logger.info("model download completed, restarting service...");
        mainWindowRef?.webContents.send("translate:download-progress", dl);
        // Auto-restart to load the newly downloaded models
        await restartTranslate();
      } else if (dl.status === "error") {
        clearInterval(pollInterval);
        mainWindowRef?.webContents.send("translate:download-progress", dl);
        logger.error("model download failed: %s", dl.error);
      }
    } catch {
      // ignore poll errors (service might be restarting)
    }
  }, 2000);
}

export function getDownloadStatus(): DownloadStatus {
  return { status: "idle", percent: 0, error: null };
}

export function startTranslate(mainWindow: BrowserWindow): void {
  mainWindowRef = mainWindow;
  const logger = getMainLogger({ tag: "translate" });

  if (translateProc) {
    logger.warn("translate service is already running, skipping start");
    return;
  }

  try {
    const cmd = resolveCommand();
    logger.info(`starting translate service: ${cmd.bin} ${cmd.args.join(" ")}`);

    translateProc = spawn(cmd.bin, cmd.args, {
      env: { ...process.env, APP_PATH: app.getAppPath() },
      stdio: ["ignore", "pipe", "pipe"],
    });

    setStatus({ status: "starting", pid: translateProc.pid, uptime: null, error: null, loadedModels: [], modelsAvailable: false });
    startTime = Date.now();

    monitorStdio(translateProc);

    // Health polling
    healthInterval = setInterval(pollHealth, HEALTH_CHECK_INTERVAL);

    // Start timeout
    setTimeout(() => {
      if (status.status === "starting") {
        setStatus({ status: "error", error: "Health check timed out after 30s" });
        mainWindowRef?.webContents.send("translate:service-error", { error: "Health check timed out after 30s" });
        logger.error("translate service health check timed out");
        killProcess();
      }
    }, HEALTH_START_TIMEOUT);

    translateProc.on("exit", (code, signal) => {
      logger.warn(`translate service exited (code=${code}, signal=${signal})`);
      if (healthInterval) clearInterval(healthInterval);
      healthInterval = null;

      if (status.status !== "stopped") {
        setStatus({
          status: "error",
          pid: null,
          uptime: null,
          loadedModels: [],
          modelsAvailable: false,
          error: `Process exited with code ${code} signal ${signal}`,
        });
        mainWindowRef?.webContents.send("translate:service-error", {
          error: `Process exited with code ${code} signal ${signal}`,
        });
      }

      translateProc = null;
      startTime = null;
    });

    translateProc.on("error", (err) => {
      logger.error("translate service spawn error", err);
      if (healthInterval) clearInterval(healthInterval);
      healthInterval = null;
      setStatus({ status: "error", pid: null, error: err.message });
      mainWindowRef?.webContents.send("translate:service-error", { error: err.message });
      translateProc = null;
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    logger.error("failed to start translate service", err);
    setStatus({ status: "error", pid: null, error: message });
    mainWindowRef?.webContents.send("translate:service-error", { error: message });
  }
}

export async function stopTranslate(): Promise<void> {
  const logger = getMainLogger({ tag: "translate" });

  if (!translateProc) {
    setStatus({ status: "stopped", pid: null, uptime: null, loadedModels: [], error: null, modelsAvailable: false });
    return;
  }

  if (healthInterval) {
    clearInterval(healthInterval);
    healthInterval = null;
  }

  logger.info("stopping translate service");
  setStatus({ status: "stopped", pid: null, uptime: null, loadedModels: [], error: null, modelsAvailable: false });

  const child = translateProc;
  translateProc = null;
  startTime = null;

  child.kill("SIGTERM");

  await new Promise<void>((resolve) => {
    const timer = setTimeout(() => {
      child.kill("SIGKILL");
      resolve();
    }, KILL_TIMEOUT);

    child.on("exit", () => {
      clearTimeout(timer);
      resolve();
    });
  });

  mainWindowRef?.webContents.send("translate:service-stopped");
}

export async function restartTranslate(): Promise<void> {
  await stopTranslate();
  if (mainWindowRef) {
    startTranslate(mainWindowRef);
  }
}

function killProcess(): void {
  const child = translateProc;
  if (!child) return;
  if (healthInterval) {
    clearInterval(healthInterval);
    healthInterval = null;
  }
  translateProc = null;
  startTime = null;
  child.kill("SIGTERM");
  setTimeout(() => {
    child.kill("SIGKILL");
  }, KILL_TIMEOUT);
}
