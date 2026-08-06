from __future__ import annotations

import inspect
from collections.abc import Callable
from functools import lru_cache
from typing import Any

import msgspec

from turboapi import JSONResponse, TurboAPI


class Headers(dict[str, str]):
    def get(self, key: str, default: str | None = None) -> str | None:
        return super().get(key.lower(), default)


class Request:
    def __init__(
        self,
        *,
        method: str,
        path: str,
        headers: Headers,
        body: bytes,
        path_params: dict[str, str],
    ) -> None:
        self.method = method
        self.path = path
        self.headers = headers
        self.body = body
        self.path_params = path_params

    def json(self) -> Any:
        if not self.body:
            return None
        return msgspec.json.decode(self.body)


class IngestASGI(TurboAPI):
    def __init__(self, *args, on_startup=None, on_shutdown=None, **kwargs) -> None:
        super().__init__(*args, **kwargs)
        self._on_startup = on_startup
        self._on_shutdown = on_shutdown
        self._startup_complete = False

    async def __call__(self, scope: dict, receive: Callable, send: Callable) -> None:
        if scope["type"] == "lifespan":
            await self._lifespan_loop(receive, send)
            return
        if scope["type"] != "http":
            await super().__call__(scope, receive, send)
            return

        method = str(scope["method"])
        path = str(scope["path"])
        body = await self._read_body(receive)
        headers = Headers(
            {
                name.decode("latin-1").lower(): value.decode("latin-1")
                for name, value in scope.get("headers", [])
            }
        )
        match = self.registry.match_route(method, path)
        if not match:
            await self._send(send, JSONResponse(status_code=404, content={"detail": "Not Found"}))
            return

        route, path_params = match
        request = Request(
            method=method,
            path=path,
            headers=headers,
            body=body,
            path_params=path_params,
        )
        try:
            result = await self._invoke(route.handler, request, path_params)
        except Exception:
            result = JSONResponse(
                status_code=500,
                content={"detail": "Internal Server Error"},
            )
        await self._send(send, result)

    async def _lifespan_loop(self, receive: Callable, send: Callable) -> None:
        while True:
            message = await receive()
            if message["type"] == "lifespan.startup":
                try:
                    if self._on_startup is not None:
                        await self._on_startup()
                    self._startup_complete = True
                    await send({"type": "lifespan.startup.complete"})
                except Exception as exc:
                    await send(
                        {
                            "type": "lifespan.startup.failed",
                            "message": str(exc),
                        }
                    )
                    return
            elif message["type"] == "lifespan.shutdown":
                try:
                    if self._startup_complete and self._on_shutdown is not None:
                        await self._on_shutdown()
                    await send({"type": "lifespan.shutdown.complete"})
                except Exception as exc:
                    await send(
                        {
                            "type": "lifespan.shutdown.failed",
                            "message": str(exc),
                        }
                    )
                return

    @staticmethod
    async def _read_body(receive: Callable) -> bytes:
        body = b""
        while True:
            message = await receive()
            body += message.get("body", b"")
            if not message.get("more_body", False):
                return body

    @staticmethod
    async def _invoke(
        handler: Callable,
        request: Request,
        path_params: dict[str, str],
    ) -> Any:
        parameters, is_async = _handler_metadata(handler)
        kwargs: dict[str, Any] = {}
        if "request" in parameters:
            kwargs["request"] = request
        if "payload" in parameters:
            payload = request.json()
            if not isinstance(payload, dict):
                raise ValueError("JSON body must be an object")
            kwargs["payload"] = payload
        for name, value in path_params.items():
            if name in parameters:
                kwargs[name] = value

        if is_async:
            return await handler(**kwargs)
        return handler(**kwargs)

    @staticmethod
    async def _send(send: Callable, result: Any) -> None:
        if hasattr(result, "status_code") and hasattr(result, "body"):
            status_code = int(result.status_code)
            body = result.body
            if not isinstance(body, bytes):
                body = str(body).encode("utf-8")
            raw_headers = getattr(result, "raw_headers", None)
            if raw_headers is None:
                headers = getattr(result, "headers", {}) or {}
                raw_headers = [
                    (str(key).encode("latin-1"), str(value).encode("latin-1"))
                    for key, value in headers.items()
                ]
        else:
            status_code = 200
            body = msgspec.json.encode(result)
            raw_headers = [(b"content-type", b"application/json")]

        await send(
            {
                "type": "http.response.start",
                "status": status_code,
                "headers": raw_headers,
            }
        )
        await send({"type": "http.response.body", "body": body})

@lru_cache(maxsize=256)
def _handler_metadata(handler: Callable) -> tuple[frozenset[str], bool]:
    return (
        frozenset(inspect.signature(handler).parameters),
        inspect.iscoroutinefunction(handler),
    )
