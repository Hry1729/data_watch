"""B6：传输尝试 / 协议拒绝 / 数据错误三类计数分离（存储层 + Uploader 判定）。

存储层（W1-E1 落地，本文件原有用例）：
- 429/5xx/断网等传输类失败**绝不**消耗坏数据隔离预算（``reject_count`` 保持 0）；
- 只有中心给出协议级裁决的三个原因（``http_400_contract_rejected`` / ``seq_reject`` /
  ``commit_unknown``）才消耗该预算；
- 中心侧异常（``missing_ack`` / ``malformed_ack`` / ``unknown_ack`` / 未确认 AOF /
  ``session_obsolete``）同样不消耗该预算（保守方向：不得因中心侧问题杀好数据）；
- 已存在的现场库能被补列迁移。

跨 IPC 与判定层（W1-E1b 按路线 B 落地，本文件新增用例）：
- ``reject_count`` 经 Edge Storage IPC 的 claim 回传给 Uploader（真实 HTTP IPC 往返）；
- Uploader 的隔离判定改用 ``reject_count``（``edge/app/uploader.py``
  ``_quarantine_budget_exhausted``），阈值仍是 ``UploaderSettings.max_attempts``；
- 预算**跨 session 切换累积**、协议裁决的行级证据（``last_error``/``http_status``）
  跨切换保留；
- 反向：真正被协议反复拒绝的载荷**仍然**会被隔离（不得矫枉过正成"永不隔离"）；
- 兼容：旧版 Edge Storage 不回传 ``reject_count`` 时**显式**降级为 ``attempt_count``
  并告警一次（见 docs/整改记录/B6.md「兼容策略」）。
"""

from __future__ import annotations

import json
import sqlite3
import threading
import time
from pathlib import Path
from uuid import uuid4

import httpx
import pytest

from edge.app.outbox import OutboxRecord, OutboxStore
from edge.app.storage_client import EdgeStorageClient, read_role_tokens
from edge.app.storage_service import IpcAuthenticator, StorageEngine, StorageHttpServer
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


def seed(store: OutboxStore, count: int = 1) -> None:
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


def row(store: OutboxStore) -> dict:
    with store._lock:
        fetched = store._connection.execute(
            "SELECT batch_id, local_seq, state, attempt_count, reject_count, "
            "next_retry_at, last_error, http_status FROM outbox ORDER BY local_seq"
        ).fetchone()
    return dict(fetched)


def clear_retry_schedule(store: OutboxStore) -> None:
    """免除退避等待，让下一轮能立刻重新 claim（仅测试脚手架）。"""
    with store._lock:
        store._connection.execute("UPDATE outbox SET next_retry_at=0")


def ack_result(item: dict, status: str, **extra) -> dict:
    return {
        "batch_id": item["batch_id"],
        "upload_session_id": item["upload_session_id"],
        "local_seq": item["local_seq"],
        "status": status,
        **extra,
    }


def transport_client() -> httpx.AsyncClient:
    return httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(
            lambda request: httpx.Response(503, text="service unavailable")
        ),
    )


def _run_status_cycle(store: OutboxStore, tmp_path: Path, results_builder):
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/ingest/session_switch":
            return httpx.Response(200, json={"status": "switched"})
        items = json.loads(request.content)["items"]
        return httpx.Response(200, json={"results": results_builder(items)})

    async def run() -> None:
        async with httpx.AsyncClient(
            base_url="https://center.test", transport=httpx.MockTransport(handler)
        ) as client:
            uploader = Uploader(
                store, settings(tmp_path), client=client, signer=FakeSigner()
            )
            await uploader.run_cycle()

    return run


