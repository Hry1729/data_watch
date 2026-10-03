"""运行期设备拓扑变更测试（#6）

覆盖 4 个 mock 场景 + 防重复：
1. 新增普通 PLC 设备：注册、连接、生成采集任务。
2. 删除已有设备：driver 断开、reconnect task 取消、从 devices/熔断器移除。
3. 新增 external-push 设备：不连接 PLC、不建轮询任务。
4. 新增 center 设备：调用端口映射、生成任务，不要求真实代理进程。
"""

import asyncio
import copy
import threading
import time
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from device.device import DeviceStatus
from device.manager import DeviceManager
from monitor.performance import PerformanceMetrics


def _empty_manager() -> DeviceManager:
    manager = DeviceManager(CollectorConfig(devices=[], variables=[]))
    manager.register_devices()  # 初始化 _port_mapper，devices 为空
    return manager


def test_create_driver_injects_read_executor():
    """_create_driver 把 read_executor 下发给 S7/GE 阻塞型驱动（§7.3）。"""
    sentinel = object()
    manager = DeviceManager(
        CollectorConfig(devices=[], variables=[]),
        read_executor=sentinel,
    )
    assert manager._create_driver("s7")._read_executor is sentinel
    assert manager._create_driver("ge_srtp")._read_executor is sentinel


@pytest.mark.asyncio
async def test_add_device_normal_plc_connects_and_builds_tasks():
    """场景1：新增普通 PLC 设备 —— 注册、连接、生成采集任务。"""
    manager = _empty_manager()
    dev_cfg = DeviceConfig("NEW_PLC", "QC", "s7", "192.168.1.50", port=102, slot=1)
    var_cfgs = [VariableConfig("v1", "NEW_PLC", "DB100.DBW0", "INT", 1, True, 1)]

    device = manager.add_device(dev_cfg, var_cfgs)

    assert device is not None
    assert "NEW_PLC" in manager.devices
    assert len(device.variables) == 1
    assert len(device.scan_tasks) == 1  # 普通 PLC 生成轮询任务

    with patch.object(manager, "_create_driver") as mock_create:
        mock_driver = AsyncMock()
        mock_driver.connect = AsyncMock()
        mock_driver.is_connected = True
        mock_create.return_value = mock_driver

        ok = await manager.connect_device(device)

    assert ok is True
    assert device.status == DeviceStatus.ONLINE


@pytest.mark.asyncio
async def test_remove_device_cancels_reconnect_and_disconnects():
    """场景2：删除已有设备 —— driver 断开、reconnect task 取消、移除。"""
    manager = _empty_manager()
    dev_cfg = DeviceConfig("DEL_PLC", "QC", "s7", "192.168.1.51", port=102, slot=1)
    var_cfgs = [VariableConfig("v1", "DEL_PLC", "DB100.DBW0", "INT", 1, True, 1)]
    device = manager.add_device(dev_cfg, var_cfgs)

    # 模拟已连接并因故障进入重连
    device.driver = AsyncMock()
    device.driver.disconnect = AsyncMock()
    saved_driver = device.driver
    device.status = DeviceStatus.ERROR
    manager._circuit_breakers["DEL_PLC"] = MagicMock()
    await manager.schedule_reconnect(device)
    assert "DEL_PLC" in manager._reconnect_tasks

    await manager.remove_device("DEL_PLC")

    saved_driver.disconnect.assert_awaited_once()
    assert "DEL_PLC" not in manager._reconnect_tasks
    assert "DEL_PLC" not in manager.devices
    assert "DEL_PLC" not in manager._circuit_breakers


@pytest.mark.asyncio
async def test_device_generation_invalidates_old_tasks_and_is_monotonic():
    manager = _empty_manager()
    dev_cfg = DeviceConfig("GEN_PLC", "QC", "s7", "192.168.1.61", port=102, slot=1)
    var_cfgs = [VariableConfig("v1", "GEN_PLC", "DB100.DBW0", "INT", 1, True, 1)]

    first = manager.add_device(dev_cfg, var_cfgs)
    first_generation = first.generation
    assert manager.is_current_generation("GEN_PLC", first_generation)

    manager.invalidate_device("GEN_PLC")
    assert not manager.is_current_generation("GEN_PLC", first_generation)
    await manager.remove_device("GEN_PLC")

    second = manager.add_device(dev_cfg, var_cfgs)
    assert second.generation > first_generation
    assert manager.is_current_generation("GEN_PLC", second.generation)


