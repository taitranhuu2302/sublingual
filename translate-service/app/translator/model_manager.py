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
        quality_beam_size: int = 4,
        quality_compute_type: str = "int8_float16",
        quality_model_suffix: str = "-quality",
    ):
        self.base_model_dir = Path(base_model_dir)
        self.device = device
        self.compute_type = compute_type
        self.inter_threads = inter_threads
        self.intra_threads = intra_threads
        self.quality_beam_size = quality_beam_size
        self.quality_compute_type = quality_compute_type
        self.quality_model_suffix = quality_model_suffix
        self.cache: dict[str, MarianCT2Translator] = {}
        self._load_lock = Lock()

    def get_pair(self, source_lang: str, target_lang: str) -> str:
        return f"{source_lang}-{target_lang}"

    def get_translator(
        self,
        source_lang: str,
        target_lang: str,
        quality: bool = False,
    ) -> MarianCT2Translator:
        pair = self.get_pair(source_lang, target_lang)

        if quality:
            cache_key = f"{pair}|quality"
            cached = self.cache.get(cache_key)
            if cached is not None:
                return cached

            quality_pair = f"{pair}{self.quality_model_suffix}"
            quality_path = self.base_model_dir / quality_pair

            if quality_path.is_dir():
                beam_size = self.quality_beam_size
                ct2_compute_type = self.quality_compute_type
                model_path = quality_path
            else:
                beam_size = self.quality_beam_size
                ct2_compute_type = self.compute_type
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
                cached = self.cache.get(cache_key)
                if cached is not None:
                    return cached

                translator = self._try_create_translator(
                    model_path=str(model_path),
                    device=self.device,
                    compute_type=ct2_compute_type,
                    inter_threads=self.inter_threads,
                    intra_threads=self.intra_threads,
                    beam_size=beam_size,
                )
                self.cache[cache_key] = translator
                return translator

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

            translator = self._try_create_translator(
                model_path=str(model_path),
                device=self.device,
                compute_type=self.compute_type,
                inter_threads=self.inter_threads,
                intra_threads=self.intra_threads,
                beam_size=1,
            )
            self.cache[pair] = translator
            return translator

    def _try_create_translator(
        self,
        model_path: str,
        device: str,
        compute_type: str,
        inter_threads: int,
        intra_threads: int,
        beam_size: int,
    ) -> MarianCT2Translator:
        try:
            return MarianCT2Translator(
                model_path=model_path,
                device=device,
                compute_type=compute_type,
                inter_threads=inter_threads,
                intra_threads=intra_threads,
                beam_size=beam_size,
            )
        except ValueError as exc:
            msg = str(exc)
            if "compute type" in msg.lower() and compute_type != "int8":
                logger = __import__("logging").getLogger("translate-service")
                logger.warning(
                    "Compute type '%s' not supported on device '%s', "
                    "falling back to 'int8'. Error: %s",
                    compute_type,
                    device,
                    exc,
                )
                return MarianCT2Translator(
                    model_path=model_path,
                    device=device,
                    compute_type="int8",
                    inter_threads=inter_threads,
                    intra_threads=intra_threads,
                    beam_size=beam_size,
                )
            raise

    def list_available_pairs(self) -> list[str]:
        if not self.base_model_dir.exists():
            return []

        pairs: list[str] = []
        for path in self.base_model_dir.iterdir():
            if path.is_dir() and not path.name.startswith("."):
                name = path.name
                if name.endswith(self.quality_model_suffix):
                    name = name[: -len(self.quality_model_suffix)]
                if name not in pairs:
                    pairs.append(name)

        return sorted(pairs)
