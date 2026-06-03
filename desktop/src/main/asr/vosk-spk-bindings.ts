import koffi from "koffi";
import { getLibPath } from "./vosk-bindings";

const isVoskSupported = process.platform === "darwin";

let lib: any = null;
let VoskSpkModel: any = null;
let VoskSpkModelPtr: any = null;
let VoskSpkRecognizer: any = null;
let VoskSpkRecognizerPtr: any = null;

let vosk_spk_model_new: (...args: any[]) => any = () => null;
let vosk_spk_model_free: (...args: any[]) => void = () => {};
let vosk_spk_recognizer_new: (...args: any[]) => any = () => null;
let vosk_spk_recognizer_free: (...args: any[]) => void = () => {};
let vosk_spk_recognizer_accept_waveform: (...args: any[]) => any = () => false;
let vosk_recognizer_result: (...args: any[]) => any = () => null;

let initialized = false;

function init(): void {
  if (initialized) return;
  initialized = true;

  if (!isVoskSupported) return;

  try {
    lib = koffi.load(getLibPath());

    VoskSpkModel = koffi.opaque("VoskSpkModel");
    VoskSpkModelPtr = koffi.pointer(VoskSpkModel);
    VoskSpkRecognizer = koffi.opaque("VoskSpkRecognizer");
    VoskSpkRecognizerPtr = koffi.pointer(VoskSpkRecognizer);

    vosk_spk_model_new = lib.func("vosk_spk_model_new", VoskSpkModelPtr, ["string"]);
    vosk_spk_model_free = lib.func("vosk_spk_model_free", "void", [VoskSpkModelPtr]);
    vosk_spk_recognizer_new = lib.func(
      "vosk_spk_recognizer_new",
      VoskSpkRecognizerPtr,
      [VoskSpkModelPtr, "float"]
    );
    vosk_spk_recognizer_free = lib.func(
      "vosk_spk_recognizer_free",
      "void",
      [VoskSpkRecognizerPtr]
    );
    vosk_spk_recognizer_accept_waveform = lib.func(
      "vosk_spk_recognizer_accept_waveform",
      "bool",
      [VoskSpkRecognizerPtr, koffi.pointer("void"), "int"]
    );
    vosk_recognizer_result = lib.func(
      "vosk_recognizer_result",
      "string",
      [koffi.pointer("void")]
    );

    console.log("[vosk-spk] bindings initialized");
  } catch (err) {
    console.error("[vosk-spk] Failed to initialize bindings:", err);
  }
}

export function isSpkSupported(): boolean {
  init();
  return isVoskSupported && lib !== null;
}

export function spkModelNew(modelPath: string): unknown {
  init();
  const ptr = vosk_spk_model_new(modelPath);
  if (!ptr) throw new Error("Failed to create Vosk SPK model");
  return ptr;
}

export function spkModelFree(modelPtr: unknown): void {
  vosk_spk_model_free(modelPtr);
}

export function spkRecognizerNew(modelPtr: unknown, sampleRate: number): unknown {
  init();
  const ptr = vosk_spk_recognizer_new(modelPtr, sampleRate);
  if (!ptr) throw new Error("Failed to create Vosk SPK recognizer");
  return ptr;
}

export function spkRecognizerFree(recPtr: unknown): void {
  vosk_spk_recognizer_free(recPtr);
}

export function spkAcceptWaveform(recPtr: unknown, data: Buffer): boolean {
  return vosk_spk_recognizer_accept_waveform(recPtr, data, data.length);
}

export function spkGetResult(recPtr: unknown): string {
  return vosk_recognizer_result(recPtr) ?? "";
}
