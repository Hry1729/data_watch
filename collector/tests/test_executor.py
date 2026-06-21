"""采集执行器单元测试：覆盖 Redis 多段写入的失败语义。

P1-2：executor._write_to_redis 内 window/stream/latest 三段写入非原子。
修复点：写顺序调整为 window -> stream -> latest。latest 必须最后写，
否则 stream/window 失败时 latest 已被刷新，客户端据此误判批次已落库。

本测试验证：
1. 三段全部成功时，_total_errors == 0、_total_reads == 1。
2. stream 失败时（window 已成功、latest 未写）：错误被如实计为一次 _total_errors，
   device 保持 online；write_latest 未被调用（write_latest 不会"假装成功"）。
3. latest 失败时（window、stream 已成功）：错误被如实计为一次 _total_errors，
   device 保持 online。
4. 多 group 场景：group1 全部成功、group2 的 stream 失败，错误仍被计为一次。
5. 顺序回归：write_latest 必须在 write_window 之后被调用。
"""

from __future__ import annotations

import asyncio
import threading
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from collector.executor import TaskExecutor
from collector.bounded_executor import BoundedExecutor
from collector.task import CollectTask
from core.config import CollectorConfig, DeviceConfig, VariableConfig
from device.manager import DeviceManager
from monitor.performance import PerformanceMetrics


def _make_single_group_config(device_id: str = "EXE001") -> CollectorConfig:
    return CollectorConfig(
        devices=[
            DeviceConfig(device_id, "QC", "s7", "192.168.1.100", port=102, slot=1),
        ],
        variables=[
            VariableConfig(
                "pressure",
                device_id,
                "DB100.DBW0",
                "INT",
                1,
                True,
                1,
            ),
        ],
    )


def _make_multi_group_config() -> CollectorConfig:
    return CollectorConfig(
        devices=[
            DeviceConfig("EXE_MG", "QC", "s7", "192.168.1.100", port=102, slot=1),
        ],
        variables=[
            VariableConfig(
                "p1", "EXE_MG", "DB100.DBW0", "INT", 1, True, 1,
                group_code="g001", group_config_version=11,
            ),
            VariableConfig(
                "p2", "EXE_MG", "DB100.DBW2", "INT", 1, True, 2,
                group_code="g002", group_config_version=22,
            ),
        ],
    )


def _connect_with_read(device_manager: DeviceManager, read_payload: dict) -> None:
    """替换 device_manager._create_driver 使其返回注入 read 的 mock driver。"""

    async def _fake_read(blocks):  # noqa: ARG001
        return read_payload

    def _factory(*args, **kwargs):
        mock_driver = AsyncMock()
        mock_driver.connect = AsyncMock()
        mock_driver.read = AsyncMock(side_effect=_fake_read)
        mock_driver.is_connected = True
        return mock_driver

    patcher = patch.object(device_manager, "_create_driver", side_effect=_factory)
    patcher.start()


@pytest.mark.asyncio
async def test_executor_redis_all_writes_succeed_no_error():
    """三段全部成功 → _total_errors == 0、_total_reads == 1。"""
    config = _make_single_group_config()
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    _connect_with_read(device_manager, {"DB100.DBB0": [0x00, 0x64]})
    await device_manager.connect_all()

    redis_writer = MagicMock()
    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    device = device_manager.devices["EXE001"]
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)

    assert redis_writer.write_window.call_count == 1
    assert redis_writer.write_stream.call_count == 1
    assert redis_writer.write_latest.call_count == 1
    assert executor.stats["total_reads"] == 1
    assert executor.stats["total_errors"] == 0
    timings = executor.stats["performance"]["timings"]
    assert timings["read_ms"]["count"] == 1
    assert timings["parse_ms"]["count"] == 1
    assert timings["redis_ms"]["count"] == 1
    await device_manager.disconnect_all()


@pytest.mark.asyncio
async def test_executor_drops_result_when_generation_changes_during_read():
    config = _make_single_group_config("EXE_STALE")
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    device = device_manager.devices["EXE_STALE"]

    async def read_then_invalidate(_blocks):
        device_manager.invalidate_device(device.device_id)
        return {"DB100.DBB0": [0x00, 0x64]}

    driver = MagicMock()
    driver.is_connected = True
    driver.read = AsyncMock(side_effect=read_then_invalidate)
    device.driver = driver

    redis_writer = MagicMock()
    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)

    redis_writer.write_window.assert_not_called()
    redis_writer.write_stream.assert_not_called()
    redis_writer.write_latest.assert_not_called()
    assert executor.stats["performance"]["counters"]["stale_generation_drop_count"] == 1


