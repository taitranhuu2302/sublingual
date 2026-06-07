# Translate Service Packaging & Process Management — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Package Python translate service with PyInstaller, spawn as subprocess from Electron desktop app, auto-start/shutdown, add file-based logging with timestamp rotation for both services.

**Architecture:** `translate-process.ts` manages child_process.spawn lifecycle (dev uses venv, production uses PyInstaller exe in extraResource). File-based logger rotates logs with timestamp filenames, 5MB trigger, 10-file retention. Settings UI shows service status, logs, and model download progress.

**Tech Stack:** Electron, TypeScript, React, FastAPI, PyInstaller, Python logging

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `desktop/src/main/utils/logger.ts` | File-based logger with timestamp rotation |
| Modify | `translate/app/utils/logger.py` | Add file logging with rotation |
| Modify | `translate/app/main.py` | Accept `--log-dir` CLI arg |
| Modify | `translate/app/config.py` | Add `log_dir` setting |
| Create | `desktop/src/main/translation/translate-process.ts` | Spawn/kill/monitor Python subprocess |
| Modify | `desktop/src/main/settings/settings-store.ts` | Add `modelsDir`, `logsDir` |
| Modify | `desktop/src/main/ipc/translation-handlers.ts` | Add status/restart/log IPC |
| Modify | `desktop/src/main/ipc-handlers.ts` | Register translation handlers with mainWindow |
| Modify | `desktop/src/preload.ts` | Expose new translation IPC |
| Modify | `desktop/src/types/electron-api.d.ts` | Add translate service types |
| Create | `desktop/src/hooks/use-translate-service.ts` | React hook for service state |
| Modify | `desktop/src/components/settings/TranslationSettings.tsx` | Status panel + model path + download progress |
| Modify | `desktop/src/main.ts` | Auto-start/stop translate process |
| Modify | `desktop/forge.config.ts` | Include bin/translate in extraResource |
| Create | `translate/scripts/build_pyinstaller.ps1` | PyInstaller build (Windows) |
| Create | `translate/scripts/build_pyinstaller.sh` | PyInstaller build (macOS/Linux) |

---

### Task 1: Desktop File-based Logger

**Files:**
- Create: `desktop/src/main/utils/logger.ts`

- [ ] **Step 1: Implement desktop logger with timestamp rotation**

```typescript
import { app } from "electron";
import fs from "fs";
import path from "path";
import os from "os";

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5 MB
const MAX_LOG_FILES = 10;
const LOGS_ROOT = path.join(os.homedir(), ".sublingual", "logs", "desktop");

type LogLevel = "INFO" | "WARN" | "ERROR";

interface LoggerOptions {
  onLog?: (line: string) => void;
  tag?: string;
}

class FileLogger {
  private currentFile: string | null = null;
  private stream: fs.WriteStream | null = null;
  private onLog: ((line: string) => void) | undefined;
  private tag: string;

  constructor(options?: LoggerOptions) {
    this.onLog = options?.onLog;
    this.tag = options?.tag ?? "main";
    this.ensureDir();
    this.cleanupOldFiles();
    this.openNewFile();
  }

  private ensureDir(): void {
    if (!fs.existsSync(LOGS_ROOT)) {
      fs.mkdirSync(LOGS_ROOT, { recursive: true });
    }
  }

  private logFilePath(tag: string): string {
    const timestamp = new Date()
      .toISOString()
      .replace(/[-:T]/g, "-")
      .replace(/\..+/, "")
      .replace(/-/g, "-")
      .replace(/(\d{4})-(\d{2})-(\d{2})-(\d{2})-(\d{2})-(\d{2})/, "$1-$2-$3-$4-$5-$6");
    return path.join(LOGS_ROOT, `${tag}-${timestamp}.log`);
  }

  private openNewFile(): void {
    if (this.stream) {
      this.stream.end();
    }
    this.currentFile = this.logFilePath(this.tag);
    this.stream = fs.createWriteStream(this.currentFile, { flags: "a" });
  }

  private shouldRotate(): boolean {
    if (!this.currentFile) return true;
    try {
      const stat = fs.statSync(this.currentFile);
      return stat.size > MAX_FILE_SIZE;
    } catch {
      return false;
    }
  }

  private cleanupOldFiles(): void {
    try {
      const files = fs
        .readdirSync(LOGS_ROOT)
        .filter((f) => f.startsWith(this.tag + "-") && f.endsWith(".log"))
        .sort()
        .reverse(); // newest first

      const toDelete = files.slice(MAX_LOG_FILES);
      for (const file of toDelete) {
        fs.unlinkSync(path.join(LOGS_ROOT, file));
      }
    } catch {
      // ignore cleanup errors
    }
  }

  private write(level: LogLevel, message: string): void {
    if (this.shouldRotate()) {
      this.cleanupOldFiles();
      this.openNewFile();
    }

    const timestamp = new Date().toISOString().replace("T", " ").slice(0, 19);
    const line = `[${timestamp}] [${level}] ${message}`;

    if (this.stream) {
      this.stream.write(line + "\n");
    }

    // forward to IPC listener (for Settings UI)
    this.onLog?.(line);

    // also print to console in dev
    if (!app.isPackaged) {
      const consoleFn = level === "ERROR" ? console.error : level === "WARN" ? console.warn : console.log;
      consoleFn(line);
    }
  }

  info(message: string): void {
    this.write("INFO", message);
  }

  warn(message: string): void {
    this.write("WARN", message);
  }

  error(message: string, err?: unknown): void {
    const errMsg = err instanceof Error ? err.stack ?? err.message : String(err ?? "");
    this.write("ERROR", errMsg ? `${message} | ${errMsg}` : message);
  }

  dispose(): void {
    if (this.stream) {
      this.stream.end();
      this.stream = null;
    }
  }
}

let mainLogger: FileLogger | null = null;

export function getMainLogger(options?: LoggerOptions): FileLogger {
  if (!mainLogger) {
    mainLogger = new FileLogger(options);
  }
  return mainLogger;
}

export { FileLogger };
export type { LoggerOptions };
```

