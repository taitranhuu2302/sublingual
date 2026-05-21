import re


CONTROL_CHAR_RE = re.compile(r"[\x00-\x1f\x7f]")
WHITESPACE_RE = re.compile(r"\s+")
BOUNDARY_PUNCTUATION = ".,!?;:)])}"


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
