from app.domain.transcription.value_objects import (
    FinalTranscriptionMessage,
    PartialTranscriptionMessage,
)
from engines.stt_vosk import VoskSTTEngine


class ProcessIncomingAudioChunk:
    def __init__(self) -> None:
        self._engine = VoskSTTEngine()

    def execute(
        self, audio_chunk: bytes
    ) -> list[PartialTranscriptionMessage | FinalTranscriptionMessage]:
        result = self._engine.transcribe_chunk(audio_chunk)
        messages: list[PartialTranscriptionMessage | FinalTranscriptionMessage] = []

        if result.partial:
            messages.append(PartialTranscriptionMessage(text=result.partial))
        if result.final:
            messages.append(FinalTranscriptionMessage(text=result.final))
        return messages
