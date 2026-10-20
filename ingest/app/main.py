from __future__ import annotations

import asyncio
import logging
import os
from dataclasses import dataclass
from typing import Any, Awaitable, Callable
from uuid import UUID

from turboapi import JSONResponse

from .config import IngestSettings, get_settings
from .http_compat import IngestASGI, Request
from .redis_store import IngestRedisStore, NonceReplayError, TransientStoreError
from .repository import MySQLIngestRepository
from .schemas import RequestValidationError, SessionSwitch, UploadBatch
from .security import (
    AuthenticationError,
    NodeSignatureAuthenticator,
    SignatureHeaders,
)
from .service import IngestService


logger = logging.getLogger(__name__)


@dataclass(slots=True)
class IngestRuntime:
    repository: MySQLIngestRepository
    store: IngestRedisStore
    authenticator: NodeSignatureAuthenticator
    service: IngestService
    control_task: asyncio.Task[None] | None = None

    async def close(self) -> None:
        if self.control_task is not None:
            self.control_task.cancel()
            try:
                await self.control_task
            except asyncio.CancelledError:
                pass
        await self.store.close()
        await self.repository.close()


_runtime: IngestRuntime | None = None
_runtime_lock = asyncio.Lock()


async def get_runtime() -> IngestRuntime:
    global _runtime
    if _runtime is not None:
        return _runtime
    async with _runtime_lock:
        if _runtime is None:
            settings = get_settings()
            repository = await MySQLIngestRepository.create(
                settings,
                migrate_schema=os.getenv("DATAWATCH_INGEST_SCHEMA_READY") != "1",
            )
            store = IngestRedisStore.create(settings)
            runtime = IngestRuntime(
                repository=repository,
                store=store,
                authenticator=NodeSignatureAuthenticator(
                    repository,
                    store,
                    max_skew_seconds=settings.signature_max_skew_seconds,
                    nonce_ttl_seconds=settings.nonce_ttl_seconds,
                ),
                service=IngestService(
                    repository,
                    store,
                    max_batch_items=settings.max_batch_items,
                ),
            )
            runtime.control_task = asyncio.create_task(
                store.listen_control_invalidations(
                    repository.invalidate_control_cache
                )
            )
            _runtime = runtime
    return _runtime


async def close_runtime() -> None:
    global _runtime
    async with _runtime_lock:
        runtime = _runtime
        _runtime = None
    if runtime is not None:
        await runtime.close()


RuntimeProvider = Callable[[], Awaitable[IngestRuntime]]


def create_app(
    runtime_provider: RuntimeProvider = get_runtime,
    settings: IngestSettings | None = None,
) -> IngestASGI:
    current_settings = settings or get_settings()
    shutdown = close_runtime if runtime_provider is get_runtime else None
    app = IngestASGI(
        title="Data Watch Central Ingest",
        version="1.0.0",
        on_startup=runtime_provider,
        on_shutdown=shutdown,
        max_body_bytes=current_settings.max_body_bytes,
    )

    @app.get("/healthz")
    async def healthz():
        return {
            "service": "ingest",
            "status": "ready",
            "port": current_settings.port,
            "stream_shard_count": current_settings.stream_shard_count,
            "workers": current_settings.workers,
            "control_cache_ttl_ms": current_settings.control_cache_ttl_ms,
            "mysql_pool_maxsize": current_settings.mysql_pool_maxsize,
        }

    @app.post("/ingest")
    async def ingest_upload(payload: dict[str, Any], request: Request):
        try:
            node_uuid = _node_uuid(payload)
            runtime = await runtime_provider()
            headers = _signature_headers(request)
            await runtime.authenticator.verify(
                node_uuid=node_uuid,
                method="POST",
                path="/ingest",
                payload=payload,
                headers=headers,
            )
            try:
                batch = UploadBatch.from_dict(payload)
                results = await runtime.service.ingest(batch, nonce=headers.nonce)
            except (RequestValidationError, ValueError):
                await runtime.authenticator.reserve_nonce(node_uuid, headers.nonce)
                raise
            return {"results": [result.to_dict() for result in results]}
        except (AuthenticationError, NonceReplayError) as exc:
            return _error(401, "unauthorized", str(exc))
        except TransientStoreError as exc:
            logger.error("Ingest deferred: transient Redis persistence failure: %s", exc)
            return _error(503, "store_unavailable", str(exc))
        except (RequestValidationError, ValueError) as exc:
            return _error(
                400,
                "bad_request",
                str(exc),
                scope=getattr(exc, "scope", "batch"),
            )

    @app.post("/ingest/session_switch")
    async def session_switch(payload: dict[str, Any], request: Request):
        try:
            node_uuid = _node_uuid(payload)
            runtime = await runtime_provider()
            await _authenticate(
                runtime,
                node_uuid,
                "POST",
                "/ingest/session_switch",
                payload,
                request,
            )
            switch = SessionSwitch.from_dict(payload)
            status = await runtime.service.switch_session(switch)
            return {"status": status}
        except AuthenticationError as exc:
            return _error(401, "unauthorized", str(exc))
        except TransientStoreError as exc:
            # B003：切换未达到持久化等级前不返回 200，边缘保留待完成切换并重试（Lua 幂等）。
            logger.error("Session switch deferred: transient Redis persistence failure: %s", exc)
            return _error(503, "store_unavailable", str(exc))
        except (RequestValidationError, ValueError) as exc:
            return _error(400, "bad_request", str(exc))

    @app.get("/ingest/session/{node_uuid}")
    async def active_session(node_uuid: str, request: Request):
        try:
            normalized = str(UUID(node_uuid))
            runtime = await runtime_provider()
            await _authenticate(
                runtime,
                normalized,
                "GET",
                f"/ingest/session/{normalized}",
                {},
                request,
            )
            value = await runtime.service.get_active_session(normalized)
            return {"node_uuid": normalized, "active_session_id": value}
        except AuthenticationError as exc:
            return _error(401, "unauthorized", str(exc))
        except ValueError as exc:
            return _error(400, "bad_request", str(exc))

    return app


async def _authenticate(
    runtime: IngestRuntime,
    node_uuid: str,
    method: str,
    path: str,
    payload: dict[str, Any],
    request: Request,
) -> None:
    await runtime.authenticator.authenticate(
        node_uuid=node_uuid,
        method=method,
        path=path,
        payload=payload,
        headers=_signature_headers(request),
    )


def _signature_headers(request: Request) -> SignatureHeaders:
    headers = request.headers
    return SignatureHeaders(
        timestamp=headers.get("x-datawatch-timestamp", ""),
        nonce=headers.get("x-datawatch-nonce", ""),
        signature=headers.get("x-datawatch-signature", ""),
    )


def _node_uuid(payload: dict[str, Any]) -> str:
    return str(UUID(str(payload.get("node_uuid") or "")))


def _error(
    http_status: int, status: str, reason: str, *, scope: str | None = None
) -> JSONResponse:
    content = {"status": status, "reason": reason}
    if scope is not None:
        content["scope"] = scope
    return JSONResponse(status_code=http_status, content=content)


app = create_app()
