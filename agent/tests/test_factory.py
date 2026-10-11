"""T020 — factory.build_sidecar_manager 纯函数构建 SidecarManager。

纯函数：只吃 (cfg, runtime_dir)，不依赖 app.state，便于单测。
约定：collector_settings_path = working_dir/config/settings.toml（供 admin 拼 --config）；
[opcda] 缺省走 ZPMC.OPCServer/OPD_Server，有则覆盖；install_root frozen-aware。
"""

import sys
import pytest

from app.factory import build_sidecar_manager
from app.services.sidecar_manager import SidecarManager


def _cfg(**over):
    base = {
        "collector": {"working_dir": r"C:\app\collector"},
        "mysql": {"host": "h", "port": 3306, "user": "u",
                  "database": "db", "password_enc": ""},
    }
    base.update(over)
    return base


def test_returns_sidecar_manager_with_runner_and_runtime_dir(tmp_path):
    sm = build_sidecar_manager(_cfg(), tmp_path)
    assert isinstance(sm, SidecarManager)
    assert sm.runtime_dir == tmp_path
    assert sm.runner is not None


def test_collector_settings_path_derived_from_data_root(tmp_path, monkeypatch):
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    sm = build_sidecar_manager(_cfg(), tmp_path)
    assert sm.collector_settings_path == tmp_path / "collector" / "config" / "settings.toml"


def test_collector_settings_path_independent_of_cfg(tmp_path, monkeypatch):
    # 派生自数据根，不再依赖 cfg.working_dir；无 collector 段也成立
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    sm = build_sidecar_manager({"mysql": {}}, tmp_path)
    assert sm.collector_settings_path == tmp_path / "collector" / "config" / "settings.toml"


def test_agent_mysql_injected_from_cfg(tmp_path):
    cfg = _cfg()
    sm = build_sidecar_manager(cfg, tmp_path)
    assert sm._agent_mysql == cfg["mysql"]


def test_opcda_defaults_when_section_absent(tmp_path):
    sm = build_sidecar_manager(_cfg(), tmp_path)
    assert sm._server_prog_id == "ZPMC.OPCServer"
    assert sm._channel == "OPD_Server"


def test_opcda_overrides_when_section_present(tmp_path):
    cfg = _cfg(opcda={"server_prog_id": "Other.Srv", "channel": "CH9"})
    sm = build_sidecar_manager(cfg, tmp_path)
    assert sm._server_prog_id == "Other.Srv"
    assert sm._channel == "CH9"


def test_install_root_dev_is_repo_root(tmp_path):
    # 非 frozen：factory parents[2] = 仓库根，应含 agent/ 子目录
    sm = build_sidecar_manager(_cfg(), tmp_path)
    assert (sm.install_root / "agent").is_dir()


def test_install_root_frozen_uses_executable_parent(tmp_path, monkeypatch):
    monkeypatch.setattr(sys, "frozen", True, raising=False)
    monkeypatch.setattr(sys, "executable", str(tmp_path / "bin" / "agent.exe"))
    monkeypatch.delenv("DATAWATCH_INSTALL_ROOT", raising=False)
    sm = build_sidecar_manager(_cfg(), tmp_path)
    assert sm.install_root == (tmp_path / "bin").resolve()


def test_install_root_env_var_takes_precedence(tmp_path, monkeypatch):
    # 环境变量优先于 frozen 推导（安装器/NSSM 注入场景）
    monkeypatch.setattr(sys, "frozen", True, raising=False)
    monkeypatch.setattr(sys, "executable", str(tmp_path / "agent" / "agent.exe"))
    monkeypatch.setenv("DATAWATCH_INSTALL_ROOT", str(tmp_path))
    sm = build_sidecar_manager(_cfg(), tmp_path)
    assert sm.install_root == tmp_path.resolve()


@pytest.mark.asyncio
async def test_storage_crash_recovery_backs_off(tmp_path, monkeypatch):
    import asyncio
    from types import SimpleNamespace
    from unittest.mock import AsyncMock, MagicMock
    from app import factory
    delays = []
    async def sleep(delay):
        delays.append(delay)
        if len(delays) == 5:
            raise asyncio.CancelledError()
    monkeypatch.setattr(factory.asyncio, "sleep", sleep)
    monkeypatch.setattr(factory, "_storage_env", lambda: {})
    pm = SimpleNamespace(wants_running=lambda *a, **k: True, status=lambda name: SimpleNamespace(status="crashed"), restart=AsyncMock(), log_file=lambda name: tmp_path / "current.log")
    app = SimpleNamespace(state=SimpleNamespace(pm=pm, edge_storage=SimpleNamespace(wait_ready=lambda: None), log_manager=MagicMock()))
    with pytest.raises(asyncio.CancelledError):
        await factory._recover_edge_storage(app)
    assert delays == [2.0, 4.0, 8.0, 16.0, 32.0]


