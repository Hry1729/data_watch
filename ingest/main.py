from __future__ import annotations

import asyncio
import multiprocessing
import os
import sys
from pathlib import Path

def _configure_console_encoding() -> None:
    for stream_name in ("stdout", "stderr"):
        stream = getattr(sys, stream_name, None)
        if stream is None or not hasattr(stream, "reconfigure"):
            continue
        try:
            stream.reconfigure(encoding="utf-8", errors="backslashreplace")
        except Exception:
            pass


_configure_console_encoding()

_REPO_ROOT = Path(__file__).resolve().parent.parent
if str(_REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(_REPO_ROOT))

from ingest.app.config import IngestSettings, get_settings
from ingest.app.main import app
from ingest.app.repository import MySQLIngestRepository

__all__ = ["app"]

_SCHEMA_READY_ENV = "DATAWATCH_INGEST_SCHEMA_READY"


async def _prepare_schema_for_workers(settings: IngestSettings) -> None:
    repository = await MySQLIngestRepository.create(settings)
    await repository.close()
    os.environ[_SCHEMA_READY_ENV] = "1"


if __name__ == "__main__":
    import uvicorn

    multiprocessing.freeze_support()
    os.environ.setdefault("PYTHONUTF8", "1")
    settings = get_settings()
    if settings.workers > 1 and os.getenv(_SCHEMA_READY_ENV) != "1":
        asyncio.run(_prepare_schema_for_workers(settings))
    uvicorn.run(
        "ingest.app.main:app",
        host=settings.host,
        port=settings.port,
        log_level="info",
        access_log=settings.access_log,
        workers=settings.workers,
    )
