from __future__ import annotations

import asyncio
import json
import logging
import time as _time
import weakref
from typing import Any

from app.core.status_thresholds import UNSTABLE_MAX_AGE_SECONDS

logger = logging.getLogger("backend.ws_push")

_td_health: dict = {"ok": None, "ts": 0.0}
_TD_HEALTH_TTL = 30.0


def _latest_timestamp(latest: dict[str, Any]) -> int | float | None:
    timestamps = [
        entry.get("t")
        for entry in latest.values()
        if isinstance(entry, dict) and isinstance(entry.get("t"), (int, float))
    ]
    return max(timestamps) if timestamps else None


def _latest_is_stale(latest: dict[str, Any]) -> bool:
    timestamp = _latest_timestamp(latest)
    return timestamp is None or _time.time() * 1000 - timestamp > UNSTABLE_MAX_AGE_SECONDS * 1000


def parse_subscription_point_ids(raw: Any) -> set[str] | None:
    """None keeps the legacy whole-group subscription; IDs are positive BIGINTs."""
    if raw is None:
        return None
    if not isinstance(raw, list) or len(raw) > 5000:
        raise ValueError("invalid point_ids")
    point_ids = set()
    for item in raw:
        if isinstance(item, bool) or not isinstance(item, (str, int)):
            raise ValueError("invalid point_ids")
        if isinstance(item, int) and (item < 1 or item > 9223372036854775807):
            raise ValueError("invalid point_ids")
        text = str(item)
        if len(text) > 19 or not text.isascii() or not text.isdecimal():
            raise ValueError("invalid point_ids")
        point_id = int(text)
        if point_id < 1 or point_id > 9223372036854775807:
            raise ValueError("invalid point_ids")
        point_ids.add(str(point_id))
    return point_ids


