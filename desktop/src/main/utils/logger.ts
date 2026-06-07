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
    const now = new Date();
    const pad = (n: number) => String(n).padStart(2, "0");
    const timestamp = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}-${pad(now.getHours())}-${pad(now.getMinutes())}-${pad(now.getSeconds())}`;
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
