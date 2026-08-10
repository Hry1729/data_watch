from __future__ import annotations

from datetime import datetime
from typing import Any

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.status_thresholds import ONLINE_MAX_AGE_SECONDS, UNSTABLE_MAX_AGE_SECONDS
from app.core.time_utils import now_local
from app.core.writer_group import get_writer_group_name
from app.db.redis import get_redis_client
from app.services.device_activity import fetch_latest_collect_times


class ModuleMonitorService:
    """采集 / 入库页一期即时类指标（只读，零改 collector/writer）。"""

    def __init__(self, session: AsyncSession):
        self.session = session

    async def get_collector_metrics(self) -> dict[str, Any]:
        rows = await self._fetch_device_aggregates()
        last_time_map, _redis_connected = await fetch_latest_collect_times(
            str(row["device_code"]) for row in rows
        )

        tasks: list[dict[str, Any]] = []
        online_devices = 0
        point_total = 0
        scan_rates: list[int] = []

        for row in rows:
            last_time = last_time_map.get(str(row["device_code"]))
            connect_status = self._connect_status(last_time)
            if connect_status == "online":
                online_devices += 1

            point_count = int(row["point_count"] or 0)
            point_total += point_count

            scan_rate = int(row["scan_rate_ms"]) if row["scan_rate_ms"] is not None else None
            if scan_rate is not None:
                scan_rates.append(scan_rate)

            tasks.append(
                {
                    "deviceCode": str(row["device_code"]),
                    "deviceName": str(row["device_name"] or row["device_code"]),
                    "groupName": str(row["group_name"] or ""),
                    "protocol": str(row["protocol"] or ""),
                    "scanRateMs": scan_rate,
                    "pointCount": point_count,
                    "lastCollectTime": self._format_datetime(last_time),
                    "connectStatus": connect_status,
                }
            )

        return {
            "scanRateMs": min(scan_rates) if scan_rates else None,
            "onlineDevices": online_devices,
            "totalDevices": len(rows),
            "pointTotal": point_total,
            "tasks": tasks,
        }

    async def get_device_statuses(self) -> dict[str, Any]:
        rows = await self._fetch_device_status_rows()
        last_time_map, activity_connected = await fetch_latest_collect_times(
            str(row["device_code"]) for row in rows
        )

        items: list[dict[str, Any]] = []
        counts = {"online": 0, "unstable": 0, "offline": 0, "unknown": 0}
        status_source = (
            "collector_metrics" if activity_connected else "collector_metrics_unavailable"
        )
        for row in rows:
            device_code = str(row["device_code"])
            last_time = last_time_map.get(device_code)
            connect_status = (
                self._connect_status(last_time) if activity_connected else "unknown"
            )
            counts[connect_status] += 1
            items.append(
                {
                    "deviceId": int(row["device_id"]),
                    "deviceCode": device_code,
                    "deviceName": str(row["device_name"] or device_code),
                    "deviceStatus": "enabled" if int(row["enable"] or 0) == 1 else "disabled",
                    "connectStatus": connect_status,
                    "lastCollectTime": self._format_datetime(last_time),
                    "groupName": str(row["group_name"] or ""),
                    "protocol": str(row["protocol"] or ""),
                    "statusSource": status_source,
                }
            )

        return {
            "totalDevices": len(items),
            "onlineDevices": counts["online"],
            "unstableDevices": counts["unstable"],
            "offlineDevices": counts["offline"],
            "unknownDevices": counts["unknown"],
            "statusSource": status_source,
            "items": items,
        }

    async def get_writer_metrics(self) -> dict[str, Any]:
        r = await get_redis_client()
        writer_group = get_writer_group_name()

        streams: list[dict[str, Any]] = []
        stream_lag_total = 0
        pel_pending_total = 0

        async for key in r.scan_iter(match="rt:stream:*", count=100):
            try:
                groups = await r.xinfo_groups(key)
            except Exception:
                groups = []

            # 主指标只计 writer 消费组；其它 group 仅作诊断字段
            lag: int | None = None
            pel = 0
            other_groups: list[dict[str, Any]] = []
            for group in groups:
                name = str(group.get("name") or "")
                if name != writer_group:
                    other_groups.append(
                        {
                            "name": name,
                            "lag": group.get("lag"),
                            "pending": group.get("pending"),
                        }
                    )
                    continue
                group_lag = group.get("lag")
                lag = None if group_lag is None else int(group_lag)
                group_pending = group.get("pending")
                pel = int(group_pending) if group_pending is not None else 0

            try:
                length = int(await r.xlen(key))
            except Exception:
                length = 0

            if lag is not None:
                stream_lag_total += lag
            pel_pending_total += pel
            backlog = (lag is not None and lag >= 1000) or pel >= 1000
            streams.append(
                {
                    "streamKey": str(key),
                    "lag": lag,
                    "pel": pel,
                    "length": length,
                    "status": "backlog" if backlog else "normal",
                    "otherGroups": other_groups,
                }
            )

        dlq_total = 0
        async for key in r.scan_iter(match="rt:dlq:*", count=100):
            try:
                dlq_total += int(await r.llen(key))
            except Exception:
                pass

        streams.sort(key=lambda item: item["streamKey"])

        return {
            "streamLagTotal": stream_lag_total,
            "pelPendingTotal": pel_pending_total,
            "dlqTotal": dlq_total,
            "writerGroupName": writer_group,
            "streams": streams,
        }

    async def _fetch_device_status_rows(self) -> list[dict[str, Any]]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name,
                        COALESCE(d.enable, 0) AS enable,
                        COALESCE(dg.group_name, '') AS group_name,
                        COALESCE(cp.protocol, '') AS protocol
                    FROM device AS d
                    LEFT JOIN device_group AS dg ON dg.id = d.group_id
                    LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
                    ORDER BY d.id ASC
                    """
                )
            )
        ).mappings().all()
        return [dict(row) for row in rows]

    async def _fetch_device_aggregates(self) -> list[dict[str, Any]]:
        rows = (
            await self.session.execute(
                text(
                    # 查询设备及其关联驱动、组信息和变量扫描率
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name,
                        COALESCE(dg.group_name, '') AS group_name,
                        COALESCE(cp.protocol, '') AS protocol,
                        MIN(
                            CASE
                                WHEN dv.enable = 1 AND dv.deleted = 0
                                     AND dv.scan_rate IS NOT NULL AND dv.scan_rate > 0
                                THEN dv.scan_rate
                            END
                        ) AS scan_rate_ms,
                        COUNT(
                            CASE WHEN dv.enable = 1 AND dv.deleted = 0 THEN 1 END
                        ) AS point_count
                    FROM device AS d
                    LEFT JOIN device_group AS dg ON dg.id = d.group_id
                    LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
                    LEFT JOIN device_variable AS dv ON dv.device_id = d.id
                    WHERE d.enable = 1
                    GROUP BY d.id, d.device_code, d.device_name, d.status, dg.group_name, cp.protocol
                    ORDER BY d.id ASC
                    """
                )
            )
        ).mappings().all()
        return [dict(row) for row in rows]

    @staticmethod
    def _connect_status(last_collect_time: datetime | None) -> str:
        if last_collect_time is None:
            return "offline"
        age_seconds = max(0.0, (now_local() - last_collect_time).total_seconds())
        if age_seconds <= ONLINE_MAX_AGE_SECONDS:
            return "online"
        if age_seconds <= UNSTABLE_MAX_AGE_SECONDS:
            return "unstable"
        return "offline"

    def _format_datetime(self, value: datetime | None) -> str:
        return value.strftime("%Y-%m-%d %H:%M:%S") if value else ""
