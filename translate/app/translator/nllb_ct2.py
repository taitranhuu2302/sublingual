import logging
from pathlib import Path

import ctranslate2
from transformers import AutoTokenizer

from app.postprocess.glossary import Glossary
from app.postprocess.vi_normalizer import normalize_vietnamese
from app.utils.text import normalize_text

logger = logging.getLogger("translate.nllb")


FLORES_CODE_MAP = {
    "en": "eng_Latn",
    "vi": "vie_Latn",
    "zh": "zho_Hans",
}


class NLLBCT2Translator:
    def __init__(
        self,
        model_path: str,
        device: str = "cpu",
        compute_type: str = "int8",
        inter_threads: int = 4,
        intra_threads: int = 4,
        beam_size: int = 1,
        glossary: Glossary | None = None,
    ):
        self.model_path = str(Path(model_path))
        self.device = device
        self.compute_type = compute_type
        self.beam_size = beam_size
        self.glossary = glossary
        self.translator = ctranslate2.Translator(
            self.model_path,
            device=self.device,
            compute_type=self.compute_type,
            inter_threads=inter_threads,
            intra_threads=intra_threads,
        )
        self.tokenizer = AutoTokenizer.from_pretrained(self.model_path)
        self._target_prefix_cache: dict[str, list[list[str]]] = {}

    def _get_target_prefix(self, target_lang: str) -> list[list[str]]:
        flores_code = FLORES_CODE_MAP.get(target_lang, target_lang)
        cached = self._target_prefix_cache.get(flores_code)
        if cached is not None:
            return cached
        tokens = [[flores_code]]
        self._target_prefix_cache[flores_code] = tokens
        return tokens

    def translate(
        self, text: str, source_lang: str = "en", target_lang: str = "vi"
    ) -> str:
        translations = self.translate_batch([text], source_lang, target_lang)
        return translations[0] if translations else ""

    def translate_batch(
        self,
        texts: list[str],
        source_lang: str = "en",
        target_lang: str = "vi",
    ) -> list[str]:
        normalized_texts = [normalize_text(text) for text in texts]
        translated_texts: list[str] = ["" for _ in normalized_texts]

        indexed_texts = [(i, t) for i, t in enumerate(normalized_texts) if t]

        if not indexed_texts:
            return translated_texts

        flores_src = FLORES_CODE_MAP.get(source_lang, source_lang)

        self.tokenizer.src_lang = flores_src
        encoded = self.tokenizer(
            [t for _, t in indexed_texts],
            add_special_tokens=True,
            return_attention_mask=False,
        )
        batch_tokens = [
            self.tokenizer.convert_ids_to_tokens(ids)
            for ids in encoded.input_ids
        ]

        target_prefix = self._get_target_prefix(target_lang)

        results = self.translator.translate_batch(
            batch_tokens,
            beam_size=self.beam_size,
            target_prefix=target_prefix,
        )

        for (index, _), result in zip(indexed_texts, results):
            output_tokens = result.hypotheses[0] if result.hypotheses else []
            output_ids = self.tokenizer.convert_tokens_to_ids(output_tokens)
            decoded = self.tokenizer.decode(
                output_ids,
                skip_special_tokens=True,
            ).strip()
            decoded = normalize_vietnamese(decoded)
            if self.glossary:
                decoded = self.glossary.apply(decoded)
            translated_texts[index] = decoded

        return translated_texts
