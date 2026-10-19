from __future__ import annotations

from types import SimpleNamespace
from uuid import uuid4
from dataclasses import replace

import httpx
import pytest

from ingest.app.config import IngestSettings
from ingest.app.http_compat import IngestASGI
from ingest.app.main import create_app
from ingest.app.schemas import AckResult


class AllowAuthenticator:
    async def verify(self, **kwargs) -> None:
        return None

    async def reserve_nonce(self, node_uuid: str, nonce: str) -> None:
        return None

    async def authenticate(self, **kwargs) -> None:
        return None


class FakeService:
    async def ingest(self, batch, *, nonce: str):
        item = batch.items[0]
        return [
            AckResult(
                item.batch_id,
                item.upload_session_id,
                item.local_seq,
                "ack",
            )
        ]

    async def switch_session(self, request):
        return "switched"

    async def get_active_session(self, node_uuid):
        return None


async def runtime_provider():
    return SimpleNamespace(
        authenticator=AllowAuthenticator(),
        service=FakeService(),
    )


def settings() -> IngestSettings:
    return IngestSettings(
        host="127.0.0.1",
        port=8010,
        redis_url="redis://127.0.0.1:6379/0",
        mysql_host="127.0.0.1",
        mysql_port=3306,
        mysql_user="root",
        mysql_password="",
        mysql_database="device_watch",
        stream_shard_count=4,
        stream_maxlen=50000,
        signature_max_skew_seconds=300,
        nonce_ttl_seconds=600,
        max_batch_items=500,
    )


def upload_payload() -> dict:
    return {
        "node_uuid": str(uuid4()),
        "request_id": str(uuid4()),
        "items": [
            {
                "batch_id": str(uuid4()),
                "upload_session_id": str(uuid4()),
                "local_seq": 1,
                "assignment_epoch": 1,
                "device_code": "QC100",
                "device_type": "qc",
                "group_code": "g001",
                "scan_rate": 100,
                "ts": 1721800000000,
                "config_version": 1,
                "values": {"1": "12.5"},
            }
        ],
    }


@pytest.mark.asyncio
async def test_ingest_http_ack_shape() -> None:
    app = create_app(runtime_provider, settings())
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.post("/ingest", json=upload_payload())

    assert response.status_code == 200
    assert response.json()["results"][0]["status"] == "ack"


@pytest.mark.asyncio
async def test_ingest_http_bad_request_shape() -> None:
    app = create_app(runtime_provider, settings())
    transport = httpx.ASGITransport(app=app)
    payload = upload_payload()
    payload["items"][0]["local_seq"] = 0
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.post("/ingest", json=payload)

    assert response.status_code == 400
    assert response.json()["status"] == "bad_request"
    # 某条记录本身非法：scope=item（边缘按记录级隔离）。
    assert response.json()["scope"] == "item"


@pytest.mark.asyncio
async def test_ingest_http_batch_level_bad_request_is_scoped_batch() -> None:
    """Edge 6：与具体记录无关的 400 标注 scope=batch。"""
    app = create_app(runtime_provider, settings())
    transport = httpx.ASGITransport(app=app)
    payload = upload_payload()
    payload["node_uuid"] = "not-a-uuid"
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.post("/ingest", json=payload)

    assert response.status_code == 400
    assert response.json()["scope"] == "batch"


@pytest.mark.asyncio
async def test_ingest_http_transient_store_failure_is_503() -> None:
    """S1：Redis 暂时性持久化故障返回 503（边缘按传输失败退避重试）。"""
    from ingest.app.redis_store import TransientStoreError

    class TransientService(FakeService):
        async def ingest(self, batch, *, nonce: str):
            raise TransientStoreError("Redis AOF persistence not confirmed")

    async def transient_runtime():
        return SimpleNamespace(
            authenticator=AllowAuthenticator(),
            service=TransientService(),
        )

    app = create_app(transient_runtime, settings())
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.post("/ingest", json=upload_payload())

    assert response.status_code == 503
    assert response.json()["status"] == "store_unavailable"
    assert "persistence" in response.json()["reason"]


@pytest.mark.asyncio
async def test_unhandled_error_is_logged_and_returns_500(caplog) -> None:
    """S3：兜底 500 必须留下带方法与路径的异常日志。"""
    import logging

    class BrokenService(FakeService):
        async def get_active_session(self, node_uuid):
            raise RuntimeError("mysql connection lost")

    async def broken_runtime():
        return SimpleNamespace(
            authenticator=AllowAuthenticator(),
            service=BrokenService(),
        )

    app = create_app(broken_runtime, settings())
    transport = httpx.ASGITransport(app=app)
    node = str(uuid4())
    caplog.set_level(logging.ERROR, logger="ingest.app.http_compat")
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.get(f"/ingest/session/{node}")

    assert response.status_code == 500
    assert response.json() == {"detail": "Internal Server Error"}
    assert f"GET /ingest/session/{node}" in caplog.text
    assert "mysql connection lost" in caplog.text


@pytest.mark.asyncio
async def test_ingest_http_rejects_oversized_content_length_before_reading() -> None:
    limited = replace(settings(), max_body_bytes=64)
    app = create_app(runtime_provider, limited)
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.post(
            "/ingest",
            content=b"{}",
            headers={"content-type": "application/json", "content-length": "65"},
        )

    assert response.status_code == 413
    assert response.json()["detail"] == "Request body too large"


@pytest.mark.asyncio
async def test_lifespan_reports_startup_failure() -> None:
    async def fail_startup() -> None:
        raise RuntimeError("mapping gate failed")

    app = IngestASGI(
        title="test",
        version="1",
        on_startup=fail_startup,
    )
    messages = [{"type": "lifespan.startup"}]
    sent: list[dict] = []

    async def receive() -> dict:
        return messages.pop(0)

    async def send(message: dict) -> None:
        sent.append(message)

    await app._lifespan_loop(receive, send)

    assert sent == [
        {
            "type": "lifespan.startup.failed",
            "message": "mapping gate failed",
        }
    ]
