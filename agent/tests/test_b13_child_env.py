"""B13 回归：Agent 服务令牌不得经父环境扩散到受管子进程。

修复前 ``ManagedProcessRunner.launch`` 用 ``{**os.environ, ...}`` 组装子进程环境，
安装器通过 NSSM 注入的 ``DATAWATCH_AGENT_SERVICE_TOKEN`` 因而会传给每一个受管的
Collector / Writer / proxy 进程；``sidecar_manager`` 甚至不传 env，完全依赖继承。

现在改为 ``child_environment()``：继承必要项，剥离 Agent 自身凭据，再叠加调用方显式
注入的字段（数据库连接等）。
"""

from __future__ import annotations

import os
import sys
import time
from pathlib import Path

import pytest

AGENT_ROOT = Path(__file__).resolve().parents[1]
if str(AGENT_ROOT) not in sys.path:
    sys.path.insert(0, str(AGENT_ROOT))

from app.services.managed_process_runner import (  # noqa: E402
    ManagedProcessRunner,
    child_environment,
)


def test_agent_credentials_are_stripped(monkeypatch):
    monkeypatch.setenv("DATAWATCH_AGENT_SERVICE_TOKEN", "SENTINEL_agent_token")
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN", "SENTINEL_ipc_token")
    monkeypatch.setenv("AUTH_SECRET_KEY", "SENTINEL_auth_secret")

    env = child_environment()

    assert "DATAWATCH_AGENT_SERVICE_TOKEN" not in env
    assert "DATAWATCH_EDGE_STORAGE_TOKEN" not in env
    assert "AUTH_SECRET_KEY" not in env
    assert "SENTINEL_agent_token" not in env.values()


def test_necessary_inheritance_is_preserved(monkeypatch):
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", r"D:\data-root")
    monkeypatch.setenv("DATAWATCH_ROLE", "edge")
    monkeypatch.setenv("PATH", os.environ.get("PATH", ""))

    env = child_environment({"REDIS_HOST": "127.0.0.1", "MYSQL_PORT": "3306"})

    # 受管进程仍需数据根/角色/系统路径，以及调用方显式注入的库连接字段。
    assert env["DATAWATCH_DATA_ROOT"] == r"D:\data-root"
    assert env["DATAWATCH_ROLE"] == "edge"
    assert "PATH" in env
    assert env["REDIS_HOST"] == "127.0.0.1"
    assert env["MYSQL_PORT"] == "3306"


def test_utf8_and_unbuffered_defaults_are_set(monkeypatch):
    monkeypatch.delenv("PYTHONUTF8", raising=False)
    monkeypatch.delenv("PYTHONIOENCODING", raising=False)
    monkeypatch.delenv("PYTHONUNBUFFERED", raising=False)

    env = child_environment()

    assert env["PYTHONUNBUFFERED"] == "1"
    assert env["PYTHONUTF8"] == "1"
    assert env["PYTHONIOENCODING"] == "utf-8"


def test_explicit_extra_cannot_reinject_stripped_credentials(monkeypatch):
    """凭据必须来自该进程自己的数据文件，不能靠调用方“顺手”再注入一次。"""
    env = child_environment({"DATAWATCH_AGENT_SERVICE_TOKEN": "SENTINEL_reinjected"})
    assert "DATAWATCH_AGENT_SERVICE_TOKEN" not in env
    assert "SENTINEL_reinjected" not in env.values()


def _wait_for_log(path: Path, marker: str, timeout: float = 20.0) -> str:
    deadline = time.monotonic() + timeout
    text = ""
    while time.monotonic() < deadline:
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            text = ""
        if marker in text:
            return text
        time.sleep(0.2)
    return text


def test_real_child_process_reports_no_agent_token(tmp_path, monkeypatch):
    """真实受管启动路径：替身进程打印环境变量名与哨兵值判定，父进程读日志核对。"""
    monkeypatch.setenv("DATAWATCH_AGENT_SERVICE_TOKEN", "SENTINEL_agent_token")
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN", "SENTINEL_ipc_token")
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))

    runner = ManagedProcessRunner(tmp_path / "runtime")
    # 子进程稍作停留，避免被 launch 判定为"立即退出"。
    child_code = (
        "import os, time;"
        "print('HAS_AGENT_TOKEN=' + str('DATAWATCH_AGENT_SERVICE_TOKEN' in os.environ), flush=True);"
        "print('HAS_IPC_TOKEN=' + str('DATAWATCH_EDGE_STORAGE_TOKEN' in os.environ), flush=True);"
        "print('HAS_DATA_ROOT=' + str('DATAWATCH_DATA_ROOT' in os.environ), flush=True);"
        "print('SENTINEL_LEAK=' + str(any(v.startswith('SENTINEL_') for v in os.environ.values())), flush=True);"
        "time.sleep(1.5)"
    )
    runner.launch("envstub", [sys.executable, "-c", child_code], str(tmp_path))
    try:
        text = _wait_for_log(runner.log_file("envstub"), "SENTINEL_LEAK=")
    finally:
        runner.close_log("envstub")

    assert "HAS_AGENT_TOKEN=False" in text, text
    assert "HAS_IPC_TOKEN=False" in text, text
    assert "HAS_DATA_ROOT=True" in text, text
    assert "SENTINEL_LEAK=False" in text, text
