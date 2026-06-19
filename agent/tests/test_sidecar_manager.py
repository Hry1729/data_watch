"""T013 — SidecarManager 的 proxy exe 路径解析（点3）。

候选路径（依次找，相对安装根/仓库根）：
  ① sidecars/opcda_proxy_service/bin/OpcDaProxyService.exe          （打包安装目录）
  ② collector/sidecars/opcda_proxy_service/bin/OpcDaProxyService.exe（开发仓库）
缺失时错误须列出全部候选路径。
"""

import json
import socket
import sys
import time
from pathlib import Path

import psutil
import pytest

from app.services.managed_process_runner import ManagedProcessRunner
from app.services.opcda_detect import (
    load_port_mapping,
    save_port_mapping,
)
from app.services.sidecar_manager import (
    ProxyExecutableNotFound,
    ProxyStartError,
    SidecarManager,
)

_HERE = __import__("os").path.dirname(__file__)
FAKE_PROXY = __import__("os").path.join(_HERE, "fixtures", "fake_proxy.py")
DUMMY_CHILD = __import__("os").path.join(_HERE, "fixtures", "dummy_child.py")

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


def test_detect_endpoints_reads_latest_mysql_config(monkeypatch):
    # Agent 服务启动时 SidecarManager 可能拿到模板快照；bootstrap 后必须读最新 config.toml。
    stale = {"host": "old", "port": 3306, "database": "device_watch", "password_enc": ""}
    fresh = {"host": "new", "port": 3306, "database": "device_watch", "password_enc": "enc"}
    seen = {}

    monkeypatch.setattr(
        "app.services.sidecar_manager.load_config",
        lambda: {"mysql": fresh},
    )
    def fake_detect(mysql):
        seen["mysql"] = mysql
        return ["endpoint-a"]

    monkeypatch.setattr(
        "app.services.sidecar_manager.detect_center_endpoints",
        fake_detect,
    )

    sm = SidecarManager(agent_mysql=stale)

    assert sm._detect_endpoints() == ["endpoint-a"]
    assert seen["mysql"] == fresh


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


# ---------------------------------------------------------------------------
# T018 — prepare_for_collector_start() 编排（检测→粘性映射→差量起 proxy→闸门）
#
# detect 走薄方法（_detect_endpoints），单测在实例上 stub 掉，不连真 MySQL；
# proxy spawn 仍用 fake_proxy。端口经 port_mapping.json 预置为 _free_port()，
# 避开默认 9090 可能被占。
# ---------------------------------------------------------------------------

_DUMMY_MYSQL = {"host": "h", "port": 3306, "database": "db"}


def _mapping_path(runtime_dir):
    return runtime_dir / "opcda_proxy" / "port_mapping.json"


def _wire_prepare(runtime_dir, endpoints):
    """接真 runner 的 SidecarManager；detect stub，spawn 换 fake_proxy。"""
    runner = ManagedProcessRunner(runtime_dir)
    sm = SidecarManager(
        runtime_dir=runtime_dir,
        runner=runner,
        agent_mysql=_DUMMY_MYSQL,
        collector_settings_path=runtime_dir / "settings.toml",
    )
    sm._build_proxy_cmd = lambda config_path: [
        sys.executable, FAKE_PROXY, "--config", str(config_path)
    ]
    sm._detect_endpoints = lambda: list(endpoints)
    return sm


def test_prepare_no_opcda_returns_none_and_no_spawn(runtime_dir):
    sm = _wire_prepare(runtime_dir, [])
    calls = []
    orig = sm.runner.launch
    sm.runner.launch = lambda *a, **k: (calls.append(1), orig(*a, **k))[1]

    plan = sm.prepare_for_collector_start()

    # 无 OPC DA → 不拼 --port-mapping 的语义靠 port_mapping_path is None 写死
    assert plan.port_mapping_path is None
    assert plan.proxies == []
    assert calls == []  # 零 spawn


