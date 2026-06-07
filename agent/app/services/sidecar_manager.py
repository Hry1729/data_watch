"""SidecarManager —— OPC DA proxy 动态编排。

本文件当前仅实现 T013：proxy 可执行文件路径解析。spawn、端口闸门、认领、
差量启停等（T014~T020）后续按需扩展，不在此预先铺设。
"""

from __future__ import annotations

import json
import logging
import socket
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping

import psutil

from app.services.opcda_detect import (
    assign_sticky_ports,
    detect_center_endpoints,
    load_collector_mysql,
    load_port_mapping,
    save_port_mapping,
    verify_mysql_consistency,
)

logger = logging.getLogger(__name__)

PROXY_EXE_NAME = "OpcDaProxyService.exe"

# proxy 运行参数默认值（与现有 proxy_*.json 样例一致）。
DEFAULT_UPDATE_RATE_MS = 500
DEFAULT_CONNECTION_TIMEOUT_MS = 10000
# OPC server 标识默认值；serverProgId/channel 全场单一，由 factory 从 config[opcda] 注入（T020）。
DEFAULT_SERVER_PROG_ID = "ZPMC.OPCServer"
DEFAULT_CHANNEL = "OPD_Server"
# endpoint 归一为本地的取值（与 opcda_detect._normalize_endpoint 对应），生成 config 时映射为空 vmHost。
_LOCAL_ENDPOINTS = {"", "localhost", "127.0.0.1"}

# 候选目录（相对安装根/仓库根，依次查找）。集中定义，避免路径硬编码散落。
_PROXY_BIN_CANDIDATES = (
    Path("sidecars/opcda_proxy_service/bin"),            # 打包安装目录
    Path("collector/sidecars/opcda_proxy_service/bin"),  # 开发仓库
)


class ProxyExecutableNotFound(Exception):
    """在所有候选路径下都找不到 OpcDaProxyService.exe。"""


class ProxyStartError(Exception):
    """proxy spawn 后端口闸门未在超时内通过（含 endpoint/port/日志路径）。"""


@dataclass(frozen=True)
class ProxyInstance:
    """端口闸门通过后的 proxy 句柄。"""

    port: int
    pid: int
    create_time: float
    config_path: Path
    log_path: Path


@dataclass(frozen=True)
class CollectorStartPlan:
    """prepare_for_collector_start 的产物。

    port_mapping_path 为 None 表示无 OPC DA 端点——T019 据此**不**拼 --port-mapping。
    """

    port_mapping_path: Path | None
    proxies: list[ProxyInstance]


def _default_install_root() -> Path:
    # agent/app/services/sidecar_manager.py → parents[3] = 安装根（打包）/仓库根（开发）。
    # 注意：PyInstaller frozen（onefile/onedir）下 __file__ 可能指向解包临时目录，
    # 此推导未必成立。T014/T020 真正 spawn/接入前须验证 frozen 下根定位，
    # 届时 factory 应显式注入 install_root（构造参数已预留）。
    return Path(__file__).resolve().parents[3]


