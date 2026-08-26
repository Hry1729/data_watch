from __future__ import annotations

import asyncio
import inspect
import json
import os
import traceback
import types
from contextlib import asynccontextmanager
from datetime import datetime
from typing import Any, Callable, get_args, get_origin, get_type_hints
from urllib.parse import parse_qsl, urlencode

from turboapi import CORSMiddleware, JSONResponse, TurboAPI
from turboapi.request_handler import DependencyResolver, HeaderParser, RequestBodyParser, ResponseHandler
from turboapi.responses import Response as TurboResponse
from turboapi.websockets import WebSocket as TurboWebSocket
from turboapi.websockets import WebSocketDisconnect

from app.api.router import api_router
from app.api.routes.monitor import register_ws_push_websocket
from app.api.routes.perspective import register_perspective_websocket
from app.core.config import get_settings
from app.db.mysql import close_mysql
from app.db.redis import close_redis
from app.db.tdengine import close_tdengine
from app.core.exceptions import register_exception_handlers
from app.core.logging import configure_logging, get_logger
from app.core.log_writer import write_system_log, write_system_log_sync
from app.core.log_scheduler import start_log_scheduler, stop_log_scheduler
from app.http_body import RequestBodyTooLarge, read_limited_body
from app.services.auth_bootstrap import bootstrap_auth_schema
from app.services.driver_lifecycle import (
    reconcile_driver_operations,
    start_driver_operation_cleanup,
    stop_driver_operation_cleanup,
)
from app.services.rate_sampler import start_rate_sampler, stop_rate_sampler
from app.services.perspective import get_perspective_session_manager


# 不安全的 JWT secret 集合：旧硬编码默认值、模板占位符、空串。
_UNSAFE_AUTH_SECRETS = {
    "device-monitor-dev-secret",
    "replace-with-a-strong-random-secret",
    "",
}


def _write_startup_error_file(message: str, stack: str, phase: str) -> None:
    """写入启动错误文件兜底日志（数据库可能连不上）"""
    try:
        logs_dir = os.path.join(os.path.dirname(__file__), "..", "logs")
        os.makedirs(logs_dir, exist_ok=True)
        log_file = os.path.join(logs_dir, "backend-startup-error.log")
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        entry = f"\n{'=' * 60}\n[{timestamp}] [{phase}]\nError: {message}\nStack:\n{stack}\n"
        with open(log_file, "a", encoding="utf-8") as f:
            f.write(entry)
    except Exception as e:
        # 本函数作用域没有模块级 logger；现取一个，避免兜底路径自身抛 NameError 吞掉原始错误。
        get_logger("backend.startup").exception("Failed to write startup error file: %s", e)


class CaseInsensitiveHeaders(dict[str, str]):
    def __init__(self, headers: dict[str, str]):
        super().__init__({str(key).lower(): str(value) for key, value in headers.items()})

    def __contains__(self, key: object) -> bool:
        return super().__contains__(str(key).lower())

    def __getitem__(self, key: str) -> str:
        return super().__getitem__(key.lower())

    def get(self, key: str, default: str | None = None) -> str | None:
        return super().get(key.lower(), default)


def _cors_response_headers(headers: CaseInsensitiveHeaders) -> dict[str, str]:
    origin = headers.get("origin")
    if not origin:
        return {}

    settings = get_settings()
    if origin not in settings.cors_origins:
        return {}

    return {
        "access-control-allow-origin": origin,
        "access-control-allow-credentials": "true",
        "vary": "Origin",
    }


async def _send_cors_preflight_response(send: Callable, headers: CaseInsensitiveHeaders) -> bool:
    request_method = headers.get("access-control-request-method")
    cors_headers = _cors_response_headers(headers)
    if not request_method or not cors_headers:
        return False

    allow_headers = headers.get("access-control-request-headers") or "*"
    cors_headers.update(
        {
            "access-control-allow-methods": "*",
            "access-control-allow-headers": allow_headers,
        }
    )
    await send(
        {
            "type": "http.response.start",
            "status": 204,
            "headers": [
                (str(key).encode("latin-1"), str(value).encode("latin-1"))
                for key, value in cors_headers.items()
            ],
        }
    )
    await send({"type": "http.response.body", "body": b""})
    return True


