from pydantic import BaseModel, Field


class HealthResponse(BaseModel):
    status: str = "ok"
    device: str
    compute_type: str
    loaded_models: list[str] = Field(default_factory=list)
    available_pairs: list[str] = Field(default_factory=list)
    model: str = "nllb-200-distilled-600M"
    models_available: bool = False

    model_config = {
        "json_schema_extra": {
            "example": {
                "status": "ok",
                "device": "cpu",
                "compute_type": "int8",
                "loaded_models": ["nllb-fast"],
                "available_pairs": ["en-vi", "vi-en"],
                "model": "nllb-200-distilled-600M",
                "models_available": True,
            }
        }
    }


class DownloadStatusResponse(BaseModel):
    status: str  # idle | downloading | completed | error
    percent: int = 0
    error: str | None = None


class TranslateFastRequest(BaseModel):
    text: str = Field(min_length=1)
    source_lang: str = Field(default="en", min_length=2)
    target_lang: str = Field(default="vi", min_length=2)
    session_id: str = Field(default="default", min_length=1)
    is_final: bool = False

    model_config = {
        "json_schema_extra": {
            "example": {
                "text": "hello everyone welcome to",
                "source_lang": "en",
                "target_lang": "vi",
                "session_id": "abc123",
                "is_final": False,
            }
        }
    }


class TranslateFastResponse(BaseModel):
    translated_text: str
    should_display: bool
    latency_ms: float

    model_config = {
        "json_schema_extra": {
            "example": {
                "translated_text": "xin chào mọi người chào mừng đến",
                "should_display": True,
                "latency_ms": 18.1,
            }
        }
    }


class TranslateRequest(BaseModel):
    text: str | list[str] = Field(min_length=1)
    source_lang: str = Field(default="en", min_length=2)
    target_lang: str = Field(default="vi", min_length=2)

    model_config = {
        "json_schema_extra": {
            "example": {
                "text": "Hello everyone, welcome to today's meeting.",
                "source_lang": "en",
                "target_lang": "vi",
            }
        }
    }


class TranslateResponse(BaseModel):
    translated_text: str | list[str]
    latency_ms: float

    model_config = {
        "json_schema_extra": {
            "example": {
                "translated_text": "Xin chào mọi người, chào mừng đến với cuộc họp hôm nay.",
                "latency_ms": 250.0,
            }
        }
    }
