from __future__ import annotations

import logging
import os
import subprocess
import time
from pathlib import Path
from typing import BinaryIO

import psutil
import toml

logger = logging.getLogger(__name__)


class ManagedProcessError(Exception):
    pass


class ManagedProcessRunner:
    """共享进程监管内核。

    封装 ProcessManager（collector/writer）与 SidecarManager（动态 proxy）共用的
    底层能力：PID 文件读写/校验（pid+create_time）、子进程 spawn + stdout/stderr
    重定向到日志文件、强制停止（terminate→kill）。

    **不**包含 shutdown_flag 优雅停止协议——那是 collector/writer 专用，保留在
    ProcessManager。runner 只负责"按 PID 文件追踪 + 启动 + 强停"这层机制。
    """

    def __init__(self, runtime_dir: Path):
        self.runtime_dir = Path(runtime_dir)
        self.pids_dir = self.runtime_dir / "pids"
        # 子进程 stdout/stderr 重定向文件句柄（launch 时打开，close_log 时关闭）
        self._log_files: dict[str, BinaryIO] = {}

    # ── 目录与路径 ──────────────────────────────────────────────
    def ensure_dirs(self) -> None:
        self.pids_dir.mkdir(parents=True, exist_ok=True)

    def pid_file(self, name: str) -> Path:
        return self.pids_dir / f"{name}.pid"

    def log_file(self, name: str) -> Path:
        """子进程控制台输出落盘路径（实时日志唯一来源，LogManager.attach 也读它）。"""
        return self.runtime_dir / "logs" / name / "current.log"

    def close_log(self, name: str) -> None:
        fp = self._log_files.pop(name, None)
        if fp is not None:
            try:
                fp.close()
            except OSError:
                pass

    # ── PID 文件 ────────────────────────────────────────────────
    def read_pid_record(self, name: str) -> dict | None:
        path = self.pid_file(name)
        if not path.exists():
            return None
        try:
            return toml.load(path)
        except (toml.TomlDecodeError, OSError):
            logger.warning("Corrupt PID file: %s, treating as no record", path)
            return None

    def write_pid_record(self, name: str, pid: int, create_time: float) -> None:
        path = self.pid_file(name)
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

    def delete_pid_record(self, name: str) -> None:
        path = self.pid_file(name)
        try:
            path.unlink(missing_ok=True)
        except OSError as exc:
            logger.warning("Failed to delete PID file %s: %s", path, exc)

    def verify_alive(self, record: dict) -> psutil.Process | None:
        """以 pid + create_time 校验记录指向的进程仍是原进程（防 PID 复用误判）。"""
        try:
            proc = psutil.Process(record["pid"])
            recorded = float(record.get("create_time", 0))
            if abs(proc.create_time() - recorded) < 1.0:
                return proc
        except (psutil.NoSuchProcess, psutil.AccessDenied, KeyError, ValueError):
            return None
        return None

    # ── 启动与停止 ──────────────────────────────────────────────
    def launch(
        self,
        name: str,
        cmd: list[str],
        cwd: str,
        env: dict[str, str] | None = None,
    ) -> tuple[int, float]:
        """spawn 子进程 + 日志重定向 + 写 PID 文件，返回 (pid, create_time)。

        子进程 stdout/stderr → logs/{name}/current.log（启动前截断，PYTHONUNBUFFERED
        保证实时落盘）。立即退出或 PID 落盘失败时回收进程并抛 ManagedProcessError。
        """
        self.ensure_dirs()

        log_path = self.log_file(name)
        log_path.parent.mkdir(parents=True, exist_ok=True)
        self.close_log(name)  # 防御：关掉可能残留的旧句柄
        log_fp = open(log_path, "wb")  # 二进制 sink，交给子进程直接写

        run_env = {**os.environ, "PYTHONUNBUFFERED": "1"}
        if env:
            run_env.update(env)

        try:
            popen = subprocess.Popen(
                cmd,
                cwd=cwd,
                close_fds=True,
                creationflags=subprocess.CREATE_NO_WINDOW,
                stdout=log_fp,
                stderr=subprocess.STDOUT,
                env=run_env,
            )
        except (OSError, FileNotFoundError) as exc:
            log_fp.close()
            raise ManagedProcessError(f"spawn failed for {name}: {exc}") from exc
        self._log_files[name] = log_fp

        try:
            proc = psutil.Process(popen.pid)
            create_time = proc.create_time()
        except psutil.NoSuchProcess as exc:
            # 子进程秒退：其继承的 fd 已随进程释放，关掉我方残留句柄避免泄漏
            self.close_log(name)
            raise ManagedProcessError(
                f"spawned process for {name} died immediately: {exc}"
            ) from exc

        try:
            self.write_pid_record(name, popen.pid, create_time)
        except OSError as exc:
            # PID 文件落不下 → 子进程已起但无法追踪，必须回收
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
            self.close_log(name)
            raise ManagedProcessError(
                f"failed to persist PID file for {name}: {exc}"
            ) from exc

        return popen.pid, create_time

    def force_stop(self, proc: psutil.Process) -> None:
        """Terminate a managed process and every descendant it has already spawned."""
        try:
            descendants = proc.children(recursive=True)
        except psutil.Error:
            descendants = []

        processes = [proc, *descendants]
        for target in processes:
            try:
                target.terminate()
            except psutil.NoSuchProcess:
                pass
            except psutil.Error as exc:
                logger.error("force_stop terminate failed for pid=%s: %s", target.pid, exc)

        _, alive = psutil.wait_procs(processes, timeout=2.0)
        for target in alive:
            try:
                target.kill()
            except psutil.NoSuchProcess:
                pass
            except psutil.Error as exc:
                logger.error("force_stop kill failed for pid=%s: %s", target.pid, exc)
        if alive:
            psutil.wait_procs(alive, timeout=2.0)