class CompatQueryParams:
    def __init__(self, pairs: list[tuple[str, str]]):
        self._values: dict[str, list[str]] = {}
        for raw_key, raw_value in pairs:
            key = str(raw_key)
            value = str(raw_value)
            self._values.setdefault(key, []).append(value)
            if key.endswith("[]"):
                self._values.setdefault(key[:-2], []).append(value)

    def get(self, key: str, default: Any = None) -> Any:
        values = self._values.get(key)
        if not values:
            return default
        return values[0]

    def getlist(self, key: str) -> list[str]:
        return list(self._values.get(key, []))

    def to_handler_dict(self) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, values in self._values.items():
            result[key] = values[0] if len(values) == 1 else list(values)
        return result


class CompatRequest:
    def __init__(
        self,
        *,
        method: str,
        path: str,
        query_string: str,
        headers: CaseInsensitiveHeaders,
        path_params: dict[str, str],
        query_params: CompatQueryParams,
        body: bytes,
    ):
        self.method = method
        self.path = path
        self.query_string = query_string
        self.headers = headers
        self.path_params = path_params
        self.query_params = query_params
        self.body = body
        self.client = None

    def json(self) -> Any:
        if not self.body:
            return None
        return json.loads(self.body.decode("utf-8"))

    def text(self) -> str:
        return self.body.decode("utf-8") if self.body else ""


class ASGICompatWebSocket(TurboWebSocket):
    def __init__(
        self,
        *,
        scope: dict[str, Any],
        receive: Callable,
        send: Callable,
        headers: CaseInsensitiveHeaders,
        path_params: dict[str, str],
        query_params: CompatQueryParams,
    ):
        super().__init__(scope=scope)
        self._asgi_receive = receive
        self._asgi_send = send
        self.headers = headers
        self.path_params = path_params
        self.query_params = query_params.to_handler_dict()

    async def accept(
        self,
        subprotocol: str | None = None,
        headers: dict[str, str] | None = None,
    ) -> None:
        if self._accepted:
            return
        raw_headers = [
            [str(key).encode("latin-1"), str(value).encode("latin-1")]
            for key, value in (headers or {}).items()
        ]
        await self._asgi_send(
            {
                "type": "websocket.accept",
                "subprotocol": subprotocol,
                "headers": raw_headers,
            }
        )
        self._accepted = True
        self.client_state = "connected"

    async def close(self, code: int = 1000, reason: str | None = None) -> None:
        if self._closed:
            return
        message: dict[str, Any] = {"type": "websocket.close", "code": int(code)}
        if reason is not None:
            message["reason"] = str(reason)
        await self._asgi_send(message)
        self._closed = True
        self.client_state = "disconnected"

    async def send_text(self, data: str) -> None:
        if not self._accepted or self._closed:
            raise RuntimeError("WebSocket is not connected")
        await self._asgi_send({"type": "websocket.send", "text": data})

    async def send_bytes(self, data: bytes) -> None:
        if not self._accepted or self._closed:
            raise RuntimeError("WebSocket is not connected")
        await self._asgi_send({"type": "websocket.send", "bytes": data})

    async def receive_text(self) -> str:
        if self._closed:
            raise WebSocketDisconnect()
        while True:
            message = await self._asgi_receive()
            message_type = message.get("type")
            if message_type == "websocket.receive":
                if message.get("text") is not None:
                    return str(message["text"])
                if message.get("bytes") is not None:
                    return bytes(message["bytes"]).decode("utf-8")
                return ""
            if message_type == "websocket.disconnect":
                self._closed = True
                self.client_state = "disconnected"
                raise WebSocketDisconnect(
                    code=int(message.get("code", 1000)),
                    reason=message.get("reason"),
                )

    async def receive_bytes(self) -> bytes:
        if self._closed:
            raise WebSocketDisconnect()
        while True:
            message = await self._asgi_receive()
            message_type = message.get("type")
            if message_type == "websocket.receive":
                if message.get("bytes") is not None:
                    return bytes(message["bytes"])
                if message.get("text") is not None:
                    return str(message["text"]).encode("utf-8")
                return b""
            if message_type == "websocket.disconnect":
                self._closed = True
                self.client_state = "disconnected"
                raise WebSocketDisconnect(
                    code=int(message.get("code", 1000)),
                    reason=message.get("reason"),
                )


