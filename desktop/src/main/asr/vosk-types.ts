export interface VoskConfig {
  modelPath: string;
  language: string;
}

export interface VoskResult {
  text: string;
  result?: Array<{ conf: number; end: number; start: number; word: string }>;
}

export interface VoskPartialResult {
  partial: string;
}
