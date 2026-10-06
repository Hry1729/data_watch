from __future__ import annotations

import asyncio
import logging
import time
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
async def test_ack_batch_is_ordered_and_marked_sent(tmp_path: Path, caplog) -> None:
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
                        "status": "ack", "durability": "redis-aof",
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
    uploader._summary_started_at = time.monotonic() - 11
    caplog.set_level(logging.INFO)

    await uploader.run_cycle()

    assert [item["local_seq"] for item in seen_items] == [1, 2]
    assert "Upload summary" in caplog.text
    assert all(item["assignment_epoch"] == 7 for item in seen_items)
    assert store.stats() == {"sent": 2}
    await client.aclose()
    store.close()


@pytest.mark.asyncio
async def test_server_error_retries_same_session(tmp_path: Path, caplog) -> None:
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
    assert "Upload deferred status=503" in caplog.text
    await client.aclose()
    store.close()


@pytest.mark.asyncio
async def test_network_error_is_logged_and_retried(tmp_path: Path, caplog) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 1)

    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("connection refused", request=request)

    client = httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(handler),
    )
    uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())

    await uploader.run_cycle()

    assert store.stats() == {"retry_wait": 1}
    assert "Upload request failed" in caplog.text
    assert "connection refused" in caplog.text
    await client.aclose()
    store.close()


@pytest.mark.asyncio
async def test_seq_reject_switches_session_without_discarding_backlog(tmp_path: Path) -> None:
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
    assert store.stats() == {"pending": 2}
    with store._lock:
        rows = store._connection.execute(
            "SELECT local_seq, upload_session_id FROM outbox ORDER BY local_seq"
        ).fetchall()
    assert [row["local_seq"] for row in rows] == [1, 2]
    assert {row["upload_session_id"] for row in rows} == {store.current_session_id()}
    assert switch_payloads[0]["old_session_id"] == old_session
    await client.aclose()
    store.close()


@pytest.mark.asyncio
async def test_auth_rejection_pauses_without_changing_row_state(tmp_path: Path, caplog) -> None:
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
                        "status": "ack", "durability": "redis-aof",
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
    assert "Upload authentication rejected status=401" in caplog.text

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


@pytest.mark.asyncio
async def test_blocking_storage_call_does_not_block_event_loop(
    tmp_path: Path,
    monkeypatch,
) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    original_snapshot = store.snapshot_target_seq

    def slow_snapshot() -> int:
        time.sleep(0.2)
        return original_snapshot()

    monkeypatch.setattr(store, "snapshot_target_seq", slow_snapshot)
    client = httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(lambda request: httpx.Response(500)),
    )
    uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())

    cycle = asyncio.create_task(uploader.run_cycle())
    await asyncio.sleep(0.02)

    assert cycle.done() is False

    await cycle
    await client.aclose()
    store.close()


@pytest.mark.asyncio
async def test_memory_only_ack_retains_local_backlog(tmp_path):
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 1)
    def handler(request):
        item = __import__("json").loads(request.content)["items"][0]
        return httpx.Response(200, json={"results": [{
            "batch_id": item["batch_id"], "upload_session_id": item["upload_session_id"],
            "local_seq": item["local_seq"], "status": "ack"}]})
    async with httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(handler)) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()
        assert store.stats() == {"retry_wait": 1}
    store.close()


@pytest.mark.asyncio
async def test_session_network_failure_does_not_end_upload_loop(tmp_path):
    from unittest.mock import AsyncMock
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 1)
    async with httpx.AsyncClient(base_url="https://center.test") as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        uploader._startup_phase_applied = True
        uploader._maintain_if_due = AsyncMock()
        uploader.run_cycle = AsyncMock(side_effect=httpx.ConnectError("offline"))
        async def stop_after_retry(seconds):
            uploader._stopped.set()
        uploader._sleep = stop_after_retry
        await uploader.run_forever()
        uploader.run_cycle.assert_awaited_once()
        assert store.stats() == {"pending": 1}
    store.close()