@pytest.mark.asyncio
async def test_transport_failures_never_consume_the_quarantine_budget(
    tmp_path: Path,
) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    seed(store)

    async with transport_client() as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        for round_index in range(1, 4):
            await uploader.run_cycle()
            current = row(store)
            assert current["state"] == "retry_wait"
            assert current["attempt_count"] == round_index
            # 传输类失败：隔离预算必须保持 0。
            assert current["reject_count"] == 0
            assert current["http_status"] == 503
            clear_retry_schedule(store)
    store.close()


@pytest.mark.asyncio
async def test_only_protocol_verdicts_consume_the_quarantine_budget(tmp_path: Path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    seed(store)

    # 先制造 2 次传输失败：只累加 attempt_count。
    async with transport_client() as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        for _ in range(2):
            await uploader.run_cycle()
            clear_retry_schedule(store)
    assert row(store)["attempt_count"] == 2
    assert row(store)["reject_count"] == 0

    # 序列异常（协议级裁决）：消耗隔离预算 1 次。
    #
    # ⚠️ 修正记录（W1-E1b）：原 mock 对**所有**请求都按批量上传体解析
    # ``json.loads(request.content)["items"]``，而 ``seq_reject`` 之后 Uploader 会
    # 立刻走 ``_abandon_and_switch()`` 向 ``/ingest/session_switch`` 发请求，其真实
    # 请求体是 ``{"node_uuid","old_session_id","new_session_id"}``
    # （``edge/app/uploader.py:541-546``），没有 ``items`` → ``KeyError: 'items'``。
    # 这是**测试 mock 写错**，不是实现分支被 B5 改动（HEAD 版本的 seq_reject 分支
    # 同样调用 ``_abandon_and_switch()``）。按真实生产请求体形状分派后，
    # 原有用例的**所有断言一字未改**。
    def protocol_verdict_handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/ingest/session_switch":
            return httpx.Response(200, json={"status": "switched"})
        items = json.loads(request.content)["items"]
        return httpx.Response(
            200,
            json={"results": [ack_result(items[0], "seq_reject")]},
        )

    async with httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(protocol_verdict_handler),
    ) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()

    current = row(store)
    assert current["reject_count"] == 1
    assert current["attempt_count"] == 3
    assert current["http_status"] == 200
    assert current["last_error"] == "seq_reject"
    store.close()


@pytest.mark.asyncio
async def test_contract_rejection_is_recorded_as_independent_evidence(tmp_path: Path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    seed(store)

    async with transport_client() as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()
    clear_retry_schedule(store)

    async with httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(lambda request: httpx.Response(400, text="bad contract")),
    ) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()

    current = row(store)
    # dead/隔离判定的依据必须来自协议裁决，而不是残留的传输状态码。
    assert current["reject_count"] == 1
    assert current["http_status"] == 400
    assert current["last_error"] == "http_400_contract_rejected"
    store.close()


@pytest.mark.asyncio
async def test_center_side_anomalies_do_not_consume_the_quarantine_budget(
    tmp_path: Path,
) -> None:
    """中心侧异常（ACK 缺失/未知状态）不是该行数据的错，不得消耗隔离预算。"""
    store = OutboxStore(tmp_path / "edge.db")
    seed(store)

    async with httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(
            lambda request: httpx.Response(200, json={"results": []})
        ),
    ) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()
    current = row(store)
    assert current["state"] == "retry_wait"
    assert current["reject_count"] == 0
    assert current["last_error"] == "missing_ack"
    clear_retry_schedule(store)

    async with httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(
            lambda request: httpx.Response(
                200,
                json={
                    "results": [
                        ack_result(json.loads(request.content)["items"][0], "brand_new_status")
                    ]
                },
            )
        ),
    ) as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        await uploader.run_cycle()
    current = row(store)
    assert current["reject_count"] == 0
    assert current["last_error"] == "unknown_ack:brand_new_status"
    store.close()


