"""T029 — Agent status 暴露日志路径（collector/proxy）以支撑现场排查（可观测、非阻断）。

不读日志内容、不做错误摘要解析（只给 log_path）。直接 await 路由协程 + 轻量 stub。
"""

import types

from fastapi import WebSocketDisconnect

from app.routers.viewer import process_metrics, tail_logs


class _FakePM:
    def __init__(self):
        self.modules = {
            "collector": object(),
            "writer": object(),
            "ingest": object(),
            "uploader": object(),
        }

    def status(self, name):
        return types.SimpleNamespace(
            name=name, status="running", pid=123, uptime_seconds=10,
            memory_mb=1.5, cpu_percent=2.0, started_at="t", restart_count=0,
        )

    def log_file(self, name):
        return f"/logs/{name}/current.log"


def _req(pm, sidecar_manager=None):
    state = types.SimpleNamespace(pm=pm)
    if sidecar_manager is not None:
        state.sidecar_manager = sidecar_manager
    return types.SimpleNamespace(app=types.SimpleNamespace(state=state))


def _sm(snapshot):
    return types.SimpleNamespace(proxy_log_snapshot=lambda: list(snapshot))


async def test_collector_metrics_includes_log_path_and_proxy_logs():
    pm = _FakePM()
    sm = _sm([("172.27.18.221", 9090, "/logs/proxy_9090/current.log")])
    resp = await process_metrics("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert resp.log_path == "/logs/collector/current.log"
    assert len(resp.proxy_logs) == 1
    item = resp.proxy_logs[0]
    assert item.endpoint == "172.27.18.221"
    assert item.port == 9090
    assert item.log_path == "/logs/proxy_9090/current.log"


async def test_writer_metrics_has_log_path_and_empty_proxy_logs():
    pm = _FakePM()
    # writer 不碰 sidecar_manager：state 上不放也不应报错
    resp = await process_metrics("writer", _req(pm), _user="u")
    assert resp.log_path == "/logs/writer/current.log"
    assert resp.proxy_logs == []


async def test_ingest_metrics_is_a_supported_module():
    pm = _FakePM()
    resp = await process_metrics("ingest", _req(pm), _user="u")
    assert resp.name == "ingest"
    assert resp.log_path == "/logs/ingest/current.log"
    assert resp.proxy_logs == []


async def test_uploader_metrics_is_a_supported_module():
    pm = _FakePM()
    resp = await process_metrics("uploader", _req(pm), _user="u")
    assert resp.name == "uploader"
    assert resp.log_path == "/logs/uploader/current.log"
    assert resp.proxy_logs == []


async def test_collector_metrics_no_proxies_empty_list():
    pm = _FakePM()
    sm = _sm([])
    resp = await process_metrics("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert resp.log_path == "/logs/collector/current.log"
    assert resp.proxy_logs == []


async def test_ingest_log_tail_is_allowed():
    class FakeWebSocket:
        def __init__(self):
            self.accepted = False
            self.closed_code = None

        async def accept(self):
            self.accepted = True

        async def close(self, code):
            self.closed_code = code

        async def receive_json(self):
            raise WebSocketDisconnect()

    websocket = FakeWebSocket()
    await tail_logs("ingest", websocket)
    assert websocket.accepted is True
    assert websocket.closed_code is None


async def test_uploader_log_tail_is_allowed():
    class FakeWebSocket:
        def __init__(self):
            self.accepted = False
            self.closed_code = None

        async def accept(self):
            self.accepted = True

        async def close(self, code):
            self.closed_code = code

        async def receive_json(self):
            raise WebSocketDisconnect()

    websocket = FakeWebSocket()
    await tail_logs("uploader", websocket)
    assert websocket.accepted is True
    assert websocket.closed_code is None
