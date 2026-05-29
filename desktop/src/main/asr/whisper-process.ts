import { spawn, ChildProcess } from "child_process";
import path from "path";
import fs from "fs";
import os from "os";
import { app, BrowserWindow } from "electron";
import { WhisperConfig } from "./whisper-types";
import { getSettings } from "../settings/settings-store";

let mainWindowRef: BrowserWindow | null = null;
let configRef: WhisperConfig | null = null;

// Audio buffer for batch processing
let audioBuffer: Buffer[] = [];
let isProcessing = false;
const SAMPLE_RATE = 16000;
const BYTES_PER_SAMPLE = 2;
let batchTimer: ReturnType<typeof setInterval> | null = null;

function getChunkDurationMs(): number {
  const preset = getSettings().speechToText.realtimeChunkPreset;
  switch (preset) {
    case "Fast": return 500;
    case "Balanced": return 1000;
    case "Accurate": return 2000;
    default: return 1000;
  }
}

export function startWhisper(config: WhisperConfig, mainWindow: BrowserWindow) {
  mainWindowRef = mainWindow;
  configRef = config;
  audioBuffer = [];
  isProcessing = false;
  
  const chunkMs = getChunkDurationMs();
  batchTimer = setInterval(() => {
    processBatch();
  }, chunkMs);
}

export function feedAudio(pcmData: Buffer) {
  audioBuffer.push(pcmData);
}

async function processBatch() {
  if (isProcessing || audioBuffer.length === 0 || !mainWindowRef || mainWindowRef.isDestroyed() || !configRef) {
    return;
  }
  
  isProcessing = true;
  
  // Concatenate all buffered audio
  const combinedBuffer = Buffer.concat(audioBuffer);
  audioBuffer = []; // Clear buffer
  
  // Skip if too little audio (less than 0.5 seconds)
  const minSamples = SAMPLE_RATE * 0.5;
  if (combinedBuffer.length < minSamples * BYTES_PER_SAMPLE) {
    isProcessing = false;
    return;
  }
  
  try {
    // Write to temp WAV file
    const tempFile = path.join(os.tmpdir(), `whisper_${Date.now()}.wav`);
    writeWavFile(tempFile, combinedBuffer, SAMPLE_RATE);
    
    // Run whisper on the file
    const binaryPath = getWhisperBinaryPath();
    const result = await runWhisper(binaryPath, configRef.modelPath, configRef.language, tempFile);
    
    // Clean up temp file
    try { fs.unlinkSync(tempFile); } catch {}
    
    // Send result to renderer — filter out noise markers
    if (result && mainWindowRef && !mainWindowRef.isDestroyed()) {
      const trimmed = result.trim();
      // Skip whisper noise markers like [BLANK_AUDIO], (silence), [Music], etc.
      if (/^\[.*\]$|^\(.*\)$/.test(trimmed)) {
        isProcessing = false;
        return;
      }
      mainWindowRef.webContents.send("asr:transcript", {
        text: trimmed,
        isFinal: true,
        timestamp: Date.now(),
      });
    }
  } catch (err) {
    console.error("[whisper] processBatch error:", err);
  }
  
  isProcessing = false;
}

function runWhisper(binaryPath: string, modelPath: string, language: string, audioFile: string): Promise<string> {
  return new Promise((resolve) => {
    const proc = spawn(binaryPath, [
      "--model", modelPath,
      "--language", language,
      "--threads", "4",
      "--no-prints",
      audioFile,
    ], {
      // Set cwd to binary dir so Windows can find DLLs (whisper.dll, ggml*.dll)
      cwd: path.dirname(binaryPath),
    });
    
    let output = "";
    let stderr = "";
    
    proc.stdout?.on("data", (chunk: Buffer) => {
      output += chunk.toString();
    });

    proc.stderr?.on("data", (chunk: Buffer) => {
      stderr += chunk.toString();
    });
    
    proc.on("close", (code) => {
      if (stderr) console.error("[whisper] stderr:", stderr.slice(0, 500));
      if (code !== 0) console.error("[whisper] exited with code:", code);
      // Extract text from output (remove timestamps like [00:00:00.000 --> 00:00:03.000])
      const text = output
        .split("\n")
        .map(line => line.replace(/^\s*\[\d+:\d+:\d+\.\d+\s*-->\s*\d+:\d+:\d+\.\d+\]\s*/, "").trim())
        .filter(line => line.length > 0)
        .join(" ")
        .trim();
      
      resolve(text);
    });
    
    proc.on("error", () => resolve(""));
  });
}

function writeWavFile(filePath: string, pcmData: Buffer, sampleRate: number) {
  const numChannels = 1;
  const bitsPerSample = 16;
  const byteRate = sampleRate * numChannels * (bitsPerSample / 8);
  const blockAlign = numChannels * (bitsPerSample / 8);
  const dataSize = pcmData.length;
  const fileSize = 36 + dataSize;
  
  const header = Buffer.alloc(44);
  
  // RIFF header
  header.write("RIFF", 0);
  header.writeUInt32LE(fileSize, 4);
  header.write("WAVE", 8);
  
  // fmt chunk
  header.write("fmt ", 12);
  header.writeUInt32LE(16, 16);
  header.writeUInt16LE(1, 20);
  header.writeUInt16LE(numChannels, 22);
  header.writeUInt32LE(sampleRate, 24);
  header.writeUInt32LE(byteRate, 28);
  header.writeUInt16LE(blockAlign, 32);
  header.writeUInt16LE(bitsPerSample, 34);
  
  // data chunk
  header.write("data", 36);
  header.writeUInt32LE(dataSize, 40);
  
  const fd = fs.openSync(filePath, "w");
  fs.writeSync(fd, header);
  fs.writeSync(fd, pcmData);
  fs.closeSync(fd);
}

export function stopWhisper() {
  if (batchTimer) {
    clearInterval(batchTimer);
    batchTimer = null;
  }
  
  audioBuffer = [];
  mainWindowRef = null;
  configRef = null;
}

function getWhisperBinaryPath(): string {
  const binName = process.platform === "win32" ? "whisper-cli.exe" : "whisper-cli";
  
  if (app.isPackaged) {
    return path.join(process.resourcesPath, "bin", binName);
  } else {
    return path.join(app.getAppPath(), "bin", binName);
  }
}
