"""T028 — OPC DA 中心采集驱动连接失败错误补定位字段（endpoint/port/serverProgId）。

channel 当前不在 connect() 上下文（它是点位 ID 前缀 {channel}.{device}.{group}.{tag}），
本次不强塞——见 specs/002 tasks 备注。
"""

import logging
from datetime import datetime

import pytest
from unittest.mock import AsyncMock, MagicMock

from packages.opcda_center.opcda_center import driver as opcda_center_driver
from packages.opcda_center.opcda_center.driver import OpcDaCenterDriver


def _patch_pool(monkeypatch, *, connect_result):
    client = MagicMock()
    client.connect_opc_server = AsyncMock(return_value=connect_result)
    pool = MagicMock()
    pool.get_client = AsyncMock(return_value=client)
    monkeypatch.setattr(opcda_center_driver, "get_ipc_pool", lambda: pool)
    return client


@pytest.mark.asyncio
async def test_connect_applies_explicit_read_timeout_per_request(monkeypatch):
    """高-5：客户端按代理端口共享，超时必须按请求传入，不能写回共享对象。"""
    client = _patch_pool(monkeypatch, connect_result=True)
    client.timeout = 10.0
    client.read_items = AsyncMock(return_value={})
    drv = OpcDaCenterDriver()
    await drv.connect({
        "opc_server": "ZPMC.OPCServer",
        "vm_host": "172.27.18.221",
        "read_timeout_ms": 850,
    })
    assert client.timeout == 10.0, "不得修改共享客户端的超时"
    assert client.connect_opc_server.await_args.kwargs["timeout"] == 0.85

    from drivers.base import ReadBlock
    await drv.read([ReadBlock(address="OPD_Server.AGV801.ViewMain.Tag1", count=1, data_type="REAL")])
    assert client.read_items.await_args.kwargs["timeout"] == 0.85


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
    from packages.opcda_center.opcda_center import opcda_ipc_client
    from packages.opcda_center.opcda_center.opcda_ipc_protocol import ResponseStatus

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
    from packages.opcda_center.opcda_center import opcda_ipc_client
    from packages.opcda_center.opcda_center.opcda_ipc_protocol import ResponseStatus
    from core.quality_codes import QUALITY_GOOD
    from drivers.base import DriverSample

    client = opcda_ipc_client.OpcDaIpcClient(host="127.0.0.1", port=9090)
    resp = MagicMock()
    resp.status = ResponseStatus.OK
    resp.data = [
        {"item_id": "tag_ok_1", "value": 12.5, "quality": 192, "timestamp": "2026-01-01T00:00:00"},
        {"item_id": "tag_bad", "value": 99.0, "timestamp": "2026-01-01T00:00:01"},
        {"item_id": "tag_invalid_quality", "value": 88.0, "quality": "bad", "timestamp": "2026-01-01T00:00:01"},
        {"item_id": "tag_ok_2", "value": 7.0, "quality": 192, "timestamp": "2026-01-01T00:00:02"},
    ]
    client._send_request = AsyncMock(return_value=resp)

    with caplog.at_level(logging.WARNING):
        values = await client.read_items(
            ["tag_ok_1", "tag_bad", "tag_invalid_quality", "tag_ok_2"]
        )

    # 现在返回 DriverSample，Good 质量的点位正常返回，解析失败的点位为 None
    assert values["tag_ok_1"] == DriverSample(
        value=12.5,
        quality=QUALITY_GOOD,
        source_timestamp=datetime(2026, 1, 1, 0, 0, 0),
        protocol_quality=192,
    )
    assert values["tag_bad"] is None
    assert values["tag_invalid_quality"] is None
    assert values["tag_ok_2"] == DriverSample(
        value=7.0,
        quality=QUALITY_GOOD,
        source_timestamp=datetime(2026, 1, 1, 0, 0, 2),
        protocol_quality=192,
    )
    assert "跳过无法解析的 OPC DA 结果项" in caplog.text


# ---------------------------------------------------------------------------
# A方-11(b) / P1-007: 连接池借出前仅看 is_connected（内存态），不在锁内 ping
# ---------------------------------------------------------------------------


@pytest.mark.asyncio
async def test_pool_get_client_returns_connected_without_ping():
    """已连接的 client 直接借出，不触发 ping（消除锁内网络往返）。"""
    from packages.opcda_center.opcda_center.opcda_ipc_client import OpcDaIpcClientPool

    pool = OpcDaIpcClientPool()
    existing = MagicMock()
    existing.is_connected = True
    existing.ping = AsyncMock(return_value=True)
    existing.close = AsyncMock()
    pool._clients["127.0.0.1:9090"] = existing

    result = await pool.get_client("127.0.0.1", 9090)

    assert result is existing
    existing.ping.assert_not_awaited()   # 借出不 ping
    existing.close.assert_not_awaited()  # 未重建


