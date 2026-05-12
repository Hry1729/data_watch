from __future__ import annotations

import asyncio
from collections import defaultdict
from dataclasses import dataclass
from datetime import datetime, timedelta
import re
from typing import Any

from sqlalchemy import bindparam, text
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import ApiError
from app.core.time_utils import format_local_datetime, now_local, parse_local_datetime
from app.db.tdengine import TDengineClient


DEVICE_PREFIX_RE = re.compile(r"^[A-Za-z]+")
IDENTIFIER_SANITIZE_RE = re.compile(r"[^A-Za-z0-9_]+")

ONLINE_MAX_AGE_SECONDS = 120
UNSTABLE_MAX_AGE_SECONDS = 900
SUPPORTED_RANGE_TYPES = {"last24h", "today", "last7d"}
SUPPORTED_GRANULARITIES = {"auto", "5m", "15m", "1h", "1d"}
SUPPORTED_LOG_LEVELS = {"INFO", "WARN", "ERROR"}
SUPPORTED_LOG_TYPES = {"system", "collect", "storage", "alarm"}
FACT_TABLE_NAMES = {
    "alarm_level",
    "alarm_rule",
    "alarm_record",
    "alarm_active",
    "operation_log",
    "system_log",
}


@dataclass(frozen=True)
class DashboardDeviceItem:
    device_id: int
    device_code: str
    device_name: str
    device_type: str
    device_type_code: str
    raw_status: int


@dataclass(frozen=True)
class DashboardTrendPoint:
    legacy_point_id: int
    device_id: int
    device_code: str
    device_type_code: str | None
    var_seq: int


@dataclass(frozen=True)
class SyntheticAlarmRecord:
    event_id: str
    device_id: int
    device_code: str
    device_name: str
    device_type_code: str
    alarm_type: str
    level_code: str
    level_name: str
    severity: int
    status: str
    content: str
    offset_minutes: int