class EnhancedASGITurboAPI(TurboAPI):
    async def __call__(self, scope: dict, receive: Callable, send: Callable) -> None:
        if scope["type"] == "lifespan":
            lifespan_cm = None
            lifespan_started = False
            if self._lifespan is not None:
                lifespan_cm = self._lifespan(self)
            while True:
                message = await receive()
                if message["type"] == "lifespan.startup":
                    try:
                        if lifespan_cm is not None:
                            await lifespan_cm.__aenter__()
                            lifespan_started = True
                        if self.startup_handlers:
                            await self._run_startup_handlers()
                    except BaseException as exc:
                        await send({"type": "lifespan.startup.failed", "message": str(exc)})
                        raise
                    await send({"type": "lifespan.startup.complete"})
                elif message["type"] == "lifespan.shutdown":
                    try:
                        if lifespan_cm is not None and lifespan_started:
                            await lifespan_cm.__aexit__(None, None, None)
                        if self.shutdown_handlers:
                            await self._run_shutdown_handlers()
                    except BaseException as exc:
                        await send({"type": "lifespan.shutdown.failed", "message": str(exc)})
                        raise
                    await send({"type": "lifespan.shutdown.complete"})
                    return

        if scope["type"] == "websocket":
            await self._dispatch_websocket(scope, receive, send)
            return

        if scope["type"] != "http":
            await super().__call__(scope, receive, send)
            return

        method = scope["method"]
        path = scope["path"]
        raw_query_string = scope.get("query_string", b"").decode("utf-8", errors="replace")
        query_pairs = parse_qsl(raw_query_string, keep_blank_values=True)
        normalized_query_pairs = [
            (key[:-2] if key.endswith("[]") else key, value) for key, value in query_pairs
        ]
        normalized_query_string = urlencode(normalized_query_pairs, doseq=True)

        headers = CaseInsensitiveHeaders(
            {
                hdr_name.decode("latin-1"): hdr_val.decode("latin-1")
                for hdr_name, hdr_val in scope.get("headers", [])
            }
        )
        max_body_bytes = get_settings().max_body_bytes
        raw_content_length = headers.get("content-length")
        if raw_content_length is not None:
            try:
                content_length = int(raw_content_length)
            except (TypeError, ValueError):
                await self._send_asgi_response(
                    send,
                    JSONResponse(status_code=400, content={"detail": "Invalid Content-Length"}),
                    headers,
                )
                return
            if content_length < 0:
                await self._send_asgi_response(
                    send,
                    JSONResponse(status_code=400, content={"detail": "Invalid Content-Length"}),
                    headers,
                )
                return
            if content_length > max_body_bytes:
                await self._send_asgi_response(
                    send,
                    JSONResponse(status_code=413, content={"detail": "Request body too large"}),
                    headers,
                )
                return

        try:
            body = await read_limited_body(receive, max_body_bytes)
        except RequestBodyTooLarge:
            await self._send_asgi_response(
                send,
                JSONResponse(status_code=413, content={"detail": "Request body too large"}),
                headers,
            )
            return

        if method == "OPTIONS" and await _send_cors_preflight_response(send, headers):
            return

        match_result = self.registry.match_route(method, path)
        if not match_result:
            await self._send_asgi_response(
                send,
                JSONResponse(status_code=404, content={"detail": "Not Found"}),
                headers,
            )
            return

        route, path_params = match_result
        request = CompatRequest(
            method=method,
            path=path,
            query_string=normalized_query_string,
            headers=headers,
            path_params=path_params,
            query_params=CompatQueryParams(query_pairs),
            body=body,
        )

        try:
            result = await self._dispatch_route(
                route=route,
                path_params=path_params,
                query_params=request.query_params,
                query_string=normalized_query_string,
                headers=headers,
                body=body,
                request=request,
            )
        except Exception as exc:  # pragma: no cover - exercised through HTTP smoke test
            result = await self._dispatch_exception(request, exc)

        await self._send_asgi_response(send, result, headers)

    async def _dispatch_websocket(self, scope: dict, receive: Callable, send: Callable) -> None:
        path = scope["path"]
        raw_query_string = scope.get("query_string", b"").decode("utf-8", errors="replace")
        query_pairs = parse_qsl(raw_query_string, keep_blank_values=True)
        headers = CaseInsensitiveHeaders(
            {
                hdr_name.decode("latin-1"): hdr_val.decode("latin-1")
                for hdr_name, hdr_val in scope.get("headers", [])
            }
        )
        handler = self._websocket_routes.get(path)
        if handler is None:
            await send({"type": "websocket.close", "code": 1008, "reason": "Not Found"})
            return

        websocket = ASGICompatWebSocket(
            scope=scope,
            receive=receive,
            send=send,
            headers=headers,
            path_params={},
            query_params=CompatQueryParams(query_pairs),
        )

        try:
            await self._invoke_websocket_handler(handler=handler, websocket=websocket)
        except WebSocketDisconnect:
            return
        except Exception:
            if not websocket._closed:
                await websocket.close(code=1011, reason="Internal Server Error")

    async def _invoke_websocket_handler(
        self,
        *,
        handler: Callable,
        websocket: ASGICompatWebSocket,
    ) -> None:
        sig = inspect.signature(handler)
        call_args: dict[str, Any] = {}

        if "websocket" in sig.parameters:
            call_args["websocket"] = websocket

        for param_name, param in sig.parameters.items():
            if param_name == "websocket":
                continue
            if param_name in websocket.path_params:
                call_args[param_name] = websocket.path_params[param_name]
                continue
            if param_name in websocket.query_params:
                call_args[param_name] = websocket.query_params[param_name]
                continue
            if param.default is inspect.Parameter.empty:
                raise TypeError(f"Missing required websocket parameter: {param_name}")

        if inspect.iscoroutinefunction(handler):
            await handler(**call_args)
            return
        handler(**call_args)

    async def _dispatch_route(
        self,
        *,
        route: Any,
        path_params: dict[str, str],
        query_params: CompatQueryParams,
        query_string: str,
        headers: CaseInsensitiveHeaders,
        body: bytes,
        request: CompatRequest,
    ) -> Any:
        sig = inspect.signature(route.handler)
        type_hints = get_type_hints(route.handler, globalns=getattr(route.handler, "__globals__", {}))
        call_args: dict[str, Any] = {}

        if "request" in sig.parameters:
            call_args["request"] = request

        for param_name, param_value in path_params.items():
            if param_name not in sig.parameters:
                continue
            annotation = type_hints.get(param_name)
            if annotation is None:
                param_def = next((item for item in route.path_params if item.name == param_name), None)
                candidate = getattr(param_def, "type", None) if param_def is not None else None
                annotation = candidate if callable(candidate) else None
            param_value = self._coerce_scalar_value(param_value, annotation)
            call_args[param_name] = param_value

        for param_name, value in query_params.to_handler_dict().items():
            if param_name not in call_args and param_name in sig.parameters:
                annotation = type_hints.get(param_name)
                call_args[param_name] = self._coerce_scalar_value(value, annotation)

        header_values = HeaderParser.parse_headers(dict(headers), sig)
        for param_name, value in header_values.items():
            if param_name not in call_args:
                call_args[param_name] = value

        if body:
            body_values = RequestBodyParser.parse_json_body(body, sig)
            call_args.update(body_values)
            raw_json_body = request.json()
            for param_name, param in sig.parameters.items():
                if param_name in call_args or param_name == "request":
                    continue
                annotation = type_hints.get(param_name, param.annotation)
                origin = get_origin(annotation)
                if annotation is dict or origin is dict or param_name == "payload":
                    call_args[param_name] = raw_json_body

        context = {
            "headers": dict(headers),
            "query_string": query_string,
            "body": body,
        }
        dependency_values = DependencyResolver.resolve_dependencies(sig, context)
        call_args.update(dependency_values)

        filtered_kwargs = {key: value for key, value in call_args.items() if key in sig.parameters}
        try:
            if inspect.iscoroutinefunction(route.handler):
                result = await route.handler(**filtered_kwargs)
            else:
                result = route.handler(**filtered_kwargs)
        finally:
            for cleanup in context.get("_cleanups", []):
                try:
                    next(cleanup)
                except StopIteration:
                    pass
                except Exception:
                    pass

        return result

    def _coerce_scalar_value(self, value: Any, annotation: Any) -> Any:
        if annotation is None or value is None:
            return value

        origin = get_origin(annotation)
        if origin in (types.UnionType,):
            args = [
                arg
                for arg in get_args(annotation)
                if arg is not type(None)  # noqa: E721 - sentinel comparison
            ]
            if len(args) == 1:
                return self._coerce_scalar_value(value, args[0])

        if annotation is str:
            return str(value)
        if annotation is int:
            return int(value)
        if annotation is float:
            return float(value)
        if annotation is bool:
            if isinstance(value, bool):
                return value
            normalized = str(value).strip().lower()
            if normalized in {"1", "true", "yes", "on"}:
                return True
            if normalized in {"0", "false", "no", "off"}:
                return False
            raise ValueError(f"Cannot coerce {value!r} to bool")
        return value

    async def _dispatch_exception(self, request: CompatRequest, exc: Exception) -> Any:
        for exc_type in type(exc).mro():
            handler = self._exception_handlers.get(exc_type)
            if handler is None:
                continue
            if inspect.iscoroutinefunction(handler):
                return await handler(request, exc)
            return handler(request, exc)

        if hasattr(exc, "status_code") and hasattr(exc, "detail"):
            return JSONResponse(status_code=int(exc.status_code), content={"detail": str(exc.detail)})
        # 兜底 500 不回传 str(exc)，避免泄露内部细节（详细异常由注册的异常处理器落日志）。
        return JSONResponse(status_code=500, content={"detail": "服务器内部错误"})

    async def _send_asgi_response(
        self,
        send: Callable,
        result: Any,
        request_headers: CaseInsensitiveHeaders | None = None,
    ) -> None:
        response_headers: dict[str, str] = {"server": "TurboAPI"}

        if isinstance(result, TurboResponse):
            status_code = result.status_code
            content_type = result.media_type or result.headers.get("content-type") or "application/json"
            response_headers.update(result.headers)
            response_headers.setdefault("content-type", content_type)
            body = result.body if isinstance(result.body, bytes) else str(result.body).encode("utf-8")
        else:
            normalized = ResponseHandler.normalize_response(result)
            if len(normalized) == 3:
                content, status_code, content_type = normalized
            else:
                content, status_code = normalized
                content_type = None
            response_headers["content-type"] = content_type or self._guess_content_type(content)
            body = self._encode_content(content)

        if request_headers is not None:
            response_headers.update(_cors_response_headers(request_headers))

        await send(
            {
                "type": "http.response.start",
                "status": int(status_code),
                "headers": [
                    [str(key).encode("latin-1"), str(value).encode("latin-1")]
                    for key, value in response_headers.items()
                ],
            }
        )
        await send({"type": "http.response.body", "body": body})

    def _guess_content_type(self, content: Any) -> str:
        if isinstance(content, bytes):
            return "application/octet-stream"
        if isinstance(content, str):
            return "text/plain; charset=utf-8"
        return "application/json"

    def _encode_content(self, content: Any) -> bytes:
        if isinstance(content, bytes):
            return content
        if isinstance(content, str):
            return content.encode("utf-8")
        return json.dumps(
            content,
            ensure_ascii=False,
            default=self._json_default,
        ).encode("utf-8")

    @staticmethod
    def _json_default(value: Any) -> Any:
        if isinstance(value, datetime):
            return value.isoformat()
        raise TypeError(f"Object of type {type(value).__name__} is not JSON serializable")


