from functools import lru_cache
from pathlib import Path

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=True,
        extra="ignore",
    )

    model_base_dir: str = Field(default="models/ct2", alias="MODEL_BASE_DIR")
    translation_device: str = Field(default="cpu", alias="TRANSLATION_DEVICE")
    translation_compute_type: str = Field(
        default="int8",
        alias="TRANSLATION_COMPUTE_TYPE",
    )
    inter_threads: int = Field(default=1, alias="INTER_THREADS")
    intra_threads: int = Field(default=4, alias="INTRA_THREADS")
    default_source_lang: str = Field(default="en", alias="DEFAULT_SOURCE_LANG")
    default_target_lang: str = Field(default="vi", alias="DEFAULT_TARGET_LANG")
    min_realtime_chars: int = Field(default=8, alias="MIN_REALTIME_CHARS")
    max_text_chars: int = Field(default=1000, alias="MAX_TEXT_CHARS")
    session_cache_ttl_sec: int = Field(default=300, alias="SESSION_CACHE_TTL_SEC")
    log_level: str = Field(default="INFO", alias="LOG_LEVEL")

    @property
    def resolved_model_base_dir(self) -> Path:
        return Path(self.model_base_dir)


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()
