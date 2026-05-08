"""Collector system-log writer.

This module writes selected collector runtime events to MySQL system_log.
It is deliberately explicit instead of mirroring every Python logging record,
so high-frequency collection debug output does not flood MySQL.
"""

from __future__ import annotations

import asyncio
import time
from dataclasses import dataclass, field
from typing import Any, Dict, Optional

from core.logger import get_logger

logger = get_logger("system_log")


@dataclass
class SystemLogEvent:
    level: str
    log_type: str
    source: str
    content: str
    detail_json: Optional[Dict[str, Any]] = field(default_factory=dict)
    device_id: Optional[int] = None
    device_code: str = ""
    point_id: Optional[int] = None
    trace_id: str = ""
    exception_stack: Optional[str] = None
    elapsed_ms: Optional[int] = None


class SystemLogWriter:
    """Asynchronous best-effort writer for collector system logs."""

    def __init__(self, config_db_manager: Any, queue_max_size: int = 1000):
        self.config_db_manager = config_db_manager
        self.queue_max_size = queue_max_size
        self._queue: asyncio.Queue[Optional[SystemLogEvent]] = asyncio.Queue(maxsize=queue_max_size)
        self._worker_task: Optional[asyncio.Task] = None
        self._running = False
        self._last_queue_full_warning = 0.0

    async def start(self) -> None:
        if self._running or self.config_db_manager is None:
            return
        self._running = True
        self._worker_task = asyncio.create_task(self._run(), name="collector-system-log-writer")
        logger.info("Collector system log writer started")

    async def stop(self, timeout: float = 5.0) -> None:
        if not self._running:
            return
        self._running = False
        await self._queue.put(None)
        if self._worker_task is not None:
            try:
                await asyncio.wait_for(self._worker_task, timeout=timeout)
            except asyncio.TimeoutError:
                self._worker_task.cancel()
                logger.warning("Collector system log writer stop timed out")
            finally:
                self._worker_task = None
        logger.info("Collector system log writer stopped")

    def emit(
        self,
        *,
        level: str,
        log_type: str,
        source: str,
        content: str,
        detail_json: Optional[Dict[str, Any]] = None,
        device_id: Optional[int] = None,
        device_code: str = "",
        point_id: Optional[int] = None,
        trace_id: str = "",
        exception_stack: Optional[str] = None,
        elapsed_ms: Optional[int] = None,
    ) -> bool:
        """Queue a system-log event without blocking the collection path."""
        if not self._running:
            return False
        event = SystemLogEvent(
            level=level,
            log_type=log_type,
            source=source,
            content=content,
            detail_json=detail_json or {},
            device_id=device_id,
            device_code=device_code or "",
            point_id=point_id,
            trace_id=trace_id or "",
            exception_stack=exception_stack,
            elapsed_ms=elapsed_ms,
        )
        try:
            self._queue.put_nowait(event)
            return True
        except asyncio.QueueFull:
            now = time.time()
            if now - self._last_queue_full_warning >= 60:
                self._last_queue_full_warning = now
                logger.warning("Collector system log queue full, dropping events")
            return False

    async def _run(self) -> None:
        while True:
            event = await self._queue.get()
            if event is None:
                self._queue.task_done()
                break
            try:
                await self.config_db_manager.write_system_log(
                    level=event.level,
                    log_type=event.log_type,
                    source=event.source,
                    content=event.content,
                    detail_json=event.detail_json,
                    device_id=event.device_id,
                    device_code=event.device_code,
                    point_id=event.point_id,
                    trace_id=event.trace_id,
                    exception_stack=event.exception_stack,
                    elapsed_ms=event.elapsed_ms,
                )
            except Exception as exc:  # write_system_log is best-effort; keep this as a final guard.
                logger.warning("Collector system log write failed: %s", exc)
            finally:
                self._queue.task_done()