- [ ] **Step 2: Verify logger compiles**

```bash
cd desktop && npx tsc --noEmit src/main/utils/logger.ts
```

Expected: no errors

- [ ] **Step 3: Commit**

```bash
git add desktop/src/main/utils/logger.ts
git commit -m "feat(logger): add file-based logger with timestamp rotation"
```

---

### Task 2: Python File Logging with Rotation

**Files:**
- Modify: `translate/app/utils/logger.py`
- Modify: `translate/app/config.py`
- Modify: `translate/app/main.py`

- [ ] **Step 1: Update config.py to add log_dir setting**

Replace `translate/app/config.py`:

```python
from functools import lru_cache
from pathlib import Path

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=True,
        extra="ignore",
    )

    model_base_dir: str = Field(default="models/ct2", alias="MODEL_BASE_DIR")
    translation_device: str = Field(default="cpu", alias="TRANSLATION_DEVICE")
    translation_compute_type: str = Field(
        default="int8",
        alias="TRANSLATION_COMPUTE_TYPE",
    )
    inter_threads: int = Field(default=1, alias="INTER_THREADS")
    intra_threads: int = Field(default=4, alias="INTRA_THREADS")
    default_source_lang: str = Field(default="en", alias="DEFAULT_SOURCE_LANG")
    default_target_lang: str = Field(default="vi", alias="DEFAULT_TARGET_LANG")
    min_realtime_chars: int = Field(default=8, alias="MIN_REALTIME_CHARS")
    max_text_chars: int = Field(default=1000, alias="MAX_TEXT_CHARS")
    session_cache_ttl_sec: int = Field(default=300, alias="SESSION_CACHE_TTL_SEC")
    log_level: str = Field(default="INFO", alias="LOG_LEVEL")
    log_dir: str = Field(default="", alias="LOG_DIR")
    fast_beam_size: int = Field(default=1, alias="FAST_BEAM_SIZE")
    quality_beam_size: int = Field(default=4, alias="QUALITY_BEAM_SIZE")
    glossary_path: str = Field(default="glossary.json", alias="GLOSSARY_PATH")

    @property
    def resolved_model_base_dir(self) -> Path:
        return Path(self.model_base_dir)


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()
```

- [ ] **Step 2: Update logger.py with file handler and rotation**

Replace `translate/app/utils/logger.py`:

```python
import logging
import os
from datetime import datetime
from pathlib import Path

MAX_LOG_SIZE = 5 * 1024 * 1024  # 5 MB
MAX_LOG_FILES = 10

_current_log_file: str | None = None
_file_handler: logging.FileHandler | None = None


def _check_rotation(log_dir: str, prefix: str) -> None:
    """Rotate if current file exceeds MAX_LOG_SIZE. Cleanup old files."""
    global _current_log_file, _file_handler

    if not _current_log_file or not os.path.exists(_current_log_file):
        return

    try:
        if os.path.getsize(_current_log_file) > MAX_LOG_SIZE:
            # close current
            if _file_handler:
                root_logger = logging.getLogger()
                root_logger.removeHandler(_file_handler)
                _file_handler.close()

            # create new file
            _open_new_log_file(log_dir, prefix)

            # cleanup old files
            _cleanup_old_files(log_dir, prefix)
    except OSError:
        pass


def _open_new_log_file(log_dir: str, prefix: str) -> None:
    global _current_log_file, _file_handler

    timestamp = datetime.now().strftime("%Y-%m-%d-%H-%M-%S")
    _current_log_file = str(Path(log_dir) / f"{prefix}-{timestamp}.log")

    _file_handler = logging.FileHandler(_current_log_file, encoding="utf-8")
    _file_handler.setFormatter(
        logging.Formatter(
            "%(asctime)s | %(levelname)s | %(name)s | %(message)s"
        )
    )
    logging.getLogger().addHandler(_file_handler)


def _cleanup_old_files(log_dir: str, prefix: str) -> None:
    """Delete oldest files beyond MAX_LOG_FILES limit."""
    try:
        log_path = Path(log_dir)
        if not log_path.is_dir():
            return
        files = sorted(
            [f for f in log_path.iterdir() if f.name.startswith(prefix + "-") and f.suffix == ".log"],
            key=lambda f: f.stat().st_mtime,
            reverse=True,
        )
        for old in files[MAX_LOG_FILES:]:
            old.unlink(missing_ok=True)
    except OSError:
        pass


class LogRotationFilter(logging.Filter):
    """Filter that checks rotation after each log record."""

    def __init__(self, log_dir: str, prefix: str) -> None:
        super().__init__()
        self.log_dir = log_dir
        self.prefix = prefix

    def filter(self, record: logging.LogRecord) -> bool:
        _check_rotation(self.log_dir, self.prefix)
        return True


def configure_logging(level: str = "INFO", log_dir: str = "") -> None:
    root_logger = logging.getLogger()
    root_logger.setLevel(getattr(logging, level.upper(), logging.INFO))

    root_logger.handlers.clear()

    # always add stream handler
    stream_handler = logging.StreamHandler()
    stream_handler.setFormatter(
        logging.Formatter(
            "%(asctime)s | %(levelname)s | %(name)s | %(message)s"
        )
    )
    root_logger.addHandler(stream_handler)

    # add file handler if log_dir provided
    if log_dir:
        log_path = Path(log_dir)
        log_path.mkdir(parents=True, exist_ok=True)

        _open_new_log_file(log_dir, "service")
        _cleanup_old_files(log_dir, "service")

        root_logger.addFilter(LogRotationFilter(log_dir, "service"))
```

- [ ] **Step 3: Update main.py to accept --log-dir and pass it to config/logger**

In `translate/app/main.py`, update the startup section (add argparse before app setup, replace the `configure_logging` call):

