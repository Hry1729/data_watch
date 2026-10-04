from __future__ import annotations

from collections import OrderedDict
from datetime import datetime
import json
import logging
from typing import Any

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import ApiError
from app.core.status_thresholds import ONLINE_MAX_AGE_SECONDS, UNSTABLE_MAX_AGE_SECONDS
from app.core.time_utils import LOCAL_TZ, now_local, to_local_naive
from app.db.redis import get_redis_client
from app.db.tdengine import TDengineClient

logger = logging.getLogger("backend.monitor")


class MonitorService:
    def __init__(self, session: AsyncSession, tdengine: TDengineClient):
        self.session = session
        self.tdengine = tdengine

    async def get_device_tree(self) -> list[dict[str, Any]]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        dg.id AS group_id,
                        dg.group_name AS group_name,
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name,
                        d.status AS device_status
                    FROM device_group AS dg
                    LEFT JOIN device AS d ON d.group_id = dg.id
                    ORDER BY dg.id ASC, d.id ASC
                    """
                )
            )
        ).mappings().all()

        if not rows:
            return []

        groups: OrderedDict[int, dict[str, Any]] = OrderedDict()
        for row in rows:
            group_id = int(row["group_id"])
            group_node = groups.setdefault(
                group_id,
                {
                    "key": f"group-{group_id}",
                    "name": row["group_name"] or f"Group {group_id}",
                    "nodeType": "group",
                    "children": [],
                },
            )

            if row["device_id"] is None:
                continue

            device_code = str(row["device_code"])
            group_node["children"].append(
                {
                    "key": f"device-{int(row['device_id'])}",
                    "name": row["device_name"] or device_code,
                    "nodeType": "device",
                    "status": self._build_connect_status_from_raw(int(row["device_status"] or 0)),
                    "deviceId": int(row["device_id"]),
                    "deviceCode": device_code,
                    "deviceName": row["device_name"] or device_code,
                }
            )

        return list(groups.values())

    async def get_realtime_init(self, device_id: int | None, group_code: str = "g001") -> dict[str, Any]:
        device = await self._fetch_target_device(device_id)
        if device is None:
            raise ApiError(404, "Device not found")

        device_id_value = int(device["device_id"])
        device_code = str(device["device_code"])
        points = await self._fetch_device_points(device_id_value, group_code)

        redis_map, redis_connected = await self._get_device_latest_from_redis(device_code, group_code)
        latest_point_map: dict[str, dict[str, Any]] = {}
        if redis_map:
            # redis_map keyed by var_id (str of point_id) -> {"v": ..., "t": ms_epoch, "q": ...}
            # Build latest_point_map keyed by str(var_seq) to match existing response logic
            point_id_to_seq = {str(p["point_id"]): int(p["var_seq"]) for p in points}
            for var_id, entry in redis_map.items():
                var_seq = point_id_to_seq.get(str(var_id))
                if var_seq is None:
                    continue
                ts_ms = entry.get("t")
                ts_dt = to_local_naive(datetime.fromtimestamp(ts_ms / 1000, tz=LOCAL_TZ)) if isinstance(ts_ms, (int, float)) else None
                latest_point_map[str(var_seq)] = {
                    "ts": ts_dt,
                    "value": entry.get("v"),
                    "quality": entry.get("q"),
                }

        last_collect_time = self._resolve_last_collect_time(latest_point_map)
        connect_status = self._build_connect_status(int(device["device_status"] or 0), last_collect_time)

        data: list[dict[str, Any]] = []
        for point in points:
            var_seq = int(point["var_seq"])
            latest_point = latest_point_map.get(str(var_seq))
            data.append(
                {
                    "pointId": int(point["point_id"]),
                    "varSeq": var_seq,
                    "pointName": point["point_name"],
                    "ioAddress": point["io_address"],
                    "value": self._normalize_point_value(point["var_type"], latest_point["value"] if latest_point else None),
                    "updateTime": self._format_datetime(latest_point["ts"]) if latest_point else "",
                    "quality": latest_point.get("quality") if latest_point else None,
                    "dataType": point["var_type"],
                }
            )

        return {
            "deviceId": device_id_value,
            "deviceCode": device_code,
            "deviceName": device["device_name"] or device_code,
            "driverName": device["driver_name"] or "",
            "connectStatus": connect_status,
            "lastCollectTime": self._format_datetime(last_collect_time),
            "redisConnected": redis_connected,
            "degraded": not redis_connected,
            "data": data,
        }

    async def _get_device_latest_from_redis(self, device_code: str, group_code: str = "g001") -> tuple[dict[str, Any], bool]:
        """Read latest hash from Redis.

        Returns ({}, True) when the key doesn't exist, and ({}, False) when Redis is unavailable.
        """
        try:
            r = await get_redis_client()
            key = f"rt:latest:{device_code}:{group_code}"
            raw = await r.hgetall(key)
            result: dict[str, Any] = {}
            for var_id, json_str in raw.items():
                try:
                    result[var_id] = json.loads(json_str)
                except Exception:
                    pass
            return result, True
        except Exception as exc:
            # 区分“无数据”与“Redis 故障”：故障不能静默吞掉，否则会被前端当成设备离线且无从排障。
            logger.warning(
                "Redis 读取设备最新值失败 device=%s group=%s: %s",
                device_code, group_code, exc,
            )
            return {}, False

    async def get_realtime_window(
        self,
        device_id: int | None = None,
        device_code: str | None = None,
        group_code: str = "g001",
        scan_rate_ms: int | None = None,
        limit: int = 10,
    ) -> dict[str, Any]:
        device = await self._fetch_target_device(device_id, device_code=device_code)
        if device is None:
            raise ApiError(404, "Device not found")

        device_id_value = int(device["device_id"])
        device_code = str(device["device_code"])

        if scan_rate_ms is None:
            scan_rate_ms = await self._fetch_min_scan_rate(device_id_value, group_code)

        redis_connected = True
        key = f"rt:window:{device_code}:{group_code}:{scan_rate_ms}"
        try:
            r = await get_redis_client()
            raw_frames = await r.lrange(key, 0, limit - 1)
        except Exception as exc:
            logger.warning(
                "Redis 读取实时窗口失败 device=%s group=%s scan_rate_ms=%s: %s",
                device_code, group_code, scan_rate_ms, exc,
            )
            raw_frames = []
            redis_connected = False

        frames: list[dict[str, Any]] = []
        for raw in raw_frames:
            try:
                frames.append(json.loads(raw))
            except Exception:
                pass

        return {
            "deviceId": device_id_value,
            "deviceCode": device_code,
            "groupCode": group_code,
            "scanRateMs": scan_rate_ms,
            "redisConnected": redis_connected,
            "degraded": not redis_connected,
            "frames": frames,
        }

    async def _fetch_min_scan_rate(self, device_id: int, group_code: str) -> int:
        row = (
            await self.session.execute(
                text(
                    """
                    SELECT MIN(dv.scan_rate) AS min_scan_rate
                    FROM device_variable AS dv
                    JOIN device_point_group AS dpg ON dpg.id = dv.point_group_id
                    WHERE dv.device_id = :device_id
                      AND dpg.group_code = :group_code
                      AND dv.deleted = 0
                      AND dv.enable = 1
                    """
                ),
                {"device_id": device_id, "group_code": group_code},
            )
        ).mappings().first()
        if row is None or row["min_scan_rate"] is None:
            return 1000
        return int(row["min_scan_rate"])

    async def _fetch_target_device(self, device_id: int | None, device_code: str | None = None) -> dict[str, Any] | None:
        if device_code:
            stmt = text(
                """
                SELECT
                    d.id AS device_id,
                    d.device_code AS device_code,
                    COALESCE(d.device_name, d.device_code) AS device_name,
                    COALESCE(cp.protocol, '') AS driver_name,
                    d.status AS device_status,
                    COALESCE(dt.type_code, '') AS device_type_code
                FROM device AS d
                LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
                LEFT JOIN device_type AS dt ON dt.id = d.type_id
                WHERE d.device_code = :device_code
                LIMIT 1
                """
            )
            return (await self.session.execute(stmt, {"device_code": device_code})).mappings().first()

        if device_id is None:
            stmt = text(
                """
                SELECT
                    d.id AS device_id,
                    d.device_code AS device_code,
                    COALESCE(d.device_name, d.device_code) AS device_name,
                    COALESCE(cp.protocol, '') AS driver_name,
                    d.status AS device_status,
                    COALESCE(dt.type_code, '') AS device_type_code
                FROM device AS d
                LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
                LEFT JOIN device_type AS dt ON dt.id = d.type_id
                ORDER BY d.status DESC, d.id ASC
                LIMIT 1
                """
            )
            return (await self.session.execute(stmt)).mappings().first()

        stmt = text(
            """
            SELECT
                d.id AS device_id,
                d.device_code AS device_code,
                COALESCE(d.device_name, d.device_code) AS device_name,
                COALESCE(cp.protocol, '') AS driver_name,
                d.status AS device_status,
                COALESCE(dt.type_code, '') AS device_type_code
            FROM device AS d
            LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
            LEFT JOIN device_type AS dt ON dt.id = d.type_id
            WHERE d.id = :device_id
            LIMIT 1
            """
        )
        return (await self.session.execute(stmt, {"device_id": device_id})).mappings().first()

    async def _fetch_device_points(self, device_id: int, group_code: str) -> list[dict[str, Any]]:
        stmt = text(
            """
            SELECT
                dv.id AS point_id,
                COALESCE(dv.var_name, CAST(dv.var_seq AS CHAR), '') AS point_name,
                dv.var_address AS io_address,
                dv.var_type AS var_type,
                dv.var_seq AS var_seq,
                dv.group_index AS group_index
            FROM device_variable AS dv
            JOIN device_point_group AS dpg ON dpg.id = dv.point_group_id
            WHERE dv.device_id = :device_id
              AND dpg.group_code = :group_code
              AND dv.deleted = 0
              AND dv.enable = 1
              AND dv.var_seq IS NOT NULL
              AND dv.var_seq > 0
            ORDER BY dv.group_index ASC, dv.var_seq ASC
            """
        )
        return [
            dict(row)
            for row in (
                await self.session.execute(stmt, {"device_id": device_id, "group_code": group_code})
            ).mappings().all()
        ]

    async def validate_realtime_target(self, device_code: str, group_code: str) -> bool:
        """Return whether a device/group has a known, enabled point set."""
        normalized_device = str(device_code or "").strip()
        normalized_group = str(group_code or "").strip()
        if not normalized_device or not normalized_group:
            return False
        if len(normalized_device) > 128 or len(normalized_group) > 64:
            return False
        device = await self._fetch_target_device(None, device_code=normalized_device)
        if device is None:
            return False
        points = await self._fetch_device_points(int(device["device_id"]), normalized_group)
        return bool(points)

    def _build_connect_status(self, _device_status: int, last_collect_time: datetime | None) -> str:
        if last_collect_time is None:
            return "offline"

        age_seconds = max(0.0, (now_local() - last_collect_time).total_seconds())
        if age_seconds <= ONLINE_MAX_AGE_SECONDS:
            return "online"
        if age_seconds <= UNSTABLE_MAX_AGE_SECONDS:
            return "unstable"
        return "offline"

    def _build_connect_status_from_raw(self, raw_status: int) -> str:
        if raw_status == 1:
            return "online"
        if raw_status == 2:
            return "unstable"
        return "offline"

    def _resolve_last_collect_time(self, latest_point_map: dict[str, dict[str, Any]]) -> datetime | None:
        timestamps = [item["ts"] for item in latest_point_map.values() if item.get("ts") is not None]
        return max(timestamps) if timestamps else None

    def _coerce_datetime(self, value: Any) -> datetime | None:
        if value is None:
            return None
        if isinstance(value, datetime):
            return to_local_naive(value) if value.tzinfo is not None else value
        if isinstance(value, str):
            normalized = value.replace("Z", "+00:00")
            try:
                parsed = datetime.fromisoformat(normalized)
            except ValueError:
                return None
            return to_local_naive(parsed) if parsed.tzinfo is not None else parsed
        return None

    def _format_datetime(self, value: datetime | None) -> str:
        return value.strftime("%Y-%m-%d %H:%M:%S") if value else ""

    _EMPTY_POINT_VALUE = "-"

    @staticmethod
    def _coerce_point_number(value: Any) -> float | None:
        """B17：把 latest 里的原始值安全归一为**有限数字**；无法确定时返回 None。

        空串/空白、非数字文本、`None`、`inf`/`nan`、以及损坏载荷（dict/list 等）一律返回 None，
        由调用方转成"明确无值"。**刻意不补 0**——把坏点变成 0 会掩盖异常（清单硬要求）；
        同时避免一处坏值让 realtime-init **整页 500**。
        """
        if isinstance(value, bool):
            return float(value)
        if isinstance(value, (int, float)):
            number = float(value)
        elif isinstance(value, (str, bytes, bytearray)):
            text = (
                value.decode("utf-8", "replace")
                if isinstance(value, (bytes, bytearray))
                else value
            ).strip()
            if not text:
                return None
            try:
                number = float(text)
            except (TypeError, ValueError):
                return None
        else:
            return None
        if number != number or number in (float("inf"), float("-inf")):  # NaN / ±Inf
            return None
        return number

    def _normalize_point_value(self, var_type: str | None, value: Any) -> str | int | float | bool:
        if value is None:
            return self._EMPTY_POINT_VALUE

        normalized_type = (var_type or "").upper()
        if normalized_type == "BOOL":
            if isinstance(value, str):
                text = value.strip().lower()
                if not text:
                    return self._EMPTY_POINT_VALUE
                if text in {"1", "true", "yes", "on"}:
                    return True
                if text in {"0", "false", "no", "off"}:
                    return False
            number = self._coerce_point_number(value)
            return self._EMPTY_POINT_VALUE if number is None else bool(number)

        if normalized_type in {"INT", "DINT", "WORD", "DWORD"}:
            number = self._coerce_point_number(value)
            return self._EMPTY_POINT_VALUE if number is None else int(number)

        if normalized_type == "REAL":
            number = self._coerce_point_number(value)
            return self._EMPTY_POINT_VALUE if number is None else number

        if isinstance(value, float) and value.is_integer():
            return int(value)
        return value
