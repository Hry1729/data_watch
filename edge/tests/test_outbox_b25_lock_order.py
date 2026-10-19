"""B25：Uploader 必须在获得单实例锁之后才执行 outbox 状态恢复。

覆盖：
1. 仅构造第二个 Uploader（未取锁）不得改写他人 inflight 租约（最小反例，W0-B 实测的缺陷）。
2. 胜出实例仍能显式恢复旧崩溃租约（``recover_inflight`` 必须在锁内被调用）。
3. 真实入口 ``edge/uploader_main.py`` 双进程争锁：败方无任何 outbox 状态写入、
   胜方恢复旧崩溃租约、运行中实例的未过期租约不被重置。
"""

from __future__ import annotations

import base64
import os
import signal
import sqlite3
import subprocess
import sys
import threading
import time
from pathlib import Path
from uuid import uuid4

import httpx
import pytest
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.serialization import (
    Encoding,
    NoEncryption,
    PrivateFormat,
)

import edge.uploader_main as uploader_main
from edge.app.outbox import OutboxStore
from edge.app.storage_service import StorageEngine, StorageHttpServer
from edge.app.uploader import SingleInstanceLock, Uploader, UploaderSettings


REPO_ROOT = Path(__file__).resolve().parents[2]
TABLE_COLUMNS = (
    "batch_id",
    "local_seq",
    "state",
    "attempt_count",
    "upload_session_id",
    "lease_owner",
    "lease_until",
    "next_retry_at",
    "last_error",
    "http_status",
)


class FakeSigner:
    def headers(self, method, path, payload):
        return {}


def settings(tmp_path: Path, *, lock_name: str = "uploader.lock") -> UploaderSettings:
    return UploaderSettings(
        center_url="https://center.test",
        node_uuid=str(uuid4()),
        private_key_path=tmp_path / "unused.pem",
        lock_path=tmp_path / "edge" / lock_name,
        retry_base_ms=500,
    )


def seed(store: OutboxStore, count: int = 3) -> None:
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


def claim_all(store: OutboxStore, lease_owner: str, *, lease_ms: int = 30_000):
    return store.claim_batch(
        target_seq=store.snapshot_target_seq(),
        lease_owner=lease_owner,
        lease_ms=lease_ms,
        max_items=10,
        max_bytes=10**6,
    )


def rows(store: OutboxStore) -> list[dict]:
    with store._lock:
        fetched = store._connection.execute(
            f"SELECT {', '.join(TABLE_COLUMNS)} FROM outbox ORDER BY local_seq"
        ).fetchall()
    return [dict(row) for row in fetched]


def _idle_client() -> httpx.AsyncClient:
    return httpx.AsyncClient(
        base_url="https://center.test",
        transport=httpx.MockTransport(lambda request: httpx.Response(503, text="down")),
    )


@pytest.mark.asyncio
async def test_second_uploader_construction_does_not_touch_another_owners_lease(
    tmp_path: Path,
) -> None:
    """最小反例：未取锁的实例不得改写存储状态。"""
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 1)
    assert [record.local_seq for record in claim_all(store, "owner-A")] == [1]
    assert store.stats() == {"inflight": 1}

    async with _idle_client() as client:
        # 只构造，不取锁、不调用 run_forever。
        Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())

        assert store.stats() == {"inflight": 1}
        row = rows(store)[0]
        assert row["state"] == "inflight"
        assert row["lease_owner"] == "owner-A"
    store.close()


@pytest.mark.asyncio
async def test_explicit_recovery_still_recovers_stale_lease(tmp_path: Path) -> None:
    """胜出实例（锁内）仍必须能恢复旧崩溃租约。"""
    store = OutboxStore(tmp_path / "edge.db")
    seed(store, 1)
    claim_all(store, "crashed-owner")

    async with _idle_client() as client:
        uploader = Uploader(store, settings(tmp_path), client=client, signer=FakeSigner())
        assert store.stats() == {"inflight": 1}

        recovered = await uploader.recover_inflight()

        assert recovered == 1
        assert store.stats() == {"pending": 1}
        assert rows(store)[0]["lease_owner"] is None
    store.close()


class _RecordingStore:
    """记录所有调用；被 Uploader 构造期触碰即视为 B25 回归。"""

    def __init__(self) -> None:
        self.calls: list[str] = []

    def recover_inflight(self, lease_owner: str) -> int:
        self.calls.append("recover_inflight")
        raise AssertionError("recover_inflight must run inside the single-instance lock")

    def close(self) -> None:
        self.calls.append("close")


