"""T019 — admin.start_process("collector") 接入 SidecarManager 编排。

直接 await 路由协程 + 轻量 stub（不起真进程、不连真 DB）：
- FakePM 记录 start(name, extra_args) 入参；
- FakeSM 控制 prepare 返回/抛错、暴露 collector_settings_path；
- 缺失 sidecar_manager（T020 接线 bug）保留 AttributeError→500，不伪装 400。

覆盖：无 OPC DA 仅 --config / 有 OPC DA + --port-mapping / writer 不碰 sidecar /
prepare 抛错 400 不启动 / settings 路径 None 400 不 prepare / 已运行 409 不 prepare。
"""

import types

import pytest
from fastapi import HTTPException

from app.routers.admin import start_process
from app.services.sidecar_manager import ProxyStartError


class FakePM:
    def __init__(self, names=("collector", "writer"), running=False):
        self.modules = {n: object() for n in names}
        self._running = running
        self.start_calls = []

    def status(self, name):
        return types.SimpleNamespace(status="running" if self._running else "stopped")

    async def start(self, name, extra_args=None):
        self.start_calls.append((name, extra_args))

    def log_file(self, name):
        return f"/logs/{name}.log"


class FakeLogManager:
    def __init__(self):
        self.attached = []

    def attach(self, name, path, loop):
        self.attached.append(name)


class FakeSM:
    def __init__(self, *, port_mapping_path=None, settings_path="/cfg/settings.toml",
                 raise_exc=None):
        self.collector_settings_path = settings_path
        self._port_mapping_path = port_mapping_path
        self._raise = raise_exc
        self.prepare_calls = 0

    def prepare_for_collector_start(self):
        self.prepare_calls += 1
        if self._raise is not None:
            raise self._raise
        return types.SimpleNamespace(
            port_mapping_path=self._port_mapping_path, proxies=[]
        )


def _req(pm, *, sidecar_manager=None, log_manager=None):
    state = types.SimpleNamespace(pm=pm, log_manager=log_manager or FakeLogManager())
    if sidecar_manager is not None:
        state.sidecar_manager = sidecar_manager  # 不设 → 访问即 AttributeError
    app = types.SimpleNamespace(state=state)
    return types.SimpleNamespace(app=app)


async def test_collector_no_opcda_passes_only_config():
    pm = FakePM()
    sm = FakeSM(port_mapping_path=None, settings_path="/cfg/settings.toml")
    await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert sm.prepare_calls == 1
    assert pm.start_calls == [("collector", ["--config", "/cfg/settings.toml"])]


async def test_collector_with_opcda_passes_config_and_port_mapping():
    pm = FakePM()
    sm = FakeSM(
        port_mapping_path="/rt/opcda_proxy/port_mapping.json",
        settings_path="/cfg/settings.toml",
    )
    await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert pm.start_calls == [(
        "collector",
        ["--config", "/cfg/settings.toml",
         "--port-mapping", "/rt/opcda_proxy/port_mapping.json"],
    )]


async def test_writer_does_not_touch_sidecar_manager():
    pm = FakePM()
    # 故意不在 state 上放 sidecar_manager：writer 路径若误访问会 AttributeError→测试失败
    await start_process("writer", _req(pm), _user="u")
    assert pm.start_calls == [("writer", None)]


async def test_collector_prepare_error_returns_400_and_no_start():
    pm = FakePM()
    sm = FakeSM(raise_exc=ProxyStartError(
        "proxy 端口闸门未通过: endpoint=opcda_center://h port=9090 日志=/logs/proxy_9090/current.log"
    ))
    with pytest.raises(HTTPException) as ei:
        await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert ei.value.status_code == 400
    # 错误信息原样带 endpoint/port/log path，不吞
    assert "9090" in ei.value.detail
    assert "opcda_center://h" in ei.value.detail
    assert "proxy_9090" in ei.value.detail
    assert pm.start_calls == []


async def test_collector_missing_settings_path_returns_400_without_prepare():
    pm = FakePM()
    sm = FakeSM(settings_path=None)
    with pytest.raises(HTTPException) as ei:
        await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert ei.value.status_code == 400
    assert "collector settings path is not configured" in ei.value.detail
    assert sm.prepare_calls == 0  # 未配置就别 prepare、别动 proxy
    assert pm.start_calls == []


async def test_collector_already_running_409_before_prepare():
    pm = FakePM(running=True)
    sm = FakeSM()
    with pytest.raises(HTTPException) as ei:
        await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert ei.value.status_code == 409
    assert sm.prepare_calls == 0  # 已运行不动 proxy
    assert pm.start_calls == []


async def test_collector_already_running_409_even_without_sidecar_manager():
    # 锁死「409 检查先于取 sidecar_manager」：state 上没有 sidecar_manager 也应 409，
    # 而非因取 sidecar 抛 AttributeError。防止以后顺序被改坏。
    pm = FakePM(running=True)
    with pytest.raises(HTTPException) as ei:
        await start_process("collector", _req(pm), _user="u")  # 不放 sidecar_manager
    assert ei.value.status_code == 409
    assert pm.start_calls == []
