"""B5 独立验证（W1-E1b）：已 ACK 帧不得因后续 session 异常被再次迁移重传。

本文件**不是**修复者 W1-E1 的用例（那份在
``edge/tests/test_uploader_b5_ack_preservation.py``），而是接手 agent 按
``W1-验收协议.md`` §0 自行构造的独立反证，刻意与前者在以下方面不同：

1. 混合响应按真实中心语义构造：命中不确定项后**响应就截止**（``ingest/app/service.py``
   的 ``break`` 行为），因此不确定项**之后**的帧在响应里完全缺席，而不是也被回应。
2. 「ACK 应答丢失」用一个**模拟中心高水位**的 fake 中心（按
   ``ingest/app/redis_store.py:68-74`` 的 ``duplicate``/``seq_reject`` 语义），
   断言重放是靠**幂等对账**收敛，而不是靠丢帧。
3. 「确认后本地崩溃」在**混合批次**里注入（不是纯 ACK 批次），注入点也不同
   （代理对象在首次 ``complete_batch`` 直接抛 ``SystemExit``）。
4. 「切换中崩溃」用 ``SystemExit`` 硬中断 session_switch，重启用一个**全新的
   Uploader 实例**（不同 lease owner），模拟真实进程重启。
5. 每条路径都同时断言**数据守恒**（``sum(stats) == 写入条数``）与**帧身份**
   （``batch_id`` + ``payload`` 的 sha256 不变），并显式排除「清空 outbox /
   跳过高水位之前数据 / 把未知当已提交」这类假修复。

环境：系统 python + temp SQLite + ``httpx.MockTransport``；不连现场栈，不读
``C:\\ProgramData\\DataWatch Edge``。
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
from uuid import uuid4

import httpx
import pytest

from edge.app.outbox import OutboxStore
from edge.app.uploader import Uploader, UploaderSettings


class FakeSigner:
    def headers(self, method, path, payload):
        return {}


def settings(tmp_path: Path, **overrides) -> UploaderSettings:
    base = dict(
        center_url="https://center.test",
        node_uuid=str(uuid4()),
        private_key_path=tmp_path / "unused.pem",
        lock_path=tmp_path / "uploader.lock",
        retry_base_ms=1,
    )
    base.update(overrides)
    return UploaderSettings(**base)


def seed(store: OutboxStore, count: int) -> dict[str, str]:
    """写入 count 条并按 batch_id 记录 payload sha256（帧身份基线）。"""
    store.configure_assignment("QC100", 7, 100, 1)
    fingerprints: dict[str, str] = {}
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
        fingerprints[record.batch_id] = hashlib.sha256(record.payload).hexdigest()
    return fingerprints


def snapshot(store: OutboxStore) -> dict[str, dict]:
    with store._lock:
        fetched = store._connection.execute(
            "SELECT batch_id, upload_session_id, local_seq, state, attempt_count, "
            "payload, next_retry_at FROM outbox ORDER BY batch_id"
        ).fetchall()
    return {str(row["batch_id"]): dict(row) for row in fetched}


def payload_hashes(store: OutboxStore) -> dict[str, str]:
    rows = snapshot(store)
    return {
        batch_id: hashlib.sha256(bytes(row["payload"])).hexdigest()
        for batch_id, row in rows.items()
    }


def assert_conserved(store: OutboxStore, fingerprints: dict[str, str]) -> None:
    """数据守恒 + 帧身份：条数、batch_id 集合、payload 字节都不许变。"""
    rows = snapshot(store)
    assert set(rows) == set(fingerprints), "帧集合（batch_id）发生变化 = 有帧丢失或被改写"
    assert sum(store.stats().values()) == len(fingerprints)
    assert payload_hashes(store) == fingerprints, "payload 字节被改写 = 帧身份未保全"


def ack_for(item: dict, status: str, **extra) -> dict:
    """按真实 ``ingest/app/schemas.py:142-153`` 的 ``AckResult.to_dict()`` 造结果。

    真实中心对 ``ack``/``duplicate``/``stale_config_warning`` 三种状态都会带上
    ``durability="redis-aof"``；漏掉它会被 Uploader 正确判为「中心未确认耐久」。
    """
    result = {
        "batch_id": item["batch_id"],
        "upload_session_id": item["upload_session_id"],
        "local_seq": item["local_seq"],
        "status": status,
    }
    if status in {"ack", "duplicate", "stale_config_warning"}:
        result["durability"] = "redis-aof"
    result.update(extra)
    return result


def client_for(handler) -> httpx.AsyncClient:
    return httpx.AsyncClient(
        base_url="https://center.test", transport=httpx.MockTransport(handler)
    )


def envelope_handler(results_for_items, *, session_switch=None):
    """批量上传按 items 生成 results；session_switch 单独分派（真实请求体形状）。"""

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/ingest/session_switch":
            body = json.loads(request.content)
            assert set(body) == {"node_uuid", "old_session_id", "new_session_id"}, (
                f"真实 session_switch 请求体形状被意外修改: {sorted(body)}"
            )
            if session_switch is not None:
                return session_switch(request, body)
            return httpx.Response(200, json={"status": "switched"})
        items = json.loads(request.content)["items"]
        return httpx.Response(200, json={"results": results_for_items(items)})

    return handler


class HighWaterCenter:
    """按 ``ingest/app/redis_store.py:68-74`` 的会话高水位语义模拟中心。

    ``local_seq <= hw`` → ``duplicate``；``local_seq == hw + 1`` → ``ack`` 并推进 hw；
    ``local_seq > hw + 1`` → ``seq_reject`` 并**截止**（后续项无结果）。
    """

    def __init__(self, *, truncate_first_response_to: int | None = None) -> None:
        self.hw: dict[str, int] = {}
        self.truncate_first_response_to = truncate_first_response_to
        self.statuses: list[str] = []

    def results(self, items: list[dict]) -> list[dict]:
        results: list[dict] = []
        for item in items:
            session = str(item["upload_session_id"])
            seq = int(item["local_seq"])
            hw = self.hw.get(session, 0)
            if seq <= hw:
                results.append(ack_for(item, "duplicate"))
                continue
            if seq > hw + 1:
                results.append(ack_for(item, "seq_reject"))
                break
            self.hw[session] = seq
            results.append(ack_for(item, "ack"))
        self.statuses.extend(str(result["status"]) for result in results)
        if self.truncate_first_response_to is not None:
            # 中心已提交（hw 已推进），但**这一次**响应把后面的 result 丢了
            # （ACK 应答丢失是一次性故障，后续重放必须能正常对账）。
            limit = self.truncate_first_response_to
            self.truncate_first_response_to = None
            return results[:limit]
        return results


@pytest.mark.asyncio
async def test_mixed_ack_then_commit_unknown_keeps_ack_identity(tmp_path: Path) -> None:
    """4 条：ack, ack, commit_unknown, （响应截止，第 4 条无结果）。"""
    store = OutboxStore(tmp_path / "edge.db")
    fingerprints = seed(store, 4)
    order = list(fingerprints)
    old_session = store.current_session_id()
    old_seqs = {batch_id: snapshot(store)[batch_id]["local_seq"] for batch_id in order}

    def results(items: list[dict]) -> list[dict]:
        # 真实中心：命中 commit_unknown 即 break，后面的帧没有 result。
        return [
            ack_for(item, "ack", durability="redis-aof")
            for item in items[:2]
        ] + [ack_for(items[2], "commit_unknown")]

    async with client_for(envelope_handler(results)) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()

        rows = snapshot(store)
        # 1) 已明确 ACK 的前缀：sent，且**保留旧会话身份与原 local_seq**（未被迁移）。
        for batch_id in order[:2]:
            assert rows[batch_id]["state"] == "sent"
            assert rows[batch_id]["upload_session_id"] == old_session
            assert rows[batch_id]["local_seq"] == old_seqs[batch_id]
        # 2) 不确定项与其后的缺席帧：未确认，被迁到新会话并重排（不在 sent 集合里）。
        assert store.current_session_id() != old_session
        for batch_id in order[2:]:
            assert rows[batch_id]["state"] == "pending"
            assert rows[batch_id]["upload_session_id"] == store.current_session_id()
        assert sorted(rows[batch_id]["local_seq"] for batch_id in order[2:]) == [1, 2]
        # 3) 数据守恒 + 帧身份 + 会话切换完成。
        assert_conserved(store, fingerprints)
        assert store.stats() == {"sent": 2, "pending": 2}
        assert store.pending_session_switch() is None
        assert store.operational_status()["sent_records_total"] == 2
        # 反模式检查：新会话的 local_seq 必须恰好覆盖**全部**未确认帧（连续 1..k），
        # 即没有「跳过高水位之前数据」造成漏帧。
        assert store.operational_status()["last_seq"] == 2

        # 4) 重放未确认项：全部收敛为 sent，帧身份不变。
        async with client_for(
            envelope_handler(
                lambda items: [
                    ack_for(item, "ack", durability="redis-aof") for item in items
                ]
            )
        ) as replay_client:
            restarted = Uploader(
                store,
                settings(tmp_path),
                client=replay_client,
                signer=FakeSigner(),
            )
            await restarted.run_cycle()

    assert store.stats() == {"sent": 4}
    assert_conserved(store, fingerprints)
    assert store.operational_status()["sent_records_total"] == 4
    store.close()


@pytest.mark.asyncio
async def test_lost_ack_is_reconciled_by_center_idempotency(tmp_path: Path) -> None:
    """ACK 应答丢失：中心已提交但客户端只收到前 1 个 result。"""
    store = OutboxStore(tmp_path / "edge.db")
    fingerprints = seed(store, 3)
    old_session = store.current_session_id()
    center = HighWaterCenter(truncate_first_response_to=1)

    async with client_for(envelope_handler(center.results)) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()

        rows = snapshot(store)
        confirmed = [b for b, r in rows.items() if r["state"] == "sent"]
        assert len(confirmed) == 1
        assert rows[confirmed[0]]["upload_session_id"] == old_session
        # 中心已经收下 3 条（hw=3），本地不许把没收到的 ACK 当成已提交。
        assert center.hw[old_session] == 3
        unconfirmed = [b for b in fingerprints if b not in confirmed]
        assert len(unconfirmed) == 2
        for batch_id in unconfirmed:
            assert rows[batch_id]["state"] == "retry_wait"
            assert rows[batch_id]["upload_session_id"] == old_session
        assert_conserved(store, fingerprints)

        # 重放：中心按高水位返回 duplicate（幂等对账），不产生静默丢弃。
        for batch_id in unconfirmed:
            with store._lock:
                store._connection.execute(
                    "UPDATE outbox SET next_retry_at=0 WHERE batch_id=?", (batch_id,)
                )
        await uploader.run_cycle()

    assert store.stats() == {"sent": 3}
    assert_conserved(store, fingerprints)
    assert center.statuses.count("duplicate") == 2, center.statuses
    store.close()


@pytest.mark.asyncio
async def test_transport_loss_after_center_commit_replays_to_duplicates(
    tmp_path: Path,
) -> None:
    """ACK 应答整体丢失（连接断开）：中心已提交，本地必须重放并按 duplicate 对账。"""
    store = OutboxStore(tmp_path / "edge.db")
    fingerprints = seed(store, 3)
    old_session = store.current_session_id()
    center = HighWaterCenter()
    state = {"drops": 0}

    def handler(request: httpx.Request) -> httpx.Response:
        items = json.loads(request.content)["items"]
        results = center.results(items)
        assert len(results) == 3
        if state["drops"] == 0:
            state["drops"] = 1
            raise httpx.ConnectError("response lost after center commit", request=request)
        return httpx.Response(200, json={"results": results})

    async with client_for(handler) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()

        # 中心已提交 3 条，但本地一条都不能算已确认。
        assert center.hw[old_session] == 3
        assert store.stats() == {"retry_wait": 3}
        assert_conserved(store, fingerprints)

        with store._lock:
            store._connection.execute("UPDATE outbox SET next_retry_at=0")
        await uploader.run_cycle()

    assert store.stats() == {"sent": 3}
    assert_conserved(store, fingerprints)
    assert center.statuses.count("duplicate") == 3, center.statuses
    store.close()


class CrashOnceStore:
    """代理：首次 ``complete_batch`` 抛 ``SystemExit``，模拟确认后本地硬崩。"""

    def __init__(self, inner: OutboxStore) -> None:
        self._inner = inner
        self.crashed = False

    def complete_batch(self, completions, lease_owner):
        if not self.crashed:
            self.crashed = True
            raise SystemExit("simulated local crash before terminal persistence")
        return self._inner.complete_batch(completions, lease_owner)

    def __getattr__(self, name):
        return getattr(self._inner, name)


@pytest.mark.asyncio
async def test_crash_after_ack_in_a_mixed_batch_keeps_no_false_sent(
    tmp_path: Path,
) -> None:
    """确认后本地崩溃（混合批次）：不得出现 sent，重启后重放并按 duplicate 收敛。"""
    store = OutboxStore(tmp_path / "edge.db")
    fingerprints = seed(store, 2)
    order = list(fingerprints)
    crashing = CrashOnceStore(store)

    def results(items: list[dict]) -> list[dict]:
        return [ack_for(items[0], "ack", durability="redis-aof"), ack_for(items[1], "commit_unknown")]

    async with client_for(envelope_handler(results)) as client:
        uploader = Uploader(
            crashing, settings(tmp_path), client=client, signer=FakeSigner()
        )
        with pytest.raises(SystemExit, match="simulated local crash"):
            await uploader.run_cycle()

    # 崩溃发生在「已 ACK 前缀」落库之前：没有任何帧被当成已确认。
    assert store.stats() == {"inflight": 2}
    assert all(row["state"] != "sent" for row in snapshot(store).values())
    assert_conserved(store, fingerprints)

    # 重启：恢复租约 → 重放 → 中心按 duplicate 对账（至少一次语义）。
    assert store.recover_inflight("restart-owner") == 2
    assert store.stats() == {"pending": 2}
    async with client_for(
        envelope_handler(
            lambda items: [ack_for(item, "duplicate") for item in items]
        )
    ) as replay_client:
        restarted = Uploader(
            store, settings(tmp_path), client=replay_client, signer=FakeSigner()
        )
        await restarted.run_cycle()

    assert store.stats() == {"sent": 2}
    assert_conserved(store, fingerprints)
    assert [snapshot(store)[batch_id]["state"] for batch_id in order] == ["sent", "sent"]
    store.close()


@pytest.mark.asyncio
async def test_crash_during_session_switch_then_restart_completes_without_loss(
    tmp_path: Path,
) -> None:
    """切换中崩溃（硬中断）：ACK 前缀已落库，未确认帧留在新会话，重启补完切换。"""
    store = OutboxStore(tmp_path / "edge.db")
    fingerprints = seed(store, 2)
    order = list(fingerprints)
    old_session = store.current_session_id()
    switch_bodies: list[dict] = []

    def results(items: list[dict]) -> list[dict]:
        return [ack_for(items[0], "ack", durability="redis-aof"), ack_for(items[1], "seq_reject")]

    def dying_switch(request: httpx.Request, body: dict) -> httpx.Response:
        switch_bodies.append(body)
        raise SystemExit("simulated process death during session switch")

    async with client_for(envelope_handler(results, session_switch=dying_switch)) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        with pytest.raises(SystemExit, match="simulated process death"):
            await uploader.run_cycle()

    # 切换尚未完成（中心未确认），但 ACK 前缀已持久，且旧会话身份保留。
    assert switch_bodies and switch_bodies[0]["old_session_id"] == old_session
    rows = snapshot(store)
    assert rows[order[0]]["state"] == "sent"
    assert rows[order[0]]["upload_session_id"] == old_session
    pending = store.pending_session_switch()
    assert pending is not None and pending.old_session_id == old_session
    assert rows[order[1]]["state"] == "pending"
    assert rows[order[1]]["upload_session_id"] == store.current_session_id()
    assert_conserved(store, fingerprints)

    # 真重启：新实例（新 lease owner）先补完切换，再重放未确认帧。
    async with client_for(
        envelope_handler(
            lambda items: [ack_for(item, "ack", durability="redis-aof") for item in items]
        )
    ) as restart_client:
        restarted = Uploader(
            store, settings(tmp_path), client=restart_client, signer=FakeSigner()
        )
        assert await restarted.recover_inflight() == 0
        await restarted.run_cycle()

    assert store.pending_session_switch() is None
    assert store.stats() == {"sent": 2}
    assert_conserved(store, fingerprints)
    store.close()
