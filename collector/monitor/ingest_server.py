"""外部接口采集协议服务（HTTP Push）"""

import asyncio
import json
import threading
import time
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any, Dict, List, Optional, Tuple, TYPE_CHECKING
from urllib.parse import urlparse

from core.logger import get_logger
from data_queue.buffer import DataBuffer, DataPoint

if TYPE_CHECKING:
    from core.opcda_mapper import OpcDaTagMapper

logger = get_logger("ingest_server")

# 采样日志配置：每 N 秒输出一次汇总
LOG_SAMPLE_INTERVAL_SECONDS = 10


def _parse_timestamp(raw_value: Any) -> datetime:
    """解析时间戳，支持 ISO8601 字符串/Unix 秒/Unix 毫秒"""
    if raw_value is None:
        return datetime.now()

    if isinstance(raw_value, (int, float)):
        ts = float(raw_value)
        if ts > 1_000_000_000_000:
            ts /= 1000.0
        return datetime.fromtimestamp(ts)

    if isinstance(raw_value, str):
        text = raw_value.strip()
        if not text:
            return datetime.now()

        # 兼容 Z 结尾
        if text.endswith("Z"):
            text = text[:-1] + "+00:00"

        dt = datetime.fromisoformat(text)
        # 当前链路统一使用 naive datetime，避免下游 SQL 构造出现时区格式不兼容
        if dt.tzinfo is not None:
            dt = dt.astimezone().replace(tzinfo=None)
        return dt

    raise ValueError("timestamp must be ISO8601 string or unix timestamp")


def _normalize_payload(payload: Any, max_batch_size: int) -> List[Dict[str, Any]]:
    """标准化请求体，支持单点和批量点位"""
    if isinstance(payload, dict) and "points" in payload:
        points = payload.get("points")
        if not isinstance(points, list):
            raise ValueError("points must be a list")
        normalized = points
    elif isinstance(payload, list):
        normalized = payload
    elif isinstance(payload, dict):
        normalized = [payload]
    else:
        raise ValueError("payload must be an object or list")

    if not normalized:
        raise ValueError("points cannot be empty")

    if len(normalized) > max_batch_size:
        raise ValueError(f"points exceeds max_batch_size={max_batch_size}")

    return normalized


def _point_from_dict(item: Dict[str, Any]) -> DataPoint:
    """将单个字典转换为 DataPoint"""
    device_id = str(item.get("device_id", "")).strip()
    variable_id = str(item.get("variable_id", "")).strip()
    value = item.get("value")

    if not device_id:
        raise ValueError("device_id is required")
    if not variable_id:
        raise ValueError("variable_id is required")
    if value is None:
        raise ValueError("value is required")

    timestamp = _parse_timestamp(item.get("timestamp"))

    return DataPoint(
        device_id=device_id,
        variable_id=variable_id,
        value=value,
        timestamp=timestamp,
    )


class _IngestHTTPServer(ThreadingHTTPServer):
    """携带运行时上下文的 HTTPServer"""

    daemon_threads = True

    # 默认请求体上限：1MB
    DEFAULT_MAX_BODY_BYTES = 1_048_576

    def __init__(
        self,
        server_address: Tuple[str, int],
        request_handler,
        data_buffer: DataBuffer,
        event_loop: asyncio.AbstractEventLoop,
        ingest_path: str,
        api_key: Optional[str],
        max_batch_size: int,
        opcda_mapper: Optional["OpcDaTagMapper"] = None,
        opcda_ingest_path: str = "/ingest/opcda-tags",
        max_body_bytes: int = DEFAULT_MAX_BODY_BYTES,
    ):
        super().__init__(server_address, request_handler)
        self.data_buffer = data_buffer
        self.event_loop = event_loop
        self.ingest_path = ingest_path
        self.api_key = api_key
        self.max_batch_size = max_batch_size
        self.opcda_mapper = opcda_mapper
        self.opcda_ingest_path = opcda_ingest_path
        self.max_body_bytes = max_body_bytes


