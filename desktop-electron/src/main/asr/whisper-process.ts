import { spawn, ChildProcess } from "child_process";
import path from "path";
import { app, BrowserWindow } from "electron";
import { WhisperConfig, WhisperSegment } from "./whisper-types";

let whisperProcess: ChildProcess | null = null;

/**
 * Spawns whisper.cpp's `main` binary in streaming mode.
 * Expects raw 16kHz mono PCM s16le on stdin.
 * Outputs JSON segments on stdout.
 */
export function startWhisper(config: WhisperConfig, mainWindow: BrowserWindow) {
  if (whisperProcess) return;

  const binaryPath = getWhisperBinaryPath();

  whisperProcess = spawn(binaryPath, [
    "--model", config.modelPath,
    "--language", config.language,
    "--threads", String(config.threads ?? 4),
    "--output-json",
    "-",
  ]);

  let buffer = "";

  whisperProcess.stdout?.on("data", (chunk: Buffer) => {
    buffer += chunk.toString();
    const lines = buffer.split("\n");
    buffer = lines.pop() ?? "";

    for (const line of lines) {
      if (!line.trim()) continue;
      try {
        const segment: WhisperSegment = JSON.parse(line);
        mainWindow.webContents.send("asr:transcript", {
          text: segment.text,
          isFinal: segment.isFinal,
          timestamp: segment.t0,
        });
      } catch {
        // Check if it's a text output line (not JSON)
        if (line.trim() && !line.startsWith("whisper") && !line.startsWith("ggml")) {
          mainWindow.webContents.send("asr:transcript", {
            text: line.trim(),
            isFinal: true,
            timestamp: Date.now(),
          });
        }
      }
    }
  });

  whisperProcess.on("exit", () => {
    whisperProcess = null;
  });
}

export function feedAudio(pcmData: Buffer) {
  if (whisperProcess?.stdin?.writable) {
    whisperProcess.stdin.write(pcmData);
  }
}

export function stopWhisper() {
  if (whisperProcess) {
    whisperProcess.stdin?.end();
    whisperProcess.kill();
    whisperProcess = null;
  }
}

function getWhisperBinaryPath(): string {
  // Platform-specific binary location
  const binName = process.platform === "win32" ? "whisper-cli.exe" : "whisper-cli";
  
  // In development, use the project bin directory
  // In production, use the app resources directory
  if (app.isPackaged) {
    return path.join(process.resourcesPath, "bin", binName);
  } else {
    // Development - find bin relative to project root
    return path.join(app.getAppPath(), "bin", binName);
  }
}

