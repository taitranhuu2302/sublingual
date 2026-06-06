import json
import logging
import re
from pathlib import Path

logger = logging.getLogger("translate.glossary")


class Glossary:
    def __init__(self, glossary_path: str):
        self._terms: dict[str, str] = {}
        self._load(glossary_path)

    def _load(self, path: str) -> None:
        try:
            with open(path, "r", encoding="utf-8") as f:
                data = json.load(f)
            self._terms = {k.lower(): v for k, v in data.items() if k and v}
            logger.info("loaded %d glossary terms from %s", len(self._terms), path)
        except FileNotFoundError:
            logger.debug("glossary file not found: %s", path)
        except (json.JSONDecodeError, OSError) as exc:
            logger.warning("failed to load glossary from %s: %s", path, exc)

    def lookup(self, term: str) -> str | None:
        return self._terms.get(term.lower())

    def apply(self, text: str) -> str:
        if not self._terms or not text:
            return text
        result = text
        for src, tgt in sorted(self._terms.items(), key=lambda x: -len(x[0])):
            pattern = re.compile(re.escape(src), re.IGNORECASE)
            result = pattern.sub(tgt, result)
        return result