def test_prepare_single_endpoint_starts_proxy_and_returns_path(runtime_dir):
    port = _free_port()
    ep = "host-a"
    save_port_mapping(_mapping_path(runtime_dir), {ep: port})  # 让 sticky 复用可用端口
    sm = _wire_prepare(runtime_dir, [ep])

    plan = sm.prepare_for_collector_start()
    try:
        assert plan.port_mapping_path == _mapping_path(runtime_dir)
        assert len(plan.proxies) == 1
        inst = plan.proxies[0]
        assert inst.port == port
        with socket.create_connection(("127.0.0.1", port), timeout=1.0):
            pass  # 闸门已通过，端口此刻可连接
    finally:
        _kill_tree(plan.proxies[0].pid)
        sm.runner.close_log(f"proxy_{port}")


def test_prepare_reuses_existing_proxy_no_double_start(runtime_dir):
    port = _free_port()
    ep = "host-a"
    save_port_mapping(_mapping_path(runtime_dir), {ep: port})
    sm = _wire_prepare(runtime_dir, [ep])
    calls = []
    orig = sm.runner.launch
    sm.runner.launch = lambda *a, **k: (calls.append(1), orig(*a, **k))[1]

    plan1 = sm.prepare_for_collector_start()
    plan2 = sm.prepare_for_collector_start()  # 第二次：proxy 仍存活 → 复用
    try:
        assert len(calls) == 1  # 只 spawn 一次
        assert len(plan2.proxies) == 1
        assert plan2.proxies[0].pid == plan1.proxies[0].pid
    finally:
        _kill_tree(plan1.proxies[0].pid)
        sm.runner.close_log(f"proxy_{port}")


def test_prepare_rollback_stops_only_new_keeps_existing(runtime_dir):
    p1, p2, p3 = _free_port(), _free_port(), _free_port()
    ep1, ep2, ep3 = "host-1", "host-2", "host-3"
    save_port_mapping(_mapping_path(runtime_dir), {ep1: p1, ep2: p2, ep3: p3})
    sm = _wire_prepare(runtime_dir, [ep1])

    # p3 的 proxy 永不监听 → 闸门超时失败
    def build(config_path):
        cfg = json.loads(Path(config_path).read_text(encoding="utf-8"))
        extra = ["--never-listen"] if cfg["listenPort"] == p3 else []
        return [sys.executable, FAKE_PROXY, "--config", str(config_path), *extra]
    sm._build_proxy_cmd = build

    # 先建立 ep1 为"既有 proxy"
    plan0 = sm.prepare_for_collector_start()
    ep1_pid = plan0.proxies[0].pid

    # 现在检测出三个端点；记录本轮 spawn 的 pid
    sm._detect_endpoints = lambda: [ep1, ep2, ep3]
    launched = []
    orig = sm.runner.launch

    def track(name, cmd, **kw):
        r = orig(name, cmd, **kw)
        launched.append((name, r[0]))
        return r
    sm.runner.launch = track

    try:
        with pytest.raises(ProxyStartError):
            sm.prepare_for_collector_start(ready_timeout=1.0)

        # 既有 ep1 不受牵连：仍存活、且仍登记在注册表、pid 未变（被复用而非重起）
        assert psutil.pid_exists(ep1_pid) and psutil.Process(ep1_pid).is_running()
        assert sm._proxies.get(ep1) is not None and sm._proxies[ep1].pid == ep1_pid

        # 本轮新起的 ep2 回滚后不残留在注册表（成功后才登记，回滚不登记）
        assert ep2 not in sm._proxies

        # 本轮新起的 ep2 被回滚（不存活）
        p2_pids = [pid for (n, pid) in launched if n == f"proxy_{p2}"]
        assert p2_pids, "ep2 应当在本轮被 spawn 过"
        end = time.time() + 5
        while end > time.time() and any(psutil.pid_exists(pid) for pid in p2_pids):
            time.sleep(0.05)
        for pid in p2_pids:
            assert not psutil.pid_exists(pid)
    finally:
        _kill_tree(ep1_pid)
        for (_n, pid) in launched:
            _kill_tree(pid)
        for port in (p1, p2, p3):
            try:
                sm.runner.close_log(f"proxy_{port}")
            except Exception:
                pass


