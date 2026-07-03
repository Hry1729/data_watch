from __future__ import annotations

import asyncio
from datetime import datetime
import json
from typing import Any

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.time_utils import LOCAL_TZ, to_local_naive
from app.db.redis import get_redis_client


DEFAULT_POINT_GROUP_CODE = "g001"


class ModuleMonitorService:
    """采集 / 入库页一期即时类指标（只读，零改 collector/writer）。"""

    def __init__(self, session: AsyncSession):
        self.session = session

    async def get_collector_metrics(self) -> dict[str, Any]:
        rows = await self._fetch_device_aggregates()
        last_times = await asyncio.gather(
            *(self._device_last_collect_time(str(row["device_code"])) for row in rows)
        )

        tasks: list[dict[str, Any]] = []
        online_devices = 0
        point_total = 0
        scan_rates: list[int] = []

        for row, last_time in zip(rows, last_times, strict=True):
            connect_status = self._connect_status_from_raw(int(row["device_status"] or 0))
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

    async def get_writer_metrics(self) -> dict[str, Any]:
        r = await get_redis_client()

        streams: list[dict[str, Any]] = []
        stream_lag_total = 0
        pel_pending_total = 0

        async for key in r.scan_iter(match="rt:stream:*", count=100):
            try:
                groups = await r.xinfo_groups(key)
            except Exception:
                groups = []

            lag = 0
            pel = 0
            for group in groups:
                group_lag = group.get("lag")
                lag += int(group_lag) if group_lag is not None else 0
                group_pending = group.get("pending")
                pel += int(group_pending) if group_pending is not None else 0

            try:
                length = int(await r.xlen(key))
            except Exception:
                length = 0

            stream_lag_total += lag
            pel_pending_total += pel
            streams.append(
                {
                    "streamKey": str(key),
                    "lag": lag,
                    "pel": pel,
                    "length": length,
                    "status": "normal" if (lag < 1000 and pel < 1000) else "backlog",
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
            "streams": streams,
        }

    async def _fetch_device_aggregates(self) -> list[dict[str, Any]]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name,
                        COALESCE(dg.group_name, '') AS group_name,
                        COALESCE(cp.protocol, '') AS protocol,
                        d.status AS device_status,
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

    async def _device_last_collect_time(self, device_code: str) -> datetime | None:
        """读 rt:latest:{device_code}:g001 Hash，取各点位最大时间戳。"""
        try:
            r = await get_redis_client()
            key = f"rt:latest:{device_code}:{DEFAULT_POINT_GROUP_CODE}"
            raw = await r.hgetall(key)
        except Exception:
            return None

        max_ts_ms: float | None = None
        for json_str in raw.values():
            try:
                entry = json.loads(json_str)
            except Exception:
                continue
            ts_ms = entry.get("t")
            if isinstance(ts_ms, (int, float)):
                if max_ts_ms is None or ts_ms > max_ts_ms:
                    max_ts_ms = ts_ms

        if max_ts_ms is None:
            return None
        return to_local_naive(datetime.fromtimestamp(max_ts_ms / 1000, tz=LOCAL_TZ))

    @staticmethod
    def _connect_status_from_raw(raw_status: int) -> str:
        """与 monitor.MonitorService._build_connect_status_from_raw 同口径：device.status 1=online 2=unstable 其余 offline。"""
        if raw_status == 1:
            return "online"
        if raw_status == 2:
            return "unstable"
        return "offline"

    def _format_datetime(self, value: datetime | None) -> str:
        return value.strftime("%Y-%m-%d %H:%M:%S") if value else ""
