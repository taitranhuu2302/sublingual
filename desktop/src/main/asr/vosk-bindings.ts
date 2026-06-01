import koffi from "koffi";
import path from "path";
import { app } from "electron";

function getLibPath(): string {
  if (app.isPackaged) {
    return path.join(process.resourcesPath, "bin", "vosk", "libvosk.dylib");
  }
  return path.join(app.getAppPath(), "bin", "vosk", "libvosk.dylib");
}

const lib = koffi.load(getLibPath());

const VoskModel = koffi.opaque("VoskModel");
const VoskModelPtr = koffi.pointer(VoskModel);
const VoskRecognizer = koffi.opaque("VoskRecognizer");
const VoskRecognizerPtr = koffi.pointer(VoskRecognizer);

const vosk_set_log_level = lib.func("vosk_set_log_level", "void", ["int"]);

const vosk_model_new = lib.func("vosk_model_new", VoskModelPtr, ["string"]);
const vosk_model_free = lib.func("vosk_model_free", "void", [VoskModelPtr]);

const vosk_recognizer_new = lib.func("vosk_recognizer_new", VoskRecognizerPtr, [VoskModelPtr, "float"]);
const vosk_recognizer_free = lib.func("vosk_recognizer_free", "void", [VoskRecognizerPtr]);
const vosk_recognizer_set_words = lib.func("vosk_recognizer_set_words", "void", [VoskRecognizerPtr, "bool"]);
const vosk_recognizer_set_partial_words = lib.func("vosk_recognizer_set_partial_words", "void", [VoskRecognizerPtr, "bool"]);

const vosk_recognizer_accept_waveform = lib.func("vosk_recognizer_accept_waveform", "bool", [VoskRecognizerPtr, koffi.pointer("void"), "int"]);

const vosk_recognizer_result = lib.func("vosk_recognizer_result", "string", [VoskRecognizerPtr]);
const vosk_recognizer_partial_result = lib.func("vosk_recognizer_partial_result", "string", [VoskRecognizerPtr]);
const vosk_recognizer_final_result = lib.func("vosk_recognizer_final_result", "string", [VoskRecognizerPtr]);

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
