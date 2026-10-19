from __future__ import annotations

import json
import math
import shutil
import sqlite3
import time
from contextlib import closing
from datetime import datetime
from pathlib import Path
from typing import Any


ONLINE_MAX_AGE_MS = 60_000
UNSTABLE_MAX_AGE_MS = 120_000


class EdgeLocalStore:
    """Read-only operational queries for the Edge Agent API."""

    def __init__(self, path: str | Path) -> None:
        self.path = Path(path)

    def devices(self) -> dict[str, Any]:
        if not self.path.exists():
            return {
                "config": None,
                "fastest_point_scan_rate_ms": None,
                "devices": [],
            }
        with closing(self._connect()) as connection:
            meta = connection.execute(
                """
                SELECT config_revision, checksum, read_timeout_ms, applied_at
                FROM edge_config_meta WHERE singleton=1
                """
            ).fetchone()
            try:
                rows = connection.execute(
                    """
                    SELECT d.device_code, d.device_type, d.protocol, d.endpoint,
                           d.scan_rate_ms, d.config_version, d.assignment_epoch,
                           d.enabled, s.status, s.message,
                           s.updated_at AS status_updated_at,
                           MAX(ll.updated_at) AS data_updated_at
                    FROM edge_device AS d
                    LEFT JOIN edge_device_status AS s ON s.device_code=d.device_code
                    LEFT JOIN local_latest AS ll ON ll.device_code=d.device_code
                    GROUP BY d.device_code, d.device_type, d.protocol, d.endpoint,
                             d.scan_rate_ms, d.config_version, d.assignment_epoch,
                             d.enabled, s.status, s.message, s.updated_at
                    ORDER BY d.device_code
                    """
                ).fetchall()
            except sqlite3.OperationalError as exc:
                if "no such table: local_latest" not in str(exc):
                    raise
                rows = connection.execute(
                    """
                    SELECT d.device_code, d.device_type, d.protocol, d.endpoint,
                           d.scan_rate_ms, d.config_version, d.assignment_epoch,
                           d.enabled, s.status, s.message,
                           s.updated_at AS status_updated_at,
                           NULL AS data_updated_at
                    FROM edge_device AS d
                    LEFT JOIN edge_device_status AS s ON s.device_code=d.device_code
                    ORDER BY d.device_code
                    """
                ).fetchall()
            try:
                fastest_point_rate = connection.execute(
                    """
                    SELECT MIN(v.scan_rate_ms) AS value
                    FROM edge_variable AS v
                    INNER JOIN edge_device AS d ON d.device_code=v.device_code
                    WHERE d.enabled=1
                    """
                ).fetchone()
            except sqlite3.OperationalError:
                fastest_point_rate = None
        devices = []
        for row in rows:
            item = dict(row)
            item["status"] = self._runtime_connect_status(row)
            devices.append(item)
        return {
            "config": dict(meta) if meta is not None else None,
            "fastest_point_scan_rate_ms": (
                int(fastest_point_rate["value"])
                if fastest_point_rate is not None
                and fastest_point_rate["value"] is not None
                else None
            ),
            "devices": devices,
        }

    def monitor_device_scope(self) -> list[dict[str, Any]]:
        if not self.path.exists():
            return []
        try:
            with closing(self._connect()) as connection:
                rows = connection.execute("SELECT device_pk, device_code, device_type FROM edge_device ORDER BY device_code").fetchall()
        except sqlite3.OperationalError:
            return []
        return [{"id": int(row["device_pk"]), "deviceCode": str(row["device_code"]), "deviceType": str(row["device_type"])} for row in rows]

    def monitor_devices(
        self, *, page_no: int = 1, page_size: int = 1000, keyword: str = "",
        device_ids: list[int] | None = None, device_type: str = "", device_type_exact: str | None = None,
        device_code_exact: str | None = None,
        connect_status: str = "", device_status: str = "",
    ) -> dict[str, Any]:
        if not self.path.exists():
            return {"list": [], "total": 0, "pageNo": page_no, "pageSize": page_size}
        try:
            with closing(self._connect()) as connection:
                rows = connection.execute(
                    """
                    SELECT d.device_pk, d.device_code, d.device_type, d.protocol,
                           d.enabled, s.status, s.updated_at AS status_updated_at,
                           MAX(ll.updated_at) AS data_updated_at
                    FROM edge_device AS d
                    LEFT JOIN edge_device_status AS s ON s.device_code=d.device_code
                    LEFT JOIN local_latest AS ll ON ll.device_code=d.device_code
                    GROUP BY d.device_pk, d.device_code, d.device_type, d.protocol,
                             d.enabled, s.status, s.updated_at
                    ORDER BY d.device_code
                    """
                ).fetchall()
        except sqlite3.OperationalError:
            rows = []
        needle = keyword.strip().lower()
        requested_status = connect_status
        items = []
        for row in rows:
            device_code = str(row["device_code"])
            if device_code_exact is not None and device_code != device_code_exact:
                continue
            if device_ids is not None and int(row["device_pk"]) not in device_ids:
                continue
            if device_type_exact is not None and str(row["device_type"]) != device_type_exact:
                continue
            if device_type and device_type.lower() not in str(row["device_type"]).lower():
                continue
            if needle and needle not in f"{device_code} {row['device_type']}".lower():
                continue
            data_updated_at = self._optional_int(row["data_updated_at"])
            connect_status = self._runtime_connect_status(row)
            if requested_status and {1: "online", 2: "unstable"}.get(connect_status, "offline") != requested_status:
                continue
            if device_status and ("enabled" if bool(row["enabled"]) else "disabled") != device_status:
                continue
            data_age_ms = self._age_ms(data_updated_at)
            items.append(
                {
                    "id": int(row["device_pk"]),
                    "deviceType": str(row["device_type"]),
                    "deviceCode": device_code,
                    "deviceName": device_code,
                    "driverName": str(row["protocol"]),
                    "deviceStatus": "enabled" if bool(row["enabled"]) else "disabled",
                    "connectStatus": {1: "online", 2: "unstable"}.get(connect_status, "offline"),
                    "connectUpdateTime": self._format_ms(data_updated_at or row["status_updated_at"]),
                    "dataStatus": "nodata" if data_age_ms is None else "normal" if data_age_ms <= ONLINE_MAX_AGE_MS else "abnormal",
                    "dataUpdateTime": self._format_ms(data_updated_at),
                    "description": "",
                }
            )
        total = len(items)
        start = max(0, page_no - 1) * page_size
        return {
            "list": items[start : start + page_size],
            "total": total,
            "pageNo": page_no,
            "pageSize": page_size,
        }

    def _runtime_connect_status(self, row: sqlite3.Row) -> int:
        if not bool(row["enabled"]):
            return 0
        data_age_ms = self._age_ms(self._optional_int(row["data_updated_at"]))
        if data_age_ms is not None:
            if data_age_ms <= ONLINE_MAX_AGE_MS:
                return 1
            if data_age_ms <= UNSTABLE_MAX_AGE_MS:
                return 2
            return 0
        status_age_ms = self._age_ms(self._optional_int(row["status_updated_at"]))
        if int(row["status"] or 0) == 1 and status_age_ms is not None:
            if status_age_ms <= ONLINE_MAX_AGE_MS:
                return 1
            if status_age_ms <= UNSTABLE_MAX_AGE_MS:
                return 2
        return 0

    @staticmethod
    def _age_ms(timestamp: int | None) -> int | None:
        if timestamp is None:
            return None
        return max(0, int(time.time() * 1000) - timestamp)

    def monitor_points(
        self,
        *,
        page_no: int = 1,
        page_size: int = 1000,
        device_id: int | None = None,
        device_ids: list[int] | None = None,
        device_code: str = "",
        keyword: str = "",
        data_type: str = "",
        device_type: str = "",
        connect_status: str = "",
    ) -> dict[str, Any]:
        if not self.path.exists():
            return {"list": [], "total": 0, "pageNo": page_no, "pageSize": page_size}
        conditions = []
        params: list[Any] = []
        if device_type or connect_status:
            matching = self.monitor_devices(
                page_size=2**31 - 1, device_ids=device_ids,
                device_type_exact=device_type or None, connect_status=connect_status,
            )
            device_ids = [device["id"] for device in matching["list"]]
        if device_id is not None:
            conditions.append("d.device_pk=?")
            params.append(device_id)
        if device_ids is not None:
            placeholders = ",".join("?" for _ in device_ids)
            conditions.append(f"d.device_pk IN ({placeholders})")
            params.extend(device_ids)
        if device_code:
            conditions.append("d.device_code=?")
            params.append(device_code)
        if data_type and data_type != "all":
            conditions.append("UPPER(v.data_type)=?")
            params.append(data_type.upper())
        where = " WHERE " + " AND ".join(conditions) if conditions else ""
        try:
            with closing(self._connect()) as connection:
                rows = connection.execute(
                    """
                    SELECT v.variable_id, d.device_pk, d.device_code, v.var_seq,
                           v.address, v.data_type, d.protocol
                    FROM edge_variable AS v
                    JOIN edge_device AS d ON d.device_code=v.device_code
                    """
                    + where
                    + " ORDER BY d.device_code, v.var_seq",
                    params,
                ).fetchall()
        except sqlite3.OperationalError:
            rows = []
        needle = keyword.strip().lower()
        items = []
        for row in rows:
            point_name = str(row["var_seq"])
            if needle and needle not in f"{point_name} {row['address']} {row['device_code']}".lower():
                continue
            items.append(
                {
                    "id": int(row["variable_id"]),
                    "deviceId": int(row["device_pk"]),
                    "deviceCode": str(row["device_code"]),
                    "varSeq": int(row["var_seq"]),
                    "pointName": point_name,
                    "ioAddress": str(row["address"]),
                    "dataType": str(row["data_type"]).upper(),
                    "description": "",
                    "isReadonly": True,
                    "driverName": str(row["protocol"]),
                }
            )
        total = len(items)
        start = max(0, page_no - 1) * page_size
        return {
            "list": items[start : start + page_size],
            "total": total,
            "pageNo": page_no,
            "pageSize": page_size,
        }

    def realtime_groups(self, device_code: str) -> list[dict[str, Any]]:
        if not self.path.exists():
            return []
        try:
            with closing(self._connect()) as connection:
                rows = connection.execute(
                    """
                    SELECT group_code, COUNT(*) AS point_count
                    FROM edge_variable
                    WHERE device_code=?
                    GROUP BY group_code
                    ORDER BY group_code
                    """,
                    (device_code,),
                ).fetchall()
        except sqlite3.OperationalError:
            return []
        return [
            {
                "group_code": str(row["group_code"]),
                "group_name": str(row["group_code"]),
                "active_point_count": int(row["point_count"]),
                "max_point_count": int(row["point_count"]),
                "status": "active",
            }
            for row in rows
        ]

    def realtime(self, device_id: int, group_code: str) -> dict[str, Any]:
        if not self.path.exists():
            return self._empty_realtime(device_id)
        try:
            with closing(self._connect()) as connection:
                device = connection.execute(
                    """
                    SELECT d.device_pk, d.device_code, d.protocol, s.status
                    FROM edge_device AS d
                    LEFT JOIN edge_device_status AS s ON s.device_code=d.device_code
                    WHERE d.device_pk=?
                    """,
                    (device_id,),
                ).fetchone()
                if device is None:
                    return self._empty_realtime(device_id)
                points = connection.execute(
                    """
                    SELECT variable_id, var_seq, address, data_type
                    FROM edge_variable
                    WHERE device_code=? AND group_code=?
                    ORDER BY var_seq
                    """,
                    (device["device_code"], group_code),
                ).fetchall()
                latest = self._read_latest_values(
                    connection,
                    str(device["device_code"]),
                    group_code,
                )
        except sqlite3.OperationalError:
            return self._empty_realtime(device_id)
        data = []
        last_timestamp = 0
        for point in points:
            value = latest.get(str(point["variable_id"])) or {}
            timestamp = int(value.get("t") or 0)
            last_timestamp = max(last_timestamp, timestamp)
            data.append(
                {
                    "pointId": int(point["variable_id"]),
                    "varSeq": int(point["var_seq"]),
                    "pointName": str(point["var_seq"]),
                    "ioAddress": str(point["address"]),
                    "value": value.get("v", "--"),
                    "updateTime": self._format_ms(timestamp),
                    "quality": value.get("q"),
                    "dataType": str(point["data_type"]).upper(),
                }
            )
        return {
            "deviceId": int(device["device_pk"]),
            "deviceCode": str(device["device_code"]),
            "deviceName": str(device["device_code"]),
            "driverName": str(device["protocol"]),
            "connectStatus": "online" if int(device["status"] or 0) == 1 else "offline",
            "lastCollectTime": self._format_ms(last_timestamp),
            "redisConnected": False,
            "degraded": True,
            "data": data,
        }

    def latest_values(self, device_code: str, group_code: str) -> dict[str, Any]:
        if not self.path.exists():
            return {"values": {}, "deviceOffline": True}
        try:
            with closing(self._connect()) as connection:
                values = self._read_latest_values(connection, device_code, group_code)
        except sqlite3.OperationalError:
            values = {}
        return {"values": values, "deviceOffline": not bool(values)}

    def _read_latest_values(
        self,
        connection: sqlite3.Connection,
        device_code: str,
        group_code: str,
    ) -> dict[str, Any]:
        try:
            frame = connection.execute(
                """
                SELECT values_json FROM local_latest_frame
                WHERE device_code=? AND group_code=?
                """,
                (device_code, group_code),
            ).fetchone()
        except sqlite3.OperationalError:
            frame = None
        if frame is not None:
            values = self._decode_json(frame["values_json"])
            return dict(values) if isinstance(values, dict) else {}

        rows = connection.execute(
            """
            SELECT var_id, value_json FROM local_latest
            WHERE device_code=? AND group_code=?
            """,
            (device_code, group_code),
        ).fetchall()
        return {
            str(row["var_id"]): self._decode_json(row["value_json"])
            for row in rows
        }

    def history_devices(self) -> list[dict[str, Any]]:
        return [
            {
                "id": item["id"],
                "name": item["deviceCode"],
                "deviceCode": item["deviceCode"],
                "deviceName": item["deviceName"],
                "typeCode": item["deviceType"],
                "typeName": item["deviceType"],
            }
            for item in self.monitor_devices(page_no=1, page_size=100000)["list"]
        ]

    def history_device_types(self) -> list[dict[str, Any]]:
        counts: dict[str, int] = {}
        for device in self.history_devices():
            code = str(device.get("typeCode") or "UNKNOWN")
            counts[code] = counts.get(code, 0) + 1
        return [
            {"typeCode": code, "typeName": code, "deviceCount": count}
            for code, count in sorted(counts.items())
        ]

    def history_points(self, device_id: int) -> list[dict[str, Any]]:
        return [
            {
                "varSeq": item["varSeq"],
                "pointName": item["pointName"],
                "ioAddress": item["ioAddress"],
            }
            for item in self.monitor_points(
                page_no=1, page_size=100000, device_id=device_id
            )["list"]
        ]

    def history_chart(self, query: dict[str, Any]) -> dict[str, Any]:
        device_id = int(query["deviceId"])
        selected = [int(value) for value in query.get("varSeqs") or []]
        start_ms, end_ms = self._history_bounds(query)
        with closing(self._connect()) as connection:
            device_code = self._device_code(connection, device_id)
            point_rows = self._history_point_rows(connection, device_code, selected)
            if not point_rows:
                return {"interval": "local-sampled", "series": []}
            count = int(
                connection.execute(
                    "SELECT COUNT(*) FROM outbox WHERE device_code=? AND ts BETWEEN ? AND ?",
                    (device_code, start_ms, end_ms),
                ).fetchone()[0]
            )
            stride = max(1, math.ceil(count / 5000))
            rows = connection.execute(
                """
                SELECT ts, payload FROM outbox
                WHERE device_code=? AND ts BETWEEN ? AND ?
                  AND (?=1 OR local_seq % ?=0)
                ORDER BY ts
                """,
                (device_code, start_ms, end_ms, stride, stride),
            ).fetchall()
        variable_to_seq = {
            str(row["variable_id"]): int(row["var_seq"]) for row in point_rows
        }
        series_data = {int(row["var_seq"]): [] for row in point_rows}
        for row in rows:
            payload = self._decode_json(row["payload"])
            values = payload.get("values") if isinstance(payload, dict) else None
            if not isinstance(values, dict):
                continue
            timestamp = self._format_ms(row["ts"])
            for variable_id, raw_value in values.items():
                var_seq = variable_to_seq.get(str(variable_id))
                if var_seq is not None:
                    series_data[var_seq].append(
                        [timestamp, self._number_or_none(raw_value)]
                    )
        return {
            "interval": "local-sampled" if stride > 1 else "local-raw",
            "series": [
                {
                    "varSeq": int(point["var_seq"]),
                    "name": str(point["var_seq"]),
                    "unit": "",
                    "dataType": str(point["data_type"]).upper(),
                    "data": series_data[int(point["var_seq"])],
                }
                for point in point_rows
            ],
        }

    def history_matrix(self, query: dict[str, Any]) -> dict[str, Any]:
        device_id = int(query["deviceId"])
        selected = [int(value) for value in query.get("varSeqs") or []]
        start_ms, end_ms = self._history_bounds(query)
        page_size = min(500, max(1, int(query.get("pageSize") or 200)))
        cursor = int(query.get("cursorTs") or start_ms - 1)
        granularity = str(query.get("alignGranularity") or "1s")
        granularity_ms = {
            "100ms": 100,
            "500ms": 500,
            "1s": 1000,
        }.get(granularity, 1000)
        with closing(self._connect()) as connection:
            device_code = self._device_code(connection, device_id)
            point_rows = self._history_point_rows(connection, device_code, selected)
            rows = connection.execute(
                """
                SELECT ts, payload FROM outbox
                WHERE device_code=? AND ts BETWEEN ? AND ? AND ts>?
                ORDER BY ts LIMIT ?
                """,
                (device_code, start_ms, end_ms, cursor, page_size + 1),
            ).fetchall()
        has_more = len(rows) > page_size
        consumed = rows[:page_size]
        variable_to_seq = {
            str(row["variable_id"]): int(row["var_seq"]) for row in point_rows
        }
        matrix: dict[int, dict[str, Any]] = {}
        for row in consumed:
            timestamp = int(row["ts"])
            bucket = timestamp - timestamp % granularity_ms
            target = matrix.setdefault(bucket, {"ts": self._format_ms(bucket)})
            payload = self._decode_json(row["payload"])
            values = payload.get("values") if isinstance(payload, dict) else None
            if not isinstance(values, dict):
                continue
            for variable_id, value in values.items():
                var_seq = variable_to_seq.get(str(variable_id))
                if var_seq is not None:
                    target[f"v{var_seq}"] = value
        columns = [{"prop": "ts", "label": "采样时间"}]
        columns.extend(
            {
                "prop": f"v{int(point['var_seq'])}",
                "label": str(point["var_seq"]),
                "varSeq": int(point["var_seq"]),
                "dataType": str(point["data_type"]).upper(),
            }
            for point in point_rows
        )
        return {
            "columns": columns,
            "rows": [matrix[key] for key in sorted(matrix)],
            "nextCursor": str(consumed[-1]["ts"]) if has_more and consumed else None,
            "meta": {
                "alignGranularity": granularity,
                "fillMode": "none",
                "pageSize": page_size,
            },
        }

    @staticmethod
    def _empty_realtime(device_id: int) -> dict[str, Any]:
        return {
            "deviceId": device_id,
            "deviceCode": "",
            "deviceName": "",
            "driverName": "",
            "connectStatus": "offline",
            "lastCollectTime": "",
            "redisConnected": False,
            "degraded": True,
            "data": [],
        }

    @staticmethod
    def _decode_json(value: Any) -> dict[str, Any]:
        try:
            if isinstance(value, memoryview):
                value = value.tobytes()
            if isinstance(value, bytes):
                value = value.decode("utf-8")
            decoded = json.loads(str(value))
            return decoded if isinstance(decoded, dict) else {}
        except (UnicodeDecodeError, TypeError, ValueError):
            return {}

    @staticmethod
    def _optional_int(value: Any) -> int | None:
        return int(value) if value is not None else None

    @staticmethod
    def _format_ms(value: Any) -> str:
        if value is None or int(value) <= 0:
            return ""
        return datetime.fromtimestamp(int(value) / 1000).isoformat(
            timespec="milliseconds"
        )

    @staticmethod
    def _number_or_none(value: Any) -> float | int | None:
        if isinstance(value, bool):
            return int(value)
        try:
            number = float(value)
        except (TypeError, ValueError):
            return None
        return int(number) if number.is_integer() else number

    @staticmethod
    def _parse_time(value: Any) -> int:
        parsed = datetime.fromisoformat(str(value).replace("Z", "+00:00"))
        return int(parsed.timestamp() * 1000)

    def _history_bounds(self, query: dict[str, Any]) -> tuple[int, int]:
        start_ms = self._parse_time(query["startTime"])
        end_ms = self._parse_time(query["endTime"])
        if end_ms <= start_ms:
            raise ValueError("endTime must be later than startTime")
        return start_ms, end_ms

    @staticmethod
    def _device_code(connection: sqlite3.Connection, device_id: int) -> str:
        row = connection.execute(
            "SELECT device_code FROM edge_device WHERE device_pk=?",
            (device_id,),
        ).fetchone()
        if row is None:
            raise ValueError("device is not available in local snapshot")
        return str(row["device_code"])

    @staticmethod
    def _history_point_rows(
        connection: sqlite3.Connection,
        device_code: str,
        selected: list[int],
    ) -> list[sqlite3.Row]:
        if not selected:
            return []
        placeholders = ",".join("?" for _ in selected)
        return connection.execute(
            f"""
            SELECT variable_id, var_seq, data_type
            FROM edge_variable
            WHERE device_code=? AND var_seq IN ({placeholders})
            ORDER BY var_seq
            """,
            [device_code, *selected],
        ).fetchall()
    def outbox_status(self) -> dict[str, Any]:
        if not self.path.exists():
            return self._empty_outbox()
        try:
            with closing(self._connect()) as connection:
                counts = {
                    str(row["state"]): int(row["count"])
                    for row in connection.execute(
                        "SELECT state, COUNT(*) AS count FROM outbox GROUP BY state"
                    ).fetchall()
                }
                oldest = connection.execute(
                    """
                    SELECT MIN(created_at) AS value FROM outbox
                    WHERE state IN ('pending', 'retry_wait', 'inflight')
                    """
                ).fetchone()
                metadata = {
                    str(row["key"]): str(row["value"])
                    for row in connection.execute(
                        """
                        SELECT key, value FROM metadata
                        WHERE key IN (
                            'upload_session_id', 'last_seq', 'dropped_records',
                            'sent_records_total', 'pending_session_switch_old_id'
                        )
                        """
                    ).fetchall()
                }
                try:
                    interval = connection.execute(
                        """
                        SELECT COALESCE(
                            (
                                SELECT MIN(v.scan_rate_ms)
                                FROM edge_variable AS v
                                INNER JOIN device_assignment AS a
                                    ON a.device_code=v.device_code
                                WHERE a.enabled=1
                            ),
                            (
                                SELECT MIN(scan_rate_ms)
                                FROM device_assignment
                                WHERE enabled=1
                            )
                        ) AS value
                        """
                    ).fetchone()
                except sqlite3.OperationalError:
                    interval = connection.execute(
                        "SELECT MIN(scan_rate_ms) AS value FROM device_assignment WHERE enabled=1"
                    ).fetchone()
        except sqlite3.OperationalError:
            return self._empty_outbox()
        oldest_at = (
            int(oldest["value"])
            if oldest is not None and oldest["value"] is not None
            else None
        )
        return {
            "counts": counts,
            "backlog_count": sum(
                counts.get(name, 0) for name in ("pending", "retry_wait", "inflight")
            ),
            "oldest_backlog_at": oldest_at,
            "upload_session_id": metadata.get("upload_session_id"),
            "last_seq": int(metadata.get("last_seq", "0")),
            "upload_interval_ms": (
                int(interval["value"])
                if interval is not None and interval["value"] is not None
                else None
            ),
            "session_switch_pending": "pending_session_switch_old_id" in metadata,
            "database_bytes": self._database_bytes(),
            "dropped_records": int(metadata.get("dropped_records", "0")),
            "sent_records_total": int(metadata.get("sent_records_total", "0")),
            **self._disk_status(),
        }
    def _connect(self) -> sqlite3.Connection:
        connection = sqlite3.connect(
            # M8 / Edge 17：as_uri() 会转义 # % ? 等字符；`with connection` 只提交不关闭，调用方用 closing()。
            self.path.resolve().as_uri() + "?mode=ro",
            uri=True,
            timeout=2,
        )
        connection.row_factory = sqlite3.Row
        connection.execute("PRAGMA query_only=ON")
        connection.execute("PRAGMA busy_timeout=2000")
        return connection

    def _disk_status(self) -> dict[str, Any]:
        usage = shutil.disk_usage(self.path.parent)
        used_percent = round((usage.used / usage.total) * 100, 2) if usage.total else 0.0
        if used_percent >= 95:
            watermark = "overflow"
        elif used_percent >= 85:
            watermark = "critical"
        elif used_percent >= 70:
            watermark = "warning"
        else:
            watermark = "normal"
        return {
            "disk_total_bytes": int(usage.total),
            "disk_free_bytes": int(usage.free),
            "disk_used_percent": used_percent,
            "watermark": watermark,
        }

    def _database_bytes(self) -> int:
        files = (
            self.path,
            Path(str(self.path) + "-wal"),
            Path(str(self.path) + "-shm"),
        )
        return sum(path.stat().st_size for path in files if path.exists())

    @staticmethod
    def _empty_outbox() -> dict[str, Any]:
        return {
            "counts": {},
            "backlog_count": 0,
            "oldest_backlog_at": None,
            "upload_session_id": None,
            "last_seq": 0,
            "upload_interval_ms": None,
            "session_switch_pending": False,
            "database_bytes": 0,
            "dropped_records": 0,
            "sent_records_total": 0,
            "disk_total_bytes": 0,
            "disk_free_bytes": 0,
            "disk_used_percent": 0.0,
            "watermark": "normal",
        }
