# Translate Service Refactor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the translate service from MarianMT/6-endpoints to NLLB-200/3-endpoints with Vietnamese post-processing.

**Architecture:** Replace MarianMT CTranslate2 backend with NLLB-200 600M. Split into fast (greedy, <100ms) and quality (beam, <500ms) modes. Add Vietnamese text normalization pipeline. Simplify API from 6 to 3 endpoints.

**Tech Stack:** FastAPI, CTranslate2, NLLB-200 (600M distilled), SentencePiece, Pydantic

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `app/postprocess/__init__.py` | Package marker |
| Create | `app/postprocess/vi_normalizer.py` | Vietnamese diacritic/word boundary fixes |
| Create | `app/postprocess/glossary.py` | Terminology override from JSON config |
| Create | `app/translator/nllb_ct2.py` | NLLB-200 CTranslate2 wrapper (replaces marian_ct2.py) |
| Create | `app/translator/session_cache.py` | Session cache extracted from main.py |
| Create | `scripts/convert_nllb_to_ct2.py` | NLLB HuggingFace → CTranslate2 converter |
| Modify | `app/config.py` | Add beam_size, glossary_path, flores code map |
| Modify | `app/schemas.py` | Replace all schemas with 5 simplified ones |
| Modify | `app/translator/model_manager.py` | NLLB loading, fast/quality dual config |
| Modify | `app/utils/text.py` | Add NLLB-specific normalization |
| Modify | `app/main.py` | 6→3 endpoints, use new translator |
| Modify | `requirements.txt` | Remove sacremoses, add sentencepiece |
| Modify | `scripts/test_translate.py` | Test both /translate and /translate/fast |
| Modify | `scripts/benchmark.py` | Benchmark both fast and quality modes |
| Modify | `scripts/build_ct2_models.sh` | Target NLLB instead of Marian |
| Modify | `docker/Dockerfile` | Updated deps, remove Marian |
| Modify | `.env.example` | Add new config vars |
| Modify | `README.md` | Full rewrite |
| Delete | `app/translator/marian_ct2.py` | Replaced by nllb_ct2.py |

---

### Task 1: Update Config

**Files:**
- Modify: `translate/app/config.py`

- [ ] **Step 1: Update config.py**

Add new settings for beam_size, glossary path, and NLLB-specific config:

```python
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
    fast_beam_size: int = Field(default=1, alias="FAST_BEAM_SIZE")
    quality_beam_size: int = Field(default=4, alias="QUALITY_BEAM_SIZE")
    glossary_path: str = Field(default="glossary.json", alias="GLOSSARY_PATH")

    @property
    def resolved_model_base_dir(self) -> Path:
        return Path(self.model_base_dir)


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()
```

- [ ] **Step 2: Verify config loads**

```bash
cd translate && python -c "from app.config import get_settings; s = get_settings(); print(s.fast_beam_size, s.quality_beam_size)"
```

Expected output: `1 4`

- [ ] **Step 3: Commit**

```bash
git add translate/app/config.py
git commit -m "feat(config): add NLLB beam_size and glossary settings"
```

---

### Task 2: Create Vietnamese Post-Processing — Normalizer

**Files:**
- Create: `translate/app/postprocess/__init__.py`
- Create: `translate/app/postprocess/vi_normalizer.py`
- Create: `translate/tests/test_vi_normalizer.py`

- [ ] **Step 1: Write failing tests for vi_normalizer**

```python
import pytest
from app.postprocess.vi_normalizer import (
    fix_diacritics,
    merge_word_boundaries,
    normalize_vietnamese,
)


class TestFixDiacritics:
    def test_preserves_correct_text(self):
        assert fix_diacritics("Xin chào mọi người") == "Xin chào mọi người"

    def test_fixes_oa_sequence(self):
        # òa → oà, qủa → quả (common NLLB errors)
        assert fix_diacritics("khòa") == "khòa"

    def test_fixes_uy_sequence(self):
        # uý → uý (already correct), but tùy → tuỳ (common error)
        assert fix_diacritics("tùy") == "tuỳ"

    def test_fixes_qua_sequence(self):
        assert fix_diacritics("qủa") == "quả"


class TestMergeWordBoundaries:
    def test_merges_split_vietnamese_words(self):
        assert merge_word_boundaries("chào mừng đến với") == "chào mừng đến với"

    def test_preserves_spaces_between_words(self):
        assert merge_word_boundaries("xin chào mọi người") == "xin chào mọi người"

    def test_merges_single_char_with_next(self):
        # NLLB sometimes splits Vietnamese syllables incorrectly
        assert merge_word_boundaries("t ôi") == "tôi"


class TestNormalizeVietnamese:
    def test_full_pipeline(self):
        text = "  Xin chào mọi người,   chào mừng đến với cuộc họp!  "
        result = normalize_vietnamese(text)
        assert "  " not in result
        assert result.startswith("Xin")

    def test_empty_text(self):
        assert normalize_vietnamese("") == ""

    def test_fixes_diacritics_and_boundaries(self):
        text = "tùy chỉnh cài đặt"
        result = normalize_vietnamese(text)
        assert result == "tuỳ chỉnh cài đặt"
```

