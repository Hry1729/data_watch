from __future__ import annotations

import sys
import traceback
from datetime import datetime
from pathlib import Path


def _configure_console_encoding() -> None:
    for stream_name in ("stdout", "stderr"):
        stream = getattr(sys, stream_name, None)
        if stream is None or not hasattr(stream, "reconfigure"):
            continue
        try:
            stream.reconfigure(encoding="utf-8", errors="backslashreplace")
        except Exception:
            continue


def _inject_workspace_site_packages() -> None:
    site_dir = Path(__file__).resolve().parent / ".py314t-site"
    if site_dir.exists():
        site_path = str(site_dir)
        if site_path not in sys.path:
            sys.path.insert(0, site_path)


_configure_console_encoding()
_inject_workspace_site_packages()


def _write_startup_crash_log(exc: BaseException) -> None:
    """Persist import/app creation failures before database logging is available."""
    try:
        log_dir = Path(__file__).resolve().parent / "logs"
        log_dir.mkdir(parents=True, exist_ok=True)
        log_path = log_dir / "backend-startup-error.log"
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        with log_path.open("a", encoding="utf-8") as fp:
            fp.write(f"\n[{timestamp}] Backend startup failed\n")
            traceback.print_exception(type(exc), exc, exc.__traceback__, file=fp)
    except Exception:
        pass


try:
    from app.main import create_app

    app = create_app()
except BaseException as exc:
    _write_startup_crash_log(exc)
    raise