def test_existing_database_is_migrated_and_keeps_the_budget_at_zero(tmp_path: Path) -> None:
    database = tmp_path / "legacy.db"
    connection = sqlite3.connect(database)
    connection.executescript(
        """
        CREATE TABLE outbox (
            batch_id TEXT PRIMARY KEY,
            upload_session_id TEXT NOT NULL,
            local_seq INTEGER NOT NULL,
            device_code TEXT NOT NULL,
            group_code TEXT NOT NULL,
            assignment_epoch INTEGER NOT NULL,
            config_version INTEGER NOT NULL,
            ts INTEGER NOT NULL,
            payload BLOB NOT NULL,
            state TEXT NOT NULL DEFAULT 'pending',
            attempt_count INTEGER NOT NULL DEFAULT 0,
            next_retry_at INTEGER,
            last_error TEXT,
            http_status INTEGER,
            lease_owner TEXT,
            inflight_at INTEGER,
            lease_until INTEGER,
            acked_at INTEGER,
            dead_at INTEGER,
            created_at INTEGER NOT NULL,
            UNIQUE (upload_session_id, local_seq)
        );
        """
    )
    connection.execute(
        """
        INSERT INTO outbox(
            batch_id, upload_session_id, local_seq, device_code, group_code,
            assignment_epoch, config_version, ts, payload, state, attempt_count,
            lease_owner, inflight_at, lease_until, created_at
        ) VALUES('legacy-1', 'session-legacy', 1, 'QC100', 'g001', 7, 12, 1721800000000, '{}',
                 'inflight', 4, 'legacy-owner', 1, 9999999999999, 1)
        """
    )
    connection.commit()
    connection.close()

    store = OutboxStore(database)
    with store._lock:
        columns = {
            str(info[1]) for info in store._connection.execute("PRAGMA table_info(outbox)")
        }
        migrated = store._connection.execute(
            "SELECT state, attempt_count, reject_count FROM outbox WHERE batch_id='legacy-1'"
        ).fetchone()
    assert "reject_count" in columns
    assert migrated["attempt_count"] == 4  # 历史计数不被重置
    assert migrated["reject_count"] == 0  # 新预算从 0 开始，不追溯历史网络失败

    # 已有行必须能继续按新规则累加（无 no such column）。
    store.mark_retry_batch(
        [
            (
                type(
                    "R",
                    (),
                    {
                        "batch_id": "legacy-1",
                        "upload_session_id": "session-legacy",
                        "local_seq": 1,
                    },
                )(),
                500,
            )
        ],
        "legacy-owner",
        error="seq_reject",
        http_status=200,
    )
    with store._lock:
        updated = store._connection.execute(
            "SELECT state, attempt_count, reject_count FROM outbox WHERE batch_id='legacy-1'"
        ).fetchone()
    assert updated["attempt_count"] == 5
    assert updated["reject_count"] == 1
    store.close()


# ---------------------------------------------------------------------------
# W1-E1b（路线 B 落地）：判定层改读 reject_count、预算跨切换累积、反向仍隔离、
# IPC 往返、旧版对端显式降级。
# ---------------------------------------------------------------------------


def protocol_verdict_handler(request: httpx.Request) -> httpx.Response:
    """中心对第一项给出协议级裁决 ``seq_reject``（其后按 service.py 的 break 截止）。"""
    if request.url.path == "/ingest/session_switch":
        return httpx.Response(200, json={"status": "switched"})
    items = json.loads(request.content)["items"]
    return httpx.Response(
        200, json={"results": [ack_result(items[0], "seq_reject")]}
    )


async def _run_cycle(store: OutboxStore, tmp_path: Path, handler, **overrides) -> None:
    async with httpx.AsyncClient(
        base_url="https://center.test", transport=httpx.MockTransport(handler)
    ) as client:
        uploader = Uploader(
            store, settings(tmp_path, **overrides), client=client, signer=FakeSigner()
        )
        await uploader.run_cycle()


