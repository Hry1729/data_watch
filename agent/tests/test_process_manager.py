"""ProcessManager 表征(characterization)测试。

agent 此前无测试。本文件记录现状 ProcessManager 的可观察行为，作为
002 特性中抽取 ManagedProcessRunner 的回归网——抽取前后均须全绿。
"""
import asyncio
import os
import time
from dataclasses import replace

import pytest

from app.services.process_manager import ProcessManagerError


async def _wait(cond, timeout=6.0, interval=0.05):
    end = time.time() + timeout
    while time.time() < end:
        if cond():
            return True
        await asyncio.sleep(interval)
    return False


async def test_start_sets_running_and_writes_pid(make_pm):
    pm, name = make_pm()
    info = await pm.start(name)
    try:
        assert info.status == "running"
        assert info.pid
        assert pm._pid_file(name).exists()
        st = pm.status(name)
        assert st.status == "running"
        assert st.pid == info.pid
    finally:
        await pm.stop(name)


async def test_start_is_idempotent_when_already_running(make_pm):
    pm, name = make_pm()
    a = await pm.start(name)
    try:
        b = await pm.start(name)
        assert b.status == "running"
        assert b.pid == a.pid
    finally:
        await pm.stop(name)


async def test_stop_graceful_removes_pid_file(make_pm):
    pm, name = make_pm()
    await pm.start(name)
    info = await pm.stop(name)
    assert info.status == "stopped"
    assert not pm._pid_file(name).exists()
    assert pm.status(name).status == "stopped"


async def test_stop_force_kills_stubborn_child(make_pm):
    pm, name = make_pm(child_args=["--ignore-flag"])
    await pm.start(name)
    t0 = time.time()
    info = await pm.stop(name, timeout=1.0)
    assert info.status == "stopped"
    # 短超时后应 terminate/kill，不应一直挂着
    assert time.time() - t0 < 8.0
    assert not pm._pid_file(name).exists()


async def test_stop_skips_flag_wait_for_unsupported_module(make_pm):
    pm, name = make_pm(child_args=["--ignore-flag"])
    pm.modules[name] = replace(pm.modules[name], supports_shutdown_flag=False)
    await pm.start(name)
    t0 = time.time()
    info = await pm.stop(name)
    assert info.status == "stopped"
    assert time.time() - t0 < 5.0


async def test_status_reports_crashed_on_nonzero_exit(make_pm):
    pm, name = make_pm(child_args=["--exit-after", "0.3", "--exit-code", "1"])
    await pm.start(name)
    try:
        assert await _wait(lambda: pm.status(name).status == "crashed")
    finally:
        await pm.stop(name)


async def test_start_appends_extra_args_to_cmd(make_pm, tmp_path):
    out = tmp_path / "argv.txt"
    pm, name = make_pm()  # 默认 child_args = ["--flag", <flag>]
    await pm.start(name, extra_args=["--argv-out", str(out)])
    try:
        assert await _wait(lambda: out.exists())
        content = out.read_text(encoding="utf-8")
        assert "--argv-out" in content
        assert str(out) in content
    finally:
        await pm.stop(name)


async def test_start_passes_env_to_runner(make_pm, monkeypatch):
    captured = {}
    pm, name = make_pm()

    def fake_launch(module_name, cmd, cwd, env=None):
        captured["module_name"] = module_name
        captured["env"] = env
        return os.getpid(), time.time()

    monkeypatch.setattr(pm._runner, "launch", fake_launch)
    await pm.start(name, env={"DATAWATCH_MYSQL_PASSWORD": "secret"})
    assert captured == {
        "module_name": name,
        "env": {"DATAWATCH_MYSQL_PASSWORD": "secret"},
    }


async def test_restart_appends_extra_args_to_cmd(make_pm, tmp_path):
    out = tmp_path / "argv_restart.txt"
    pm, name = make_pm()
    await pm.start(name)
    await pm.restart(name, extra_args=["--argv-out", str(out)])
    try:
        assert await _wait(lambda: out.exists())
        content = out.read_text(encoding="utf-8")
        assert "--argv-out" in content
        assert str(out) in content
    finally:
        await pm.stop(name)


async def test_start_rejects_non_list_extra_args(make_pm):
    pm, name = make_pm()
    with pytest.raises(ProcessManagerError):
        await pm.start(name, extra_args="--oops")  # 字符串而非 list[str]


async def test_restart_increments_persisted_count(make_pm):
    pm, name = make_pm()
    await pm.start(name)
    try:
        info = await pm.restart(name)
        assert info.status == "running"
        assert info.restart_count >= 1
        # 持久化：新建 manager 读到的 status 仍带该计数
        assert pm.status(name).restart_count >= 1
    finally:
        await pm.stop(name)
