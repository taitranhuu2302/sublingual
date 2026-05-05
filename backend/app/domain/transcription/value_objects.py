from dataclasses import dataclass


@dataclass(frozen=True)
class AckMessage:
    type: str = "ack"