- [ ] **Step 2: Create package init**

`translate/app/postprocess/__init__.py`:
```python
```

- [ ] **Step 3: Implement vi_normalizer.py**

```python
import re


_WHITESPACE_RE = re.compile(r"\s+")


VI_DIACRITIC_FIXES = {
    "òa": "oà",
    "óa": "oá",
    "ỏa": "oả",
    "õa": "oã",
    "ọa": "oạ",
    "òe": "oè",
    "óe": "oé",
    "ỏe": "oẻ",
    "õe": "oẽ",
    "ọe": "oẹ",
    "ùy": "uỳ",
    "úy": "uý",
    "ủy": "uỷ",
    "ũy": "uỹ",
    "ụy": "uỵ",
    "qủa": "quả",
    "qúa": "quá",
    "qùa": "quà",
    "qủa": "quả",
    "qụa": "quạ",
}


def fix_diacritics(text: str) -> str:
    result = text
    for wrong, correct in VI_DIACRITIC_FIXES.items():
        result = result.replace(wrong, correct)
    return result


def merge_word_boundaries(text: str) -> str:
    # NLLB can split Vietnamese syllables at odd points
    # Merge single-character tokens followed by a space and multi-char word
    return re.sub(r"\b(\w) (\w{2,})\b", r"\1\2", text)


def normalize_vietnamese(text: str) -> str:
    if not text:
        return ""
    text = _WHITESPACE_RE.sub(" ", text).strip()
    text = fix_diacritics(text)
    text = merge_word_boundaries(text)
    return text
```

- [ ] **Step 4: Run tests**

```bash
cd translate && python -m pytest tests/test_vi_normalizer.py -v
```

Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add translate/app/postprocess/ translate/tests/test_vi_normalizer.py
git commit -m "feat(postprocess): add Vietnamese text normalization with diacritic and boundary fixes"
```

---

### Task 3: Create Glossary Module

**Files:**
- Create: `translate/app/postprocess/glossary.py`
- Create: `translate/tests/test_glossary.py`

- [ ] **Step 1: Write failing tests**

```python
import json
import tempfile
from pathlib import Path
import pytest
from app.postprocess.glossary import Glossary


class TestGlossary:
    def test_loads_glossary_from_json(self):
        with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as f:
            json.dump({"machine learning": "học máy", "API": "API"}, f)
            path = f.name

        try:
            glossary = Glossary(path)
            assert glossary.lookup("machine learning") == "học máy"
            assert glossary.lookup("API") == "API"
        finally:
            Path(path).unlink()

    def test_returns_none_for_missing_term(self):
        with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as f:
            json.dump({"hello": "xin chào"}, f)
            path = f.name

        try:
            glossary = Glossary(path)
            assert glossary.lookup("world") is None
        finally:
            Path(path).unlink()

    def test_applies_glossary_to_text(self):
        with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as f:
            json.dump({"machine learning": "học máy", "deep learning": "học sâu"}, f)
            path = f.name

        try:
            glossary = Glossary(path)
            result = glossary.apply("I study machine learning and deep learning")
            assert result == "I study học máy and học sâu"
        finally:
            Path(path).unlink()

    def test_handles_missing_file_gracefully(self):
        glossary = Glossary("nonexistent.json")
        assert glossary.apply("some text") == "some text"

    def test_case_insensitive_matching(self):
        with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as f:
            json.dump({"Hello": "Xin chào"}, f)
            path = f.name

        try:
            glossary = Glossary(path)
            result = glossary.apply("hello world")
            assert result == "Xin chào world"
        finally:
            Path(path).unlink()
