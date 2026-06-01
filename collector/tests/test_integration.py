"""采集链路集成测试"""

from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from collector.executor import TaskExecutor
from collector.task import CollectTask
from core.config import CollectorConfig, DeviceConfig, VariableConfig
from device.manager import DeviceManager


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
    device_manager.register_devices()

    with patch.object(device_manager, "_create_driver") as mock_create:
        mock_driver = AsyncMock()
        mock_driver.connect = AsyncMock()
        mock_driver.read = AsyncMock(side_effect=Exception("read failed"))
        mock_driver.is_connected = True
        mock_create.return_value = mock_driver

        await device_manager.connect_all()
        executor = TaskExecutor(device_manager=device_manager, redis_writer=MagicMock())
        device = device_manager.devices["INT002"]
        task = CollectTask(device=device, scan_task=device.scan_tasks[0])

        with pytest.raises(Exception, match="read failed"):
            await executor.execute(task)

        assert device.status.value == "error"
        assert "INT002" in device_manager._reconnect_tasks
        await device_manager.disconnect_all()
