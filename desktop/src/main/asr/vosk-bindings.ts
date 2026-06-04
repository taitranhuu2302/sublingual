import koffi from "koffi";
import path from "path";
import { app } from "electron";

const LIB_NAME = process.platform === "win32" ? "libvosk.dll" : "libvosk.dylib";

export function getLibDir(): string {
  if (app.isPackaged) {
    return path.join(process.resourcesPath, "bin", "vosk");
  }
  return path.join(app.getAppPath(), "bin", "vosk");
}

export function getLibPath(): string {
  return path.join(getLibDir(), LIB_NAME);
}

function ensureDllPath(): void {
  if (process.platform !== "win32") return;
  const dir = getLibDir();
  const current = process.env.PATH ?? "";
  if (!current.includes(dir)) {
    process.env.PATH = `${dir}${path.delimiter}${current}`;
  }
}

const isVoskSupported = process.platform === "darwin" || process.platform === "win32";

let lib: any = null;
let VoskModel: any = null;
let VoskModelPtr: any = null;
let VoskRecognizer: any = null;
let VoskRecognizerPtr: any = null;

let vosk_set_log_level: (...args: any[]) => void = () => {};
let vosk_model_new: (...args: any[]) => any = () => null;
let vosk_model_free: (...args: any[]) => void = () => {};
let vosk_recognizer_new: (...args: any[]) => any = () => null;
let vosk_recognizer_free: (...args: any[]) => void = () => {};
let vosk_recognizer_set_words: (...args: any[]) => void = () => {};
let vosk_recognizer_set_partial_words: (...args: any[]) => void = () => {};
let vosk_recognizer_accept_waveform: (...args: any[]) => any = () => false;
let vosk_recognizer_result: (...args: any[]) => any = () => null;
let vosk_recognizer_partial_result: (...args: any[]) => any = () => null;
let vosk_recognizer_final_result: (...args: any[]) => any = () => null;

if (isVoskSupported) {
  ensureDllPath();
  lib = koffi.load(getLibPath());

  VoskModel = koffi.opaque("VoskModel");
  VoskModelPtr = koffi.pointer(VoskModel);
  VoskRecognizer = koffi.opaque("VoskRecognizer");
  VoskRecognizerPtr = koffi.pointer(VoskRecognizer);

  vosk_set_log_level = lib.func("vosk_set_log_level", "void", ["int"]);

  vosk_model_new = lib.func("vosk_model_new", VoskModelPtr, ["string"]);
  vosk_model_free = lib.func("vosk_model_free", "void", [VoskModelPtr]);

  vosk_recognizer_new = lib.func("vosk_recognizer_new", VoskRecognizerPtr, [VoskModelPtr, "float"]);
  vosk_recognizer_free = lib.func("vosk_recognizer_free", "void", [VoskRecognizerPtr]);
  vosk_recognizer_set_words = lib.func("vosk_recognizer_set_words", "void", [VoskRecognizerPtr, "bool"]);
  vosk_recognizer_set_partial_words = lib.func("vosk_recognizer_set_partial_words", "void", [VoskRecognizerPtr, "bool"]);

  vosk_recognizer_accept_waveform = lib.func("vosk_recognizer_accept_waveform", "bool", [VoskRecognizerPtr, koffi.pointer("void"), "int"]);

  vosk_recognizer_result = lib.func("vosk_recognizer_result", "string", [VoskRecognizerPtr]);
  vosk_recognizer_partial_result = lib.func("vosk_recognizer_partial_result", "string", [VoskRecognizerPtr]);
  vosk_recognizer_final_result = lib.func("vosk_recognizer_final_result", "string", [VoskRecognizerPtr]);
}

export function setLogLevel(level: number): void {
  vosk_set_log_level(level);
}

export function modelNew(modelPath: string): unknown {
  const ptr = vosk_model_new(modelPath);
  if (!ptr) throw new Error("Failed to create Vosk model");
  return ptr;
}

export function modelFree(modelPtr: unknown): void {
  vosk_model_free(modelPtr);
}

export function recognizerNew(modelPtr: unknown, sampleRate: number): unknown {
  const ptr = vosk_recognizer_new(modelPtr, sampleRate);
  if (!ptr) throw new Error("Failed to create Vosk recognizer");
  return ptr;
}

export function recognizerFree(recPtr: unknown): void {
  vosk_recognizer_free(recPtr);
}

export function recognizerSetWords(recPtr: unknown, words: boolean): void {
  vosk_recognizer_set_words(recPtr, words);
}

export function recognizerSetPartialWords(recPtr: unknown, partialWords: boolean): void {
  vosk_recognizer_set_partial_words(recPtr, partialWords);
}

export function acceptWaveform(recPtr: unknown, data: Buffer): boolean {
  return vosk_recognizer_accept_waveform(recPtr, data, data.length);
}

export function getResult(recPtr: unknown): string {
  return vosk_recognizer_result(recPtr) ?? "";
}

export function getPartialResult(recPtr: unknown): string {
  return vosk_recognizer_partial_result(recPtr) ?? "";
}

export function getFinalResult(recPtr: unknown): string {
  return vosk_recognizer_final_result(recPtr) ?? "";
}