```python
import argparse
import logging
import sys
import time

from fastapi import FastAPI, HTTPException

from app.config import Settings, get_settings
from app.schemas import (
    HealthResponse,
    TranslateFastRequest,
    TranslateFastResponse,
    TranslateRequest,
    TranslateResponse,
)
from app.translator.model_manager import TranslationModelManager
from app.translator.session_cache import RealtimeSessionCache
from app.utils.logger import configure_logging
from app.utils.text import normalize_text, truncate_text


# CLI args override env for port, host, models-dir, log-dir
parser = argparse.ArgumentParser(description="Translate Service")
parser.add_argument("--host", default="127.0.0.1", help="Bind host")
parser.add_argument("--port", type=int, default=3333, help="Bind port")
parser.add_argument("--models-dir", default=None, help="Path to CTranslate2 model directory")
parser.add_argument("--log-dir", default=None, help="Path to log directory")
cli_args = parser.parse_args()

# Override settings with CLI args
settings_overrides: dict = {}
if cli_args.models_dir:
    settings_overrides["MODEL_BASE_DIR"] = cli_args.models_dir
if cli_args.log_dir:
    settings_overrides["LOG_DIR"] = cli_args.log_dir

# Re-export cli args for uvicorn
CLI_HOST = cli_args.host
CLI_PORT = cli_args.port

if settings_overrides:
    # Patch env so Settings picks them up (pydantic-settings reads from env)
    import os
    for key, val in settings_overrides.items():
        os.environ[key] = str(val)

settings = get_settings()
configure_logging(settings.log_level, log_dir=settings.log_dir if settings.log_dir else None)
logger = logging.getLogger("translate")

model_manager = TranslationModelManager(
    base_model_dir=settings.model_base_dir,
    device=settings.translation_device,
    compute_type=settings.translation_compute_type,
    inter_threads=settings.inter_threads,
    intra_threads=settings.intra_threads,
    fast_beam_size=settings.fast_beam_size,
    quality_beam_size=settings.quality_beam_size,
)
realtime_session_cache = RealtimeSessionCache(
    ttl_sec=settings.session_cache_ttl_sec,
    min_realtime_chars=settings.min_realtime_chars,
)

app = FastAPI(
    title="Translate Service",
    version="0.2.0",
    description=(
        "Standalone self-hosted translation API powered by NLLB-200 and CTranslate2. "
        "Supports fast greedy translation for realtime subtitles and quality beam-search "
        "translation with Vietnamese post-processing."
    ),
    docs_url="/docs",
    redoc_url="/redoc",
    openapi_tags=[
        {
            "name": "system",
            "description": "Health and model discovery.",
        },
        {
            "name": "translation",
            "description": "Quality translation with beam search and post-processing.",
        },
        {
            "name": "fast",
            "description": "Low-latency greedy translation for realtime subtitles.",
        },
    ],
)


@app.on_event("startup")
def warmup_default_model() -> None:
    try:
        translator = model_manager.get_translator(
            settings.default_source_lang,
            settings.default_target_lang,
            mode="fast",
        )
        translator.translate("hello", source_lang="en", target_lang="vi")
        logger.info("warmed up NLLB-200 fast model on startup")
    except HTTPException as exc:
        logger.warning("fast model warmup skipped: %s", exc.detail)

    try:
        translator = model_manager.get_translator(
            settings.default_source_lang,
            settings.default_target_lang,
            mode="quality",
        )
        translator.translate("hello", source_lang="en", target_lang="vi")
        logger.info("warmed up NLLB-200 quality model on startup")
    except HTTPException as exc:
        logger.warning("quality model warmup skipped: %s", exc.detail)


def _prepare_text(text: str) -> str:
    prepared = truncate_text(normalize_text(text), settings.max_text_chars)
    if not prepared:
        raise HTTPException(status_code=400, detail="Text must not be empty.")
    return prepared


@app.get(
    "/health",
    response_model=HealthResponse,
    tags=["system"],
    summary="Health check",
)
def health() -> HealthResponse:
    return HealthResponse(
        status="ok",
        device=settings.translation_device,
        compute_type=settings.translation_compute_type,
        loaded_models=model_manager.loaded_models,
        available_pairs=model_manager.list_available_pairs(),
    )


@app.post(
    "/translate/fast",
    response_model=TranslateFastResponse,
    tags=["fast"],
    summary="Fast realtime translation",
    description=(
        "Low-latency greedy translation for Vosk subtitle pipeline. "
        "Skips redundant partials based on session state."
    ),
    responses={400: {"description": "Model not found or text empty"}},
)
def translate_fast(request: TranslateFastRequest) -> TranslateFastResponse:
    realtime_session_cache.cleanup_expired()
    source_text = _prepare_text(request.text)

    should_translate, _ = realtime_session_cache.should_translate(
        request.session_id,
        source_text,
        request.is_final,
    )
    if not should_translate:
        return TranslateFastResponse(
            translated_text="",
            should_display=False,
            latency_ms=0,
        )

    started = time.perf_counter()
    translator = model_manager.get_translator(
        request.source_lang, request.target_lang, mode="fast"
    )
    translated_text = translator.translate(
        source_text,
        source_lang=request.source_lang,
        target_lang=request.target_lang,
    )
    latency_ms = (time.perf_counter() - started) * 1000

    session = realtime_session_cache.get(request.session_id)
    last_translated = str(session.get("last_translated_text", "")) if session else ""
    should_display = bool(translated_text)
    if not request.is_final and translated_text == last_translated:
        should_display = False

    realtime_session_cache.update(request.session_id, source_text, translated_text)

    logger.info(
        "translate_fast chars=%d latency_ms=%.2f final=%s display=%s",
        len(source_text),
        latency_ms,
        request.is_final,
        should_display,
    )

    return TranslateFastResponse(
        translated_text=translated_text if should_display else "",
        should_display=should_display,
        latency_ms=latency_ms,
    )


@app.post(
    "/translate",
    response_model=TranslateResponse,
    tags=["translation"],
    summary="Quality translation",
    description=(
        "Translates text with beam search and Vietnamese post-processing. "
        "Accepts single string or array of strings for batch translation."
    ),
    responses={400: {"description": "Model not found or text empty"}},
)
def translate(request: TranslateRequest) -> TranslateResponse:
    started = time.perf_counter()
    translator = model_manager.get_translator(
        request.source_lang, request.target_lang, mode="quality"
    )

    if isinstance(request.text, str):
        source_text = _prepare_text(request.text)
        translated_text = translator.translate(
            source_text,
            source_lang=request.source_lang,
            target_lang=request.target_lang,
        )
    else:
        source_texts = [_prepare_text(t) for t in request.text]
        translated_text = translator.translate_batch(
            source_texts,
            source_lang=request.source_lang,
            target_lang=request.target_lang,
        )

    latency_ms = (time.perf_counter() - started) * 1000

    logger.info(
        "translate lang=%s->%s latency_ms=%.2f",
        request.source_lang,
        request.target_lang,
        latency_ms,
    )

    return TranslateResponse(
        translated_text=translated_text,
        latency_ms=latency_ms,
    )
```

