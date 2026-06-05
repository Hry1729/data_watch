"""T013 — SidecarManager 的 proxy exe 路径解析（点3）。

候选路径（依次找，相对安装根/仓库根）：
  ① sidecars/opcda_proxy_service/bin/OpcDaProxyService.exe          （打包安装目录）
  ② collector/sidecars/opcda_proxy_service/bin/OpcDaProxyService.exe（开发仓库）
缺失时错误须列出全部候选路径。
"""

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
