"""T013 — SidecarManager 的 proxy exe 路径解析（点3）。

候选路径（依次找，相对安装根/仓库根）：
  ① sidecars/opcda_proxy_service/bin/OpcDaProxyService.exe          （打包安装目录）
  ② collector/sidecars/opcda_proxy_service/bin/OpcDaProxyService.exe（开发仓库）
缺失时错误须列出全部候选路径。
"""

import json
import socket
import sys

import psutil
import pytest

from app.services.managed_process_runner import ManagedProcessRunner
from app.services.sidecar_manager import (
    ProxyExecutableNotFound,
    ProxyStartError,
    SidecarManager,
)

_HERE = __import__("os").path.dirname(__file__)
FAKE_PROXY = __import__("os").path.join(_HERE, "fixtures", "fake_proxy.py")

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


# ---------------------------------------------------------------------------
# T014-spawn + T015 — start_proxy 端口闸门三条件（矩阵：ready / never-listen / 外部占用）
#
# 注：runner.launch 用 sys.executable（venv redirector python）起 fake_proxy，真正
# 监听的是其子进程；故条件②必须按"子树归属"判定（属主 ∈ {proxy_pid} ∪ 后代）。
# ---------------------------------------------------------------------------

def _free_port() -> int:
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]
    finally:
        s.close()


def _wire(runtime_dir, *extra):
    """构造接真 runner 的 SidecarManager，spawn 目标换成 fake_proxy（可附加参数）。"""
    runner = ManagedProcessRunner(runtime_dir)
    sm = SidecarManager(runtime_dir=runtime_dir, runner=runner)
    sm._build_proxy_cmd = lambda config_path: [
        sys.executable, FAKE_PROXY, "--config", str(config_path), *extra
    ]
    return sm


def _kill_tree(pid):
    try:
        proc = psutil.Process(pid)
    except psutil.NoSuchProcess:
        return
    for p in (*proc.children(recursive=True), proc):
        try:
            p.kill()
        except psutil.Error:
            pass


def test_start_proxy_ready_returns_instance(runtime_dir):
    port = _free_port()
    sm = _wire(runtime_dir)
    inst = sm.start_proxy(port=port, vm_host="localhost", endpoint="opcda_center://local")
    try:
        assert inst.port == port
        assert inst.pid > 0
        assert inst.config_path.name == f"proxy_{port}.json"
        assert inst.log_path == sm.runner.log_file(f"proxy_{port}")
        assert inst.log_path.exists()
        # 闸门确实通过：端口此刻可连接
        with socket.create_connection(("127.0.0.1", port), timeout=1.0):
            pass
    finally:
        _kill_tree(inst.pid)
        sm.runner.close_log(f"proxy_{port}")


def test_start_proxy_never_listen_times_out_and_reaps(runtime_dir):
    port = _free_port()
    sm = _wire(runtime_dir, "--never-listen")
    launched = {}
    orig = sm.runner.launch
    sm.runner.launch = lambda name, cmd, **kw: launched.setdefault(
        "pid", orig(name, cmd, **kw)
    ) or launched["pid"]

    with pytest.raises(ProxyStartError) as excinfo:
        sm.start_proxy(
            port=port, vm_host="localhost",
            endpoint="opcda_center://never", ready_timeout=1.0,
        )
    msg = str(excinfo.value)
    assert str(port) in msg
    assert "opcda_center://never" in msg
    assert str(sm.runner.log_file(f"proxy_{port}")) in msg
    # 整树已回收：spawn 的 proxy 进程不再存活
    pid = launched["pid"][0]
    end = __import__("time").time() + 5
    while __import__("time").time() < end and psutil.pid_exists(pid):
        __import__("time").sleep(0.05)
    assert not psutil.pid_exists(pid)


def test_start_proxy_external_occupant_fails_no_drift(runtime_dir):
    port = _free_port()
    # 外部进程（pytest 自身）先占住目标端口——不在 proxy 子树内
    occupant = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    occupant.bind(("127.0.0.1", port))
    occupant.listen(8)
    sm = _wire(runtime_dir)
    launched = {}
    orig = sm.runner.launch
    sm.runner.launch = lambda name, cmd, **kw: launched.setdefault(
        "pid", orig(name, cmd, **kw)
    ) or launched["pid"]
    try:
        with pytest.raises(ProxyStartError) as excinfo:
            sm.start_proxy(
                port=port, vm_host="localhost",
                endpoint="opcda_center://occupied", ready_timeout=2.0,
            )
        msg = str(excinfo.value)
        # 端口虽可连接（被外部占用），但属主不在 proxy 子树 → 判失败、不漂移到别的端口
        assert str(port) in msg
        assert "opcda_center://occupied" in msg
        assert str(sm.runner.log_file(f"proxy_{port}")) in msg
    finally:
        if "pid" in launched:
            _kill_tree(launched["pid"][0])
        occupant.close()
