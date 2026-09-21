"""Collector 驱动加载状态探针（供 Agent 安装后确认真实 loaded）。"""

from __future__ import annotations

import json
import os
import threading
import sys
import uuid
from pathlib import Path
from core.version import __version__
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


_BOOT_ID = str(uuid.uuid4())


def runtime_identity() -> dict[str, Any]:
    if getattr(sys, "frozen", False):
        catalog = Path(sys.executable).parent / "driver-catalog/catalog.json"
    else:
        catalog = Path(__file__).resolve().parents[2] / "packages/driver-catalog/catalog.json"
    try:
        build_id = json.loads(catalog.read_text(encoding="utf-8"))["build_id"]
    except (OSError, ValueError, KeyError, TypeError):
        build_id = f"unverified-{__version__}"
    return {
        "role": "collector",
        "deployment_role": os.environ.get("DATAWATCH_ROLE", "centralized"),
        "build_id": build_id, "boot_id": _BOOT_ID, "pid": os.getpid(),
        "version": __version__,
        "data_root": str(Path(os.environ.get("DATAWATCH_DATA_ROOT", r"C:\ProgramData\DataWatch")).resolve()),
    }


class DriverStatusServer:
    """轻量 HTTP：GET /drivers、GET /health。

    P0 修复：两个端点共享同一注册表快照，避免并发请求读到不同状态。
    """

    def __init__(
        self,
        *,
        host: str = "127.0.0.1",
        port: Optional[int] = None,
        list_drivers: Optional[Callable[[], list[dict[str, Any]]]] = None,
        get_generation: Optional[Callable[[], int]] = None,
        get_snapshot: Optional[Callable[[], dict[str, Any]]] = None,
    ):
        self.host = host
        self.port = default_status_port() if port is None else int(port)
        self._list_drivers = list_drivers or (lambda: [])
        self._get_generation = get_generation or (lambda: 0)
        self._get_snapshot = get_snapshot or (lambda: {"generation": self._get_generation(), "drivers": self._list_drivers()})
        self._identity = runtime_identity()
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
                if path in {"/health", "/drivers/health", "/drivers"}:
                    snapshot = outer._get_snapshot()
                    body = {"generation": snapshot["generation"], "instance": outer._identity}
                    if path == "/drivers":
                        body["drivers"] = snapshot["drivers"]
                    else:
                        body["status"] = "ok"
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
            self._server.server_close()
        except Exception as exc:
            logger.warning("Driver status server shutdown failed: %s", exc)
        self._server = None
        self._thread = None
