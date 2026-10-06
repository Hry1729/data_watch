"""OPC UA 驱动安全边界测试。"""

import pytest

from packages.opcua.opcua.driver import OPCUADriver


@pytest.mark.asyncio
async def test_opcua_write_is_disabled_in_collector():
    driver = OPCUADriver()

    with pytest.raises(Exception, match="read-only"):
        await driver.write([])


@pytest.mark.asyncio
async def test_opcua_uses_one_network_read_and_preserves_bad_nodes():
    from unittest.mock import AsyncMock, MagicMock
    from types import SimpleNamespace
    driver = OPCUADriver()
    driver._connected = True
    driver._client = MagicMock()
    good = SimpleNamespace(StatusCode=SimpleNamespace(is_good=lambda: True), Value=SimpleNamespace(Value=12))
    bad = SimpleNamespace(StatusCode=SimpleNamespace(is_good=lambda: False), Value=None)
    driver._client.read_attributes = AsyncMock(return_value=[good, bad])
    blocks = [SimpleNamespace(address="a"), SimpleNamespace(address="b")]
    assert await driver.read(blocks) == {"a": [12], "b": None}
    driver._client.read_attributes.assert_awaited_once()
    driver._client.read_attributes.side_effect = ConnectionError("offline")
    with pytest.raises(Exception, match="batch read failed"):
        await driver.read(blocks)
    assert not driver.is_connected
