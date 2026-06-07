"""US1 端到端自动验收（SC-P01）——装配验证，不动生产代码。

串起真实 admin.start_process → 真实 SidecarManager.prepare（真 start_proxy 三条件闸门，
proxy=fake_proxy）→ 真实 ProcessManager.start（collector=dummy_child）。只 stub 无法单元化
的 MySQL：_detect_endpoints / _verify_consistency。

顺序证据两层：
  ① launch_order == [proxy_{port}, collector]（proxy 先 launch）——两个 runner 共享一个
     launch_order，分别 spy；
  ② collector launch 的那一刻 proxy 端口已可连接（prepare 在 pm.start 前已闸门到 ready）。
"""

import json
import os
import socket
import sys
import time
import types

import psutil

from app.routers.admin import start_process
from app.services.managed_process_runner import ManagedProcessRunner
from app.services.opcda_detect import load_port_mapping, save_port_mapping
from app.services.process_manager import ModuleSpec, ProcessManager
from app.services.sidecar_manager import SidecarManager

_HERE = os.path.dirname(__file__)
FAKE_PROXY = os.path.join(_HERE, "fixtures", "fake_proxy.py")
DUMMY_CHILD = os.path.join(_HERE, "fixtures", "dummy_child.py")

_SETTINGS = r"C:\app\collector\config\settings.toml"  # dummy_child 忽略其内容，仅验透传


def _free_port() -> int:
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]
    finally:
        s.close()


def _connectable(port: int) -> bool:
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=0.5):
            return True
    except OSError:
        return False


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


class _FakeLogManager:
    def attach(self, *a, **k):
        pass

    def detach(self, *a, **k):
        pass


def _build(runtime_dir, *, endpoints, argv_file, launch_order, captured):
    """真 SidecarManager（fake_proxy）+ 真 ProcessManager（dummy_child collector），
    两个 runner 的 launch 都 spy 进共享 launch_order。"""
    runner = ManagedProcessRunner(runtime_dir)
    sm = SidecarManager(
        runtime_dir=runtime_dir,
        runner=runner,
        agent_mysql={"host": "h", "port": 3306, "database": "db"},
        collector_settings_path=_SETTINGS,
    )
    sm._detect_endpoints = lambda: list(endpoints)
    sm._verify_consistency = lambda: None
    sm._build_proxy_cmd = lambda config_path: [
        sys.executable, FAKE_PROXY, "--config", str(config_path)
    ]

    orig_proxy_launch = runner.launch

    def proxy_spy(name, cmd, **kw):
        launch_order.append(name)
        return orig_proxy_launch(name, cmd, **kw)
    runner.launch = proxy_spy

    spec = ModuleSpec(
        name="collector",
        executable=sys.executable,
        args=[DUMMY_CHILD, "--argv-out", str(argv_file)],
        working_dir=str(runtime_dir),
    )
    pm = ProcessManager(runtime_dir=runtime_dir, modules={"collector": spec})

    orig_coll_launch = pm._runner.launch

    def coll_spy(name, cmd, *a, **k):
        launch_order.append(name)
        captured["cmd"] = list(cmd)
        captured["proxy_ready_at_collector_launch"] = [
            _connectable(p) for p in captured.get("ports", [])
        ]
        r = orig_coll_launch(name, cmd, *a, **k)
        captured["coll_pid"] = r[0]
        return r
    pm._runner.launch = coll_spy

    req = types.SimpleNamespace(
        app=types.SimpleNamespace(
            state=types.SimpleNamespace(
                pm=pm, sidecar_manager=sm, log_manager=_FakeLogManager()
            )
        )
    )
    return sm, pm, req


def _wait(cond, timeout=5.0, interval=0.05):
    end = time.time() + timeout
    while time.time() < end:
        if cond():
            return True
        time.sleep(interval)
    return cond()


async def test_us1_with_opcda_proxy_first_then_collector(runtime_dir):
    port = _free_port()
    ep = "host-a"
    save_port_mapping(runtime_dir / "opcda_proxy" / "port_mapping.json", {ep: port})
    argv_file = runtime_dir / "collector_argv.txt"
    launch_order = []
    captured = {"ports": [port]}
    sm, pm, req = _build(
        runtime_dir, endpoints=[ep], argv_file=argv_file,
        launch_order=launch_order, captured=captured,
    )

    proxy_pid = None
    try:
        await start_process("collector", req, _user="u")
        proxy_pid = sm._proxies[ep].pid

        # ① proxy 先 launch、collector 后 launch
        assert launch_order == [f"proxy_{port}", "collector"]
        # ② collector launch 那一刻 proxy 端口已 ready
        assert captured["proxy_ready_at_collector_launch"] == [True]

        # extra_args 真透传到 collector 进程命令
        cmd = captured["cmd"]
        assert "--config" in cmd and _SETTINGS in cmd
        mapping_path = str(runtime_dir / "opcda_proxy" / "port_mapping.json")
        assert "--port-mapping" in cmd and mapping_path in cmd

        # 进一步：collector 子进程自身 argv 也收到了（落盘 argv-out）
        assert _wait(lambda: argv_file.exists())
        argv = argv_file.read_text(encoding="utf-8")
        assert "--config" in argv and _SETTINGS in argv
        assert "--port-mapping" in argv and mapping_path in argv

        # 状态：mapping 落盘、proxy 在监听、collector 在跑
        assert load_port_mapping(runtime_dir / "opcda_proxy" / "port_mapping.json") == {ep: port}
        assert _connectable(port)
        assert pm.status("collector").status == "running"
    finally:
        if "coll_pid" in captured:
            _kill_tree(captured["coll_pid"])
        if proxy_pid is not None:
            _kill_tree(proxy_pid)
        sm.runner.close_log(f"proxy_{port}")
        pm._runner.close_log("collector")


async def test_us1_no_opcda_collector_only_config(runtime_dir):
    argv_file = runtime_dir / "collector_argv.txt"
    launch_order = []
    captured = {"ports": []}
    sm, pm, req = _build(
        runtime_dir, endpoints=[], argv_file=argv_file,
        launch_order=launch_order, captured=captured,
    )

    try:
        await start_process("collector", req, _user="u")

        # 无 OPC DA：零 proxy，只 launch collector
        assert launch_order == ["collector"]
        assert sm._proxies == {}

        # collector 命令只有 --config，无 --port-mapping（锁 port_mapping_path=None 语义）
        cmd = captured["cmd"]
        assert "--config" in cmd and _SETTINGS in cmd
        assert "--port-mapping" not in cmd

        assert _wait(lambda: argv_file.exists())
        argv = argv_file.read_text(encoding="utf-8")
        assert "--config" in argv
        assert "--port-mapping" not in argv

        assert pm.status("collector").status == "running"
    finally:
        if "coll_pid" in captured:
            _kill_tree(captured["coll_pid"])
        pm._runner.close_log("collector")