@pytest.mark.asyncio
async def test_batch_contract_rejection_isolates_single_bad_record(tmp_path):
    import json
    from dataclasses import replace
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 3)
    calls = []
    def handler(request):
        items = json.loads(request.content)["items"]
        calls.append([item["local_seq"] for item in items])
        if any(item["local_seq"] == 2 for item in items):
            return httpx.Response(400, text="bad item")
        return httpx.Response(200, json={"results": [dict(batch_id=item["batch_id"], upload_session_id=item["upload_session_id"], local_seq=item["local_seq"], status="ack", durability="redis-aof") for item in items]})
    client = httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(handler))
    uploader = Uploader(store, replace(settings(tmp_path), retry_base_ms=0), client=client, signer=FakeSigner())
    try:
        await uploader.run_cycle()
        await uploader.run_cycle()
        assert calls == [[1, 2, 3], [1], [2]]
        rows = store._connection.execute("SELECT local_seq, state, reject_count FROM outbox ORDER BY local_seq").fetchall()
        assert [tuple(row) for row in rows] == [(1, "sent", 0), (2, "retry_wait", 1), (3, "retry_wait", 0)]
    finally:
        await client.aclose()
        store.close()


@pytest.mark.asyncio
@pytest.mark.parametrize("status", ["seq_reject", "commit_unknown"])
async def test_uncertain_head_never_quarantines_following_records(tmp_path, status):
    import json
    from unittest.mock import AsyncMock
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 3)
    cfg = settings(tmp_path)
    store._connection.execute("UPDATE outbox SET reject_count=?", (cfg.max_attempts,))
    def handler(request):
        items = json.loads(request.content)["items"]
        return httpx.Response(200, json={"results": [dict(batch_id=item["batch_id"], upload_session_id=item["upload_session_id"], local_seq=item["local_seq"], status=status) for item in items]})
    client = httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(handler))
    uploader = Uploader(store, cfg, client=client, signer=FakeSigner())
    uploader._abandon_and_switch = AsyncMock()
    try:
        await uploader.run_cycle()
        rows = store._connection.execute("SELECT local_seq, state, reject_count FROM outbox ORDER BY local_seq").fetchall()
        assert rows[0]["state"] == "dead"
        assert [row["state"] for row in rows[1:]] == ["retry_wait", "retry_wait"]
        assert all(row["reject_count"] == cfg.max_attempts for row in rows[1:])
    finally:
        await client.aclose()
        store.close()


@pytest.mark.asyncio
async def test_oversized_head_switches_session_before_good_frame(tmp_path):
    import json
    from dataclasses import replace
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 2)
    cfg = replace(settings(tmp_path), max_batch_bytes=1000, retry_base_ms=0)
    store._connection.execute("UPDATE outbox SET payload=? WHERE local_seq=1", (b"x" * 2000,))
    store._connection.execute("UPDATE outbox SET reject_count=? WHERE local_seq=2", (cfg.max_attempts - 1,))
    calls = []
    high_water = 0
    def handler(request):
        nonlocal high_water
        calls.append(request.url.path)
        if request.url.path == "/ingest/session_switch":
            high_water = 0
            return httpx.Response(200, json={"status": "switched"})
        items = json.loads(request.content)["items"]
        results = []
        for item in items:
            status = "ack" if item["local_seq"] == high_water + 1 else "seq_reject"
            if status == "ack":
                high_water += 1
            results.append(dict(batch_id=item["batch_id"], upload_session_id=item["upload_session_id"], local_seq=item["local_seq"], status=status, durability="redis-aof"))
        return httpx.Response(200, json={"results": results})
    async with httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(handler)) as client:
        uploader = Uploader(store, cfg, client=client, signer=FakeSigner())
        try:
            await uploader.run_cycle()
            assert calls == []
            await uploader.run_cycle()
            assert calls == ["/ingest/session_switch", "/ingest"]
            rows = store._connection.execute("SELECT state, reject_count FROM outbox ORDER BY created_at, rowid").fetchall()
            assert [row["state"] for row in rows] == ["dead", "sent"]
            assert rows[1]["reject_count"] == cfg.max_attempts - 1
        finally:
            store.close()