def _filter_points(values: dict[str, Any], point_ids: set[str] | None) -> dict[str, Any]:
    if point_ids is None:
        return values
    return {point_id: value for point_id, value in values.items() if point_id in point_ids}


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
            if "freshness" in existing or "freshness" in incoming:
                merged["freshness"] = {
                    **(existing.get("freshness") or {}),
                    **(incoming.get("freshness") or {}),
                }
            latest_ts = [ts for ts in (existing.get("latestTs"), incoming.get("latestTs"))
                         if isinstance(ts, (int, float))]
            if latest_ts:
                merged["latestTs"] = max(latest_ts)
            if merged.get("type") == "snapshot" and (incoming.get("values") or incoming.get("freshness")):
                merged["deviceOffline"] = False
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
        self._sending = None
        self._inflight_key = None
        self._task = asyncio.create_task(self._run(), name="ws-sender")

    def enqueue(self, key: tuple[str, str], msg: dict) -> None:
        if self._closed:
            return
        self._pending[key] = _coalesce_ws_message(self._pending.get(key), msg)
        self._wakeup.set()

    def drop_key(self, key: tuple[str, str]) -> None:
        self._pending.pop(key, None)
        if self._inflight_key == key and self._sending is not None:
            self._sending.cancel()

    def drop_all(self) -> None:
        self._pending.clear()

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
                    self._inflight_key = key
                    self._sending = asyncio.create_task(self.ws.send_json(msg))
                    try:
                        await asyncio.wait_for(self._sending, timeout=self._send_timeout)
                    except asyncio.CancelledError:
                        if self._closed:
                            raise
                        # A single subscription was removed; serve other keys.
                    except Exception:
                        await self._fail()
                        return
                    finally:
                        self._sending = None
                        self._inflight_key = None
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
    def __init__(
        self,
        send_timeout_seconds: float | None = None,
        min_push_interval_seconds: float | None = None,
        full_sync_interval_seconds: float | None = None,
    ):
        self._subscribers: dict[tuple, set] = {}
        self._point_filters: dict[tuple, dict[Any, set[str] | None]] = {}
        self._filter_versions: dict[tuple, int] = {}
        self._freshness_clients: dict[tuple, set[Any]] = {}
        self._subscriber_offline: dict[tuple, dict[Any, bool]] = {}
        self._tasks: dict[tuple, asyncio.Task] = {}
        self._snapshots: dict[tuple, dict] = {}
        self._offline: dict[tuple, bool] = {}
        self._events: dict[tuple, asyncio.Event] = {}
        self._change_task: asyncio.Task | None = None
        self._senders: dict[Any, _ConnectionSender] = {}
        self._revoked = weakref.WeakSet()
        if send_timeout_seconds is None:
            try:
                from app.core.config import get_settings

                send_timeout_seconds = get_settings().realtime_ws_send_timeout_seconds
            except Exception:
                send_timeout_seconds = 2.0
        self._send_timeout = max(0.05, float(send_timeout_seconds))
        if min_push_interval_seconds is None or full_sync_interval_seconds is None:
            try:
                from app.core.config import get_settings

                settings = get_settings()
                if min_push_interval_seconds is None:
                    min_push_interval_seconds = getattr(settings, "realtime_ws_min_push_interval_ms", 100) / 1000
                if full_sync_interval_seconds is None:
                    full_sync_interval_seconds = getattr(settings, "realtime_ws_full_sync_interval_seconds", 5)
            except Exception:
                min_push_interval_seconds = min_push_interval_seconds or 0.1
                full_sync_interval_seconds = full_sync_interval_seconds or 5.0
        self._min_push_interval = max(0.01, float(min_push_interval_seconds))
        self._full_sync_interval = max(self._min_push_interval, float(full_sync_interval_seconds))

    def _sender_for(self, ws) -> _ConnectionSender:
        sender = self._senders.get(ws)
        if sender is None:
            sender = _ConnectionSender(
                ws,
                send_timeout=self._send_timeout,
                on_dead=self._disconnect_slow,
            )
            self._senders[ws] = sender
        return sender

    def pending_count(self, ws) -> int:
        sender = self._senders.get(ws)
        return 0 if sender is None else len(sender._pending)

    def sender_task_count(self) -> int:
        return sum(1 for sender in self._senders.values() if not sender._task.done())

    async def subscribe(
        self, ws, device_code: str, group_code: str, point_ids: set[str] | None = None,
        supports_freshness: bool = False,
    ) -> None:
        key = (device_code, group_code)
        if ws in self._revoked:
            return
        latest = await self._read_latest(device_code, group_code, point_ids)
        td_ok = await _check_tdengine()
        if ws in self._revoked:
            return
        selected = _filter_points(latest or {}, point_ids)
        offline = latest is None or _latest_is_stale(selected)
        snapshot = {} if offline else selected
        sender = self._sender_for(ws)
        # Drop an old filter's queued/in-flight data before replacing the snapshot.
        sender.drop_key(key)
        self._point_filters.setdefault(key, {})[ws] = point_ids
        self._subscriber_offline.setdefault(key, {})[ws] = offline
        freshness_clients = self._freshness_clients.setdefault(key, set())
        if supports_freshness:
            freshness_clients.add(ws)
        else:
            freshness_clients.discard(ws)
        # Queue the initial snapshot before making this connection visible to
        # broadcasts. One sender owns all data sends, including initialization.
        sender.enqueue(key, {"type": "snapshot", "device_code": device_code,
            "group_code": group_code, "values": snapshot,
            "latestTs": _latest_timestamp(snapshot),
            "tdengineConnected": td_ok, "deviceOffline": offline})
        self._subscribers.setdefault(key, set()).add(ws)
        self._filter_versions[key] = self._filter_versions.get(key, 0) + 1
        self._events.setdefault(key, asyncio.Event())
        if key in self._tasks:
            for selected_id, value in (latest or {}).items():
                self._snapshots[key].setdefault(selected_id, value)
            self._events[key].set()
        if self._change_task is None or self._change_task.done():
            self._change_task = asyncio.create_task(self._change_loop())
        if key not in self._tasks:
            self._snapshots[key] = latest or {}
            self._offline[key] = latest is None or _latest_is_stale(latest)
            self._tasks[key] = asyncio.create_task(self._push_loop(device_code, group_code))

    async def unsubscribe(self, ws, device_code: str, group_code: str) -> None:
        key = (device_code, group_code)
        subs = self._subscribers.get(key, set())
        subs.discard(ws)
        self._point_filters.get(key, {}).pop(ws, None)
        self._filter_versions[key] = self._filter_versions.get(key, 0) + 1
        self._freshness_clients.get(key, set()).discard(ws)
        self._subscriber_offline.get(key, {}).pop(ws, None)
        sender = self._senders.get(ws)
        if sender is not None:
            sender.drop_key(key)
        if not subs:
            task = self._tasks.pop(key, None)
            if task:
                task.cancel()
            self._subscribers.pop(key, None)
            self._point_filters.pop(key, None)
            self._filter_versions.pop(key, None)
            self._freshness_clients.pop(key, None)
            self._subscriber_offline.pop(key, None)
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
        self._revoked.add(ws)
        sender = self._senders.get(ws)
        if sender is not None:
            await sender.close()
        await self.remove_all_subscriptions(ws)

    async def _disconnect_slow(self, ws):
        await self.revoke_connection(ws)
        try:
            await asyncio.wait_for(ws.close(), timeout=self._send_timeout)
        except (AttributeError, Exception):
            pass

    async def _close_sender(self, ws) -> None:
        sender = self._senders.pop(ws, None)
        if sender is not None:
            await sender.close()

    def _selected_point_ids(self, key: tuple[str, str]) -> set[str] | None:
        filters = self._point_filters.get(key, {})
        if any(point_ids is None for point_ids in filters.values()):
            return None
        return set().union(*(point_ids for point_ids in filters.values()))

    async def _read_latest(
        self, device_code: str, group_code: str, point_ids: set[str] | None = None,
    ) -> dict[str, Any] | None:
        """Read latest values; None means Redis unavailable, {} means no data."""
        from app.db.redis import get_redis_client

        try:
            client = await get_redis_client()
            key = f"rt:latest:{device_code}:{group_code}"
            if point_ids is None:
                raw = await client.hgetall(key)
            elif point_ids:
                ordered = sorted(point_ids)
                raw = dict(zip(ordered, await client.hmget(key, ordered)))
            else:
                raw = {}
            result: dict[str, Any] = {}
            for var_id, json_str in raw.items():
                if json_str is None:
                    continue
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
        last_read = 0.0
        while True:
            try:
                try:
                    await asyncio.wait_for(event.wait(), timeout=self._full_sync_interval)
                except asyncio.TimeoutError:
                    pass
                delay = last_read + self._min_push_interval - _time.monotonic()
                if delay > 0:
                    await asyncio.sleep(delay)
                event.clear()
                last_read = _time.monotonic()
                filter_version = self._filter_versions.get(key, 0)
                latest = await self._read_latest(device_code, group_code, self._selected_point_ids(key))
                if self._filter_versions.get(key) != filter_version:
                    event.set()
                    continue
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
                values = {
                    point_id: entry for point_id, entry in latest.items()
                    if not isinstance(entry, dict)
                    or not isinstance(previous.get(point_id), dict)
                    or (entry.get("v"), entry.get("q"))
                    != (previous[point_id].get("v"), previous[point_id].get("q"))
                }
                freshness = {
                    point_id: entry["t"] for point_id, entry in latest.items()
                    if isinstance(entry, dict) and isinstance(entry.get("t"), (int, float))
                    and entry.get("t") != (
                        previous[point_id].get("t")
                        if isinstance(previous.get(point_id), dict) else None
                    )
                }
                self._snapshots[key] = latest
                await self._broadcast(
                    key,
                    {
                        "type": "diff",
                        "device_code": device_code,
                        "group_code": group_code,
                        "values": values,
                        "freshness": freshness,
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
            outgoing = msg
            if msg.get("type") == "offline":
                self._subscriber_offline.setdefault(key, {})[ws] = True
            elif msg.get("type") == "diff":
                point_ids = self._point_filters.get(key, {}).get(ws)
                selected = _filter_points(self._snapshots.get(key, {}), point_ids)
                offline = _latest_is_stale(selected)
                was_offline = self._subscriber_offline.setdefault(key, {}).get(ws, False)
                if offline:
                    if was_offline:
                        continue
                    outgoing = {
                        "type": "offline", "device_code": key[0], "group_code": key[1]
                    }
                    self._subscriber_offline[key][ws] = True
                elif was_offline:
                    outgoing = {
                        "type": "snapshot", "device_code": key[0], "group_code": key[1],
                        "values": selected, "latestTs": _latest_timestamp(selected),
                        "deviceOffline": False,
                    }
                    self._subscriber_offline[key][ws] = False
                else:
                    values = _filter_points(msg.get("values") or {}, point_ids)
                    freshness = _filter_points(msg.get("freshness") or {}, point_ids)
                    if not values and not freshness:
                        continue
                    if ws not in self._freshness_clients.get(key, set()):
                        # Legacy clients need a full value entry when only t changes.
                        changed_ids = values.keys() | freshness.keys()
                        values = {
                            point_id: values.get(point_id, selected.get(point_id))
                            for point_id in changed_ids
                        }
                        outgoing = {**msg, "values": values}
                        outgoing.pop("freshness", None)
                    else:
                        outgoing = {
                            **msg,
                            "values": values,
                            "freshness": freshness,
                            "latestTs": max(freshness.values()) if freshness else _latest_timestamp(values),
                        }
            sender = self._senders.get(ws)
            if sender is None:
                sender = self._sender_for(ws)
            sender.enqueue(key, outgoing)

_manager: WsPushManager | None = None


def get_ws_manager() -> WsPushManager:
    global _manager
    if _manager is None:
        _manager = WsPushManager()
    return _manager