@pytest.mark.asyncio
async def test_executor_drops_result_when_generation_changes_during_parse():
    config = _make_single_group_config("EXE_PARSE_STALE")
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    device = device_manager.devices["EXE_PARSE_STALE"]

    driver = MagicMock()
    driver.is_connected = True
    driver.read = AsyncMock(return_value={"DB100.DBB0": [0x00, 0x64]})
    device.driver = driver

    redis_writer = MagicMock()
    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    original_parse = executor._parse_result

    def parse_then_invalidate(*args, **kwargs):
        points = original_parse(*args, **kwargs)
        device_manager.invalidate_device(device.device_id)
        return points

    executor._parse_result = MagicMock(side_effect=parse_then_invalidate)
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)

    redis_writer.write_window.assert_not_called()
    redis_writer.write_stream.assert_not_called()
    redis_writer.write_latest.assert_not_called()
    assert executor.stats["performance"]["counters"]["stale_generation_drop_count"] == 1


@pytest.mark.asyncio
async def test_executor_runs_parse_on_injected_worker_executor():
    config = _make_single_group_config("EXE_PARSE_WORKER")
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    device = device_manager.devices["EXE_PARSE_WORKER"]

    driver = MagicMock()
    driver.is_connected = True
    driver.read = AsyncMock(return_value={"DB100.DBB0": [0x00, 0x64]})
    device.driver = driver

    parse_executor = BoundedExecutor(
        name="parse-test",
        max_workers=1,
        max_pending=2,
    )
    executor = TaskExecutor(
        device_manager=device_manager,
        redis_writer=MagicMock(),
        parse_executor=parse_executor,
    )
    event_loop_thread = threading.get_ident()
    parse_threads = []
    original_parse = executor._parse_result

    def record_parse_thread(*args, **kwargs):
        parse_threads.append(threading.get_ident())
        return original_parse(*args, **kwargs)

    executor._parse_result = MagicMock(side_effect=record_parse_thread)
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)
    await executor.shutdown()

    assert parse_threads
    assert parse_threads[0] != event_loop_thread


@pytest.mark.asyncio
async def test_executor_rejects_cycle_when_parse_executor_is_saturated():
    config = _make_single_group_config("EXE_PARSE_FULL")
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    device = device_manager.devices["EXE_PARSE_FULL"]

    driver = MagicMock()
    driver.is_connected = True
    driver.read = AsyncMock(return_value={"DB100.DBB0": [0x00, 0x64]})
    device.driver = driver

    started = threading.Event()
    release = threading.Event()
    metrics = PerformanceMetrics()
    parse_executor = BoundedExecutor(
        name="parse",
        max_workers=1,
        max_pending=1,
        metrics=metrics,
    )

    def occupy_executor():
        started.set()
        release.wait(timeout=5)

    blocker = asyncio.create_task(parse_executor.run(occupy_executor))
    await asyncio.to_thread(started.wait, 2)

    redis_writer = MagicMock()
    executor = TaskExecutor(
        device_manager=device_manager,
        redis_writer=redis_writer,
        metrics=metrics,
        parse_executor=parse_executor,
    )
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)
    release.set()
    await blocker
    await executor.shutdown()

    redis_writer.write_window.assert_not_called()
    redis_writer.write_stream.assert_not_called()
    redis_writer.write_latest.assert_not_called()
    assert executor.stats["total_errors"] == 1
    assert metrics.snapshot()["counters"]["parse_executor_reject_count"] == 1


@pytest.mark.asyncio
async def test_executor_runs_redis_on_injected_worker_executor():
    config = _make_single_group_config("EXE_REDIS_WORKER")
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    device = device_manager.devices["EXE_REDIS_WORKER"]

    driver = MagicMock()
    driver.is_connected = True
    driver.read = AsyncMock(return_value={"DB100.DBB0": [0x00, 0x64]})
    device.driver = driver

    redis_threads = []
    redis_writer = MagicMock()
    redis_writer.write_window.side_effect = lambda *_args: redis_threads.append(
        threading.get_ident()
    )
    redis_executor = BoundedExecutor(
        name="redis-test",
        max_workers=1,
        max_pending=2,
    )
    executor = TaskExecutor(
        device_manager=device_manager,
        redis_writer=redis_writer,
        redis_executor=redis_executor,
    )
    event_loop_thread = threading.get_ident()
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)
    await executor.shutdown()

    assert redis_threads
    assert redis_threads[0] != event_loop_thread


@pytest.mark.asyncio
async def test_executor_rejects_cycle_when_redis_executor_is_saturated():
    config = _make_single_group_config("EXE_REDIS_FULL")
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    device = device_manager.devices["EXE_REDIS_FULL"]

    driver = MagicMock()
    driver.is_connected = True
    driver.read = AsyncMock(return_value={"DB100.DBB0": [0x00, 0x64]})
    device.driver = driver

    started = threading.Event()
    release = threading.Event()
    metrics = PerformanceMetrics()
    redis_executor = BoundedExecutor(
        name="redis",
        max_workers=1,
        max_pending=1,
        metrics=metrics,
    )

    def occupy_executor():
        started.set()
        release.wait(timeout=5)

    blocker = asyncio.create_task(redis_executor.run(occupy_executor))
    await asyncio.to_thread(started.wait, 2)

    redis_writer = MagicMock()
    executor = TaskExecutor(
        device_manager=device_manager,
        redis_writer=redis_writer,
        metrics=metrics,
        redis_executor=redis_executor,
    )
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)
    release.set()
    await blocker
    await executor.shutdown()

    redis_writer.write_window.assert_not_called()
    redis_writer.write_stream.assert_not_called()
    redis_writer.write_latest.assert_not_called()
    assert executor.stats["total_errors"] == 1
    assert metrics.snapshot()["counters"]["redis_executor_reject_count"] == 1