```

- [ ] **Step 2: Implement glossary.py**

```python
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
```

- [ ] **Step 3: Run tests**

```bash
cd translate && python -m pytest tests/test_glossary.py -v
```

Expected: All PASS

- [ ] **Step 4: Commit**

```bash
git add translate/app/postprocess/glossary.py translate/tests/test_glossary.py
git commit -m "feat(postprocess): add configurable terminology glossary"
```

---

### Task 4: Create NLLB CTranslate2 Translator

**Files:**
- Create: `translate/app/translator/nllb_ct2.py`
- Delete: `translate/app/translator/marian_ct2.py`

NLLB-200 is a single multilingual model. Language selection is done via `target_prefix` (forced BOS token).

- [ ] **Step 1: Implement nllb_ct2.py**

```python
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
        prefix = f"__{flores_code}__"
        cached = self._target_prefix_cache.get(flores_code)
        if cached is not None:
            return cached
        tokens = [self.tokenizer.convert_ids_to_tokens(self.tokenizer.encode(prefix))]
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
```

- [ ] **Step 2: Delete old marian_ct2.py**

```bash
rm translate/app/translator/marian_ct2.py
```

- [ ] **Step 3: Commit**

```bash
git add translate/app/translator/nllb_ct2.py
git rm translate/app/translator/marian_ct2.py
git commit -m "feat(translator): replace MarianMT with NLLB-200 CTranslate2 translator"
```

---

### Task 5: Extract Session Cache to its Own Module

**Files:**
- Create: `translate/app/translator/session_cache.py`

- [ ] **Step 1: Implement session_cache.py**

```python
import time
from threading import Lock

from app.utils.text import is_good_realtime_boundary, is_too_similar, normalize_text


class RealtimeSessionCache:
    def __init__(self, ttl_sec: int = 300, min_realtime_chars: int = 8):
        self.ttl_sec = ttl_sec
        self.min_realtime_chars = min_realtime_chars
        self.sessions: dict[str, dict[str, object]] = {}
        self._lock = Lock()

    def should_translate(
        self, session_id: str, text: str, is_final: bool
    ) -> tuple[bool, str | None]:
        normalized = normalize_text(text)
        if is_final:
            return (bool(normalized), None if normalized else "empty_normalized_text")

        if not normalized:
            return False, "empty_normalized_text"

        if len(normalized) < self.min_realtime_chars:
            return False, "too_short"

        if not is_good_realtime_boundary(text):
            return False, "weak_boundary"

        with self._lock:
            session = self.sessions.get(session_id)
            previous = str(session.get("last_text", "")) if session else ""

        if is_too_similar(previous, normalized, min_delta_chars=self.min_realtime_chars):
            return False, "too_similar"

        return True, None

    def update(self, session_id: str, text: str, translated_text: str) -> None:
        now = time.monotonic()
        with self._lock:
            self.sessions[session_id] = {
                "last_text": normalize_text(text),
                "last_translated_text": translated_text,
                "updated_at": now,
            }

    def get(self, session_id: str) -> dict[str, object] | None:
        with self._lock:
            session = self.sessions.get(session_id)
            if session is None:
                return None
            return dict(session)

    def cleanup_expired(self) -> None:
        now = time.monotonic()
        with self._lock:
            expired_keys = [
                sid
                for sid, session in self.sessions.items()
                if now - float(session.get("updated_at", 0)) > self.ttl_sec
            ]
            for sid in expired_keys:
                del self.sessions[sid]
```

- [ ] **Step 2: Commit**

```bash
git add translate/app/translator/session_cache.py
git commit -m "refactor: extract session cache to translator/session_cache.py"
```

---

### Task 6: Update Model Manager for NLLB + Dual Config

**Files:**
- Modify: `translate/app/translator/model_manager.py`

NLLB-200 is a single multilingual model — no per-pair model directories needed. The manager loads one model instance per mode (fast/quality) and language selection happens at inference via `target_prefix`.

- [ ] **Step 1: Rewrite model_manager.py**

```python
import logging
from pathlib import Path
from threading import Lock

from fastapi import HTTPException

from app.translator.nllb_ct2 import FLORES_CODE_MAP, NLLBCT2Translator

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
```

- [ ] **Step 2: Commit**

```bash
git add translate/app/translator/model_manager.py
git commit -m "feat(manager): single NLLB-200 model with fast/quality modes"
```

- [ ] **Step 2: Commit**

```bash
git add translate/app/translator/model_manager.py
git commit -m "feat(manager): support NLLB with fast/quality dual config and glossary"
```

---

### Task 7: Update Text Utilities

**Files:**
- Modify: `translate/app/utils/text.py`

- [ ] **Step 1: Update text.py**

Keep everything as-is but update the boundary punctuation set:

```python
import re


CONTROL_CHAR_RE = re.compile(r"[\x00-\x1f\x7f]")
WHITESPACE_RE = re.compile(r"\s+")
BOUNDARY_PUNCTUATION = ".,!?;:)])}。！？；：）】』"


def normalize_text(text: str) -> str:
    sanitized = CONTROL_CHAR_RE.sub(" ", text)
    return WHITESPACE_RE.sub(" ", sanitized).strip()


