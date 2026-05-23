from pydantic import BaseModel, Field


class ErrorResponse(BaseModel):
    detail: str

    model_config = {
        "json_schema_extra": {
            "example": {
                "detail": "Translation model for pair en-vi not found. Please convert the model first."
            }
        }
    }


class ValidationErrorItem(BaseModel):
    loc: list[str | int]
    msg: str
    type: str


class ValidationErrorResponse(BaseModel):
    detail: list[ValidationErrorItem]

    model_config = {
        "json_schema_extra": {
            "example": {
                "detail": [
                    {
                        "loc": ["body", "text"],
                        "msg": "String should have at least 1 character",
                        "type": "string_too_short",
                    }
                ]
            }
        }
    }


class HealthResponse(BaseModel):
    status: str = "ok"
    device: str
    compute_type: str
    loaded_models: list[str] = Field(default_factory=list)

    model_config = {
        "json_schema_extra": {
            "example": {
                "status": "ok",
                "device": "cpu",
                "compute_type": "int8",
                "loaded_models": ["en-vi", "vi-en"],
            }
        }
    }


class ModelsResponse(BaseModel):
    available_pairs: list[str] = Field(default_factory=list)
    base_model_dir: str
    device: str
    compute_type: str

    model_config = {
        "json_schema_extra": {
            "example": {
                "available_pairs": ["en-vi", "vi-en"],
                "base_model_dir": "models/ct2",
                "device": "cpu",
                "compute_type": "int8",
            }
        }
    }


class TranslateRequest(BaseModel):
    text: str = Field(min_length=1)
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
    source_text: str
    translated_text: str
    source_lang: str
    target_lang: str
    latency_ms: float
    model: str

    model_config = {
        "json_schema_extra": {
            "example": {
                "source_text": "Hello everyone, welcome to today's meeting.",
                "translated_text": "Xin chao moi nguoi, chao mung den voi cuoc hop hom nay.",
                "source_lang": "en",
                "target_lang": "vi",
                "latency_ms": 24.5,
                "model": "en-vi",
            }
        }
    }


class BatchTranslateRequest(BaseModel):
    texts: list[str] = Field(min_length=1)
    source_lang: str = Field(default="en", min_length=2)
    target_lang: str = Field(default="vi", min_length=2)

    model_config = {
        "json_schema_extra": {
            "example": {
                "texts": ["Hello everyone.", "Welcome to today's meeting."],
                "source_lang": "en",
                "target_lang": "vi",
            }
        }
    }


class BatchTranslationItem(BaseModel):
    source_text: str
    translated_text: str


class BatchTranslateResponse(BaseModel):
    translations: list[BatchTranslationItem]
    source_lang: str
    target_lang: str
    latency_ms: float
    model: str

    model_config = {
        "json_schema_extra": {
            "example": {
                "translations": [
                    {
                        "source_text": "Hello everyone.",
                        "translated_text": "Xin chao moi nguoi.",
                    },
                    {
                        "source_text": "Welcome to today's meeting.",
                        "translated_text": "Chao mung den voi cuoc hop hom nay.",
                    },
                ],
                "source_lang": "en",
                "target_lang": "vi",
                "latency_ms": 40.2,
                "model": "en-vi",
            }
        }
    }


class RealtimeTranslateRequest(BaseModel):
    text: str = Field(min_length=1)
    source_lang: str = Field(default="en", min_length=2)
    target_lang: str = Field(default="vi", min_length=2)
    is_final: bool = False
    session_id: str = Field(default="default", min_length=1)
    segment_id: str = Field(default="default", min_length=1)
    sequence_id: int = Field(default=0, ge=0)
    kind: str = Field(default="draft", min_length=1)
    force: bool = False

    model_config = {
        "json_schema_extra": {
            "example": {
                "text": "hello everyone welcome to",
                "source_lang": "en",
                "target_lang": "vi",
                "is_final": False,
                "session_id": "abc123",
                "segment_id": "draft-001",
                "sequence_id": 108,
                "kind": "draft",
                "force": False,
            }
        }
    }


class RealtimeTranslateResponse(BaseModel):
    translated_text: str
    should_display: bool
    is_final: bool
    latency_ms: float
    model: str
    session_id: str
    segment_id: str
    sequence_id: int
    kind: str
    was_skipped: bool
    skip_reason: str | None = None
    normalized_text: str
    cache_hit: bool = False

    model_config = {
        "json_schema_extra": {
            "example": {
                "translated_text": "xin chao moi nguoi chao mung den",
                "should_display": True,
                "is_final": False,
                "latency_ms": 18.1,
                "model": "en-vi",
                "session_id": "abc123",
                "segment_id": "draft-001",
                "sequence_id": 108,
                "kind": "draft",
                "was_skipped": False,
                "skip_reason": None,
                "normalized_text": "hello everyone welcome to",
                "cache_hit": False,
            }
        }
    }


class RealtimeSessionResetRequest(BaseModel):
    session_id: str = Field(min_length=1)

    model_config = {
        "json_schema_extra": {
            "example": {
                "session_id": "abc123",
            }
        }
    }


class RealtimeSessionResetResponse(BaseModel):
    session_id: str
    cleared: bool

    model_config = {
        "json_schema_extra": {
            "example": {
                "session_id": "abc123",
                "cleared": True,
            }
        }
    }