def test_uploader_main_writes_nothing_when_the_lock_is_held(
    tmp_path: Path, monkeypatch
) -> None:
    """真实入口（进程内）：锁被占用时 main() 不得触碰 outbox 状态。"""
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    monkeypatch.setenv("DATAWATCH_CENTER_URL", "https://center.invalid")
    monkeypatch.setenv("DATAWATCH_NODE_UUID", "node-b25")
    monkeypatch.setenv(
        "DATAWATCH_UPLOADER_PRIVATE_KEY_B64",
        base64.b64encode(_private_key_pem()).decode("ascii"),
    )
    # main() 在 Windows 上会回退到 signal.signal；不要污染 pytest 的 SIGINT 处理。
    monkeypatch.setattr(signal, "signal", lambda *args, **kwargs: None)
    recording = _RecordingStore()
    monkeypatch.setattr(uploader_main, "EdgeStorageClient", lambda: recording)

    with SingleInstanceLock(tmp_path / "edge" / "uploader.lock"):
        with pytest.raises(RuntimeError, match="another uploader instance is running"):
            uploader_main.asyncio.run(uploader_main.main())

    assert "recover_inflight" not in recording.calls


def test_lock_file_never_grows_while_the_lock_is_contended(tmp_path: Path) -> None:
    """争锁失败不得让锁文件增长，且单实例锁语义不变（仍只有字节 0 被锁）。"""
    lock_path = tmp_path / "edge" / "uploader.lock"
    with SingleInstanceLock(lock_path):
        # 首次获取时写入 1 字节哨兵，锁定的就是这个字节。
        assert lock_path.stat().st_size == 1
        for _ in range(5):
            with pytest.raises(RuntimeError, match="another uploader instance is running"):
                SingleInstanceLock(lock_path).__enter__()
            # 每次失败都保持 1 字节：append 模式下无条件 write 会让它 +1 字节/次。
            assert lock_path.stat().st_size == 1
    # Windows 字节锁是强制锁：持锁期间连本进程的第二个句柄都读不到锁定区间，故解锁后再读内容。
    assert lock_path.read_bytes() == b"0"
    # 释放后仍可再次获取（锁语义未变），且不追加任何字节。
    with SingleInstanceLock(lock_path):
        assert lock_path.stat().st_size == 1
    assert lock_path.read_bytes() == b"0"
    assert lock_path.stat().st_size == 1


def _private_key_pem() -> bytes:
    return Ed25519PrivateKey.generate().private_bytes(
        Encoding.PEM,
        PrivateFormat.PKCS8,
        NoEncryption(),
    )


def _connect(database: Path) -> sqlite3.Connection:
    connection = sqlite3.connect(database, timeout=10)
    connection.row_factory = sqlite3.Row
    connection.execute("PRAGMA busy_timeout=10000")
    return connection


def _snapshot(database: Path) -> list[dict]:
    with _connect(database) as connection:
        fetched = connection.execute(
            f"SELECT {', '.join(TABLE_COLUMNS)} FROM outbox ORDER BY local_seq"
        ).fetchall()
    return [dict(row) for row in fetched]


def _wait_for_pending(database: Path, expected: int, timeout: float = 60.0) -> None:
    deadline = time.monotonic() + timeout
    last: dict[str, int] = {}
    while time.monotonic() < deadline:
        with _connect(database) as connection:
            fetched = connection.execute(
                "SELECT state, COUNT(*) AS count FROM outbox GROUP BY state"
            ).fetchall()
        last = {str(row["state"]): int(row["count"]) for row in fetched}
        if last.get("pending") == expected and len(last) == 1:
            return
        time.sleep(0.2)
    raise AssertionError(f"winner did not recover the crashed lease: {last}")


def _force_live_lease(database: Path, local_seq: int, owner: str) -> None:
    deadline = time.monotonic() + 10.0
    now = int(time.time() * 1000)
    while True:
        try:
            with _connect(database) as connection:
                connection.execute(
                    """
                    UPDATE outbox
                    SET state='inflight', lease_owner=?, inflight_at=?, lease_until=?
                    WHERE local_seq=?
                    """,
                    (owner, now, now + 60_000, local_seq),
                )
            return
        except sqlite3.OperationalError:
            if time.monotonic() > deadline:
                raise
            time.sleep(0.2)


