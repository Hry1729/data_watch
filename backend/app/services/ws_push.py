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
        self._offline: dict[tuple, bool] = {}
        self._events: dict[tuple, asyncio.Event] = {}
        self._change_task: asyncio.Task | None = None

    async def subscribe(self, ws, device_code: str, group_code: str) -> None:
        key = (device_code, group_code)
        self._subscribers.setdefault(key, set()).add(ws)
        self._events.setdefault(key, asyncio.Event())
        if self._change_task is None or self._change_task.done():
            self._change_task = asyncio.create_task(self._change_loop())
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
            task = self._tasks.pop(key, None)
            if task:
                task.cancel()
            self._subscribers.pop(key, None)
            self._snapshots.pop(key, None)
            self._offline.pop(key, None)
            self._events.pop(key, None)
        if not self._subscribers and self._change_task is not None:
            self._change_task.cancel()
            self._change_task = None

    async def remove_all_subscriptions(self, ws) -> None:
        keys = list(self._subscribers.keys())
        for key in keys:
            if ws in self._subscribers.get(key, set()):
                await self.unsubscribe(ws, key[0], key[1])

    async def _read_latest(self, device_code: str, group_code: str) -> dict[str, Any] | None:
        """Read latest values; None means Redis unavailable, {} means no data."""
        from app.db.redis import get_redis_client

        try:
            client = await get_redis_client()
            raw = await client.hgetall(f"rt:latest:{device_code}:{group_code}")
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
                device_code,
                group_code,
                exc,
            )
            return None

    async def _change_loop(self) -> None:
        from app.db.redis import get_redis_client

        while self._subscribers:
            pubsub = None
            try:
                client = await get_redis_client()
                pubsub = client.pubsub()
                await pubsub.subscribe("rt:change")
                while self._subscribers:
                    message = await pubsub.get_message(
                        ignore_subscribe_messages=True,
                        timeout=1.0,
                    )
                    if message is not None:
                        self._notify_change(message.get("data"))
            except asyncio.CancelledError:
                break
            except Exception as exc:
                logger.warning("Redis rt:change subscription unavailable: %s", exc)
                await asyncio.sleep(1)
            finally:
                if pubsub is not None:
                    try:
                        await pubsub.aclose()
                    except Exception:
                        pass

    def _notify_change(self, raw: Any) -> None:
        try:
            if isinstance(raw, bytes):
                raw = raw.decode("utf-8")
            payload = json.loads(raw) if isinstance(raw, str) else raw
        except (TypeError, ValueError, json.JSONDecodeError):
            return

        changes = payload if isinstance(payload, list) else [payload]
        for change in changes:
            try:
                if not isinstance(change, dict):
                    continue
                key = (str(change["device_code"]), str(change["group_code"]))
            except (KeyError, TypeError, ValueError):
                continue
            event = self._events.get(key)
            if event is not None:
                event.set()

    async def _push_loop(self, device_code: str, group_code: str) -> None:
        key = (device_code, group_code)
        event = self._events.setdefault(key, asyncio.Event())
        while True:
            try:
                try:
                    await asyncio.wait_for(event.wait(), timeout=1.0)
                except asyncio.TimeoutError:
                    pass
                event.clear()
                latest = await self._read_latest(device_code, group_code)
                if latest is None or _latest_is_stale(latest):
                    if not self._offline.get(key, False):
                        await self._broadcast(
                            key,
                            {
                                "type": "offline",
                                "device_code": device_code,
                                "group_code": group_code,
                            },
                        )
                        self._offline[key] = True
                    self._snapshots[key] = {}
                    continue
                previous = self._snapshots.get(key, {})
                self._offline[key] = False
                diff = {
                    var_id: value
                    for var_id, value in latest.items()
                    if value != previous.get(var_id)
                }
                if diff:
                    self._snapshots[key] = {**previous, **diff}
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
                    device_code,
                    group_code,
                    exc,
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