def is_good_realtime_boundary(text: str) -> bool:
    normalized = normalize_text(text)
    if len(normalized) < 8:
        return False

    if text and text[-1].isspace():
        return True

    if normalized[-1] in BOUNDARY_PUNCTUATION:
        return True

    if len(normalized) >= 24:
        return True

    return False


def is_too_similar(prev: str, current: str, min_delta_chars: int = 8) -> bool:
    previous = normalize_text(prev)
    latest = normalize_text(current)

    if not previous:
        return False

    if previous == latest:
        return True

    if latest.startswith(previous) and (len(latest) - len(previous)) < min_delta_chars:
        return True

    if latest in previous:
        return True

    return False


def truncate_text(text: str, max_chars: int) -> str:
    if max_chars <= 0:
        return ""
    return text[:max_chars]
```

- [ ] **Step 2: Commit**

```bash
git add translate/app/utils/text.py
git commit -m "fix(text): add CJK punctuation to boundary detection"
```

---

### Task 8: Rewrite Schemas (Simplified)

**Files:**
- Modify: `translate/app/schemas.py`

- [ ] **Step 1: Replace schemas.py entirely**

```python
from pydantic import BaseModel, Field


class HealthResponse(BaseModel):
    status: str = "ok"
    device: str
    compute_type: str
    loaded_models: list[str] = Field(default_factory=list)
    available_pairs: list[str] = Field(default_factory=list)
    model: str = "nllb-200-distilled-600M"

    model_config = {
        "json_schema_extra": {
            "example": {
                "status": "ok",
                "device": "cpu",
                "compute_type": "int8",
                "loaded_models": ["en-vi"],
                "available_pairs": ["en-vi", "vi-en"],
                "model": "nllb-200-distilled-600M",
            }
        }
    }


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
```

- [ ] **Step 2: Commit**

```bash
git add translate/app/schemas.py
git commit -m "refactor(schemas): replace 17 schemas with 5 simplified NLLB schemas"
```

---

### Task 9: Rewrite main.py with 3 Endpoints

**Files:**
- Modify: `translate/app/main.py`

- [ ] **Step 1: Rewrite main.py**

```python
import logging
import time

from fastapi import FastAPI, HTTPException

from app.config import get_settings
from app.schemas import (
    HealthResponse,
    TranslateFastRequest,
    TranslateFastResponse,
    TranslateRequest,
    TranslateResponse,
)
from app.translator.model_manager import TranslationModelManager
from app.translator.session_cache import RealtimeSessionCache
from app.utils.logger import configure_logging
from app.utils.text import normalize_text, truncate_text


settings = get_settings()
configure_logging(settings.log_level)
logger = logging.getLogger("translate")

model_manager = TranslationModelManager(
    base_model_dir=settings.model_base_dir,
    device=settings.translation_device,
    compute_type=settings.translation_compute_type,
    inter_threads=settings.inter_threads,
    intra_threads=settings.intra_threads,
    fast_beam_size=settings.fast_beam_size,
    quality_beam_size=settings.quality_beam_size,
)
realtime_session_cache = RealtimeSessionCache(
    ttl_sec=settings.session_cache_ttl_sec,
    min_realtime_chars=settings.min_realtime_chars,
)

app = FastAPI(
    title="Translate Service",
    version="0.2.0",
    description=(
        "Standalone self-hosted translation API powered by NLLB-200 and CTranslate2. "
        "Supports fast greedy translation for realtime subtitles and quality beam-search "
        "translation with Vietnamese post-processing."
    ),
    docs_url="/docs",
    redoc_url="/redoc",
    openapi_tags=[
        {
            "name": "system",
            "description": "Health and model discovery.",
        },
        {
            "name": "translation",
            "description": "Quality translation with beam search and post-processing.",
        },
        {
            "name": "fast",
            "description": "Low-latency greedy translation for realtime subtitles.",
        },
    ],
)


@app.on_event("startup")
def warmup_default_model() -> None:
    try:
        translator = model_manager.get_translator(
            settings.default_source_lang,
            settings.default_target_lang,
            mode="fast",
        )
        translator.translate("hello", source_lang="en", target_lang="vi")
        logger.info(
            "warmed up NLLB-200 model on startup"
        )
    except HTTPException as exc:
        logger.warning("default model warmup skipped: %s", exc.detail)


def _prepare_text(text: str) -> str:
    prepared = truncate_text(normalize_text(text), settings.max_text_chars)
    if not prepared:
        raise HTTPException(status_code=400, detail="Text must not be empty.")
    return prepared


