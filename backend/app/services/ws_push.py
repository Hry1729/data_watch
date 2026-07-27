from __future__ import annotations

import asyncio
import json
import logging
import time as _time
from typing import Any

from app.core.status_thresholds import UNSTABLE_MAX_AGE_SECONDS

logger = logging.getLogger("backend.ws_push")

_td_health: dict = {"ok": None, "ts": 0.0}
_TD_HEALTH_TTL = 30.0


def _latest_is_stale(latest: dict[str, Any]) -> bool:
    timestamps = [
        entry.get("t")
        for entry in latest.values()
        if isinstance(entry, dict) and isinstance(entry.get("t"), (int, float))
    ]
    if not timestamps:
        return True
    return _time.time() * 1000 - max(timestamps) > UNSTABLE_MAX_AGE_SECONDS * 1000


async def _check_tdengine() -> bool | None:
    now = _time.monotonic()
    if now - _td_health["ts"] < _TD_HEALTH_TTL:
        return _td_health["ok"]
    try:
        from app.db.tdengine import get_tdengine_client
        client = get_tdengine_client()
        conn = await asyncio.to_thread(client.connect)
        conn.close()
        _td_health.update(ok=True, ts=now)
        return True
    except Exception:
        _td_health.update(ok=False, ts=now)
        return False


class WsPushManager:
    def __init__(self):
        self._subscribers: dict[tuple, set] = {}
        self._tasks: dict[tuple, asyncio.Task] = {}
        self._snapshots: dict[tuple, dict] = {}
        # 离线状态去重：仅在 在线→离线 跳变时推一次 offline，避免每 0.1s 刷屏。
        self._offline: dict[tuple, bool] = {}

    async def subscribe(self, ws, device_code: str, group_code: str) -> None:
        key = (device_code, group_code)
        self._subscribers.setdefault(key, set()).add(ws)
        if key not in self._tasks:
            self._tasks[key] = asyncio.create_task(
                self._push_loop(device_code, group_code)
            )
        latest = await self._read_latest(device_code, group_code)
        offline = latest is None or _latest_is_stale(latest)
        snapshot = {} if offline else latest
        self._snapshots[key] = snapshot
        self._offline[key] = offline
        td_ok = await _check_tdengine()
        await ws.send_json(
            {
                "type": "snapshot",
                "device_code": device_code,
                "group_code": group_code,
                "values": snapshot,
                "tdengineConnected": td_ok,
                "deviceOffline": offline,
            }
        )

    async def unsubscribe(self, ws, device_code: str, group_code: str) -> None:
        key = (device_code, group_code)
        subs = self._subscribers.get(key, set())
        subs.discard(ws)
        if not subs:
            t = self._tasks.pop(key, None)
            if t:
                t.cancel()
            self._subscribers.pop(key, None)
            self._snapshots.pop(key, None)
            self._offline.pop(key, None)

    async def remove_all_subscriptions(self, ws) -> None:
        keys = list(self._subscribers.keys())
        for key in keys:
            if ws in self._subscribers.get(key, set()):
                await self.unsubscribe(ws, key[0], key[1])

    async def _read_latest(self, device_code: str, group_code: str) -> dict[str, Any] | None:
        """从 Redis 读取最新哈希。当 Redis 不可用时返回 None（调用方应跳过更新周期）。当键不存在时返回 {}（设备无数据/离线）。"""
        from app.db.redis import get_redis_client
        try:
            r = await get_redis_client()
            raw = await r.hgetall(f"rt:latest:{device_code}:{group_code}")
            result: dict[str, Any] = {}
            for var_id, json_str in raw.items():
                try:
                    result[var_id] = json.loads(json_str)
                except Exception:
                    pass
            return result
        except Exception as exc:
            logger.warning(
                "Redis unavailable for device=%s group=%s: %s",
                device_code, group_code, exc,
            )
            return None

    async def _push_loop(self, device_code: str, group_code: str) -> None:
        key = (device_code, group_code)
        while True:
            await asyncio.sleep(0.1)
            try:
                latest = await self._read_latest(device_code, group_code)
                if latest is None or _latest_is_stale(latest):
                    if not self._offline.get(key, False):
                        await self._broadcast(
                            key,
                            {"type": "offline", "device_code": device_code, "group_code": group_code},
                        )
                        self._offline[key] = True
                    self._snapshots[key] = {}
                    continue
                prev = self._snapshots.get(key, {})
                self._offline[key] = False
                diff = {k: v for k, v in latest.items() if v != prev.get(k)}
                if diff:
                    self._snapshots[key] = {**prev, **diff}
                    await self._broadcast(
                        key,
                        {
                            "type": "diff",
                            "device_code": device_code,
                            "group_code": group_code,
                            "values": diff,
                        },
                    )
            except asyncio.CancelledError:
                break
            except Exception as exc:
                logger.warning(
                    "Unexpected error in push loop for device=%s group=%s: %s",
                    device_code, group_code, exc,
                )

    async def _broadcast(self, key: tuple, msg: dict) -> None:
        dead: set = set()
        for ws in list(self._subscribers.get(key, set())):
            try:
                await ws.send_json(msg)
            except Exception:
                dead.add(ws)
        for ws in dead:
            await self.unsubscribe(ws, key[0], key[1])


_manager: WsPushManager | None = None


def get_ws_manager() -> WsPushManager:
    global _manager
    if _manager is None:
        _manager = WsPushManager()
    return _manager
