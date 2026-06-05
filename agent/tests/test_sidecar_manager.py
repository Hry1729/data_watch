"""T013 — SidecarManager 的 proxy exe 路径解析（点3）。

候选路径（依次找，相对安装根/仓库根）：
  ① sidecars/opcda_proxy_service/bin/OpcDaProxyService.exe          （打包安装目录）
  ② collector/sidecars/opcda_proxy_service/bin/OpcDaProxyService.exe（开发仓库）
缺失时错误须列出全部候选路径。
"""

import json

import pytest

from app.services.sidecar_manager import ProxyExecutableNotFound, SidecarManager

_EXE = "OpcDaProxyService.exe"
_INSTALL_REL = "sidecars/opcda_proxy_service/bin"
_DEV_REL = "collector/sidecars/opcda_proxy_service/bin"


def _make_exe(root, rel):
    exe = root.joinpath(*rel.split("/"), _EXE)
    exe.parent.mkdir(parents=True, exist_ok=True)
    exe.write_text("")
    return exe


def test_resolves_from_install_layout(tmp_path):
    exe = _make_exe(tmp_path, _INSTALL_REL)
    sm = SidecarManager(install_root=tmp_path)
    assert sm.resolve_proxy_executable() == exe


def test_resolves_from_dev_collector_layout(tmp_path):
    exe = _make_exe(tmp_path, _DEV_REL)
    sm = SidecarManager(install_root=tmp_path)
    assert sm.resolve_proxy_executable() == exe


def test_install_layout_preferred_when_both_exist(tmp_path):
    install_exe = _make_exe(tmp_path, _INSTALL_REL)
    _make_exe(tmp_path, _DEV_REL)
    sm = SidecarManager(install_root=tmp_path)
    assert sm.resolve_proxy_executable() == install_exe


def test_missing_exe_error_lists_all_candidates(tmp_path):
    sm = SidecarManager(install_root=tmp_path)
    with pytest.raises(ProxyExecutableNotFound) as excinfo:
        sm.resolve_proxy_executable()
    msg = str(excinfo.value)
    assert _EXE in msg
    # 两个候选目录都须出现在错误里（用 os 分隔符无关的片段断言）
    assert "opcda_proxy_service" in msg
    assert "collector" in msg


def test_default_install_root_resolves_to_package_root():
    # 不依赖 cwd：默认 install_root 由包位置推导，且为已存在目录。
    sm = SidecarManager()
    assert sm.install_root.exists()


def test_install_root_is_resolved_to_absolute(tmp_path, monkeypatch):
    # 传相对路径时，install_root 须被 resolve 成绝对，错误信息才给绝对路径。
    monkeypatch.chdir(tmp_path)
    sm = SidecarManager(install_root="rel_root")
    assert sm.install_root.is_absolute()


# ---------------------------------------------------------------------------
# T014 — 生成 proxy_{port}.json
# ---------------------------------------------------------------------------

def test_generate_proxy_config_writes_expected_fields(tmp_path):
    sm = SidecarManager(runtime_dir=tmp_path)
    path = sm.generate_proxy_config(9090, "172.27.18.221")
    assert path == tmp_path / "opcda_proxy" / "proxy_9090.json"
    cfg = json.loads(path.read_text(encoding="utf-8"))
    assert cfg["listenPort"] == 9090
    srv = cfg["opcServers"][0]
    assert srv["vmHost"] == "172.27.18.221"
    assert srv["serverProgId"] == "ZPMC.OPCServer"
    assert srv["channel"] == "OPD_Server"
    assert srv["devices"] == []


def test_generate_proxy_config_localhost_maps_to_empty_vmhost(tmp_path):
    # endpoint=localhost（本地 OPC）→ proxy config 的 vmHost 用空串（spec Edge Case）。
    sm = SidecarManager(runtime_dir=tmp_path)
    cfg = json.loads(sm.generate_proxy_config(9091, "localhost").read_text(encoding="utf-8"))
    assert cfg["opcServers"][0]["vmHost"] == ""


def test_generate_proxy_config_uses_injected_progid_channel(tmp_path):
    sm = SidecarManager(runtime_dir=tmp_path, server_prog_id="Other.Server", channel="CH1")
    cfg = json.loads(sm.generate_proxy_config(9090, "10.0.0.1").read_text(encoding="utf-8"))
    srv = cfg["opcServers"][0]
    assert srv["serverProgId"] == "Other.Server"
    assert srv["channel"] == "CH1"


def test_init_rejects_positional_args():
    # 构造参数全部 keyword-only，避免 install_root/runtime_dir 位置混淆。
    with pytest.raises(TypeError):
        SidecarManager("x")


def test_generate_proxy_config_normalizes_local_host_case_and_spaces(tmp_path):
    sm = SidecarManager(runtime_dir=tmp_path)
    cfg = json.loads(sm.generate_proxy_config(9090, " LOCALHOST ").read_text(encoding="utf-8"))
    assert cfg["opcServers"][0]["vmHost"] == ""


def test_generate_proxy_config_strips_remote_host(tmp_path):
    sm = SidecarManager(runtime_dir=tmp_path)
    cfg = json.loads(sm.generate_proxy_config(9090, " 192.168.1.10 ").read_text(encoding="utf-8"))
    assert cfg["opcServers"][0]["vmHost"] == "192.168.1.10"


def test_generate_proxy_config_coerces_str_port(tmp_path):
    sm = SidecarManager(runtime_dir=tmp_path)
    path = sm.generate_proxy_config("9090", "10.0.0.1")
    assert path.name == "proxy_9090.json"
    cfg = json.loads(path.read_text(encoding="utf-8"))
    assert cfg["listenPort"] == 9090


def test_generate_proxy_config_rejects_invalid_port(tmp_path):
    sm = SidecarManager(runtime_dir=tmp_path)
    for bad in (0, 70000, None, "abc"):
        with pytest.raises(ValueError):
            sm.generate_proxy_config(bad, "10.0.0.1")
