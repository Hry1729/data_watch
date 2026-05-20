from __future__ import annotations

import asyncio
import csv
import io
import os
import re
from dataclasses import dataclass
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any

from sqlalchemy import bindparam, text
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import Settings, get_settings
from app.core.exceptions import ApiError
from app.core.time_utils import LOCAL_TZ
from app.db.tdengine import TDengineClient
from app.services.perspective import get_perspective_session_manager
from app.services.task_manager import get_export_task_manager


DEVICE_PREFIX_RE = re.compile(r"^[A-Za-z]+")
IDENTIFIER_SANITIZE_RE = re.compile(r"[^A-Za-z0-9_]+")


def get_repo_root() -> Path:
    return Path(__file__).resolve().parents[3]


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
    unit: str | None
    td_column_name: str | None = None
    group_code: str | None = None


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
        """获取设备列表（带类型和分组信息）"""
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name,
                        dg.id AS group_id,
                        dg.group_name AS group_name,
                        COALESCE(dt.type_code, '') AS type_code,
                        COALESCE(dt.type_name, dt.type_code, '其他') AS type_name
                    FROM device AS d
                    LEFT JOIN device_group AS dg ON dg.id = d.group_id
                    LEFT JOIN device_type AS dt ON dt.id = d.type_id
                    ORDER BY dt.type_code ASC, d.id ASC
                    """
                )
            )
        ).mappings().all()

        return [
            {
                "id": int(row["device_id"]) if row["device_id"] else None,
                "name": row["device_name"] or "",
                "deviceCode": row["device_code"] or "",
                "deviceName": row["device_name"] or "",
                "groupId": int(row["group_id"]) if row["group_id"] else None,
                "groupName": row["group_name"] or "",
                "typeCode": row["type_code"] or "",
                "typeName": row["type_name"] or "其他",
            }
            for row in rows
        ]

    async def get_device_types(self) -> list[dict[str, Any]]:
        """获取设备类型列表（用于级联选择）"""
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT DISTINCT
                        COALESCE(dt.type_code, '') AS type_code,
                        COALESCE(dt.type_name, dt.type_code, '其他') AS type_name,
                        COUNT(d.id) AS device_count
                    FROM device AS d
                    LEFT JOIN device_type AS dt ON dt.id = d.type_id
                    GROUP BY dt.type_code, dt.type_name
                    ORDER BY device_count DESC, type_code ASC
                    """
                )
            )
        ).mappings().all()

        return [
            {
                "typeCode": row["type_code"] or "",
                "typeName": row["type_name"] or "其他",
                "deviceCount": int(row["device_count"] or 0),
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
            *(self._fetch_point_series(device, point, start_dt, end_dt) for point in points),
            return_exceptions=True
        )

        timeline: dict[datetime, str] = {}
        point_value_maps: dict[int, dict[str, str | int | float]] = {}
        perspective_records: list[dict[str, Any]] = []
        total_record_count = 0

        for point, rows in zip(points, series_list, strict=False):
            if isinstance(rows, Exception):
                continue
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

    async def get_chart_data(
        self,
        device_id: int,
        var_seqs: list[int],
        start_time: str,
        end_time: str,
        interval: str = "auto",
        aggregate: str = "avg",
    ) -> dict[str, Any]:
        device = await self._fetch_device(device_id)
        points = await self._fetch_selected_points(device_id, var_seqs)
        start_dt, end_dt = self._validate_query_range(start_time, end_time)
        self._validate_query_points(points, var_seqs)

        # Validate interval
        ALLOWED_INTERVALS = {"auto", "100ms", "500ms", "1s", "5s", "30s", "1m", "5m", "30m", "1h"}
        if interval not in ALLOWED_INTERVALS:
            raise ApiError(400, f"Unsupported interval: {interval}. Allowed: {ALLOWED_INTERVALS}")

        if interval == "auto":
            calc_interval = self._calculate_auto_interval(start_dt, end_dt)
        else:
            calc_interval = interval

        series_results = []
        for point in points:
            # Force LAST aggregate for boolean types to avoid invalid interpolation like 0.5
            is_bool = str(point.var_type).lower() == "bool"
            current_aggregate = "last" if is_bool else aggregate
            agg_func = self._map_aggregate_function(current_aggregate)
            
            rows = await self._fetch_downsampled_series(
                device, point, start_dt, end_dt, calc_interval, agg_func
            )
            series_results.append(rows)

        series_payload = []
        for point, rows in zip(points, series_results):
            data_points = []
            for ts, value in rows:
                ts_dt = self._coerce_datetime(ts)
                if ts_dt:
                    normalized_value = self._normalize_point_value(point.var_type, value)
                    # For chart data, we prefer actual null over "-" if possible, 
                    # but _normalize_point_value returns "-" for None.
                    # We'll convert it back to None if it's "-" to let frontend handle gaps.
                    if normalized_value == "-":
                        normalized_value = None
                    data_points.append([self._format_timestamp(ts_dt), normalized_value])

            series_payload.append(
                {
                    "varSeq": point.var_seq,
                    "name": point.point_name,
                    "dataType": str(point.var_type),
                    "unit": point.unit or "",
                    "data": data_points,
                }
            )

        return {
            "interval": calc_interval,
            "series": series_payload,
        }

    async def get_raw_data(
        self,
        device_id: int,
        var_seq: int,
        start_time: str,
        end_time: str,
        page_size: int = 1000,
        cursor_ts: str | None = None,
    ) -> dict[str, Any]:
        """获取原始点位数据，支持分页"""
        device = await self._fetch_device(device_id)
        # 验证点位是否存在
        points = await self._fetch_selected_points(device_id, [var_seq])
        self._validate_query_points(points, [var_seq])
        point = points[0]
        
        start_dt, end_dt = self._validate_query_range(start_time, end_time)
        
        database_name = self._resolve_tdengine_database(device)
        end_sql = self._format_sql_timestamp(end_dt)

        # 构造时间过滤条件：如果有游标则使用游标，否则使用开始时间
        if cursor_ts and cursor_ts.strip():
            # Validate and format cursor_ts to prevent SQL injection
            cursor_dt = self._parse_datetime(cursor_ts.strip(), "cursorTs")
            safe_cursor = self._format_sql_timestamp(cursor_dt)
            time_filter = f"ts > {safe_cursor}"
        else:
            start_sql = self._format_sql_timestamp(start_dt)
            time_filter = f"ts >= {start_sql}"

        rows = []

        # v0.6: try child table with td_column_name
        if point.td_column_name and point.group_code:
            child_table = self.tdengine.qualify_table(
                database_name,
                self._resolve_child_table_name(device.device_code, point.group_code),
            )
            col = self.tdengine.quote_identifier(point.td_column_name)
            try:
                rows = await self.tdengine.fetch_all(
                    f"SELECT ts, {col} FROM {child_table} "
                    f"WHERE {time_filter} AND ts <= {end_sql} "
                    f"ORDER BY ts ASC LIMIT {page_size}",
                    database=database_name,
                )
            except Exception:
                rows = []

        if not rows:
            subtable_name = self._resolve_point_subtable_name(
                device_code=device.device_code,
                var_seq=point.var_seq,
                point_id=point.point_id,
            )
            table_name = self.tdengine.qualify_table(database_name, subtable_name)
            try:
                # 优先查询子表
                rows = await self.tdengine.fetch_all(
                    f"SELECT ts, `value` FROM {table_name} "
                    f"WHERE {time_filter} AND ts <= {end_sql} "
                    f"ORDER BY ts ASC LIMIT {page_size}",
                    database=database_name,
                )
            except Exception:
                rows = []

        if not rows:
            # 失败则回退到超级表查询
            stable_name = self.tdengine.qualify_table(
                database_name,
                self._normalize_identifier(device.device_code).lower(),
            )
            try:
                rows = await self.tdengine.fetch_all(
                    f"SELECT ts, `value` FROM {stable_name} "
                    f"WHERE var_seq = {point.var_seq} "
                    f"AND {time_filter} AND ts <= {end_sql} "
                    f"ORDER BY ts ASC LIMIT {page_size}",
                    database=database_name,
                )
            except Exception:
                rows = []

        results = []
        last_ts_str = None
        for ts, value in rows:
            ts_dt = self._coerce_datetime(ts)
            if ts_dt:
                formatted_ts = self._format_timestamp(ts_dt)
                results.append({
                    "ts": formatted_ts,
                    "value": self._normalize_point_value(point.var_type, value),
                    "quality": 1
                })
                last_ts_str = formatted_ts

        # 如果返回的数据量等于 page_size，说明可能还有下一页
        next_cursor = None
        if len(rows) >= page_size and last_ts_str:
            next_cursor = last_ts_str

        return {
            "list": results,
            "nextCursor": next_cursor
        }

    def _align_to_bucket(self, ts: datetime, granularity: str) -> datetime:
        bucket_ms_map = {"100ms": 100, "500ms": 500, "1s": 1000}
        bucket_ms = bucket_ms_map[granularity]

        total_ms = (
            ts.hour * 3600000
            + ts.minute * 60000
            + ts.second * 1000
            + ts.microsecond // 1000
        )
        aligned_ms = (total_ms // bucket_ms) * bucket_ms

        return ts.replace(
            hour=aligned_ms // 3600000,
            minute=(aligned_ms % 3600000) // 60000,
            second=(aligned_ms % 60000) // 1000,
            microsecond=(aligned_ms % 1000) * 1000,
        )

    def _build_raw_matrix_columns(self, points: list[HistoryPoint]) -> list[dict[str, Any]]:
        return [
            {"prop": "ts", "label": "采样时间", "fixed": "left"},
            *[
                {
                    "prop": f"varSeq_{point.var_seq}",
                    "label": point.point_name,
                    "varSeq": point.var_seq,
                    "unit": point.unit or "",
                    "dataType": point.var_type or "",
                }
                for point in points
            ],
        ]

    def _filter_bucket_rows(self, rows: list) -> dict[datetime, Any]:
        result: dict[datetime, Any] = {}
        for ts, value in rows:
            ts_dt = self._coerce_datetime(ts)
            if ts_dt is not None:
                result[ts_dt] = value
        return result

    async def _fetch_point_bucket_data(
        self,
        device: HistoryDevice,
        point: HistoryPoint,
        start_dt: datetime,
        end_dt: datetime,
        granularity: str,
        limit: int,
    ) -> dict[datetime, Any]:
        start_sql = self._format_sql_timestamp(start_dt)
        end_sql = self._format_sql_timestamp(end_dt)
        td_interval = self._to_tdengine_interval(granularity)

        var_type = (point.var_type or "").upper()
        agg = "AVG" if var_type == "REAL" else "LAST"

        database_name = self._resolve_tdengine_database(device)

        # v0.6: try child table with td_column_name
        if point.td_column_name and point.group_code:
            child_table = self.tdengine.qualify_table(
                database_name,
                self._resolve_child_table_name(device.device_code, point.group_code),
            )
            col = self.tdengine.quote_identifier(point.td_column_name)
            query = (
                f"SELECT _wstart AS ts, {agg}({col}) AS `value` "
                f"FROM {child_table} "
                f"WHERE ts >= {start_sql} AND ts <= {end_sql} "
                f"INTERVAL({td_interval}) "
                f"ORDER BY ts ASC LIMIT {limit}"
            )
            try:
                rows = await self.tdengine.fetch_all(query, database=database_name)
                if rows:
                    return self._filter_bucket_rows(rows)
            except Exception:
                pass

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
            query = (
                f"SELECT _wstart AS ts, {agg}(`value`) AS `value` "
                f"FROM {table_name} "
                f"WHERE ts >= {start_sql} AND ts <= {end_sql} "
                f"INTERVAL({td_interval}) "
                f"ORDER BY ts ASC LIMIT {limit}"
            )
            try:
                rows = await self.tdengine.fetch_all(query, database=database_name)
            except Exception:
                continue
            if rows:
                return self._filter_bucket_rows(rows)

        stable_name = self.tdengine.qualify_table(
            database_name,
            self._normalize_identifier(device.device_code).lower(),
        )

        stable_statements: list[str] = []
        if point.var_seq is not None:
            stable_statements.append(
                f"SELECT _wstart AS ts, {agg}(`value`) AS `value` "
                f"FROM {stable_name} "
                f"WHERE var_seq = {int(point.var_seq)} "
                f"AND ts >= {start_sql} AND ts <= {end_sql} "
                f"INTERVAL({td_interval}) "
                f"ORDER BY ts ASC LIMIT {limit}"
            )
        stable_statements.append(
            f"SELECT _wstart AS ts, {agg}(`value`) AS `value` "
            f"FROM {stable_name} "
            f"WHERE variable_id = {int(point.point_id)} "
            f"AND ts >= {start_sql} AND ts <= {end_sql} "
            f"INTERVAL({td_interval}) "
            f"ORDER BY ts ASC LIMIT {limit}"
        )

        for statement in stable_statements:
            try:
                rows = await self.tdengine.fetch_all(statement, database=database_name)
            except Exception:
                continue
            if rows:
                return self._filter_bucket_rows(rows)

        return {}

    async def get_raw_matrix_data(
        self,
        device_id: int,
        var_seqs: list[int],
        start_time: str,
        end_time: str,
        align_granularity: str = "1s",
        page_size: int = 200,
        cursor_ts: str | None = None,
    ) -> dict[str, Any]:
        device = await self._fetch_device(device_id)
        points = await self._fetch_selected_points(device_id, var_seqs)
        start_dt, end_dt = self._validate_query_range(start_time, end_time)
        self._validate_query_points(points, var_seqs)

        if end_dt - start_dt > timedelta(hours=24):
            raise ApiError(400, "raw-matrix time range cannot exceed 24 hours")

        columns = self._build_raw_matrix_columns(points)
        bucket_ms = {"100ms": 100, "500ms": 500, "1s": 1000}[align_granularity]

        if cursor_ts:
            cursor_dt = self._parse_datetime(cursor_ts, "cursorTs")
            aligned_cursor = self._align_to_bucket(cursor_dt, align_granularity)
            start_dt = aligned_cursor + timedelta(milliseconds=bucket_ms)
            if start_dt >= end_dt:
                return {
                    "columns": columns,
                    "rows": [],
                    "nextCursor": None,
                    "meta": {
                        "alignGranularity": align_granularity,
                        "fillMode": "none",
                        "pageSize": page_size,
                    },
                }
        else:
            start_dt = self._align_to_bucket(start_dt, align_granularity)

        limit = page_size + 1
        point_maps = await asyncio.gather(
            *(
                self._fetch_point_bucket_data(
                    device,
                    point,
                    start_dt,
                    end_dt,
                    align_granularity,
                    limit,
                )
                for point in points
            ),
            return_exceptions=True
        )

        point_data_maps = {
            point.var_seq: point_map
            for point, point_map in zip(points, point_maps, strict=False)
            if isinstance(point_map, dict)
        }

        all_timestamps: set[datetime] = set()
        for point_map in point_maps:
            if isinstance(point_map, dict):
                all_timestamps.update(point_map.keys())

        sorted_times = sorted(all_timestamps)
        page_times = sorted_times[:page_size]
        has_more = len(all_timestamps) > page_size or any(
            isinstance(point_map, dict) and len(point_map) >= limit for point_map in point_maps
        )
        next_cursor = self._format_timestamp(page_times[-1]) if has_more and page_times else None

        rows: list[dict[str, Any]] = []
        for ts in page_times:
            row: dict[str, Any] = {"ts": self._format_timestamp(ts)}
            for point in points:
                row[f"varSeq_{point.var_seq}"] = point_data_maps.get(point.var_seq, {}).get(ts)
            rows.append(row)

        return {
            "columns": columns,
            "rows": rows,
            "nextCursor": next_cursor,
            "meta": {
                "alignGranularity": align_granularity,
                "fillMode": "none",
                "pageSize": page_size,
            },
        }

    def _calculate_auto_interval(self, start_dt: datetime, end_dt: datetime, target_points: int = 2000) -> str:
        total_seconds = (end_dt - start_dt).total_seconds()
        if total_seconds <= 0:
            return "5s"
        interval_sec = max(1, int(total_seconds / target_points))
        # 对于短时间范围，使用更合理的间隔
        if interval_sec < 1:
            return "500ms"
        if interval_sec < 5:
            return "5s"
        if interval_sec < 30:
            return "30s"
        if interval_sec < 60:
            return "1m"
        if interval_sec < 300:
            return "5m"
        if interval_sec < 1800:
            return "30m"
        if interval_sec < 3600:
            return "30m"
        return "1h"

    def _map_aggregate_function(self, aggregate: str) -> str:
        mapping = {
            "avg": "AVG",
            "max": "MAX",
            "min": "MIN",
            "last": "LAST",
            "first": "FIRST",
            "sum": "SUM",
            "count": "COUNT",
        }
        return mapping.get(aggregate.lower(), "AVG")

    async def _fetch_downsampled_series(
        self,
        device: HistoryDevice,
        point: HistoryPoint,
        start_dt: datetime,
        end_dt: datetime,
        interval: str,
        agg_func: str,
    ) -> list[tuple[Any, Any]]:
        database_name = self._resolve_tdengine_database(device)
        start_sql = self._format_sql_timestamp(start_dt)
        end_sql = self._format_sql_timestamp(end_dt)
        td_interval = self._to_tdengine_interval(interval)

        # v0.6: try child table with td_column_name
        if point.td_column_name and point.group_code:
            child_table = self.tdengine.qualify_table(
                database_name,
                self._resolve_child_table_name(device.device_code, point.group_code),
            )
            col = self.tdengine.quote_identifier(point.td_column_name)
            query = (
                f"SELECT _wstart AS ts, {agg_func}({col}) AS `value` "
                f"FROM {child_table} "
                f"WHERE ts >= {start_sql} AND ts <= {end_sql} "
                f"INTERVAL({td_interval}) "
                f"ORDER BY ts ASC"
            )
            try:
                rows = await self.tdengine.fetch_all(query, database=database_name)
                if rows:
                    return rows
            except Exception:
                pass

        # Legacy fallback
        subtable_name = self._resolve_point_subtable_name(
            device_code=device.device_code,
            var_seq=point.var_seq,
            point_id=point.point_id,
        )
        table_name = self.tdengine.qualify_table(database_name, subtable_name)

        query = (
            f"SELECT _wstart AS ts, {agg_func}(`value`) AS `value` "
            f"FROM {table_name} "
            f"WHERE ts >= {start_sql} AND ts <= {end_sql} "
            f"INTERVAL({td_interval}) "
            f"ORDER BY ts ASC"
        )

        try:
            rows = await self.tdengine.fetch_all(query, database=database_name)
            if rows:
                return rows
        except Exception:
            pass

        stable_name = self.tdengine.qualify_table(
            database_name,
            self._normalize_identifier(device.device_code).lower(),
        )
        query = (
            f"SELECT _wstart AS ts, {agg_func}(`value`) AS `value` "
            f"FROM {stable_name} "
            f"WHERE var_seq = {point.var_seq} "
            f"AND ts >= {start_sql} AND ts <= {end_sql} "
            f"INTERVAL({td_interval}) "
            f"ORDER BY ts ASC"
        )

        try:
            rows = await self.tdengine.fetch_all(query, database=database_name)
            return rows
        except Exception:
            return []

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

    async def create_export_task(
        self,
        device_id: int,
        var_seqs: list[int],
        start_time: str,
        end_time: str,
    ) -> str:
        """创建异步导出任务"""
        # 预先验证设备和点位
        device = await self._fetch_device(device_id)
        points = await self._fetch_selected_points(device_id, var_seqs)
        start_dt, end_dt = self._validate_query_range(start_time, end_time)
        self._validate_query_points(points, var_seqs)

        task_manager = get_export_task_manager()
        task_id = task_manager.create_task(
            metadata={
                "device_id": device_id,
                "var_seqs": var_seqs,
                "start_time": start_time,
                "end_time": end_time,
                "device_name": device.device_name,
            }
        )

        # 启动后台任务
        # 注意：这里需要确保后台任务能获取到所需的资源（如数据库连接）
        # 既然 HistoryService 是有状态的（持有 session），我们可能需要一种方式来重建或传递它
        # 或者在后台任务中重新初始化 Service
        asyncio.create_task(
            self._process_export_task(
                task_id=task_id,
                device=device,
                points=points,
                start_dt=start_dt,
                end_dt=end_dt,
            )
        )

        return task_id

    async def _process_export_task(
        self,
        task_id: str,
        device: HistoryDevice,
        points: list[HistoryPoint],
        start_dt: datetime,
        end_dt: datetime,
    ):
        task_manager = get_export_task_manager()
        export_dir = get_repo_root() / "data" / "exports"
        export_dir.mkdir(parents=True, exist_ok=True)
        
        file_path = export_dir / f"{task_id}.csv"
        
        try:
            # 分块查询，每块 1 小时
            chunk_delta = timedelta(hours=1)
            total_duration = end_dt - start_dt
            
            with open(file_path, "w", encoding="utf-8-sig", newline="") as f:
                writer = csv.writer(f)
                # 写入表头
                writer.writerow(["时间", *[p.point_name for p in points]])
                
                current_start = start_dt
                processed_duration = timedelta(0)
                
                while current_start < end_dt:
                    current_end = min(current_start + chunk_delta, end_dt)
                    
                    # 获取该时段内所有点位的数据
                    series_list = await asyncio.gather(
                        *(self._fetch_point_series(device, point, current_start, current_end) for point in points),
                        return_exceptions=True
                    )

                    # 对齐数据
                    timeline: dict[datetime, dict[int, Any]] = {}
                    for point, rows in zip(points, series_list):
                        if isinstance(rows, Exception):
                            continue
                        for ts, value in rows:
                            ts_dt = self._coerce_datetime(ts)
                            if not ts_dt:
                                continue
                            if ts_dt not in timeline:
                                timeline[ts_dt] = {}
                            timeline[ts_dt][point.var_seq] = self._normalize_point_value(point.var_type, value)
                    
                    # 排序并写入
                    sorted_times = sorted(timeline.keys())
                    for ts in sorted_times:
                        row = [self._format_timestamp(ts)]
                        for point in points:
                            row.append(timeline[ts].get(point.var_seq, "-"))
                        writer.writerow(row)
                    
                    # 更新进度
                    processed_duration += (current_end - current_start)
                    progress = int((processed_duration.total_seconds() / total_duration.total_seconds()) * 100)
                    task_manager.update_task(task_id, progress=min(99, progress))
                    
                    current_start = current_end
            
            task_manager.update_task(
                task_id, 
                status="completed", 
                progress=100, 
                file_path=str(file_path)
            )
            
        except Exception as e:
            task_manager.update_task(task_id, status="failed", error=str(e))
            if file_path.exists():
                try:
                    file_path.unlink()
                except:
                    pass

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
                        dv.var_seq AS var_seq,
                        ud.unit_symbol AS unit,
                        dv.td_column_name AS td_column_name,
                        dpg.group_code AS group_code
                    FROM device_variable AS dv
                    LEFT JOIN unit_dict AS ud ON ud.id = dv.unit_id
                    LEFT JOIN device_point_group AS dpg ON dpg.id = dv.point_group_id
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
                point_name=row["point_name"],
                io_address=row["io_address"],
                var_type=row["var_type"],
                var_seq=int(row["var_seq"]),
                unit=row["unit"],
                td_column_name=row["td_column_name"] or None,
                group_code=row["group_code"] or None,
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
                        dv.var_seq AS var_seq,
                        ud.unit_symbol AS unit,
                        dv.td_column_name AS td_column_name,
                        dpg.group_code AS group_code
                    FROM device_variable AS dv
                    LEFT JOIN unit_dict AS ud ON ud.id = dv.unit_id
                    LEFT JOIN device_point_group AS dpg ON dpg.id = dv.point_group_id
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
                point_name=row["point_name"],
                io_address=row["io_address"],
                var_type=row["var_type"],
                var_seq=int(row["var_seq"]),
                unit=row["unit"],
                td_column_name=row["td_column_name"] or None,
                group_code=row["group_code"] or None,
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

        # v0.6: try child table t_{device_code}_{group_code} with td_column_name column
        if point.td_column_name and point.group_code:
            child_table = self.tdengine.qualify_table(
                database_name,
                self._resolve_child_table_name(device.device_code, point.group_code),
            )
            col = self.tdengine.quote_identifier(point.td_column_name)
            try:
                rows = await self.tdengine.fetch_all(
                    (
                        f"SELECT ts, {col} FROM {child_table} "
                        f"WHERE ts >= {start_sql} AND ts <= {end_sql} "
                        f"ORDER BY ts ASC"
                    ),
                    database=database_name,
                )
                if rows:
                    return rows
            except Exception:
                pass

        # Legacy fallback: per-point subtables with `value` column
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
                        f"WHERE ts >= {start_sql} AND ts <= {end_sql} "
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
                    f"AND ts >= {start_sql} AND ts <= {end_sql} "
                    f"ORDER BY ts ASC"
                )
            )
        stable_statements.append(
            (
                f"SELECT ts, `value` FROM {stable_name} "
                f"WHERE variable_id = {point.point_id} "
                f"AND ts >= {start_sql} AND ts <= {end_sql} "
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

    def _resolve_child_table_name(self, device_code: str, group_code: str | None) -> str:
        """v0.6 child table: t_{device_code}_{group_code}"""
        normalized = self._normalize_identifier(device_code)
        gc = group_code or "g001"
        return f"t_{normalized}_{gc}"

    def _resolve_point_subtable_name(self, device_code: str, var_seq: int | None, point_id: int) -> str:
        """Legacy subtable naming (pre-v0.6): {device_code_lower}_{var_seq_or_point_id}"""
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

    def _to_tdengine_interval(self, interval: str) -> str:
        """将interval字符串转换为TDengine支持的格式

        TDengine 毫秒级 INTERVAL 使用 'a' 后缀：
        - 100ms -> 100a
        - 500ms -> 500a
        - 1s/5s/30s/1m/5m/30m/1h 保持原样
        """
        mapping = {
            "100ms": "100a",
            "500ms": "500a",
            "1s": "1s",
            "5s": "5s",
            "30s": "30s",
            "1m": "1m",
            "5m": "5m",
            "30m": "30m",
            "1h": "1h",
        }
        return mapping.get(interval, interval)

    def _format_sql_timestamp(self, value: datetime) -> str:
        """Format TDengine SQL timestamps as Unix milliseconds."""
        local_value = value if value.tzinfo is not None else value.replace(tzinfo=LOCAL_TZ)
        return str(int(local_value.timestamp() * 1000))

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