- [ ] **Step 4: Verify Python app loads**

```bash
cd translate && python -c "from app.main import app; print('OK'); print('Routes:', [r.path for r in app.routes])"
```

Expected: `OK` and routes printed

- [ ] **Step 5: Commit**

```bash
git add translate/app/utils/logger.py translate/app/config.py translate/app/main.py
git commit -m "feat(translate): add file logging with rotation, --log-dir CLI arg"
```

---

### Task 3: Translate Process Manager

**Files:**
- Create: `desktop/src/main/translation/translate-process.ts`

- [ ] **Step 1: Implement translate-process.ts**

```typescript
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

function resolveCommand(): { bin: string; args: string[] } {
  const settings = getSettings().translation.local;

  if (app.isPackaged) {
    const name = process.platform === "win32" ? "translate-service.exe" : "translate-service";
    return {
      bin: path.join(process.resourcesPath, "bin", "translate", name),
      args: [
        "--host", "127.0.0.1",
        "--port", "3333",
        "--models-dir", settings.modelsDir,
        "--log-dir", path.dirname(settings.modelsDir) + "/logs/translate",
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
      "--log-dir", path.dirname(settings.modelsDir) + "/logs/translate",
    ],
    // Set cwd for dev mode so Python finds app/ and .env
    cwd: path.join(projectRoot, "translate"),
  };
}

function pollHealth(): void {
  const settings = getSettings().translation.local;
  const baseUrl = settings.baseUrl.replace(/\/+$/, "");
  const url = new URL("/health", baseUrl);
  const logger = getMainLogger({ tag: "translate" });

  const req = http.get(url, (res) => {
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
      logger.info(line);
      mainWindowRef?.webContents.send("translate:log", { line });
    }
  });

  child.stderr?.on("data", (data: Buffer) => {
    const lines = data.toString().trim().split("\n");
    for (const line of lines) {
      logger.warn(line);
      mainWindowRef?.webContents.send("translate:log", { line });
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
      cwd: (cmd as { cwd?: string }).cwd,
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
      process = null;
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    logger.error("failed to start translate service", err);
    setStatus({ status: "error", pid: null, error: message });
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
  if (!process) return;
  if (healthInterval) {
    clearInterval(healthInterval);
    healthInterval = null;
  }
  process.kill("SIGTERM");
  setTimeout(() => {
    if (process) process.kill("SIGKILL");
  }, KILL_TIMEOUT);
  process = null;
  startTime = null;
}
```

- [x] **Step 2: Verify compiles**

```bash
cd desktop && npx tsc --noEmit src/main/translation/translate-process.ts
```

Expected: no errors

- [ ] **Step 3: Commit**

```bash
git add desktop/src/main/translation/translate-process.ts
git commit -m "feat(translate): add subprocess manager for Python translate service"
```

---

### Task 4: Update Settings Store

**Files:**
- Modify: `desktop/src/main/settings/settings-store.ts`

- [ ] **Step 1: Add modelsDir and logsDir to TranslationProviderLocal**

In `desktop/src/main/settings/settings-store.ts`, update `TranslationProviderLocal` interface and defaults:

```typescript
export interface TranslationProviderLocal {
  baseUrl: string;
  modelsDir: string;
  logsDir: string;
}
```

Update defaults:

```typescript
const DEFAULTS: AppSettings = {
  storage: {
    sessionsRoot: path.join(SETTINGS_DIR, "sessions"),
    speechToTextModelsRoot: path.join(SETTINGS_DIR, "models"),
  },
  overlay: {
    fontSize: 26,
    lineHeight: 1.35,
    width: 720,
    height: 200,
    theme: "Dark",
    opacity: 0.88,
    showTranslation: true,
    positionX: null,
    positionY: null,
  },
  speechToText: {
    selectedModel: "",
    sourceLanguage: "en",
    speakerModel: "",
    maxSpeakers: 4,
    flushTimeoutMs: 3000,
  },
  translation: {
    enabled: true,
    provider: "google-free",
    targetLanguage: "vi",
    google: { endpoint: "https://translate.googleapis.com/translate_a/single" },
    local: {
      baseUrl: "http://127.0.0.1:3333",
      modelsDir: path.join(SETTINGS_DIR, "translate-models"),
      logsDir: path.join(SETTINGS_DIR, "logs", "translate"),
    },
  },
};
```

- [ ] **Step 2: Verify compiles**

```bash
cd desktop && npx tsc --noEmit src/main/settings/settings-store.ts
```

Expected: no errors

- [ ] **Step 3: Commit**

```bash
git add desktop/src/main/settings/settings-store.ts
git commit -m "feat(settings): add modelsDir and logsDir to translate-local provider"
```

---

### Task 5: Update IPC Handlers and Registration

**Files:**
- Modify: `desktop/src/main/ipc/translation-handlers.ts`
- Modify: `desktop/src/main/ipc-handlers.ts`

- [ ] **Step 1: Rewrite translation-handlers.ts with new IPC channels**

Replace `desktop/src/main/ipc/translation-handlers.ts`:

