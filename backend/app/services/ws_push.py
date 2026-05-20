from __future__ import annotations

import asyncio
import json
from typing import Any


class WsPushManager:
    def __init__(self):
        self._subscribers: dict[tuple, set] = {}
        self._tasks: dict[tuple, asyncio.Task] = {}
        self._snapshots: dict[tuple, dict] = {}

    async def subscribe(self, ws, device_code: str, group_code: str) -> None:
        key = (device_code, group_code)
        self._subscribers.setdefault(key, set()).add(ws)
        if key not in self._tasks:
            self._tasks[key] = asyncio.create_task(
                self._push_loop(device_code, group_code)
            )
        snapshot = await self._read_latest(device_code, group_code)
        self._snapshots[key] = snapshot
        await ws.send_json(
            {
                "type": "snapshot",
                "device_code": device_code,
                "group_code": group_code,
                "values": snapshot,
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

    async def remove_all_subscriptions(self, ws) -> None:
        keys = list(self._subscribers.keys())
        for key in keys:
            if ws in self._subscribers.get(key, set()):
                await self.unsubscribe(ws, key[0], key[1])

    async def _read_latest(self, device_code: str, group_code: str) -> dict[str, Any]:
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
        except Exception:
            return {}

    async def _push_loop(self, device_code: str, group_code: str) -> None:
        key = (device_code, group_code)
        while True:
            await asyncio.sleep(0.1)
            try:
                latest = await self._read_latest(device_code, group_code)
                prev = self._snapshots.get(key, {})
                if not latest:
                    await self._broadcast(key, {"type": "offline", "device_code": device_code})
                    continue
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
            except Exception:
                pass

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
