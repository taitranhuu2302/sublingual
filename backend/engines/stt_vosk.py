import json
import os
from dataclasses import dataclass

from vosk import KaldiRecognizer, Model


@dataclass(frozen=True)
class VoskChunkResult:
    partial: str | None = None
    final: str | None = None


class VoskSTTEngine:
    def __init__(self) -> None:
        default_model_path = os.path.join(
            os.path.dirname(os.path.dirname(__file__)),
            "models",
            "vosk-model-small-en-us",
        )
        model_path = os.getenv("VOSK_MODEL_PATH", default_model_path)
        self._model = Model(model_path)
        self._recognizer = KaldiRecognizer(self._model, 16000)

    def transcribe_chunk(self, pcm_chunk: bytes) -> VoskChunkResult:
        if not pcm_chunk:
            return VoskChunkResult()

        accepted = self._recognizer.AcceptWaveform(pcm_chunk)
        if accepted:
            payload = json.loads(self._recognizer.Result())
            final_text = (payload.get("text") or "").strip()
            return VoskChunkResult(final=final_text if final_text else None)

        payload = json.loads(self._recognizer.PartialResult())
        partial_text = (payload.get("partial") or "").strip()
        return VoskChunkResult(partial=partial_text if partial_text else None)

