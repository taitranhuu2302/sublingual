import koffi from "koffi";
import path from "node:path";
import { parentPort } from "node:worker_threads";

if (!parentPort) {
  console.error("[vosk-worker] parentPort is not available; worker must be run as a Worker thread");
  process.exit(1);
}

const LIB_NAME = process.platform === "win32" ? "libvosk.dll" : "libvosk.dylib";

function getLibDir(): string {
  const appPath = process.env.APP_PATH;
  const resourcesPath = process.env.RESOURCES_PATH;
  if (resourcesPath) return path.join(resourcesPath, "bin", "vosk");
  if (appPath) return path.join(appPath, "bin", "vosk");
  if (process.resourcesPath) return path.join(process.resourcesPath, "bin", "vosk");
  return path.join(process.cwd(), "bin", "vosk");
}

function ensureDllPath(): void {
  if (process.platform !== "win32") return;
  const dir = getLibDir();
  const current = process.env.PATH ?? "";
  if (!current.includes(dir)) {
    process.env.PATH = `${dir}${path.delimiter}${current}`;
  }
}

let modelNew: (...args: any[]) => any;
let modelFree: (...args: any[]) => void;
let recognizerNew: (...args: any[]) => any;
let recognizerFree: (...args: any[]) => void;
let recognizerSetWords: (...args: any[]) => void;
let recognizerSetPartialWords: (...args: any[]) => void;
let acceptWaveform: (...args: any[]) => any;
let getResult: (...args: any[]) => any;
let getPartialResult: (...args: any[]) => any;
let getFinalResult: (...args: any[]) => any;
let modelSetAddPunc: (...args: any[]) => void;

try {
  ensureDllPath();

  const VoskModel = koffi.opaque("VoskModel");
  const VoskModelPtr = koffi.pointer(VoskModel);
  const VoskRecognizer = koffi.opaque("VoskRecognizer");
  const VoskRecognizerPtr = koffi.pointer(VoskRecognizer);

  const lib = koffi.load(path.join(getLibDir(), LIB_NAME));

  lib.func("vosk_set_log_level", "void", ["int"])(-1);

  modelNew = lib.func("vosk_model_new", VoskModelPtr, ["string"]);
  modelFree = lib.func("vosk_model_free", "void", [VoskModelPtr]);
  recognizerNew = lib.func("vosk_recognizer_new", VoskRecognizerPtr, [VoskModelPtr, "float"]);
  recognizerFree = lib.func("vosk_recognizer_free", "void", [VoskRecognizerPtr]);
  recognizerSetWords = lib.func("vosk_recognizer_set_words", "void", [VoskRecognizerPtr, "bool"]);
  recognizerSetPartialWords = lib.func("vosk_recognizer_set_partial_words", "void", [VoskRecognizerPtr, "bool"]);
  acceptWaveform = lib.func("vosk_recognizer_accept_waveform", "bool", [VoskRecognizerPtr, koffi.pointer("void"), "int"]);
  getResult = lib.func("vosk_recognizer_result", "string", [VoskRecognizerPtr]);
  getPartialResult = lib.func("vosk_recognizer_partial_result", "string", [VoskRecognizerPtr]);
  getFinalResult = lib.func("vosk_recognizer_final_result", "string", [VoskRecognizerPtr]);

  try {
    modelSetAddPunc = lib.func("vosk_model_set_add_punc", "void", [VoskModelPtr, "string"]);
  } catch {
    modelSetAddPunc = () => {};
    console.log("[vosk-worker] vosk_model_set_add_punc not available in this Vosk version");
  }

  console.log("[vosk-worker] Vosk ready");
} catch (err) {
  console.error("[vosk-worker] Failed to initialize Vosk bindings:", err);
  process.exit(1);
}

let model: unknown = null;
let recognizer: unknown = null;

function tryParseJson(s: string): Record<string, unknown> | null {
  if (!s) return null;
  try { return JSON.parse(s); } catch { return null; }
}

parentPort.on("message", (msg: any) => {
  switch (msg.type) {
    case "start": {
      console.log("[vosk-worker] Loading model...");
      parentPort.postMessage({ type: "log", message: "Loading speech model..." });
      try {
        model = modelNew(msg.modelPath);
        if (!model) throw new Error("Failed to create Vosk model");

        if (msg.puncModelPath) {
          console.log("[vosk-worker] Attaching punctuation model...");
          parentPort.postMessage({ type: "log", message: "Loading punctuation model..." });
          modelSetAddPunc(model, msg.puncModelPath);
        }

        recognizer = recognizerNew(model, msg.sampleRate ?? 16000);
        if (!recognizer) throw new Error("Failed to create Vosk recognizer");

        recognizerSetWords(recognizer, true);
        recognizerSetPartialWords(recognizer, true);

        console.log("[vosk-worker] Model ready");
        parentPort.postMessage({ type: "ready" });
      } catch (err) {
        console.error("[vosk-worker] Start error:", err);
        if (recognizer) { recognizerFree(recognizer); recognizer = null; }
        if (model) { modelFree(model); model = null; }
        parentPort.postMessage({ type: "error", message: String(err) });
      }
      break;
    }

    case "audio": {
      if (!recognizer) break;
      try {
        const data = Buffer.from(msg.data);
        const isFinal = acceptWaveform(recognizer, data, data.length);

        if (isFinal) {
          const raw = getResult(recognizer) ?? "";
          const parsed = tryParseJson(raw);
          if (parsed?.text?.trim()) {
            parentPort.postMessage({
              type: "transcript",
              text: parsed.text.trim(),
              isFinal: true,
              timestamp: Date.now(),
            });
          }
        } else {
          const raw = getPartialResult(recognizer) ?? "";
          const parsed = tryParseJson(raw);
          if (parsed?.partial?.trim()) {
            parentPort.postMessage({
              type: "transcript",
              text: parsed.partial.trim(),
              isFinal: false,
              timestamp: Date.now(),
            });
          }
        }
      } catch (err) {
        console.error("[vosk-worker] acceptWaveform error:", err);
      }
      break;
    }

    case "stop": {
      console.log("[vosk-worker] Received stop command");
      try {
        if (recognizer) {
          const raw = getFinalResult(recognizer) ?? "";
          const parsed = tryParseJson(raw);
          if (parsed?.text?.trim()) {
            parentPort.postMessage({
              type: "transcript",
              text: parsed.text.trim(),
              isFinal: true,
              timestamp: Date.now(),
            });
          }
          recognizerFree(recognizer);
          recognizer = null;
        }
        if (model) {
          modelFree(model);
          model = null;
        }
      } catch (err) {
        console.error("[vosk-worker] Stop error:", err);
      }
      parentPort.postMessage({ type: "stopped" });
      process.exit(0);
      break;
    }
  }
});

console.log("[vosk-worker] Worker initialized, waiting for messages...");
