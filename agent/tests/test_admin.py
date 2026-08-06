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

from app.routers.admin import (
    _raise_driver_error,
    get_config,
    restart_process,
    start_process,
    stop_process,
)
from app.services.driver_installer import DriverInstallError
from app.services.driver_zip import DriverZipError
from app.services.sidecar_manager import ProxyStartError


class FakePM:
    def __init__(self, names=("collector", "writer"), running=False):
        self.modules = {n: object() for n in names}
        self._running = running
        self.runtime_dir = "/runtime"  # 写 shutdown_flag 的同源目录
        self.start_calls = []
        self.start_envs = []
        self.restart_calls = []
        self.restart_envs = []
        self.stop_calls = []

    def status(self, name):
        return types.SimpleNamespace(status="running" if self._running else "stopped")

    async def start(self, name, extra_args=None, env=None):
        self.start_calls.append((name, extra_args))
        self.start_envs.append(env)

    async def restart(self, name, extra_args=None, env=None):
        self.restart_calls.append((name, extra_args))
        self.restart_envs.append(env)

    async def stop(self, name):
        self.stop_calls.append(name)

    def log_file(self, name):
        return f"/logs/{name}.log"


class FakeLogManager:
    def __init__(self):
        self.attached = []
        self.detached = []

    def attach(self, name, path, loop):
        self.attached.append(name)

    def detach(self, name):
        self.detached.append(name)


class FakeSM:
    def __init__(self, *, port_mapping_path=None, settings_path="/cfg/settings.toml",
                 raise_exc=None):
        self.collector_settings_path = settings_path
        self._port_mapping_path = port_mapping_path
        self._raise = raise_exc
        self.prepare_calls = 0
        self.on_stop_calls = 0

    def prepare_for_collector_start(self):
        self.prepare_calls += 1
        if self._raise is not None:
            raise self._raise
        return types.SimpleNamespace(
            port_mapping_path=self._port_mapping_path, proxies=[]
        )

    def on_collector_stop(self):
        self.on_stop_calls += 1


def _req(pm, *, sidecar_manager=None, log_manager=None):
    state = types.SimpleNamespace(pm=pm, log_manager=log_manager or FakeLogManager())
    if sidecar_manager is not None:
        state.sidecar_manager = sidecar_manager  # 不设 → 访问即 AttributeError
    app = types.SimpleNamespace(state=state)
    return types.SimpleNamespace(app=app)


def _existing_settings(tmp_path):
    """守卫改为「settings.toml 不存在→400」后，成功路径需真实存在的配置文件。"""
    p = tmp_path / "settings.toml"
    p.write_text("", encoding="utf-8")
    return str(p)


async def test_collector_no_opcda_passes_only_config(tmp_path):
    pm = FakePM()
    settings = _existing_settings(tmp_path)
    sm = FakeSM(port_mapping_path=None, settings_path=settings)
    await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert sm.prepare_calls == 1
    assert pm.start_calls == [(
        "collector",
        ["--config", settings, "--runtime-dir", "/runtime"],
    )]


async def test_collector_with_opcda_passes_config_and_port_mapping(tmp_path):
    pm = FakePM()
    settings = _existing_settings(tmp_path)
    sm = FakeSM(
        port_mapping_path="/rt/opcda_proxy/port_mapping.json",
        settings_path=settings,
    )
    await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert pm.start_calls == [(
        "collector",
        ["--config", settings,
         "--runtime-dir", "/runtime",
         "--port-mapping", "/rt/opcda_proxy/port_mapping.json"],
    )]


async def test_collector_start_passes_runtime_dir_matching_pm(tmp_path):
    """C7 回归：collector 启动参数必须显式带 --runtime-dir，且值 == pm.runtime_dir
    （Agent 写 shutdown_flag 的同源目录）。否则自定义数据根时两端分叉，collector
    收不到优雅停机 flag 被强杀。"""
    pm = FakePM()
    settings = _existing_settings(tmp_path)
    sm = FakeSM(port_mapping_path=None, settings_path=settings)
    await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    args = pm.start_calls[0][1]
    assert "--runtime-dir" in args
    assert args[args.index("--runtime-dir") + 1] == pm.runtime_dir