```typescript
import { ipcMain } from "electron";
import { getTranslationService } from "../translation/translation-service";
import { getTranslateStatus, restartTranslate } from "../translation/translate-process";

export function registerTranslationHandlers() {
  ipcMain.handle(
    "translation:translate",
    async (_event, sourceText: string, sourceLang: string, targetLang: string) => {
      return getTranslationService().translate(sourceText, sourceLang, targetLang);
    },
  );

  ipcMain.handle("translate:get-status", async () => {
    return getTranslateStatus();
  });

  ipcMain.handle("translate:restart", async () => {
    await restartTranslate();
  });
}
```

- [ ] **Step 2: Update ipc-handlers.ts to pass mainWindow**

Replace `desktop/src/main/ipc-handlers.ts`:

```typescript
import { BrowserWindow } from "electron";
import { registerAudioHandlers } from "./ipc/audio-handlers";
import { registerAsrHandlers } from "./ipc/asr-handlers";
import { registerSettingsHandlers } from "./ipc/settings-handlers";
import { registerTranslationHandlers } from "./ipc/translation-handlers";
import { registerModelHandlers } from "./ipc/model-handlers";
import { registerOverlayHandlers } from "./ipc/overlay-handlers";
import { registerSessionHandlers } from "./ipc/session-handlers";

export function registerIpcHandlers(mainWindow: BrowserWindow) {
  registerAudioHandlers(mainWindow);
  registerAsrHandlers(mainWindow);
  registerSettingsHandlers(mainWindow);
  registerTranslationHandlers();
  registerModelHandlers(mainWindow);
  registerOverlayHandlers(mainWindow);
  registerSessionHandlers(mainWindow);
}
```

- [ ] **Step 3: Verify compiles**

```bash
cd desktop && npx tsc --noEmit src/main/ipc/translation-handlers.ts src/main/ipc-handlers.ts
```

Expected: no errors

- [ ] **Step 4: Commit**

```bash
git add desktop/src/main/ipc/translation-handlers.ts desktop/src/main/ipc-handlers.ts
git commit -m "feat(ipc): add translate status/restart handlers"
```

---

### Task 6: Update main.ts — Auto-start/stop Translate Service

**Files:**
- Modify: `desktop/src/main.ts`

- [ ] **Step 1: Add translate process integration**

Replace `desktop/src/main.ts`:

```typescript
import { app, BrowserWindow } from 'electron';
import path from 'path';
import { registerIpcHandlers } from './main/ipc-handlers';
import { stopAudioCapture } from './main/audio/audio-capture';
import { stopVosk } from './main/asr/vosk-process';
import { getSessionStorage } from './main/sessions/session-storage';
import { getOverlayManager } from './main/overlay/overlay-window';
import { startTranslate, stopTranslate } from './main/translation/translate-process';

declare const MAIN_WINDOW_VITE_DEV_SERVER_URL: string;
declare const MAIN_WINDOW_VITE_NAME: string;

let mainWindow: BrowserWindow | null = null;

const createWindow = () => {
  mainWindow = new BrowserWindow({
    width: 1024,
    height: 768,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });

  if (MAIN_WINDOW_VITE_DEV_SERVER_URL) {
    mainWindow.loadURL(MAIN_WINDOW_VITE_DEV_SERVER_URL);
    mainWindow.webContents.openDevTools();
  } else {
    mainWindow.loadFile(
      path.join(__dirname, `../renderer/${MAIN_WINDOW_VITE_NAME}/index.html`)
    );
  }

  registerIpcHandlers(mainWindow);

  // Auto-start translate service after window is ready
  mainWindow.webContents.on('did-finish-load', () => {
    startTranslate(mainWindow!);
  });
};

app.on('ready', createWindow);

app.on('window-all-closed', () => {
  stopAudioCapture();
  stopVosk();
  getSessionStorage().stopSession();
  getOverlayManager().destroy();
  stopTranslate();
  app.quit();
});

app.on('before-quit', () => {
  stopAudioCapture();
  stopVosk();
  getSessionStorage().stopSession();
  getOverlayManager().destroy();
  stopTranslate();
});

app.on('activate', () => {
  if (mainWindow === null) {
    createWindow();
  }
});
```

- [ ] **Step 2: Verify compiles**

```bash
cd desktop && npx tsc --noEmit src/main.ts
```

Expected: no errors

- [ ] **Step 3: Commit**

```bash
git add desktop/src/main.ts
git commit -m "feat(main): auto-start/stop translate service with app lifecycle"
```

---

### Task 7: Update Preload Bridge and Type Definitions

**Files:**
- Modify: `desktop/src/preload.ts`
- Modify: `desktop/src/types/electron-api.d.ts`

- [ ] **Step 1: Add translate service IPC to preload.ts**

In `desktop/src/preload.ts`, add to the `translation` block:

```typescript
  translation: {
    translate: (sourceText: string, sourceLang: string, targetLang: string) =>
      ipcRenderer.invoke("translation:translate", sourceText, sourceLang, targetLang),
    onSegmentResult: (
      callback: (result: {
        segmentId: string;
        translatedText: string;
        providerName: string;
        durationMs: number;
      }) => void
    ) => {
      const handler = (
        _event: unknown,
        result: {
          segmentId: string;
          translatedText: string;
          providerName: string;
          durationMs: number;
        }
      ) => callback(result);
      ipcRenderer.on("translation:segment-result", handler);
      return () =>
        ipcRenderer.removeListener("translation:segment-result", handler);
    },
    getServiceStatus: () => ipcRenderer.invoke("translate:get-status"),
    restartService: () => ipcRenderer.invoke("translate:restart"),
    onServiceStatusChange: (callback: (status: {
      status: string;
      pid: number | null;
      uptime: number | null;
      loadedModels: string[];
      error: string | null;
    }) => void) => {
      const handler = (_event: unknown, status: {
        status: string;
        pid: number | null;
        uptime: number | null;
        loadedModels: string[];
        error: string | null;
      }) => callback(status);
      ipcRenderer.on("translate:status-change", handler);
      return () => ipcRenderer.removeListener("translate:status-change", handler);
    },
    onServiceLog: (callback: (log: { line: string }) => void) => {
      const handler = (_event: unknown, log: { line: string }) => callback(log);
      ipcRenderer.on("translate:log", handler);
      return () => ipcRenderer.removeListener("translate:log", handler);
    },
  },
```