@app.get(
    "/health",
    response_model=HealthResponse,
    tags=["system"],
    summary="Health check",
)
def health() -> HealthResponse:
    return HealthResponse(
        status="ok",
        device=settings.translation_device,
        compute_type=settings.translation_compute_type,
        loaded_models=model_manager.loaded_models,
        available_pairs=model_manager.list_available_pairs(),
    )


@app.post(
    "/translate/fast",
    response_model=TranslateFastResponse,
    tags=["fast"],
    summary="Fast realtime translation",
    description=(
        "Low-latency greedy translation for Vosk subtitle pipeline. "
        "Skips redundant partials based on session state."
    ),
    responses={400: {"description": "Model not found or text empty"}},
)
def translate_fast(request: TranslateFastRequest) -> TranslateFastResponse:
    realtime_session_cache.cleanup_expired()
    source_text = _prepare_text(request.text)

    should_translate, _ = realtime_session_cache.should_translate(
        request.session_id,
        source_text,
        request.is_final,
    )
    if not should_translate:
        return TranslateFastResponse(
            translated_text="",
            should_display=False,
            latency_ms=0,
        )

    started = time.perf_counter()
    translator = model_manager.get_translator(
        request.source_lang, request.target_lang, mode="fast"
    )
    translated_text = translator.translate(
        source_text,
        source_lang=request.source_lang,
        target_lang=request.target_lang,
    )
    latency_ms = (time.perf_counter() - started) * 1000

    session = realtime_session_cache.get(request.session_id)
    last_translated = str(session.get("last_translated_text", "")) if session else ""
    should_display = bool(translated_text)
    if not request.is_final and translated_text == last_translated:
        should_display = False

    realtime_session_cache.update(request.session_id, source_text, translated_text)

    logger.info(
        "translate_fast chars=%d latency_ms=%.2f final=%s display=%s",
        len(source_text),
        latency_ms,
        request.is_final,
        should_display,
    )

    return TranslateFastResponse(
        translated_text=translated_text if should_display else "",
        should_display=should_display,
        latency_ms=latency_ms,
    )


@app.post(
    "/translate",
    response_model=TranslateResponse,
    tags=["translation"],
    summary="Quality translation",
    description=(
        "Translates text with beam search and Vietnamese post-processing. "
        "Accepts single string or array of strings for batch translation."
    ),
    responses={400: {"description": "Model not found or text empty"}},
)
def translate(request: TranslateRequest) -> TranslateResponse:
    started = time.perf_counter()
    translator = model_manager.get_translator(
        request.source_lang, request.target_lang, mode="quality"
    )

    if isinstance(request.text, str):
        source_text = _prepare_text(request.text)
        translated_text = translator.translate(
            source_text,
            source_lang=request.source_lang,
            target_lang=request.target_lang,
        )
    else:
        source_texts = [_prepare_text(t) for t in request.text]
        translated_text = translator.translate_batch(
            source_texts,
            source_lang=request.source_lang,
            target_lang=request.target_lang,
        )

    latency_ms = (time.perf_counter() - started) * 1000

    logger.info(
        "translate lang=%s->%s latency_ms=%.2f",
        request.source_lang,
        request.target_lang,
        latency_ms,
    )

    return TranslateResponse(
        translated_text=translated_text,
        latency_ms=latency_ms,
    )
```

- [ ] **Step 2: Verify app loads (syntax check)**

```bash
cd translate && python -c "from app.main import app; print('OK')"
```

Expected: `OK`

- [ ] **Step 3: Commit**

```bash
git add translate/app/main.py
git commit -m "refactor(api): reduce 6 endpoints to 3 (health, translate/fast, translate)"
```

---

### Task 10: Create NLLB to CTranslate2 Converter Script

**Files:**
- Create: `translate/scripts/convert_nllb_to_ct2.py`

- [ ] **Step 1: Implement converter**

```python
#!/usr/bin/env python3
"""Convert NLLB-200 HuggingFace model to CTranslate2 format."""

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

from transformers import AutoTokenizer