@pytest.mark.asyncio
async def test_transport_history_never_exhausts_the_quarantine_budget(
    tmp_path: Path,
) -> None:
    """清单 229 第 1 条：长时间断网后一次序列异常，好数据不得被判 dead。"""
    store = OutboxStore(tmp_path / "edge.db")
    seed(store)

    # 先制造 5 次传输失败（远超 max_attempts-1），只累加 attempt_count。
    async with transport_client() as client:
        uploader = Uploader(
            store, settings(tmp_path, max_attempts=3), client=client, signer=FakeSigner()
        )
        for _ in range(5):
            await uploader.run_cycle()
            clear_retry_schedule(store)
    before = row(store)
    assert before["attempt_count"] == 5
    assert before["reject_count"] == 0
    assert before["http_status"] == 503

    # 一次真正的序列异常：好数据仍不得被判 dead（判定只看 reject_count）。
    await _run_cycle(store, tmp_path, protocol_verdict_handler, max_attempts=3)

    current = row(store)
    assert current["state"] != "dead"
    assert current["reject_count"] == 1
    assert current["attempt_count"] == 6
    # 行级证据必须指向协议裁决，而不是残留的传输状态码 503。
    assert current["http_status"] == 200
    assert current["last_error"] == "seq_reject"
    store.close()


@pytest.mark.asyncio
async def test_protocol_budget_accumulates_across_session_switches(
    tmp_path: Path,
) -> None:
    """预算跨 session 切换累积（不被切换清零），且真正非法的载荷仍会被隔离。"""
    store = OutboxStore(tmp_path / "edge.db")
    seed(store)
    sessions = [store.current_session_id()]

    for expected_rejects in (1, 2):
        await _run_cycle(store, tmp_path, protocol_verdict_handler, max_attempts=3)
        current = row(store)
        assert current["reject_count"] == expected_rejects
        assert current["state"] != "dead"
        # 每次协议裁决都会切换会话；预算与证据都必须活过切换。
        assert store.current_session_id() not in sessions
        sessions.append(store.current_session_id())
        assert current["last_error"] == "seq_reject"
        assert current["http_status"] == 200

    # 第三次协议裁决耗尽预算（2 + 1 >= max_attempts=3）→ 隔离。
    await _run_cycle(store, tmp_path, protocol_verdict_handler, max_attempts=3)
    final = row(store)
    assert final["state"] == "dead"
    assert final["reject_count"] == 2  # 致命那一次裁决记在 last_error/http_status
    assert final["last_error"] == "seq_reject"
    assert final["http_status"] == 200
    assert store.stats() == {"dead": 1}
    store.close()


@pytest.mark.asyncio
async def test_dead_row_carries_the_protocol_verdict_not_a_stale_transport_status(
    tmp_path: Path,
) -> None:
    """dead 行的取证必须来自协议裁决：不得残留上一次 503 传输状态码。"""
    store = OutboxStore(tmp_path / "edge.db")
    seed(store)
    max_attempts = 3

    # 交替：协议裁决(1) → 503 → 协议裁决(2) → 503 → 致命协议裁决(3)
    await _run_cycle(store, tmp_path, protocol_verdict_handler, max_attempts=max_attempts)
    async with transport_client() as client:
        uploader = Uploader(
            store,
            settings(tmp_path, max_attempts=max_attempts),
            client=client,
            signer=FakeSigner(),
        )
        await uploader.run_cycle()
        clear_retry_schedule(store)
        assert row(store)["http_status"] == 503
        await _run_cycle(
            store, tmp_path, protocol_verdict_handler, max_attempts=max_attempts
        )
        assert row(store)["reject_count"] == 2
        await uploader.run_cycle()
        clear_retry_schedule(store)
        assert row(store)["http_status"] == 503  # 致命裁决之前确实是传输状态码

    await _run_cycle(store, tmp_path, protocol_verdict_handler, max_attempts=max_attempts)
    final = row(store)
    assert final["state"] == "dead"
    assert final["last_error"] == "seq_reject"
    assert final["http_status"] == 200, "dead 行残留了传输状态码，取证会指向错误原因"
    store.close()