def _conn_cfg():
    """Agent config.toml 的连接段样例（mysql/redis/tdengine 全字段 + 密文）。"""
    return {
        "mysql": {"host": "db", "port": 3306, "user": "root",
                  "database": "device_watch", "password_enc": "m-enc"},
        "redis": {"host": "rds", "port": 6379, "password_enc": "r-enc"},
        "tdengine": {"host": "td", "port": 6041, "user": "root",
                     "database": "device_data", "password_enc": "t-enc"},
    }


def _decrypt_stub(value):
    return {"m-enc": "mpw", "r-enc": "rpw", "t-enc": "tpw"}.get(value, "")


async def test_distributed_center_config_uses_ingest_instead_of_collector(monkeypatch):
    monkeypatch.setattr("app.routers.admin.load_config", _conn_cfg)
    monkeypatch.setattr(
        "app.routers.admin.managed_module_names",
        lambda: ("ingest", "writer"),
    )
    monkeypatch.setattr(
        "app.routers.admin._process_config_out",
        lambda name: {"executable": f"/{name}.exe", "working_dir": f"/{name}"},
    )

    result = await get_config(_user="u")

    assert result.collector is None
    assert result.ingest is not None
    assert result.ingest.executable == "/ingest.exe"
    assert result.writer is not None


_COLLECTOR_ENV = {
    "DATAWATCH_MYSQL_HOST": "db", "DATAWATCH_MYSQL_PORT": "3306",
    "DATAWATCH_MYSQL_USER": "root", "DATAWATCH_MYSQL_DATABASE": "device_watch",
    "DATAWATCH_MYSQL_PASSWORD": "mpw",
    "DATAWATCH_REDIS_HOST": "rds", "DATAWATCH_REDIS_PORT": "6379",
    "DATAWATCH_REDIS_PASSWORD": "rpw",
}

_WRITER_ENV = {
    **_COLLECTOR_ENV,
    "DATAWATCH_TDENGINE_HOST": "td", "DATAWATCH_TDENGINE_PORT": "6041",
    "DATAWATCH_TDENGINE_USER": "root", "DATAWATCH_TDENGINE_DATABASE": "device_data",
    "DATAWATCH_TDENGINE_PASSWORD": "tpw",
}


async def test_collector_start_injects_connection_env(tmp_path, monkeypatch):
    monkeypatch.setattr("app.routers.admin.load_config", _conn_cfg)
    monkeypatch.setattr("app.routers.admin.dpapi_decrypt", _decrypt_stub)
    pm = FakePM()
    settings = _existing_settings(tmp_path)
    sm = FakeSM(port_mapping_path=None, settings_path=settings)
    await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert pm.start_envs == [_COLLECTOR_ENV]


async def test_writer_start_injects_connection_env(monkeypatch):
    monkeypatch.setattr("app.routers.admin.load_config", _conn_cfg)
    monkeypatch.setattr("app.routers.admin.dpapi_decrypt", _decrypt_stub)
    pm = FakePM()
    await start_process("writer", _req(pm), _user="u")
    assert pm.start_envs == [_WRITER_ENV]


def test_env_emits_empty_strings_for_missing_fields(monkeypatch):
    # 缺失字段必须以空串显式下发，覆盖父进程可能残留的同名 DATAWATCH_* 变量。
    from app.routers import admin
    monkeypatch.setattr(admin, "load_config", lambda: {"mysql": {"host": "db"}})
    monkeypatch.setattr(admin, "dpapi_decrypt", lambda v: "")
    env = admin._collector_env()
    assert env["DATAWATCH_MYSQL_HOST"] == "db"  # 有值照常下发
    for empty_key in (
        "DATAWATCH_MYSQL_PORT", "DATAWATCH_MYSQL_USER", "DATAWATCH_MYSQL_DATABASE",
        "DATAWATCH_MYSQL_PASSWORD", "DATAWATCH_REDIS_HOST", "DATAWATCH_REDIS_PORT",
        "DATAWATCH_REDIS_PASSWORD",
    ):
        assert env[empty_key] == ""