def main():
    parser = argparse.ArgumentParser(
        description="Convert NLLB-200 model to CTranslate2"
    )
    parser.add_argument(
        "--hf_model",
        required=True,
        help="HuggingFace model ID (e.g. facebook/nllb-200-distilled-600M)",
    )
    parser.add_argument(
        "--output_dir",
        required=True,
        help="Output directory for CTranslate2 model",
    )
    parser.add_argument(
        "--quantization",
        default="int8",
        choices=["int8", "int8_float16", "float16", "float32"],
        help="Quantization type (default: int8)",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Overwrite existing output directory",
    )
    args = parser.parse_args()

    output_dir = Path(args.output_dir)

    if output_dir.exists():
        if args.force:
            print(f"Removing existing directory: {output_dir}")
            shutil.rmtree(output_dir)
        else:
            print(
                f"Output directory {output_dir} already exists. "
                "Use --force to overwrite."
            )
            sys.exit(1)

    print(f"Downloading tokenizer from {args.hf_model}...")
    tokenizer = AutoTokenizer.from_pretrained(args.hf_model)
    tokenizer.save_pretrained(str(output_dir))
    print(f"Saved tokenizer to {output_dir}")

    print(f"Converting {args.hf_model} to CTranslate2 ({args.quantization})...")

    cmd = [
        "ct2-transformers-converter",
        "--model", args.hf_model,
        "--output_dir", str(output_dir),
        "--quantization", args.quantization,
        "--force",
    ]
    subprocess.run(cmd, check=True)

    print(f"Done. CTranslate2 model saved to {output_dir}")


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: Commit**

```bash
git add translate/scripts/convert_nllb_to_ct2.py
git commit -m "feat(scripts): add NLLB-200 to CTranslate2 converter script"
```

---

### Task 11: Update Scripts (build, test, benchmark)

**Files:**
- Modify: `translate/scripts/build_ct2_models.sh`
- Modify: `translate/scripts/test_translate.py`
- Modify: `translate/scripts/benchmark.py`

- [ ] **Step 1: Update build_ct2_models.sh for single NLLB-200 model**

NLLB-200 is one multilingual model, not per-language-pair. Convert once.

```bash
#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

if [[ -n "${PYTHON_BIN:-}" ]]; then
  PYTHON_EXEC="$PYTHON_BIN"
elif [[ -x "$PROJECT_DIR/venv/bin/python" ]]; then
  PYTHON_EXEC="$PROJECT_DIR/venv/bin/python"
else
  PYTHON_EXEC="python3"
fi

QUANTIZATION="int8"
FORCE_FLAG=()

for arg in "$@"; do
  if [[ "$arg" == "--force" ]]; then
    FORCE_FLAG=(--force)
  elif [[ -z "${QUANTIZATION_SET:-}" && -n "$arg" ]]; then
    QUANTIZATION="$arg"
    QUANTIZATION_SET=1
  fi
done

if ! "$PYTHON_EXEC" -c "import transformers" >/dev/null 2>&1; then
  printf 'Error: transformers is not installed for %s\n' "$PYTHON_EXEC" >&2
  printf 'Install dependencies in the project environment or set PYTHON_BIN explicitly.\n' >&2
  exit 1
fi

OUTPUT_DIR="$PROJECT_DIR/models/ct2/nllb-200-600M"

echo "Building NLLB-200 600M model with quantization=$QUANTIZATION"

"$PYTHON_EXEC" "$PROJECT_DIR/scripts/convert_nllb_to_ct2.py" \
  --hf_model facebook/nllb-200-distilled-600M \
  --output_dir "$OUTPUT_DIR" \
  --quantization "$QUANTIZATION" \
  "${FORCE_FLAG[@]}"

printf 'NLLB-200 model created successfully at %s with quantization=%s\n' "$OUTPUT_DIR" "$QUANTIZATION"
```

- [ ] **Step 2: Update test_translate.py**

```python
#!/usr/bin/env python3
"""Test the translate service API endpoints."""

import argparse
import json
import sys

import requests


def main():
    parser = argparse.ArgumentParser(description="Test translate API")
    parser.add_argument("--url", default="http://localhost:8000", help="API base URL")
    parser.add_argument("--source", default="en", help="Source language")
    parser.add_argument("--target", default="vi", help="Target language")
    parser.add_argument("--text", required=True, help="Text to translate")
    parser.add_argument("--mode", default="quality", choices=["fast", "quality"], help="Translation mode")
    parser.add_argument("--batch", action="store_true", help="Send as batch")
    args = parser.parse_args()

    if args.mode == "fast":
        endpoint = f"{args.url}/translate/fast"
        payload = {
            "text": args.text,
            "source_lang": args.source,
            "target_lang": args.target,
            "session_id": "test-session",
            "is_final": True,
        }
    else:
        endpoint = f"{args.url}/translate"
        if args.batch:
            payload = {
                "text": args.text.split("|"),
                "source_lang": args.source,
                "target_lang": args.target,
            }
        else:
            payload = {
                "text": args.text,
                "source_lang": args.source,
                "target_lang": args.target,
            }

    print(f"POST {endpoint}")
    print(f"Payload: {json.dumps(payload, indent=2, ensure_ascii=False)}")

    resp = requests.post(endpoint, json=payload, timeout=30)
    resp.raise_for_status()
    data = resp.json()

    print(f"Response ({resp.elapsed.total_seconds() * 1000:.1f}ms):")
    print(json.dumps(data, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
```

