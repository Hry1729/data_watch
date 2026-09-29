"""Request-local timing for the page-loading endpoints."""

from __future__ import annotations

import logging
from contextlib import contextmanager
from contextvars import ContextVar
from dataclasses import dataclass, field
from time import perf_counter
from uuid import uuid4


logger = logging.getLogger("backend.request")
_PATHS = {
    "/api/device-monitor/dashboard/overview",
    "/api/device-monitor/dashboard/trend",
    "/api/device-monitor/device/list",
    "/api/device-monitor/device/summary",
    "/api/device-monitor/point/list",
    "/api/device-monitor/point/latest",
}


@dataclass
class RequestMetrics:
    request_id: str = field(default_factory=lambda: uuid4().hex)
    durations: dict[str, float] = field(default_factory=dict)
    counts: dict[str, int] = field(default_factory=dict)
    completed: bool = False


_current: ContextVar[RequestMetrics | None] = ContextVar("request_metrics", default=None)


def record_operation(name: str, duration: float) -> None:
    metrics = _current.get()
    if metrics is not None:
        if metrics.completed:
            logger.info("request_id=%s operation=%s operation_ms=%.1f after_response=true", metrics.request_id, name, duration * 1000)
            return
        metrics.durations[name] = metrics.durations.get(name, 0.0) + duration * 1000
        metrics.counts[name] = metrics.counts.get(name, 0) + 1


@contextmanager
def measure_operation(name: str):
    started = perf_counter()
    try:
        yield
    finally:
        record_operation(name, perf_counter() - started)


async def observe_http(dispatch, scope, receive, send) -> None:
    if scope["type"] != "http" or scope.get("path") not in _PATHS:
        await dispatch(scope, receive, send)
        return

    metrics = RequestMetrics()
    token = _current.set(metrics)
    started = perf_counter()
    status = 500

    async def timed_send(message):
        nonlocal status
        if message["type"] == "http.response.start":
            status = message["status"]
            message = dict(message)
            message["headers"] = list(message.get("headers", [])) + [
                (b"x-request-id", metrics.request_id.encode("ascii")),
                (b"server-timing", f"app;dur={(perf_counter() - started) * 1000:.1f}".encode("ascii")),
                (b"access-control-expose-headers", b"x-request-id, server-timing"),
            ]
        await send(message)

    try:
        await dispatch(scope, receive, timed_send)
    finally:
        elapsed = (perf_counter() - started) * 1000
        metrics.completed = True
        _current.reset(token)
        log = logger.warning if status >= 500 or elapsed >= 1000 else logger.info
        log(
            "request_id=%s method=%s path=%s status=%s elapsed_ms=%.1f operations_ms=%s operations_count=%s",
            metrics.request_id, scope.get("method"), scope["path"], status, elapsed,
            {name: round(value, 1) for name, value in metrics.durations.items()}, metrics.counts,
        )
