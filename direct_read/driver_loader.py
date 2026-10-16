"""装载已安装的 S7 驱动。协议实现仍在受控驱动包里，这里只调用注册表。"""

from __future__ import annotations

import os
import sys
from pathlib import Path

from direct_read.errors import DirectReadError
from direct_read.quality import QUALITY_NOT_CONNECTED
from direct_read.runtime import ensure_mvp_runtime


def collector_root() -> Path:
    configured = os.environ.get("DATAWATCH_COLLECTOR_ROOT")
    if configured:
        return Path(configured)
    return Path(__file__).resolve().parents[1] / "collector"


def bootstrap_collector_imports() -> None:
    """把采集契约目录放到导入路径上。不复制驱动代码。"""
    collector = collector_root()
    repo = collector.parent
    if not (collector / "drivers" / "base.py").is_file():
        raise DirectReadError(
            "driver_unavailable",
            "找不到采集侧驱动契约，无法装载 S7",
            quality=QUALITY_NOT_CONNECTED,
        )
    for entry in (str(collector), str(repo)):
        if entry not in sys.path:
            sys.path.insert(0, entry)


def import_read_block():
    bootstrap_collector_imports()
    from drivers.base import ReadBlock

    return ReadBlock


def load_registry():
    ensure_mvp_runtime()
    bootstrap_collector_imports()
    from drivers.registry import DriverRegistry

    return DriverRegistry.discover()


def production_s7_factory():
    """每次调用都 ``registry.create("s7")``，用完由会话断开并丢弃。"""
    ensure_mvp_runtime()
    try:
        registry = load_registry()
    except DirectReadError:
        raise
    except Exception as exc:
        raise DirectReadError(
            "driver_unavailable",
            "S7 驱动注册表不可用",
            quality=QUALITY_NOT_CONNECTED,
        ) from exc
    if not registry.has("s7"):
        reason = registry.failure_reason("s7") if registry.has_failed("s7") else None
        raise DirectReadError(
            "driver_unavailable",
            reason or "未安装 S7 驱动",
            quality=QUALITY_NOT_CONNECTED,
        )
    try:
        return registry.create("s7")
    except Exception as exc:
        raise DirectReadError(
            "driver_unavailable",
            "S7 驱动无法创建",
            quality=QUALITY_NOT_CONNECTED,
        ) from exc
