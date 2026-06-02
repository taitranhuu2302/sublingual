import * as spkBindings from "./vosk-spk-bindings";
import { assignSpeaker, type SpeakerIdentity } from "./speaker-cluster";

let spkModel: unknown = null;
let spkRecognizer: unknown = null;
let clusters: SpeakerIdentity[] = [];
let maxSpeakers = 4;

export function startSpk(modelPath: string, _maxSpeakers: number): void {
  if (!spkBindings.isSpkSupported()) {
    console.log("[spk] Not supported on this platform, skipping");
    return;
  }

  maxSpeakers = _maxSpeakers;
  clusters = [];

  try {
    spkModel = spkBindings.spkModelNew(modelPath);
    spkRecognizer = spkBindings.spkRecognizerNew(spkModel, 16000);
    console.log("[spk] Speaker model loaded successfully");
  } catch (err) {
    console.error("[spk] Failed to load speaker model:", err);
    spkModel = null;
    spkRecognizer = null;
  }
}

export function isSpkRunning(): boolean {
  return spkRecognizer !== null;
}

/**
 * Extract speaker embedding from a PCM16 audio buffer.
 * Returns null if SPK is not running or extraction fails.
 */
export function extractSpeakerEmbedding(pcmBuffer: Buffer): Float64Array | null {
  if (!spkRecognizer) return null;

  try {
    spkBindings.spkAcceptWaveform(spkRecognizer, pcmBuffer);
    const raw = spkBindings.spkGetResult(spkRecognizer);
    if (!raw) return null;

    const parsed = JSON.parse(raw);
    if (!parsed || !Array.isArray(parsed.spk)) return null;

    return new Float64Array(parsed.spk);
  } catch (err) {
    console.error("[spk] Failed to extract embedding:", err);
    return null;
  }
}

/**
 * Assign a speaker label for an embedding. Returns identity info for the
 * transcript line, or null if no embedding was provided.
 */
export function classifySpeaker(embedding: Float64Array | null): {
  speakerId: string;
  speakerLabel: string;
  speakerColor: string;
} | null {
  if (!embedding) return null;

  const identity = assignSpeaker(clusters, embedding, maxSpeakers, Date.now());
  return {
    speakerId: identity.id,
    speakerLabel: identity.label,
    speakerColor: identity.color,
  };
}

export function stopSpk(): void {
  if (spkRecognizer) {
    spkBindings.spkRecognizerFree(spkRecognizer);
    spkRecognizer = null;
  }
  if (spkModel) {
    spkBindings.spkModelFree(spkModel);
    spkModel = null;
  }
  clusters = [];
}