- [ ] **Step 2: Add types to electron-api.d.ts**

In `desktop/src/types/electron-api.d.ts`, add to the `ElectronAPI.translation` interface:

```typescript
export interface TranslateServiceStatus {
  status: "running" | "starting" | "stopped" | "error";
  pid: number | null;
  uptime: number | null;
  loadedModels: string[];
  error: string | null;
}

// In ElectronAPI.translation, add:
    getServiceStatus: () => Promise<TranslateServiceStatus>;
    restartService: () => Promise<void>;
    onServiceStatusChange: (callback: (status: TranslateServiceStatus) => void) => () => void;
    onServiceLog: (callback: (log: { line: string }) => void) => () => void;
```

- [ ] **Step 3: Verify compiles**

```bash
cd desktop && npx tsc --noEmit
```

Expected: no errors

- [ ] **Step 4: Commit**

```bash
git add desktop/src/preload.ts desktop/src/types/electron-api.d.ts
git commit -m "feat(bridge): expose translate service status/log/restart IPC to renderer"
```

---

### Task 8: Create useTranslateService Hook

**Files:**
- Create: `desktop/src/hooks/use-translate-service.ts`

- [ ] **Step 1: Implement the hook**

```typescript
import { useState, useEffect, useCallback } from "react";
import type { TranslateServiceStatus } from "@/types/electron-api";

export function useTranslateService() {
  const [status, setStatus] = useState<TranslateServiceStatus>({
    status: "stopped",
    pid: null,
    uptime: null,
    loadedModels: [],
    error: null,
  });
  const [logs, setLogs] = useState<string[]>([]);

  useEffect(() => {
    const unsubStatus = window.electronAPI.translation.onServiceStatusChange((s) => {
      setStatus(s);
    });

    const unsubLog = window.electronAPI.translation.onServiceLog((log) => {
      setLogs((prev) => {
        const next = [...prev, log.line];
        return next.length > 50 ? next.slice(-50) : next;
      });
    });

    // Fetch initial status
    window.electronAPI.translation.getServiceStatus().then(setStatus).catch(console.error);

    return () => {
      unsubStatus();
      unsubLog();
    };
  }, []);

  const restart = useCallback(async () => {
    setStatus((prev) => ({ ...prev, status: "starting", pid: null, uptime: null, error: null, loadedModels: [] }));
    await window.electronAPI.translation.restartService();
  }, []);

  const clearLogs = useCallback(() => {
    setLogs([]);
  }, []);

  return { status, logs, restart, clearLogs };
}
```

- [ ] **Step 2: Verify compiles**

```bash
cd desktop && npx tsc --noEmit src/hooks/use-translate-service.ts
```

Expected: no errors

- [ ] **Step 3: Commit**

```bash
git add desktop/src/hooks/use-translate-service.ts
git commit -m "feat(hooks): add useTranslateService hook for service status and logs"
```

---

### Task 9: Update TranslationSettings UI

**Files:**
- Modify: `desktop/src/components/settings/TranslationSettings.tsx`

- [ ] **Step 1: Rewrite TranslationSettings.tsx with service status panel**

Replace `desktop/src/components/settings/TranslationSettings.tsx`:

