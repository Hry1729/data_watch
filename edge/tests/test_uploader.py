from __future__ import annotations

from pathlib import Path
from uuid import uuid4

import httpx
import pytest

from edge.app.outbox import OutboxStore
from edge.app.uploader import (
    Uploader,
    UploaderPaused,
    UploaderSettings,
    startup_phase_seconds,
)


class FakeSigner:
    def headers(self, method, path, payload):
        return {}


def settings(tmp_path: Path) -> UploaderSettings:
    return UploaderSettings(
        center_url="https://center.test",
        node_uuid=str(uuid4()),
        private_key_path=tmp_path / "unused.pem",
        lock_path=tmp_path / "uploader.lock",
        retry_base_ms=500,
    )


def seed(store: OutboxStore, count: int = 2) -> None:
    store.configure_assignment("QC100", 7, 100, 1)
    for seq in range(count):
        store.append_envelope(
            device_code="QC100",
            device_type="qc",
            group_code="g001",
            scan_rate_ms=100,
            ts=1721800000000 + seq,
            config_version=12,
            values={"1": str(seq)},
            qualities={"1": 192},
        )


def test_startup_phase_is_deterministic_and_bounded() -> None:
    first = startup_phase_seconds("node-a", 100)

    assert first == startup_phase_seconds("node-a", 100)
    assert 0 <= first < 0.1
    assert startup_phase_seconds("node-a", 400) < 0.4
    with pytest.raises(ValueError, match="positive"):
        startup_phase_seconds("node-a", 0)


@pytest.mark.asyncio
async def test_ack_batch_is_ordered_and_marked_sent(tmp_path: Path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    seed(store)
    seen_items = []

    def handler(request: httpx.Request) -> httpx.Response:
        payload = __import__("json").loads(request.content)
        seen_items.extend(payload["items"])
        return httpx.Response(
            200,
            json={
                "results": [
                    {
                        "batch_id": item["batch_id"],
                        "upload_session_id": item["upload_session_id"],
                        "local_seq": item["local_seq"],
                        "status": "ack",
                    }
                    for item in payload["items"]
                ]
            },
        )

    client = httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(handler),
    )
    uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())

    await uploader.run_cycle()

    assert [item["local_seq"] for item in seen_items] == [1, 2]
    assert all(item["assignment_epoch"] == 7 for item in seen_items)
    assert store.stats() == {"sent": 2}
    await client.aclose()
    store.close()


@pytest.mark.asyncio
async def test_server_error_retries_same_session(tmp_path: Path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 1)
    session_id = store.current_session_id()
    client = httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(lambda request: httpx.Response(503, text="down")),
    )
    uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())

    await uploader.run_cycle()

    assert store.current_session_id() == session_id
    assert store.stats() == {"retry_wait": 1}
    await client.aclose()
    store.close()


@pytest.mark.asyncio
async def test_seq_reject_abandons_session_and_cas_switches(tmp_path: Path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 2)
    old_session = store.current_session_id()
    switch_payloads = []

    def handler(request: httpx.Request) -> httpx.Response:
        payload = __import__("json").loads(request.content)
        if request.url.path == "/ingest/session_switch":
            switch_payloads.append(payload)
            return httpx.Response(200, json={"status": "switched"})
        item = payload["items"][0]
        return httpx.Response(
            200,
            json={
                "results": [
                    {
                        "batch_id": item["batch_id"],
                        "upload_session_id": item["upload_session_id"],
                        "local_seq": item["local_seq"],
                        "status": "seq_reject",
                    }
                ]
            },
        )

    client = httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(handler),
    )
    uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())

    await uploader.run_cycle()

    assert store.current_session_id() != old_session
    assert store.pending_session_switch() is None
    assert store.stats() == {}
    assert switch_payloads[0]["old_session_id"] == old_session
    await client.aclose()
    store.close()


@pytest.mark.asyncio
async def test_auth_rejection_pauses_without_changing_row_state(tmp_path: Path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 1)
    calls = [0]

    def handler(request: httpx.Request) -> httpx.Response:
        calls[0] += 1
        if calls[0] == 1:
            return httpx.Response(401)
        item = __import__("json").loads(request.content)["items"][0]
        return httpx.Response(
            200,
            json={
                "results": [
                    {
                        "batch_id": item["batch_id"],
                        "upload_session_id": item["upload_session_id"],
                        "local_seq": item["local_seq"],
                        "status": "ack",
                    }
                ]
            },
        )

    client = httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(handler),
    )
    uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())

    with pytest.raises(UploaderPaused):
        await uploader.run_cycle()
    assert store.stats() == {"inflight": 1}

    await uploader.run_cycle()
    assert store.stats() == {"sent": 1}
    await client.aclose()
    store.close()

@pytest.mark.asyncio
async def test_session_obsolete_with_current_active_session_retries_batch(tmp_path: Path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 1)
    current_session = store.current_session_id()

    def handler(request: httpx.Request) -> httpx.Response:
        if request.method == "GET":
            return httpx.Response(200, json={"active_session_id": current_session})
        item = __import__("json").loads(request.content)["items"][0]
        return httpx.Response(
            200,
            json={
                "results": [
                    {
                        "batch_id": item["batch_id"],
                        "upload_session_id": item["upload_session_id"],
                        "local_seq": item["local_seq"],
                        "status": "session_obsolete",
                    }
                ]
            },
        )

    client = httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(handler),
    )
    uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())

    await uploader.run_cycle()

    assert store.current_session_id() == current_session
    assert store.stats() == {"retry_wait": 1}
    await client.aclose()
    store.close()
