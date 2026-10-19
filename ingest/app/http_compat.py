from __future__ import annotations

import inspect
import logging
from collections.abc import Callable
from functools import lru_cache
from typing import Any

import msgspec

from turboapi import JSONResponse, TurboAPI


logger = logging.getLogger(__name__)


class Headers(dict[str, str]):
    def get(self, key: str, default: str | None = None) -> str | None:
        return super().get(key.lower(), default)


class RequestBodyTooLarge(ValueError):
    pass


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
    def __init__(
        self,
        *args,
        on_startup=None,
        on_shutdown=None,
        max_body_bytes: int = 4 * 1024 * 1024,
        **kwargs,
    ) -> None:
        super().__init__(*args, **kwargs)
        self._on_startup = on_startup
        self._on_shutdown = on_shutdown
        if max_body_bytes <= 0:
            raise ValueError("max_body_bytes must be positive")
        self._max_body_bytes = max_body_bytes
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
        headers = Headers(
            {
                name.decode("latin-1").lower(): value.decode("latin-1")
                for name, value in scope.get("headers", [])
            }
        )
        raw_content_length = headers.get("content-length")
        content_length: int | None = None
        if raw_content_length is not None:
            try:
                content_length = int(raw_content_length)
            except (TypeError, ValueError):
                await self._send(
                    send,
                    JSONResponse(status_code=400, content={"detail": "Invalid Content-Length"}),
                )
                return
            if content_length < 0:
                await self._send(
                    send,
                    JSONResponse(status_code=400, content={"detail": "Invalid Content-Length"}),
                )
                return
            if content_length > self._max_body_bytes:
                await self._send(
                    send,
                    JSONResponse(status_code=413, content={"detail": "Request body too large"}),
                )
                return
        try:
            body = await self._read_body(
                receive,
                max_bytes=self._max_body_bytes,
                content_length=content_length,
            )
        except RequestBodyTooLarge:
            await self._send(
                send,
                JSONResponse(status_code=413, content={"detail": "Request body too large"}),
            )
            return
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
            logger.exception("Unhandled error handling %s %s", method, path)
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
    async def _read_body(
        receive: Callable,
        *,
        max_bytes: int,
        content_length: int | None = None,
    ) -> bytes:
        body_parts: list[bytes] = []
        body_size = 0
        while True:
            message = await receive()
            chunk = message.get("body", b"")
            body_size += len(chunk)
            if body_size > max_bytes:
                raise RequestBodyTooLarge("request body exceeds configured limit")
            body_parts.append(chunk)
            if not message.get("more_body", False):
                break
        if content_length is not None and body_size > content_length:
            raise RequestBodyTooLarge("request body exceeds Content-Length")
        return b"".join(body_parts)

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
