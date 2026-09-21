"""进程内登录失败滑窗限流（DW-08）。"""
from __future__ import annotations

import threading
import time


class LoginLimiter:
    def __init__(self, max_failures: int, window_seconds: float) -> None:
        self._max_failures = max(1, int(max_failures))
        self._window_seconds = max(1.0, float(window_seconds))
        self._failures: dict[tuple[str, str], list[float]] = {}
        self._lock = threading.Lock()

    def _key(self, user_name: str, client_ip: str) -> tuple[str, str]:
        return (str(user_name or "").strip().lower(), str(client_ip or "").strip())

    def _prune_locked(self, key: tuple[str, str], now: float) -> list[float]:
        cutoff = now - self._window_seconds
        stamps = [ts for ts in self._failures.get(key, []) if ts > cutoff]
        if stamps:
            self._failures[key] = stamps
        else:
            self._failures.pop(key, None)
        return stamps

    def is_blocked(self, user_name: str, client_ip: str) -> bool:
        key = self._key(user_name, client_ip)
        now = time.monotonic()
        with self._lock:
            return len(self._prune_locked(key, now)) >= self._max_failures

    def record_failure(self, user_name: str, client_ip: str) -> None:
        key = self._key(user_name, client_ip)
        now = time.monotonic()
        with self._lock:
            stamps = self._prune_locked(key, now)
            stamps.append(now)
            self._failures[key] = stamps

    def reset(self, user_name: str, client_ip: str) -> None:
        key = self._key(user_name, client_ip)
        with self._lock:
            self._failures.pop(key, None)


_limiter: LoginLimiter | None = None
_limiter_lock = threading.Lock()


def get_login_limiter() -> LoginLimiter:
    global _limiter
    if _limiter is None:
        with _limiter_lock:
            if _limiter is None:
                from app.core.config import get_settings

                settings = get_settings()
                _limiter = LoginLimiter(
                    max_failures=settings.login_fail_max,
                    window_seconds=settings.login_fail_window_seconds,
                )
    return _limiter
