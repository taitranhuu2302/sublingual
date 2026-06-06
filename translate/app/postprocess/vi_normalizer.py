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
    "qụa": "quạ",
}


def fix_diacritics(text: str) -> str:
    result = text
    for wrong, correct in VI_DIACRITIC_FIXES.items():
        result = result.replace(wrong, correct)
    return result


def merge_word_boundaries(text: str) -> str:
    return re.sub(r"\b(\w) (\w{2,})\b", r"\1\2", text)


def normalize_vietnamese(text: str) -> str:
    if not text:
        return ""
    text = _WHITESPACE_RE.sub(" ", text).strip()
    text = fix_diacritics(text)
    text = merge_word_boundaries(text)
    return text
