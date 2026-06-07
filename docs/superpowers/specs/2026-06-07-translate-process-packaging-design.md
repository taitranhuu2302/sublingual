# Translate Service Packaging & Process Management — Design Spec

**Date**: 2026-06-07
**Status**: approved

## Overview

Package the Python translate service (`translate/`) with PyInstaller, bundle it as a subprocess of the Electron desktop app (`desktop/`), and auto-start it when the app launches. Add file-based logging with timestamp rotation for both desktop and translate service.

### Motivations

- Currently the translate service must be started manually — users need to run `uvicorn app.main:app` separately
- Desktop app's `LocalTranslationProvider` calls `http://127.0.0.1:3333` but nothing spawns the server
- No unified logging — debug is hard for packaged/production builds
- Logs grow unbounded — need rotation to prevent disk bloat

---

## Architecture

```
desktop/
├── src/main/
│   ├── main.ts                        ★ MODIFY: auto-start/kill translate subprocess
│   └── translation/
│       ├── translate-process.ts        ★ NEW: child_process.spawn manager
│       ├── translate-local.ts          (unchanged — HTTP client)
│       └── translation-service.ts      (unchanged)
├── src/main/utils/
│   └── logger.ts                       ★ NEW: file-based logger with rotation
├── src/main/ipc/
│   └── translation-handlers.ts         ★ MODIFY: add status/restart/health IPC
├── bin/
│   └── translate/                      ★ NEW: PyInstaller output dir
│       └── translate-service(.exe)
└── forge.config.ts                     ★ MODIFY: extraResource includes bin/translate
```

```
~/.sublingual/
├── settings.json
├── models/                    ← Vosk models (unchanged)
├── translate-models/          ★ NEW: NLLB models (user-configurable path)
└── logs/                      ★ NEW: file-based logs
    ├── desktop/
    │   ├── main-2026-06-07-14-30-05.log
    │   ├── main-2026-06-07-18-22-41.log
    │   └── main-2026-06-08-09-15-00.log  ← current
    └── translate/
        ├── service-2026-06-07-14-30-05.log
        └── service-2026-06-08-09-15-00.log  ← current
```

### Flow: App Startup

```
app.on('ready')
  → createWindow()
  → translateProcess.start()
      → resolve exe: app.isPackaged
          ? process.resourcesPath/bin/translate/translate-service(.exe)
          : translate/.venv/Scripts/python -m uvicorn app.main:app
      → pass --port, --host, --models-dir, --log-dir
      → spawn child process
  → poll GET /health mỗi 500ms đến khi status=ok (timeout 30s)
  → emit "translate:service-ready" IPC → renderer cập nhật status
```

### Flow: App Shutdown

```
app.on('before-quit')
  → translateProcess.stop()
      → process.kill('SIGTERM') → wait 5s → process.kill('SIGKILL')
      → emit "translate:service-stopped"
```

---

## Section 1: Settings & Storage

### New/Modified Setting Fields

**`TranslationProviderLocal` mở rộng** (`settings-store.ts`):

```typescript
export interface TranslationProviderLocal {
  baseUrl: string;        // giữ nguyên, default "http://127.0.0.1:3333"
  modelsDir: string;      // ★ NEW: default ~/.sublingual/translate-models
  logsDir: string;        // ★ NEW: default ~/.sublingual/logs/translate
}
```

Default `modelsDir`: `path.join(os.homedir(), ".sublingual", "translate-models")`

Đặt tên khác `models` để tránh trùng với Vosk model directory.

### User-configurable via Settings UI

Trong `TranslationSettings.tsx`, khi provider = `translate-local`:

- **modelsDir**: input field + nút Browse (dùng `settings:browse-directory` IPC hiện có)
- **logsDir**: input field + nút Browse

---

## Section 2: Process Management

### File: `translate-process.ts` (NEW)

`desktop/src/main/translation/translate-process.ts`

```typescript
interface TranslateProcessStatus {
  status: "running" | "starting" | "stopped" | "error";
  pid: number | null;
  uptime: number | null;
  loadedModels: string[];
  error: string | null;
}

// Core API
function start(): void;           // spawn subprocess, non-blocking
function stop(): Promise<void>;   // kill subprocess
function restart(): Promise<void>;// stop() + start()
function getStatus(): TranslateProcessStatus;
```

**Resolve command (dev vs production):**

```typescript
function resolveCommand(): { bin: string; args: string[] } {
  if (app.isPackaged) {
    const name = process.platform === "win32" ? "translate-service.exe" : "translate-service";
    return {
      bin: path.join(process.resourcesPath, "bin", "translate", name),
      args: [],
    };
  }
  // Dev: use venv
  return {
    bin: path.join(projectRoot, "translate/.venv/Scripts/python"),
    args: ["-m", "uvicorn", "app.main:app"],
  };
}
```

