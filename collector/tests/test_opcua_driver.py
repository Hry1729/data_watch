"""OPC UA 驱动安全边界测试。"""

import pytest

from drivers.opcua_driver import OPCUADriver


@pytest.mark.asyncio
async def test_opcua_write_is_disabled_in_collector():
    driver = OPCUADriver()

    with pytest.raises(Exception, match="read-only"):
        await driver.write([])