def _uploader_env(
    data_root: Path, storage_url: str, token_file: Path
) -> dict[str, str]:
    env = {
        key: value
        for key, value in os.environ.items()
        if not key.startswith("DATAWATCH_")
    }
    env.update(
        {
            "PYTHONPATH": str(REPO_ROOT),
            "DATAWATCH_DATA_ROOT": str(data_root),
            "DATAWATCH_EDGE_STORAGE_URL": storage_url,
            # S3 的 Edge Storage IPC 每角色令牌文件（本测试内自建，不使用现场凭据）。
            "DATAWATCH_EDGE_STORAGE_TOKEN_FILE": str(token_file),
            "DATAWATCH_CENTER_URL": "https://center.invalid",
            "DATAWATCH_NODE_UUID": "node-b25-process",
            "DATAWATCH_UPLOADER_PRIVATE_KEY_B64": base64.b64encode(
                _private_key_pem()
            ).decode("ascii"),
        }
    )
    return env


def test_two_processes_contending_for_the_lock_write_no_state_when_losing(
    tmp_path: Path, monkeypatch
) -> None:
    """真实入口双进程争锁（验收：失败实例无状态写 / 胜方恢复崩溃租约 / live 租约不被重置）。"""
    database = tmp_path / "edge.db"
    token_file = tmp_path / "edge" / "storage-ipc-tokens.json"
    # 服务端与两个子进程共用同一份新建的 IPC 令牌文件（全部位于 tmp_path 内）。
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN_FILE", str(token_file))
    store = OutboxStore(database)
    seed(store, 3)
    assert len(claim_all(store, "crashed-owner")) == 3
    assert store.stats() == {"inflight": 3}
    # 让胜出实例保持空闲：Edge 9 之后有积压时即使 assignment 全部停用也会继续上传，
    # 因此再把快照目标序号置 0，使 claim_batch 取不到任何行（仅测试脚手架）。
    store.configure_assignment("QC100", 7, 100, 1, enabled=False)
    with store._lock:
        store._connection.execute("UPDATE metadata SET value='0' WHERE key='last_seq'")
    store.close()

    engine = StorageEngine(database, max_queue=256)
    engine.start()
    server = StorageHttpServer(("127.0.0.1", 0), engine)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    storage_url = f"http://127.0.0.1:{server.server_port}"
    env = _uploader_env(tmp_path, storage_url, token_file)
    winner_out = (tmp_path / "winner.log").open("wb")
    loser_out = (tmp_path / "loser.log").open("wb")
    winner: subprocess.Popen | None = None
    try:
        winner = subprocess.Popen(
            [sys.executable, str(REPO_ROOT / "edge" / "uploader_main.py")],
            cwd=str(REPO_ROOT),
            env=env,
            stdout=winner_out,
            stderr=subprocess.STDOUT,
        )
        # 胜方必须在锁内恢复旧崩溃租约（3 条 inflight → pending）。
        _wait_for_pending(database, 3)
        assert winner.poll() is None, "winner exited unexpectedly"

        # 运行中实例的未过期租约（模拟胜方自己的 inflight）。
        _force_live_lease(database, 3, "live-owner")
        before = _snapshot(database)
        assert before[2]["state"] == "inflight"
        assert before[2]["lease_owner"] == "live-owner"

        loser = subprocess.run(
            [sys.executable, str(REPO_ROOT / "edge" / "uploader_main.py")],
            cwd=str(REPO_ROOT),
            env=env,
            stdout=loser_out,
            stderr=subprocess.STDOUT,
            timeout=120,
        )
        assert loser.returncode != 0
        loser_out.flush()
        loser_log = (tmp_path / "loser.log").read_bytes()
        assert b"another uploader instance is running" in loser_log

        after = _snapshot(database)
        assert after == before
        assert after[2]["state"] == "inflight"
        assert after[2]["lease_owner"] == "live-owner"
        assert [row["state"] for row in after] == ["pending", "pending", "inflight"]
    finally:
        if winner is not None and winner.poll() is None:
            winner.terminate()
            try:
                winner.wait(timeout=15)
            except subprocess.TimeoutExpired:  # pragma: no cover - defensive
                winner.kill()
                winner.wait(timeout=15)
        winner_out.close()
        loser_out.close()
        server.shutdown()
        server.server_close()
        engine.close()
        thread.join(timeout=5)


