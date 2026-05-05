from app.domain.transcription.value_objects import AckMessage


class ProcessIncomingAudioChunk:
    def execute(self) -> AckMessage:
        return AckMessage()