@pytest.mark.asyncio
async def test_legacy_storage_without_reject_count_degrades_explicitly(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    """兼容策略：旧版存储不回传 reject_count → 显式降级 attempt_count 并告警一次。"""
    store = OutboxStore(tmp_path / "edge.db")
    async with httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(
            lambda request: httpx.Response(503, text="down")
        ),
    ) as client:
        uploader = Uploader(
            store, settings(tmp_path, max_attempts=3), client=client, signer=FakeSigner()
        )
        legacy = OutboxRecord("b1", "s1", 1, 7, b"{}", 9, None)
        fresh = OutboxRecord("b2", "s1", 2, 7, b"{}", 0, 0)
        budget_two = OutboxRecord("b3", "s1", 3, 7, b"{}", 0, 2)

        with caplog.at_level("WARNING", logger="edge.app.uploader"), caplog.at_level(
            "WARNING", logger="edge.uploader"
        ):
            # 旧版对端：字段缺失 → 降级为旧的 attempt_count 语义（不得静默当成 0，
            # 否则 attempt_count=9 的好数据会被当成"预算为 0"而永不隔离）。
            assert uploader._quarantine_budget_exhausted(legacy) is True
            # 第二次调用不再重复告警（一次升级窗口只提示一次）。
            assert uploader._quarantine_budget_exhausted(legacy) is True
        warnings = [r for r in caplog.records if r.levelname == "WARNING"]
        assert len(warnings) == 1, [r.message for r in warnings]
        assert "reject_count" in warnings[0].getMessage()

        # 新版对端：只看 reject_count，历史 attempt_count 不再参与判定。
        assert uploader._quarantine_budget_exhausted(fresh) is False
        assert uploader._quarantine_budget_exhausted(budget_two) is True
        assert (
            uploader._quarantine_budget_exhausted(
                OutboxRecord("b4", "s1", 4, 7, b"{}", 99, 0)
            )
            is False
        ), "历史网络次数不得影响隔离判定"
    store.close()


def test_reject_count_is_reported_as_unknown_only_when_absent() -> None:
    """IPC 反序列化按**存在性**判定：缺失 → None（旧版），显式 0 → 0（新版）。"""
    base = {
        "batch_id": "b",
        "upload_session_id": "s",
        "local_seq": 1,
        "assignment_epoch": 7,
        "payload_b64": "",
        "attempt_count": 3,
    }
    assert EdgeStorageClient._record(dict(base)).reject_count is None
    assert EdgeStorageClient._record({**base, "reject_count": 0}).reject_count == 0
    assert EdgeStorageClient._record({**base, "reject_count": 4}).reject_count == 4


