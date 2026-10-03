"""B5 真实入口验证（W1-E1b）：切换中崩溃 + 重启补完，帧身份与数据守恒。

与 ``test_uploader_b5_independent.py``（单元级）的区别：这里走**真实生产入口**
``edge/uploader_main.py`` 子进程 —— 真实单实例锁、真实 Edge Storage HTTP IPC
（``StorageEngine`` + ``StorageHttpServer``，进程内本地回环）、真实 SQLite outbox
文件、真实 HTTP 中心（本测试内的 fake，仅替代中心，不连现场栈）。

注入的故障：中心对 ``/ingest`` 返回「ack + commit_unknown」，随后在
``/ingest/session_switch`` 上**不再应答并保持挂起**，测试在此时把子进程
``kill()``（TerminateProcess，无清理机会）→ 精确复现「切换中崩溃」。

断言要点：
1. 崩溃前已被中心 ACK 的帧必须已是 ``sent``，且保留**旧会话身份**与**原 local_seq**
   （没有作为未确认行被迁移）；
2. 未确认帧（含响应截止后缺席的帧）必须仍在 outbox 里，被迁到新会话且 ``local_seq``
   连续 —— 不得用「清空 outbox / 跳过高水位之前数据」实现「不重传」；
3. 数据守恒：条数、``batch_id`` 集合、``payload`` 字节（sha256）三项都不变；
4. 真重启后：先补完 session 切换、再重放未确认帧，最终全部 ``sent``，帧身份不变。

安全：只使用 ``tmp_path`` 与 127.0.0.1 回环端口；``DATAWATCH_DATA_ROOT`` 被显式
覆盖到 ``tmp_path``（绝不落到 ``C:\\ProgramData\\DataWatch``）；不连现场栈。
"""

from __future__ import annotations

import base64
import hashlib
import json
import os
import sqlite3
import subprocess
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from uuid import uuid4

import pytest
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.serialization import (
    Encoding,
    NoEncryption,
    PrivateFormat,
)

from edge.app.outbox import OutboxStore
from edge.app.storage_service import StorageEngine, StorageHttpServer


REPO_ROOT = Path(__file__).resolve().parents[2]
ROW_COLUMNS = "batch_id, upload_session_id, local_seq, state, payload, lease_owner"


def _private_key_pem() -> bytes:
    return Ed25519PrivateKey.generate().private_bytes(
        Encoding.PEM, PrivateFormat.PKCS8, NoEncryption()
    )


def _ack(item: dict, status: str) -> dict:
    result = {
        "batch_id": item["batch_id"],
        "upload_session_id": item["upload_session_id"],
        "local_seq": item["local_seq"],
        "status": status,
    }
    if status in {"ack", "duplicate", "stale_config_warning"}:
        result["durability"] = "redis-aof"
    return result


