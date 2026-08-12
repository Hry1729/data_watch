from __future__ import annotations

import json
import time
from collections.abc import Iterator
from typing import Any, Callable
from urllib.parse import urlsplit, urlunsplit

from websockets.exceptions import ConnectionClosed, InvalidHandshake, InvalidURI
from websockets.sync.client import connect

from cli.errors import (
    CliError,
    EXIT_API,
    EXIT_AUTH_REQUIRED,
    EXIT_NETWORK,
    EXIT_PERMISSION_DENIED,
    EXIT_TIMEOUT,
)


class RealtimeWatcher:
    def __init__(
        self,
        base_url: str,
        token: str,
        *,
        timeout_seconds: float,
        connect_factory: Callable[..., Any] = connect,
        max_reconnects: int = 3,
        reconnect_backoff_seconds: float = 0.5,
        sleep: Callable[[float], None] = time.sleep,
    ) -> None:
        self.url = self._websocket_url(base_url)
        self.token = token
        self.timeout_seconds = timeout_seconds
        self.connect_factory = connect_factory
        self.max_reconnects = max(0, int(max_reconnects))
        self.reconnect_backoff_seconds = max(0.0, reconnect_backoff_seconds)
        self.sleep = sleep

    def events(
        self,
        device_code: str,
        group_code: str,
        *,
        point_id: int | None = None,
        max_events: int = 0,
        idle_timeout: float | None = None,
        duration_seconds: float | None = None,
    ) -> Iterator[dict[str, Any]]:
        subscribe = {
            "action": "subscribe",
            "device_code": device_code,
            "group_code": group_code,
        }
        emitted = 0
        reconnects = 0
        deadline = (
            time.monotonic() + duration_seconds
            if duration_seconds is not None and duration_seconds > 0
            else None
        )
        while max_events <= 0 or emitted < max_events:
            if deadline is not None and time.monotonic() >= deadline:
                return
            try:
                with self.connect_factory(
                    self.url,
                    open_timeout=self.timeout_seconds,
                    close_timeout=min(self.timeout_seconds, 5.0),
                    proxy=None,
                ) as websocket:
                    websocket.send(
                        json.dumps(
                            {"type": "auth", "token": self.token},
                            ensure_ascii=False,
                            separators=(",", ":"),
                        )
                    )
                    websocket.send(
                        json.dumps(subscribe, ensure_ascii=False, separators=(",", ":"))
                    )
                    try:
                        while max_events <= 0 or emitted < max_events:
                            remaining = (
                                max(0.0, deadline - time.monotonic())
                                if deadline is not None
                                else None
                            )
                            if remaining is not None and remaining <= 0:
                                return
                            receive_timeout = idle_timeout
                            if remaining is not None:
                                receive_timeout = (
                                    remaining
                                    if receive_timeout is None
                                    else min(receive_timeout, remaining)
                                )
                            try:
                                raw = websocket.recv(timeout=receive_timeout)
                            except TimeoutError:
                                if deadline is not None:
                                    return
                                raise
                            event = self._decode_event(raw)
                            if event is None:
                                continue
                            self._raise_server_error(event)
                            event = self._filter_point_event(event, point_id)
                            if event is None:
                                continue
                            emitted += 1
                            yield event
                    finally:
                        try:
                            websocket.send(
                                json.dumps(
                                    {**subscribe, "action": "unsubscribe"},
                                    ensure_ascii=False,
                                    separators=(",", ":"),
                                )
                            )
                        except Exception:
                            pass
                return
            except CliError:
                raise
            except TimeoutError as exc:
                raise CliError("timeout", "实时订阅等待超时", EXIT_TIMEOUT) from exc
            except (ConnectionClosed, InvalidHandshake, InvalidURI, OSError) as exc:
                if reconnects >= self.max_reconnects:
                    raise CliError(
                        "network_error",
                        "实时 WebSocket 重连失败",
                        EXIT_NETWORK,
                        details={"reconnectAttempts": reconnects},
                    ) from exc
                reconnects += 1
                self.sleep(self.reconnect_backoff_seconds * (2 ** (reconnects - 1)))
    @staticmethod
    def _filter_point_event(
        event: dict[str, Any],
        point_id: int | None,
    ) -> dict[str, Any] | None:
        if point_id is None:
            return event
        point_key = str(point_id)
        values = event.get("values")
        if isinstance(values, dict):
            if point_key not in values:
                if event.get("type") == "snapshot":
                    return {**event, "values": {}, "pointId": point_id}
                return None
            return {
                **event,
                "values": {point_key: values[point_key]},
                "pointId": point_id,
            }
        return {**event, "pointId": point_id}

    @staticmethod
    def _websocket_url(base_url: str) -> str:
        parsed = urlsplit(base_url.rstrip("/"))
        scheme = "wss" if parsed.scheme == "https" else "ws"
        path = parsed.path.rstrip("/") + "/api/device-monitor/ws/realtime-push"
        return urlunsplit((scheme, parsed.netloc, path, "", ""))

    @staticmethod
    def _decode_event(raw: str | bytes) -> dict[str, Any] | None:
        if isinstance(raw, bytes):
            raw = raw.decode("utf-8")
        if raw == "pong":
            return None
        try:
            payload = json.loads(raw)
        except (TypeError, ValueError) as exc:
            raise CliError("api_error", "WebSocket 返回了非法 JSON", EXIT_API) from exc
        if not isinstance(payload, dict):
            raise CliError("api_error", "WebSocket 事件格式错误", EXIT_API)
        return payload

    @staticmethod
    def _raise_server_error(event: dict[str, Any]) -> None:
        if event.get("type") != "error":
            return
        try:
            code = int(event.get("code") or 500)
        except (TypeError, ValueError):
            code = 500
        message = str(event.get("message") or "实时订阅失败")
        if code == 401:
            raise CliError("unauthorized", message, EXIT_AUTH_REQUIRED, status=401)
        if code == 403:
            raise CliError(
                "permission_denied", message, EXIT_PERMISSION_DENIED, status=403
            )
        raise CliError("api_error", message, EXIT_API, status=code)