# ---------------------------------------------------------------------------
# T022 — claim_existing_proxies() Agent 重启认领存活 proxy（FR-P15）
#
# 认领只校验 pid+create_time（不看监听），故用 dummy_child（空转存活、不绑端口）。
# port→endpoint 经 port_mapping.json 反转；活着不可映射→跳过保留；死亡/stale→清 pid。
# ---------------------------------------------------------------------------

def _sm_claim(runtime_dir):
    return SidecarManager(
        runtime_dir=runtime_dir,
        runner=ManagedProcessRunner(runtime_dir),
        agent_mysql={"host": "h", "port": 3306, "database": "db"},
        collector_settings_path=runtime_dir / "settings.toml",
    )


def _launch_dummy(runtime_dir, name, *extra):
    runner = ManagedProcessRunner(runtime_dir)
    pid, ct = runner.launch(
        name, [sys.executable, DUMMY_CHILD, *extra], cwd=str(runtime_dir)
    )
    return runner, pid, ct


def test_claim_alive_matching_proxy_into_registry(runtime_dir):
    port = _free_port()
    ep = "host-a"
    save_port_mapping(_mapping_path(runtime_dir), {ep: port})
    name = f"proxy_{port}"
    runner, pid, ct = _launch_dummy(runtime_dir, name, "--ignore-flag")
    try:
        sm = _sm_claim(runtime_dir)
        sm.claim_existing_proxies()
        assert ep in sm._proxies
        inst = sm._proxies[ep]
        assert inst.port == port
        assert inst.pid == pid
        assert inst.create_time == pytest.approx(ct)
        # config_path / log_path 按现有约定重建，不依赖 pid 文件内容
        assert inst.config_path == runtime_dir / "opcda_proxy" / f"proxy_{port}.json"
        assert inst.log_path == sm.runner.log_file(name)
        # 活着的 pid 文件保留
        assert runner.pid_file(name).exists()
    finally:
        _kill_tree(pid)
        runner.close_log(name)


def test_claim_dead_proxy_cleans_pid_record(runtime_dir):
    port = _free_port()
    ep = "host-a"
    save_port_mapping(_mapping_path(runtime_dir), {ep: port})
    name = f"proxy_{port}"
    runner, pid, ct = _launch_dummy(runtime_dir, name, "--exit-after", "0.1")
    end = time.time() + 5
    while end > time.time() and psutil.pid_exists(pid):
        time.sleep(0.05)
    assert not psutil.pid_exists(pid)
    try:
        sm = _sm_claim(runtime_dir)
        sm.claim_existing_proxies()
        assert ep not in sm._proxies
        assert not runner.pid_file(name).exists()  # 死亡 pid 记录已清
    finally:
        runner.close_log(name)


def test_claim_create_time_mismatch_cleans_record_without_kill(runtime_dir):
    port = _free_port()
    ep = "host-a"
    save_port_mapping(_mapping_path(runtime_dir), {ep: port})
    name = f"proxy_{port}"
    runner, pid, ct = _launch_dummy(runtime_dir, name, "--ignore-flag")
    try:
        # 真 pid 但 create_time 写错 → verify_alive 判 stale
        runner.write_pid_record(name, pid, ct - 10000.0)
        sm = _sm_claim(runtime_dir)
        sm.claim_existing_proxies()
        assert ep not in sm._proxies                 # 不认领
        assert not runner.pid_file(name).exists()    # stale 记录已删
        assert psutil.pid_exists(pid)                # 真进程未被杀（无法证明归属，只清记录）
    finally:
        _kill_tree(pid)
        runner.close_log(name)


def test_claim_alive_unmappable_port_left_running_not_claimed(runtime_dir):
    # mapping 里没有该 port（端口不可映射到 endpoint）→ 跳过：保留运行、不认领、不杀
    port = _free_port()
    save_port_mapping(_mapping_path(runtime_dir), {"other-ep": _free_port()})
    name = f"proxy_{port}"
    runner, pid, ct = _launch_dummy(runtime_dir, name, "--ignore-flag")
    try:
        sm = _sm_claim(runtime_dir)
        sm.claim_existing_proxies()
        assert sm._proxies == {}                  # 未认领
        assert runner.pid_file(name).exists()     # 未清 pid（进程活着）
        assert psutil.pid_exists(pid)             # 未杀
    finally:
        _kill_tree(pid)
        runner.close_log(name)


