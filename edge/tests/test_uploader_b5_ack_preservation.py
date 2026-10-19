"""B5：同批已 ACK 的帧不得因后续 session 异常被再次迁移重传。

覆盖验收要求（清单 215 行 / 任务卡 §6）：
1. 混合 ACK + seq_reject：已确定 ACK 的项先持久为 ``sent`` 且保留**旧会话身份**，
   不得进入新会话的迁移集合。
2. ACK 应答丢失（尾部缺 ACK）：ACK 前缀照样先落库。
3. 尾部 durability 未确认（中心未确认 AOF）：ACK 前缀照样先落库。
4. 切换中崩溃（``/ingest/session_switch`` 传输失败）：ACK 前缀已落库，未确认载荷保留，
   下一个循环补完切换而不丢数据。
5. 确认后本地崩溃（``complete_batch`` 抛异常）：不得出现 ``sent``，
   重启后仍可重放，并按 duplicate 语义对账（至少一次）。
6. 数据守恒：任何路径下 ``sum(stats().values()) == 写入条数``。
"""

from __future__ import annotations

import json
import time
from pathlib import Path
from uuid import uuid4

import httpx
import pytest

from edge.app.outbox import OutboxStore
from edge.app.uploader import Uploader, UploaderSettings


class FakeSigner:
    def headers(self, method, path, payload):
        return {}


def settings(tmp_path: Path) -> UploaderSettings:
    return UploaderSettings(
        center_url="https://center.test",
        node_uuid=str(uuid4()),
        private_key_path=tmp_path / "unused.pem",
        lock_path=tmp_path / "uploader.lock",
        retry_base_ms=1,
    )


def seed(store: OutboxStore, count: int) -> list[str]:
    store.configure_assignment("QC100", 7, 100, 1)
    batch_ids = []
    for seq in range(count):
        record = store.append_envelope(
            device_code="QC100",
            device_type="qc",
            group_code="g001",
            scan_rate_ms=100,
            ts=1721800000000 + seq,
            config_version=12,
            values={"1": str(seq)},
            qualities={"1": 192},
        )
        batch_ids.append(record.batch_id)
    return batch_ids


def rows_by_batch(store: OutboxStore) -> dict[str, dict]:
    with store._lock:
        fetched = store._connection.execute(
            "SELECT batch_id, upload_session_id, local_seq, state, attempt_count "
            "FROM outbox ORDER BY batch_id"
        ).fetchall()
    return {str(row["batch_id"]): dict(row) for row in fetched}


def ack(item: dict, status: str = "ack", **extra) -> dict:
    return {
        "batch_id": item["batch_id"],
        "upload_session_id": item["upload_session_id"],
        "local_seq": item["local_seq"],
        "status": status,
        **extra,
    }


def client_for(handler) -> httpx.AsyncClient:
    return httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(handler),
    )


@pytest.mark.asyncio
async def test_mixed_ack_and_seq_reject_commits_ack_before_switching(
    tmp_path: Path,
) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    batch_ids = seed(store, 2)
    old_session = store.current_session_id()

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/ingest/session_switch":
            return httpx.Response(200, json={"status": "switched"})
        items = json.loads(request.content)["items"]
        return httpx.Response(
            200,
            json={
                "results": [
                    ack(item, "ack", durability="redis-aof")
                    if item["local_seq"] == 1
                    else ack(item, "seq_reject")
                    for item in items
                ]
            },
        )

    async with client_for(handler) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()

    confirmed, unconfirmed = batch_ids[0], batch_ids[1]
    rows = rows_by_batch(store)
    # 1) 已 ACK 的项先持久提交，且保留原身份（旧会话 + 原 local_seq）。
    assert rows[confirmed]["state"] == "sent"
    assert rows[confirmed]["upload_session_id"] == old_session
    assert rows[confirmed]["local_seq"] == 1
    # 2) 未确认项被迁到新会话并重排为 1，仍在本地（退避跨切换保留，Edge 2/15）。
    assert rows[unconfirmed]["state"] == "retry_wait"
    assert rows[unconfirmed]["upload_session_id"] == store.current_session_id()
    assert rows[unconfirmed]["upload_session_id"] != old_session
    assert rows[unconfirmed]["local_seq"] == 1
    # 3) 数据守恒 + 会话切换已完成。
    assert sum(store.stats().values()) == 2
    assert store.stats() == {"sent": 1, "retry_wait": 1}
    assert store.pending_session_switch() is None
    assert store.operational_status()["sent_records_total"] == 1
    store.close()


@pytest.mark.asyncio
async def test_ack_prefix_is_persisted_when_a_later_ack_is_missing(
    tmp_path: Path,
) -> None:
    """ACK 应答丢失：已收到的 ACK 必须落库，未收到的不许视为已提交。"""
    store = OutboxStore(tmp_path / "edge.db")
    batch_ids = seed(store, 2)
    old_session = store.current_session_id()

    def handler(request: httpx.Request) -> httpx.Response:
        items = json.loads(request.content)["items"]
        # 中心只回了第一项的 ACK；第二项在响应中缺失（ACK 应答丢失）。
        return httpx.Response(
            200,
            json={"results": [ack(items[0], "ack", durability="redis-aof")]},
        )

    async with client_for(handler) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()

    rows = rows_by_batch(store)
    assert rows[batch_ids[0]]["state"] == "sent"
    assert rows[batch_ids[0]]["upload_session_id"] == old_session
    assert rows[batch_ids[0]]["local_seq"] == 1
    assert rows[batch_ids[1]]["state"] == "retry_wait"
    assert rows[batch_ids[1]]["upload_session_id"] == old_session
    assert sum(store.stats().values()) == 2
    assert store.pending_session_switch() is None
    store.close()