async def test_collector_start_uses_empty_env_when_password_decrypt_fails(tmp_path, monkeypatch):
    monkeypatch.setattr("app.routers.admin.load_config", lambda: {
        "mysql": {"password_enc": "encrypted"}
    })

    def fail_decrypt(_value):
        raise RuntimeError("dpapi unavailable")

    monkeypatch.setattr("app.routers.admin.dpapi_decrypt", fail_decrypt)
    pm = FakePM()
    settings = _existing_settings(tmp_path)
    sm = FakeSM(port_mapping_path=None, settings_path=settings)
    await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert pm.start_envs == [{}]


async def test_writer_does_not_touch_sidecar_manager(monkeypatch):
    # writer 现在也会收到 Agent 下发的连接 env；此处仅锁定「不访问 sidecar_manager」。
    monkeypatch.setattr("app.routers.admin.load_config", _conn_cfg)
    monkeypatch.setattr("app.routers.admin.dpapi_decrypt", _decrypt_stub)
    pm = FakePM()
    # 故意不在 state 上放 sidecar_manager：writer 路径若误访问会 AttributeError→测试失败
    await start_process("writer", _req(pm), _user="u")
    assert pm.start_calls == [("writer", None)]
    assert pm.start_envs == [_WRITER_ENV]


async def test_collector_prepare_error_returns_400_and_no_start(tmp_path):
    pm = FakePM()
    sm = FakeSM(
        settings_path=_existing_settings(tmp_path),
        raise_exc=ProxyStartError(
            "proxy 端口闸门未通过: endpoint=opcda_center://h port=9090 日志=/logs/proxy_9090/current.log"
        ),
    )
    with pytest.raises(HTTPException) as ei:
        await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert ei.value.status_code == 400
    # 错误信息原样带 endpoint/port/log path，不吞
    assert "9090" in ei.value.detail
    assert "opcda_center://h" in ei.value.detail
    assert "proxy_9090" in ei.value.detail
    assert pm.start_calls == []


async def test_collector_missing_settings_file_returns_400_without_prepare(tmp_path):
    pm = FakePM()
    # 文件不存在(bootstrap 未跑) → 400，且不 prepare、不动 proxy
    sm = FakeSM(settings_path=str(tmp_path / "nope.toml"))
    with pytest.raises(HTTPException) as ei:
        await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert ei.value.status_code == 400
    assert "collector settings.toml not found" in ei.value.detail
    assert sm.prepare_calls == 0
    assert pm.start_calls == []


async def test_collector_already_running_409_before_prepare():
    pm = FakePM(running=True)
    sm = FakeSM()
    with pytest.raises(HTTPException) as ei:
        await start_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert ei.value.status_code == 409
    assert sm.prepare_calls == 0  # 已运行不动 proxy
    assert pm.start_calls == []


async def test_collector_restart_prepares_and_passes_args(tmp_path):
    pm = FakePM()
    settings = _existing_settings(tmp_path)
    sm = FakeSM(
        port_mapping_path="/rt/opcda_proxy/port_mapping.json",
        settings_path=settings,
    )
    await restart_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert sm.prepare_calls == 1
    assert pm.restart_calls == [(
        "collector",
        ["--config", settings,
         "--runtime-dir", "/runtime",
         "--port-mapping", "/rt/opcda_proxy/port_mapping.json"],
    )]
    assert pm.start_calls == []  # restart 走 pm.restart，不是 pm.start


async def test_collector_restart_injects_connection_env(tmp_path, monkeypatch):
    monkeypatch.setattr("app.routers.admin.load_config", _conn_cfg)
    monkeypatch.setattr("app.routers.admin.dpapi_decrypt", _decrypt_stub)
    pm = FakePM()
    settings = _existing_settings(tmp_path)
    sm = FakeSM(port_mapping_path=None, settings_path=settings)
    await restart_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert pm.restart_envs == [_COLLECTOR_ENV]