def test_invalidate_waits_for_current_generation_action():
    manager = _empty_manager()
    device = manager.add_device(
        DeviceConfig("LOCK_PLC", "QC", "s7", "192.168.1.62", port=102, slot=1),
        [],
    )
    action_started = threading.Event()
    release_action = threading.Event()
    invalidated = threading.Event()

    def action():
        action_started.set()
        assert release_action.wait(timeout=2)

    writer = threading.Thread(
        target=manager.run_if_current_generation,
        args=(device.device_id, device.generation, action),
    )
    writer.start()
    assert action_started.wait(timeout=2)

    def invalidate():
        manager.invalidate_device(device.device_id)
        invalidated.set()

    invalidator = threading.Thread(target=invalidate)
    invalidator.start()
    time.sleep(0.05)
    assert not invalidated.is_set()

    release_action.set()
    writer.join(timeout=2)
    invalidator.join(timeout=2)
    assert invalidated.is_set()
    assert not manager.is_current_generation(device.device_id, device.generation)


@pytest.mark.asyncio
async def test_reconnect_metrics_track_lifecycle():
    metrics = PerformanceMetrics()
    manager = DeviceManager(
        CollectorConfig(devices=[], variables=[]),
        metrics=metrics,
    )
    manager.register_devices()
    device = manager.add_device(
        DeviceConfig("RECONNECT_PLC", "QC", "s7", "192.168.1.52", port=102, slot=1),
        [],
    )
    device.status = DeviceStatus.ERROR
    manager._reconnect_interval = 1
    manager._reconnect_jitter_ratio = 0
    delays = []

    async def fake_sleep(delay):
        delays.append(delay)

    manager._sleep = fake_sleep

    attempts = 0

    async def connect(_device, use_circuit_breaker=True):
        nonlocal attempts
        attempts += 1
        if attempts == 1:
            raise ConnectionError("temporary failure")
        _device.status = DeviceStatus.ONLINE
        return True

    manager.connect_device = connect
    await manager.schedule_reconnect(device)
    await manager._reconnect_tasks[device.device_id]

    snapshot = metrics.snapshot()
    assert snapshot["counters"]["reconnect_scheduled_count"] == 1
    assert snapshot["counters"]["reconnect_attempt_count"] == 2
    assert snapshot["counters"]["reconnect_success_count"] == 1
    assert snapshot["counters"]["reconnect_failure_count"] == 1
    assert snapshot["gauges"]["reconnect_active"] == 0
    assert snapshot["gauges"]["reconnect_peak_active"] == 1
    assert delays == [1, 2]


def test_reconnect_delay_adds_jitter_and_respects_cap():
    manager = _empty_manager()
    manager._reconnect_interval = 30
    manager._reconnect_max_interval = 300
    manager._reconnect_jitter_ratio = 0.2
    manager._random = MagicMock()
    manager._random.uniform.side_effect = lambda _low, high: high

    assert manager._next_reconnect_delay(0) == 36
    assert manager._next_reconnect_delay(1) == 72
    assert manager._next_reconnect_delay(10) == 300


@pytest.mark.asyncio
async def test_add_external_push_device_no_tasks_no_plc():
    """场景3：新增 external-push 设备 —— 不建轮询任务、不连接 PLC。"""
    manager = _empty_manager()
    dev_cfg = DeviceConfig("EXT_PUSH", "AGV", "opcda_sidecar", "127.0.0.1")
    var_cfgs = [VariableConfig("v1", "EXT_PUSH", "OPD.Tag1", "REAL", 1, True, 1)]

    device = manager.add_device(dev_cfg, var_cfgs)

    assert device is not None
    assert device.scan_tasks == []  # 外部推送无轮询任务

    with patch.object(manager, "_create_driver") as mock_create:
        ok = await manager.connect_device(device)

    assert ok is True
    assert device.status == DeviceStatus.ONLINE
    assert device.driver is None  # 不创建 PLC driver
    mock_create.assert_not_called()


@pytest.mark.asyncio
async def test_add_center_device_allocates_port_and_builds_tasks():
    """场景4：新增 center 设备 —— 调用端口映射、生成任务路径正确。"""
    manager = _empty_manager()
    # 用 mock 端口映射器，避免真实绑定端口 / 写映射文件
    fake_mapper = MagicMock()
    fake_mapper.get_port.return_value = 9099
    manager._port_mapper = fake_mapper

    dev_cfg = DeviceConfig(
        "CENTER01", "AGV", "opcda_center", "10.0.0.5",
        endpoint="10.0.0.5", opc_server="OPD.Server.1",
    )
    var_cfgs = [VariableConfig("v1", "CENTER01", "OPD.Server.AGV.Tag1", "REAL", 1, True, 1)]

    device = manager.add_device(dev_cfg, var_cfgs)

    assert device is not None
    fake_mapper.get_port.assert_called_once_with("10.0.0.5")
    assert device.connection_config["proxy_port"] == 9099
    assert len(device.scan_tasks) == 1  # center 协议生成轮询任务