class _IngestRequestHandler(BaseHTTPRequestHandler):
    """处理外部推送请求"""

    server: _IngestHTTPServer

    def _send_json(self, status_code: int, payload: Dict[str, Any]) -> None:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status_code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _read_json_body(self) -> Any:
        content_len = self.headers.get("Content-Length")
        if not content_len:
            raise ValueError("Content-Length is required")

        content_len_int = int(content_len)
        if content_len_int <= 0:
            raise ValueError("Content-Length must be positive")
        if content_len_int > self.server.max_body_bytes:
            raise ValueError(f"request body exceeds max_body_bytes={self.server.max_body_bytes}")

        raw = self.rfile.read(content_len_int)
        try:
            return json.loads(raw.decode("utf-8"))
        except json.JSONDecodeError as exc:
            raise ValueError(f"invalid json: {exc}") from exc

    def _is_authorized(self) -> bool:
        server_key = (self.server.api_key or "").strip()
        if not server_key:
            return True
        client_key = (self.headers.get("X-API-Key") or "").strip()
        return client_key == server_key

    def do_GET(self):  # noqa: N802 - BaseHTTPRequestHandler override
        parsed = urlparse(self.path)
        if parsed.path == "/ingest/health":
            response = {
                "status": "ok",
                "queue": self.server.data_buffer.stats,
                "ingest_path": self.server.ingest_path,
            }
            if self.server.opcda_mapper:
                response["opcda_ingest_path"] = self.server.opcda_ingest_path
                response["opcda_mappings"] = self.server.opcda_mapper.mapping_count
            self._send_json(200, response)
            return

        self._send_json(404, {"error": "not found"})

    def do_POST(self):  # noqa: N802 - BaseHTTPRequestHandler override
        parsed = urlparse(self.path)

        # OPC DA 专用端点
        if parsed.path == self.server.opcda_ingest_path:
            self._handle_opcda_ingest()
            return

        # 通用 variable_id 端点
        if parsed.path != self.server.ingest_path:
            self._send_json(404, {"error": "not found"})
            return

        if not self._is_authorized():
            self._send_json(401, {"error": "unauthorized"})
            return

        try:
            payload = self._read_json_body()
            normalized = _normalize_payload(payload, self.server.max_batch_size)

            points: List[DataPoint] = []
            errors: List[str] = []
            for idx, item in enumerate(normalized):
                try:
                    if not isinstance(item, dict):
                        raise ValueError("point must be object")
                    points.append(_point_from_dict(item))
                except ValueError as exc:
                    errors.append(f"points[{idx}]: {exc}")

            if not points:
                self._send_json(400, {"accepted": 0, "rejected": len(normalized), "errors": errors})
                return

            future = asyncio.run_coroutine_threadsafe(
                self.server.data_buffer.put_batch(points),
                self.server.event_loop,
            )
            accepted = future.result(timeout=5)
            dropped = len(points) - accepted

            if errors:
                logger.warning(
                    "Ingest partially accepted: accepted=%s rejected=%s dropped=%s",
                    accepted,
                    len(errors),
                    dropped,
                )
            else:
                logger.debug("Ingest accepted points=%s dropped=%s", accepted, dropped)

            self._send_json(
                200,
                {
                    "accepted": accepted,
                    "received": len(normalized),
                    "rejected": len(errors),
                    "dropped": dropped,
                    "errors": errors,
                },
            )
        except ValueError as exc:
            self._send_json(400, {"error": str(exc)})
        except Exception as exc:  # pragma: no cover - 兜底保护
            logger.error("Ingest request failed: %s", exc)
            self._send_json(500, {"error": "internal server error"})

    def _handle_opcda_ingest(self) -> None:
        """处理 OPC DA sidecar 推送的数据"""
        if not self._is_authorized():
            self._send_json(401, {"error": "unauthorized"})
            return

        mapper = self.server.opcda_mapper
        if mapper is None:
            self._send_json(503, {"error": "OPC DA mapper not configured"})
            return

        try:
            payload = self._read_json_body()

            # 验证 payload 格式
            if not isinstance(payload, dict):
                self._send_json(400, {"error": "payload must be an object"})
                return

            schema = payload.get("schema", "")
            if schema != "opcda_tag_batch_v1":
                self._send_json(400, {"error": f"unsupported schema: {schema}"})
                return

            raw_points = payload.get("points", [])
            if not isinstance(raw_points, list):
                self._send_json(400, {"error": "points must be a list"})
                return

            if not raw_points:
                self._send_json(200, {"accepted": 0, "unmapped": 0, "errors": []})
                return

            if len(raw_points) > self.server.max_batch_size:
                self._send_json(400, {"error": f"points exceeds max_batch_size={self.server.max_batch_size}"})
                return

            points: List[DataPoint] = []
            unmapped: List[str] = []
            errors: List[str] = []

            for idx, item in enumerate(raw_points):
                if not isinstance(item, dict):
                    errors.append(f"points[{idx}]: not an object")
                    continue

                # 从 item_id 或构建完整路径
                item_id = item.get("item_id", "")
                device_id = item.get("device_id", "")
                if not item_id:
                    # 兼容旧格式：尝试从 device_id + group + tag 构建
                    # 注意：推荐使用完整 item_id，格式如 "Channel.Device.Group.Tag"
                    group = item.get("group", "")
                    tag = item.get("tag", "")
                    if device_id and group and tag:
                        item_id = f"{device_id}.{group}.{tag}"
                        logger.debug(
                            "Constructed item_id from legacy format: %s "
                            "(recommend using full item_id like 'Channel.Device.Group.Tag')",
                            item_id,
                        )
                    else:
                        errors.append(
                            f"points[{idx}]: missing item_id. "
                            f"Provide full item_id (e.g., 'OPD_Server.AGV801.ViewMain.Tag30041800') "
                            f"or device_id/group/tag for legacy fallback."
                        )
                        continue

                # 查找 variable_id
                variable_id = mapper.get_variable_id(item_id)
                if variable_id is None:
                    unmapped.append(item_id)
                    logger.debug(
                        "item_id not found in mapper: %s. "
                        "Ensure MySQL device_variable.var_address matches the OPC DA item_id.",
                        item_id,
                    )
                    continue

                # 解析值和时间戳
                value = item.get("value")
                if value is None:
                    errors.append(f"points[{idx}]: missing value")
                    continue

                try:
                    timestamp = _parse_timestamp(item.get("timestamp"))
                except ValueError as exc:
                    errors.append(f"points[{idx}]: {exc}")
                    continue

                # 获取 device_id（用于 DataPoint）
                device_id = item.get("device_id", "")

                points.append(DataPoint(
                    device_id=device_id,
                    variable_id=variable_id,
                    value=value,
                    timestamp=timestamp,
                ))

            if not points:
                self._send_json(200, {
                    "accepted": 0,
                    "unmapped": len(unmapped),
                    "errors": errors,
                })
                return

            # 写入 DataBuffer
            future = asyncio.run_coroutine_threadsafe(
                self.server.data_buffer.put_batch(points),
                self.server.event_loop,
            )
            accepted = future.result(timeout=5)
            dropped = len(points) - accepted

            if unmapped or errors:
                logger.warning(
                    "OPC DA ingest: accepted=%s unmapped=%s errors=%s dropped=%s",
                    accepted,
                    len(unmapped),
                    len(errors),
                    dropped,
                )
            else:
                logger.debug(
                    "OPC DA ingest: accepted=%s dropped=%s",
                    accepted,
                    dropped,
                )

            self._send_json(200, {
                "accepted": accepted,
                "unmapped": len(unmapped),
                "dropped": dropped,
                "errors": errors[:10] if errors else [],  # 限制错误数量
            })

        except ValueError as exc:
            self._send_json(400, {"error": str(exc)})
        except Exception as exc:  # pragma: no cover
            logger.error("OPC DA ingest request failed: %s", exc)
            self._send_json(500, {"error": "internal server error"})

    def log_message(self, fmt, *args):  # noqa: A003 - BaseHTTPRequestHandler API
        """采样日志：避免高频请求刷屏"""
        # 使用服务器级别的计数器实现采样
        if not hasattr(self.server, '_log_counter'):
            self.server._log_counter = 0
            self.server._log_last_time = time.time()
            self.server._log_last_fmt = ""

        self.server._log_counter += 1
        current_time = time.time()

        # 每隔 LOG_SAMPLE_INTERVAL_SECONDS 秒输出一次汇总
        if current_time - self.server._log_last_time >= LOG_SAMPLE_INTERVAL_SECONDS:
            count = self.server._log_counter
            self.server._log_counter = 0
            self.server._log_last_time = current_time
            logger.info(
                "IngestAPI %s - %d requests in last %ds",
                self.address_string(),
                count,
                LOG_SAMPLE_INTERVAL_SECONDS,
            )


