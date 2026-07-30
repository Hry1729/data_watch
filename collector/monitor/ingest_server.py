"""外部接口采集协议服务（HTTP Push）"""

import json
import threading
import time
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any, Dict, List, Optional, Tuple, TYPE_CHECKING
from urllib.parse import urlparse

from core.logger import get_logger
from core.opcda_quality import map_opcda_quality
from core.quality_codes import (
    QUALITY_GOOD,
    QUALITY_UNCERTAIN,
    is_valid_quality,
)
from data_queue.types import DataPoint

if TYPE_CHECKING:
    from core.opcda_mapper import OpcDaTagMapper
    from device.manager import DeviceManager
    from storage.redis_writer import RedisWriter

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


def _timestamp_to_millis(timestamp: Any) -> int:
    if isinstance(timestamp, datetime):
        return int(timestamp.timestamp() * 1000)
    if isinstance(timestamp, (int, float)):
        ts = float(timestamp)
        return int(ts if ts > 1_000_000_000_000 else ts * 1000)
    return int(time.time() * 1000)


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


def _parse_quality_code(raw_value: Any) -> int:
    """解析平台质量码；未提供时默认为 Good。以 is_valid_quality 为唯一校验。"""
    if raw_value is None:
        return QUALITY_GOOD
    # 拒绝 bool/浮点后再规范化（避免 True→1、0.9→0）
    if isinstance(raw_value, bool) or isinstance(raw_value, float):
        raise ValueError("quality must be an integer code")
    if type(raw_value) is int:
        quality = raw_value
    else:
        try:
            quality = int(raw_value)
        except (TypeError, ValueError) as exc:
            raise ValueError("quality must be an integer code") from exc
    if not is_valid_quality(quality):
        raise ValueError("quality must be a valid platform quality code")
    return quality