def test_claim_skips_whole_round_on_corrupt_mapping(runtime_dir):
    port = _free_port()
    name = f"proxy_{port}"
    # 写损坏的 mapping（非对象）→ load_port_mapping fail-fast；claim 应跳过整轮、不抛
    _mapping_path(runtime_dir).write_text("[1, 2, 3]", encoding="utf-8")
    runner, pid, ct = _launch_dummy(runtime_dir, name, "--ignore-flag")
    try:
        sm = _sm_claim(runtime_dir)
        sm.claim_existing_proxies()  # 不抛
        assert sm._proxies == {}
        assert runner.pid_file(name).exists()  # 跳过整轮，未动 pid
    finally:
        _kill_tree(pid)
        runner.close_log(name)


def test_claim_duplicate_port_in_mapping_left_running_not_claimed(runtime_dir):
    # 损坏 mapping：两 endpoint 指向同一 port → 无法证明归属 → 跳过、保留运行、不杀
    port = _free_port()
    save_port_mapping(_mapping_path(runtime_dir), {"ep-1": port, "ep-2": port})
    name = f"proxy_{port}"
    runner, pid, ct = _launch_dummy(runtime_dir, name, "--ignore-flag")
    try:
        sm = _sm_claim(runtime_dir)
        sm.claim_existing_proxies()
        assert sm._proxies == {}                  # 重复端口不认领（不误配到 ep-2）
        assert runner.pid_file(name).exists()     # 未清 pid（进程活着）
        assert psutil.pid_exists(pid)             # 未杀
    finally:
        _kill_tree(pid)
        runner.close_log(name)


def test_claim_then_prepare_reuses_no_double_start(runtime_dir):
    port = _free_port()
    ep = "host-a"
    save_port_mapping(_mapping_path(runtime_dir), {ep: port})
    name = f"proxy_{port}"
    runner0, pid, ct = _launch_dummy(runtime_dir, name, "--ignore-flag")
    try:
        sm = _wire_prepare(runtime_dir, [ep])  # detect/一致性 stub + fake build_proxy_cmd
        sm.claim_existing_proxies()
        assert sm._proxies.get(ep) is not None and sm._proxies[ep].pid == pid

        calls = []
        orig = sm.runner.launch
        sm.runner.launch = lambda *a, **k: (calls.append(1), orig(*a, **k))[1]
        plan = sm.prepare_for_collector_start()
        assert calls == []                       # 复用认领的，不再 spawn
        assert len(plan.proxies) == 1
        assert plan.proxies[0].pid == pid
    finally:
        _kill_tree(pid)
        sm.runner.close_log(name)
        runner0.close_log(name)


# ---------------------------------------------------------------------------
# T024/T025 — prepare 差量调整（增/删端点动 proxy，未变不重起，粘性不漂移；FR-P16）
# ---------------------------------------------------------------------------

def test_prepare_added_endpoint_starts_only_new_no_drift(runtime_dir):
    p1, p2 = _free_port(), _free_port()
    ep1, ep2 = "host-1", "host-2"
    save_port_mapping(_mapping_path(runtime_dir), {ep1: p1, ep2: p2})
    sm = _wire_prepare(runtime_dir, [ep1])
    sm.prepare_for_collector_start()
    ep1_pid = sm._proxies[ep1].pid
    calls = []
    orig = sm.runner.launch
    sm.runner.launch = lambda *a, **k: (calls.append(1), orig(*a, **k))[1]
    try:
        sm._detect_endpoints = lambda: [ep1, ep2]
        plan = sm.prepare_for_collector_start()
        assert len(calls) == 1                       # 只起新增 ep2
        assert sm._proxies[ep1].pid == ep1_pid       # 未变端点不重起
        assert sm._proxies[ep2].port == p2           # 新端点用粘性端口，不漂移
        assert load_port_mapping(_mapping_path(runtime_dir)) == {ep1: p1, ep2: p2}
        assert {i.port for i in plan.proxies} == {p1, p2}
    finally:
        _kill_tree(ep1_pid)
        if ep2 in sm._proxies:
            _kill_tree(sm._proxies[ep2].pid)
        sm.runner.close_log(f"proxy_{p1}")
        sm.runner.close_log(f"proxy_{p2}")


