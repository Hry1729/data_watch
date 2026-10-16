"""质量码与采集侧 ``collector/core/quality_codes.py`` 同值。

直接执行该文件，避免复制一套常量，也避免在导入本包时拉起采集进程。
"""

from __future__ import annotations

import importlib.util
from pathlib import Path


DIRECT_SOURCE = "direct_plc"

_QUALITY_PATH = (
    Path(__file__).resolve().parents[1] / "collector" / "core" / "quality_codes.py"
)


def _load_collector_quality():
    spec = importlib.util.spec_from_file_location(
        "direct_read_collector_quality_codes",
        _QUALITY_PATH,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"找不到采集侧质量码: {_QUALITY_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


_QUALITY = _load_collector_quality()

QUALITY_GOOD = _QUALITY.QUALITY_GOOD
QUALITY_UNCERTAIN = _QUALITY.QUALITY_UNCERTAIN
QUALITY_BAD = _QUALITY.QUALITY_BAD
QUALITY_STALE = _QUALITY.QUALITY_STALE
QUALITY_NOT_CONNECTED = _QUALITY.QUALITY_NOT_CONNECTED
QUALITY_CONFIG_DECODE_ERROR = _QUALITY.QUALITY_CONFIG_DECODE_ERROR
is_valid_quality = _QUALITY.is_valid_quality

_QUALITY_NAMES = {
    QUALITY_GOOD: "GOOD",
    QUALITY_UNCERTAIN: "UNCERTAIN",
    QUALITY_BAD: "BAD",
    QUALITY_STALE: "STALE",
    QUALITY_NOT_CONNECTED: "NOT_CONNECTED",
    QUALITY_CONFIG_DECODE_ERROR: "CONFIG_DECODE_ERROR",
}


def quality_name(code: int) -> str:
    """已知业务码用采集侧同名；保留码只标编号，不另起业务名。"""
    return _QUALITY_NAMES.get(code, f"RESERVED_{code}")