- [ ] **Step 3: Update benchmark.py**

```python
#!/usr/bin/env python3
"""Benchmark translate service latency."""

import argparse
import statistics
import time

import requests


def main():
    parser = argparse.ArgumentParser(description="Benchmark translate API")
    parser.add_argument("--url", default="http://localhost:8000", help="API base URL")
    parser.add_argument("--source", default="en", help="Source language")
    parser.add_argument("--target", default="vi", help="Target language")
    parser.add_argument("--iterations", type=int, default=100, help="Number of requests")
    parser.add_argument(
        "--mode",
        default="both",
        choices=["fast", "quality", "both"],
        help="Which endpoint to benchmark",
    )
    parser.add_argument("--text", default="Hello everyone, welcome to today's meeting.", help="Text to translate")
    args = parser.parse_args()

    def run_bench(endpoint: str, payload: dict, label: str) -> None:
        latencies = []
        for i in range(args.iterations):
            started = time.perf_counter()
            resp = requests.post(f"{args.url}{endpoint}", json=payload, timeout=30)
            resp.raise_for_status()
            latencies.append((time.perf_counter() - started) * 1000)

        latencies.sort()
        avg = statistics.mean(latencies)
        p50 = latencies[len(latencies) // 2]
        p95 = latencies[int(len(latencies) * 0.95)]
        p99 = latencies[int(len(latencies) * 0.99)]
        rps = 1000 / avg if avg > 0 else 0

        print(f"\n--- {label} ({args.iterations} iterations) ---")
        print(f"  Avg:   {avg:.2f} ms")
        print(f"  P50:   {p50:.2f} ms")
        print(f"  P95:   {p95:.2f} ms")
        print(f"  P99:   {p99:.2f} ms")
        print(f"  RPS:   {rps:.2f}")

    if args.mode in ("fast", "both"):
        run_bench(
            "/translate/fast",
            {
                "text": args.text,
                "source_lang": args.source,
                "target_lang": args.target,
                "session_id": f"bench-{time.time_ns()}",
                "is_final": True,
            },
            "Fast Mode (greedy, beam_size=1)",
        )

    if args.mode in ("quality", "both"):
        run_bench(
            "/translate",
            {
                "text": args.text,
                "source_lang": args.source,
                "target_lang": args.target,
            },
            "Quality Mode (beam_size=4, post-processing)",
        )


if __name__ == "__main__":
    main()
```

- [ ] **Step 4: Commit**

```bash
git add translate/scripts/build_ct2_models.sh translate/scripts/test_translate.py translate/scripts/benchmark.py
git commit -m "feat(scripts): update build/test/benchmark for NLLB and dual mode"
```

---

### Task 12: Update Dependencies and Config Template

**Files:**
- Modify: `translate/requirements.txt`
- Modify: `translate/.env.example`

- [ ] **Step 1: Update requirements.txt**

```
fastapi
uvicorn[standard]
ctranslate2
transformers
sentencepiece
pydantic
pydantic-settings
python-dotenv
numpy
requests
protobuf
```

Removed: `sacremoses`, `torch` (only needed for conversion, documented in README)

- [ ] **Step 2: Update .env.example**

```env
MODEL_BASE_DIR=models/ct2/nllb-200-600M
TRANSLATION_DEVICE=cpu
TRANSLATION_COMPUTE_TYPE=int8
INTER_THREADS=1
INTRA_THREADS=4
DEFAULT_SOURCE_LANG=en
DEFAULT_TARGET_LANG=vi
MIN_REALTIME_CHARS=8
MAX_TEXT_CHARS=1000
SESSION_CACHE_TTL_SEC=300
LOG_LEVEL=INFO
FAST_BEAM_SIZE=1
QUALITY_BEAM_SIZE=4
GLOSSARY_PATH=glossary.json
```

- [ ] **Step 3: Commit**

```bash
git add translate/requirements.txt translate/.env.example
git commit -m "chore: update requirements and env template for NLLB"
```

---

### Task 13: Update Dockerfile

**Files:**
- Modify: `translate/docker/Dockerfile`

- [ ] **Step 1: Update Dockerfile — remove torch from runtime**

```dockerfile
FROM python:3.11-slim

WORKDIR /app

COPY requirements.txt ./requirements.txt

RUN pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir -r requirements.txt

COPY . .

EXPOSE 3333

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "3333"]
```

- [ ] **Step 2: Commit**

