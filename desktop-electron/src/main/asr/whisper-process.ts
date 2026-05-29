import { spawn, ChildProcess } from "child_process";
import path from "path";
import { BrowserWindow } from "electron";
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
    "--no-timestamps", "false",
    "-", // read from stdin
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
        // skip non-JSON lines
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
  return path.join(__dirname, "../../bin", binName);
}