**CLI arguments passed to translate-service:**

```
--host 127.0.0.1
--port 3333
--models-dir <modelsDir from settings>
--log-dir <logsDir from settings>
```

**Health polling:**

- Interval: 500ms
- Timeout: 30s (nếu không ready → emit error)
- Poll `GET http://127.0.0.1:{port}/health`
- Khi `status=ok` → emit `translate:service-ready`
- Khi service crash (exit code != 0) → emit `translate:service-error`

### Main process integration (`main.ts`)

```typescript
import { translateProcess } from "./main/translation/translate-process";

app.on("ready", () => {
  createWindow();
  translateProcess.start();
});

app.on("before-quit", () => {
  translateProcess.stop();
});
```

### IPC Handlers (`translation-handlers.ts`)

```typescript
// handle (renderer → main):
ipcMain.handle("translate:get-status", () => translateProcess.getStatus());
ipcMain.handle("translate:restart", async () => { await translateProcess.restart(); });

// push (main → renderer):
// "translate:status-change" → { status, message }
// "translate:service-ready" → { pid, loadedModels }
// "translate:service-stopped" → {}
// "translate:service-error" → { error }
// "translate:log" → { line }
// "translate:model-download" → { modelName, percent, status }
```

### forge.config.ts

```typescript
extraResource: ["bin", "native"], // "bin" đã có, chỉ cần thêm bin/translate/ vào đó
```

---

## Section 3: Renderer UI

### TranslationSettings.tsx (mở rộng)

Khi `provider = translate-local`, thêm 3 section:

#### A. Model Storage Path

```
┌─ Model Storage ─────────────────────────────────────────────┐
│ Path: [~/.sublingual/translate-models      ]  [Browse]      │
└─────────────────────────────────────────────────────────────┘
```

#### B. Service Status Panel

```
┌─ Service Status ────────────────────────────────────────────┐
│ ● Running   PID: 12345   Uptime: 5m 32s                      │
│ Models: en→vi, vi→en                                         │
│                                      [Restart Service]        │
│ ┌─ Logs ───────────────────────────────────────────────────┐ │
│ │ 10:30:01  warming up NLLB-200 fast model...              │ │
│ │ 10:30:12  model ready, listening on :3333                │ │
│ └──────────────────────────────────────────────────────────┘ │
│                                              [Clear Logs]     │
└──────────────────────────────────────────────────────────────┘
```

Status dot color:
- `●` xanh (`running`)
- `●` vàng (`starting`)
- `●` đỏ (`stopped` / `error`)

Logs: tối đa 50 dòng trong UI, auto-scroll khi có dòng mới, nút Clear để xóa.

#### C. Model Download Progress (chỉ hiện khi first-run tải model)

```
┌─ Downloading Translation Model ─────────────────────────────┐
│ en→vi model                                    [████▒▒  45%]  │
│ Size: 1.2 GB   Speed: 3.4 MB/s   ETA: 2:30                    │
│                                        [Cancel Download]       │
└──────────────────────────────────────────────────────────────┘
```

### Hook: `useTranslateService`

```typescript
// src/hooks/use-translate-service.ts
function useTranslateService(): {
  status: TranslateProcessStatus;
  logs: string[];
  downloadProgress: { modelName: string; percent: number; status: string } | null;
  restart: () => Promise<void>;
  clearLogs: () => void;
}
```

---

## Section 4: File-based Logging

### Directory

```
~/.sublingual/logs/
├── desktop/
│   ├── main-2026-06-07-14-30-05.log
│   ├── main-2026-06-07-18-22-41.log
│   └── main-2026-06-08-09-15-00.log  ← current
└── translate/
    ├── service-2026-06-07-14-30-05.log
    └── service-2026-06-08-09-15-00.log  ← current
```

### Rotation Rules

| Rule | Value |
|------|-------|
| File naming | `<name>-YYYY-MM-DD-HH-MM-SS.log` |
| Rotation trigger | File size > **5 MB** |
| Retention | **10 files** mới nhất per service |
| Startup cleanup | Xóa file cũ vượt quá 10, ưu tiên xóa cũ nhất (theo timestamp trong tên file) |
| Total cap | ~50 MB per service × 2 = ~**100 MB max** |

### Desktop Logger

File mới: `desktop/src/main/utils/logger.ts`

```typescript
// Ghi log thay thế console.log / console.error trong main process
logger.info("translate service started, pid=12345");
logger.error("translate service crashed", err);
// → ghi vào ~/.sublingual/logs/desktop/main-<timestamp>.log
// → đồng thời forward ra console (dev) và IPC "translate:log" (cho Settings panel)
```