def _map_opcda_quality(item: Dict[str, Any]) -> int:
    """将 OPC DA quality/quality_good 映射为内部质量码。"""
    quality_good = item.get("quality_good")
    if isinstance(quality_good, bool):
        return QUALITY_GOOD if quality_good else QUALITY_UNCERTAIN

    raw_quality = item.get("quality")
    if raw_quality is None:
        return QUALITY_GOOD
    try:
        opc_quality = int(raw_quality)
    except (TypeError, ValueError) as exc:
        raise ValueError("quality must be an integer code") from exc
    return map_opcda_quality(opc_quality)


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
    quality = _parse_quality_code(item.get("quality"))

    return DataPoint(
        device_id=device_id,
        variable_id=variable_id,
        value=value,
        timestamp=timestamp,
        quality=quality,
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
        ingest_path: str,
        api_key: Optional[str],
        max_batch_size: int,
        opcda_mapper: Optional["OpcDaTagMapper"] = None,
        opcda_ingest_path: str = "/ingest/opcda-tags",
        max_body_bytes: int = DEFAULT_MAX_BODY_BYTES,
        redis_writer: Optional["RedisWriter"] = None,
        realtime_config: Optional[Any] = None,
        device_manager: Optional["DeviceManager"] = None,
    ):
        super().__init__(server_address, request_handler)
        self.ingest_path = ingest_path
        self.api_key = api_key
        self.max_batch_size = max_batch_size
        self.opcda_mapper = opcda_mapper
        self.opcda_ingest_path = opcda_ingest_path
        self.max_body_bytes = max_body_bytes
        self.redis_writer = redis_writer
        self.device_manager = device_manager
        # 元数据快照在 asyncio 主线程（热重载）写、在 HTTP 工作线程读，
        # 用锁保证读取时拿到 device_meta / variable_meta 一致的一对。
        self._metadata_lock = threading.Lock()
        self._device_meta: Dict[str, Any] = {}
        self._variable_meta: Dict[str, Any] = {}
        self._device_generations: Dict[str, int] = {}
        self.update_metadata(
            getattr(realtime_config, "devices", []),
            getattr(realtime_config, "variables", []),
        )
        self._log_counter = 0
        self._log_last_time = time.time()
        self._log_lock = threading.Lock()

    def update_metadata(self, devices, variables) -> None:
        """重建设备/变量元数据快照（热重载后由主线程调用）。

        先构造完整的新 dict，再在锁内一次性替换，避免 HTTP 工作线程
        看到「新 device_meta + 旧 variable_meta」的混合状态。
        """
        new_device_meta = {
            getattr(device, "device_id", ""): device
            for device in (devices or [])
        }
        new_variable_meta = {
            str(getattr(variable, "variable_id", "")): variable
            for variable in (variables or [])
        }
        new_device_generations: Dict[str, int] = {}
        if self.device_manager is not None:
            for device_id in new_device_meta:
                generation = self.device_manager.current_generation(device_id)
                if generation is not None:
                    new_device_generations[device_id] = generation
        with self._metadata_lock:
            self._device_meta = new_device_meta
            self._variable_meta = new_variable_meta
            self._device_generations = new_device_generations

    def write_points_to_redis(self, points: List[DataPoint]) -> Tuple[int, List[str]]:
        """写入 Redis latest/window/stream，返回 accepted 和错误列表。"""
        if self.redis_writer is None:
            return 0, ["redis writer not configured"]

        # 在锁内抓取一致快照，处理过程使用局部引用，避免读到半更新状态
        with self._metadata_lock:
            variable_meta = self._variable_meta
            device_meta = self._device_meta
            device_generations = self._device_generations

        by_latest: Dict[Tuple[str, str, int, Optional[int]], Dict[str, Dict[str, Any]]] = {}
        by_frame: Dict[Tuple[str, str, str, int, int, Optional[int]], Dict[str, Any]] = {}
        accepted = 0
        errors: List[str] = []

        for idx, point in enumerate(points):
            var = variable_meta.get(str(point.variable_id))
            if var is None:
                errors.append(f"points[{idx}]: variable_id not configured: {point.variable_id}")
                continue

            var_device_id = getattr(var, "device_id", "") or ""
            # C9：变量已配置且归属设备明确时，客户端传入的 device_id 必须与之一致，
            # 否则会静默误路由（数据被写到变量真正归属的设备）。仅在两边都非空且不一致
            # 时拒绝；point.device_id 为空时（如 OPC DA 端点）仍按变量归属回退。
            if var_device_id and point.device_id and var_device_id != point.device_id:
                errors.append(
                    f"points[{idx}]: device_id mismatch: client sent {point.device_id} "
                    f"but variable {point.variable_id} belongs to {var_device_id}"
                )
                continue
            device_id = var_device_id or point.device_id
            device = device_meta.get(device_id)
            if device is None:
                errors.append(f"points[{idx}]: device not configured: {device_id}")
                continue

            group_code = getattr(var, "group_code", "g001")
            scan_rate_ms = max(1, int(float(getattr(var, "scan_rate", 1.0)) * 1000))
            ts_ms = _timestamp_to_millis(point.timestamp)
            device_code = getattr(device, "device_id", device_id)
            device_type = str(getattr(device, "device_type", "")).lower()
            generation = device_generations.get(device_code)
            if self.device_manager is not None and generation is None:
                errors.append(f"points[{idx}]: device is inactive: {device_code}")
                continue
            # C2：stream/frame 的 config_version 必须与 writer 对账列同源。writer
            # （mysql_mapper）用 device_point_group.config_version，故这里用变量的
            # group_config_version（与 executor 路径 035a532 一致），不用 device 表的
            # variable_config_version——否则两列不恒等时 writer 版本错配 → 刷屏告警/DLQ。
            config_version = int(getattr(var, "group_config_version", 1))
            value_text = str(point.value)
            quality = int(getattr(point, "quality", 0))

            # latest 聚合键必须包含 scan_rate_ms：同一 (device, group) 不同 scan_rate 的
            # 帧各自独立落库，latest 字段也要与 frame 对齐，否则任一帧失败时其它帧的
            # 变量会先于失败帧进 latest，客户端据此误判已落库。
            by_latest.setdefault(
                (device_code, group_code, scan_rate_ms, generation), {}
            )[str(point.variable_id)] = {
                "v": value_text,
                "t": ts_ms,
                "q": quality,
            }

            frame_key = (
                device_code,
                device_type,
                group_code,
                scan_rate_ms,
                config_version,
                generation,
            )
            frame = by_frame.setdefault(
                frame_key,
                {"ts": ts_ms, "values": {}, "stream_values": {}, "stream_qualities": {}},
            )
            frame["ts"] = max(frame["ts"], ts_ms)
            frame["values"][str(point.variable_id)] = value_text
            if getattr(var, "is_storage", True) and str(getattr(var, "data_type", "")).upper() != "STRING":
                frame["stream_values"][str(point.variable_id)] = value_text
                frame["stream_qualities"][str(point.variable_id)] = quality

            accepted += 1

        for (
            device_code,
            device_type,
            group_code,
            scan_rate_ms,
            config_version,
            generation,
        ), frame_data in by_frame.items():
            frame = {
                "device_code": device_code,
                "group_code": group_code,
                "ts": frame_data["ts"],
                "scan_rate": scan_rate_ms,
                "values": frame_data["values"],
            }
            frame_label = f"{device_code}/{group_code}/{scan_rate_ms}ms"

            def write_frame() -> None:
                # 写顺序：window -> stream -> latest。latest 必须最后写，
                # 否则 stream/window 失败时 latest 已被刷新，客户端据此误判批次已落库。
                try:
                    self.redis_writer.write_window(device_code, group_code, scan_rate_ms, frame)
                except Exception as exc:
                    errors.append(f"redis write_window failed for {frame_label}: {exc}")
                    logger.error("redis write_window failed for %s: %s", frame_label, exc)
                    return

                if frame_data["stream_values"]:
                    try:
                        self.redis_writer.write_stream(
                            device_code,
                            device_type,
                            group_code,
                            scan_rate_ms,
                            frame_data["ts"],
                            config_version,
                            frame_data["stream_values"],
                            frame_data["stream_qualities"],
                        )
                    except Exception as exc:
                        errors.append(f"redis write_stream failed for {frame_label}: {exc}")
                        logger.error("redis write_stream failed for %s: %s", frame_label, exc)
                        return

                latest_fields = by_latest.get(
                    (device_code, group_code, scan_rate_ms, generation)
                )
                if latest_fields is None:
                    return
                try:
                    self.redis_writer.write_latest(device_code, group_code, latest_fields)
                except Exception as exc:
                    errors.append(f"redis write_latest failed for {frame_label}: {exc}")
                    logger.error("redis write_latest failed for %s: %s", frame_label, exc)

            if self.device_manager is None:
                write_frame()
            elif generation is None or not self.device_manager.run_if_current_generation(
                device_code, generation, write_frame
            ):
                errors.append(f"stale generation rejected for {frame_label}")

        return accepted, errors


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

            accepted, write_errors = self.server.write_points_to_redis(points)
            errors.extend(write_errors)
            dropped = 0

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
                    quality = _map_opcda_quality(item)
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
                    quality=quality,
                ))

            if not points:
                self._send_json(200, {
                    "accepted": 0,
                    "unmapped": len(unmapped),
                    "errors": errors,
                })
                return

            accepted, write_errors = self.server.write_points_to_redis(points)
            errors.extend(write_errors)
            dropped = 0

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
        with self.server._log_lock:
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
        self.redis_writer: Optional["RedisWriter"] = None
        self.realtime_config: Optional[Any] = None
        self._server: Optional[_IngestHTTPServer] = None
        self._thread: Optional[threading.Thread] = None

    async def start(
        self,
        opcda_mapper: Optional["OpcDaTagMapper"] = None,
        redis_writer: Optional["RedisWriter"] = None,
        realtime_config: Optional[Any] = None,
        device_manager: Optional["DeviceManager"] = None,
    ) -> None:
        """启动接口服务（后台线程）"""
        if self._server is not None:
            return

        self.redis_writer = redis_writer
        self.realtime_config = realtime_config

        self._server = _IngestHTTPServer(
            (self.host, self.port),
            _IngestRequestHandler,
            ingest_path=self.ingest_path,
            api_key=self.api_key,
            max_batch_size=self.max_batch_size,
            opcda_mapper=opcda_mapper,
            opcda_ingest_path=self.opcda_ingest_path,
            max_body_bytes=self.max_body_bytes,
            redis_writer=redis_writer,
            realtime_config=realtime_config,
            device_manager=device_manager,
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

    def update_metadata(self, realtime_config: Any) -> None:
        """热重载后刷新 HTTP server 持有的设备/变量元数据快照。"""
        self.realtime_config = realtime_config
        if self._server is None or realtime_config is None:
            return
        self._server.update_metadata(
            getattr(realtime_config, "devices", []),
            getattr(realtime_config, "variables", []),
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
