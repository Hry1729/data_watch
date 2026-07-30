"""质量码链路端到端测试：驱动 → Executor → Redis Stream v2

验证四条路径：
1. native_quality 驱动返回裸值 → CONFIG_DECODE_ERROR
2. DriverSample(UNCERTAIN) → 保留
3. DriverSample(非法质量) → CONFIG_DECODE_ERROR
4. DriverSample(value=None) → 跳过
"""

from unittest.mock import AsyncMock, MagicMock

import pytest

from collector.executor import TaskExecutor
from collector.task import CollectTask
from core.config import CollectorConfig, DeviceConfig, VariableConfig
from core.quality_codes import QUALITY_GOOD, QUALITY_UNCERTAIN, QUALITY_CONFIG_DECODE_ERROR
from device.manager import DeviceManager
from drivers.base import BaseDriver, DriverSample, ReadBlock


class MockQualityDriver(BaseDriver):
    """测试用驱动：返回混合质量样本"""

    native_quality = True

    async def connect(self, config):
        self._connected = True
        self._config = config

    async def disconnect(self):
        self._connected = False

    async def read(self, blocks):
        result = {}
        for block in blocks:
            addr = block.address
            if addr == "addr_bare":
                result[addr] = 42  # 裸值
            elif addr == "addr_uncertain":
                result[addr] = DriverSample(value=100, quality=QUALITY_UNCERTAIN, protocol_quality=128)
            elif addr == "addr_invalid":
                result[addr] = DriverSample(value=200, quality=999)  # 非法质量
            elif addr == "addr_none":
                result[addr] = DriverSample(value=None, quality=QUALITY_GOOD)  # None 值
        return result

    async def write(self, points):
        pass


def _make_quality_test_config() -> CollectorConfig:
    """使用非 S7 协议，避免触发映射路径"""
    return CollectorConfig(
        devices=[
            DeviceConfig("QT001", "QC", "opcua", "opc.tcp://localhost:4840"),
        ],
        variables=[
            VariableConfig("bare_value", "QT001", "addr_bare", "INT", 1, True, 1),
            VariableConfig("uncertain", "QT001", "addr_uncertain", "INT", 1, True, 2),
            VariableConfig("invalid_q", "QT001", "addr_invalid", "INT", 1, True, 3),
            VariableConfig("none_val", "QT001", "addr_none", "INT", 1, True, 4),
        ],
    )


@pytest.mark.asyncio
async def test_quality_chain_e2e_bare_value_uncertain_invalid_none(monkeypatch):
    """端到端：裸值/Uncertain/非法质量/None 值 → Redis Stream v2"""
    config = _make_quality_test_config()
    device_manager = DeviceManager(config)
    device_manager.register_devices()

    # 注入 mock 驱动
    def _create_mock_driver(protocol):
        return MockQualityDriver()

    monkeypatch.setattr(device_manager, "_create_driver", _create_mock_driver)
    await device_manager.connect_all()

    redis_writer = MagicMock()
    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    device = device_manager.devices["QT001"]
    task = CollectTask(device=device, scan_task=device.scan_tasks[0])

    await executor.execute(task)

    # 验证 write_window_stream 被调用一次
    assert redis_writer.write_window_stream.call_count == 1
    call_args = redis_writer.write_window_stream.call_args

    # 提取 stream_values 和 stream_qualities
    stream_values = call_args[0][7]  # 第 8 个位置参数
    stream_qualities = call_args[0][8]  # 第 9 个位置参数

    # 验证键集合一致（none_val 被跳过）
    assert set(stream_values.keys()) == {"bare_value", "uncertain", "invalid_q"}
    assert set(stream_qualities.keys()) == {"bare_value", "uncertain", "invalid_q"}

    # 验证质量码
    assert stream_qualities["bare_value"] == QUALITY_CONFIG_DECODE_ERROR
    assert stream_qualities["uncertain"] == QUALITY_UNCERTAIN  # 保留
    assert stream_qualities["invalid_q"] == QUALITY_CONFIG_DECODE_ERROR  # 非法 → 转换

    # 验证值
    assert stream_values["bare_value"] == "42"
    assert stream_values["uncertain"] == "100"
    assert stream_values["invalid_q"] == "200"

    await device_manager.disconnect_all()
