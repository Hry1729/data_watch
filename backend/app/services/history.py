from __future__ import annotations

import asyncio
import csv
from dataclasses import dataclass
from datetime import datetime, timedelta
import io
import re
from typing import Any

from sqlalchemy import bindparam, text
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import Settings, get_settings
from app.core.exceptions import ApiError
from app.db.tdengine import TDengineClient
from app.services.perspective import get_perspective_session_manager


DEVICE_PREFIX_RE = re.compile(r"^[A-Za-z]+")
IDENTIFIER_SANITIZE_RE = re.compile(r"[^A-Za-z0-9_]+")


@dataclass(frozen=True)
class HistoryDevice:
    device_id: int
    device_code: str
    device_name: str
    device_type_code: str | None


@dataclass(frozen=True)
class HistoryPoint:
    point_id: int
    point_name: str
    io_address: str
    var_type: str | None
    var_seq: int


class HistoryService:
    def __init__(
        self,
        session: AsyncSession,
        tdengine: TDengineClient,
        settings: Settings | None = None,
    ):
        self.session = session
        self.tdengine = tdengine
        self.settings = settings or get_settings()

    async def get_devices(self) -> list[dict[str, Any]]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name
                    FROM device AS d
                    ORDER BY d.id ASC
                    """
                )
            )
        ).mappings().all()

        return [
            {
                "id": int(row["device_id"]),
                "name": row["device_name"],
                "deviceCode": row["device_code"],
                "deviceName": row["device_name"],
            }
            for row in rows
        ]

    async def get_points(self, device_id: int) -> list[dict[str, Any]]:
        if device_id <= 0:
            raise ApiError(400, "deviceId must be a positive integer")

        await self._fetch_device(device_id)
        points = await self._fetch_points(device_id)
        return [
            {
                "varSeq": point.var_seq,
                "pointName": point.point_name,
                "ioAddress": point.io_address,
            }
            for point in points
        ]

    async def query_history(
        self,
        device_id: int,
        var_seqs: list[int],
        start_time: str,
        end_time: str,
        perspective_session_id: str | None = None,
        perspective_query_version: int = 0,
        authorization: str | None = None,
    ) -> dict[str, Any]:
        device = await self._fetch_device(device_id)
        points = await self._fetch_selected_points(device_id, var_seqs)
        start_dt, end_dt = self._validate_query_range(start_time, end_time)
        self._validate_query_points(points, var_seqs)

        series_list = await asyncio.gather(
            *(self._fetch_point_series(device, point, start_dt, end_dt) for point in points)
        )

        timeline: dict[datetime, str] = {}
        point_value_maps: dict[int, dict[str, str | int | float]] = {}
        perspective_records: list[dict[str, Any]] = []
        total_record_count = 0

        for point, rows in zip(points, series_list, strict=False):
            value_map: dict[str, str | int | float] = {}
            for ts, value in rows:
                ts_value = self._coerce_datetime(ts)
                if ts_value is None:
                    continue

                total_record_count += 1
                if total_record_count > self.settings.perspective_max_records_per_query:
                    raise ApiError(
                        400,
                        (
                            "History query result exceeds the session protection limit "
                            f"({self.settings.perspective_max_records_per_query} records)"
                        ),
                    )

                normalized_time = self._format_timestamp(ts_value)
                normalized_value = self._normalize_point_value(point.var_type, value)
                timeline[ts_value] = normalized_time
                value_map[normalized_time] = normalized_value
                perspective_records.append(
                    {
                        "timestamp": normalized_time,
                        "deviceId": device.device_id,
                        "deviceCode": device.device_code,
                        "varSeq": point.var_seq,
                        "pointName": point.point_name,
                        "ioAddress": point.io_address,
                        "value": normalized_value,
                    }
                )

            point_value_maps[point.var_seq] = value_map

        times = [item[1] for item in sorted(timeline.items(), key=lambda item: item[0])]
        point_payload = [
            {
                "varSeq": point.var_seq,
                "pointName": point.point_name,
                "values": [point_value_maps[point.var_seq].get(timestamp, "-") for timestamp in times],
            }
            for point in points
        ]

        if perspective_session_id:
            await self._store_perspective_snapshot(
                perspective_session_id=perspective_session_id,
                query_version=perspective_query_version,
                authorization=authorization,
                records=perspective_records,
            )

        return {
            "times": times,
            "points": point_payload,
        }

    async def export_history_csv(
        self,
        device_id: int,
        var_seqs: list[int],
        start_time: str,
        end_time: str,
    ) -> bytes:
        data = await self.query_history(
            device_id=device_id,
            var_seqs=var_seqs,
            start_time=start_time,
            end_time=end_time,
        )

        output = io.StringIO()
        writer = csv.writer(output)
        writer.writerow(["time", *[point["pointName"] for point in data["points"]]])

        for index, time_value in enumerate(data["times"]):
            row = [time_value]
            for point in data["points"]:
                row.append(point["values"][index])
            writer.writerow(row)

        return output.getvalue().encode("utf-8-sig")

    async def _fetch_device(self, device_id: int) -> HistoryDevice:
        if device_id <= 0:
            raise ApiError(400, "deviceId must be a positive integer")

        row = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name,
                        dt.type_code AS device_type_code
                    FROM device AS d
                    LEFT JOIN device_type AS dt ON dt.id = d.type_id
                    WHERE d.id = :device_id
                    LIMIT 1
                    """
                ),
                {"device_id": device_id},
            )
        ).mappings().first()

        if row is None:
            raise ApiError(404, "Device not found")

        return HistoryDevice(
            device_id=int(row["device_id"]),
            device_code=str(row["device_code"]),
            device_name=str(row["device_name"]),
            device_type_code=str(row["device_type_code"]) if row["device_type_code"] else None,
        )

    async def _fetch_points(self, device_id: int) -> list[HistoryPoint]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        dv.id AS point_id,
                        COALESCE(dv.var_name, CAST(dv.var_seq AS CHAR), '') AS point_name,
                        COALESCE(dv.var_address, '') AS io_address,
                        dv.var_type AS var_type,
                        dv.var_seq AS var_seq
                    FROM device_variable AS dv
                    WHERE dv.device_id = :device_id
                      AND dv.var_seq IS NOT NULL
                      AND dv.var_seq > 0
                    ORDER BY dv.var_seq ASC
                    """
                ),
                {"device_id": device_id},
            )
        ).mappings().all()

        return [
            HistoryPoint(
                point_id=int(row["point_id"]),
                point_name=str(row["point_name"]),
                io_address=str(row["io_address"]),
                var_type=str(row["var_type"]) if row["var_type"] else None,
                var_seq=int(row["var_seq"]),
            )
            for row in rows
        ]

    async def _fetch_selected_points(self, device_id: int, var_seqs: list[int]) -> list[HistoryPoint]:
        sanitized_var_seqs = self._sanitize_var_seqs(var_seqs)
        if not sanitized_var_seqs:
            raise ApiError(400, "varSeqs is required")
        if len(sanitized_var_seqs) > self.settings.perspective_max_points_per_query:
            raise ApiError(
                400,
                (
                    f"varSeqs exceeds the limit of "
                    f"{self.settings.perspective_max_points_per_query} points"
                ),
            )

        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        dv.id AS point_id,
                        COALESCE(dv.var_name, CAST(dv.var_seq AS CHAR), '') AS point_name,
                        COALESCE(dv.var_address, '') AS io_address,
                        dv.var_type AS var_type,
                        dv.var_seq AS var_seq
                    FROM device_variable AS dv
                    WHERE dv.device_id = :device_id
                      AND dv.var_seq IN :var_seqs
                    ORDER BY dv.var_seq ASC
                    """
                ).bindparams(bindparam("var_seqs", expanding=True)),
                {"device_id": device_id, "var_seqs": sanitized_var_seqs},
            )
        ).mappings().all()

        return [
            HistoryPoint(
                point_id=int(row["point_id"]),
                point_name=str(row["point_name"]),
                io_address=str(row["io_address"]),
                var_type=str(row["var_type"]) if row["var_type"] else None,
                var_seq=int(row["var_seq"]),
            )
            for row in rows
        ]

    def _sanitize_var_seqs(self, var_seqs: list[int]) -> list[int]:
        unique_var_seqs: list[int] = []
        seen: set[int] = set()
        for var_seq in var_seqs:
            if var_seq <= 0 or var_seq in seen:
                continue
            unique_var_seqs.append(var_seq)
            seen.add(var_seq)
        return unique_var_seqs

    def _validate_query_points(self, points: list[HistoryPoint], selected_var_seqs: list[int]) -> None:
        requested_var_seqs = self._sanitize_var_seqs(selected_var_seqs)
        existing_var_seqs = {point.var_seq for point in points}
        missing_var_seqs = [var_seq for var_seq in requested_var_seqs if var_seq not in existing_var_seqs]
        if missing_var_seqs:
            raise ApiError(404, f"varSeq not found for device: {missing_var_seqs}")

    def _validate_query_range(self, start_time: str, end_time: str) -> tuple[datetime, datetime]:
        start_dt = self._parse_datetime(start_time, "startTime")
        end_dt = self._parse_datetime(end_time, "endTime")
        if start_dt >= end_dt:
            raise ApiError(400, "startTime must be earlier than endTime")

        max_range = timedelta(hours=self.settings.perspective_max_range_hours)
        if end_dt - start_dt > max_range:
            raise ApiError(
                400,
                f"time range exceeds the limit of {self.settings.perspective_max_range_hours} hours",
            )

        return start_dt, end_dt

    def _parse_datetime(self, value: str, field_name: str) -> datetime:
        raw = str(value or "").strip()
        if not raw:
            raise ApiError(400, f"{field_name} is required")

        candidates = [raw]
        if "T" in raw:
            candidates.append(raw.replace("T", " "))
        normalized = raw.replace("Z", "+00:00")
        candidates.append(normalized)
        if "T" in normalized:
            candidates.append(normalized.replace("T", " "))

        for candidate in candidates:
            try:
                parsed = datetime.fromisoformat(candidate)
            except ValueError:
                continue
            if parsed.tzinfo is not None:
                return parsed.astimezone().replace(tzinfo=None)
            return parsed

        raise ApiError(400, f"{field_name} must be a valid datetime string")

    async def _fetch_point_series(
        self,
        device: HistoryDevice,
        point: HistoryPoint,
        start_dt: datetime,
        end_dt: datetime,
    ) -> list[tuple[Any, Any]]:
        database_name = self._resolve_tdengine_database(device)
        start_sql = self._format_sql_timestamp(start_dt)
        end_sql = self._format_sql_timestamp(end_dt)
        primary_subtable_name = self._resolve_point_subtable_name(
            device_code=device.device_code,
            var_seq=point.var_seq,
            point_id=point.point_id,
        )
        fallback_subtable_name = (
            f"{self._normalize_identifier(device.device_code).lower()}_{point.point_id}"
        )

        candidate_table_names = [primary_subtable_name]
        if fallback_subtable_name not in candidate_table_names:
            candidate_table_names.append(fallback_subtable_name)

        for candidate_table_name in candidate_table_names:
            table_name = self.tdengine.qualify_table(database_name, candidate_table_name)
            try:
                rows = await self.tdengine.fetch_all(
                    (
                        f"SELECT ts, `value` FROM {table_name} "
                        f"WHERE ts >= '{start_sql}' AND ts <= '{end_sql}' "
                        f"ORDER BY ts ASC"
                    ),
                    database=database_name,
                )
            except Exception:
                continue

            if rows:
                return rows

        stable_name = self.tdengine.qualify_table(
            database_name,
            self._normalize_identifier(device.device_code).lower(),
        )
        stable_statements: list[str] = []
        if point.var_seq is not None:
            stable_statements.append(
                (
                    f"SELECT ts, `value` FROM {stable_name} "
                    f"WHERE var_seq = {int(point.var_seq)} "
                    f"AND ts >= '{start_sql}' AND ts <= '{end_sql}' "
                    f"ORDER BY ts ASC"
                )
            )
        stable_statements.append(
            (
                f"SELECT ts, `value` FROM {stable_name} "
                f"WHERE variable_id = {point.point_id} "
                f"AND ts >= '{start_sql}' AND ts <= '{end_sql}' "
                f"ORDER BY ts ASC"
            )
        )
        for statement in stable_statements:
            try:
                rows = await self.tdengine.fetch_all(statement, database=database_name)
            except Exception:
                continue
            if rows:
                return rows
        return []

    async def _store_perspective_snapshot(
        self,
        perspective_session_id: str,
        query_version: int,
        authorization: str | None,
        records: list[dict[str, Any]],
    ) -> None:
        manager = get_perspective_session_manager()
        owner_key = manager.build_owner_key(authorization)
        manager.ensure_session(owner_key, perspective_session_id)
        stored = manager.store_snapshot(
            owner_key=owner_key,
            session_id=perspective_session_id,
            query_version=max(0, int(query_version)),
            records=records,
        )
        if stored:
            await manager.publish_snapshot(owner_key=owner_key, session_id=perspective_session_id)

    def _resolve_tdengine_database(self, device: HistoryDevice) -> str:
        if device.device_type_code:
            return self._normalize_identifier(device.device_type_code).lower()

        match = DEVICE_PREFIX_RE.match(self._normalize_identifier(device.device_code).lower())
        if match is None:
            raise ApiError(400, f"Unable to resolve TDengine database for {device.device_code}")
        return match.group(0)

    def _resolve_point_subtable_name(self, device_code: str, var_seq: int | None, point_id: int) -> str:
        normalized_device_code = self._normalize_identifier(device_code).lower()
        suffix = int(var_seq) if var_seq not in (None, 0) else int(point_id)
        return f"{normalized_device_code}_{suffix}"

    def _normalize_identifier(self, value: Any) -> str:
        normalized = IDENTIFIER_SANITIZE_RE.sub("_", str(value or "").strip())
        if not normalized:
            raise ApiError(400, "TDengine identifier cannot be empty")
        return normalized

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

    def _format_timestamp(self, value: datetime) -> str:
        return value.strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]

    def _format_sql_timestamp(self, value: datetime) -> str:
        return value.strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]

    def _normalize_point_value(self, var_type: str | None, value: Any) -> str | int | float:
        if value is None:
            return "-"

        normalized_type = (var_type or "").upper()
        if normalized_type == "BOOL":
            if isinstance(value, str):
                normalized_value = value.strip().lower()
                if normalized_value in {"1", "true", "yes", "on"}:
                    return 1
                if normalized_value in {"0", "false", "no", "off"}:
                    return 0
                try:
                    numeric_value = float(value)
                except ValueError:
                    return value
            else:
                numeric_value = float(value)

            if numeric_value in {0.0, 1.0}:
                return int(numeric_value)
            return int(numeric_value) if numeric_value.is_integer() else numeric_value

        if normalized_type in {"INT", "DINT", "WORD", "DWORD"}:
            return int(float(value))

        if normalized_type == "REAL":
            return float(value)

        if isinstance(value, float) and value.is_integer():
            return int(value)
        return value
