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
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from collector.executor import TaskExecutor
from collector.bounded_executor import BoundedExecutor, ExecutorSaturatedError
from collector.task import CollectTask
from core.config import CollectorConfig, DeviceConfig, VariableConfig
from core.exceptions import ReadError
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

    assert redis_writer.write_window_stream.call_count == 1
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

    redis_writer.write_window_stream.assert_not_called()
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

    redis_writer.write_window_stream.assert_not_called()
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
async def test_executor_backpressures_when_parse_executor_is_saturated():
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

    execution = asyncio.create_task(executor.execute(task))
    await asyncio.sleep(0.02)
    assert execution.done() is False
    release.set()
    await blocker
    await execution
    await executor.shutdown()

    redis_writer.write_window_stream.assert_called_once()
    redis_writer.write_latest.assert_called_once()
    assert executor.stats["total_errors"] == 0
    assert metrics.snapshot()["counters"]["parse_executor_wait_count"] == 1


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
    redis_writer.write_window_stream.side_effect = lambda *_args: redis_threads.append(
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
async def test_executor_backpressures_when_redis_executor_is_saturated():
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

    execution = asyncio.create_task(executor.execute(task))
    await asyncio.sleep(0.02)
    assert execution.done() is False
    release.set()
    await blocker
    await execution
    await executor.shutdown()

    redis_writer.write_window_stream.assert_called_once()
    redis_writer.write_latest.assert_called_once()
    assert executor.stats["total_errors"] == 0
    assert metrics.snapshot()["counters"]["redis_executor_wait_count"] == 1


@pytest.mark.asyncio
async def test_executor_redis_stream_failure_does_not_refresh_latest():
    """window+stream 失败 → 错误如实计为一次，write_latest 未被调用（latest 不被假装推进）。"""
    config = _make_single_group_config()
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    _connect_with_read(device_manager, {"DB100.DBB0": [0x00, 0x64]})
    await device_manager.connect_all()

    redis_writer = MagicMock()
    redis_writer.write_window_stream.side_effect = RuntimeError("window+stream write failed")
    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    device = device_manager.devices["EXE001"]
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)

    redis_writer.write_window_stream.assert_called_once()
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

    redis_writer.write_window_stream.assert_called_once()
    redis_writer.write_latest.assert_called_once()
    assert executor.stats["total_reads"] == 1
    assert executor.stats["total_errors"] == 1
    assert device.status.value == "online"
    await device_manager.disconnect_all()


@pytest.mark.asyncio
async def test_executor_redis_multi_group_partial_failure_counted():
    """多 group 场景：group1 全部成功、group2 的 window+stream 失败 → 错误计为一次。

    每 group 的 window+stream / latest 都会按 group 各自调用一次。
    """
    config = _make_multi_group_config()
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    _connect_with_read(device_manager, {"DB100.DBB0": [0x00, 0x01, 0x00, 0x02]})
    await device_manager.connect_all()

    redis_writer = MagicMock()

    def _group_side_effect(*args, **kwargs):
        if args[2] == "g002":
            raise RuntimeError("group2 window+stream failed")
        return None

    redis_writer.write_window_stream.side_effect = _group_side_effect
    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    device = device_manager.devices["EXE_MG"]
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)

    assert redis_writer.write_window_stream.call_count == 2
    assert executor.stats["total_reads"] == 1
    assert executor.stats["total_errors"] == 1
    assert device.status.value == "online"
    await device_manager.disconnect_all()


@pytest.mark.asyncio
async def test_executor_read_saturation_is_backpressure_not_device_error():
    """读池满（driver.read 抛 ExecutorSaturatedError）按背压跳过本周期。

    池满是背压、不是设备故障：不标设备错误、不触发重连（§7.9-5 避免重连风暴）、
    不向调度器抛出，且不计入 read_error_count（区别于真实读失败）。
    """
    config = _make_single_group_config("EXE_READ_SAT")
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    device = device_manager.devices["EXE_READ_SAT"]

    driver = MagicMock()
    driver.is_connected = True
    driver.read = AsyncMock(
        side_effect=ExecutorSaturatedError("plc_read executor capacity reached: 1")
    )
    device.driver = driver

    device_manager.mark_device_error = AsyncMock()
    device_manager.schedule_reconnect = AsyncMock()

    redis_writer = MagicMock()
    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    # 不得向调度器抛出
    await executor.execute(task)

    device_manager.mark_device_error.assert_not_called()
    device_manager.schedule_reconnect.assert_not_called()
    redis_writer.write_window_stream.assert_not_called()
    redis_writer.write_latest.assert_not_called()
    assert executor.stats["total_errors"] == 1
    counters = executor.stats["performance"]["counters"]
    assert counters.get("read_error_count", 0) == 0
    assert counters.get("read_success_count", 0) == 0


