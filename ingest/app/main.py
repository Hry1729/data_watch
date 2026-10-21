from __future__ import annotations

import asyncio
import logging
import os
import time
from dataclasses import dataclass
from typing import Any, Awaitable, Callable
from uuid import UUID

from turboapi import JSONResponse

from .config import IngestSettings, get_settings
from .http_compat import IngestASGI, Request
from .redis_store import (
    IngestRedisStore,
    NonceReplayError,
    RedisRequirementReport,
    TransientStoreError,
)
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
            # 部署前提（Redis >= 7.2 + AOF）：只告警、不阻断启动；/healthz 同时报告不健康。
            _log_redis_requirements(await store.check_persistence_requirements())
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

_REDIS_REQUIREMENT_RECHECK_S = 30.0


def _log_redis_requirements(report: RedisRequirementReport) -> None:
    if not report.verified:
        logger.warning(
            "Cannot verify Redis deployment requirements (Redis >= 7.2, appendonly yes): %s",
            report.detail,
        )
    for problem in report.problems:
        logger.error("Redis deployment requirement violated: %s", problem)


class _RedisRequirementCheck:
    """/healthz 用：缓存 Redis 部署前提检查结果，至多每 30s 重查一次。"""

    def __init__(self, runtime_provider: RuntimeProvider) -> None:
        self._runtime_provider = runtime_provider
        self._report: RedisRequirementReport | None = None
        self._checked_at = 0.0

    async def report(self) -> RedisRequirementReport | None:
        now = time.monotonic()
        if self._checked_at and now - self._checked_at < _REDIS_REQUIREMENT_RECHECK_S:
            return self._report
        try:
            runtime = await self._runtime_provider()
            report = await runtime.store.check_persistence_requirements()
        except Exception as exc:
            # 运行时/Redis 不可用：沿用上次结论，同样 30s 后再试，避免每次探活都去重连。
            logger.warning("Redis requirement check unavailable: %s", exc)
            self._checked_at = now
            return self._report
        if report.problems and (self._report is None or self._report.problems != report.problems):
            _log_redis_requirements(report)
        self._report, self._checked_at = report, now
        return report


def _health_response(
    settings: IngestSettings, report: RedisRequirementReport | None
) -> dict[str, Any] | JSONResponse:
    content: dict[str, Any] = {
        "service": "ingest",
        "status": "ready",
        "port": settings.port,
        "stream_shard_count": settings.stream_shard_count,
        "workers": settings.workers,
        "control_cache_ttl_ms": settings.control_cache_ttl_ms,
        "mysql_pool_maxsize": settings.mysql_pool_maxsize,
    }
    if report is None or not report.verified:
        content["redis_requirements"] = "unverified"
        return content
    if report.problems:
        # 违反部署前提：上传 ACK 无法达到 AOF 持久化等级（上传/会话切换按 503 推迟）。
        content["status"] = "unhealthy"
        content["redis_requirements"] = list(report.problems)
        return JSONResponse(status_code=503, content=content)
    content["redis_requirements"] = "ok"
    return content


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

    redis_requirements = _RedisRequirementCheck(runtime_provider)

    @app.get("/healthz")
    async def healthz():
        return _health_response(current_settings, await redis_requirements.report())

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
