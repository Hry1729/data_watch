"""``read_point``：解析拓扑、短连接读一点、断开，返回可序列化结果。"""

from __future__ import annotations

import asyncio
import contextlib
import contextvars
import time
from pathlib import Path
from typing import Any, Callable, Sequence

from direct_read.audit import default_state_dir, record_audit
from direct_read.driver_loader import production_s7_factory
from direct_read.errors import DirectReadError
from direct_read.limits import MAX_CONCURRENCY, MIN_DEVICE_INTERVAL_S, RateLimiter
from direct_read.quality import (
    DIRECT_SOURCE,
    QUALITY_NOT_CONNECTED,
    quality_name,
)
from direct_read.runtime import ensure_mvp_runtime
from direct_read.session import read_once
from direct_read.topology import build_sources, is_s7_protocol, resolve_chain


DEFAULT_TIMEOUT_MS = 5000
MAX_TIMEOUT_MS = 30_000

# 测试替身只走这个私有钩子。公开参数里不提供 driver_factory，避免跳过运行时检查。
_factory_override: contextvars.ContextVar[Callable[[], Any] | None] = contextvars.ContextVar(
    "direct_read_driver_factory",
    default=None,
)


@contextlib.contextmanager
def _override_driver_factory(factory: Callable[[], Any] | None):
    token = _factory_override.set(factory)
    try:
        yield
    finally:
        _factory_override.reset(token)


def read_point(
    device: str,
    point: str,
    *,
    actor: str,
    timeout_ms: int = DEFAULT_TIMEOUT_MS,
    topology: str = "auto",
    snapshot_path: str | None = None,
    endpoint: str | None = None,
    address: str | None = None,
    protocol: str | None = None,
    rack: int | None = None,
    slot: int | None = None,
    data_type: str | None = None,
    port: int | None = None,
    sources: Sequence[Any] | None = None,
    state_dir: str | Path | None = None,
    audit_path: str | Path | None = None,
    now: Callable[[], float] | None = None,
    min_interval_s: float = MIN_DEVICE_INTERVAL_S,
    max_concurrency: int = MAX_CONCURRENCY,
) -> dict[str, Any]:
    """读一个现场点。``actor`` 必须是已经过 CLI 鉴权的用户名，没有白名单旁路。"""
    state_directory = Path(state_dir) if state_dir is not None else default_state_dir()
    audit_file = Path(audit_path) if audit_path is not None else None
    started = time.perf_counter()
    outcome = "ok"
    error_type: str | None = None
    topology_source: str | None = None
    audit_device: str | None = str(device or "").strip() or None
    audit_point: str | None = str(point or "").strip() or None
    actor_name = str(actor or "").strip()
    try:
        _validate_request(device, point, timeout_ms)
        if not actor_name:
            raise DirectReadError(
                "auth_required",
                "请先执行 auth login",
                quality=QUALITY_NOT_CONNECTED,
            )
        # 总预算从拓扑解析开始，覆盖 MySQL、读点和断开。
        deadline = time.monotonic() + int(timeout_ms) / 1000
        chain = list(sources) if sources is not None else build_sources(
            mode=topology,
            snapshot_path=snapshot_path,
            endpoint=endpoint,
            address=address,
            protocol=protocol,
            rack=rack,
            slot=slot,
            data_type=data_type,
            port=port,
        )
        resolved = resolve_chain(
            chain,
            str(device).strip(),
            str(point).strip(),
            deadline=deadline,
        )
        topology_source = resolved.topology_source
        audit_device = resolved.device_code
        audit_point = resolved.point_name or resolved.io_address
        if not is_s7_protocol(resolved.protocol):
            raise DirectReadError(
                "unsupported_protocol",
                f"MVP 仅支持 S7，当前协议为 {resolved.protocol}",
                quality=QUALITY_NOT_CONNECTED,
                details={
                    "topologySource": resolved.topology_source,
                    "protocol": resolved.protocol,
                },
            )
        factory = _factory_override.get()
        if factory is None:
            ensure_mvp_runtime()
            factory = production_s7_factory
        limiter = RateLimiter(
            state_directory,
            now=now,
            min_interval_s=min_interval_s,
            max_concurrency=max_concurrency,
        )
        endpoint_key = f"{resolved.host}:{resolved.port}"
        try:
            permit = limiter.acquire(endpoint_key, device_code=resolved.device_code)
        except DirectReadError as exc:
            exc.details.setdefault("topologySource", resolved.topology_source)
            raise
        try:
            try:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise DirectReadError(
                        "timeout",
                        "直连读取超时",
                        quality=QUALITY_NOT_CONNECTED,
                    )
                read_timeout_ms = max(1, min(int(timeout_ms), int(remaining * 1000)))
                value, quality, elapsed_ms = asyncio.run(
                    read_once(
                        factory,
                        resolved.connection_config(read_timeout_ms),
                        address=resolved.io_address,
                        data_type=resolved.data_type,
                        deadline=deadline,
                    )
                )
            except DirectReadError as exc:
                exc.details.setdefault("topologySource", resolved.topology_source)
                raise
        finally:
            permit.release()
        point_payload: dict[str, Any] = {
            "pointName": resolved.point_name,
            "ioAddress": resolved.io_address,
            "dataType": resolved.data_type,
        }
        if resolved.var_seq is not None:
            point_payload["varSeq"] = resolved.var_seq
        return {
            "source": DIRECT_SOURCE,
            "topologySource": resolved.topology_source,
            "protocol": "s7",
            "device": {
                "deviceCode": resolved.device_code,
                "deviceName": resolved.device_name,
            },
            "point": point_payload,
            "value": value,
            "quality": quality,
            "qualityName": quality_name(quality),
            "timestampMs": int(time.time() * 1000),
            "elapsedMs": elapsed_ms,
            "connection": {
                "endpoint": resolved.host,
                "port": resolved.port,
                "rack": resolved.rack,
                "slot": resolved.slot,
            },
        }
    except DirectReadError as exc:
        outcome = exc.error_type
        error_type = exc.error_type
        topology_source = exc.details.get("topologySource", topology_source)
        raise
    finally:
        record_audit(
            actor=actor_name or None,
            device=audit_device,
            point=audit_point,
            outcome=outcome,
            error_type=error_type,
            elapsed_ms=int((time.perf_counter() - started) * 1000),
            topology_source=topology_source if isinstance(topology_source, str) else None,
            audit_path=audit_file,
            state_dir=state_directory,
        )


def _validate_request(device: str, point: str, timeout_ms: int) -> None:
    if not str(device or "").strip() or not str(point or "").strip():
        raise DirectReadError(
            "usage_error",
            "需要同时提供设备和点位",
            quality=QUALITY_NOT_CONNECTED,
        )
    if type(timeout_ms) is bool or not isinstance(timeout_ms, int):
        raise DirectReadError(
            "usage_error",
            "超时必须是毫秒整数",
            quality=QUALITY_NOT_CONNECTED,
        )
    if timeout_ms < 1 or timeout_ms > MAX_TIMEOUT_MS:
        raise DirectReadError(
            "usage_error",
            "超时必须在 1 到 30000 毫秒之间",
            quality=QUALITY_NOT_CONNECTED,
        )