def test_format_point_value_bool_serialized_as_numeric_string():
    """bool 必须先于数值判断：str(True)="True" 会让 writer 的 float() 兜底成 0。

    bool 是 int 子类，若按 str() 处理整批 bool 点都会入库成 0；统一转 "1"/"0"，
    与其它数值串一致、可被 float() 正常解析。
    """
    assert TaskExecutor._format_point_value(True) == "1"
    assert TaskExecutor._format_point_value(False) == "0"
    # 非 bool 仍走 str()，保持原行为
    assert TaskExecutor._format_point_value(100) == "100"
    assert TaskExecutor._format_point_value(1.5) == "1.5"
    assert TaskExecutor._format_point_value("ok") == "ok"


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
        for call in redis_writer.write_window_stream.call_args_list
    }
    assert stream_versions == {"g001": 11, "g002": 22}
    await device_manager.disconnect_all()


@pytest.mark.asyncio
async def test_executor_redis_latest_called_after_window_and_stream():
    """回归：write_latest 必须在 write_window_stream 之后被调用。"""
    config = _make_single_group_config()
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    _connect_with_read(device_manager, {"DB100.DBB0": [0x00, 0x64]})
    await device_manager.connect_all()

    redis_writer = MagicMock()
    parent = MagicMock()
    parent.attach_mock(redis_writer.write_window_stream, "write_window_stream")
    parent.attach_mock(redis_writer.write_latest, "write_latest")

    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    device = device_manager.devices["EXE001"]
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)

    method_names = [c[0] for c in parent.mock_calls]
    assert method_names.index("write_latest") > method_names.index("write_window_stream")
    await device_manager.disconnect_all()


def test_normalize_driver_sample():
    """归一化函数覆盖：裸值、DriverSample、非法质量、None 值。"""
    from datetime import datetime
    from collector.executor import _normalize_driver_sample
    from core.quality_codes import QUALITY_GOOD, QUALITY_UNCERTAIN, QUALITY_CONFIG_DECODE_ERROR
    from drivers.base import DriverSample

    default_ts = datetime(2026, 7, 30, 12, 0, 0)

    # 裸值快路径
    value, quality, timestamp, protocol_quality = _normalize_driver_sample(42, default_ts)
    assert value == 42
    assert quality == QUALITY_GOOD
    assert timestamp == default_ts
    assert protocol_quality is None

    # DriverSample with source_timestamp
    sample_ts = datetime(2026, 7, 30, 11, 59, 50)
    sample = DriverSample(value=3.14, quality=QUALITY_UNCERTAIN, source_timestamp=sample_ts, protocol_quality=192)
    value, quality, timestamp, protocol_quality = _normalize_driver_sample(sample, default_ts)
    assert value == 3.14
    assert quality == QUALITY_UNCERTAIN
    assert timestamp == sample_ts
    assert protocol_quality == 192

    # DriverSample without source_timestamp (回落到 default_ts)
    sample2 = DriverSample(value="ok", quality=QUALITY_GOOD)
    value, quality, timestamp, protocol_quality = _normalize_driver_sample(sample2, default_ts)
    assert value == "ok"
    assert quality == QUALITY_GOOD
    assert timestamp == default_ts
    assert protocol_quality is None

    # 非法质量码转 CONFIG_DECODE_ERROR
    sample3 = DriverSample(value=100, quality=999)
    value, quality, timestamp, protocol_quality = _normalize_driver_sample(sample3, default_ts)
    assert value == 100
    assert quality == QUALITY_CONFIG_DECODE_ERROR
    assert timestamp == default_ts

    # None 值（调用方应跳过不构造 DataPoint）
    sample4 = DriverSample(value=None, quality=QUALITY_GOOD)
    value, quality, timestamp, protocol_quality = _normalize_driver_sample(sample4, default_ts)
    assert value is None
    assert quality == QUALITY_GOOD

@pytest.mark.asyncio
async def test_explicit_read_timeout_rejects_late_sample_after_driver_finishes():
    config = _make_single_group_config("EXE_TIMEOUT")
    config.devices[0].read_timeout_ms = 5
    device_manager = DeviceManager(config)
    device_manager.register_devices()
    device = device_manager.devices["EXE_TIMEOUT"]

    finished = asyncio.Event()

    async def slow_read(_blocks):
        await asyncio.sleep(0.02)
        finished.set()
        return {"DB100.DBB0": [0x00, 0x64]}

    driver = MagicMock()
    driver.is_connected = True
    driver.read = AsyncMock(side_effect=slow_read)
    device.driver = driver

    redis_writer = MagicMock()
    executor = TaskExecutor(redis_writer=redis_writer)
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])
    started = asyncio.get_running_loop().time()

    with pytest.raises(ReadError, match="configured timeout"):
        await executor.execute(task)

    assert finished.is_set()
    assert asyncio.get_running_loop().time() - started >= 0.02
    redis_writer.write_window_stream.assert_not_called()
    redis_writer.write_latest.assert_not_called()
    assert executor.stats["performance"]["counters"]["read_timeout_count"] == 1

