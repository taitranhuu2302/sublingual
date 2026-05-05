import json
from dataclasses import asdict, is_dataclass
from typing import Any


def to_json(payload: Any) -> str:
    if is_dataclass(payload):
        return json.dumps(asdict(payload))
    return json.dumps(payload)
