from __future__ import annotations

from typing import Any

from edge.app.outbox import OutboxStore
from monitor.performance import PerformanceMetrics


class EdgeOutboxWriter:
    storage_path = "sqlite_outbox"

    def __init__(
        self,
        store: OutboxStore,
        metrics: PerformanceMetrics | None = None,
    ) -> None:
        self.store = store
        self._metrics = metrics or PerformanceMetrics()

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
        self.store.write_local_latest(device_code, group_code, fields)

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
        self.store.append_envelope(
            device_code=device_code,
            device_type=device_type,
            group_code=group_code,
            scan_rate_ms=scan_rate_ms,
            ts=ts,
            config_version=config_version,
            values=values,
            qualities=qualities,
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
        self.write_stream(
            device_code,
            device_type,
            group_code,
            scan_rate_ms,
            ts,
            config_version,
            stream_values,
            stream_qualities,
        )
