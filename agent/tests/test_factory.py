"""T020 — factory.build_sidecar_manager 纯函数构建 SidecarManager。

纯函数：只吃 (cfg, runtime_dir)，不依赖 app.state，便于单测。
约定：collector_settings_path = working_dir/config/settings.toml（同源一致性校验）；
[opcda] 缺省走 ZPMC.OPCServer/OPD_Server，有则覆盖；install_root frozen-aware。
"""

import sys
from pathlib import Path

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


def test_collector_settings_path_is_working_dir_config_settings(tmp_path):
    sm = build_sidecar_manager(_cfg(), tmp_path)
    assert sm.collector_settings_path == Path(r"C:\app\collector") / "config" / "settings.toml"


def test_settings_path_none_when_working_dir_missing(tmp_path):
    sm = build_sidecar_manager({"collector": {}, "mysql": {}}, tmp_path)
    assert sm.collector_settings_path is None


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
    sm = build_sidecar_manager(_cfg(), tmp_path)
    assert sm.install_root == (tmp_path / "bin").resolve()