@pytest.mark.asyncio
async def test_executor_redis_stream_failure_does_not_refresh_latest():
    """stream 失败 → 错误如实计为一次，write_latest 未被调用（latest 不被假装推进）。"""
    config = _make_single_group_config()
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    _connect_with_read(device_manager, {"DB100.DBB0": [0x00, 0x64]})
    await device_manager.connect_all()

    redis_writer = MagicMock()
    redis_writer.write_stream.side_effect = RuntimeError("stream write failed")
    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    device = device_manager.devices["EXE001"]
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)

    redis_writer.write_window.assert_called_once()
    redis_writer.write_stream.assert_called_once()
    redis_writer.write_latest.assert_not_called()
    assert executor.stats["total_reads"] == 1
    assert executor.stats["total_errors"] == 1
    assert device.status.value == "online"
    await device_manager.disconnect_all()


@pytest.mark.asyncio
async def test_executor_redis_latest_failure_counted_as_error():
    """latest 失败（window/stream 已成功） → 错误如实计为一次，device 保持 online。"""
    config = _make_single_group_config()
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    _connect_with_read(device_manager, {"DB100.DBB0": [0x00, 0x64]})
    await device_manager.connect_all()

    redis_writer = MagicMock()
    redis_writer.write_latest.side_effect = RuntimeError("latest write failed")
    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    device = device_manager.devices["EXE001"]
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)

    redis_writer.write_window.assert_called_once()
    redis_writer.write_stream.assert_called_once()
    redis_writer.write_latest.assert_called_once()
    assert executor.stats["total_reads"] == 1
    assert executor.stats["total_errors"] == 1
    assert device.status.value == "online"
    await device_manager.disconnect_all()


@pytest.mark.asyncio
async def test_executor_redis_multi_group_partial_failure_counted():
    """多 group 场景：group1 全部成功、group2 的 stream 失败 → 错误计为一次。

    每 group 的 window/stream/latest 都会按 group 各自调用一次。
    """
    config = _make_multi_group_config()
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    _connect_with_read(device_manager, {"DB100.DBB0": [0x00, 0x01, 0x00, 0x02]})
    await device_manager.connect_all()

    redis_writer = MagicMock()

    def _stream_side_effect(*args, **kwargs):
        if args[2] == "g002":
            raise RuntimeError("group2 stream failed")
        return None

    redis_writer.write_stream.side_effect = _stream_side_effect
    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    device = device_manager.devices["EXE_MG"]
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)

    assert redis_writer.write_window.call_count == 2
    assert redis_writer.write_stream.call_count == 2
    assert executor.stats["total_reads"] == 1
    assert executor.stats["total_errors"] == 1
    assert device.status.value == "online"
    await device_manager.disconnect_all()


@pytest.mark.asyncio
async def test_executor_stream_uses_group_config_version_per_group():
    """多 group 设备写 stream 时，各组使用自己的 device_point_group.config_version。"""
    config = _make_multi_group_config()
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    _connect_with_read(device_manager, {"DB100.DBB0": [0x00, 0x01, 0x00, 0x02]})
    await device_manager.connect_all()

    redis_writer = MagicMock()
    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    device = device_manager.devices["EXE_MG"]
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)

    stream_versions = {
        call.args[2]: call.args[5]
        for call in redis_writer.write_stream.call_args_list
    }
    assert stream_versions == {"g001": 11, "g002": 22}
    await device_manager.disconnect_all()


@pytest.mark.asyncio
async def test_executor_redis_latest_called_after_window_and_stream():
    """回归：write_latest 必须在 write_window 与 write_stream 之后被调用。"""
    config = _make_single_group_config()
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    _connect_with_read(device_manager, {"DB100.DBB0": [0x00, 0x64]})
    await device_manager.connect_all()

    redis_writer = MagicMock()
    parent = MagicMock()
    parent.attach_mock(redis_writer.write_window, "write_window")
    parent.attach_mock(redis_writer.write_stream, "write_stream")
    parent.attach_mock(redis_writer.write_latest, "write_latest")

    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    device = device_manager.devices["EXE001"]
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)

    method_names = [c[0] for c in parent.mock_calls]
    assert method_names.index("write_latest") > method_names.index("write_window")
    assert method_names.index("write_latest") > method_names.index("write_stream")
    await device_manager.disconnect_all()
