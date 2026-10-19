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
    # 退避跨 session 切换保留（Edge 2/15），不会在下一周期立即重放。
    assert store.stats() == {"retry_wait": 2}
    with store._lock:
        assert all(
            row["next_retry_at"] is not None
            for row in store._connection.execute("SELECT next_retry_at FROM outbox")
        )
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
            return httpx.Response(400, json={"status": "bad_request", "reason": "bad item", "scope": "item"})
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
@pytest.mark.parametrize("status", ["seq_reject"])
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


def _ack_all(items, status="ack"):
    return {"results": [dict(batch_id=item["batch_id"], upload_session_id=item["upload_session_id"], local_seq=item["local_seq"], status=status, durability="redis-aof") for item in items]}


@pytest.mark.asyncio
async def test_commit_unknown_retries_same_session_with_backoff_without_reject_budget(tmp_path):
    """Edge 2/15：commit_unknown 不切换 session、不清退避、不消耗 reject_count。"""
    import json
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 2)
    cfg = settings(tmp_path)
    # 预算只差一次就耗尽：旧实现会把首条判 dead。
    store._connection.execute("UPDATE outbox SET reject_count=?", (cfg.max_attempts - 1,))
    session_id = store.current_session_id()
    paths = []
    def handler(request):
        paths.append(request.url.path)
        items = json.loads(request.content)["items"]
        return httpx.Response(200, json={"results": [dict(batch_id=items[0]["batch_id"], upload_session_id=items[0]["upload_session_id"], local_seq=items[0]["local_seq"], status="commit_unknown", reason="Redis AOF persistence not confirmed")]})
    async with httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(handler)) as client:
        uploader = Uploader(store, cfg, client=client, signer=FakeSigner())
        await uploader.run_cycle()
        # 退避生效：紧接着的周期不会再发请求。
        await uploader.run_cycle()
    assert paths == ["/ingest"]
    assert store.current_session_id() == session_id
    assert store.pending_session_switch() is None
    rows = store._connection.execute("SELECT local_seq, upload_session_id, state, reject_count, attempt_count, last_error, next_retry_at FROM outbox ORDER BY local_seq").fetchall()
    assert [row["local_seq"] for row in rows] == [1, 2]
    assert {row["upload_session_id"] for row in rows} == {session_id}
    assert [row["state"] for row in rows] == ["retry_wait", "retry_wait"]
    assert [row["reject_count"] for row in rows] == [cfg.max_attempts - 1] * 2
    assert [row["attempt_count"] for row in rows] == [1, 1]
    assert {row["last_error"] for row in rows} == {"commit_unknown"}
    assert all(row["next_retry_at"] is not None for row in rows)
    store.close()


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "response",
    [
        lambda: httpx.Response(400, json={"status": "bad_request", "reason": "node_uuid must be a UUID", "scope": "batch"}),
        lambda: httpx.Response(400, text="<html>400 Bad Request</html>"),
    ],
    ids=["center-batch-scope", "proxy-without-scope"],
)
async def test_batch_level_400_never_consumes_record_reject_budget(tmp_path, response):
    """Edge 6：批级 400 与记录无关，不消耗 reject_count，单条也不会被判 dead。"""
    from dataclasses import replace
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 1)
    cfg = replace(settings(tmp_path), max_attempts=2, retry_base_ms=0)
    async with httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(lambda request: response())) as client:
        uploader = Uploader(store, cfg, client=client, signer=FakeSigner())
        for _ in range(4):
            await uploader.run_cycle()
    row = store._connection.execute("SELECT state, reject_count, attempt_count, last_error, http_status FROM outbox").fetchone()
    assert row["state"] == "retry_wait"
    assert row["reject_count"] == 0
    assert row["attempt_count"] == 4
    assert row["last_error"] == "http_400_batch_rejected"
    assert row["http_status"] == 400
    store.close()


@pytest.mark.asyncio
async def test_batch_level_400_halves_batch_until_center_accepts(tmp_path):
    """中心调低 max_batch_items 时（批级 400），批次减半直到被接受，不判 dead。"""
    import json
    from dataclasses import replace
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 4)
    calls = []
    def handler(request):
        items = json.loads(request.content)["items"]
        calls.append(len(items))
        if len(items) > 1:
            return httpx.Response(400, json={"status": "bad_request", "reason": "batch exceeds max_batch_items", "scope": "batch"})
        return httpx.Response(200, json=_ack_all(items))
    async with httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(handler)) as client:
        uploader = Uploader(store, replace(settings(tmp_path), retry_base_ms=0), client=client, signer=FakeSigner())
        for _ in range(3):
            await uploader.run_cycle()
    assert calls[:3] == [4, 2, 1]
    assert store.stats() == {"sent": 4}
    assert store._connection.execute("SELECT MAX(reject_count) FROM outbox").fetchone()[0] == 0
    store.close()


@pytest.mark.asyncio
async def test_413_halves_batch_and_logs_single_oversized_record(tmp_path, caplog):
    """Edge 18：413 时批次减半；单条仍 413 时打出明确错误，记录保留重试、不判 dead。"""
    import json
    from dataclasses import replace
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 4)
    calls = []
    def handler(request):
        items = json.loads(request.content)["items"]
        calls.append([item["local_seq"] for item in items])
        if len(items) > 1 or items[0]["local_seq"] == 3:
            return httpx.Response(413, json={"detail": "Request body too large"})
        return httpx.Response(200, json=_ack_all(items))
    caplog.set_level(logging.WARNING)
    async with httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(handler)) as client:
        uploader = Uploader(store, replace(settings(tmp_path), retry_base_ms=0, max_attempts=2), client=client, signer=FakeSigner())
        for _ in range(5):
            await uploader.run_cycle()
    assert calls[:4] == [[1, 2, 3, 4], [1, 2], [1], [2]]
    assert [3] in calls
    rows = store._connection.execute("SELECT local_seq, state, reject_count, http_status FROM outbox ORDER BY local_seq").fetchall()
    assert [(row["local_seq"], row["state"]) for row in rows] == [(1, "sent"), (2, "sent"), (3, "retry_wait"), (4, "retry_wait")]
    assert rows[2]["reject_count"] == 0
    assert rows[2]["http_status"] == 413
    assert "413 for a single record" in caplog.text
    store.close()

