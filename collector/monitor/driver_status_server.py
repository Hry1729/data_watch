"""Collector 驱动加载状态探针（供 Agent 安装后确认真实 loaded）。"""

from __future__ import annotations

import json
import os
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any, Callable, Optional
from urllib.parse import urlparse

from core.logger import get_logger

logger = get_logger("driver_status_server")


def default_status_port() -> int:
    raw = os.environ.get("DATAWATCH_COLLECTOR_STATUS_PORT", "6868")
    try:
        return int(raw)
    except (TypeError, ValueError):
        return 6868


class DriverStatusServer:
    """轻量 HTTP：GET /drivers、GET /health。"""

    def __init__(
        self,
        *,
        host: str = "127.0.0.1",
        port: Optional[int] = None,
        list_drivers: Optional[Callable[[], list[dict[str, Any]]]] = None,
        get_generation: Optional[Callable[[], int]] = None,
    ):
        self.host = host
        self.port = default_status_port() if port is None else int(port)
        self._list_drivers = list_drivers or (lambda: [])
        self._get_generation = get_generation or (lambda: 0)
        self._server: Optional[ThreadingHTTPServer] = None
        self._thread: Optional[threading.Thread] = None

    def start(self) -> None:
        if self._server is not None:
            return
        outer = self

        class _Handler(BaseHTTPRequestHandler):
            def log_message(self, fmt: str, *args) -> None:  # noqa: A003
                logger.debug("driver-status: " + fmt, *args)

            def do_GET(self):  # noqa: N802
                path = urlparse(self.path).path
                if path in {"/health", "/drivers/health"}:
                    body = {
                        "status": "ok",
                        "generation": outer._get_generation(),
                    }
                    self._send(200, body)
                    return
                if path == "/drivers":
                    body = {
                        "generation": outer._get_generation(),
                        "drivers": outer._list_drivers(),
                    }
                    self._send(200, body)
                    return
                self._send(404, {"error": "not found"})

            def _send(self, code: int, payload: dict[str, Any]) -> None:
                raw = json.dumps(payload, ensure_ascii=False).encode("utf-8")
                self.send_response(code)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Content-Length", str(len(raw)))
                self.end_headers()
                self.wfile.write(raw)

        self._server = ThreadingHTTPServer((self.host, self.port), _Handler)
        self._thread = threading.Thread(
            target=self._server.serve_forever,
            name="driver-status-http",
            daemon=True,
        )
        self._thread.start()
        logger.info("Driver status server listening on %s:%s", self.host, self.port)

    def stop(self) -> None:
        if self._server is None:
            return
        try:
            self._server.shutdown()
        except Exception as exc:
            logger.warning("Driver status server shutdown failed: %s", exc)
        self._server = None
        self._thread = None