@pytest.mark.asyncio
async def test_storage_retries_failed_recovery_from_stopped_state(tmp_path, monkeypatch):
    import asyncio
    from types import SimpleNamespace
    from unittest.mock import MagicMock
    from app import factory
    from app.services.managed_process_runner import ManagedProcessError
    status = SimpleNamespace(status="crashed")
    attempts = []
    async def restart(*args, **kwargs):
        attempts.append(1)
        status.status = "stopped"
        if len(attempts) == 1:
            raise ManagedProcessError("archive failed")
        status.status = "running"
    delays = []
    async def sleep(delay):
        delays.append(delay)
        if len(delays) == 4:
            raise asyncio.CancelledError()
    monkeypatch.setattr(factory.asyncio, "sleep", sleep)
    monkeypatch.setattr(factory, "_storage_env", lambda: {})
    pm = SimpleNamespace(wants_running=lambda *a, **k: True, status=lambda name: status, restart=restart, log_file=lambda name: tmp_path / "current.log")
    app = SimpleNamespace(state=SimpleNamespace(pm=pm, edge_storage=SimpleNamespace(wait_ready=lambda: None), log_manager=MagicMock()))
    with pytest.raises(asyncio.CancelledError):
        await factory._recover_edge_storage(app)
    assert len(attempts) == 2
    assert app.state._storage_recovery_pending is False


@pytest.mark.asyncio
async def test_recovery_restores_real_crashed_writer_and_stops_on_user_request(make_pm, monkeypatch):
    import asyncio
    from dataclasses import replace
    from types import SimpleNamespace
    from unittest.mock import MagicMock
    from app import factory
    from app.routers import admin

    pm, name = make_pm(name="writer", child_args=["--exit-after", "0.2", "--exit-code", "1"])
    first = await pm.start(name)
    for _ in range(100):
        if pm.status(name).status == "crashed":
            break
        await asyncio.sleep(0.02)
    assert pm.status(name).status == "crashed"
    pm.modules[name] = replace(pm.modules[name], args=[pm.modules[name].args[0], "--flag", str(pm._flag_file(name))])
    monkeypatch.setattr(factory, "load_config", lambda: {"agent": {"bootstrap_done": True}})
    monkeypatch.setattr(admin, "_process_env", lambda name: {})
    app = SimpleNamespace(state=SimpleNamespace(pm=pm, log_manager=MagicMock()))
    recovery = asyncio.create_task(factory._recover_processes(app, interval=0.02))
    try:
        for _ in range(100):
            if pm.status(name).status == "running":
                break
            await asyncio.sleep(0.02)
        assert pm.status(name).status == "running"
        assert pm.status(name).pid != first.pid
        assert pm._restart_count(name) == 1
        await pm.stop(name)
        await asyncio.sleep(0.1)
        assert pm.status(name).status == "stopped"
    finally:
        recovery.cancel()
        with pytest.raises(asyncio.CancelledError):
            await recovery
        await pm.stop(name)


@pytest.mark.asyncio
async def test_recovery_survives_initial_config_failure_and_backs_off(monkeypatch):
    import asyncio
    from types import SimpleNamespace
    from app import factory

    clock = [0.0]
    attempts = []
    calls = [0]
    def config():
        calls[0] += 1
        if calls[0] == 1:
            raise OSError("temporarily unavailable")
        return {"agent": {"bootstrap_done": True}}
    async def sleep(delay):
        clock[0] += delay
        if clock[0] >= 130:
            raise asyncio.CancelledError()
    async def start(*args, **kwargs):
        attempts.append(clock[0])
        raise OSError("spawn failed")
    monkeypatch.setattr(factory, "load_config", config)
    monkeypatch.setattr(factory.asyncio, "sleep", sleep)
    monkeypatch.setattr(factory.asyncio.get_running_loop(), "time", lambda: clock[0])
    monkeypatch.setattr(factory, "start_process", start)
    pm = SimpleNamespace(modules={"writer": None}, wants_running=lambda name: True,
        status=lambda name: SimpleNamespace(status="stopped", uptime_seconds=0))
    app = SimpleNamespace(state=SimpleNamespace(pm=pm))
    with pytest.raises(asyncio.CancelledError):
        await factory._recover_processes(app)
    assert attempts == [2.0, 4.0, 8.0, 16.0, 32.0, 64.0, 124.0]
