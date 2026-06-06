import logging
from pathlib import Path
from threading import Lock

from fastapi import HTTPException

from app.translator.nllb_ct2 import NLLBCT2Translator

logger = logging.getLogger("translate.manager")

SUPPORTED_PAIRS = [
    "en-vi",
    "vi-en",
    "en-zh",
    "zh-en",
    "vi-zh",
    "zh-vi",
]


class TranslationModelManager:
    def __init__(
        self,
        base_model_dir: str,
        device: str,
        compute_type: str,
        inter_threads: int,
        intra_threads: int,
        fast_beam_size: int = 1,
        quality_beam_size: int = 4,
    ):
        self.base_model_dir = Path(base_model_dir)
        self.device = device
        self.compute_type = compute_type
        self.inter_threads = inter_threads
        self.intra_threads = intra_threads
        self.fast_beam_size = fast_beam_size
        self.quality_beam_size = quality_beam_size
        self.translators: dict[str, NLLBCT2Translator] = {}
        self._load_lock = Lock()

    @staticmethod
    def make_pair(source_lang: str, target_lang: str) -> str:
        return f"{source_lang}-{target_lang}"

    def _validate_pair(self, source_lang: str, target_lang: str) -> None:
        pair = self.make_pair(source_lang, target_lang)
        if pair not in SUPPORTED_PAIRS:
            raise HTTPException(
                status_code=400,
                detail=(
                    f"Unsupported language pair: {pair}. "
                    f"Supported pairs: {', '.join(SUPPORTED_PAIRS)}"
                ),
            )

    def get_translator(
        self, source_lang: str, target_lang: str, mode: str = "fast"
    ) -> NLLBCT2Translator:
        self._validate_pair(source_lang, target_lang)

        cache_key = f"nllb-{mode}"
        cached = self.translators.get(cache_key)
        if cached is not None:
            return cached

        if not self.base_model_dir.is_dir():
            raise HTTPException(
                status_code=400,
                detail=(
                    f"Model directory not found: {self.base_model_dir}. "
                    "Please convert the NLLB-200 model first."
                ),
            )

        beam_size = self.fast_beam_size if mode == "fast" else self.quality_beam_size

        with self._load_lock:
            cached = self.translators.get(cache_key)
            if cached is not None:
                return cached

            logger.info(
                "loading NLLB-200 model mode=%s beam_size=%d", mode, beam_size
            )
            translator = NLLBCT2Translator(
                model_path=str(self.base_model_dir),
                device=self.device,
                compute_type=self.compute_type,
                inter_threads=self.inter_threads,
                intra_threads=self.intra_threads,
                beam_size=beam_size,
            )
            self.translators[cache_key] = translator
            return translator

    def list_available_pairs(self) -> list[str]:
        return sorted(SUPPORTED_PAIRS)

    @property
    def loaded_models(self) -> list[str]:
        return list(self.translators.keys())