def test_split_data_root_cannot_hijack_the_live_lease_of_the_same_outbox(
    tmp_path: Path, monkeypatch
) -> None:
    """P1-C 真实入口回归：**同 DATAWATCH_EDGE_DB + 不同 DATAWATCH_DATA_ROOT** 时，
    第二个实例必须被同一把锁挡住。

    修复前实测（真实现场入口，仅临时目录）：两个实例各自拿到
    ``rootA/edge/uploader.lock`` 与 ``rootB/edge/uploader.lock``，都能存活；后启动者经
    Edge Storage 执行 ``outbox.recover``，把前者**未过期**的 live 租约重置。因此这里同时断言
    「败方非零退出 + 日志为锁冲突 + outbox 逐行不变 + live 租约仍是原持有者 + rootB 下不存在锁」。
    """
    database = tmp_path / "shared" / "edge" / "edge.db"
    token_file = tmp_path / "shared" / "edge" / "storage-ipc-tokens.json"
    root_a = tmp_path / "rootA"
    root_b = tmp_path / "rootB"
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN_FILE", str(token_file))
    store = OutboxStore(database)
    seed(store, 2)
    assert len(claim_all(store, "crashed-owner")) == 2
    store.configure_assignment("QC100", 7, 100, 1, enabled=False)
    # 同上：有积压时会继续上传（Edge 9），置 0 目标序号让胜出实例保持空闲。
    with store._lock:
        store._connection.execute("UPDATE metadata SET value='0' WHERE key='last_seq'")
    store.close()

    engine = StorageEngine(database, max_queue=256)
    engine.start()
    server = StorageHttpServer(("127.0.0.1", 0), engine)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    storage_url = f"http://127.0.0.1:{server.server_port}"
    # 两个实例共享同一个 outbox 库，但 DATAWATCH_DATA_ROOT 分指（修复前的触发组合）。
    env_a = _uploader_env(root_a, storage_url, token_file) | {"DATAWATCH_EDGE_DB": str(database)}
    env_b = env_a | {"DATAWATCH_DATA_ROOT": str(root_b)}
    winner_out = (tmp_path / "split-a.log").open("wb")
    loser_out = (tmp_path / "split-b.log").open("wb")
    winner: subprocess.Popen | None = None
    try:
        winner = subprocess.Popen(
            [sys.executable, str(REPO_ROOT / "edge" / "uploader_main.py")],
            cwd=str(REPO_ROOT),
            env=env_a,
            stdout=winner_out,
            stderr=subprocess.STDOUT,
        )
        _wait_for_pending(database, 2)
        assert winner.poll() is None, "first instance exited unexpectedly"

        # 第一个实例运行中的未过期 live 租约。
        _force_live_lease(database, 2, "A-live-owner")
        before = _snapshot(database)
        assert before[1]["state"] == "inflight"
        assert before[1]["lease_owner"] == "A-live-owner"

        loser = subprocess.Popen(
            [sys.executable, str(REPO_ROOT / "edge" / "uploader_main.py")],
            cwd=str(REPO_ROOT),
            env=env_b,
            stdout=loser_out,
            stderr=subprocess.STDOUT,
        )
        try:
            loser_rc = loser.wait(timeout=30)
        except subprocess.TimeoutExpired:
            # 缺陷行为：第二个实例拿到了「自己那把」锁并长期存活，即同库不同锁。
            loser.kill()
            loser.wait(timeout=15)
            pytest.fail(
                "a second uploader instance stayed alive on the same outbox "
                "(lock path is not derived from edge_database_path())"
            )
        assert loser_rc != 0, "a second instance must not survive on the same outbox"
        loser_out.flush()
        loser_log = (tmp_path / "split-b.log").read_bytes()
        assert b"another uploader instance is running" in loser_log
        # 败方没有发起任何 Edge Storage 调用（无 recover / 无 HTTP 命令）。
        assert b"Recovered" not in loser_log

        assert winner.poll() is None, "first instance must keep running"
        after = _snapshot(database)
        assert after == before
        assert after[1]["state"] == "inflight"
        assert after[1]["lease_owner"] == "A-live-owner"
        # 锁只有一个，且与 outbox 库同源：败方的 root 下不得出现第二把锁。
        assert (database.parent / "uploader.lock").exists()
        assert not (root_b / "edge" / "uploader.lock").exists()
    finally:
        if winner is not None and winner.poll() is None:
            winner.terminate()
            try:
                winner.wait(timeout=15)
            except subprocess.TimeoutExpired:  # pragma: no cover - defensive
                winner.kill()
                winner.wait(timeout=15)
        winner_out.close()
        loser_out.close()
        server.shutdown()
        server.server_close()
        engine.close()
        thread.join(timeout=5)