class DashboardService:
    def __init__(self, session: AsyncSession, tdengine: TDengineClient):
        self.session = session
        self.tdengine = tdengine
        self._fact_tables: set[str] | None = None

    async def get_overview(self) -> dict[str, Any]:
        # 获取设备列表和最后采集时间，动态计算在线/风险/离线设备数
        devices = await self._fetch_dashboard_devices()
        latest_time_map = await self._fetch_latest_collect_times(devices)

        online_devices = 0
        risk_devices = 0
        offline_devices = 0
        for d in devices:
            status = self._build_dynamic_connect_status(d, latest_time_map.get(d.device_code))
            if status == "online":
                online_devices += 1
            elif status == "unstable":
                risk_devices += 1
            else:
                offline_devices += 1

        # 点位总数
        point_total = (
            await self.session.execute(text("SELECT COUNT(*) FROM device_variable"))
        ).scalar_one() or 0

        # 系统告警统计（从 system_log + operation_log 统计 WARN/ERROR/CRITICAL）
        now = now_local()
        current_start = now - timedelta(hours=24)
        previous_start = current_start - timedelta(hours=24)

        system_alert_total = 0
        system_alert_compare_prev = {"countDiff": 0, "ratioDiff": 0}

        try:
            # 近24小时系统告警数
            current_result = await self.session.execute(
                text(
                    """
                    SELECT COUNT(*) FROM (
                        SELECT 1 FROM system_log
                        WHERE level IN ('WARN', 'ERROR', 'CRITICAL')
                          AND log_time >= :current_start
                        UNION ALL
                        SELECT 1 FROM operation_log
                        WHERE level IN ('WARN', 'ERROR', 'CRITICAL')
                          AND log_time >= :current_start
                    ) AS combined
                    """
                ),
                {"current_start": current_start},
            )
            system_alert_total = int(current_result.scalar_one() or 0)

            # 上个24小时系统告警数
            previous_result = await self.session.execute(
                text(
                    """
                    SELECT COUNT(*) FROM (
                        SELECT 1 FROM system_log
                        WHERE level IN ('WARN', 'ERROR', 'CRITICAL')
                          AND log_time >= :prev_start AND log_time < :curr_start
                        UNION ALL
                        SELECT 1 FROM operation_log
                        WHERE level IN ('WARN', 'ERROR', 'CRITICAL')
                          AND log_time >= :prev_start AND log_time < :curr_start
                    ) AS combined
                    """
                ),
                {"prev_start": previous_start, "curr_start": current_start},
            )
            previous_count = int(previous_result.scalar_one() or 0)

            system_alert_compare_prev = {
                "countDiff": system_alert_total - previous_count,
                "ratioDiff": self._calculate_ratio_diff(system_alert_total, previous_count),
            }
        except Exception:
            pass

        return {
            "deviceTotal": len(devices),
            "onlineDeviceTotal": online_devices,
            "offlineDeviceTotal": offline_devices,
            "riskDeviceTotal": risk_devices,
            "pointTotal": int(point_total),
            "systemAlertTotal": system_alert_total,
            "systemAlertComparePrev": system_alert_compare_prev,
            "generatedAt": now_local().strftime("%Y-%m-%d %H:%M:%S"),
        }

    async def get_device_type_distribution(self, params: dict[str, Any]) -> dict[str, Any]:
        top_n = self._to_positive_int(params.get("topN"), default=10)
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        COALESCE(dt.type_code, '') AS type_code,
                        COALESCE(dt.type_name, dt.type_code, '未分类') AS type_name,
                        COUNT(d.id) AS device_count,
                        SUM(CASE WHEN COALESCE(d.status, 0) = 1 THEN 1 ELSE 0 END) AS online_count
                    FROM device AS d
                    LEFT JOIN device_type AS dt ON dt.id = d.type_id
                    GROUP BY dt.type_code, dt.type_name
                    ORDER BY device_count DESC, type_code ASC
                    """
                )
            )
        ).mappings().all()

        total = sum(int(row["device_count"] or 0) for row in rows)
        groups = []
        for row in rows[:top_n]:
            device_count = int(row["device_count"] or 0)
            groups.append(
                {
                    "typeCode": row["type_code"] or "",
                    "typeName": row["type_name"] or row["type_code"] or "未分类",
                    "deviceCount": device_count,
                    "onlineCount": int(row["online_count"] or 0),
                    "ratio": round((device_count / total) * 100, 2) if total else 0,
                }
            )

        return {
            "total": total,
            "groups": groups,
        }

    async def get_trend(self, params: dict[str, Any]) -> dict[str, Any]:
        range_type = self._normalize_range_type(params.get("rangeType"))
        start_dt, end_dt = self._resolve_time_range(range_type)
        granularity = self._resolve_granularity(range_type, params.get("granularity"))
        bucket_delta = self._granularity_to_delta(granularity)
        bucket_starts = self._build_bucket_starts(start_dt, end_dt, bucket_delta)
        metric_keys = self._normalize_metric_keys(params.get("metricKeys"))

        points = await self._fetch_trend_points()
        collect_count_by_bucket: dict[datetime, int] = defaultdict(int)
        active_devices_by_bucket: dict[datetime, set[int]] = defaultdict(set)

        if points and bucket_starts:
            query_results = await asyncio.gather(
                *(
                    self._fetch_point_bucket_counts(
                        point=point,
                        start_dt=start_dt,
                        end_dt=end_dt,
                        granularity=granularity,
                    )
                    for point in points
                )
            )
            for point, point_counts in zip(points, query_results, strict=False):
                for bucket_start, count in point_counts.items():
                    collect_count_by_bucket[bucket_start] += count
                    if count > 0:
                        active_devices_by_bucket[bucket_start].add(point.device_id)

        trend_device_total = len({point.device_id for point in points})
        collect_series = [int(collect_count_by_bucket.get(bucket_start, 0)) for bucket_start in bucket_starts]
        online_rate_series = [
            round((len(active_devices_by_bucket.get(bucket_start, set())) / trend_device_total) * 100, 2)
            if trend_device_total
            else 0
            for bucket_start in bucket_starts
        ]
        alarm_series = await self._build_alarm_series_from_logs(bucket_starts, granularity)

        metric_definitions = {
            "collectCount": {
                "metricName": "采集次数",
                "unit": "次",
                "data": collect_series,
            },
            "alarmCount": {
                "metricName": "告警次数",
                "unit": "次",
                "data": alarm_series,
            },
            "onlineRate": {
                "metricName": "在线率",
                "unit": "%",
                "data": online_rate_series,
            },
        }

        series = [
            {
                "metricKey": metric_key,
                "metricName": metric_definitions[metric_key]["metricName"],
                "unit": metric_definitions[metric_key]["unit"],
                "data": metric_definitions[metric_key]["data"],
            }
            for metric_key in metric_keys
            if metric_key in metric_definitions
        ]

        return {
            "query": {
                "rangeType": range_type,
                "granularity": granularity,
            },
            "timeAxis": [
                self._format_bucket_label(
                    bucket_start=bucket_start,
                    bucket_delta=bucket_delta,
                    range_start=start_dt,
                    range_end=end_dt,
                )
                for bucket_start in bucket_starts
            ],
            "series": series,
            "summary": {
                "collectCountTotal": sum(collect_series),
                "alarmCountTotal": sum(alarm_series),
                "onlineRateAvg": round(sum(online_rate_series) / len(online_rate_series), 2)
                if online_rate_series
                else 0,
            },
        }

    async def get_device_connection_health(self, params: dict[str, Any]) -> dict[str, Any]:
        stat_range = self._normalize_stat_range(params.get("statRange"))
        top_n = self._to_positive_int(params.get("topN"), default=10)
        include_offline = self._to_bool(params.get("includeOffline"), default=True)

        devices = await self._fetch_dashboard_devices()
        latest_time_map = await self._fetch_latest_collect_times(devices)

        online_devices = 0
        risk_devices = 0
        offline_devices = 0
        for device in devices:
            status = self._build_dynamic_connect_status(device, latest_time_map.get(device.device_code))
            if status == "online":
                online_devices += 1
            elif status == "unstable":
                risk_devices += 1
            else:
                offline_devices += 1

        # 使用动态状态筛选
        candidate_devices = [
            device for device in devices
            if include_offline or self._build_dynamic_connect_status(
                device, latest_time_map.get(device.device_code)
            ) in {"online", "unstable"}
        ]
        candidate_devices.sort(
            key=lambda d: self._connection_health_dynamic_sort_key(d, latest_time_map.get(d.device_code))
        )
        selected_devices = candidate_devices[:top_n]

        items = [
            self._serialize_connection_health_item(device, latest_time_map.get(device.device_code))
            for device in selected_devices
        ]

        return {
            "query": {
                "statRange": stat_range,
                "topN": top_n,
                "includeOffline": include_offline,
            },
            "overview": {
                "onlineDevices": online_devices,
                "offlineDevices": offline_devices,
                "riskDevices": risk_devices,
            },
            "list": items,
        }

    async def get_alarm_distribution(self, params: dict[str, Any]) -> dict[str, Any]:
        stat_range = self._normalize_stat_range(params.get("statRange"))
        group_by = self._normalize_alarm_group_by(params.get("groupBy"))
        top_n = self._to_positive_int(params.get("topN"), default=6)
        include_other = self._to_bool(params.get("includeOther"), default=True)
        fact_tables = await self._get_fact_tables()
        result = await self._query_alarm_distribution(
            stat_range=stat_range,
            group_by=group_by,
            top_n=top_n,
            include_other=include_other,
            fact_tables=fact_tables,
        )
        if result is not None:
            return result

        return self._build_synthetic_alarm_distribution(
            stat_range=stat_range,
            group_by=group_by,
            top_n=top_n,
            include_other=include_other,
        )

    async def get_recent_events(self, params: dict[str, Any]) -> dict[str, Any]:
        limit = self._to_positive_int(params.get("limit"), default=10)
        event_type = self._normalize_recent_event_type(params.get("eventType"))
        fact_tables = await self._get_fact_tables()

        if event_type in {"alarm", "all"}:
            result = await self._query_recent_alarm_events(limit=limit, fact_tables=fact_tables)
            if result is not None:
                return result
            return {"list": self._build_synthetic_recent_events(limit=limit)}

        return {"list": []}

    async def list_logs(self, params: dict[str, Any]) -> dict[str, Any]:
        page_no = self._to_positive_int(params.get("pageNo") or params.get("current"), default=1)
        page_size = self._to_positive_int(params.get("pageSize") or params.get("size"), default=20)
        level = self._normalize_log_level(params.get("level"))
        log_type = self._normalize_log_type(params.get("type"))
        self._normalize_optional_datetime(params.get("startTime"), "startTime")
        self._normalize_optional_datetime(params.get("endTime"), "endTime")
        if level and level not in SUPPORTED_LOG_LEVELS:
            raise ApiError(400, "level is invalid")
        if log_type and log_type not in SUPPORTED_LOG_TYPES:
            raise ApiError(400, "type is invalid")

        fact_tables = await self._get_fact_tables()
        if "operation_log" not in fact_tables or "system_log" not in fact_tables:
            return self._build_page_payload([], 0, page_no, page_size)

        return self._build_page_payload([], 0, page_no, page_size)

    async def _query_alarm_distribution(
        self,
        *,
        stat_range: str,
        group_by: str,
        top_n: int,
        include_other: bool,
        fact_tables: set[str],
    ) -> dict[str, Any] | None:
        if "alarm_record" not in fact_tables:
            return None

        current_start, current_end = self._resolve_stat_time_window(stat_range)
        previous_start = current_start - (current_end - current_start)
        previous_end = current_start

        try:
            current_rows = (
                await self.session.execute(
                    text(
                        """
                        SELECT
                            alarm_type,
                            device_type_code,
                            level_code,
                            level_name_snapshot,
                            severity
                        FROM alarm_record
                        WHERE start_time >= :start_time
                          AND start_time < :end_time
                        """
                    ),
                    {
                        "start_time": current_start,
                        "end_time": current_end,
                    },
                )
            ).mappings().all()
            previous_total = int(
                (
                    await self.session.execute(
                        text(
                            """
                            SELECT COUNT(*) AS total
                            FROM alarm_record
                            WHERE start_time >= :start_time
                              AND start_time < :end_time
                            """
                        ),
                        {
                            "start_time": previous_start,
                            "end_time": previous_end,
                        },
                    )
                ).scalar_one()
                or 0
            )
        except Exception:
            return None

        current_records = [dict(row) for row in current_rows]
        if not current_records:
            return None

        return self._build_alarm_distribution_response(
            records=current_records,
            stat_range=stat_range,
            group_by=group_by,
            top_n=top_n,
            include_other=include_other,
            previous_total=previous_total,
        )

    async def _query_recent_alarm_events(
        self,
        *,
        limit: int,
        fact_tables: set[str],
    ) -> dict[str, Any] | None:
        if "alarm_record" not in fact_tables:
            return None

        try:
            rows = (
                await self.session.execute(
                    text(
                        """
                        SELECT
                            id,
                            alarm_no,
                            start_time,
                            device_id,
                            device_code,
                            device_name,
                            alarm_type,
                            level_code,
                            level_name_snapshot,
                            severity,
                            status,
                            content
                        FROM alarm_record
                        ORDER BY start_time DESC, id DESC
                        LIMIT :limit
                        """
                    ),
                    {"limit": limit},
                )
            ).mappings().all()
        except Exception:
            return None

        if not rows:
            return None

        return {
            "list": [self._serialize_alarm_event(dict(row), synthetic=False) for row in rows],
        }

    def _build_synthetic_recent_events(self, *, limit: int) -> list[dict[str, Any]]:
        records = self._synthetic_alarm_records()
        records.sort(key=lambda item: item.offset_minutes)
        return [
            self._serialize_alarm_event(
                {
                    "id": record.event_id,
                    "alarm_no": record.event_id,
                    "start_time": now_local() - timedelta(minutes=record.offset_minutes),
                    "device_id": record.device_id,
                    "device_code": record.device_code,
                    "device_name": record.device_name,
                    "alarm_type": record.alarm_type,
                    "level_code": record.level_code,
                    "level_name_snapshot": record.level_name,
                    "severity": record.severity,
                    "status": record.status,
                    "content": record.content,
                },
                synthetic=True,
            )
            for record in records[:limit]
        ]

    def _build_synthetic_alarm_distribution(
        self,
        *,
        stat_range: str,
        group_by: str,
        top_n: int,
        include_other: bool,
    ) -> dict[str, Any]:
        current_start, current_end = self._resolve_stat_time_window(stat_range)
        previous_start = current_start - (current_end - current_start)
        previous_end = current_start

        now = now_local()
        current_records = [
            self._synthetic_alarm_record_to_dict(record, now)
            for record in self._synthetic_alarm_records()
            if current_start <= now - timedelta(minutes=record.offset_minutes) < current_end
        ]
        previous_records = [
            self._synthetic_alarm_record_to_dict(record, now)
            for record in self._synthetic_alarm_records()
            if previous_start <= now - timedelta(minutes=record.offset_minutes) < previous_end
        ]

        return self._build_alarm_distribution_response(
            records=current_records,
            stat_range=stat_range,
            group_by=group_by,
            top_n=top_n,
            include_other=include_other,
            previous_total=len(previous_records),
        )

    def _build_alarm_distribution_response(
        self,
        *,
        records: list[dict[str, Any]],
        stat_range: str,
        group_by: str,
        top_n: int,
        include_other: bool,
        previous_total: int,
    ) -> dict[str, Any]:
        groups, total = self._aggregate_alarm_groups(
            records=records,
            group_by=group_by,
            top_n=top_n,
            include_other=include_other,
        )
        return {
            "query": {
                "statRange": stat_range,
                "groupBy": group_by,
                "topN": top_n,
            },
            "totalAlarmCount": total,
            "groups": groups,
            "comparePrev": {
                "countDiff": total - previous_total,
                "ratioDiff": self._calculate_ratio_diff(total, previous_total),
            },
        }

    def _aggregate_alarm_groups(
        self,
        *,
        records: list[dict[str, Any]],
        group_by: str,
        top_n: int,
        include_other: bool,
    ) -> tuple[list[dict[str, Any]], int]:
        totals: dict[tuple[str, str], int] = defaultdict(int)
        for record in records:
            group_key, group_name = self._resolve_alarm_distribution_group(record, group_by)
            totals[(group_key, group_name)] += 1

        entries = sorted(
            (
                {"groupKey": key, "groupName": name, "count": count}
                for (key, name), count in totals.items()
            ),
            key=lambda item: (-item["count"], item["groupKey"]),
        )

        total = sum(item["count"] for item in entries)
        visible_entries = entries[:top_n]
        if include_other and len(entries) > top_n:
            other_count = sum(item["count"] for item in entries[top_n:])
            if other_count > 0:
                visible_entries.append(
                    {
                        "groupKey": "other",
                        "groupName": "其他",
                        "count": other_count,
                    }
                )

        for item in visible_entries:
            item["ratio"] = round((item["count"] / total) * 100, 2) if total else 0
        return visible_entries, total

    def _resolve_alarm_distribution_group(
        self,
        record: dict[str, Any],
        group_by: str,
    ) -> tuple[str, str]:
        if group_by == "deviceType":
            device_type_code = str(record.get("device_type_code") or "UNKNOWN").upper()
            return device_type_code, device_type_code
        if group_by == "severity":
            level_code = str(record.get("level_code") or record.get("severity") or "UNKNOWN").upper()
            level_name = self._resolve_alarm_level_name(
                level_code=level_code,
                level_name=str(record.get("level_name_snapshot") or ""),
                severity=int(record.get("severity") or 0),
            )
            return level_code, level_name

        alarm_type = str(record.get("alarm_type") or "unknown").lower()
        return alarm_type, self._resolve_alarm_type_name(alarm_type)

    def _calculate_ratio_diff(self, current_total: int, previous_total: int) -> float:
        if previous_total <= 0:
            return 0.0
        return round(((current_total - previous_total) / previous_total) * 100, 2)

    def _serialize_alarm_event(self, row: dict[str, Any], *, synthetic: bool) -> dict[str, Any]:
        level_code = str(row.get("level_code") or "").upper()
        level_name = self._resolve_alarm_level_name(
            level_code=level_code,
            level_name=str(row.get("level_name_snapshot") or ""),
            severity=int(row.get("severity") or 0),
        )
        content = str(row.get("content") or "")
        if synthetic and "联调假数据" not in content:
            content = f"{content}（联调假数据）"

        event_id = str(row.get("alarm_no") or row.get("id") or "")
        return {
            "eventId": event_id,
            "eventTime": self._format_datetime(self._coerce_datetime(row.get("start_time"))),
            "deviceId": int(row.get("device_id") or 0),
            "deviceCode": str(row.get("device_code") or ""),
            "deviceName": str(row.get("device_name") or row.get("device_code") or ""),
            "eventType": "alarm",
            "eventLevel": level_name,
            "content": content,
            "status": str(row.get("status") or ""),
        }

    def _resolve_alarm_level_name(self, *, level_code: str, level_name: str, severity: int) -> str:
        if level_name.strip():
            return level_name.strip()
        level_code_map = {
            "INFO": "提示",
            "WARN": "预警",
            "ERROR": "告警",
        }
        if level_code in level_code_map:
            return level_code_map[level_code]
        severity_map = {1: "提示", 2: "预警", 3: "告警"}
        return severity_map.get(severity, level_code or "未分类")

    def _resolve_alarm_type_name(self, alarm_type: str) -> str:
        mapping = {
            "threshold": "阈值告警",
            "communication": "通讯告警",
            "bool_state": "状态告警",
        }
        return mapping.get(alarm_type, alarm_type or "未分类")

    def _resolve_stat_time_window(self, stat_range: str) -> tuple[datetime, datetime]:
        now = now_local().replace(second=0, microsecond=0)
        if stat_range == "last1h":
            return now - timedelta(hours=1), now
        if stat_range == "last7d":
            return now - timedelta(days=7), now
        return now - timedelta(hours=24), now

    def _synthetic_alarm_record_to_dict(
        self,
        record: SyntheticAlarmRecord,
        now: datetime,
    ) -> dict[str, Any]:
        return {
            "id": record.event_id,
            "alarm_no": record.event_id,
            "start_time": now - timedelta(minutes=record.offset_minutes),
            "device_id": record.device_id,
            "device_code": record.device_code,
            "device_name": record.device_name,
            "device_type_code": record.device_type_code,
            "alarm_type": record.alarm_type,
            "level_code": record.level_code,
            "level_name_snapshot": record.level_name,
            "severity": record.severity,
            "status": record.status,
            "content": record.content,
        }

    def _synthetic_alarm_records(self) -> list[SyntheticAlarmRecord]:
        return [
            SyntheticAlarmRecord(
                event_id="FAKE-ALARM-001",
                device_id=900001,
                device_code="qc100",
                device_name="QC100 轨道吊",
                device_type_code="QC",
                alarm_type="threshold",
                level_code="WARN",
                level_name="预警",
                severity=2,
                status="recorded",
                content="联调假数据：起升高度高于预警阈值。",
                offset_minutes=10,
            ),
            SyntheticAlarmRecord(
                event_id="FAKE-ALARM-002",
                device_id=900002,
                device_code="asc7011",
                device_name="ASC7011 轨道吊",
                device_type_code="ASC",
                alarm_type="communication",
                level_code="ERROR",
                level_name="告警",
                severity=3,
                status="confirmed",
                content="联调假数据：设备心跳丢失，进入通讯中断确认态。",
                offset_minutes=35,
            ),
            SyntheticAlarmRecord(
                event_id="FAKE-ALARM-003",
                device_id=900003,
                device_code="agv801",
                device_name="AGV801 搬运车",
                device_type_code="AGV",
                alarm_type="bool_state",
                level_code="WARN",
                level_name="预警",
                severity=2,
                status="processing",
                content="联调假数据：设备状态位异常，正在处理中。",
                offset_minutes=75,
            ),
            SyntheticAlarmRecord(
                event_id="FAKE-ALARM-004",
                device_id=900001,
                device_code="qc100",
                device_name="QC100 轨道吊",
                device_type_code="QC",
                alarm_type="threshold",
                level_code="WARN",
                level_name="预警",
                severity=2,
                status="handled",
                content="联调假数据：越限告警已处置完成。",
                offset_minutes=180,
            ),
            SyntheticAlarmRecord(
                event_id="FAKE-ALARM-005",
                device_id=900002,
                device_code="asc7011",
                device_name="ASC7011 轨道吊",
                device_type_code="ASC",
                alarm_type="communication",
                level_code="ERROR",
                level_name="告警",
                severity=3,
                status="recovered",
                content="联调假数据：通讯中断告警已恢复。",
                offset_minutes=300,
            ),
            SyntheticAlarmRecord(
                event_id="FAKE-ALARM-006",
                device_id=900003,
                device_code="agv801",
                device_name="AGV801 搬运车",
                device_type_code="AGV",
                alarm_type="threshold",
                level_code="INFO",
                level_name="提示",
                severity=1,
                status="handled",
                content="联调假数据：短时越线后已确认。",
                offset_minutes=1080,
            ),
            SyntheticAlarmRecord(
                event_id="FAKE-ALARM-007",
                device_id=900001,
                device_code="qc100",
                device_name="QC100 轨道吊",
                device_type_code="QC",
                alarm_type="bool_state",
                level_code="WARN",
                level_name="预警",
                severity=2,
                status="recovered",
                content="联调假数据：状态位恢复正常，告警关闭。",
                offset_minutes=1440,
            ),
            SyntheticAlarmRecord(
                event_id="FAKE-ALARM-008",
                device_id=900002,
                device_code="asc7011",
                device_name="ASC7011 轨道吊",
                device_type_code="ASC",
                alarm_type="communication",
                level_code="ERROR",
                level_name="告警",
                severity=3,
                status="handled",
                content="联调假数据：通讯抖动导致告警，已完成记录。",
                offset_minutes=2880,
            ),
            SyntheticAlarmRecord(
                event_id="FAKE-ALARM-009",
                device_id=900001,
                device_code="qc100",
                device_name="QC100 轨道吊",
                device_type_code="QC",
                alarm_type="threshold",
                level_code="WARN",
                level_name="预警",
                severity=2,
                status="handled",
                content="联调假数据：高值预警历史样本，用于近 7 天统计。",
                offset_minutes=5760,
            ),
            SyntheticAlarmRecord(
                event_id="FAKE-ALARM-010",
                device_id=900003,
                device_code="agv801",
                device_name="AGV801 搬运车",
                device_type_code="AGV",
                alarm_type="bool_state",
                level_code="INFO",
                level_name="提示",
                severity=1,
                status="handled",
                content="联调假数据：状态异常历史样本，用于等级分布统计。",
                offset_minutes=8640,
            ),
        ]

    async def _get_fact_tables(self) -> set[str]:
        if self._fact_tables is not None:
            return self._fact_tables

        statement = text(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name IN :table_names
            """
        ).bindparams(bindparam("table_names", expanding=True))

        rows = (
            await self.session.execute(
                statement,
                {"table_names": sorted(FACT_TABLE_NAMES)},
            )
        ).scalars().all()
        self._fact_tables = {str(row) for row in rows}
        return self._fact_tables

    async def _fetch_dashboard_devices(self) -> list[DashboardDeviceItem]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name,
                        COALESCE(dt.type_name, dt.type_code, '未分类') AS device_type,
                        COALESCE(dt.type_code, '') AS device_type_code,
                        COALESCE(d.status, 0) AS raw_status
                    FROM device AS d
                    LEFT JOIN device_type AS dt ON dt.id = d.type_id
                    ORDER BY d.id ASC
                    """
                )
            )
        ).mappings().all()

        return [
            DashboardDeviceItem(
                device_id=int(row["device_id"]),
                device_code=str(row["device_code"] or ""),
                device_name=str(row["device_name"] or row["device_code"] or ""),
                device_type=str(row["device_type"] or row["device_type_code"] or "未分类"),
                device_type_code=str(row["device_type_code"] or ""),
                raw_status=int(row["raw_status"] or 0),
            )
            for row in rows
        ]

    async def _fetch_trend_points(self) -> list[DashboardTrendPoint]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        dv.id AS point_id,
                        dv.device_id AS device_id,
                        d.device_code AS device_code,
                        dt.type_code AS device_type_code,
                        dv.var_seq AS var_seq
                    FROM device_variable AS dv
                    JOIN device AS d ON d.id = dv.device_id
                    LEFT JOIN device_type AS dt ON dt.id = d.type_id
                    WHERE dv.var_seq IS NOT NULL
                      AND dv.var_seq > 0
                    ORDER BY dv.var_seq ASC
                    """
                )
            )
        ).mappings().all()

        return [
            DashboardTrendPoint(
                legacy_point_id=int(row["point_id"]),
                device_id=int(row["device_id"]),
                device_code=str(row["device_code"] or ""),
                device_type_code=str(row["device_type_code"]) if row["device_type_code"] else None,
                var_seq=int(row["var_seq"]),
            )
            for row in rows
        ]

    async def _fetch_point_bucket_counts(
        self,
        *,
        point: DashboardTrendPoint,
        start_dt: datetime,
        end_dt: datetime,
        granularity: str,
    ) -> dict[datetime, int]:
        database_name = self._resolve_tdengine_database(point.device_code, point.device_type_code)
        table_name = self.tdengine.qualify_table(
            database_name,
            self._resolve_point_subtable_name(point.device_code, point.var_seq, point.legacy_point_id),
        )
        stable_name = self.tdengine.qualify_table(
            database_name,
            self._normalize_identifier(point.device_code).lower(),
        )
        start_sql = self._format_sql_timestamp(start_dt)
        end_sql = self._format_sql_timestamp(end_dt)
        interval_sql = self._granularity_to_td_interval(granularity)

        statements = [
            (
                f"SELECT _wstart AS window_start, COUNT(*) AS collect_count FROM {table_name} "
                f"WHERE ts >= '{start_sql}' AND ts < '{end_sql}' "
                f"INTERVAL({interval_sql})"
            ),
        ]
        statements.append(
            (
                f"SELECT _wstart AS window_start, COUNT(*) AS collect_count FROM {stable_name} "
                f"WHERE var_seq = {int(point.var_seq)} "
                f"AND ts >= '{start_sql}' AND ts < '{end_sql}' "
                f"INTERVAL({interval_sql})"
            )
        )
        statements.extend(
            [
                (
                    f"SELECT _wstart AS window_start, COUNT(*) AS collect_count FROM {self.tdengine.qualify_table(database_name, self._resolve_point_subtable_name(point.device_code, None, point.legacy_point_id))} "
                    f"WHERE ts >= '{start_sql}' AND ts < '{end_sql}' "
                    f"INTERVAL({interval_sql})"
                ),
                (
                    f"SELECT _wstart AS window_start, COUNT(*) AS collect_count FROM {stable_name} "
                    f"WHERE variable_id = {point.legacy_point_id} "
                    f"AND ts >= '{start_sql}' AND ts < '{end_sql}' "
                    f"INTERVAL({interval_sql})"
                ),
            ]
        )

        for statement in statements:
            try:
                rows = await self.tdengine.fetch_all(statement, database=database_name)
            except Exception:
                continue
            if rows is not None:
                return self._normalize_bucket_rows(rows, granularity)

        return {}

    def _normalize_bucket_rows(self, rows: list[tuple[Any, ...]], granularity: str) -> dict[datetime, int]:
        result: dict[datetime, int] = {}
        for row in rows:
            if len(row) < 2:
                continue
            bucket_start = self._coerce_datetime(row[0])
            if bucket_start is None:
                continue
            aligned_bucket = self._align_bucket_start(bucket_start, granularity)
            try:
                result[aligned_bucket] = result.get(aligned_bucket, 0) + int(row[1] or 0)
            except (TypeError, ValueError):
                continue
        return result

    async def _fetch_latest_collect_times(
        self,
        devices: list[DashboardDeviceItem],
    ) -> dict[str, datetime]:
        if not devices:
            return {}

        results = await asyncio.gather(
            *(self._fetch_device_latest_collect_time(device) for device in devices),
            return_exceptions=True,
        )
        return {
            device.device_code: latest_time
            for device, latest_time in zip(devices, results, strict=False)
            if isinstance(latest_time, datetime)
        }

    async def _fetch_device_latest_collect_time(
        self,
        device: DashboardDeviceItem,
    ) -> datetime | None:
        try:
            database_name = self._resolve_tdengine_database(device.device_code, device.device_type_code)
            table_name = self.tdengine.qualify_table(
                database_name,
                self._normalize_identifier(device.device_code).lower(),
            )
            rows = await self.tdengine.fetch_all(
                f"SELECT LAST(ts) AS last_ts FROM {table_name}",
                database=database_name,
            )
        except Exception:
            return None

        if not rows:
            return None
        return self._coerce_datetime(rows[0][0])

    def _serialize_connection_health_item(
        self,
        device: DashboardDeviceItem,
        last_heartbeat_time: datetime | None,
    ) -> dict[str, Any]:
        connect_status = self._build_dynamic_connect_status(device, last_heartbeat_time)
        age_seconds = (
            max(0.0, (now_local() - last_heartbeat_time).total_seconds())
            if last_heartbeat_time is not None
            else None
        )

        timeout_count = 1 if connect_status == "offline" else 0
        reconnect_count = 1 if connect_status == "unstable" else 0
        exception_count = 1 if connect_status == "unstable" and age_seconds is not None and age_seconds > ONLINE_MAX_AGE_SECONDS else 0

        if connect_status == "online":
            success_rate = 100.0
        elif connect_status == "unstable":
            success_rate = 88.0
        else:
            success_rate = 0.0

        return {
            "deviceId": device.device_id,
            "deviceCode": device.device_code,
            "deviceName": device.device_name,
            "deviceType": device.device_type,
            "connectStatus": connect_status,
            "exceptionCount": exception_count,
            "timeoutCount": timeout_count,
            "reconnectCount": reconnect_count,
            "successRate": success_rate,
            "lastHeartbeatTime": self._format_datetime(last_heartbeat_time),
        }

    async def _build_alarm_series_from_logs(
        self,
        bucket_starts: list[datetime],
        granularity: str,
    ) -> list[int]:
        """从系统日志统计告警次数（按时间桶）"""
        if not bucket_starts:
            return []

        start_dt = bucket_starts[0]
        bucket_delta = self._granularity_to_delta(granularity)
        end_dt = bucket_starts[-1] + bucket_delta

        if granularity == "1d":
            bucket_expr = "DATE_FORMAT(log_time, '%Y-%m-%d')"
            bucket_key_format = "%Y-%m-%d"
        elif granularity == "1h":
            bucket_expr = "DATE_FORMAT(log_time, '%Y-%m-%d %H:00')"
            bucket_key_format = "%Y-%m-%d %H:00"
        else:
            bucket_minutes = 15 if granularity == "15m" else 5
            bucket_expr = (
                "CONCAT(DATE_FORMAT(log_time, '%Y-%m-%d %H:'), "
                f"LPAD(FLOOR(MINUTE(log_time) / {bucket_minutes}) * {bucket_minutes}, 2, '0'))"
            )
            bucket_key_format = "%Y-%m-%d %H:%M"

        try:
            rows = await self.session.execute(
                text(
                    f"""
                    SELECT
                        {bucket_expr} AS bucket,
                        COUNT(*) AS count
                    FROM (
                        SELECT log_time FROM system_log
                        WHERE level IN ('WARN', 'ERROR', 'CRITICAL')
                          AND log_time >= :start AND log_time < :end
                        UNION ALL
                        SELECT log_time FROM operation_log
                        WHERE level IN ('WARN', 'ERROR', 'CRITICAL')
                          AND log_time >= :start AND log_time < :end
                    ) AS combined
                    GROUP BY bucket
                    """
                ),
                {"start": start_dt, "end": end_dt},
            )
            counts_by_bucket = {
                row["bucket"]: int(row["count"])
                for row in rows.mappings().all()
            }
        except Exception:
            return [0] * len(bucket_starts)

        return [
            counts_by_bucket.get(bucket_start.strftime(bucket_key_format), 0)
            for bucket_start in bucket_starts
        ]

    def _connection_health_sort_key(self, device: DashboardDeviceItem) -> tuple[int, int]:
        status = self._build_connect_status_from_raw(device.raw_status)
        priority = {"offline": 0, "unstable": 1, "online": 2}.get(status, 3)
        return (priority, device.device_id)

    def _connection_health_dynamic_sort_key(
        self,
        device: DashboardDeviceItem,
        last_collect_time: datetime | None,
    ) -> tuple[int, int]:
        status = self._build_dynamic_connect_status(device, last_collect_time)
        priority = {"offline": 0, "unstable": 1, "online": 2}.get(status, 3)
        return (priority, device.device_id)

    def _normalize_metric_keys(self, raw_value: Any) -> list[str]:
        if raw_value in (None, ""):
            return ["collectCount", "alarmCount", "onlineRate"]
        if isinstance(raw_value, list):
            values = [str(item).strip() for item in raw_value if str(item).strip()]
        else:
            values = [item.strip() for item in str(raw_value).split(",") if item.strip()]
        filtered = [value for value in values if value in {"collectCount", "alarmCount", "onlineRate"}]
        return filtered or ["collectCount", "alarmCount", "onlineRate"]

    def _normalize_range_type(self, value: Any) -> str:
        normalized = str(value or "last24h").strip()
        if normalized not in SUPPORTED_RANGE_TYPES:
            raise ApiError(400, "rangeType is invalid")
        return normalized

    def _normalize_stat_range(self, value: Any) -> str:
        normalized = str(value or "last24h").strip()
        if normalized not in {"last1h", "last24h", "last7d"}:
            raise ApiError(400, "statRange is invalid")
        return normalized

    def _normalize_alarm_group_by(self, value: Any) -> str:
        normalized = str(value or "alarmType").strip()
        if normalized not in {"alarmType", "deviceType", "severity"}:
            raise ApiError(400, "groupBy is invalid")
        return normalized

    def _normalize_recent_event_type(self, value: Any) -> str:
        normalized = str(value or "all").strip()
        if normalized not in {"alarm", "collect", "connection", "all"}:
            raise ApiError(400, "eventType is invalid")
        return normalized

    def _normalize_log_level(self, value: Any) -> str:
        return str(value or "").strip().upper()

    def _normalize_log_type(self, value: Any) -> str:
        return str(value or "").strip().lower()

    def _resolve_time_range(self, range_type: str) -> tuple[datetime, datetime]:
        now = now_local().replace(second=0, microsecond=0)
        if range_type == "today":
            return now.replace(hour=0, minute=0), now
        if range_type == "last7d":
            return now - timedelta(days=7), now
        return now - timedelta(hours=24), now

    def _resolve_granularity(self, range_type: str, raw_value: Any) -> str:
        normalized = str(raw_value or "auto").strip()
        if normalized not in SUPPORTED_GRANULARITIES:
            raise ApiError(400, "granularity is invalid")
        if normalized != "auto":
            return normalized
        if range_type == "last7d":
            return "1d"
        return "1h"

    def _granularity_to_delta(self, granularity: str) -> timedelta:
        mapping = {
            "5m": timedelta(minutes=5),
            "15m": timedelta(minutes=15),
            "1h": timedelta(hours=1),
            "1d": timedelta(days=1),
        }
        return mapping[granularity]

    def _granularity_to_td_interval(self, granularity: str) -> str:
        mapping = {
            "5m": "5m",
            "15m": "15m",
            "1h": "1h",
            "1d": "1d",
        }
        return mapping[granularity]

    def _build_bucket_starts(
        self,
        start_dt: datetime,
        end_dt: datetime,
        bucket_delta: timedelta,
    ) -> list[datetime]:
        bucket_starts: list[datetime] = []
        current = self._align_bucket_start(start_dt, self._delta_to_granularity(bucket_delta))
        while current < end_dt:
            bucket_starts.append(current)
            current += bucket_delta
        return bucket_starts

    def _align_bucket_start(self, value: datetime, granularity: str) -> datetime:
        if granularity == "1d":
            return value.replace(hour=0, minute=0, second=0, microsecond=0)
        if granularity == "1h":
            return value.replace(minute=0, second=0, microsecond=0)
        if granularity == "15m":
            minute = (value.minute // 15) * 15
            return value.replace(minute=minute, second=0, microsecond=0)
        minute = (value.minute // 5) * 5
        return value.replace(minute=minute, second=0, microsecond=0)

    def _delta_to_granularity(self, bucket_delta: timedelta) -> str:
        if bucket_delta >= timedelta(days=1):
            return "1d"
        if bucket_delta >= timedelta(hours=1):
            return "1h"
        if bucket_delta >= timedelta(minutes=15):
            return "15m"
        return "5m"

    def _format_bucket_label(
        self,
        *,
        bucket_start: datetime,
        bucket_delta: timedelta,
        range_start: datetime,
        range_end: datetime,
    ) -> str:
        if bucket_delta >= timedelta(days=1):
            return bucket_start.strftime("%m-%d")
        if range_end - range_start > timedelta(days=1):
            return bucket_start.strftime("%m-%d %H:%M")
        return bucket_start.strftime("%H:%M")

    def _build_connect_status_from_raw(self, raw_status: int) -> str:
        if raw_status == 1:
            return "online"
        if raw_status == 2:
            return "unstable"
        return "offline"

    def _build_dynamic_connect_status(
        self,
        device: DashboardDeviceItem,
        last_collect_time: datetime | None,
    ) -> str:
        if last_collect_time is None:
            return "offline"
        age_seconds = max(0.0, (now_local() - last_collect_time).total_seconds())
        if age_seconds <= ONLINE_MAX_AGE_SECONDS:
            return "online"
        if age_seconds <= UNSTABLE_MAX_AGE_SECONDS:
            return "unstable"
        return "offline"

    def _resolve_tdengine_database(self, device_code: str, device_type_code: str | None) -> str:
        if device_type_code:
            normalized_type_code = self._normalize_identifier(device_type_code).lower()
            if normalized_type_code:
                return normalized_type_code

        normalized_device_code = self._normalize_identifier(device_code).lower()
        match = DEVICE_PREFIX_RE.match(normalized_device_code)
        if match is None:
            raise ApiError(400, f"Unable to resolve TDengine database for {device_code}")
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
        return parse_local_datetime(value)

    def _normalize_optional_datetime(self, value: Any, field_name: str) -> datetime | None:
        if value in (None, ""):
            return None

        parsed = self._coerce_datetime(value)
        if parsed is None:
            try:
                parsed = datetime.strptime(str(value).strip(), "%Y-%m-%d %H:%M:%S")
            except ValueError as exc:
                raise ApiError(400, f"{field_name} must be a valid datetime string") from exc
        return parsed

    def _to_positive_int(self, value: Any, *, default: int) -> int:
        if value in (None, ""):
            return default
        try:
            normalized = int(value)
        except (TypeError, ValueError) as exc:
            raise ApiError(400, "value must be a positive integer") from exc
        if normalized <= 0:
            raise ApiError(400, "value must be a positive integer")
        return normalized

    def _to_bool(self, value: Any, *, default: bool) -> bool:
        if value in (None, ""):
            return default
        if isinstance(value, bool):
            return value
        normalized = str(value).strip().lower()
        if normalized in {"1", "true", "yes", "on"}:
            return True
        if normalized in {"0", "false", "no", "off"}:
            return False
        raise ApiError(400, "boolean parameter is invalid")

    def _format_sql_timestamp(self, value: datetime) -> str:
        return value.strftime("%Y-%m-%d %H:%M:%S")

    def _format_datetime(self, value: datetime | None) -> str:
        return value.strftime("%Y-%m-%d %H:%M:%S") if value else ""

    def _build_page_payload(
        self,
        items: list[dict[str, Any]],
        total: int,
        page_no: int,
        page_size: int,
    ) -> dict[str, Any]:
        return {
            "list": items,
            "records": items,
            "total": total,
            "pageNo": page_no,
            "pageSize": page_size,
            "current": page_no,
            "size": page_size,
            "pageNum": page_no,
        }