@pytest.mark.asyncio
async def test_ack_prefix_is_persisted_when_tail_durability_is_not_confirmed(
    tmp_path: Path,
) -> None:
    """尾部未确认 AOF：不得把已确认耐久的前缀一起退回。"""
    store = OutboxStore(tmp_path / "edge.db")
    batch_ids = seed(store, 2)
    old_session = store.current_session_id()

    def handler(request: httpx.Request) -> httpx.Response:
        items = json.loads(request.content)["items"]
        return httpx.Response(
            200,
            json={
                "results": [
                    ack(items[0], "ack", durability="redis-aof"),
                    ack(items[1], "ack"),  # 中心未确认 AOF 耐久
                ]
            },
        )

    async with client_for(handler) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()

    rows = rows_by_batch(store)
    assert rows[batch_ids[0]]["state"] == "sent"
    assert rows[batch_ids[0]]["upload_session_id"] == old_session
    assert rows[batch_ids[1]]["state"] == "retry_wait"
    assert rows[batch_ids[1]]["upload_session_id"] == old_session
    assert sum(store.stats().values()) == 2
    store.close()


@pytest.mark.asyncio
async def test_ack_prefix_survives_a_crash_during_the_session_switch(
    tmp_path: Path,
) -> None:
    """切换中崩溃：ACK 前缀已落库，未确认载荷保留，下一循环补完切换。"""
    store = OutboxStore(tmp_path / "edge.db")
    batch_ids = seed(store, 2)
    old_session = store.current_session_id()
    state = {"switch_attempts": 0}

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/ingest/session_switch":
            state["switch_attempts"] += 1
            if state["switch_attempts"] == 1:
                raise httpx.ConnectError("switch transport failed", request=request)
            return httpx.Response(200, json={"status": "switched"})
        items = json.loads(request.content)["items"]
        return httpx.Response(
            200,
            json={
                "results": [
                    ack(item, "ack", durability="redis-aof")
                    if item["local_seq"] == 1
                    else ack(item, "seq_reject")
                    for item in items
                ]
            },
        )

    async with client_for(handler) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()

        rows = rows_by_batch(store)
        assert rows[batch_ids[0]]["state"] == "sent"  # 切换失败前已落库
        assert rows[batch_ids[0]]["upload_session_id"] == old_session
        pending = store.pending_session_switch()
        assert pending is not None and pending.old_session_id == old_session
        assert sum(store.stats().values()) == 2

        # 下一循环：先补完切换，再上传剩余未确认项。
        await uploader.run_cycle()

    assert store.pending_session_switch() is None
    assert store.stats() == {"sent": 2}
    assert sum(store.stats().values()) == 2
    store.close()


@pytest.mark.asyncio
async def test_crash_before_terminal_persistence_keeps_records_replayable(
    tmp_path: Path, monkeypatch
) -> None:
    """确认后本地崩溃：不得出现 sent；重启后可重放并按 duplicate 对账。"""
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 2)
    original_complete = store.complete_batch
    calls = {"count": 0}

    def crash_once(completions, lease_owner):
        calls["count"] += 1
        if calls["count"] == 1:
            raise RuntimeError("simulated local crash before terminal persistence")
        return original_complete(completions, lease_owner)

    monkeypatch.setattr(store, "complete_batch", crash_once)
    state = {"replay": False}

    def handler(request: httpx.Request) -> httpx.Response:
        items = json.loads(request.content)["items"]
        status = "duplicate" if state["replay"] else "ack"
        return httpx.Response(
            200,
            json={
                "results": [ack(item, status, durability="redis-aof") for item in items]
            },
        )

    async with client_for(handler) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        with pytest.raises(RuntimeError, match="simulated local crash"):
            await uploader.run_cycle()

        # 崩溃后：没有任何记录被当成已确认，载荷仍在本地的 inflight 租约里。
        assert store.stats() == {"inflight": 2}

        # 模拟重启：恢复租约 → 重放 → 中心按 duplicate 幂等对账。
        state["replay"] = True
        assert store.recover_inflight("restart-owner") == 2
        assert store.stats() == {"pending": 2}
        await uploader.run_cycle()

    assert store.stats() == {"sent": 2}
    assert sum(store.stats().values()) == 2
    store.close()


@pytest.mark.asyncio
async def test_ack_prefix_is_persisted_when_a_status_is_unknown(
    tmp_path: Path,
) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    batch_ids = seed(store, 2)
    old_session = store.current_session_id()

    def handler(request: httpx.Request) -> httpx.Response:
        items = json.loads(request.content)["items"]
        return httpx.Response(
            200,
            json={
                "results": [
                    ack(items[0], "ack", durability="redis-aof"),
                    ack(items[1], "weird_new_status"),
                ]
            },
        )

    async with client_for(handler) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()

    rows = rows_by_batch(store)
    assert rows[batch_ids[0]]["state"] == "sent"
    assert rows[batch_ids[0]]["upload_session_id"] == old_session
    assert rows[batch_ids[1]]["state"] == "retry_wait"
    assert sum(store.stats().values()) == 2
    store.close()
