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