@pytest.mark.asyncio
async def test_execute_many_coalesces_same_address_into_one_physical_read():
    driver = MagicMock()
    driver.is_connected = True
    driver.read = AsyncMock(return_value={"%R1": [10, 20]})
    device = SimpleNamespace(
        device_id="GE_COALESCE",
        driver=driver,
        connection_config={},
    )
    short_block = SimpleNamespace(address="%R1", count=1, data_type="INT")
    long_block = SimpleNamespace(address="%R1", count=2, data_type="INT")
    tasks = [
        SimpleNamespace(
            task_name="GE_COALESCE_1s",
            device=device,
            scan_task=SimpleNamespace(read_blocks=(short_block,)),
            error_count=0,
        ),
        SimpleNamespace(
            task_name="GE_COALESCE_5s",
            device=device,
            scan_task=SimpleNamespace(read_blocks=(long_block,)),
            error_count=0,
        ),
    ]
    metrics = PerformanceMetrics()
    executor = TaskExecutor(redis_writer=MagicMock(), metrics=metrics)
    executor.execute = AsyncMock()

    await executor.execute_many(tasks)

    driver.read.assert_awaited_once()
    merged_blocks = driver.read.await_args.args[0]
    assert len(merged_blocks) == 1
    assert merged_blocks[0].address == "%R1"
    assert merged_blocks[0].count == 2
    assert executor.execute.await_count == 2
    for call in executor.execute.await_args_list:
        assert call.kwargs["_prefetched_result"] == {"%R1": [10, 20]}
    counters = metrics.snapshot()["counters"]
    assert counters["coalesced_read_group_count"] == 1
    assert counters["coalesced_read_task_count"] == 2
    assert counters["coalesced_read_saved_block_count"] == 1


@pytest.mark.asyncio
async def test_execute_many_physical_read_failure_marks_every_task_once():
    driver = MagicMock()
    driver.is_connected = True
    driver.read = AsyncMock(side_effect=ReadError("connection lost"))
    device = SimpleNamespace(
        device_id="GE_COALESCE_FAIL",
        driver=driver,
        connection_config={},
    )
    block = SimpleNamespace(address="%R1", count=1, data_type="INT")
    tasks = [
        SimpleNamespace(
            task_name=f"GE_COALESCE_FAIL_{rate}s",
            device=device,
            scan_task=SimpleNamespace(read_blocks=(block,)),
            error_count=0,
        )
        for rate in (1, 5)
    ]
    metrics = PerformanceMetrics()
    executor = TaskExecutor(redis_writer=MagicMock(), metrics=metrics)

    with pytest.raises(ReadError, match="connection lost"):
        await executor.execute_many(tasks)

    driver.read.assert_awaited_once()
    assert [task.error_count for task in tasks] == [1, 1]
    counters = metrics.snapshot()["counters"]
    assert counters["read_error_count"] == 1
    assert counters["coalesced_read_error_count"] == 1

def test_partial_block_failure_writes_bad_latest_but_not_history_value():
    from core.quality_codes import QUALITY_BAD, QUALITY_GOOD

    redis_writer = MagicMock()
    executor = TaskExecutor(redis_writer=redis_writer)
    variables = [
        SimpleNamespace(
            variable_id="ok",
            address="block-ok",
            data_type="INT",
            is_storage=True,
            group_code="g001",
            group_config_version=1,
        ),
        SimpleNamespace(
            variable_id="failed",
            address="block-failed",
            data_type="INT",
            is_storage=True,
            group_code="g001",
            group_config_version=1,
        ),
    ]
    scan_task = SimpleNamespace(
        scan_rate=1.0,
        variables=variables,
        result_map={},
    )
    device = SimpleNamespace(
        device_id="PARTIAL",
        device_type="PLC",
    )

    points = executor._parse_result(
        device.device_id,
        scan_task,
        {"block-ok": [12], "block-failed": None},
    )
    by_id = {point.variable_id: point for point in points}

    assert by_id["ok"].value == 12
    assert by_id["ok"].quality == QUALITY_GOOD
    assert by_id["failed"].value is None
    assert by_id["failed"].quality == QUALITY_BAD

    executor._write_to_redis(device, scan_task, points)

    stream_call = redis_writer.write_window_stream.call_args
    assert stream_call.args[7] == {"ok": "12"}
    assert stream_call.args[8] == {"ok": QUALITY_GOOD}
    assert stream_call.args[6]["values"] == {"ok": "12", "failed": ""}
    latest_fields = redis_writer.write_latest.call_args.args[2]
    assert latest_fields["failed"]["v"] == ""
    assert latest_fields["failed"]["q"] == QUALITY_BAD


def test_repeated_failure_system_logs_follow_status_transitions():
    from types import SimpleNamespace
    from device.device import Device, DeviceStatus
    device = Device("FAILED", "QC", "s7", {})
    device.status = DeviceStatus.ONLINE
    task = SimpleNamespace(device=device, task_name="FAILED_1s", scan_task=SimpleNamespace(scan_rate=1, read_blocks=[]))
    executor = TaskExecutor.__new__(TaskExecutor)
    executor._system_logger = MagicMock()
    executor._emit_read_failure(task, RuntimeError("failure"), 10)
    device.status = DeviceStatus.ERROR
    executor._emit_read_failure(task, RuntimeError("failure"), 10)
    executor._system_logger.emit.assert_called_once()
