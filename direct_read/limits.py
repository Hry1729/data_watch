"""同设备最小间隔与全局并发上限。

状态放在本机目录，供多次短进程共享。不用平台缓存，避免和采集写路径搅在一起。
时钟用 wall clock：各进程的 monotonic 时钟不能互相比较。
"""

from __future__ import annotations

import json
import os
import sys
from pathlib import Path
from typing import Callable
from uuid import uuid4

from direct_read.audit import default_state_dir
from direct_read.errors import DirectReadError
from direct_read.quality import QUALITY_NOT_CONNECTED


MIN_DEVICE_INTERVAL_S = 0.5
MAX_CONCURRENCY = 2
# 单次读封顶 30 秒，再留几秒给断连；超过这个年龄的槽位视为泄漏。
_SLOT_STALE_S = 35.0


class RatePermit:
    def __init__(self, slot_path: Path) -> None:
        self._slot_path = slot_path
        self.released = False

    def release(self) -> None:
        if self.released:
            return
        self.released = True
        try:
            self._slot_path.unlink(missing_ok=True)
        except OSError:
            return

    def __enter__(self) -> "RatePermit":
        return self

    def __exit__(self, *_args: object) -> None:
        self.release()


class RateLimiter:
    def __init__(
        self,
        state_dir: Path | None = None,
        *,
        now: Callable[[], float] | None = None,
        min_interval_s: float = MIN_DEVICE_INTERVAL_S,
        max_concurrency: int = MAX_CONCURRENCY,
    ) -> None:
        self.state_dir = state_dir or default_state_dir()
        self.now = now or _wall_now
        # 调用方不能把护栏调松。
        self.min_interval_s = max(float(min_interval_s), MIN_DEVICE_INTERVAL_S)
        self.max_concurrency = min(max(1, int(max_concurrency)), MAX_CONCURRENCY)
        self.slot_dir = self.state_dir / "slots"
        self.lock_path = self.state_dir / "limits.lock"
        self.interval_path = self.state_dir / "device_interval.json"

    def acquire(self, device_code: str) -> RatePermit:
        self.state_dir.mkdir(parents=True, exist_ok=True)
        self.slot_dir.mkdir(parents=True, exist_ok=True)
        if not self.lock_path.exists() or self.lock_path.stat().st_size == 0:
            self.lock_path.write_text(" ", encoding="utf-8")
        with self.lock_path.open("r+", encoding="utf-8") as handle:
            _lock(handle)
            try:
                self._purge_stale()
                active = [path for path in self.slot_dir.glob("*.slot") if path.is_file()]
                if len(active) >= self.max_concurrency:
                    raise _limited(
                        "直连并发已达上限，请改用 realtime get",
                        device_code,
                        limit="concurrency",
                        min_interval_s=self.min_interval_s,
                        max_concurrency=self.max_concurrency,
                    )
                current = float(self.now())
                intervals = self._read_intervals()
                last = intervals.get(device_code)
                if last is not None and current - float(last) < self.min_interval_s:
                    raise _limited(
                        "同一设备的直连间隔过短，请改用 realtime get",
                        device_code,
                        limit="device_interval",
                        min_interval_s=self.min_interval_s,
                        max_concurrency=self.max_concurrency,
                    )
                slot_path = self.slot_dir / f"{os.getpid()}-{uuid4().hex}.slot"
                slot_path.write_text(
                    json.dumps(
                        {"pid": os.getpid(), "device": device_code, "started": current},
                        ensure_ascii=False,
                    ),
                    encoding="utf-8",
                )
                intervals[device_code] = current
                self._write_intervals(intervals)
                return RatePermit(slot_path)
            finally:
                _unlock(handle)

    def _purge_stale(self) -> None:
        current = float(self.now())
        for path in self.slot_dir.glob("*.slot"):
            try:
                payload = json.loads(path.read_text(encoding="utf-8"))
            except (OSError, ValueError):
                path.unlink(missing_ok=True)
                continue
            if not isinstance(payload, dict):
                path.unlink(missing_ok=True)
                continue
            pid = int(payload.get("pid") or 0)
            started = float(payload.get("started") or 0)
            if not _pid_alive(pid) or current - started > _SLOT_STALE_S:
                path.unlink(missing_ok=True)

    def _read_intervals(self) -> dict[str, float]:
        if not self.interval_path.exists():
            return {}
        try:
            payload = json.loads(self.interval_path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return {}
        if not isinstance(payload, dict):
            return {}
        intervals: dict[str, float] = {}
        for key, value in payload.items():
            try:
                intervals[str(key)] = float(value)
            except (TypeError, ValueError):
                continue
        return intervals

    def _write_intervals(self, intervals: dict[str, float]) -> None:
        temporary = self.interval_path.with_suffix(".json.tmp")
        temporary.write_text(
            json.dumps(intervals, ensure_ascii=False),
            encoding="utf-8",
        )
        temporary.replace(self.interval_path)


def _limited(
    message: str,
    device_code: str,
    *,
    limit: str,
    min_interval_s: float,
    max_concurrency: int,
) -> DirectReadError:
    return DirectReadError(
        "rate_limited",
        message,
        quality=QUALITY_NOT_CONNECTED,
        details={
            "limit": limit,
            "deviceCode": device_code,
            "minIntervalMs": int(min_interval_s * 1000),
            "maxConcurrency": max_concurrency,
        },
    )


def _wall_now() -> float:
    import time

    return time.time()


def _pid_alive(pid: int) -> bool:
    if pid <= 0:
        return False
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    except OSError:
        return False
    return True


def _lock(handle) -> None:
    if sys.platform == "win32":
        import msvcrt

        handle.seek(0)
        msvcrt.locking(handle.fileno(), msvcrt.LK_LOCK, 1)
        return
    import fcntl

    fcntl.flock(handle.fileno(), fcntl.LOCK_EX)


def _unlock(handle) -> None:
    if sys.platform == "win32":
        import msvcrt

        handle.seek(0)
        msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
        return
    import fcntl

    fcntl.flock(handle.fileno(), fcntl.LOCK_UN)
