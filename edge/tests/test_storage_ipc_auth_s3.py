"""S3: Edge Storage local HTTP IPC must not be usable without a credential.

Reproduces the unauthenticated write path (a bare HTTP client with no token
mutating outbox / config state) and then pins the mitigation:
per-role tokens from a deployment-local file, a per-role command allowlist,
and a loopback-only bind guard on the real entry point.
"""
from __future__ import annotations

import base64
import ipaddress
import os
import socket
import sqlite3
import subprocess
import sys
import threading
import time
from pathlib import Path

import httpx
import pytest

from edge.app.config_snapshot import snapshot_checksum
from edge.app.outbox import OutboxCompletion
from edge.app.storage_client import (
    EdgeStorageClient,
    StorageUnauthorized,
    StorageUnavailable,
    resolve_client_role,
)
from edge.app.storage_service import (
    IPC_ROLE_ACTIONS,
    IPC_TOKEN_HEADER,
    IpcAuthenticator,
    StorageEngine,
    StorageHttpServer,
    ensure_token_file,
    read_role_tokens,
)


REPO_ROOT = Path(__file__).resolve().parents[2]


def _snapshot(revision: int = 1) -> dict:
    devices = [
        {
            "device_code": "QC100",
            "device_pk": 1,
            "device_type": "qc",
            "protocol": "s7",
            "endpoint": "127.0.0.1:102",
            "scan_rate_ms": 100,
            "config_version": revision,
            "assignment_epoch": 1,
            "enabled": True,
            "variables": [
                {
                    "variable_id": "1",
                    "address": "DB1.DBD0",
                    "data_type": "REAL",
                    "scan_rate_ms": 100,
                    "is_storage": True,
                    "var_seq": 1,
                    "group_code": "g001",
                    "group_config_version": revision,
                }
            ],
        }
    ]
    return {
        "configRevision": revision,
        "checksum": snapshot_checksum(devices),
        "devices": devices,
    }


def _frame(batch_id: str, ts: int = 1721800000000) -> dict:
    return {
        "batch_id": batch_id,
        "device_code": "QC100",
        "device_type": "qc",
        "group_code": "g001",
        "scan_rate_ms": 100,
        "ts": ts,
        "config_version": 1,
        "values": {"1": "10"},
        "qualities": {"1": 192},
        "latest_fields": {"1": {"v": "10", "t": ts, "q": 192}},
    }