Format mỗi dòng: `[YYYY-MM-DD HH:MM:SS] [LEVEL] message`

Khi `fs.statSync(file).size > 5 * 1024 * 1024`:
1. Tạo file mới với timestamp hiện tại
2. Redirect writes sang file mới
3. Nếu tổng số file > 10 → xóa file cũ nhất (sort theo timestamp trong tên)

### Translate Service Logger

Modify: `translate/app/utils/logger.py`

```python
def configure_logging(level: str = "INFO", log_dir: str | None = None) -> None:
    handlers = [logging.StreamHandler()]
    if log_dir:
        log_path = Path(log_dir)
        log_path.mkdir(parents=True, exist_ok=True)
        timestamp = datetime.now().strftime("%Y-%m-%d-%H-%M-%S")
        file_handler = logging.FileHandler(log_path / f"service-{timestamp}.log")
        handlers.append(file_handler)
    logging.basicConfig(
        level=getattr(logging, level.upper(), logging.INFO),
        format="%(asctime)s | %(levelname)s | %(name)s | %(message)s",
        handlers=handlers,
    )
```

Rotation logic trong Python:
- Sau mỗi `write`, check `os.path.getsize(log_file)`
- Nếu > 5MB → đóng file hiện tại, tạo file mới với timestamp
- Startup cleanup: xóa file quá 10 file gần nhất (sort theo `os.path.getmtime`)

Note: `log_dir` được truyền qua CLI arg `--log-dir` từ electron main process.

---

## Key Files Summary

| Action | Path | Responsibility |
|--------|------|----------------|
| **Create** | `desktop/src/main/translation/translate-process.ts` | Spawn/kill/monitor Python subprocess |
| **Create** | `desktop/src/main/utils/logger.ts` | File-based logger with timestamp rotation |
| **Create** | `desktop/src/hooks/use-translate-service.ts` | React hook for service status + logs |
| **Create** | `translate/scripts/build_pyinstaller.ps1` | PyInstaller build script (Windows) |
| **Create** | `translate/scripts/build_pyinstaller.sh` | PyInstaller build script (macOS/Linux) |
| **Modify** | `desktop/src/main.ts` | Auto-start/stop translate process |
| **Modify** | `desktop/src/main/ipc/translation-handlers.ts` | Add status/restart/log IPC channels |
| **Modify** | `desktop/src/main/ipc-handlers.ts` | Register new IPC handlers |
| **Modify** | `desktop/src/main/settings/settings-store.ts` | Add `modelsDir`, `logsDir` to settings |
| **Modify** | `desktop/src/components/settings/TranslationSettings.tsx` | Service status panel + model path + download progress |
| **Modify** | `desktop/src/preload.ts` | Expose new translation IPC to renderer |
| **Modify** | `desktop/src/types/electron-api.d.ts` | Add new types for translate service |
| **Modify** | `desktop/forge.config.ts` | Include `bin/translate` in extraResource |
| **Modify** | `translate/app/utils/logger.py` | Add file logging with rotation |
| **Modify** | `translate/app/main.py` | Accept `--log-dir` CLI arg for file logging |

---

## Error Handling

| Scenario | Behavior |
|----------|----------|
| PyInstaller exe not found (production) | Emit error, show "Service not installed" in UI |
| Python/uvicorn not found (dev) | Emit error, show "Python venv not found" |
| Service crashes (non-zero exit) | Emit error, show message + Restart button |
| Health check timeout (30s) | Emit error, kill process, show Restart button |
| Model files not downloaded (first-run) | Trigger download flow, show progress |
| Port 3333 already in use | Emit error, suggest changing port or killing existing process |
| Log directory not writable | Fallback to console-only logging, warn in UI |

## Testing Strategy

1. **Unit tests**: `logger.ts` rotation logic, `translate-process.ts` command resolution
2. **Integration tests**: Spawn/kill Python service from main process, verify health polling
3. **Manual QA**: Start desktop app, verify service starts in Settings page, quit app, verify process killed
4. **Log rotation test**: Write >5MB fake logs, verify rotation and cleanup

## Rollout Plan

1. Implement `logger.ts` (desktop file-based logging)
2. Modify `translate/app/utils/logger.py` (Python file logging)
3. Implement `translate-process.ts` (subprocess management)
4. Modify IPC handlers and preload bridge
5. Modify Settings UI (`TranslationSettings.tsx`)
6. Create PyInstaller build scripts
7. Modify `forge.config.ts` and `main.ts`
8. Test end-to-end (dev + packaged)
