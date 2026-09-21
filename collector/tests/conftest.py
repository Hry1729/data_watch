"""pytest配置和fixtures"""

import asyncio
import sys
from pathlib import Path
for _driver in ("s7", "opcua", "modbus_tcp"):
    sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "packages" / _driver / "vendor"))
import pytest
import tempfile
from unittest.mock import AsyncMock, MagicMock

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from core.logger import setup_logger
from device.manager import DeviceManager
from drivers.base import BaseDriver


def pytest_configure(config):
    config.addinivalue_line(
        "markers",
        "integration: requires a real MySQL database and explicit RUN_DB_INTEGRATION=1",
    )


@pytest.fixture(scope="session")
def event_loop():
    """创建事件循环"""
    loop = asyncio.new_event_loop()
    yield loop
    loop.close()


@pytest.fixture
def mock_driver():
    """模拟驱动"""
    driver = AsyncMock(spec=BaseDriver)
    driver.connect = AsyncMock()
    driver.disconnect = AsyncMock()
    driver.read = AsyncMock(return_value={"DB100.DBW0": [100]})
    driver.write = AsyncMock()
    driver.is_connected = True
    driver.ping = AsyncMock(return_value=True)
    return driver


@pytest.fixture
def collector_config():
    """采集配置"""
    return CollectorConfig(
        devices=[
            DeviceConfig(
                device_id="TEST001",
                device_type="QC",
                protocol="s7",
                ip="192.168.1.100",
                port=102,
                slot=1,
                scan_rate=1,
            ),
        ],
        variables=[
            VariableConfig(
                variable_id="test_var",
                device_id="TEST001",
                address="DB100.DBW0",
                data_type="INT",
                scan_rate=1,
                is_storage=True,
                var_seq=1,
            ),
        ],
    )


@pytest.fixture
def device_manager(collector_config):
    """设备管理器"""
    return DeviceManager(collector_config)


@pytest.fixture
def test_logger():
    """测试日志器"""
    setup_logger(level="DEBUG")
    return setup_logger("test")


@pytest.fixture
def temp_dir():
    """临时目录"""
    with tempfile.TemporaryDirectory() as tmp_dir:
        yield tmp_dir


@pytest.fixture(scope="session")
def activated_test_packages(tmp_path_factory):
    import shutil
    from pathlib import Path
    root = tmp_path_factory.mktemp("activated-drivers")
    source = Path(__file__).resolve().parents[2] / "packages"
    for driver_id in ("s7", "ge_srtp", "opcua", "opcda_center", "modbus_tcp"):
        shutil.copytree(source / driver_id, root / "drivers" / driver_id, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
    return root


@pytest.fixture(autouse=True)
def isolated_driver_activation(monkeypatch, activated_test_packages):
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(activated_test_packages))
