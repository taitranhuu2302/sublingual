import { app, BrowserWindow } from "electron";
import { ChildProcess, spawn } from "node:child_process";
import path from "node:path";
import http from "node:http";
import { getMainLogger } from "../utils/logger";
import { getSettings } from "../settings/settings-store";

export interface TranslateProcessStatus {
  status: "running" | "starting" | "stopped" | "error";
  pid: number | null;
  uptime: number | null; // seconds
  loadedModels: string[];
  error: string | null;
}

const HEALTH_CHECK_INTERVAL = 500; // ms
const HEALTH_START_TIMEOUT = 30_000; // 30s
const KILL_TIMEOUT = 5_000; // 5s

let process: ChildProcess | null = null;
let status: TranslateProcessStatus = {
  status: "stopped",
  pid: null,
  uptime: null,
  loadedModels: [],
  error: null,
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

  if (app.isPackaged) {
    const name = process.platform === "win32" ? "translate-service.exe" : "translate-service";
    return {
      bin: path.join(process.resourcesPath, "bin", "translate", name),
      args: [
        "--host", "127.0.0.1",
        "--port", "3333",
        "--models-dir", settings.modelsDir,
        "--log-dir", path.join(require("os").homedir(), ".sublingual", "logs", "translate"),
      ],
    };
  }

  // Dev: use venv
  const projectRoot = path.resolve(__dirname, "../../../..");
  const python = process.platform === "win32"
    ? path.join(projectRoot, "translate", ".venv", "Scripts", "python.exe")
    : path.join(projectRoot, "translate", ".venv", "bin", "python");

  return {
    bin: python,
    args: [
      "-m", "uvicorn", "app.main:app",
      "--host", "127.0.0.1",
      "--port", "3333",
      "--models-dir", settings.modelsDir,
      "--log-dir", path.join(require("os").homedir(), ".sublingual", "logs", "translate"),
    ],
    cwd: path.join(projectRoot, "translate"),
  };
}

function pollHealth(): void {
  const settings = getSettings().translation.local;
  const baseUrl = settings.baseUrl.replace(/\/+$/, "");
  const logger = getMainLogger({ tag: "translate" });

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
            });
            logger.info(
              `translate service ready (pid=${process?.pid}, loaded_models=${health.loaded_models?.join(",") || "none"})`
            );
            mainWindowRef?.webContents.send("translate:service-ready", {
              pid: process?.pid,
              loadedModels: health.loaded_models ?? [],
            });
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
}

function monitorStdio(child: ChildProcess): void {
  const logger = getMainLogger({ tag: "translate" });

  child.stdout?.on("data", (data: Buffer) => {
    const lines = data.toString().trim().split("\n");
    for (const line of lines) {
      if (line) {
        logger.info(line);
        mainWindowRef?.webContents.send("translate:log", { line });
      }
    }
  });

  child.stderr?.on("data", (data: Buffer) => {
    const lines = data.toString().trim().split("\n");
    for (const line of lines) {
      if (line) {
        logger.warn(line);
        mainWindowRef?.webContents.send("translate:log", { line });
      }
    }
  });
}

export function getTranslateStatus(): TranslateProcessStatus {
  return { ...status };
}

export function startTranslate(mainWindow: BrowserWindow): void {
  mainWindowRef = mainWindow;
  const logger = getMainLogger({ tag: "translate" });

  if (process) {
    logger.warn("translate service is already running, skipping start");
    return;
  }

  try {
    const cmd = resolveCommand();
    logger.info(`starting translate service: ${cmd.bin} ${cmd.args.join(" ")}`);

    process = spawn(cmd.bin, cmd.args, {
      env: { ...process.env, APP_PATH: app.getAppPath() },
      cwd: cmd.cwd,
      stdio: ["ignore", "pipe", "pipe"],
    });

    setStatus({ status: "starting", pid: process.pid, uptime: null, error: null, loadedModels: [] });
    startTime = Date.now();

    monitorStdio(process);

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

    process.on("exit", (code, signal) => {
      logger.warn(`translate service exited (code=${code}, signal=${signal})`);
      if (healthInterval) clearInterval(healthInterval);
      healthInterval = null;

      if (status.status !== "stopped") {
        setStatus({
          status: "error",
          pid: null,
          uptime: null,
          loadedModels: [],
          error: `Process exited with code ${code} signal ${signal}`,
        });
        mainWindowRef?.webContents.send("translate:service-error", {
          error: `Process exited with code ${code} signal ${signal}`,
        });
      }

      process = null;
      startTime = null;
    });

    process.on("error", (err) => {
      logger.error("translate service spawn error", err);
      setStatus({ status: "error", pid: null, error: err.message });
      mainWindowRef?.webContents.send("translate:service-error", { error: err.message });
      process = null;
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

  if (!process) {
    setStatus({ status: "stopped", pid: null, uptime: null, loadedModels: [], error: null });
    return;
  }

  if (healthInterval) {
    clearInterval(healthInterval);
    healthInterval = null;
  }

  logger.info("stopping translate service");
  setStatus({ status: "stopped", pid: null, uptime: null, loadedModels: [], error: null });

  const child = process;
  process = null;
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
  const child = process;
  if (!child) return;
  if (healthInterval) {
    clearInterval(healthInterval);
    healthInterval = null;
  }
  process = null;
  startTime = null;
  child.kill("SIGTERM");
  setTimeout(() => {
    child.kill("SIGKILL");
  }, KILL_TIMEOUT);
}
