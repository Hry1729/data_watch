"""采集链路集成测试"""

import asyncio
from unittest.mock import AsyncMock, patch

import pytest

from collector.executor import TaskExecutor
from collector.scheduler import TaskScheduler
from collector.task import CollectTask
from core.config import CollectorConfig, DeviceConfig, VariableConfig
from data_queue.aggregator import DataAggregator
from data_queue.buffer import DataBuffer, DataPoint
from device.manager import DeviceManager


@pytest.mark.asyncio
async def test_full_collection_flow_with_standard_s7_addresses():
    config = CollectorConfig(
        devices=[
            DeviceConfig(
                device_id="INT001",
                device_type="QC",
                protocol="s7",
                ip="192.168.1.100",
                port=102,
                slot=1,
                scan_rate=1,
            )
        ],
        variables=[
            VariableConfig("pressure", "INT001", "DB100.DBW0", "INT", 1, True, 1),
            VariableConfig("position", "INT001", "%MD1304", "DINT", 1, True, 2),
        ],
        batch_size=10,
        flush_interval=1,
    )

    device_manager = DeviceManager(config)
    data_buffer = DataBuffer(max_size=1000)
    aggregator = DataAggregator(config, data_buffer)

    device_manager.register_devices()
    with patch.object(device_manager, "_create_driver") as mock_create:
        mock_driver = AsyncMock()
        mock_driver.connect = AsyncMock()
        mock_driver.disconnect = AsyncMock()
        mock_driver.read = AsyncMock(
            return_value={
                "DB100.DBW0": [100],
                "%MD1304": [42],
            }
        )
        mock_driver.is_connected = True
        mock_driver.ping = AsyncMock(return_value=True)
        mock_create.return_value = mock_driver

        await device_manager.connect_all()

        scheduler = TaskScheduler()
        executor = TaskExecutor(data_buffer, device_manager=device_manager)
        flushed_points = []

        def flush_callback(points):
            flushed_points.extend(points)

        aggregator.set_output_callback(flush_callback)

        for device in device_manager.get_all_devices():
            for scan_task in device.scan_tasks:
                scheduler.add_task(CollectTask(device=device, scan_task=scan_task))

        await scheduler.start(executor.execute)
        aggregator_task = asyncio.create_task(aggregator.run())
        await asyncio.sleep(1.2)
        await scheduler.stop()
        aggregator.stop()
        await aggregator_task
        await aggregator.flush_to_output()

        assert device_manager.devices["INT001"].status.value == "online"
        assert flushed_points
        assert {p.variable_id for p in flushed_points} == {"pressure", "position"}


@pytest.mark.asyncio
async def test_executor_read_failure_schedules_reconnect():
    config = CollectorConfig(
        devices=[
            DeviceConfig("INT002", "QC", "s7", "192.168.1.100", port=102, slot=1)
        ],
        variables=[
            VariableConfig("pressure", "INT002", "DB100.DBW0", "INT", 1, True, 1)
        ],
    )
    device_manager = DeviceManager(config)
    data_buffer = DataBuffer(max_size=100)
    device_manager.register_devices()

    with patch.object(device_manager, "_create_driver") as mock_create:
        mock_driver = AsyncMock()
        mock_driver.connect = AsyncMock()
        mock_driver.read = AsyncMock(side_effect=Exception("read failed"))
        mock_driver.is_connected = True
        mock_create.return_value = mock_driver

        await device_manager.connect_all()
        executor = TaskExecutor(data_buffer, device_manager=device_manager)
        device = device_manager.devices["INT002"]
        task = CollectTask(device=device, scan_task=device.scan_tasks[0])

        with pytest.raises(Exception, match="read failed"):
            await executor.execute(task)

        assert device.status.value == "error"
        assert "INT002" in device_manager._reconnect_tasks
        await device_manager.disconnect_all()


@pytest.mark.asyncio
async def test_aggregator_to_writer_callback_is_nonblocking():
    config = CollectorConfig(batch_size=1, flush_interval=1)
    buffer = DataBuffer(max_size=10)
    aggregator = DataAggregator(config, buffer)
    submitted = []

    def output(points):
        submitted.extend(points)

    aggregator.set_output_callback(output)
    aggregator.add(DataPoint("DEV001", "var1", 1.0))

    assert await aggregator.flush_to_output() == 1
    assert len(submitted) == 1