def test_reject_count_crosses_the_edge_storage_ipc_boundary(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """真实 HTTP IPC 往返：claim 回来的行必须带着服务端权威的 reject_count。"""
    database = tmp_path / "edge.db"
    token_file = tmp_path / "edge" / "storage-ipc-tokens.json"
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN_FILE", str(token_file))
    store = OutboxStore(database)
    seed(store)
    # 制造一条 state='retry_wait'（可被 claim）且 reject_count=4 的历史行。
    claimed = store.claim_batch(
        target_seq=store.snapshot_target_seq(),
        lease_owner="seed-owner",
        lease_ms=60_000,
        max_items=10,
        max_bytes=1024 * 1024,
    )
    assert len(claimed) == 1
    store.mark_retry_batch(
        [(claimed[0], 0)], "seed-owner", error="seq_reject", http_status=200
    )
    with store._lock:
        store._connection.execute(
            "UPDATE outbox SET reject_count=4 WHERE batch_id=?", (claimed[0].batch_id,)
        )
    store.close()

    engine = StorageEngine(database, max_queue=256)
    engine.start()
    server = StorageHttpServer(
        ("127.0.0.1", 0), engine, IpcAuthenticator.from_file(token_file)
    )
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    client = EdgeStorageClient(
        f"http://127.0.0.1:{server.server_port}",
        role="uploader",
        token=read_role_tokens(token_file)["uploader"],
    )
    try:
        rows = client.claim_batch(
            target_seq=client.snapshot_target_seq(),
            lease_owner="ipc-owner",
            lease_ms=60_000,
            max_items=10,
            max_bytes=1024 * 1024,
        )
        assert len(rows) == 1
        assert rows[0].reject_count == 4, "reject_count 没有跨 Edge Storage IPC 回传"
        assert rows[0].attempt_count == 1
    finally:
        client.close()
        server.shutdown()
        server.server_close()
        engine.close()
        thread.join(timeout=5)


@pytest.mark.asyncio
async def test_ipc_claimed_reject_count_drives_the_quarantine_judgment(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """端到端（真实 HTTP IPC + 真实 Uploader 判定 + 真实 SQLite）：

    claim 回来的 ``reject_count`` 必须直接决定隔离判定，且 dead 行经 IPC 落库后
    带着协议裁决的证据（而不是残留的传输状态码）。
    """
    database = tmp_path / "edge.db"
    token_file = tmp_path / "edge" / "storage-ipc-tokens.json"
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN_FILE", str(token_file))
    store = OutboxStore(database)
    seed(store)
    claimed = store.claim_batch(
        target_seq=store.snapshot_target_seq(),
        lease_owner="seed-owner",
        lease_ms=60_000,
        max_items=10,
        max_bytes=1024 * 1024,
    )
    assert len(claimed) == 1
    # 历史：4 次协议裁决 + 若干传输失败（http_status 先被 503 覆盖）。
    store.mark_retry_batch(
        [(claimed[0], 0)], "seed-owner", error="service unavailable", http_status=503
    )
    with store._lock:
        store._connection.execute("UPDATE outbox SET reject_count=4, attempt_count=9")
    store.close()

    engine = StorageEngine(database, max_queue=256)
    engine.start()
    server = StorageHttpServer(
        ("127.0.0.1", 0), engine, IpcAuthenticator.from_file(token_file)
    )
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    ipc = EdgeStorageClient(
        f"http://127.0.0.1:{server.server_port}",
        role="uploader",
        token=read_role_tokens(token_file)["uploader"],
    )
    try:
        # max_attempts=5：4 + 1 >= 5 → 这一次协议裁决就该隔离。
        async with httpx.AsyncClient(
            base_url="https://center.test",
            transport=httpx.MockTransport(protocol_verdict_handler),
        ) as center:
            uploader = Uploader(
                ipc, settings(tmp_path, max_attempts=5), client=center, signer=FakeSigner()
            )
            await uploader.run_cycle()

        with sqlite3.connect(f"file:{database.as_posix()}?mode=ro", uri=True) as connection:
            row = dict(
                zip(
                    ("state", "attempt_count", "reject_count", "last_error", "http_status"),
                    connection.execute(
                        "SELECT state, attempt_count, reject_count, last_error, "
                        "http_status FROM outbox ORDER BY local_seq"
                    ).fetchone(),
                )
            )
        assert row["state"] == "dead"
        # 不变量：致命那一次协议裁决不进 mark_retry_batch，所以两个计数器都停在
        # 「已记录的次数」上（reject_count=4 = max_attempts-1），裁决本身记在
        # last_error/http_status 里。传输历史只进 attempt_count。
        assert row["reject_count"] == 4
        assert row["attempt_count"] == 9
        assert row["last_error"] == "seq_reject"
        assert row["http_status"] == 200, "dead 行残留了传输状态码 503（取证误导）"
    finally:
        ipc.close()
        server.shutdown()
        server.server_close()
        engine.close()
        thread.join(timeout=5)
