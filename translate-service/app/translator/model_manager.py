from pathlib import Path
from threading import Lock

from fastapi import HTTPException

from app.translator.marian_ct2 import MarianCT2Translator


class TranslationModelManager:
    def __init__(
        self,
        base_model_dir: str,
        device: str,
        compute_type: str,
        inter_threads: int,
        intra_threads: int,
    ):
        self.base_model_dir = Path(base_model_dir)
        self.device = device
        self.compute_type = compute_type
        self.inter_threads = inter_threads
        self.intra_threads = intra_threads
        self.cache: dict[str, MarianCT2Translator] = {}
        self._load_lock = Lock()

    def get_pair(self, source_lang: str, target_lang: str) -> str:
        return f"{source_lang}-{target_lang}"

    def get_translator(self, source_lang: str, target_lang: str) -> MarianCT2Translator:
        pair = self.get_pair(source_lang, target_lang)
        cached = self.cache.get(pair)
        if cached is not None:
            return cached

        model_path = self.base_model_dir / pair
        if not model_path.is_dir():
            raise HTTPException(
                status_code=400,
                detail=(
                    f"Translation model for pair {pair} not found. "
                    "Please convert the model first."
                ),
            )

        with self._load_lock:
            cached = self.cache.get(pair)
            if cached is not None:
                return cached

            translator = MarianCT2Translator(
                model_path=str(model_path),
                device=self.device,
                compute_type=self.compute_type,
                inter_threads=self.inter_threads,
                intra_threads=self.intra_threads,
            )
            self.cache[pair] = translator
            return translator

    def list_available_pairs(self) -> list[str]:
        if not self.base_model_dir.exists():
            return []

        pairs: list[str] = []
        for path in self.base_model_dir.iterdir():
            if path.is_dir() and not path.name.startswith("."):
                pairs.append(path.name)

        return sorted(pairs)
