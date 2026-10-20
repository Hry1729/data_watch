"""写入器系统日志写入器。Writer system-log writer.

Best-effort writer that pushes selected writer runtime events into the MySQL
`system_log` table, so they show up in the central log monitoring page (same
channel as collector / backend).

Design notes:
- Decoupled from the writer's main MySQL connection: uses its OWN dedicated
  pymysql connection on a background daemon thread. The writer's main connection
  is sync pymysql shared with the consumer (used inside executors) and is NOT
  thread-safe for concurrent use, so logging must never touch it.
- Best-effort: emit() never blocks (put_nowait, drop on full); the worker
  swallows DB errors and lazily reconnects. Logging must never break ingestion.
"""

from __future__ import annotations

import json
import logging
import queue
import socket
import threading
import time
from datetime import datetime
from typing import Any, Optional

import pymysql

logger = logging.getLogger("writer.system_log")

_VALID_LEVELS = {"DEBUG", "INFO", "WARN", "ERROR", "CRITICAL"}
_QUEUE_FULL_WARN_INTERVAL = 60.0
_STOP_POLL_INTERVAL = 0.2  # 停止信号检查间隔（秒）

# 列顺序镜像后端 SystemLog 模型（backend/app/models/log.py）。
_INSERT_SQL = (
    "INSERT INTO system_log "
    "(log_time, level, log_type, source, device_id, device_code, point_id, "
    "host_name, service_name, content, detail_json, trace_id, exception_stack, "
    "elapsed_ms, create_time) "
    "VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)"
)


def _normalize_level(level: str) -> str:
    normalized = (level or "INFO").strip().upper()
    if normalized == "WARNING":
        return "WARN"
    if normalized not in _VALID_LEVELS:
        return "INFO"
    return normalized


def _clip(value: Optional[str], max_len: int) -> str:
    text = "" if value is None else str(value)
    return text if len(text) <= max_len else text[:max_len]


