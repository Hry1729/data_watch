"""S3-1 回归：Edge Storage 数据根不得在源码裸跑时退回真实安装目录。

背景：`edge/storage_main.py` 原先在未设置 `DATAWATCH_DATA_ROOT` 时默认
`C:\\ProgramData\\DataWatch Edge`，于是从源码树裸跑会读到/改到**真实安装**。
安装器（NSSM AppEnvironmentExtra）会为受管进程注入该变量，打包构建沿用历史默认，
因此"必须显式"对正规部署无影响。
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT))

from edge.storage_main import _resolve_data_root  # noqa: E402

INSTALLED_DEFAULT = Path(r"C:\ProgramData\DataWatch Edge")


def test_explicit_data_root_wins(monkeypatch):
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", r"D:\from-env")
    assert _resolve_data_root(r"D:\from-arg") == Path(r"D:\from-arg")


def test_env_data_root_used_when_no_explicit(monkeypatch):
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", r"D:\from-env")
    assert _resolve_data_root(None) == Path(r"D:\from-env")


def test_blank_explicit_falls_through_to_env(monkeypatch):
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", r"D:\from-env")
    assert _resolve_data_root("   ") == Path(r"D:\from-env")


def test_source_run_without_env_is_refused(monkeypatch):
    """源码运行且无变量时必须拒绝，而不是退回真实安装目录。"""
    monkeypatch.delenv("DATAWATCH_DATA_ROOT", raising=False)
    monkeypatch.delattr(sys, "frozen", raising=False)
    with pytest.raises(ValueError, match="DATAWATCH_DATA_ROOT is not set"):
        _resolve_data_root(None)


def test_packaged_build_keeps_historical_default(monkeypatch):
    monkeypatch.delenv("DATAWATCH_DATA_ROOT", raising=False)
    monkeypatch.setattr(sys, "frozen", True, raising=False)
    assert _resolve_data_root(None) == INSTALLED_DEFAULT


def test_real_entry_refuses_and_does_not_touch_install(monkeypatch, tmp_path):
    """真实入口级：无变量裸跑应 rc=2，且不得改动真实安装目录。"""
    monkeypatch.delenv("DATAWATCH_DATA_ROOT", raising=False)
    before = sorted(p for p in INSTALLED_DEFAULT.rglob("*") if p.exists()) if INSTALLED_DEFAULT.exists() else []
    proc = subprocess.run(
        [sys.executable, str(REPO_ROOT / "edge" / "storage_main.py"), "--data-root", ""],
        cwd=str(REPO_ROOT),
        capture_output=True,
        text=True,
        timeout=120,
        env={k: v for k, v in __import__("os").environ.items() if k != "DATAWATCH_DATA_ROOT"},
    )
    assert proc.returncode == 2, (proc.returncode, proc.stdout[-500:], proc.stderr[-500:])
    combined = proc.stdout + proc.stderr
    assert "DATAWATCH_DATA_ROOT is not set" in combined
    assert "Refusing to fall back" in combined
    after = sorted(p for p in INSTALLED_DEFAULT.rglob("*") if p.exists()) if INSTALLED_DEFAULT.exists() else []
    assert before == after, "真实安装目录被改动"


def test_data_root_option_governs_database_and_token_paths(monkeypatch, tmp_path):
    """`--data-root` 必须同时决定数据库与令牌文件位置。

    回归背景：`edge_database_path()` / `default_token_file()` 只读 DATAWATCH_DATA_ROOT，
    不认命令行参数；若入口不做桥接，`--data-root` 只影响 runtime 目录，数据库与令牌文件
    仍会落到**仓库内** edge/data/，导致本地裸跑污染工作区（并可能把 IPC 凭据误提交）。
    """
    root = tmp_path / "edge-root"
    monkeypatch.delenv("DATAWATCH_DATA_ROOT", raising=False)
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN_FILE", str(root / "edge" / "storage-ipc-tokens.json"))

    proc = subprocess.run(
        [
            sys.executable,
            str(REPO_ROOT / "edge" / "storage_main.py"),
            "--data-root",
            str(root),
            "--rotate-ipc-tokens",
        ],
        cwd=str(REPO_ROOT),
        capture_output=True,
        text=True,
        timeout=120,
        env={k: v for k, v in __import__("os").environ.items() if k != "DATAWATCH_DATA_ROOT"},
    )
    assert proc.returncode == 0, (proc.returncode, proc.stderr[-500:])
    # 令牌文件落在指定数据根之下。
    assert (root / "edge" / "storage-ipc-tokens.json").is_file()
    # 仓库内的默认位置不得被创建。
    assert not (REPO_ROOT / "edge" / "data" / "storage-ipc-tokens.json").exists()
