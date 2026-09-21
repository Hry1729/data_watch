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


def _coalesce_ws_message(existing: dict | None, incoming: dict) -> dict:
    """同一订阅至多保留一帧：snapshot/offline 覆盖，diff 合并点位。"""
    incoming_copy = {
        **incoming,
        "values": dict(incoming["values"]) if isinstance(incoming.get("values"), dict) else incoming.get("values"),
    }
    if existing is None:
        return incoming_copy
    incoming_type = incoming.get("type")
    if incoming_type in {"snapshot", "offline"}:
        return incoming_copy
    if incoming_type == "diff":
        if existing.get("type") == "offline":
            return incoming_copy
        if existing.get("type") in {"diff", "snapshot"}:
            merged = {
                **existing,
                "values": dict(existing.get("values") or {}),
            }
            merged["values"].update(incoming.get("values") or {})
            return merged
    return incoming_copy


class _ConnectionSender:
    """每连接一个发送协程：待发队列按订阅键有界，慢客户端超时断开。"""

    def __init__(self, ws, *, send_timeout: float, on_dead) -> None:
        self.ws = ws
        self._send_timeout = send_timeout
        self._on_dead = on_dead
        self._pending: dict[tuple[str, str], dict] = {}
        self._wakeup = asyncio.Event()
        self._closed = False
        self._task = asyncio.create_task(self._run(), name="ws-sender")

    def enqueue(self, key: tuple[str, str], msg: dict) -> None:
        if self._closed:
            return
        self._pending[key] = _coalesce_ws_message(self._pending.get(key), msg)
        self._wakeup.set()

    def drop_key(self, key: tuple[str, str]) -> None:
        self._pending.pop(key, None)

    def drop_all(self) -> None:
        self._pending.clear()

    async def send_now(self, msg: dict) -> None:
        if self._closed:
            return
        try:
            await asyncio.wait_for(self.ws.send_json(msg), timeout=self._send_timeout)
        except asyncio.CancelledError:
            raise
        except Exception:
            await self._fail()

    async def close(self) -> None:
        self._closed = True
        self._pending.clear()
        self._wakeup.set()
        task = self._task
        if task is not asyncio.current_task() and not task.done():
            task.cancel()
            try:
                await task
            except (asyncio.CancelledError, Exception):
                pass

    async def _run(self) -> None:
        try:
            while not self._closed:
                await self._wakeup.wait()
                self._wakeup.clear()
                while self._pending and not self._closed:
                    key, msg = next(iter(self._pending.items()))
                    del self._pending[key]
                    try:
                        await asyncio.wait_for(
                            self.ws.send_json(msg),
                            timeout=self._send_timeout,
                        )
                    except asyncio.CancelledError:
                        raise
                    except Exception:
                        await self._fail()
                        return
        except asyncio.CancelledError:
            return

    async def _fail(self) -> None:
        if self._closed:
            return
        self._closed = True
        self._pending.clear()
        callback = self._on_dead
        self._on_dead = None
        if callback is not None:
            await callback(self.ws)


class WsPushManager:
    def __init__(self, send_timeout_seconds: float | None = None):
        self._subscribers: dict[tuple, set] = {}
        self._tasks: dict[tuple, asyncio.Task] = {}
        self._snapshots: dict[tuple, dict] = {}
        self._offline: dict[tuple, bool] = {}
        self._events: dict[tuple, asyncio.Event] = {}
        self._change_task: asyncio.Task | None = None
        self._senders: dict[Any, _ConnectionSender] = {}
        if send_timeout_seconds is None:
            try:
                from app.core.config import get_settings

                send_timeout_seconds = get_settings().realtime_ws_send_timeout_seconds
            except Exception:
                send_timeout_seconds = 2.0
        self._send_timeout = max(0.05, float(send_timeout_seconds))

    def _sender_for(self, ws) -> _ConnectionSender:
        sender = self._senders.get(ws)
        if sender is None:
            sender = _ConnectionSender(
                ws,
                send_timeout=self._send_timeout,
                on_dead=self.remove_all_subscriptions,
            )
            self._senders[ws] = sender
        return sender

    def pending_count(self, ws) -> int:
        sender = self._senders.get(ws)
        return 0 if sender is None else len(sender._pending)

    def sender_task_count(self) -> int:
        return sum(1 for sender in self._senders.values() if not sender._task.done())

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
        sender = self._sender_for(ws)
        await sender.send_now(
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
        sender = self._senders.get(ws)
        if sender is not None:
            sender.drop_key(key)
        if not subs:
            task = self._tasks.pop(key, None)
            if task:
                task.cancel()
            self._subscribers.pop(key, None)
            self._snapshots.pop(key, None)
            self._offline.pop(key, None)
            self._events.pop(key, None)
        still_subscribed = any(ws in members for members in self._subscribers.values())
        if not still_subscribed:
            await self._close_sender(ws)
        if not self._subscribers and self._change_task is not None:
            self._change_task.cancel()
            self._change_task = None

    async def remove_all_subscriptions(self, ws) -> None:
        keys = [key for key, members in list(self._subscribers.items()) if ws in members]
        for key in keys:
            await self.unsubscribe(ws, key[0], key[1])
        await self._close_sender(ws)

    async def revoke_connection(self, ws) -> None:
        """撤权/失效：先清空待发队列，再退订，避免继续推送受保护数据。"""
        sender = self._senders.get(ws)
        if sender is not None:
            sender.drop_all()
        await self.remove_all_subscriptions(ws)

    async def _close_sender(self, ws) -> None:
        sender = self._senders.pop(ws, None)
        if sender is not None:
            await sender.close()

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
        for ws in list(self._subscribers.get(key, set())):
            sender = self._senders.get(ws)
            if sender is None:
                sender = self._sender_for(ws)
            sender.enqueue(key, msg)

_manager: WsPushManager | None = None


def get_ws_manager() -> WsPushManager:
    global _manager
    if _manager is None:
        _manager = WsPushManager()
    return _manager
