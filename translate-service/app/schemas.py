from pydantic import BaseModel, Field


class HealthResponse(BaseModel):
    status: str = "ok"
    device: str
    compute_type: str
    loaded_models: list[str] = Field(default_factory=list)


class ModelsResponse(BaseModel):
    available_pairs: list[str] = Field(default_factory=list)
    base_model_dir: str
    device: str
    compute_type: str


class TranslateRequest(BaseModel):
    text: str = Field(min_length=1)
    source_lang: str = Field(default="en", min_length=2)
    target_lang: str = Field(default="vi", min_length=2)


class TranslateResponse(BaseModel):
    source_text: str
    translated_text: str
    source_lang: str
    target_lang: str
    latency_ms: float
    model: str


class BatchTranslateRequest(BaseModel):
    texts: list[str] = Field(min_length=1)
    source_lang: str = Field(default="en", min_length=2)
    target_lang: str = Field(default="vi", min_length=2)


class BatchTranslationItem(BaseModel):
    source_text: str
    translated_text: str


class BatchTranslateResponse(BaseModel):
    translations: list[BatchTranslationItem]
    source_lang: str
    target_lang: str
    latency_ms: float
    model: str


class RealtimeTranslateRequest(BaseModel):
    text: str = Field(min_length=1)
    source_lang: str = Field(default="en", min_length=2)
    target_lang: str = Field(default="vi", min_length=2)
    is_final: bool = False
    session_id: str = Field(default="default", min_length=1)


class RealtimeTranslateResponse(BaseModel):
    translated_text: str
    should_display: bool
    is_final: bool
    latency_ms: float
