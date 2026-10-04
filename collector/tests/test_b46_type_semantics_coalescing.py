"""B46.2 回归：同地址不同 data_type 不得合并读取（合并会丢失类型语义）。

已核实的机制：
- `execute_many` 原以 `block.address` 为合并键，冲突时 `count` 大者取胜 ⇒ 同地址不同
  `data_type`（S7 同一字区按 INT 与 TIMER_MS/COUNTER 解码）会丢掉一套类型语义；
- 下游是**地址级**键控：S7 驱动 `_read_blocks` 用 `result[block.address] = values`
  （packages/s7/s7/driver.py:137-142），所以"把 data_type 并入合并键"会产出同地址的多个块，
  它们在结果字典里互相覆盖、谁覆盖谁取决于顺序 ⇒ 不确定性（该尝试已回退）。

现方案：检测到同地址多类型即**放弃合并**、逐任务独立读取 —— 与未合并基线语义/结果一致。
"""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
for _candidate in (REPO_ROOT, REPO_ROOT / "collector"):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from collector.executor import TaskExecutor  # noqa: E402
from drivers.base import ReadBlock  # noqa: E402


class _RecordingMetrics:
    def __init__(self) -> None:
        self.increments: dict[str, int] = {}

    def increment(self, name: str, amount: int = 1) -> None:
        self.increments[name] = self.increments.get(name, 0) + amount

    def set_gauge(self, *_args, **_kwargs) -> None:
        pass

    def record_timing(self, *_args, **_kwargs) -> None:
        pass


class _FakeDriver:
    def __init__(self) -> None:
        self.is_connected = True
        self.calls: list[list[tuple[str, str, int]]] = []

    async def read(self, blocks):
        self.calls.append(
            [(b.address, str(getattr(b, "data_type", "") or "").upper(), b.count) for b in blocks]
        )
        return {b.address: [1] * b.count for b in blocks}


def _task(name: str, device, address: str, data_type: str, count: int = 1):
    return SimpleNamespace(
        task_name=name,
        device=device,
        error_count=0,
        scan_task=SimpleNamespace(
            read_blocks=[ReadBlock(address, count, data_type)],
            result_map={},
        ),
    )


def _executor(driver) -> TaskExecutor:
    executor = TaskExecutor.__new__(TaskExecutor)
    executor._metrics = _RecordingMetrics()
    executor.device_manager = None
    executor._clock = lambda: 0.0
    executor._total_errors = 0
    executor._active_executions = 0
    executor._peak_active_executions = 0
    executor._warn_executor_saturated = lambda *_a, **_k: None
    # 合并路径走到这里即可，避免依赖 redis/解析细节
    executor._write_if_current = lambda *_a, **_k: True
    return executor


# ── 纯函数：冲突检测 ─────────────────────────────────────────────────────────


def test_same_address_same_type_is_not_a_conflict() -> None:
    device = SimpleNamespace(device_id="D1")
    tasks = [
        _task("t1", device, "%R1", "INT", 1),
        _task("t2", device, "%R1", "INT", 4),  # 同类型不同 count：可合并
    ]
    assert TaskExecutor._conflicting_address_types(tasks) == set()


def test_same_address_different_type_is_a_conflict() -> None:
    device = SimpleNamespace(device_id="D1")
    tasks = [
        _task("t1", device, "%R1", "INT"),
        _task("t2", device, "%R1", "COUNTER"),
    ]
    assert TaskExecutor._conflicting_address_types(tasks) == {"%R1"}


def test_data_type_case_is_normalized() -> None:
    device = SimpleNamespace(device_id="D1")
    tasks = [
        _task("t1", device, "%R1", "int"),
        _task("t2", device, "%R1", "INT"),
    ]
    assert TaskExecutor._conflicting_address_types(tasks) == set()


def test_different_addresses_with_different_types_are_not_conflicts() -> None:
    device = SimpleNamespace(device_id="D1")
    tasks = [
        _task("t1", device, "%R1", "INT"),
        _task("t2", device, "%R2", "COUNTER"),
    ]
    assert TaskExecutor._conflicting_address_types(tasks) == set()


# ── 行为：冲突即逐任务读取（驱动调用形态）────────────────────────────────────


@pytest.mark.asyncio
async def test_conflicting_types_fall_back_to_per_task_reads() -> None:
    device = SimpleNamespace(device_id="D1")
    driver = _FakeDriver()
    device.driver = driver
    device.connection_config = {}
    executor = _executor(driver)

    t1 = _task("t1", device, "%R1", "INT")
    t2 = _task("t2", device, "%R1", "COUNTER")

    # 逐任务通路会调用 self.execute(task)；用替身模拟"各自读自己的块"
    async def _per_task(task):
        await executor._read_blocks(task.device, task.scan_task.read_blocks)

    executor.execute = _per_task

    await executor.execute_many([t1, t2])

    # 关键：驱动被调用两次，每次只含该任务自己的解码类型；绝不出现"同一次调用里
    # 同地址两个块"（那会在 result[address] 上互相覆盖）。
    assert driver.calls == [
        [("%R1", "INT", 1)],
        [("%R1", "COUNTER", 1)],
    ]
    assert executor._metrics.increments.get("coalesced_read_type_conflict_count") == 1
    assert t1.error_count == 0 and t2.error_count == 0


@pytest.mark.asyncio
async def test_non_conflicting_blocks_still_coalesce() -> None:
    device = SimpleNamespace(device_id="D1")
    driver = _FakeDriver()
    device.driver = driver
    device.connection_config = {}
    executor = _executor(driver)

    t1 = _task("t1", device, "%R1", "INT", 1)
    t2 = _task("t2", device, "%R1", "INT", 4)  # 同类型更大 count

    await executor.execute_many([t1, t2])

    # 仍然只读一次，并取较大的 count（合并收益保留）
    assert driver.calls == [[("%R1", "INT", 4)]]
    assert "coalesced_read_type_conflict_count" not in executor._metrics.increments
