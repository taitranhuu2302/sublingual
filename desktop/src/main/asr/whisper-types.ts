export interface WhisperSegment {
  text: string;
  t0: number; // start ms
  t1: number; // end ms
  isFinal: boolean;
}

export interface WhisperConfig {
  modelPath: string;
  language: string;
  threads?: number;
}
