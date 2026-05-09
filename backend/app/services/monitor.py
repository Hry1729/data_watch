from __future__ import annotations

import asyncio
from collections import OrderedDict
from datetime import datetime
import re
from typing import Any

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import ApiError
from app.db.tdengine import TDengineClient


ONLINE_MAX_AGE_SECONDS = 120
UNSTABLE_MAX_AGE_SECONDS = 900
DEVICE_PREFIX_RE = re.compile(r"^[A-Za-z]+")
IDENTIFIER_SANITIZE_RE = re.compile(r"[^A-Za-z0-9_]+")


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

    async def get_realtime_init(self, device_id: int | None) -> dict[str, Any]:
        device = await self._fetch_target_device(device_id)
        if device is None:
            raise ApiError(404, "Device not found")

        device_id_value = int(device["device_id"])
        device_code = str(device["device_code"])
        points = await self._fetch_device_points(device_id_value)
        latest_point_map = await self._fetch_latest_point_rows(device_code, points)

        last_collect_time = self._resolve_last_collect_time(latest_point_map)
        connect_status = self._build_connect_status(int(device["device_status"] or 0), last_collect_time)

        data: list[dict[str, Any]] = []
        for point in points:
            var_seq = int(point["var_seq"])
            latest_point = latest_point_map.get(str(var_seq))
            data.append(
                {
                    "varSeq": var_seq,
                    "pointName": point["point_name"],
                    "ioAddress": point["io_address"],
                    "value": self._normalize_point_value(point["var_type"], latest_point["value"] if latest_point else None),
                    "updateTime": self._format_datetime(latest_point["ts"]) if latest_point else "",
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
            "data": data,
        }

    async def get_realtime_snapshots(self, device_ids: list[int]) -> list[dict[str, Any]]:
        snapshots: list[dict[str, Any]] = []
        seen: set[int] = set()
        for device_id in device_ids:
            if device_id in seen:
                continue
            seen.add(device_id)
            try:
                snapshots.append(await self.get_realtime_init(device_id))
            except ApiError as exc:
                if exc.code == 404:
                    continue
                raise
        return snapshots

    async def _fetch_target_device(self, device_id: int | None) -> dict[str, Any] | None:
        if device_id is None:
            stmt = text(
                """
                SELECT
                    d.id AS device_id,
                    d.device_code AS device_code,
                    COALESCE(d.device_name, d.device_code) AS device_name,
                    COALESCE(cp.protocol, '') AS driver_name,
                    d.status AS device_status
                FROM device AS d
                LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
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
                d.status AS device_status
            FROM device AS d
            LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
            WHERE d.id = :device_id
            LIMIT 1
            """
        )
        return (await self.session.execute(stmt, {"device_id": device_id})).mappings().first()

    async def _fetch_device_points(self, device_id: int) -> list[dict[str, Any]]:
        stmt = text(
            """
            SELECT
                dv.id AS point_id,
                COALESCE(dv.var_name, CAST(dv.var_seq AS CHAR), '') AS point_name,
                dv.var_address AS io_address,
                dv.var_type AS var_type,
                dv.var_seq AS var_seq
            FROM device_variable AS dv
            WHERE dv.device_id = :device_id
              AND dv.var_seq IS NOT NULL
              AND dv.var_seq > 0
            ORDER BY dv.var_seq ASC
            """
        )
        return [dict(row) for row in (await self.session.execute(stmt, {"device_id": device_id})).mappings().all()]

    async def _fetch_latest_collect_times(self, device_codes: list[str]) -> dict[str, datetime]:
        tasks = [self._fetch_device_latest_collect_time(device_code) for device_code in device_codes]
        results = await asyncio.gather(*tasks)
        return {
            device_code: latest_time
            for device_code, latest_time in zip(device_codes, results, strict=False)
            if latest_time is not None
        }

    async def _fetch_device_latest_collect_time(self, device_code: str) -> datetime | None:
        try:
            database_name = self._resolve_tdengine_database(device_code)
            stable_name = self._normalize_identifier(device_code)
            table_name = self.tdengine.qualify_table(database_name, stable_name)
            rows = await self.tdengine.fetch_all(
                f"SELECT LAST(ts) AS last_ts FROM {table_name}",
                database=database_name,
            )
        except Exception:
            return None

        if not rows:
            return None
        return self._coerce_datetime(rows[0][0])

    async def _fetch_latest_point_rows(
        self,
        device_code: str,
        points: list[dict[str, Any]],
    ) -> dict[str, dict[str, Any]]:
        if not points:
            return {}

        tasks = [self._fetch_single_point_latest_row(device_code, point) for point in points]
        results = await asyncio.gather(*tasks)
        return {
            str(point["var_seq"]): result
            for point, result in zip(points, results, strict=False)
            if result is not None
        }

    async def _fetch_single_point_latest_row(
        self,
        device_code: str,
        point: dict[str, Any],
    ) -> dict[str, Any] | None:
        database_name = self._resolve_tdengine_database(device_code)
        stable_name = self._normalize_identifier(device_code)
        table_name = self.tdengine.qualify_table(database_name, stable_name)
        variable_id = int(point["point_id"])
        var_seq = int(point["var_seq"]) if point.get("var_seq") is not None else None

        rows = []
        if var_seq is not None:
            try:
                rows = await self.tdengine.fetch_all(
                    (
                        f"SELECT ts, `value` FROM {table_name} "
                        f"WHERE var_seq = {var_seq} ORDER BY ts DESC LIMIT 1"
                    ),
                    database=database_name,
                )
            except Exception:
                rows = []

        if not rows:
            try:
                subtable_name = self._resolve_point_subtable_name(
                    device_code=device_code,
                    var_seq=var_seq,
                    point_id=variable_id,
                )
                subtable_name = self.tdengine.qualify_table(database_name, subtable_name)
                rows = await self.tdengine.fetch_all(
                    f"SELECT ts, `value` FROM {subtable_name} ORDER BY ts DESC LIMIT 1",
                    database=database_name,
                )
            except Exception:
                rows = []

        if not rows:
            try:
                legacy_subtable_name = self._resolve_point_subtable_name(
                    device_code=device_code,
                    var_seq=None,
                    point_id=variable_id,
                )
                legacy_subtable_name = self.tdengine.qualify_table(database_name, legacy_subtable_name)
                rows = await self.tdengine.fetch_all(
                    f"SELECT ts, `value` FROM {legacy_subtable_name} ORDER BY ts DESC LIMIT 1",
                    database=database_name,
                )
            except Exception:
                rows = []

        if not rows:
            try:
                rows = await self.tdengine.fetch_all(
                    (
                        f"SELECT ts, `value` FROM {table_name} "
                        f"WHERE variable_id = {variable_id} ORDER BY ts DESC LIMIT 1"
                    ),
                    database=database_name,
                )
            except Exception:
                return None

        if not rows:
            return None

        ts, value = rows[0]
        return {
            "ts": self._coerce_datetime(ts),
            "value": value,
        }

    def _build_connect_status(self, _device_status: int, last_collect_time: datetime | None) -> str:
        if last_collect_time is None:
            return "offline"

        age_seconds = max(0.0, (datetime.now() - last_collect_time).total_seconds())
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
            if value.tzinfo is not None:
                return value.astimezone().replace(tzinfo=None)
            return value
        if isinstance(value, str):
            normalized = value.replace("Z", "+00:00")
            try:
                parsed = datetime.fromisoformat(normalized)
            except ValueError:
                return None
            if parsed.tzinfo is not None:
                return parsed.astimezone().replace(tzinfo=None)
            return parsed
        return None

    def _format_datetime(self, value: datetime | None) -> str:
        return value.strftime("%Y-%m-%d %H:%M:%S") if value else ""

    def _normalize_point_value(self, var_type: str | None, value: Any) -> str | int | float | bool:
        if value is None:
            return "-"

        normalized_type = (var_type or "").upper()
        if normalized_type == "BOOL":
            if isinstance(value, str):
                return value.strip().lower() in {"1", "true", "yes", "on"}
            return bool(int(float(value)))

        if normalized_type in {"INT", "DINT", "WORD", "DWORD"}:
            return int(float(value))

        if normalized_type == "REAL":
            return float(value)

        if isinstance(value, float) and value.is_integer():
            return int(value)
        return value

    def _resolve_tdengine_database(self, device_code: str) -> str:
        normalized_device_code = self._normalize_identifier(device_code).lower()
        match = DEVICE_PREFIX_RE.match(normalized_device_code)
        if match is None:
            raise ValueError(f"Unable to resolve TDengine database from device code: {device_code}")
        return match.group(0)

    def _resolve_point_subtable_name(self, device_code: str, var_seq: int | None, point_id: Any) -> str:
        normalized_device_code = self._normalize_identifier(device_code).lower()
        suffix = int(var_seq) if var_seq not in (None, 0) else int(point_id)
        return f"{normalized_device_code}_{suffix}"

    def _normalize_identifier(self, value: Any) -> str:
        normalized = IDENTIFIER_SANITIZE_RE.sub("_", str(value or "").strip())
        if not normalized:
            raise ValueError("TDengine identifier cannot be empty")
        return normalized