@pytest.mark.asyncio
async def test_pool_get_client_rebuilds_when_disconnected(monkeypatch):
    """is_connected=False 的失效连接被清理重建，全程不 ping。"""
    from packages.opcda_center.opcda_center import opcda_ipc_client
    from packages.opcda_center.opcda_center.opcda_ipc_client import OpcDaIpcClientPool

    pool = OpcDaIpcClientPool()
    stale = MagicMock()
    stale.is_connected = False
    stale.ping = AsyncMock(return_value=True)
    stale.close = AsyncMock()
    pool._clients["127.0.0.1:9090"] = stale

    new_client = MagicMock()
    new_client.connect = AsyncMock()
    monkeypatch.setattr(opcda_ipc_client, "OpcDaIpcClient", lambda **kw: new_client)

    result = await pool.get_client("127.0.0.1", 9090)

    assert result is new_client
    stale.close.assert_awaited_once()         # 失效连接被关闭
    stale.ping.assert_not_awaited()           # 即便失效也不 ping（纯内存态）
    new_client.connect.assert_awaited_once()  # 新连接被建立


# ---------------------------------------------------------------------------
# 高-5：取消 / 协议错误后丢弃连接；校验响应 request_id
# ---------------------------------------------------------------------------


async def _start_fake_proxy(handler):
    """按 4 字节长度 + JSON 帧收发的假代理。handler(request_dict) -> list[response_dict]（可延迟）。"""
    import asyncio
    import json

    async def _serve(reader, writer):
        try:
            while True:
                header = await reader.readexactly(4)
                body = await reader.readexactly(int.from_bytes(header, "big"))
                for resp in await handler(json.loads(body)):
                    data = json.dumps(resp).encode("utf-8")
                    writer.write(len(data).to_bytes(4, "big") + data)
                    await writer.drain()
        except (asyncio.IncompleteReadError, ConnectionError):
            pass
        finally:
            writer.close()

    server = await asyncio.start_server(_serve, "127.0.0.1", 0)
    return server, server.sockets[0].getsockname()[1]


def _ok(request_id, data=None):
    return {"request_id": request_id, "status": "ok", "data": data, "error": None}


@pytest.mark.asyncio
async def test_ipc_cancelled_receive_discards_connection():
    import asyncio
    from packages.opcda_center.opcda_center.opcda_ipc_client import OpcDaIpcClient

    async def handler(req):
        await asyncio.sleep(0.2)  # 响应晚到：请求已发出，客户端在读响应时被取消
        return [_ok(req["request_id"], [])]

    server, port = await _start_fake_proxy(handler)
    client = OpcDaIpcClient(host="127.0.0.1", port=port, timeout=5.0)
    try:
        await client.connect()
        task = asyncio.create_task(client.read_items(["tag1"]))
        await asyncio.sleep(0.05)
        task.cancel()
        with pytest.raises(asyncio.CancelledError):
            await task
        assert client.is_connected is False, "取消后缓冲区可能残留旧响应，连接不得复用"
        await asyncio.sleep(0.3)  # 旧响应到达
        assert await client.ping() is False  # 未连接，不会读到上一次的响应
    finally:
        await client.close()
        server.close()
        await server.wait_closed()


@pytest.mark.asyncio
async def test_ipc_response_request_id_mismatch_discards_connection():
    from packages.opcda_center.opcda_center.opcda_ipc_client import OpcDaIpcClient

    async def handler(req):
        return [_ok("stale-request-id", [])]

    server, port = await _start_fake_proxy(handler)
    client = OpcDaIpcClient(host="127.0.0.1", port=port, timeout=5.0)
    try:
        await client.connect()
        values = await client.read_items(["tag1"])
        assert values == {"tag1": None}, "错位响应不得当成本次结果"
        assert client.is_connected is False
    finally:
        await client.close()
        server.close()
        await server.wait_closed()


@pytest.mark.asyncio
async def test_ipc_invalid_json_response_discards_connection():
    import asyncio
    from packages.opcda_center.opcda_center.opcda_ipc_client import OpcDaIpcClient

    async def _serve(reader, writer):
        header = await reader.readexactly(4)
        await reader.readexactly(int.from_bytes(header, "big"))
        writer.write((3).to_bytes(4, "big") + b"{x}")
        await writer.drain()
        await asyncio.sleep(0.5)
        writer.close()

    server = await asyncio.start_server(_serve, "127.0.0.1", 0)
    port = server.sockets[0].getsockname()[1]
    client = OpcDaIpcClient(host="127.0.0.1", port=port, timeout=5.0)
    try:
        await client.connect()
        assert await client.ping() is False
        assert client.is_connected is False
    finally:
        await client.close()
        server.close()
        await server.wait_closed()


@pytest.mark.asyncio
async def test_ipc_per_request_timeout_does_not_touch_shared_timeout():
    import asyncio
    from packages.opcda_center.opcda_center.opcda_ipc_client import OpcDaIpcClient

    async def handler(req):
        await asyncio.sleep(1.0)
        return [_ok(req["request_id"], [])]

    server, port = await _start_fake_proxy(handler)
    client = OpcDaIpcClient(host="127.0.0.1", port=port, timeout=10.0)
    try:
        await client.connect()
        loop = asyncio.get_running_loop()
        started = loop.time()
        values = await client.read_items(["tag1"], timeout=0.1)
        assert loop.time() - started < 0.8
        assert values == {"tag1": None}
        assert client.timeout == 10.0
        assert client.is_connected is False, "超时后迟到的响应会错位，必须丢弃连接"
    finally:
        await client.close()
        server.close()
        await server.wait_closed()
