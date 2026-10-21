"""审查 R2（Deferred 补丁）：Collector 向 OPC DA 代理发送预共享令牌。"""
import asyncio
import json

import pytest

from packages.opcda_center.opcda_center.opcda_ipc_client import OpcDaIpcClient


async def _capture_server(captured):
    async def handle(reader, writer):
        n = int.from_bytes(await reader.readexactly(4), "big")
        req = json.loads((await reader.readexactly(n)).decode("utf-8"))
        captured.append(req)
        body = json.dumps({"request_id": req["request_id"], "status": "ok", "data": {"pong": True}}).encode()
        writer.write(len(body).to_bytes(4, "big") + body)
        await writer.drain()
    return await asyncio.start_server(handle, "127.0.0.1", 0)


@pytest.mark.parametrize("env,explicit,expected", [
    ("s3cret", None, "s3cret"),
    ("  s3cret \n", None, "s3cret"),
    (None, "explicit", "explicit"),
    ("   ", None, None),
    (None, None, None),
])
def test_client_sends_trimmed_token_only_when_configured(monkeypatch, env, explicit, expected):
    if env is None:
        monkeypatch.delenv("DATAWATCH_OPCDA_PROXY_TOKEN", raising=False)
    else:
        monkeypatch.setenv("DATAWATCH_OPCDA_PROXY_TOKEN", env)

    async def run():
        captured = []
        server = await _capture_server(captured)
        port = server.sockets[0].getsockname()[1]
        client = OpcDaIpcClient(port=port, auth_token=explicit)
        await client.connect()
        assert await client.ping()
        await client.close()
        server.close()
        await server.wait_closed()
        return captured

    [req] = asyncio.run(run())
    assert req.get("auth_token") == expected