def test_prepare_removed_endpoint_stops_proxy_keeps_others_and_mapping(runtime_dir):
    p1, p2 = _free_port(), _free_port()
    ep1, ep2 = "host-1", "host-2"
    save_port_mapping(_mapping_path(runtime_dir), {ep1: p1, ep2: p2})
    sm = _wire_prepare(runtime_dir, [ep1, ep2])
    plan = sm.prepare_for_collector_start()
    ep1_pid = sm._proxies[ep1].pid
    ep2_pid = sm._proxies[ep2].pid
    try:
        sm._detect_endpoints = lambda: [ep1]      # ep2 被删
        plan2 = sm.prepare_for_collector_start()
        # ep2 proxy 停 + 出注册表
        assert ep2 not in sm._proxies
        end = time.time() + 5
        while end > time.time() and psutil.pid_exists(ep2_pid):
            time.sleep(0.05)
        assert not psutil.pid_exists(ep2_pid)
        # ep1 不动（pid 不变、仍存活）
        assert sm._proxies[ep1].pid == ep1_pid and psutil.pid_exists(ep1_pid)
        # mapping 仍保留 ep2（sticky，端口不回收）
        assert load_port_mapping(_mapping_path(runtime_dir)) == {ep1: p1, ep2: p2}
        assert [i.port for i in plan2.proxies] == [p1]
    finally:
        _kill_tree(ep1_pid)
        _kill_tree(ep2_pid)
        sm.runner.close_log(f"proxy_{p1}")
        sm.runner.close_log(f"proxy_{p2}")


def test_prepare_all_endpoints_removed_stops_all_returns_none_keeps_mapping(runtime_dir):
    p1 = _free_port()
    ep1 = "host-1"
    save_port_mapping(_mapping_path(runtime_dir), {ep1: p1})
    sm = _wire_prepare(runtime_dir, [ep1])
    sm.prepare_for_collector_start()
    ep1_pid = sm._proxies[ep1].pid
    try:
        sm._detect_endpoints = lambda: []
        plan = sm.prepare_for_collector_start()
        assert plan.port_mapping_path is None and plan.proxies == []
        assert sm._proxies == {}
        end = time.time() + 5
        while end > time.time() and psutil.pid_exists(ep1_pid):
            time.sleep(0.05)
        assert not psutil.pid_exists(ep1_pid)
        # mapping 文件保留历史（端点回归不漂移）
        assert load_port_mapping(_mapping_path(runtime_dir)) == {ep1: p1}
    finally:
        _kill_tree(ep1_pid)
        sm.runner.close_log(f"proxy_{p1}")


def test_on_collector_stop_stops_all_proxies_and_clears(runtime_dir):
    # US5：停 collector 模块 → 停其全部 proxy + 清空注册表（FR-P17）
    p1, p2 = _free_port(), _free_port()
    ep1, ep2 = "host-1", "host-2"
    save_port_mapping(_mapping_path(runtime_dir), {ep1: p1, ep2: p2})
    sm = _wire_prepare(runtime_dir, [ep1, ep2])
    sm.prepare_for_collector_start()
    pid1, pid2 = sm._proxies[ep1].pid, sm._proxies[ep2].pid
    try:
        sm.on_collector_stop()
        assert sm._proxies == {}
        for pid in (pid1, pid2):
            end = time.time() + 5
            while end > time.time() and psutil.pid_exists(pid):
                time.sleep(0.05)
            assert not psutil.pid_exists(pid)
    finally:
        _kill_tree(pid1)
        _kill_tree(pid2)
        sm.runner.close_log(f"proxy_{p1}")
        sm.runner.close_log(f"proxy_{p2}")


