"""T028 — OPC DA 中心采集驱动连接失败错误补定位字段（endpoint/port/serverProgId）。

channel 当前不在 connect() 上下文（它是点位 ID 前缀 {channel}.{device}.{group}.{tag}），
本次不强塞——见 specs/002 tasks 备注。
"""

import logging

import pytest
from unittest.mock import AsyncMock, MagicMock

from drivers import opcda_center_driver
from drivers.opcda_center_driver import OpcDaCenterDriver


def _patch_pool(monkeypatch, *, connect_result):
    client = MagicMock()
    client.connect_opc_server = AsyncMock(return_value=connect_result)
    pool = MagicMock()
    pool.get_client = AsyncMock(return_value=client)
    monkeypatch.setattr(opcda_center_driver, "get_ipc_pool", lambda: pool)
    return client


@pytest.mark.asyncio
async def test_connect_failure_error_includes_endpoint_port_progid(monkeypatch):
    _patch_pool(monkeypatch, connect_result=False)
    drv = OpcDaCenterDriver()
    with pytest.raises(ConnectionError) as ei:
        await drv.connect({
            "proxy_host": "127.0.0.1",
            "proxy_port": 9090,
            "opc_server": "ZPMC.OPCServer",
            "vm_host": "172.27.18.221",
        })
    msg = str(ei.value)
    assert "9090" in msg                 # proxy port
    assert "ZPMC.OPCServer" in msg       # serverProgId
    assert "172.27.18.221" in msg        # endpoint / vm_host


@pytest.mark.asyncio
async def test_ipc_connect_opc_server_failure_log_has_context(monkeypatch, caplog):
    from core import opcda_ipc_client
    from core.opcda_ipc_protocol import ResponseStatus

    client = opcda_ipc_client.OpcDaIpcClient(host="127.0.0.1", port=9090)
    resp = MagicMock()
    resp.status = ResponseStatus.ERROR
    resp.error = "server down"
    client._send_request = AsyncMock(return_value=resp)

    with caplog.at_level(logging.ERROR):
        ok = await client.connect_opc_server("ZPMC.OPCServer", "172.27.18.221")
    assert ok is False
    text = caplog.text
    assert "ZPMC.OPCServer" in text
    assert "172.27.18.221" in text
    assert "9090" in text


@pytest.mark.asyncio
async def test_ipc_read_items_isolates_bad_result_item(caplog):
    from core import opcda_ipc_client
    from core.opcda_ipc_protocol import ResponseStatus

    client = opcda_ipc_client.OpcDaIpcClient(host="127.0.0.1", port=9090)
    resp = MagicMock()
    resp.status = ResponseStatus.OK
    resp.data = [
        {"item_id": "tag_ok_1", "value": 12.5, "quality": 192, "timestamp": "2026-01-01T00:00:00"},
        {"item_id": "tag_bad", "value": 99.0, "timestamp": "2026-01-01T00:00:01"},
        {"item_id": "tag_ok_2", "value": 7.0, "quality": 192, "timestamp": "2026-01-01T00:00:02"},
    ]
    client._send_request = AsyncMock(return_value=resp)

    with caplog.at_level(logging.WARNING):
        values = await client.read_items(["tag_ok_1", "tag_bad", "tag_ok_2"])

    assert values == {"tag_ok_1": 12.5, "tag_bad": None, "tag_ok_2": 7.0}
    assert "跳过无法解析的 OPC DA 结果项" in caplog.text