class InterfaceIngestServer:
    """外部接口采集协议服务包装器"""

    def __init__(
        self,
        host: str = "0.0.0.0",
        port: int = 8090,
        ingest_path: str = "/ingest",
        api_key: Optional[str] = None,
        max_batch_size: int = 1000,
        opcda_ingest_path: str = "/ingest/opcda-tags",
        max_body_bytes: int = _IngestHTTPServer.DEFAULT_MAX_BODY_BYTES,
    ):
        self.host = host
        self.port = port
        self.ingest_path = ingest_path
        self.api_key = api_key
        self.max_batch_size = max_batch_size
        self.opcda_ingest_path = opcda_ingest_path
        self.max_body_bytes = max_body_bytes
        self._server: Optional[_IngestHTTPServer] = None
        self._thread: Optional[threading.Thread] = None

    async def start(
        self,
        data_buffer: DataBuffer,
        event_loop: asyncio.AbstractEventLoop,
        opcda_mapper: Optional["OpcDaTagMapper"] = None,
    ) -> None:
        """启动接口服务（后台线程）"""
        if self._server is not None:
            return

        self._server = _IngestHTTPServer(
            (self.host, self.port),
            _IngestRequestHandler,
            data_buffer=data_buffer,
            event_loop=event_loop,
            ingest_path=self.ingest_path,
            api_key=self.api_key,
            max_batch_size=self.max_batch_size,
            opcda_mapper=opcda_mapper,
            opcda_ingest_path=self.opcda_ingest_path,
            max_body_bytes=self.max_body_bytes,
        )

        self._thread = threading.Thread(
            target=self._server.serve_forever,
            name="interface-ingest-server",
            daemon=True,
        )
        self._thread.start()
        if not self.api_key and self.host in {"0.0.0.0", "::"}:
            logger.warning(
                "Interface ingest is bound to %s without API key; keep this only for local/testing environments.",
                self.host,
            )

        endpoints = [self.ingest_path]
        if opcda_mapper:
            endpoints.append(self.opcda_ingest_path)
        logger.info(
            "Interface ingest server started at http://%s:%s (endpoints: %s)",
            self.host,
            self.port,
            ", ".join(endpoints),
        )

    async def stop(self) -> None:
        """停止接口服务"""
        if self._server is None:
            return

        self._server.shutdown()
        self._server.server_close()
        if self._thread is not None:
            self._thread.join(timeout=5)

        logger.info("Interface ingest server stopped")
        self._server = None
        self._thread = None
