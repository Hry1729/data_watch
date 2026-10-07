"""data_root() 路径解析：env > frozen(ProgramData) > dev(agent 包目录)。"""

import sys
from pathlib import Path

from app.core import config


def test_data_root_env_var_takes_precedence(tmp_path, monkeypatch):
    monkeypatch.setattr(sys, "frozen", True, raising=False)
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    assert config.data_root() == tmp_path


def test_data_root_frozen_default_programdata(monkeypatch):
    monkeypatch.delenv("DATAWATCH_DATA_ROOT", raising=False)
    monkeypatch.setattr(sys, "frozen", True, raising=False)
    assert config.data_root() == Path(r"C:\ProgramData\DataWatch")


def test_data_root_dev_is_agent_dir(monkeypatch):
    monkeypatch.delenv("DATAWATCH_DATA_ROOT", raising=False)
    monkeypatch.setattr(sys, "frozen", False, raising=False)
    root = config.data_root()
    assert root.name == "agent"
    assert (root / "app").is_dir()


def test_data_root_frozen_isolated_by_role(monkeypatch):
    monkeypatch.delenv("DATAWATCH_DATA_ROOT", raising=False)
    monkeypatch.setattr(sys, "frozen", True, raising=False)
    monkeypatch.setenv("DATAWATCH_ROLE", "edge")
    assert config.data_root() == Path(r"C:\ProgramData\DataWatch Edge")
    monkeypatch.setenv("DATAWATCH_ROLE", "distributed-center")
    assert config.data_root() == Path(r"C:\ProgramData\DataWatch Distributed Center")


def test_default_runtime_dir_follows_data_root(tmp_path, monkeypatch):
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    assert config._default_config()["agent"]["runtime_dir"] == str(tmp_path / "runtime")


def test_resolve_runtime_dir_uses_configured_value(tmp_path, monkeypatch):
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    cfg = {"agent": {"runtime_dir": str(tmp_path / "custom")}}
    assert config.resolve_runtime_dir(cfg) == tmp_path / "custom"


def test_resolve_runtime_dir_falls_back_when_key_missing(tmp_path, monkeypatch):
    """键缺失（手改配置/旧版 config.toml）必须回退 data_root()/runtime，而不是硬编码
    ProgramData——否则边缘/分布式中心角色的写侧与读侧分叉。"""
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    assert config.resolve_runtime_dir({"agent": {}}) == tmp_path / "runtime"
    assert config.resolve_runtime_dir({}) == tmp_path / "runtime"


def test_resolve_runtime_dir_falls_back_on_empty_string(tmp_path, monkeypatch):
    """空串不能被当成路径：Path("") 会退化成当前工作目录。"""
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    assert config.resolve_runtime_dir({"agent": {"runtime_dir": ""}}) == tmp_path / "runtime"