class _CenterHandler(BaseHTTPRequestHandler):
    def _read_body(self) -> dict:
        length = int(self.headers.get("content-length") or 0)
        raw = self.rfile.read(length) if length else b"{}"
        return json.loads(raw) if raw else {}

    def _json(self, payload: dict, status: int = 200) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("content-type", "application/json")
        self.send_header("content-length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:  # pragma: no cover - 仅在 session 冲突时才走
        self._json({"active_session_id": None})

    def do_POST(self) -> None:
        server: "_Center" = self.server  # type: ignore[assignment]
        payload = self._read_body()
        if self.path == "/ingest/session_switch":
            server.switch_bodies.append(payload)
            server.switch_seen.set()
            if server.hang_on_switch:
                # 永不回应答：模拟「切换中崩溃」，由测试 kill 子进程。
                server.release.wait(120)
            self._json({"status": "switched"})
            return
        items = payload["items"]
        server.ingest_items.append([item["local_seq"] for item in items])
        self._json({"results": server.results_builder(items)})

    def log_message(self, *args) -> None:  # 静默
        return


class _Center(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, results_builder, *, hang_on_switch: bool) -> None:
        super().__init__(("127.0.0.1", 0), _CenterHandler)
        self.results_builder = results_builder
        self.hang_on_switch = hang_on_switch
        self.switch_seen = threading.Event()
        self.release = threading.Event()
        self.switch_bodies: list[dict] = []
        self.ingest_items: list[list[int]] = []

    @property
    def url(self) -> str:
        return f"http://127.0.0.1:{self.server_port}"


def _connect(database: Path) -> sqlite3.Connection:
    connection = sqlite3.connect(database, timeout=15)
    connection.row_factory = sqlite3.Row
    connection.execute("PRAGMA busy_timeout=15000")
    return connection


def _snapshot(database: Path) -> dict[str, dict]:
    with _connect(database) as connection:
        fetched = connection.execute(
            f"SELECT {ROW_COLUMNS} FROM outbox ORDER BY local_seq"
        ).fetchall()
    return {str(row["batch_id"]): dict(row) for row in fetched}


def _metadata(database: Path, key: str) -> str | None:
    with _connect(database) as connection:
        row = connection.execute(
            "SELECT value FROM metadata WHERE key=?", (key,)
        ).fetchone()
    return None if row is None else str(row["value"])


def _counts(database: Path) -> dict[str, int]:
    with _connect(database) as connection:
        fetched = connection.execute(
            "SELECT state, COUNT(*) AS count FROM outbox GROUP BY state"
        ).fetchall()
    return {str(row["state"]): int(row["count"]) for row in fetched}


def _seed(database: Path, count: int) -> dict[str, str]:
    store = OutboxStore(database)
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
    store.close()
    return fingerprints


def _uploader_env(
    data_root: Path, storage_url: str, token_file: Path, center_url: str
) -> dict[str, str]:
    env = {
        key: value for key, value in os.environ.items() if not key.startswith("DATAWATCH_")
    }
    env.update(
        {
            "PYTHONPATH": str(REPO_ROOT),
            # 显式覆盖，绝不落到真实安装 C:\ProgramData\DataWatch
            "DATAWATCH_DATA_ROOT": str(data_root),
            "DATAWATCH_EDGE_STORAGE_URL": storage_url,
            "DATAWATCH_EDGE_STORAGE_TOKEN_FILE": str(token_file),
            "DATAWATCH_CENTER_URL": center_url,
            "DATAWATCH_NODE_UUID": f"node-b5-{uuid4()}",
            "DATAWATCH_UPLOADER_PRIVATE_KEY_B64": base64.b64encode(
                _private_key_pem()
            ).decode("ascii"),
        }
    )
    return env


def _wait_for(predicate, *, timeout: float, message: str) -> None:
    deadline = time.monotonic() + timeout
    last = None
    while time.monotonic() < deadline:
        last = predicate()
        if last:
            return
        time.sleep(0.1)
    raise AssertionError(f"{message}; last={last!r}")


def test_real_entry_crash_during_session_switch_preserves_frame_identity(
    tmp_path: Path, monkeypatch
) -> None:
    database = tmp_path / "edge.db"
    token_file = tmp_path / "edge" / "storage-ipc-tokens.json"
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN_FILE", str(token_file))
    fingerprints = _seed(database, 3)
    order = list(fingerprints)
    old_session = _metadata(database, "upload_session_id")
    assert old_session

    # --- 中心（fake）：第一条 /ingest 返回 ack + commit_unknown 并截止 ---------
    def mixed_results(items: list[dict]) -> list[dict]:
        return [_ack(items[0], "ack"), _ack(items[1], "commit_unknown")]

    center = _Center(mixed_results, hang_on_switch=True)
    center_thread = threading.Thread(target=center.serve_forever, daemon=True)
    center_thread.start()

    engine = StorageEngine(database, max_queue=256)
    engine.start()
    server = StorageHttpServer(("127.0.0.1", 0), engine)
    storage_thread = threading.Thread(target=server.serve_forever, daemon=True)
    storage_thread.start()
    storage_url = f"http://127.0.0.1:{server.server_port}"

    log_path = tmp_path / "uploader.log"
    child: subprocess.Popen | None = None
    try:
        with log_path.open("wb") as log:
            child = subprocess.Popen(
                [sys.executable, str(REPO_ROOT / "edge" / "uploader_main.py")],
                cwd=str(REPO_ROOT),
                env=_uploader_env(tmp_path, storage_url, token_file, center.url),
                stdout=log,
                stderr=subprocess.STDOUT,
            )
            # 等它真的把 session_switch 打到中心（此时 outbox 已完成迁移），
            # 然后硬杀：进程没有任何机会完成切换或做清理。
            assert center.switch_seen.wait(120), log_path.read_text(
                encoding="utf-8", errors="replace"
            )[-4000:]
            child.kill()
            child.wait(timeout=60)

        rows = _snapshot(database)
        # 1) 已 ACK 的帧：sent + 旧会话身份 + 原 local_seq（未被迁移）。
        assert rows[order[0]]["state"] == "sent", rows
        assert rows[order[0]]["upload_session_id"] == old_session
        assert rows[order[0]]["local_seq"] == 1
        # 2) 未确认帧（含响应截止后缺席的第 3 条）仍在 outbox，迁入新会话，seq 连续。
        new_session = _metadata(database, "upload_session_id")
        assert new_session and new_session != old_session
        for batch_id in order[1:]:
            assert rows[batch_id]["state"] == "pending", rows
            assert rows[batch_id]["upload_session_id"] == new_session
        assert sorted(rows[batch_id]["local_seq"] for batch_id in order[1:]) == [1, 2]
        # 切换未完成（中心未确认）→ 重启必须补完，而不是丢弃。
        assert _metadata(database, "pending_session_switch_old_id") == old_session
        assert center.switch_bodies[0]["old_session_id"] == old_session
        assert center.switch_bodies[0]["new_session_id"] == new_session
        # 3) 数据守恒 + 帧身份。
        assert set(rows) == set(fingerprints)
        assert sum(_counts(database).values()) == 3
        assert _counts(database) == {"sent": 1, "pending": 2}
        assert {
            batch_id: hashlib.sha256(bytes(row["payload"])).hexdigest()
            for batch_id, row in rows.items()
        } == fingerprints

        # --- 重启：健康中心，先补完切换再重放未确认帧 --------------------------
        healthy = _Center(
            lambda items: [_ack(item, "ack") for item in items],
            hang_on_switch=False,
        )
        healthy_thread = threading.Thread(target=healthy.serve_forever, daemon=True)
        healthy_thread.start()
        try:
            with (tmp_path / "uploader-restart.log").open("wb") as log:
                child = subprocess.Popen(
                    [sys.executable, str(REPO_ROOT / "edge" / "uploader_main.py")],
                    cwd=str(REPO_ROOT),
                    env=_uploader_env(tmp_path, storage_url, token_file, healthy.url),
                    stdout=log,
                    stderr=subprocess.STDOUT,
                )
                _wait_for(
                    lambda: _counts(database) == {"sent": 3},
                    timeout=120,
                    message="restarted uploader did not reconcile the preserved backlog",
                )
                assert child.poll() is None
        finally:
            if child is not None and child.poll() is None:
                child.terminate()
                try:
                    child.wait(timeout=30)
                except subprocess.TimeoutExpired:  # pragma: no cover - defensive
                    child.kill()
                    child.wait(timeout=30)
            healthy.release.set()
            healthy.shutdown()
            healthy.server_close()
            healthy_thread.join(timeout=10)

        final_rows = _snapshot(database)
        assert _counts(database) == {"sent": 3}
        assert set(final_rows) == set(fingerprints)
        # sent 帧不被二次迁移：旧会话身份与原 local_seq 保持到最后一刻。
        assert final_rows[order[0]]["upload_session_id"] == old_session
        assert final_rows[order[0]]["local_seq"] == 1
        assert {
            batch_id: hashlib.sha256(bytes(row["payload"])).hexdigest()
            for batch_id, row in final_rows.items()
        } == fingerprints
        assert _metadata(database, "pending_session_switch_old_id") is None
    finally:
        if child is not None and child.poll() is None:
            child.kill()
            child.wait(timeout=30)
        center.release.set()
        center.shutdown()
        center.server_close()
        center_thread.join(timeout=10)
        server.shutdown()
        server.server_close()
        storage_thread.join(timeout=10)
        engine.close()
