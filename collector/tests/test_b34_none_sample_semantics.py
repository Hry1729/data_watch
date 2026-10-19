"""B34 回归：区分"没有新样本 / 读取失败 / 解码失败 / 合法零值"。

修复前的两个缺口：
1. ``_normalize_driver_sample`` 把驱动的**裸 None** 归一化成 ``QUALITY_GOOD``，与
   ``MISSING_RESULT``（契约中专用的"本轮无新样本"哨兵）无法区分；
2. ``_parse_result`` 对任何 ``value is None`` 都无条件 ``continue``，于是
   ``DriverSample(value=None, quality=QUALITY_BAD)``（驱动显式声明的读取失败）**完全不可见**：
   既不计数也不告警，质量语义被丢弃。

现在的语义：
- ``MISSING_RESULT``           → 显式发出 value=None + QUALITY_BAD 的标记点（保持既有行为）
- ``DriverSample(None, GOOD)`` → 本轮无新样本，静默跳过（不计数）
- ``DriverSample(None, 坏质量)``→ **可见**：计数 + 告警，发出 value=None 的质量点
  （高-3：只更新 latest 质量，不进 stream，不伪造值）
- 裸 ``None``                  → 归一化为坏质量后按上一行走（不再伪装成 GOOD）
- 合法零值（0 / 0.0 / False / ""）→ QUALITY_GOOD 且**原值保留**，绝不与 None 混淆
"""

from __future__ import annotations

import sys
from datetime import datetime
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
for _candidate in (REPO_ROOT, REPO_ROOT / "collector"):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from collector.executor import TaskExecutor, _normalize_driver_sample  # noqa: E402
from core.quality_codes import (  # noqa: E402
    QUALITY_BAD,
    QUALITY_GOOD,
    QUALITY_NOT_CONNECTED,
    QUALITY_STALE,
)
from drivers.base import MISSING_RESULT, DriverSample  # noqa: E402

NOW = datetime(2026, 10, 4, 0, 0, 0)


class _Variable:
    def __init__(self, variable_id: str) -> None:
        self.variable_id = variable_id


class _ScanTask:
    def __init__(self, decoded: dict) -> None:
        self.result_map = {"v1": {"protocol": "http_push"}}
        self.variables = [_Variable("v1")]
        self.decode_read_results = lambda result, result_map: decoded


class _RecordingMetrics:
    def __init__(self) -> None:
        self.increments: dict[str, int] = {}

    def increment(self, name: str, amount: int = 1) -> None:
        self.increments[name] = self.increments.get(name, 0) + amount

    def set_gauge(self, *_args, **_kwargs) -> None:
        pass

    def record_timing(self, *_args, **_kwargs) -> None:
        pass


def _executor(metrics: _RecordingMetrics) -> TaskExecutor:
    executor = TaskExecutor.__new__(TaskExecutor)
    executor._metrics = metrics
    return executor


# ── 契约层：归一化 ───────────────────────────────────────────────────────────


def test_bare_none_is_marked_bad_not_good() -> None:
    value, quality, timestamp, _protocol = _normalize_driver_sample(None, NOW, False)
    assert value is None
    assert quality == QUALITY_BAD, "裸 None 不得伪装成 QUALITY_GOOD"
    assert timestamp == NOW


@pytest.mark.parametrize("raw", [0, 0.0, False, "", 0.0 + 0])
def test_legal_zero_values_keep_quality_good_and_the_value(raw) -> None:
    value, quality, _timestamp, _protocol = _normalize_driver_sample(raw, NOW, False)
    assert quality == QUALITY_GOOD
    assert value == raw, "合法零值必须原样保留，不得被当成 None"
    assert value is not None


def test_driver_sample_none_keeps_its_declared_quality() -> None:
    _value, quality, _timestamp, _protocol = _normalize_driver_sample(
        DriverSample(None, quality=QUALITY_BAD), NOW, True
    )
    assert quality == QUALITY_BAD


# ── 行为层：_parse_result 的可见性 ───────────────────────────────────────────


def test_no_new_sample_is_silent() -> None:
    """DriverSample(None) 默认 GOOD ⇒ 本轮无新样本：不发点、不计数。"""
    metrics = _RecordingMetrics()
    points = _executor(metrics)._parse_result(
        "DEV1", _ScanTask({"v1": DriverSample(None)}), {}, native_quality=True
    )
    assert points == []
    assert "missing_sample_quality_count" not in metrics.increments


@pytest.mark.parametrize("quality", [QUALITY_BAD, QUALITY_STALE, QUALITY_NOT_CONNECTED])
def test_declared_read_failure_is_visible_but_not_fabricated(quality: int) -> None:
    metrics = _RecordingMetrics()
    points = _executor(metrics)._parse_result(
        "DEV1",
        _ScanTask({"v1": DriverSample(None, quality=quality)}),
        {},
        native_quality=True,
    )
    assert len(points) == 1, "读取失败必须以质量点可见"
    assert points[0].value is None, "读取失败不得伪造出点位值"
    assert points[0].quality == quality
    assert metrics.increments.get("missing_sample_quality_count") == 1, "失败必须可见"


def test_bare_none_through_parse_result_is_visible() -> None:
    metrics = _RecordingMetrics()
    points = _executor(metrics)._parse_result(
        "DEV1", _ScanTask({"v1": None}), {}, native_quality=False
    )
    assert len(points) == 1
    assert points[0].value is None
    assert points[0].quality == QUALITY_BAD
    assert metrics.increments.get("missing_sample_quality_count") == 1


def test_missing_result_still_emits_an_explicit_marker_point() -> None:
    """MISSING_RESULT 与裸 None 语义不同：前者是显式标记点，后者是读取失败。"""
    metrics = _RecordingMetrics()
    points = _executor(metrics)._parse_result(
        "DEV1", _ScanTask({"v1": MISSING_RESULT}), {}, native_quality=True
    )
    assert len(points) == 1
    assert points[0].value is None
    assert points[0].quality == QUALITY_BAD


def test_legal_zero_still_produces_a_point() -> None:
    metrics = _RecordingMetrics()
    points = _executor(metrics)._parse_result(
        "DEV1", _ScanTask({"v1": DriverSample(0)}), {}, native_quality=True
    )
    assert len(points) == 1
    assert points[0].value == 0
    assert points[0].quality == QUALITY_GOOD