async def test_writer_restart_does_not_touch_sidecar_manager(monkeypatch):
    monkeypatch.setattr("app.routers.admin.load_config", _conn_cfg)
    monkeypatch.setattr("app.routers.admin.dpapi_decrypt", _decrypt_stub)
    pm = FakePM()
    await restart_process("writer", _req(pm), _user="u")  # 不放 sidecar_manager
    assert pm.restart_calls == [("writer", None)]
    assert pm.restart_envs == [_WRITER_ENV]


async def test_collector_restart_prepare_error_returns_400_and_no_restart(tmp_path):
    # 锁住：restart 路由也不吞 prepare 错误，闸门失败时 collector 不重启
    pm = FakePM()
    sm = FakeSM(
        settings_path=_existing_settings(tmp_path),
        raise_exc=ProxyStartError(
            "proxy 端口闸门未通过: endpoint=opcda_center://h port=9090 日志=/logs/proxy_9090/current.log"
        ),
    )
    with pytest.raises(HTTPException) as ei:
        await restart_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert ei.value.status_code == 400
    assert "9090" in ei.value.detail
    assert "opcda_center://h" in ei.value.detail
    assert "proxy_9090" in ei.value.detail
    assert pm.restart_calls == []


async def test_collector_restart_no_opcda_only_config(tmp_path):
    pm = FakePM()
    settings = _existing_settings(tmp_path)
    sm = FakeSM(port_mapping_path=None, settings_path=settings)
    await restart_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert pm.restart_calls == [(
        "collector",
        ["--config", settings, "--runtime-dir", "/runtime"],
    )]


async def test_collector_already_running_409_even_without_sidecar_manager():
    # 锁死「409 检查先于取 sidecar_manager」：state 上没有 sidecar_manager 也应 409，
    # 而非因取 sidecar 抛 AttributeError。防止以后顺序被改坏。
    pm = FakePM(running=True)
    with pytest.raises(HTTPException) as ei:
        await start_process("collector", _req(pm), _user="u")  # 不放 sidecar_manager
    assert ei.value.status_code == 409
    assert pm.start_calls == []


# ── US5 停止策略（T030-T031, FR-P17）──────────────────────────────────────

async def test_collector_stop_stops_collector_then_proxies():
    pm = FakePM()
    sm = FakeSM()
    await stop_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert pm.stop_calls == ["collector"]
    assert sm.on_stop_calls == 1  # 停模块 → 停 proxy


async def test_writer_stop_does_not_touch_sidecar_manager():
    pm = FakePM()
    await stop_process("writer", _req(pm), _user="u")  # 不放 sidecar_manager
    assert pm.stop_calls == ["writer"]  # 不访问 sidecar → 无 AttributeError


async def test_collector_restart_does_not_stop_proxies(tmp_path):
    # 重启不停 proxy：restart 路由不调 on_collector_stop
    pm = FakePM()
    sm = FakeSM(port_mapping_path=None, settings_path=_existing_settings(tmp_path))
    await restart_process("collector", _req(pm, sidecar_manager=sm), _user="u")
    assert sm.on_stop_calls == 0


def test_driver_4xx_error_keeps_actionable_validation_detail():
    error = DriverZipError("driver.toml 缺少字段 entry", status_code=422)

    with pytest.raises(HTTPException) as exc_info:
        _raise_driver_error(error)

    assert exc_info.value.status_code == 422
    assert exc_info.value.detail == "driver.toml 缺少字段 entry"


def test_driver_5xx_error_hides_path_and_logs_detail(caplog):
    secret_path = r"C:\Users\admin\DataWatch\drivers\demo_drv"
    error = DriverInstallError(f"collector restart failed: {secret_path}", status_code=503)

    with caplog.at_level("ERROR"), pytest.raises(HTTPException) as exc_info:
        try:
            raise error
        except DriverInstallError as caught:
            _raise_driver_error(caught)

    assert exc_info.value.status_code == 503
    assert secret_path not in exc_info.value.detail
    assert secret_path in caplog.text
