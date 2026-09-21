from __future__ import annotations

import threading
from typing import Any
from uuid import uuid4

from edge.app.outbox import OutboxStore
from edge.app.storage_client import EdgeStorageClient
from monitor.performance import PerformanceMetrics


class EdgeOutboxWriter:
    storage_path = "sqlite_outbox"

    def __init__(
        self,
        store: OutboxStore | EdgeStorageClient,
        metrics: PerformanceMetrics | None = None,
    ) -> None:
        self.store = store
        self._metrics = metrics or PerformanceMetrics()
        self._pending = threading.local()

    def ping(self) -> None:
        self.store.snapshot_target_seq()

    def close(self) -> None:
        self.store.close()

    def publish_stream_points(self, delta: int) -> None:
        return None

    def write_latest(
        self,
        device_code: str,
        group_code: str,
        fields: dict[str, dict[str, Any]],
    ) -> None:
        pending = getattr(self._pending, "frame", None)
        self._pending.frame = None
        if (
            pending is None
            or pending["device_code"] != device_code
            or pending["group_code"] != group_code
        ):
            self.store.write_local_latest(device_code, group_code, fields)
            return
        if not pending["values"] and not fields:
            self.store.write_local_latest(device_code, group_code, fields)
            return
        self.store.commit_frame(
            batch_id=pending["batch_id"],
            device_code=device_code,
            device_type=pending["device_type"],
            group_code=group_code,
            scan_rate_ms=pending["scan_rate_ms"],
            ts=pending["ts"],
            config_version=pending["config_version"],
            values=pending["values"],
            qualities=pending["qualities"],
            latest_fields=fields,
        )
        self._metrics.increment("stream_points_written", len(pending["values"]))

    def write_window(
        self,
        device_code: str,
        group_code: str,
        scan_rate_ms: int,
        frame: dict,
    ) -> None:
        return None

    def write_stream(
        self,
        device_code: str,
        device_type: str,
        group_code: str,
        scan_rate_ms: int,
        ts: int,
        config_version: int,
        values: dict[str, str],
        qualities: dict[str, int] | None = None,
    ) -> None:
        if not values:
            return
        latest_fields = {
            var_id: {
                "v": value,
                "t": ts,
                "q": int((qualities or {}).get(var_id, 0)),
            }
            for var_id, value in values.items()
        }
        self.store.commit_frame(
            batch_id=str(uuid4()),
            device_code=device_code,
            device_type=device_type,
            group_code=group_code,
            scan_rate_ms=scan_rate_ms,
            ts=ts,
            config_version=config_version,
            values=values,
            qualities=qualities,
            latest_fields=latest_fields,
        )
        self._metrics.increment("stream_points_written", len(values))

    def write_window_stream(
        self,
        device_code: str,
        device_type: str,
        group_code: str,
        scan_rate_ms: int,
        ts: int,
        config_version: int,
        frame: dict,
        stream_values: dict[str, str],
        stream_qualities: dict[str, int] | None = None,
    ) -> None:
        self._pending.frame = {
            "batch_id": str(uuid4()),
            "device_code": device_code,
            "device_type": device_type,
            "group_code": group_code,
            "scan_rate_ms": scan_rate_ms,
            "ts": ts,
            "config_version": config_version,
            "values": stream_values,
            "qualities": stream_qualities,
        }
