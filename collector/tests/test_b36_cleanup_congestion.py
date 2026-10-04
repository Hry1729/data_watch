"""B36 回归（B 半）：清理能力不得被"执行资源拥塞"永久饿死。

已核实的事实：`_safe_disconnect_driver` 原以 `except Exception` 把**包括
`ExecutorSaturatedError` 在内的所有失败一并吞掉只记警告**；而 S7 的 disconnect 也提交到
`main.py:167` 那个有界 PLC 读池（packages/s7/s7/driver.py:87-90 ⇒ run_in_executor）。
池饱和时提交被**立即拒绝** ⇒ 清理被永久丢弃、连接资源永不释放。

现策略：**拥塞**退避重试（设备已被移出调度，其自身读取停止后池会排空，重试可最终成功），
预算耗尽时给出**可见**失败；**非拥塞**失败（确定性驱动/协议错误）不重试，避免放大。
"""

from __future__ import annotations

import asyncio
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
for _candidate in (REPO_ROOT, REPO_ROOT / "collector"):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from collector.bounded_executor import ExecutorSaturatedError  # noqa: E402
from core.config import CollectorConfig  # noqa: E402
from device import manager as manager_module  # noqa: E402
from device.manager import DeviceManager  # noqa: E402


class _RecordingMetrics:
    def __init__(self) -> None:
        self.increments: dict[str, int] = {}

    def increment(self, name: str, amount: int = 1) -> None:
        self.increments[name] = self.increments.get(name, 0) + amount

    def set_gauge(self, *_args, **_kwargs) -> None:
        pass

    def record_timing(self, *_args, **_kwargs) -> None:
        pass


class _Driver:
    """按预设序列抛出/成功；记录被调用次数。"""

    def __init__(self, outcomes) -> None:
        self._outcomes = list(outcomes)
        self.calls = 0

    async def disconnect(self) -> None:
        self.calls += 1
        outcome = self._outcomes.pop(0) if self._outcomes else None
        if isinstance(outcome, BaseException):
            raise outcome
        return None


@pytest.fixture(autouse=True)
def _fast_backoff(monkeypatch):
    """把退避压到 0，使测试不因重试而变慢（逻辑不变）。"""
    monkeypatch.setattr(manager_module, "_DRIVER_DISCONNECT_SATURATION_BACKOFF_SEC", 0.0)


def _manager() -> DeviceManager:
    manager = DeviceManager(CollectorConfig(devices=[], variables=[]))
    manager._metrics = _RecordingMetrics()
    return manager


@pytest.mark.asyncio
async def test_congestion_is_retried_until_cleanup_succeeds() -> None:
    """拥塞后池排空 ⇒ 重试必须最终完成清理。"""
    manager = _manager()
    driver = _Driver([ExecutorSaturatedError("full"), ExecutorSaturatedError("full"), None])

    await manager._safe_disconnect_driver(driver, device_id="D1", reason="test")

    assert driver.calls == 3, "拥塞必须重试直到成功"
    assert manager._metrics.increments.get("driver_disconnect_congestion_retry_count") == 2
    assert "driver_disconnect_congestion_giveup_count" not in manager._metrics.increments


@pytest.mark.asyncio
async def test_persistent_congestion_is_visible_not_silent() -> None:
    """重试预算耗尽必须留下**可见**失败（计数 + error 日志），而不是像修复前那样静默丢弃。"""
    manager = _manager()
    driver = _Driver([ExecutorSaturatedError("full")] * 50)

    await manager._safe_disconnect_driver(driver, device_id="D1", reason="test")

    budget = manager_module._DRIVER_DISCONNECT_SATURATION_RETRIES
    assert driver.calls == budget
    assert manager._metrics.increments.get("driver_disconnect_congestion_giveup_count") == 1


@pytest.mark.asyncio
async def test_non_congestion_failure_is_not_retried() -> None:
    """确定性失败（驱动/协议错误）不得被重试放大。"""
    manager = _manager()
    driver = _Driver([RuntimeError("protocol error")])

    await manager._safe_disconnect_driver(driver, device_id="D1", reason="test")

    assert driver.calls == 1
    assert "driver_disconnect_congestion_retry_count" not in manager._metrics.increments


@pytest.mark.asyncio
async def test_timeout_is_not_retried(monkeypatch) -> None:
    """超时已有独立超时预算，不进入拥塞重试。"""
    manager = _manager()
    monkeypatch.setattr(manager_module, "_DRIVER_DISCONNECT_TIMEOUT_SEC", 0.01)

    class _Hanging:
        def __init__(self) -> None:
            self.calls = 0

        async def disconnect(self) -> None:
            self.calls += 1
            await asyncio.sleep(5)

    driver = _Hanging()
    await manager._safe_disconnect_driver(driver, device_id="D1", reason="test")

    assert driver.calls == 1
    assert "driver_disconnect_congestion_retry_count" not in manager._metrics.increments


@pytest.mark.asyncio
async def test_none_driver_is_a_noop() -> None:
    manager = _manager()
    await manager._safe_disconnect_driver(None, device_id="D1", reason="test")