@pytest.mark.asyncio
async def test_add_device_rejects_duplicate():
    """约束3：device_id 已存在时拒绝，不覆盖已有 Device 对象。"""
    manager = _empty_manager()
    dev_cfg = DeviceConfig("DUP", "QC", "s7", "192.168.1.52", port=102, slot=1)
    var_cfgs = [VariableConfig("v1", "DUP", "DB100.DBW0", "INT", 1, True, 1)]

    first = manager.add_device(dev_cfg, var_cfgs)
    assert first is not None

    second = manager.add_device(dev_cfg, var_cfgs)
    assert second is None
    assert manager.devices["DUP"] is first  # 原对象未被覆盖


# ── B3：热替换必须收尾旧代重连任务 ───────────────────────────────────────────


def _candidate_like(device):
    """构造同 device_id 的候选 Device（浅拷贝，驱动置空）。"""
    candidate = copy.copy(device)
    candidate.driver = None
    candidate.active = True
    return candidate


@pytest.mark.asyncio
async def test_swap_device_cancels_old_reconnect_task_and_frees_slot():
    """热替换后旧重连任务必须让出槽位，否则新代永远拿不到重连任务。"""
    manager = _empty_manager()
    dev_cfg = DeviceConfig("SWAP_PLC", "QC", "s7", "192.168.1.71", port=102, slot=1)
    var_cfgs = [VariableConfig("v1", "SWAP_PLC", "DB100.DBW0", "INT", 1, True, 1)]
    old = manager.add_device(dev_cfg, var_cfgs)

    old.driver = AsyncMock()
    old.driver.disconnect = AsyncMock()
    old_driver = old.driver
    old.status = DeviceStatus.ERROR
    manager._sleep = AsyncMock()  # 旧任务不得真正等待
    await manager.schedule_reconnect(old)
    stale_task = manager._reconnect_tasks["SWAP_PLC"]

    candidate = _candidate_like(old)
    await manager.swap_device("SWAP_PLC", candidate)

    assert stale_task.done(), "旧重连任务必须已被收尾"
    assert "SWAP_PLC" not in manager._reconnect_tasks, "旧任务必须让出槽位"
    assert manager.devices["SWAP_PLC"] is candidate
    old_driver.disconnect.assert_awaited_once()

    # 关键：新代不再被旧任务占位挡住，能拿到自己的重连任务。
    manager._sleep = asyncio.sleep
    manager._reconnect_interval = 0.05
    manager._reconnect_max_interval = 0.05
    manager.connect_device = AsyncMock(return_value=False)
    await manager.schedule_reconnect(candidate)
    assert "SWAP_PLC" in manager._reconnect_tasks
    await manager.remove_device("SWAP_PLC")


@pytest.mark.asyncio
async def test_stale_reconnect_loop_stops_even_if_cancel_is_missed():
    """竞态注入：取消没赶上时，旧任务仍须因"不再是当前设备对象"而停止。"""
    manager = _empty_manager()
    dev_cfg = DeviceConfig("STALE_PLC", "QC", "s7", "192.168.1.72", port=102, slot=1)
    var_cfgs = [VariableConfig("v1", "STALE_PLC", "DB100.DBW0", "INT", 1, True, 1)]
    old = manager.add_device(dev_cfg, var_cfgs)

    old.driver = AsyncMock()
    old.status = DeviceStatus.ERROR
    manager._sleep = AsyncMock()
    connect = AsyncMock(return_value=True)
    manager.connect_device = connect
    await manager.schedule_reconnect(old)
    stale_task = manager._reconnect_tasks["STALE_PLC"]

    manager._cancel_reconnect_task = AsyncMock()  # 模拟"取消没赶上"
    candidate = _candidate_like(old)
    await manager.swap_device("STALE_PLC", candidate)

    await asyncio.wait_for(stale_task, timeout=3)
    connect.assert_not_awaited(), "旧代不得再发起连接"
    assert manager.devices["STALE_PLC"] is candidate