class _Ipc:
    """Real StorageEngine + StorageHttpServer on loopback, temp DB and token file."""

    def __init__(self, tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
        self.tmp_path = tmp_path
        self.database = tmp_path / "edge.db"
        self.token_file = tmp_path / "ipc" / "storage-ipc-tokens.json"
        # Guard: the IPC secret and the database of every S3 test must live under
        # tmp_path. This host has a real Edge installation in
        # C:\ProgramData\DataWatch Edge, which these tests must never touch.
        assert self.token_file.is_relative_to(tmp_path)
        assert self.database.is_relative_to(tmp_path)
        assert "programdata" not in str(self.token_file).lower()
        # Keep every test off the real deployment token file.
        monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN_FILE", str(self.token_file))
        monkeypatch.delenv("DATAWATCH_EDGE_STORAGE_TOKEN", raising=False)
        monkeypatch.delenv("DATAWATCH_EDGE_STORAGE_ROLE", raising=False)

        self.engine = StorageEngine(self.database, max_queue=256)
        self.engine.start()
        self.server = StorageHttpServer(
            ("127.0.0.1", 0), self.engine, IpcAuthenticator.from_file(self.token_file)
        )
        self.base_url = f"http://127.0.0.1:{self.server.server_port}"
        self._thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self._thread.start()
        self.tokens = read_role_tokens(self.token_file)
        self.raw = httpx.Client(base_url=self.base_url, timeout=10.0)

    # -- actors -----------------------------------------------------------
    def unauthenticated(self, action: str, payload: dict | None = None) -> httpx.Response:
        """A hostile local process: no credential header of any kind."""
        return self.raw.post("/v1/command", json={"action": action, "payload": payload or {}})

    def as_role(self, role: str, action: str, payload: dict | None = None) -> httpx.Response:
        return self.raw.post(
            "/v1/command",
            json={"action": action, "payload": payload or {}},
            headers={IPC_TOKEN_HEADER: self.tokens[role]},
        )

    def legit(self, role: str) -> EdgeStorageClient:
        return EdgeStorageClient(self.base_url, role=role, token=self.tokens[role])

    # -- observation ------------------------------------------------------
    def states(self) -> dict[str, str]:
        connection = sqlite3.connect(f"file:{self.database.as_posix()}?mode=ro", uri=True)
        try:
            return {
                str(row[0]): f"{row[1]}(lease={row[2]})"
                for row in connection.execute(
                    "SELECT batch_id, state, lease_owner FROM outbox ORDER BY local_seq"
                ).fetchall()
            }
        finally:
            connection.close()

    def revision(self) -> int:
        connection = sqlite3.connect(f"file:{self.database.as_posix()}?mode=ro", uri=True)
        try:
            row = connection.execute(
                "SELECT config_revision FROM edge_config_meta WHERE singleton=1"
            ).fetchone()
            return int(row[0]) if row else -1
        finally:
            connection.close()

    def close(self) -> None:
        self.raw.close()
        self.server.shutdown()
        self.server.server_close()
        self.engine.close()
        self._thread.join(timeout=5)


@pytest.fixture
def ipc(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    harness = _Ipc(tmp_path, monkeypatch)
    try:
        yield harness
    finally:
        harness.close()


# ---------------------------------------------------------------------------
# 1. The reproduction: an unauthenticated process must change nothing.
# ---------------------------------------------------------------------------

def test_unauthenticated_process_cannot_mutate_outbox_config_or_session(ipc: _Ipc) -> None:
    """No token -> 401 on every mutating action, and no state change at all."""
    for action, payload in (
        ("config.apply", {"snapshot": _snapshot(999)}),
        ("frame.commit", _frame("forged")),
        ("latest.write", {"device_code": "QC100", "group_code": "g001", "fields": {}}),
        ("outbox.recover", {"lease_owner": "attacker"}),
        ("outbox.claim", {"target_seq": 10**9, "lease_owner": "attacker", "lease_ms": 60000,
                          "max_items": 10, "max_bytes": 1024 * 1024}),
        ("outbox.complete", {"lease_owner": "attacker", "completions": []}),
        ("outbox.retry", {"lease_owner": "attacker", "records": []}),
        ("session.prepare", {}),
        ("session.complete", {"old_session_id": "a", "new_session_id": "b"}),
        ("maintenance", {"retention_days": 7}),
        ("device.status", {"device_code": "QC100", "status": 1}),
    ):
        response = ipc.unauthenticated(action, payload)
        assert response.status_code == 401, (action, response.status_code, response.text)
        assert response.json()["ok"] is False
        assert "token" in response.json()["error"]

    outbox_row = sqlite3.connect(f"file:{ipc.database.as_posix()}?mode=ro", uri=True).execute(
        "SELECT COUNT(*) FROM outbox"
    ).fetchone()
    assert outbox_row[0] == 0, "unauthenticated frame.commit wrote an outbox row"
    assert ipc.revision() == -1, "unauthenticated config.apply changed the config revision"


def test_attacker_cannot_hide_behind_an_unknown_or_empty_token(ipc: _Ipc) -> None:
    for token in ("", "0" * 64, "not-a-real-token", "Bearer " + "0" * 64):
        response = ipc.raw.post(
            "/v1/command",
            json={"action": "outbox.complete", "payload": {"lease_owner": "attacker",
                                                          "completions": []}},
            headers={IPC_TOKEN_HEADER: token},
        )
        assert response.status_code == 401, (token, response.status_code)


# ---------------------------------------------------------------------------
# 2. Unconfirmed data cannot be completed by an unauthorised caller.
# ---------------------------------------------------------------------------

def test_unconfirmed_records_cannot_be_completed_without_the_uploader_role(ipc: _Ipc) -> None:
    """Acceptance: 未确认数据不会被未授权 complete."""
    agent = ipc.legit("agent")
    collector = ipc.legit("collector")
    uploader = ipc.legit("uploader")
    try:
        assert agent.apply(_snapshot(1)) == 1
        collector.commit_frame(**_frame("real-frame"))
        claimed = uploader.claim_batch(
            target_seq=uploader.snapshot_target_seq(),
            lease_owner="owner-A",
            lease_ms=60_000,
            max_items=10,
            max_bytes=1024 * 1024,
        )
        assert len(claimed) == 1
        assert ipc.states() == {"real-frame": "inflight(lease=owner-A)"}
        record = {
            "batch_id": claimed[0].batch_id,
            "upload_session_id": claimed[0].upload_session_id,
            "local_seq": claimed[0].local_seq,
            "assignment_epoch": claimed[0].assignment_epoch,
            "payload_b64": base64.b64encode(claimed[0].payload).decode(),
            "attempt_count": claimed[0].attempt_count,
        }
        complete = {
            "lease_owner": "attacker",
            "completions": [{"record": record, "state": "sent", "error": None}],
        }

        # No credential at all.
        assert ipc.unauthenticated("outbox.complete", complete).status_code == 401
        # A real but differently scoped credential.
        assert ipc.as_role("collector", "outbox.complete", complete).status_code == 403
        assert ipc.as_role("agent", "outbox.complete", complete).status_code == 403
        # The lease-stealing chain that made the unauthenticated repro succeed.
        assert ipc.unauthenticated("outbox.recover", {"lease_owner": "attacker"}).status_code == 401
        assert ipc.as_role("collector", "outbox.claim", {
            "target_seq": 10**9, "lease_owner": "attacker", "lease_ms": 60000,
            "max_items": 10, "max_bytes": 1024 * 1024,
        }).status_code == 403

        assert ipc.states() == {"real-frame": "inflight(lease=owner-A)"}

        # The legitimate Uploader still can, and the record then really is terminal.
        uploader.complete_batch([OutboxCompletion(claimed[0], "sent")], "owner-A")
        assert ipc.states() == {"real-frame": "sent(lease=None)"}
    finally:
        agent.close()
        collector.close()
        uploader.close()


# ---------------------------------------------------------------------------
# 3. Each legitimate role works, and only inside its own command set.
# ---------------------------------------------------------------------------

def test_collector_role_can_run_its_own_commands_only(ipc: _Ipc) -> None:
    agent = ipc.legit("agent")
    collector = ipc.legit("collector")
    try:
        agent.apply(_snapshot(1))
        assert collector.commit_frame(**_frame("collector-frame")).local_seq == 1
        collector.write_local_latest("QC100", "g001", {"1": {"v": "10", "t": 1, "q": 192}})
        assert collector.snapshot_target_seq() == 1
        collector.update_device_status("QC100", 1, "connected")
        collector.refresh_schedule(1_000)
    finally:
        agent.close()
        collector.close()

    for denied in ("outbox.complete", "outbox.claim", "config.apply", "maintenance", "session.prepare"):
        response = ipc.as_role("collector", denied, {})
        assert response.status_code == 403, (denied, response.status_code, response.text)
        assert "not permitted" in response.json()["error"]


def test_uploader_role_can_run_its_own_commands_only(ipc: _Ipc) -> None:
    agent = ipc.legit("agent")
    uploader = ipc.legit("uploader")
    collector = ipc.legit("collector")
    try:
        agent.apply(_snapshot(1))
        collector.commit_frame(**_frame("u1", ts=1721800000001))
        collector.commit_frame(**_frame("u2", ts=1721800000002))
        assert uploader.upload_interval_ms() == 100
        assert uploader.snapshot_target_seq() == 2
        assert uploader.recover_inflight("owner-A") == 0
        claimed = uploader.claim_batch(
            target_seq=2, lease_owner="owner-A", lease_ms=60_000,
            max_items=10, max_bytes=1024 * 1024,
        )
        assert [row.local_seq for row in claimed] == [1, 2]
        uploader.mark_sent(claimed[0], "owner-A")
        uploader.mark_retry(
            claimed[1], "owner-A", error="boom", http_status=503, delay_ms=0
        )
        assert uploader.status()["counts"] == {"sent": 1, "retry_wait": 1}

        assert uploader.pending_session_switch() is None
        state = uploader.prepare_session_switch()
        uploader.complete_session_switch(state)
        assert uploader.current_session_id() == state.new_session_id

        assert uploader.maintenance(7, 60_000)["deleted_terminal"] == 0
        # The session switch re-queues unconfirmed rows; none of them went terminal.
        # Backoff survives the switch (Edge 2/15), so the retry_wait row stays retry_wait.
        assert uploader.status()["counts"] == {"sent": 1, "retry_wait": 1}
    finally:
        agent.close()
        uploader.close()
        collector.close()

    for denied in ("config.apply", "frame.commit", "latest.write", "device.status"):
        response = ipc.as_role("uploader", denied, {})
        assert response.status_code == 403, (denied, response.status_code, response.text)


def test_agent_role_can_run_its_own_commands_only(ipc: _Ipc) -> None:
    agent = ipc.legit("agent")
    try:
        assert agent.revision() == 0
        assert agent.apply(_snapshot(4)) == 4
        assert agent.revision() == 4
        agent.refresh_schedule(2_000)
        assert agent.status()["writer_queue_depth"] == 0
    finally:
        agent.close()

    for denied in ("frame.commit", "latest.write", "outbox.complete", "outbox.claim",
                   "maintenance", "session.prepare", "device.status"):
        response = ipc.as_role("agent", denied, {})
        assert response.status_code == 403, (denied, response.status_code, response.text)


def test_role_allowlists_only_cover_actions_the_server_can_dispatch(ipc: _Ipc) -> None:
    """Every allowlisted action must be accepted by the dispatcher (no typos)."""
    for role, actions in IPC_ROLE_ACTIONS.items():
        assert actions, role
        for action in actions:
            response = ipc.as_role(role, action, {})
            assert response.status_code != 401, (role, action)
            # Argument errors are fine; an unrecognised action means a typo.
            assert "unknown storage action" not in response.text, (role, action)


# ---------------------------------------------------------------------------
# 4. Liveness stays open; nothing else does.
# ---------------------------------------------------------------------------

def test_healthz_is_reachable_without_a_token_and_leaks_no_state(ipc: _Ipc) -> None:
    response = ipc.raw.get("/healthz")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}

    status = ipc.raw.get("/v1/status")
    assert status.status_code == 401


# ---------------------------------------------------------------------------
# 5. Token provisioning and role resolution.
# ---------------------------------------------------------------------------

def test_token_file_is_provisioned_with_one_token_per_role(tmp_path: Path) -> None:
    path = ensure_token_file(tmp_path / "ipc" / "storage-ipc-tokens.json")
    tokens = read_role_tokens(path)
    assert set(tokens) == set(IPC_ROLE_ACTIONS)
    assert len(set(tokens.values())) == len(tokens)
    assert all(len(value) == 64 for value in tokens.values())
    # Re-reading must not rotate the deployment credential.
    assert read_role_tokens(ensure_token_file(path)) == tokens


def test_client_role_comes_from_env_then_entry_point(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_ROLE", "Uploader")
    assert resolve_client_role() == "uploader"
    monkeypatch.delenv("DATAWATCH_EDGE_STORAGE_ROLE")

    def role_for(name: str) -> str | None:
        monkeypatch.setattr(sys, "argv", [name])
        monkeypatch.setattr(sys.modules["__main__"], "__file__", name, raising=False)
        return resolve_client_role()

    assert role_for(str(REPO_ROOT / "edge" / "uploader_main.py")) == "uploader"
    assert role_for(str(REPO_ROOT / "edge" / "collector_main.py")) == "collector"
    assert role_for("data-watch-uploader.exe") == "uploader"
    assert role_for("data-watch-edge-collector.exe") == "collector"
    # No implicit fallback: an unidentifiable caller must not get a role at all.
    assert role_for("whatever-else.exe") is None
    assert role_for("pytest.exe") is None


def test_unidentifiable_client_sends_no_token_and_is_rejected(ipc: _Ipc) -> None:
    """A caller with no role has no credential, so the server must refuse it."""
    anonymous = EdgeStorageClient(ipc.base_url, role=None, token=None)
    try:
        assert anonymous.role is None
        assert anonymous._token is None
        with pytest.raises(Exception):
            anonymous.status()
    finally:
        anonymous.close()


# ---------------------------------------------------------------------------
# 6. The real entry point refuses to expose the IPC endpoint on all NICs.
# ---------------------------------------------------------------------------

def test_non_loopback_bind_is_refused_by_the_real_entry_point(tmp_path: Path) -> None:
    env = {
        key: value for key, value in os.environ.items() if not key.startswith("DATAWATCH_")
    }
    env.update(
        {
            "PYTHONPATH": str(REPO_ROOT),
            "DATAWATCH_DATA_ROOT": str(tmp_path),
            "DATAWATCH_EDGE_DB": str(tmp_path / "edge" / "edge.db"),
            "DATAWATCH_EDGE_STORAGE_PORT": "0",
            "DATAWATCH_EDGE_STORAGE_TOKEN_FILE": str(tmp_path / "ipc" / "tokens.json"),
        }
    )
    result = subprocess.run(
        [sys.executable, str(REPO_ROOT / "edge" / "storage_main.py"),
         "--host", "0.0.0.0", "--port", "0"],
        cwd=str(REPO_ROOT),
        env=env,
        capture_output=True,
        text=True,
        timeout=60,
    )
    # Every path the child could write must be inside tmp_path, never a real
    # installation (C:\ProgramData\DataWatch Edge).
    for key in ("DATAWATCH_DATA_ROOT", "DATAWATCH_EDGE_DB", "DATAWATCH_EDGE_STORAGE_TOKEN_FILE"):
        assert "programdata" not in env[key].lower(), key
        assert Path(env[key]).is_relative_to(tmp_path), key
    assert result.returncode == 2, (result.returncode, result.stdout, result.stderr)
    assert "Refusing to bind Edge Storage IPC to non-loopback address" in (
        result.stdout + result.stderr
    )
    # Refused before touching any database or token file.
    assert not (tmp_path / "edge" / "edge.db").exists()


def test_loopback_is_the_default_and_non_loopback_needs_an_explicit_flag(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.delenv("DATAWATCH_EDGE_STORAGE_ALLOW_NON_LOOPBACK", raising=False)
    from edge.storage_main import _is_loopback, _parser

    assert _parser().parse_args([]).host == "127.0.0.1"
    assert _parser().parse_args([]).allow_non_loopback is False
    assert _parser().parse_args(["--host", "0.0.0.0"]).allow_non_loopback is False
    assert _parser().parse_args(["--host", "0.0.0.0", "--allow-non-loopback"]).allow_non_loopback
    # P1-A：空 host 也被视为非回环，因此只有显式授权（唯一开关）才能放行。
    assert _parser().parse_args(["--host", "", "--allow-non-loopback"]).allow_non_loopback
    assert _is_loopback("") is False

    for host in ("127.0.0.1", "127.0.0.5", "::1", "localhost"):
        assert _is_loopback(host) is True, host
    # P1-A：空串/纯空白不是回环。``socket.bind(("", port))`` 语义为 INADDR_ANY，
    # 即监听全部网卡；本用例此前把 "" 断言成回环，等于把缺陷固化成了期望。
    for host in ("", " ", "   ", "0.0.0.0", "::", "192.168.1.10", "example.com"):
        assert _is_loopback(host) is False, host
    # P2：``localhost.`` 是 ``localhost`` 的 DNS 根形式，本机 AF_INET 解析器把它解析到
    # 127.0.0.1（``socket.bind(("localhost.", 0))`` 实测绑到 127.0.0.1 且非回环地址不可达），
    # 因此必须与 ``localhost`` 一样被接受；此前它被判非回环 → rc=2，属**过度拦截**，
    # 白白牺牲一种本可用的配置。``127.0.0.2`` 代表 127.0.0.0/8 全段。
    for host in ("localhost.", "LOCALHOST.", "  LocalHost.  ", "127.0.0.2"):
        assert _is_loopback(host) is True, host
    # 反向：这些写法**不得**因为"看着像回环"就被接受。本机 AF_INET 解析器拒绝它们
    # （``getaddrinfo`` 报 gaierror / ``bind`` 报 gaierror），放行只会把清晰的 rc=2 换成
    # 绑定期崩溃；尤其 ``127.0.0.1.``（IP 字面量带尾点）与 ``localhost.`` 不同，解析失败。
    for host in ("127.1", "[::1]", "127.0.0.1.", "localhost..", "."):
        assert _is_loopback(host) is False, host


def test_empty_host_is_refused_by_the_real_entry_point_and_binds_no_nic(
    tmp_path: Path,
) -> None:
    """P1-A 真实入口回归：``--host ""`` 必须被拒，且不得监听 0.0.0.0。"""
    env = {
        key: value for key, value in os.environ.items() if not key.startswith("DATAWATCH_")
    }
    env.update(
        {
            "PYTHONPATH": str(REPO_ROOT),
            "DATAWATCH_DATA_ROOT": str(tmp_path),
            "DATAWATCH_EDGE_DB": str(tmp_path / "edge" / "edge.db"),
            "DATAWATCH_EDGE_STORAGE_TOKEN_FILE": str(tmp_path / "ipc" / "tokens.json"),
        }
    )
    # A concrete port (not 0) so the "nothing is listening" assertion below is meaningful.
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = int(probe.getsockname()[1])
    result = subprocess.run(
        [sys.executable, str(REPO_ROOT / "edge" / "storage_main.py"),
         "--host", "", "--port", str(port)],
        cwd=str(REPO_ROOT),
        env=env,
        capture_output=True,
        text=True,
        timeout=60,
    )
    for key in ("DATAWATCH_DATA_ROOT", "DATAWATCH_EDGE_DB", "DATAWATCH_EDGE_STORAGE_TOKEN_FILE"):
        assert "programdata" not in env[key].lower(), key
        assert Path(env[key]).is_relative_to(tmp_path), key
    assert result.returncode == 2, (result.returncode, result.stdout, result.stderr)
    assert "Refusing to bind Edge Storage IPC to non-loopback address" in (
        result.stdout + result.stderr
    )
    assert "listening" not in (result.stdout + result.stderr).lower()
    # Refused before touching any database or token file.
    assert not (tmp_path / "edge" / "edge.db").exists()
    # The guard fires before the socket is created: binding every interface on that port
    # still succeeds, i.e. the refused process is not listening on 0.0.0.0.
    with socket.socket() as sock:
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        sock.bind(("0.0.0.0", port))


def _non_loopback_ipv4_addresses() -> list[str]:
    """本机自己的非回环 IPv4 地址，用于验证「从非回环地址仍不可达」。"""
    try:
        _hostname, _aliases, raw = socket.gethostbyname_ex(socket.gethostname())
    except OSError:
        return []
    addresses: list[str] = []
    for address in raw:
        try:
            parsed = ipaddress.ip_address(address)
        except ValueError:
            continue
        if parsed.version == 4 and not parsed.is_loopback and str(parsed) not in addresses:
            addresses.append(str(parsed))
    return addresses


@pytest.mark.parametrize("host", ["::1", "0:0:0:0:0:0:0:1"])
def test_ipv6_loopback_fails_fast_instead_of_hanging(tmp_path: Path, host: str) -> None:
    """``::1`` 是回环，但入口是 IPv4-only：必须快速失败并说明原因，而不是挂死。

    改前：守卫判它回环 → 放行 → ``StorageHttpServer`` 内 ``bind`` 抛 gaierror，而
    ``engine.start()`` 的非守护线程不退出 ⇒ 进程 15s 后仍存活、且没有任何监听。
    """
    env = {
        key: value for key, value in os.environ.items() if not key.startswith("DATAWATCH_")
    }
    env.update(
        {
            "PYTHONPATH": str(REPO_ROOT),
            "DATAWATCH_DATA_ROOT": str(tmp_path),
            "DATAWATCH_EDGE_DB": str(tmp_path / "edge" / "edge.db"),
            "DATAWATCH_EDGE_STORAGE_TOKEN_FILE": str(tmp_path / "ipc" / "tokens.json"),
        }
    )
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = int(probe.getsockname()[1])
    # 子进程必须自己退出；若挂死，subprocess.run 会抛 TimeoutExpired 让本用例失败。
    result = subprocess.run(
        [sys.executable, str(REPO_ROOT / "edge" / "storage_main.py"),
         "--host", host, "--port", str(port)],
        cwd=str(REPO_ROOT),
        env=env,
        capture_output=True,
        text=True,
        timeout=60,
    )
    output = result.stdout + result.stderr
    assert result.returncode == 2, (result.returncode, output)
    # 错误信息必须让运维一眼看出是「地址族不支持 / 入口 IPv4-only」，而不是安全拒绝、
    # 也不是「端口被占用」。
    assert "IPv4-only" in output, output
    assert "IPv6" in output, output
    assert "already in use" not in output, output
    assert "does not belong" not in output, output
    assert "Refusing to bind" not in output, output
    assert "listening" not in output.lower()
    # 拒绝发生在任何数据库写入之前（bind 早于 engine.start()）。
    assert not (tmp_path / "edge" / "edge.db").exists()


def _entry_env(tmp_path: Path) -> dict[str, str]:
    env = {
        key: value for key, value in os.environ.items() if not key.startswith("DATAWATCH_")
    }
    env.update(
        {
            "PYTHONPATH": str(REPO_ROOT),
            "DATAWATCH_DATA_ROOT": str(tmp_path),
            "DATAWATCH_EDGE_DB": str(tmp_path / "edge" / "edge.db"),
            "DATAWATCH_EDGE_STORAGE_TOKEN_FILE": str(tmp_path / "ipc" / "tokens.json"),
        }
    )
    return env


def test_port_already_in_use_fails_fast_and_is_classified(tmp_path: Path) -> None:
    """端口被占用：快速失败、归类为「端口被占用」，且不得挂死（改前是 traceback + 挂死）。"""
    with socket.socket() as holder:
        holder.bind(("127.0.0.1", 0))
        holder.listen(1)
        busy_port = int(holder.getsockname()[1])
        result = subprocess.run(
            [sys.executable, str(REPO_ROOT / "edge" / "storage_main.py"),
             "--host", "127.0.0.1", "--port", str(busy_port)],
            cwd=str(REPO_ROOT),
            env=_entry_env(tmp_path),
            capture_output=True,
            text=True,
            timeout=60,
        )
    output = result.stdout + result.stderr
    assert result.returncode == 2, (result.returncode, output)
    assert "already in use" in output, output
    # 必须与「地址族不支持」「地址不属于本机」区分开。
    assert "IPv4-only" not in output, output
    assert "does not belong" not in output, output
    assert "listening" not in output.lower()
    assert not (tmp_path / "edge" / "edge.db").exists()


def test_address_not_on_this_host_fails_fast_and_is_classified(tmp_path: Path) -> None:
    """``EADDRNOTAVAIL``：``--allow-non-loopback`` 放行安全守卫后仍应快速失败并归类。

    192.0.2.1 属 RFC 5737 TEST-NET-1，任何主机都不会把它分配给本机。
    """
    result = subprocess.run(
        [sys.executable, str(REPO_ROOT / "edge" / "storage_main.py"),
         "--host", "192.0.2.1", "--port", "0", "--allow-non-loopback"],
        cwd=str(REPO_ROOT),
        env=_entry_env(tmp_path),
        capture_output=True,
        text=True,
        timeout=60,
    )
    output = result.stdout + result.stderr
    assert result.returncode == 2, (result.returncode, output)
    assert "does not belong to this host" in output, output
    assert "IPv4-only" not in output, output
    assert "already in use" not in output, output
    assert "listening" not in output.lower()
    assert not (tmp_path / "edge" / "edge.db").exists()


def test_unavailable_token_file_is_not_reported_as_a_bind_failure(tmp_path: Path) -> None:
    """凭据文件不可用时必须报它自己：bind 失败的分类消息不得吞掉/误标它。

    令牌路径指向一个目录，于是 ``ensure_token_file`` 必然失败（Windows: PermissionError，
    POSIX: IsADirectoryError）。该失败发生在 bind **之前**，因此必须原样暴露。
    """
    token_path = tmp_path / "ipc" / "tokens.json"
    token_path.mkdir(parents=True)
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = int(probe.getsockname()[1])
    env = _entry_env(tmp_path)
    env["DATAWATCH_EDGE_STORAGE_TOKEN_FILE"] = str(token_path)
    result = subprocess.run(
        [sys.executable, str(REPO_ROOT / "edge" / "storage_main.py"),
         "--host", "127.0.0.1", "--port", str(port)],
        cwd=str(REPO_ROOT),
        env=env,
        capture_output=True,
        text=True,
        timeout=60,
    )
    output = result.stdout + result.stderr
    assert result.returncode != 0, (result.returncode, output)
    # 不得被误标成绑定失败，且错误信息要指回真实的失败点（traceback 里路径是 repr 形式）。
    assert "Cannot bind Edge Storage IPC" not in output, output
    assert repr(str(token_path)) in output, output


def test_localhost_trailing_dot_serves_on_loopback_only(tmp_path: Path) -> None:
    """P2：``--host localhost.`` 必须能正常启动并服务，且只服务回环地址。

    ``_is_loopback("localhost.")`` 此前返回 False，于是**一种本可用的配置**被判 rc=2
    （过度拦截）。这里既正向证明它服务正常，又反向证明没有把端点开给非回环地址。
    """
    env = {
        key: value for key, value in os.environ.items() if not key.startswith("DATAWATCH_")
    }
    env.update(
        {
            "PYTHONPATH": str(REPO_ROOT),
            "DATAWATCH_DATA_ROOT": str(tmp_path),
            "DATAWATCH_EDGE_DB": str(tmp_path / "edge" / "edge.db"),
            "DATAWATCH_EDGE_STORAGE_TOKEN_FILE": str(tmp_path / "ipc" / "tokens.json"),
        }
    )
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = int(probe.getsockname()[1])
    non_loopback = _non_loopback_ipv4_addresses()
    assert non_loopback, "需要至少一个非回环 IPv4 地址才能证明端点未开给网络"
    process = subprocess.Popen(
        [sys.executable, str(REPO_ROOT / "edge" / "storage_main.py"),
         "--host", "localhost.", "--port", str(port)],
        cwd=str(REPO_ROOT),
        env=env,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
    )
    try:
        deadline = time.monotonic() + 20
        health: int | None = None
        while health is None and time.monotonic() < deadline and process.poll() is None:
            try:
                with httpx.Client(timeout=1.0) as client:
                    health = client.get(f"http://127.0.0.1:{port}/healthz").status_code
            except httpx.HTTPError:
                time.sleep(0.1)
        assert health == 200, (health, process.poll())
        # 反向：非回环地址访问仍不可达。
        for address in non_loopback:
            with pytest.raises(OSError):
                socket.create_connection((address, port), timeout=1.0).close()
    finally:
        process.terminate()
        output = process.communicate(timeout=30)[0]
    assert "listening on http://localhost.:" in output, output


def test_empty_storage_host_env_default_is_not_treated_as_loopback(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """空 ``DATAWATCH_EDGE_STORAGE_HOST`` 同样不是回环（等同显式 ``--host ""``）。"""
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_HOST", "")
    from edge.storage_main import _is_loopback, _parser

    assert _parser().parse_args([]).host == ""
    assert _is_loopback(_parser().parse_args([]).host) is False


# ---------------------------------------------------------------------------
# 7. First-party call sites: every role must be able to do its real job.
# ---------------------------------------------------------------------------

def _seed_simulated_device(database: Path) -> None:
    now = 1721800000000
    with sqlite3.connect(database, timeout=10) as connection:
        connection.execute(
            "INSERT INTO edge_device(device_code, device_pk, device_type, protocol, endpoint,"
            " ip, port, rack, slot, scan_rate_ms, config_version, assignment_epoch, enabled)"
            " VALUES('QC100', 1, 'qc', 's7', '127.0.0.1:102', '127.0.0.1', 102, 1, 1, 100, 1, 1, 1)"
        )
        connection.execute(
            "INSERT INTO device_assignment(device_code, assignment_epoch, scan_rate_ms,"
            " config_revision, enabled, updated_at) VALUES('QC100', 1, 100, 1, 1, ?)",
            (now,),
        )
        connection.execute(
            "INSERT INTO edge_variable(variable_id, device_code, address, data_type, scan_rate_ms,"
            " is_storage, var_seq, group_code, group_config_version)"
            " VALUES('1', 'QC100', 'DB1.DBW0', 'INT', 100, 1, 1, 'g001', 1)"
        )


def test_collector_simulation_script_completes(ipc: _Ipc, tmp_path: Path) -> None:
    """P1-B 回归：仓库自带的第一方仿真脚本必须能完整跑完（rc=0）。

    该脚本以 collector 角色 ``frame.commit``，并为「批次大小」提示读取
    ``outbox.upload_interval``。后者属 **uploader** 角色（真实 Collector 调用点不含它），
    脚本用一次显式 uploader 角色的只读客户端取该值，而不是放宽 collector 白名单。
    """
    _seed_simulated_device(ipc.database)
    env = {
        key: value for key, value in os.environ.items() if not key.startswith("DATAWATCH_")
    }
    env.update(
        {
            "PYTHONPATH": str(REPO_ROOT),
            "DATAWATCH_DATA_ROOT": str(tmp_path),
            "DATAWATCH_EDGE_DB": str(ipc.database),
            "DATAWATCH_EDGE_STORAGE_URL": ipc.base_url,
            "DATAWATCH_EDGE_STORAGE_TOKEN_FILE": str(ipc.token_file),
        }
    )
    assert "programdata" not in env["DATAWATCH_EDGE_DB"].lower()
    result = subprocess.run(
        [
            sys.executable,
            str(REPO_ROOT / "scripts" / "simulate_edge_collector.py"),
            "--db", str(ipc.database),
            "--devices", "qc100",
            "--duration-seconds", "0.5",
            "--drain-wait-seconds", "0",
            "--status-interval-seconds", "1",
        ],
        cwd=str(REPO_ROOT),
        env=env,
        capture_output=True,
        text=True,
        timeout=180,
    )
    assert result.returncode == 0, (result.returncode, result.stdout, result.stderr)
    assert "QC100/g001" in result.stdout
    # 仿真确实写入了真实 outbox 行（不是空转返回 0）。
    assert "generated=" in result.stdout
    assert ipc.states(), "the simulation must have committed real outbox rows"


def test_client_distinguishes_rejected_credential_from_unreachable_service(
    ipc: _Ipc,
) -> None:
    """P2-D：401/403（服务在跑但凭据/权限错）不得与「服务不可达」混为一类。"""
    anonymous = EdgeStorageClient(ipc.base_url, role=None, token=None)
    try:
        with pytest.raises(StorageUnauthorized) as anonymous_error:
            anonymous.status()
    finally:
        anonymous.close()
    assert "401" in str(anonymous_error.value)

    collector = ipc.legit("collector")
    try:
        # config.apply 属 agent 角色，collector 调用它是角色越权 → 403。
        with pytest.raises(StorageUnauthorized) as denied:
            collector.apply({})
    finally:
        collector.close()
    assert "403" in str(denied.value)
    assert "unavailable" not in str(denied.value).lower()

    # 一个确定无人监听的端口：这才是「服务不可达」。
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        closed_port = int(probe.getsockname()[1])
    unreachable = EdgeStorageClient(
        base_url=f"http://127.0.0.1:{closed_port}", role="uploader", token="irrelevant"
    )
    try:
        with pytest.raises(StorageUnavailable) as down:
            unreachable.status()
    finally:
        unreachable.close()
    assert not isinstance(down.value, StorageUnauthorized)
    assert "unavailable" in str(down.value).lower()