```tsx
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import { SettingsSection } from "./SettingsSection";
import { SettingsField } from "./SettingsField";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { RefreshCw, FolderOpen, FolderSearch, RotateCw, Trash2 } from "lucide-react";
import { useTranslateService } from "@/hooks/use-translate-service";
import type { AppSettings, TranslationResult } from "@/types/electron-api";

const LANGUAGES = [
  { value: "vi", label: "Vietnamese" },
  { value: "en", label: "English" },
  { value: "ja", label: "Japanese" },
  { value: "ko", label: "Korean" },
  { value: "zh", label: "Chinese" },
  { value: "fr", label: "French" },
  { value: "de", label: "German" },
  { value: "es", label: "Spanish" },
];

interface Props {
  settings: AppSettings;
  onUpdate: (partial: Partial<AppSettings>) => void;
}

const statusBadge: Record<string, { label: string; variant: "default" | "secondary" | "destructive" }> = {
  running: { label: "Running", variant: "default" },
  starting: { label: "Starting...", variant: "secondary" },
  stopped: { label: "Stopped", variant: "destructive" },
  error: { label: "Error", variant: "destructive" },
};

export function TranslationSettings({ settings, onUpdate }: Props) {
  const ts = settings.translation;
  const [testText, setTestText] = useState("Hello, how are you today?");
  const [testResult, setTestResult] = useState<TranslationResult | null>(null);
  const [testError, setTestError] = useState("");
  const [testing, setTesting] = useState(false);

  const { status, logs, restart, clearLogs } = useTranslateService();

  const updateTranslation = (partial: Partial<typeof ts>) => {
    onUpdate({ translation: { ...ts, ...partial } });
  };

  const updateLocal = (partial: Partial<typeof ts.local>) => {
    onUpdate({ translation: { ...ts, local: { ...ts.local, ...partial } } });
  };

  const runTest = async () => {
    setTesting(true);
    setTestError("");
    setTestResult(null);
    try {
      const result = await window.electronAPI.translation.translate(
        testText,
        settings.speechToText.sourceLanguage,
        ts.targetLanguage
      );
      setTestResult(result);
    } catch (err) {
      setTestError(err instanceof Error ? err.message : String(err));
    } finally {
      setTesting(false);
    }
  };

  const browseModelsDir = async () => {
    const dir = await window.electronAPI.settings.browseDirectory("Select Translation Models Directory");
    if (dir) updateLocal({ modelsDir: dir });
  };

  return (
    <div className="space-y-6">
      <SettingsSection title="Translation">
        <SettingsField label="Enable translation" helper="Translate transcripts automatically" horizontal>
          <Switch checked={ts.enabled} onCheckedChange={(v) => updateTranslation({ enabled: v })} />
        </SettingsField>

        <SettingsField label="Provider" helper="Translation backend to use">
          <Select
            value={ts.provider}
            onValueChange={(v) => updateTranslation({ provider: v as "google-free" | "translate-local" })}
          >
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="google-free">Google Translate</SelectItem>
              <SelectItem value="translate-local">Local TranslateService</SelectItem>
            </SelectContent>
          </Select>
        </SettingsField>

        <SettingsField label="Target language" helper="Translate transcripts into this language">
          <Select
            value={ts.targetLanguage}
            onValueChange={(v) => updateTranslation({ targetLanguage: v })}
          >
            <SelectTrigger className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {LANGUAGES.map((l) => (
                <SelectItem key={l.value} value={l.value}>{l.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </SettingsField>
      </SettingsSection>

      {ts.provider === "google-free" ? (
        <SettingsSection title="Provider: Google Translate">
          <SettingsField label="Endpoint" helper="Free Google Translate API endpoint">
            <Input
              value={ts.google.endpoint}
              onChange={(e) => updateTranslation({ google: { endpoint: e.target.value } })}
              className="font-mono text-xs"
            />
          </SettingsField>
        </SettingsSection>
      ) : (
        <>
          <SettingsSection title="Provider: Local TranslateService">
            <SettingsField label="Base URL" helper="Local translation service address">
              <Input
                value={ts.local.baseUrl}
                onChange={(e) => updateLocal({ baseUrl: e.target.value })}
                className="font-mono text-xs"
              />
            </SettingsField>

            <SettingsField label="Models directory" helper="Path to NLLB-200 CTranslate2 model files">
              <div className="flex gap-2">
                <Input
                  value={ts.local.modelsDir}
                  onChange={(e) => updateLocal({ modelsDir: e.target.value })}
                  className="font-mono text-xs flex-1"
                />
                <Button variant="outline" size="icon" onClick={browseModelsDir}>
                  <FolderSearch className="h-4 w-4" />
                </Button>
                <Button
                  variant="outline"
                  size="icon"
                  onClick={() => window.electronAPI.settings.openDirectory(ts.local.modelsDir)}
                >
                  <FolderOpen className="h-4 w-4" />
                </Button>
              </div>
            </SettingsField>
          </SettingsSection>

          <SettingsSection title="Service Status">
            <div className="rounded-md border bg-card p-4 space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Badge variant={statusBadge[status.status]?.variant ?? "secondary"}>
                    {statusBadge[status.status]?.label ?? status.status}
                  </Badge>
                  {status.pid && (
                    <span className="text-xs text-muted-foreground">PID: {status.pid}</span>
                  )}
                  {status.uptime != null && (
                    <span className="text-xs text-muted-foreground">
                      Uptime: {Math.floor(status.uptime / 60)}m {status.uptime % 60}s
                    </span>
                  )}
                </div>
                <Button variant="outline" size="sm" onClick={restart}>
                  <RotateCw className="h-3.5 w-3.5 mr-1" />
                  Restart Service
                </Button>
              </div>

              {status.loadedModels.length > 0 && (
                <p className="text-xs text-muted-foreground">
                  Models: {status.loadedModels.join(", ")}
                </p>
              )}

              {status.error && (
                <p className="text-xs text-destructive">{status.error}</p>
              )}
            </div>
          </SettingsSection>

          <SettingsSection title="Service Logs">
            <div className="rounded-md border bg-muted/30 p-3">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs text-muted-foreground">Recent logs (max 50 lines)</span>
                <Button variant="ghost" size="sm" onClick={clearLogs} disabled={logs.length === 0}>
                  <Trash2 className="h-3 w-3 mr-1" />
                  Clear
                </Button>
              </div>
              <ScrollArea className="h-40 rounded bg-black/50 p-2">
                {logs.length === 0 ? (
                  <p className="text-xs text-muted-foreground italic p-2">No logs yet...</p>
                ) : (
                  logs.map((line, i) => (
                    <p key={i} className="text-xs font-mono text-muted-foreground whitespace-nowrap">
                      {line}
                    </p>
                  ))
                )}
              </ScrollArea>
            </div>
          </SettingsSection>
        </>
      )}

      <Separator />

      <SettingsSection title="Test Translation">
        <SettingsField label="Source text">
          <Textarea
            value={testText}
            onChange={(e) => setTestText(e.target.value)}
            rows={2}
            className="resize-none"
          />
        </SettingsField>

        <Button onClick={runTest} disabled={testing || !testText.trim()}>
          <RefreshCw className={`h-4 w-4 mr-2 ${testing ? "animate-spin" : ""}`} />
          Translate
        </Button>

        {testResult && (
          <div className="space-y-2">
            <div className="rounded-md border bg-muted/50 p-3">
              <p className="text-sm">{testResult.translatedText}</p>
            </div>
            <p className="text-xs text-muted-foreground">
              Provider: {testResult.providerName} · {testResult.durationMs}ms
            </p>
          </div>
        )}

        {testError && (
          <p className="text-sm text-destructive">{testError}</p>
        )}
      </SettingsSection>
    </div>
  );
}
```

- [ ] **Step 2: Verify compiles**

```bash
cd desktop && npx tsc --noEmit
```

Expected: no errors

- [ ] **Step 3: Commit**

```bash
git add desktop/src/components/settings/TranslationSettings.tsx
git commit -m "feat(ui): add service status, logs, and model path settings to Translation page"
```

---

### Task 10: Update forge.config.ts

**Files:**
- Modify: `desktop/forge.config.ts`

- [ ] **Step 1: Add bin/translate to extraResource (already included via "bin")**

The existing `extraResource: ["bin", "native"]` already includes the entire `bin/` directory. No changes needed — just verify:

```typescript
extraResource: ["bin", "native"],
```

The `bin/translate/translate-service(.exe)` will be bundled automatically.

- [ ] **Step 2: Verify forge config compiles**

```bash
cd desktop && npx tsc --noEmit forge.config.ts
```

Expected: no errors

- [ ] **Step 3: Commit (if any changes)**

No changes needed — forge.config.ts already includes `bin/` in extraResource.

---

### Task 11: PyInstaller Build Scripts

**Files:**
- Create: `translate/scripts/build_pyinstaller.ps1`
- Create: `translate/scripts/build_pyinstaller.sh`

