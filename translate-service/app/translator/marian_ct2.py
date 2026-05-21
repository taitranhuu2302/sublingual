from pathlib import Path

import ctranslate2
from transformers import MarianTokenizer

from app.utils.text import normalize_text


class MarianCT2Translator:
    def __init__(
        self,
        model_path: str,
        device: str = "cpu",
        compute_type: str = "int8",
        inter_threads: int = 4,
        intra_threads: int = 4,
    ):
        self.model_path = str(Path(model_path))
        self.device = device
        self.compute_type = compute_type
        self.inter_threads = inter_threads
        self.intra_threads = intra_threads
        self.translator = ctranslate2.Translator(
            self.model_path,
            device=self.device,
            compute_type=self.compute_type,
            inter_threads=self.inter_threads,
            intra_threads=self.intra_threads,
        )
        self.tokenizer = MarianTokenizer.from_pretrained(self.model_path)

    def translate(self, text: str) -> str:
        translations = self.translate_batch([text])
        return translations[0] if translations else ""

    def translate_batch(self, texts: list[str]) -> list[str]:
        normalized_texts = [normalize_text(text) for text in texts]
        translated_texts = ["" for _ in normalized_texts]

        indexed_tokens: list[tuple[int, list[str]]] = []
        for index, text in enumerate(normalized_texts):
            if not text:
                continue

            token_ids = self.tokenizer.encode(text)
            tokens = self.tokenizer.convert_ids_to_tokens(token_ids)
            indexed_tokens.append((index, tokens))

        if not indexed_tokens:
            return translated_texts

        results = self.translator.translate_batch([tokens for _, tokens in indexed_tokens])

        for (index, _), result in zip(indexed_tokens, results):
            output_tokens = result.hypotheses[0] if result.hypotheses else []
            output_ids = self.tokenizer.convert_tokens_to_ids(output_tokens)
            translated_texts[index] = self.tokenizer.decode(
                output_ids,
                skip_special_tokens=True,
            ).strip()

        return translated_texts