@asynccontextmanager
async def lifespan(_: TurboAPI):
    settings = get_settings()
    configure_logging(settings.log_level)
    logger = get_logger("backend.startup")
    try:
        logger.info("Starting backend skeleton for %s", settings.app_name)
        logger.info("MySQL target: %s:%s/%s", settings.mysql_host, settings.mysql_port, settings.mysql_database)
        logger.info(
            "TDengine target: %s:%s/%s",
            settings.tdengine_host,
            settings.tdengine_port,
            settings.tdengine_database,
        )
        perspective_manager = get_perspective_session_manager()
        logger.info(
            "Perspective target: enabled=%s ws=%s runtime_available=%s",
            settings.perspective_enabled,
            settings.perspective_ws_route,
            perspective_manager.runtime_available(),
        )
        startup_detail = {
            "mysql_host": settings.mysql_host,
            "mysql_port": settings.mysql_port,
            "mysql_database": settings.mysql_database,
            "tdengine_host": settings.tdengine_host,
            "tdengine_port": settings.tdengine_port,
            "tdengine_database": settings.tdengine_database,
            "perspective_enabled": settings.perspective_enabled,
            "auth_auto_bootstrap": settings.auth_auto_bootstrap,
        }

        # JWT secret 安全自检：漏配时会回落到不安全的默认/占位值，任何人可伪造 token。
        if settings.auth_secret_key in _UNSAFE_AUTH_SECRETS:
            if settings.app_env not in {"dev", "development", "test"}:
                raise RuntimeError(
                    "AUTH_SECRET_KEY must be a strong random value outside development/test environments"
                )
            logger.warning("AUTH_SECRET_KEY uses an insecure development/test default")

        if settings.auth_auto_bootstrap:
            await bootstrap_auth_schema(app_env=settings.app_env)
            logger.info("Auth schema bootstrap enabled and completed")

        # 补偿跨 Agent/MySQL 未完成的驱动生命周期（失败不阻断启动）
        try:
            reconcile_stats = await reconcile_driver_operations()
            logger.info("Driver operation reconcile done: %s", reconcile_stats)
        except Exception:
            logger.exception("Driver operation reconcile failed (non-blocking)")
        # 启动日志定时任务调度器
        await start_log_scheduler()
        logger.info("Log scheduler started")

        # 启动速率采样器（二期：采集/入库速率与趋势，内存滚动缓冲）
        await start_rate_sampler()
        logger.info("Rate sampler started")
        await write_system_log(
            level="INFO",
            log_type="system",
            source="backend.lifecycle",
            content=f"后端服务启动完成: {settings.app_name}",
            detail=startup_detail,
        )
    except Exception as exc:
        exception_stack = traceback.format_exc()
        logger.error("Backend startup failed: %s", exc, exc_info=True)

        # 尝试写入数据库日志
        try:
            await write_system_log(
                level="ERROR",
                log_type="system",
                source="backend.lifecycle",
                content=f"后端服务启动失败: {str(exc)[:450]}",
                exception_stack=exception_stack[:65535],
                detail={
                    "app_name": settings.app_name,
                    "exception_type": type(exc).__name__,
                    "phase": "lifespan.startup",
                },
            )
        except Exception:
            logger.exception("Failed to write startup failure system log")

        # 文件兜底日志（数据库可能连不上）
        _write_startup_error_file(str(exc), exception_stack, "lifespan.startup")

        raise

    await start_driver_operation_cleanup()
    logger.info("Driver operation cleanup scheduler started")
    try:
        yield
    finally:
        await stop_driver_operation_cleanup()
        logger.info("Driver operation cleanup scheduler stopped")

        # 关闭 Redis 连接
        await close_redis()
        logger.info("Redis connection closed")

        # 停止速率采样器
        await stop_rate_sampler()
        logger.info("Rate sampler stopped")

        # 停止日志定时任务调度器
        await stop_log_scheduler()
        logger.info("Log scheduler stopped")

        # 写入关闭日志（异步确保写入完成）
        await write_system_log(
            level="INFO",
            log_type="system",
            source="backend.lifecycle",
            content=f"后端服务关闭: {settings.app_name}",
        )

        # 释放 TDengine SQLAlchemy 连接池。
        await close_tdengine()
        logger.info("TDengine connection pool disposed")

        # 释放 MySQL 连接池（须在最后一条日志写入之后，避免日志写入时引擎已关闭）
        await close_mysql()
        logger.info("MySQL connection pool disposed")

        logger.info("Backend skeleton stopped")


def create_app() -> TurboAPI:
    settings = get_settings()
    app = EnhancedASGITurboAPI(
        title=settings.app_name,
        version=settings.app_version,
        lifespan=lifespan,
    )

    app.add_middleware(
        CORSMiddleware,
        allow_origins=settings.cors_origins,
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )
    register_exception_handlers(app)
    app.include_router(api_router)
    register_ws_push_websocket(app)
    register_perspective_websocket(app)
    return app
