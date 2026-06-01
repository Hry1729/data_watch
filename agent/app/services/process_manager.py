from __future__ import annotations

import asyncio
import json
import logging
import os
import subprocess
import time
from dataclasses import dataclass
from pathlib import Path
from typing import BinaryIO, Literal

import psutil
import toml

logger = logging.getLogger(__name__)

ProcessStatus = Literal["running", "stopped", "crashed", "unknown"]

_STOP_TIMEOUT_DEFAULT = 10.0
_STOP_POLL_INTERVAL = 0.5
# 监控循环节奏：兼做崩溃检测与 CPU 采样（FR-A09 要求 CPU 每 2s 采一次）
_HEALTH_CHECK_INTERVAL = 2.0


@dataclass(frozen=True)
class ModuleSpec:
    name: str
    executable: str
    args: list[str]
    working_dir: str


@dataclass
class ProcessInfo:
    name: str
    status: ProcessStatus
    pid: int | None
    started_at: str | None
    uptime_seconds: int
    memory_mb: float = 0.0
    cpu_percent: float = 0.0
    restart_count: int = 0


class ProcessManagerError(Exception):
    pass


class ProcessManager:
    def __init__(self, runtime_dir: Path, modules: dict[str, ModuleSpec]):
        self.runtime_dir = Path(runtime_dir)
        self.modules = modules
        self.pids_dir = self.runtime_dir / "pids"
        self.state_dir = self.runtime_dir / "state"
        self.state_file = self.state_dir / "process_state.json"
        self._last_status: dict[str, str] = {}
        # CPU 采样缓存：仅在事件循环线程内写（health loop）/读（async status 路由），
        # 不跨线程，故无需加锁；若未来从线程池调用 status() 需重新评估。
        self._cpu_cache: dict[str, float] = {}
        self._cpu_procs: dict[str, psutil.Process] = {}
        self._locks: dict[str, asyncio.Lock] = {
            name: asyncio.Lock() for name in modules
        }
        # 子进程 stdout/stderr 重定向文件句柄（spawn 时打开，stop 时关闭）
        self._log_files: dict[str, BinaryIO] = {}

    def _ensure_dirs(self) -> None:
        self.pids_dir.mkdir(parents=True, exist_ok=True)

    def _pid_file(self, name: str) -> Path:
        return self.pids_dir / f"{name}.pid"

    def _flag_file(self, name: str) -> Path:
        return self.runtime_dir / f"{name}.shutdown_flag"

    def log_file(self, name: str) -> Path:
        """子进程控制台输出落盘路径（实时日志唯一来源，LogManager.attach 也读它）。"""
        return self.runtime_dir / "logs" / name / "current.log"

    def _close_log_file(self, name: str) -> None:
        fp = self._log_files.pop(name, None)
        if fp is not None:
            try:
                fp.close()
            except OSError:
                pass

    def _read_pid_file(self, name: str) -> dict | None:
        path = self._pid_file(name)
        if not path.exists():
            return None
        try:
            return toml.load(path)
        except (toml.TomlDecodeError, OSError):
            logger.warning("Corrupt PID file: %s, treating as no record", path)
            return None

    def _write_pid_file(self, name: str, pid: int, create_time: float) -> None:
        path = self._pid_file(name)
        data = {
            "pid": pid,
            "create_time": create_time,
            "started_at": time.strftime(
                "%Y-%m-%d %H:%M:%S",
                time.localtime(create_time),
            ),
            "module": name,
        }
        path.write_text(toml.dumps(data), encoding="utf-8")

    def _delete_pid_file(self, name: str) -> None:
        path = self._pid_file(name)
        try:
            path.unlink(missing_ok=True)
        except OSError as exc:
            logger.warning("Failed to delete PID file %s: %s", path, exc)

    # ── restart_count 持久化（runtime/state/process_state.json）──────────────
    def _load_state(self) -> dict:
        if not self.state_file.exists():
            return {}
        try:
            data = json.loads(self.state_file.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError, UnicodeDecodeError):
            logger.warning("Corrupt process_state.json, treating as empty")
            return {}
        return data if isinstance(data, dict) else {}

    def _restart_count(self, name: str) -> int:
        entry = self._load_state().get(name)
        if not isinstance(entry, dict):
            return 0
        try:
            return int(entry.get("restart_count", 0))
        except (TypeError, ValueError):
            return 0

    def _bump_restart_count(self, name: str) -> int:
        state = self._load_state()
        entry = state.get(name)
        if not isinstance(entry, dict):
            entry = {}
        try:
            current = int(entry.get("restart_count", 0))
        except (TypeError, ValueError):
            current = 0
        count = current + 1
        entry["restart_count"] = count
        state[name] = entry
        self.state_dir.mkdir(parents=True, exist_ok=True)
        # 临时文件 + 原子替换，避免写入中崩溃产生半截 JSON
        tmp = self.state_file.with_suffix(".json.tmp")
        tmp.write_text(
            json.dumps(state, indent=2, ensure_ascii=False),
            encoding="utf-8",
        )
        os.replace(tmp, self.state_file)
        return count

    def _verify_alive(self, record: dict) -> psutil.Process | None:
        try:
            proc = psutil.Process(record["pid"])
            recorded = float(record.get("create_time", 0))
            if abs(proc.create_time() - recorded) < 1.0:
                return proc
        except (psutil.NoSuchProcess, psutil.AccessDenied, KeyError, ValueError):
            return None
        return None

    async def start(self, name: str) -> ProcessInfo:
        if name not in self.modules:
            raise ProcessManagerError(f"unknown module: {name}")

        async with self._locks[name]:
            spec = self.modules[name]
            self._ensure_dirs()

            record = self._read_pid_file(name)
            if record is not None:
                proc = self._verify_alive(record)
                if proc is not None:
                    logger.info("module %s already running pid=%s", name, proc.pid)
                    return self._info_running(name, proc, record)
                self._delete_pid_file(name)

            if not spec.executable:
                raise ProcessManagerError(f"module {name}: executable not configured")
            if not spec.working_dir:
                raise ProcessManagerError(f"module {name}: working_dir not configured")

            cmd = [spec.executable, *spec.args]
            logger.info("starting module %s: cmd=%s cwd=%s", name, cmd, spec.working_dir)

            # 子进程 stdout/stderr → logs/{name}/current.log（实时日志数据源）；
            # 启动前截断（"wb"）避免文件无限增长；PYTHONUNBUFFERED 保证实时落盘。
            log_path = self.log_file(name)
            log_path.parent.mkdir(parents=True, exist_ok=True)
            self._close_log_file(name)  # 防御：关掉可能残留的旧句柄
            log_fp = open(log_path, "wb")  # 二进制 sink，交给子进程直接写
            try:
                popen = subprocess.Popen(
                    cmd,
                    cwd=spec.working_dir,
                    close_fds=True,
                    creationflags=subprocess.CREATE_NO_WINDOW,
                    stdout=log_fp,
                    stderr=subprocess.STDOUT,
                    env={**os.environ, "PYTHONUNBUFFERED": "1"},
                )
            except (OSError, FileNotFoundError) as exc:
                log_fp.close()
                raise ProcessManagerError(f"spawn failed for {name}: {exc}") from exc
            self._log_files[name] = log_fp

            try:
                proc = psutil.Process(popen.pid)
                create_time = proc.create_time()
            except psutil.NoSuchProcess as exc:
                # 子进程秒退：其继承的 fd 已随进程释放，关掉我方残留句柄避免泄漏
                self._close_log_file(name)
                raise ProcessManagerError(
                    f"spawned process for {name} died immediately: {exc}"
                ) from exc

            try:
                self._write_pid_file(name, popen.pid, create_time)
            except OSError as exc:
                # PID 文件落不下 → 子进程已起但 Agent 无法追踪，必须回收
                logger.error(
                    "module %s: failed to write PID file: %s; terminating spawned process",
                    name,
                    exc,
                )
                try:
                    proc.terminate()
                    try:
                        proc.wait(timeout=2.0)
                    except psutil.TimeoutExpired:
                        proc.kill()
                except psutil.Error:
                    pass
                self._close_log_file(name)
                raise ProcessManagerError(
                    f"failed to persist PID file for {name}: {exc}"
                ) from exc
            logger.info("module %s started pid=%s", name, popen.pid)

            return ProcessInfo(
                name=name,
                status="running",
                pid=popen.pid,
                started_at=time.strftime(
                    "%Y-%m-%d %H:%M:%S",
                    time.localtime(create_time),
                ),
                uptime_seconds=0,
            )

    async def stop(
        self,
        name: str,
        timeout: float = _STOP_TIMEOUT_DEFAULT,
    ) -> ProcessInfo:
        if name not in self.modules:
            raise ProcessManagerError(f"unknown module: {name}")

        async with self._locks[name]:
            self._ensure_dirs()

            record = self._read_pid_file(name)
            if record is None:
                return ProcessInfo(
                    name=name,
                    status="stopped",
                    pid=None,
                    started_at=None,
                    uptime_seconds=0,
                )

            proc = self._verify_alive(record)
            if proc is None:
                self._delete_pid_file(name)
                try:
                    self._flag_file(name).unlink(missing_ok=True)
                except OSError as exc:
                    logger.warning(
                        "Failed to delete stale flag %s: %s",
                        self._flag_file(name),
                        exc,
                    )
                self._close_log_file(name)
                return ProcessInfo(
                    name=name,
                    status="stopped",
                    pid=None,
                    started_at=None,
                    uptime_seconds=0,
                )

            flag_path = self._flag_file(name)
            try:
                flag_path.write_text("", encoding="utf-8")
            except OSError as exc:
                raise ProcessManagerError(
                    f"failed to write shutdown_flag for {name}: {exc}"
                ) from exc
            logger.info("module %s: wrote shutdown_flag at %s", name, flag_path)

            deadline = time.monotonic() + timeout
            while time.monotonic() < deadline:
                if not proc.is_running():
                    break
                await asyncio.sleep(_STOP_POLL_INTERVAL)

            if proc.is_running():
                logger.warning("module %s: timeout, terminating pid=%s", name, proc.pid)
                try:
                    proc.terminate()
                    try:
                        proc.wait(timeout=2.0)
                    except psutil.TimeoutExpired:
                        proc.kill()
                except psutil.Error as exc:
                    logger.error("module %s terminate failed: %s", name, exc)

            self._delete_pid_file(name)
            try:
                flag_path.unlink(missing_ok=True)
            except OSError:
                pass

            self._close_log_file(name)
            logger.info("module %s stopped", name)
            return ProcessInfo(
                name=name,
                status="stopped",
                pid=None,
                started_at=None,
                uptime_seconds=0,
            )

    async def restart(self, name: str) -> ProcessInfo:
        if name not in self.modules:
            raise ProcessManagerError(f"unknown module: {name}")
        await self.stop(name)
        info = await self.start(name)
        count = self._bump_restart_count(name)
        info.restart_count = count
        logger.info("module %s restarted, restart_count=%d", name, count)
        return info

    def status(self, name: str) -> ProcessInfo:
        if name not in self.modules:
            raise ProcessManagerError(f"unknown module: {name}")

        record = self._read_pid_file(name)
        if record is None:
            return ProcessInfo(
                name=name,
                status="stopped",
                pid=None,
                started_at=None,
                uptime_seconds=0,
                restart_count=self._restart_count(name),
            )

        proc = self._verify_alive(record)
        if proc is None:
            return ProcessInfo(
                name=name,
                status="crashed",
                pid=record.get("pid"),
                started_at=record.get("started_at"),
                uptime_seconds=0,
                restart_count=self._restart_count(name),
            )

        return self._info_running(name, proc, record)

    def _sample_cpu(self, name: str, info: ProcessInfo) -> None:
        """在监控循环中调用：维护每模块的持久 psutil.Process 并缓存 CPU%。

        cpu_percent(interval=None) 非阻塞，返回自上次对同一 Process 对象调用以来的
        占用；故必须复用同一 Process 对象，pid 变化时重建（重建后首轮为基线 0.0，
        下一轮才有真实增量）。
        """
        if info.status != "running" or info.pid is None:
            self._cpu_procs.pop(name, None)
            self._cpu_cache[name] = 0.0
            return
        proc = self._cpu_procs.get(name)
        if proc is None or proc.pid != info.pid:
            try:
                proc = psutil.Process(info.pid)
            except psutil.Error:
                self._cpu_procs.pop(name, None)
                self._cpu_cache[name] = 0.0
                return
            self._cpu_procs[name] = proc
        try:
            self._cpu_cache[name] = round(proc.cpu_percent(interval=None), 2)
        except psutil.Error:
            self._cpu_procs.pop(name, None)
            self._cpu_cache[name] = 0.0

    async def health_check_loop(
        self,
        interval: float = _HEALTH_CHECK_INTERVAL,
    ) -> None:
        logger.info("ProcessManager health check started, interval=%.1fs", interval)
        try:
            while True:
                await asyncio.sleep(interval)
                for name in self.modules:
                    try:
                        info = self.status(name)
                    except Exception as exc:
                        logger.warning("health check: %s status failed: %s", name, exc)
                        continue

                    self._sample_cpu(name, info)

                    current = info.status
                    prev = self._last_status.get(name)
                    if current != prev:
                        if prev == "running" and current == "crashed":
                            logger.error(
                                "module %s crashed (previous status: %s)",
                                name,
                                prev,
                            )
                        self._last_status[name] = current
        except asyncio.CancelledError:
            logger.info("ProcessManager health check stopped")
            raise

    def _info_running(
        self,
        name: str,
        proc: psutil.Process,
        record: dict,
    ) -> ProcessInfo:
        create_time = float(record.get("create_time", proc.create_time()))
        uptime = max(0, int(time.time() - create_time))
        try:
            memory_mb = proc.memory_info().rss / (1024 * 1024)
        except psutil.Error:
            memory_mb = 0.0
        return ProcessInfo(
            name=name,
            status="running",
            pid=proc.pid,
            started_at=record.get("started_at"),
            uptime_seconds=uptime,
            memory_mb=round(memory_mb, 2),
            cpu_percent=self._cpu_cache.get(name, 0.0),
            restart_count=self._restart_count(name),
        )