class SidecarManager:
    def __init__(
        self,
        *,
        runtime_dir: Path | str | None = None,
        install_root: Path | str | None = None,
        runner=None,
        agent_mysql: Mapping[str, Any] | None = None,
        collector_settings_path: Path | str | None = None,
        server_prog_id: str = DEFAULT_SERVER_PROG_ID,
        channel: str = DEFAULT_CHANNEL,
        update_rate_ms: int = DEFAULT_UPDATE_RATE_MS,
        connection_timeout_ms: int = DEFAULT_CONNECTION_TIMEOUT_MS,
    ):
        self.runtime_dir = Path(runtime_dir) if runtime_dir else None
        self.install_root = (
            Path(install_root).resolve() if install_root else _default_install_root()
        )
        self.runner = runner
        # 仅注入、不在构造里校验：无 OPC DA 场景也不应因配置文件暂缺而失败，
        # 校验留到 prepare_for_collector_start() 流程内。
        self._agent_mysql = agent_mysql
        self._collector_settings_path = (
            Path(collector_settings_path) if collector_settings_path else None
        )
        self._server_prog_id = server_prog_id
        self._channel = channel
        self._update_rate_ms = update_rate_ms
        self._connection_timeout_ms = connection_timeout_ms
        # 本进程内已起 proxy 注册表（key=endpoint）；跨 Agent 重启认领是 T022。
        self._proxies: dict[str, ProxyInstance] = {}

    @property
    def collector_settings_path(self) -> Path | None:
        """collector settings.toml 路径——与一致性校验同源，供 admin 拼 --config。"""
        return self._collector_settings_path

    # ── proxy 配置生成（T014）────────────────────────────────────
    def _opcda_dir(self) -> Path:
        if self.runtime_dir is None:
            raise ValueError("SidecarManager 未配置 runtime_dir，无法生成 proxy 配置")
        d = self.runtime_dir / "opcda_proxy"
        d.mkdir(parents=True, exist_ok=True)
        return d

    def generate_proxy_config(self, port: int, vm_host: str) -> Path:
        """生成 proxy_{port}.json，返回路径。endpoint 为本地时 vmHost 写空串。"""
        try:
            port = int(port)
        except (TypeError, ValueError):
            raise ValueError(f"非法 proxy 端口: {port!r}")
        if not (1 <= port <= 65535):
            raise ValueError(f"proxy 端口超出范围(1-65535): {port}")

        raw_vm = str(vm_host or "").strip()
        vm = "" if raw_vm.lower() in _LOCAL_ENDPOINTS else raw_vm
        config = {
            "listenPort": port,
            "updateRateMs": self._update_rate_ms,
            "connectionTimeoutMs": self._connection_timeout_ms,
            "opcServers": [
                {
                    "vmHost": vm,
                    "serverProgId": self._server_prog_id,
                    "channel": self._channel,
                    "devices": [],
                }
            ],
        }
        path = self._opcda_dir() / f"proxy_{port}.json"
        path.write_text(json.dumps(config, indent=2, ensure_ascii=False), encoding="utf-8")
        return path

    def _proxy_candidates(self) -> list[Path]:
        return [self.install_root / rel / PROXY_EXE_NAME for rel in _PROXY_BIN_CANDIDATES]

    def resolve_proxy_executable(self) -> Path:
        """返回第一个存在的 proxy exe；都不存在时抛出含全部候选路径的错误。"""
        candidates = self._proxy_candidates()
        for candidate in candidates:
            if candidate.is_file():
                return candidate
        listed = "; ".join(str(c) for c in candidates)
        raise ProxyExecutableNotFound(
            f"找不到 {PROXY_EXE_NAME}，已查找候选路径: {listed}"
        )

    # ── spawn + 端口闸门（T014-spawn / T015）─────────────────────
    def _build_proxy_cmd(self, config_path: Path) -> list[str]:
        """spawn 命令：<解析到的 exe> --config <proxy_{port}.json>。测试在此注入桩。"""
        return [str(self.resolve_proxy_executable()), "--config", str(config_path)]

    @staticmethod
    def _port_connectable(port: int) -> bool:
        try:
            with socket.create_connection(("127.0.0.1", port), timeout=0.5):
                return True
        except OSError:
            return False

    @staticmethod
    def _proxy_owns_listen(proc: psutil.Process, port: int) -> bool:
        """该端口的 LISTEN 属主是否为 proxy 进程本身或其后代（子树归属）。

        受管 proxy 经 venv redirector 启动时真正 bind 的是子进程，故须走子树；
        外部无关进程占同端口不在子树内 → 判否，守"不漂移"。
        """
        try:
            procs = [proc, *proc.children(recursive=True)]
        except psutil.Error:
            return False
        for p in procs:
            try:
                for c in p.net_connections(kind="inet"):
                    if (
                        c.status == psutil.CONN_LISTEN
                        and c.laddr
                        and c.laddr.port == port
                        # 须与 ① 的 127.0.0.1 连接同端点：回环或通配地址才覆盖本地连接，
                        # 监听在具体 LAN IP 不服务 127.0.0.1，不算归属（防 ①②指向不同 socket）。
                        and c.laddr.ip in ("127.0.0.1", "0.0.0.0", "::", "::1")
                    ):
                        return True
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue
        return False

    def _terminate_tree(self, proc: psutil.Process) -> None:
        """整树强停。

        先快照后代（父被杀后 orphan 经 ppid 不可再枚举），再**先杀父**止住继续派生、
        后杀快照子代——缩小"父在回收期间又 spawn"的窗口。
        """
        try:
            kids = proc.children(recursive=True)
        except psutil.Error:
            kids = []
        for p in (proc, *kids):
            try:
                self.runner.force_stop(p)
            except psutil.Error:
                pass

    def start_proxy(
        self,
        *,
        port: int,
        vm_host: str,
        endpoint: str,
        ready_timeout: float = 10.0,
        poll_interval: float = 0.1,
    ) -> ProxyInstance:
        """生成 config → spawn proxy → 端口闸门三条件轮询；通过返回实例，否则整树回收 + 抛错。

        三条件须同时满足：① 127.0.0.1:{port} 可连接；② 该端口 LISTEN 属主在 proxy
        子树内；③ proxy 进程仍存活。超时或进程先退 → 不返回实例，错误含 endpoint/port/日志路径。
        """
        if self.runner is None:
            raise ValueError("SidecarManager 未配置 runner，无法 spawn proxy")

        config_path = self.generate_proxy_config(port, vm_host)
        name = f"proxy_{port}"
        cmd = self._build_proxy_cmd(config_path)
        pid, create_time = self.runner.launch(name, cmd, cwd=str(self.runtime_dir))
        proc = psutil.Process(pid)
        log_path = self.runner.log_file(name)

        deadline = time.monotonic() + ready_timeout
        while time.monotonic() < deadline:
            if not proc.is_running():
                break  # proxy 进程先退（如端口被占致 bind 失败）→ 提前判失败
                # 注：不查 proc.status()——Windows 无 POSIX zombie，且 status() 在进程
                # 刚退时抛 NoSuchProcess，会绕过统一失败路径；is_running() 对死进程返回 False。
            if self._port_connectable(port) and self._proxy_owns_listen(proc, port):
                return ProxyInstance(
                    port=port,
                    pid=pid,
                    create_time=create_time,
                    config_path=config_path,
                    log_path=log_path,
                )
            time.sleep(poll_interval)

        self._terminate_tree(proc)
        self.runner.close_log(name)
        self.runner.delete_pid_record(name)
        raise ProxyStartError(
            f"proxy 端口闸门未通过: endpoint={endpoint} port={port} 日志={log_path}"
        )

    # ── prepare 编排（T018）──────────────────────────────────────
    def _mapping_path(self) -> Path:
        return self._opcda_dir() / "port_mapping.json"

    def _detect_endpoints(self) -> list[str]:
        """薄封装，便于单测 stub，不连真 MySQL。"""
        return detect_center_endpoints(self._agent_mysql)

    def _verify_consistency(self) -> None:
        """Agent 与 collector 的 MySQL 一致性（FR-P13）；薄封装便于单测 stub。"""
        collector_mysql = load_collector_mysql(self._collector_settings_path)
        verify_mysql_consistency(self._agent_mysql, collector_mysql)

    @staticmethod
    def _instance_alive(inst: ProxyInstance) -> bool:
        """轻量校验：pid 存活且 create_time 一致（防 pid 复用误判）。"""
        try:
            proc = psutil.Process(inst.pid)
            return proc.is_running() and proc.create_time() == inst.create_time
        except psutil.Error:
            return False

    def _stop_instance(self, inst: ProxyInstance) -> None:
        """停掉一个 proxy 实例：整树强停 + 收日志 + 清 PID 记录（回滚/US5 复用）。"""
        try:
            proc = psutil.Process(inst.pid)
        except psutil.Error:
            proc = None
        if proc is not None:
            self._terminate_tree(proc)
        name = f"proxy_{inst.port}"
        self.runner.close_log(name)
        self.runner.delete_pid_record(name)

    def prepare_for_collector_start(
        self,
        *,
        ready_timeout: float = 10.0,
        poll_interval: float = 0.1,
    ) -> CollectorStartPlan:
        """检测→粘性映射→差量起缺失 proxy→端口闸门，返回供 collector 启动的计划（FR-P14）。

        无 OPC DA 端点 → 返回 port_mapping_path=None（不拼 --port-mapping）。
        某个新增 proxy 启动失败 → 只回滚**本次新起**的 proxy，保留既有，再抛 ProxyStartError。
        """
        endpoints = self._detect_endpoints()
        if not endpoints:
            return CollectorStartPlan(port_mapping_path=None, proxies=[])

        self._verify_consistency()

        mapping_path = self._mapping_path()
        existing = load_port_mapping(mapping_path)
        mapping = assign_sticky_ports(endpoints, existing)
        save_port_mapping(mapping_path, mapping)  # 先写后起：失败也保住粘性端口，不漂移

        started_this_run: list[tuple[str, ProxyInstance]] = []
        proxies: list[ProxyInstance] = []
        try:
            for ep in endpoints:
                inst = self._proxies.get(ep)
                if inst is not None and self._instance_alive(inst):
                    proxies.append(inst)
                    continue
                if inst is not None:  # 注册表里有但已死 → 剔除后重起
                    self._proxies.pop(ep, None)
                inst = self.start_proxy(
                    port=mapping[ep],
                    vm_host=ep,
                    endpoint=ep,
                    ready_timeout=ready_timeout,
                    poll_interval=poll_interval,
                )
                started_this_run.append((ep, inst))
                proxies.append(inst)
        except ProxyStartError:
            for _ep, inst in started_this_run:  # 只清本次新起的，既有/认领的不动
                self._stop_instance(inst)
            raise

        for ep, inst in started_this_run:
            self._proxies[ep] = inst
        return CollectorStartPlan(port_mapping_path=mapping_path, proxies=proxies)

    # ── 启动认领（T022）─────────────────────────────────────────
    def claim_existing_proxies(self) -> None:
        """Agent 启动时认领仍存活的 proxy（FR-P15，不依赖硬编码名单）。

        扫 `pids/proxy_*.pid` → pid+create_time 校验存活则重建 ProxyInstance 入注册表，
        死亡/stale → 清 pid 记录。约束：
        - mapping 文件缺失/损坏 → 跳过整轮认领（不崩 Agent 启动）；collector 启动时
          prepare 会对损坏 mapping fail-fast，不丢错。
        - 活着但端口不在 mapping（不可映射，仅 mapping 被删/损坏才会发生）→ 跳过：
          保留运行、不认领、不杀（无法证明归属）。
        - 逐条 try/except 兜底：个别坏 pid 清理/跳过，不让一条坏数据中断整轮。
        """
        if self.runtime_dir is None or self.runner is None:
            return
        pids_dir = self.runtime_dir / "pids"
        if not pids_dir.exists():
            return
        try:
            mapping = load_port_mapping(self._mapping_path())
        except Exception as exc:
            logger.warning("认领跳过：port_mapping 不可读/损坏: %s", exc)
            return
        # 反转 port→endpoint；重复端口（损坏 mapping 才会出现）无法证明归属 → 排除、视为不可映射。
        port_to_ep: dict[int, str] = {}
        dup_ports: set[int] = set()
        for ep, port in mapping.items():
            if port in port_to_ep:
                dup_ports.add(port)
            port_to_ep[port] = ep
        for port in dup_ports:
            logger.warning("认领跳过端口 %s：mapping 中重复，无法确定归属 endpoint", port)
            port_to_ep.pop(port, None)
        try:
            pid_files = list(pids_dir.glob("proxy_*.pid"))
        except OSError as exc:
            logger.warning("认领跳过：扫描 pids 目录失败: %s", exc)
            return
        for pid_file in pid_files:
            try:
                self._claim_one(pid_file, port_to_ep)
            except Exception:
                logger.warning("认领 proxy 失败（已跳过）: %s", pid_file, exc_info=True)

    def _claim_one(self, pid_file: Path, port_to_ep: dict[int, str]) -> None:
        name = pid_file.stem  # "proxy_{port}"
        try:
            port = int(name[len("proxy_"):])
        except ValueError:
            return  # 文件名不含合法端口，忽略
        record = self.runner.read_pid_record(name)
        if record is None:
            return
        if self.runner.verify_alive(record) is None:
            self.runner.delete_pid_record(name)  # 死亡/stale → 清记录
            return
        ep = port_to_ep.get(port)
        if ep is None:
            return  # 活着但不可映射 → 保留运行、不认领、不杀
        self._proxies[ep] = ProxyInstance(
            port=port,
            pid=record["pid"],
            create_time=float(record["create_time"]),
            config_path=self._opcda_dir() / f"proxy_{port}.json",
            log_path=self.runner.log_file(name),
        )
