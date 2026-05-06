from dataclasses import dataclass


@dataclass(frozen=True)
class AckMessage:
    type: str = "ack"


@dataclass(frozen=True)
class PartialTranscriptionMessage:
    type: str = "partial"
    text: str = ""


@dataclass(frozen=True)
class FinalTranscriptionMessage:
    type: str = "final"
    text: str = ""