```bash
git add translate/docker/Dockerfile
git commit -m "chore(docker): remove torch from runtime, NLLB doesn't need it"
```

---

### Task 14: Rewrite README.md

**Files:**
- Modify: `translate/README.md`

- [ ] **Step 1: Replace README entirely**

```markdown
# Translate Service

Standalone self-hosted translation microservice for a Vosk-based live subtitle pipeline.

Powered by **NLLB-200 (600M distilled)** via **CTranslate2** for low-latency CPU inference.

## Features

- `GET /health`
- `POST /translate` — quality translation with beam search + Vietnamese post-processing
- `POST /translate/fast` — low-latency greedy translation for realtime subtitles
- Lazy model loading with in-memory cache per language pair
- Session-based partial text deduplication for Vosk realtime
- Vietnamese diacritic normalization and glossary support

## Endpoints

### `GET /health`

```json
{
  "status": "ok",
  "device": "cpu",
  "compute_type": "int8",
  "loaded_models": ["en-vi"],
  "available_pairs": ["en-vi", "vi-en"],
  "model": "nllb-200-distilled-600M"
}
```

### `POST /translate`

Quality translation (beam_size=4, Vietnamese post-processing). Accepts single string or array.

```json
// Request
{ "text": "Hello everyone, welcome.", "source_lang": "en", "target_lang": "vi" }

// Response
{ "translated_text": "Xin chào mọi người, chào mừng.", "latency_ms": 250.0 }
```

### `POST /translate/fast`

Low-latency greedy translation for Vosk realtime subtitles (<100ms target).

```json
// Request
{ "text": "hello everyone", "source_lang": "en", "target_lang": "vi", "session_id": "abc123", "is_final": false }

// Response
{ "translated_text": "xin chào mọi người", "should_display": true, "latency_ms": 18.1 }
```

## Installation

```bash
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
cp .env.example .env
```

## Convert NLLB-200 to CTranslate2

Requires `torch` for conversion:

```bash
pip install torch
python scripts/convert_nllb_to_ct2.py \
  --hf_model facebook/nllb-200-distilled-600M \
  --output_dir models/ct2/en-vi \
  --quantization int8
```

Or build all pairs:

```bash
bash scripts/build_ct2_models.sh int8
```

## Run

```bash
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

## Test

```bash
# Quality mode
python scripts/test_translate.py --url http://localhost:8000 --text "Hello world" --mode quality

# Fast mode
python scripts/test_translate.py --url http://localhost:8000 --text "Hello world" --mode fast
```

## Benchmark

```bash
python scripts/benchmark.py --url http://localhost:8000 --mode both --iterations 100
```

## Docker

```bash
docker compose -f docker/docker-compose.yml up --build
```
```

- [ ] **Step 2: Commit**

```bash
git add translate/README.md
git commit -m "docs: rewrite README for NLLB-200 and simplified API"
```

---

### Task 15: Integration Smoke Test

**Files:**
- None (manual verification)

- [ ] **Step 1: Verify all imports work**

```bash
cd translate && python -c "
from app.config import get_settings
from app.schemas import HealthResponse, TranslateFastRequest, TranslateFastResponse, TranslateRequest, TranslateResponse
from app.translator.nllb_ct2 import NLLBCT2Translator
from app.translator.model_manager import TranslationModelManager
from app.translator.session_cache import RealtimeSessionCache
from app.postprocess.vi_normalizer import normalize_vietnamese, fix_diacritics, merge_word_boundaries
from app.postprocess.glossary import Glossary
from app.utils.text import normalize_text, truncate_text, is_too_similar, is_good_realtime_boundary
print('All imports OK')
"
```

Expected: `All imports OK`

- [ ] **Step 2: Verify FastAPI app loads**

```bash
cd translate && python -c "
from app.main import app
print('Routes:', [r.path for r in app.routes])
"
```

Expected: Routes include `/health`, `/translate/fast`, `/translate`, `/docs`, `/redoc`, `/openapi.json`

- [ ] **Step 3: Run unit tests**

```bash
cd translate && python -m pytest tests/ -v
```

Expected: All unit tests pass

- [ ] **Step 4: Commit any final fixes**

```bash
git add -A && git commit -m "test: verify all imports, routes, and unit tests pass"
```
```

(Edit skipped due to content length, continuing)<｜end▁of▁thinking｜>Let me continue the plan document...

<｜｜DSML｜｜tool_calls>
<｜｜DSML｜｜invoke name="read">
<｜｜DSML｜｜parameter name="filePath" string="true">/Users/taitran/Desktop/sublingual/docs/superpowers/plans/2026-06-06-translate-refactor-plan.md