- [ ] **Step 1: Implement Windows build script**

`translate/scripts/build_pyinstaller.ps1`:

```powershell
<#
.SYNOPSIS
    Build translate-service.exe from Python app using PyInstaller.
.DESCRIPTION
    Output goes to ../desktop/bin/translate/
.PARAMETER clean
    Remove build/ and dist/ before building.
#>
param([switch]$clean)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Resolve-Path "$scriptDir/.."
$outputDir = Resolve-Path "$scriptDir/../../desktop/bin/translate" -ErrorAction SilentlyContinue

if (-not $outputDir) {
    New-Item -ItemType Directory -Path "$scriptDir/../../desktop/bin/translate" -Force | Out-Null
    $outputDir = Resolve-Path "$scriptDir/../../desktop/bin/translate"
}

Write-Host "Building translate-service.exe..."
Write-Host "  Project: $projectDir"
Write-Host "  Output:  $outputDir"

if ($clean) {
    $buildDir = Join-Path $projectDir "build"
    $distDir  = Join-Path $projectDir "dist"
    if (Test-Path $buildDir) { Remove-Item -Recurse -Force $buildDir }
    if (Test-Path $distDir)  { Remove-Item -Recurse -Force $distDir }
    Write-Host "  Cleaned build/dist directories"
}

& "$projectDir/.venv/Scripts/pip.exe" install pyinstaller
if ($LASTEXITCODE -ne 0) { throw "Failed to install pyinstaller" }

Push-Location $projectDir

try {
    & "$projectDir/.venv/Scripts/pyinstaller.exe" `
        --onefile `
        --name translate-service `
        --distpath $outputDir `
        --workpath "$projectDir/build" `
        --specpath "$projectDir/build" `
        --add-data ".env.example;." `
        --hidden-import "app.translator" `
        --hidden-import "app.translator.nllb_ct2" `
        --hidden-import "app.translator.model_manager" `
        --hidden-import "app.translator.session_cache" `
        --hidden-import "app.postprocess" `
        --hidden-import "app.postprocess.vi_normalizer" `
        --hidden-import "app.postprocess.glossary" `
        --hidden-import "app.utils" `
        --hidden-import "app.utils.text" `
        --hidden-import "app.utils.logger" `
        --hidden-import "ctranslate2" `
        --hidden-import "transformers" `
        --hidden-import "sentencepiece" `
        app/main.py

    if ($LASTEXITCODE -ne 0) { throw "PyInstaller build failed" }
} finally {
    Pop-Location
}

Write-Host "Done: $outputDir/translate-service.exe"
```

- [ ] **Step 2: Implement macOS/Linux build script**

`translate/scripts/build_pyinstaller.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="$(cd "$SCRIPT_DIR/../../desktop/bin/translate" 2>/dev/null || mkdir -p "$SCRIPT_DIR/../../desktop/bin/translate" && cd "$SCRIPT_DIR/../../desktop/bin/translate" && pwd)"

echo "Building translate-service..."
echo "  Project: $PROJECT_DIR"
echo "  Output:  $OUTPUT_DIR"

if [[ "${1:-}" == "--clean" ]]; then
  rm -rf "$PROJECT_DIR/build" "$PROJECT_DIR/dist"
  echo "  Cleaned build/dist directories"
fi

"$PROJECT_DIR/.venv/bin/pip" install pyinstaller

pushd "$PROJECT_DIR" > /dev/null

"$PROJECT_DIR/.venv/bin/pyinstaller" \
  --onefile \
  --name translate-service \
  --distpath "$OUTPUT_DIR" \
  --workpath "$PROJECT_DIR/build" \
  --specpath "$PROJECT_DIR/build" \
  --add-data ".env.example:." \
  --hidden-import "app.translator" \
  --hidden-import "app.translator.nllb_ct2" \
  --hidden-import "app.translator.model_manager" \
  --hidden-import "app.translator.session_cache" \
  --hidden-import "app.postprocess" \
  --hidden-import "app.postprocess.vi_normalizer" \
  --hidden-import "app.postprocess.glossary" \
  --hidden-import "app.utils" \
  --hidden-import "app.utils.text" \
  --hidden-import "app.utils.logger" \
  --hidden-import "ctranslate2" \
  --hidden-import "transformers" \
  --hidden-import "sentencepiece" \
  app/main.py

popd > /dev/null

echo "Done: $OUTPUT_DIR/translate-service"
```

```bash
chmod +x translate/scripts/build_pyinstaller.sh
```

- [ ] **Step 3: Commit**

```bash
git add translate/scripts/build_pyinstaller.ps1 translate/scripts/build_pyinstaller.sh
chmod +x translate/scripts/build_pyinstaller.sh
git commit -m "feat(build): add PyInstaller build scripts for Windows, macOS, and Linux"
```

---

### Task 12: Integration Verification

**Files:**
- None (manual verification)

- [ ] **Step 1: TypeScript type-check entire desktop project**

```bash
cd desktop && npx tsc --noEmit
```

Expected: no errors

- [ ] **Step 2: Verify all Python modules import correctly**

```bash
cd translate && python -c "
from app.config import get_settings
from app.utils.logger import configure_logging
from app.main import app
print('All imports OK')
print('Routes:', [r.path for r in app.routes])
"
```

Expected: `All imports OK` and routes listed

- [ ] **Step 3: Verify logger works**

```bash
cd translate && python -c "
from app.utils.logger import configure_logging
import tempfile, os
tmp = tempfile.mkdtemp()
configure_logging('INFO', log_dir=tmp)
import logging, time
log = logging.getLogger('test')
log.info('test message')
time.sleep(0.1)  # let file handler flush
files = os.listdir(tmp)
print('Log files:', files)
for f in files:
    with open(os.path.join(tmp, f)) as fh:
        print(fh.read())
"
```

Expected: log file created with timestamp name containing "test message"

- [ ] **Step 4: Commit final verification report**

```bash
git commit --allow-empty -m "test: verify TypeScript compiles and Python imports pass"
```

---
