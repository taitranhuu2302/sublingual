from app.application.transcription.use_cases import ProcessIncomingAudioChunk


class Container:
    def __init__(self) -> None:
        self.process_incoming_audio_chunk = ProcessIncomingAudioChunk()