class SystemLogWriter:
    """用于写入器进程系统日志的异步尽力投递写入器。"""

    def __init__(self, mysql_config: dict[str, Any], queue_max_size: int = 1000) -> None:
        self._config = mysql_config
        self._queue: queue.Queue[Optional[dict[str, Any]]] = queue.Queue(maxsize=queue_max_size)
        self._thread: Optional[threading.Thread] = None
        self._running = False
        # B028：独立停止信号。满队列时哨兵可能放不进去，工作线程靠它在排空后自行退出。
        self._stop_requested = threading.Event()
        self._lock = threading.Lock()
        self._conn: Optional[pymysql.connections.Connection] = None
        self._host_name = _clip(socket.gethostname(), 64)
        self._last_full_warning = 0.0

    def start(self) -> None:
        """启动后台工作线程。不会因数据库连接失败（懒连接）。"""
        if self._running:
            return
        self._running = True
        self._stop_requested.clear()
        self._thread = threading.Thread(
            target=self._run,
            name="writer-system-log",
            daemon=True,
        )
        self._thread.start()
        logger.info("Writer system log writer started")

    def emit(
        self,
        *,
        level: str,
        log_type: str,
        source: str,
        content: str,
        detail_json: Optional[dict[str, Any]] = None,
        device_code: str = "",
        service_name: str = "writer",
        trace_id: str = "",
        exception_stack: Optional[str] = None,
        elapsed_ms: Optional[int] = None,
    ) -> bool:
        """将系统日志事件加入队列，不阻塞。Queue a system-log event without blocking. Drops on full queue.

        The "check _running + enqueue" is guarded by the same lock stop() uses to
        "set _running=False + enqueue sentinel", so an event can never land after
        the sentinel (which the worker would never process).
        """
        event = {
            "level": _normalize_level(level),
            "log_type": log_type,
            "source": source,
            "content": content,
            "detail_json": detail_json,
            "device_code": device_code,
            "service_name": service_name,
            "trace_id": trace_id,
            "exception_stack": exception_stack,
            "elapsed_ms": elapsed_ms,
        }
        with self._lock:
            if not self._running:
                return False
            try:
                self._queue.put_nowait(event)
                return True
            except queue.Full:
                now = time.time()
                if now - self._last_full_warning >= _QUEUE_FULL_WARN_INTERVAL:
                    self._last_full_warning = now
                    logger.warning("Writer system log queue full, dropping events")
                return False

    def stop(self, timeout: float = 5.0) -> None:
        """刷新已排队的事件然后关闭。Flush already-queued events then shut down.

        The sentinel is enqueued AFTER existing events (queue not cleared), so the
        worker processes everything queued before stop() — including a final
        "writer stopped" — before exiting. join(timeout) bounds the wait.
        """
        with self._lock:
            first_stop = self._running
            self._running = False
            self._stop_requested.set()
            if first_stop:
                # 在现有事件之后将哨兵入队，不丢弃任何事件；
                # 有界阻塞 put 等待工作线程腾出槽位。
                try:
                    self._queue.put(None, timeout=timeout)
                except queue.Full:
                    # 哨兵放不进去也能收尾：工作线程排空队列后看到停止信号自行退出。
                    logger.warning(
                        "Writer system log queue full, stop sentinel dropped; "
                        "worker exits after draining")
        thread = self._thread
        if thread is None and not first_stop:
            return
        if thread is None:
            self._close_conn()
            return
        thread.join(timeout=timeout)
        if thread.is_alive():
            # 工作线程仍在忙碌（例如卡在执行中）；不要关闭连接
            # 它可能正从此线程使用中，保留引用。
            logger.warning("Writer system log worker did not stop within timeout")
            return
        self._thread = None
        self._close_conn()

    # ── 内部方法 ──────────────────────────────────────────────────────────────

    def _run(self) -> None:
        while True:
            try:
                event = self._queue.get(timeout=_STOP_POLL_INTERVAL)
            except queue.Empty:
                if self._stop_requested.is_set():
                    break
                continue
            if event is None:
                break
            try:
                self._write(event)
            except Exception as exc:  # best-effort: never propagate
                logger.warning("Writer system log write failed: %s", exc)
                self._close_conn()  # force reconnect on next event

    def _ensure_conn(self) -> Optional[pymysql.connections.Connection]:
        if self._conn is not None:
            return self._conn
        try:
            self._conn = pymysql.connect(
                host=self._config["host"],
                port=int(self._config["port"]),
                user=self._config["user"],
                password=self._config["password"],
                database=self._config["database"],
                charset="utf8mb4",
                autocommit=True,
                connect_timeout=5,
            )
        except Exception as exc:
            logger.warning("Writer system log cannot connect MySQL: %s", exc)
            self._conn = None
        return self._conn

    def _write(self, event: dict[str, Any]) -> None:
        conn = self._ensure_conn()
        if conn is None:
            return  # dropped this round; connection retried on next event
        detail = event["detail_json"]
        detail_str = json.dumps(detail, ensure_ascii=False) if detail is not None else None
        stack = event["exception_stack"]
        now = datetime.now()
        params = (
            now,                                  # log_time
            event["level"],                       # level
            _clip(event["log_type"], 32),         # log_type
            _clip(event["source"], 64),           # source
            None,                                 # device_id
            _clip(event["device_code"], 64),      # device_code
            None,                                 # point_id
            self._host_name,                      # host_name
            _clip(event["service_name"], 64),     # service_name
            _clip(event["content"], 500),         # content
            detail_str,                           # detail_json
            _clip(event["trace_id"], 64),         # trace_id
            stack[:65535] if stack else None,     # exception_stack
            event["elapsed_ms"],                  # elapsed_ms
            now,                                  # create_time
        )
        with conn.cursor() as cur:
            cur.execute(_INSERT_SQL, params)

    def _close_conn(self) -> None:
        if self._conn is not None:
            try:
                self._conn.close()
            except Exception:
                pass
            self._conn = None
