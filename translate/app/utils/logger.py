import logging
import os
from datetime import datetime
from pathlib import Path

MAX_LOG_SIZE = 5 * 1024 * 1024  # 5 MB
MAX_LOG_FILES = 10

_current_log_file: str | None = None
_file_handler: logging.FileHandler | None = None


def _check_rotation(log_dir: str, prefix: str) -> None:
    """Rotate if current file exceeds MAX_LOG_SIZE. Cleanup old files."""
    global _current_log_file, _file_handler

    if not _current_log_file or not os.path.exists(_current_log_file):
        return

    try:
        if os.path.getsize(_current_log_file) > MAX_LOG_SIZE:
            # close current
            if _file_handler:
                root_logger = logging.getLogger()
                root_logger.removeHandler(_file_handler)
                _file_handler.close()

            # create new file
            _open_new_log_file(log_dir, prefix)

            # cleanup old files
            _cleanup_old_files(log_dir, prefix)
    except OSError:
        pass


def _open_new_log_file(log_dir: str, prefix: str) -> None:
    global _current_log_file, _file_handler

    timestamp = datetime.now().strftime("%Y-%m-%d-%H-%M-%S")
    _current_log_file = str(Path(log_dir) / f"{prefix}-{timestamp}.log")

    _file_handler = logging.FileHandler(_current_log_file, encoding="utf-8")
    _file_handler.setFormatter(
        logging.Formatter(
            "%(asctime)s | %(levelname)s | %(name)s | %(message)s"
        )
    )
    logging.getLogger().addHandler(_file_handler)


def _cleanup_old_files(log_dir: str, prefix: str) -> None:
    """Delete oldest files beyond MAX_LOG_FILES limit."""
    try:
        log_path = Path(log_dir)
        if not log_path.is_dir():
            return
        files = sorted(
            [f for f in log_path.iterdir() if f.name.startswith(prefix + "-") and f.suffix == ".log"],
            key=lambda f: f.stat().st_mtime,
            reverse=True,
        )
        for old in files[MAX_LOG_FILES:]:
            old.unlink(missing_ok=True)
    except OSError:
        pass


class LogRotationFilter(logging.Filter):
    """Filter that checks rotation after each log record."""

    def __init__(self, log_dir: str, prefix: str) -> None:
        super().__init__()
        self.log_dir = log_dir
        self.prefix = prefix

    def filter(self, record: logging.LogRecord) -> bool:
        _check_rotation(self.log_dir, self.prefix)
        return True


def configure_logging(level: str = "INFO", log_dir: str = "") -> None:
    root_logger = logging.getLogger()
    root_logger.setLevel(getattr(logging, level.upper(), logging.INFO))

    root_logger.handlers.clear()

    # always add stream handler
    stream_handler = logging.StreamHandler()
    stream_handler.setFormatter(
        logging.Formatter(
            "%(asctime)s | %(levelname)s | %(name)s | %(message)s"
        )
    )
    root_logger.addHandler(stream_handler)

    # add file handler if log_dir provided
    if log_dir:
        log_path = Path(log_dir)
        log_path.mkdir(parents=True, exist_ok=True)

        _open_new_log_file(log_dir, "service")
        _cleanup_old_files(log_dir, "service")

        root_logger.addFilter(LogRotationFilter(log_dir, "service"))
