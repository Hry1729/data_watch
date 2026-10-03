"""S3: the Agent's Edge Storage IPC client must prove its dedicated identity.

Runs the real Edge Storage IPC server in-process and drives it through the real
Agent client (`app.services.edge_storage_client`), which does not import the edge
package in production. Covered here:
  * status / config.revision / config.apply / config.refresh_schedule work when
    the Agent presents its own least-privilege IPC token;
  * the same calls are refused when the token is absent or unknown;
  * the credential used is the dedicated IPC token, never the Agent management
    service token.
"""
from __future__ import annotations

import json
import os
import socket
import sys
import threading
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
for _path in (REPO_ROOT, REPO_ROOT / "collector"):
    if str(_path) not in sys.path:
        sys.path.insert(0, str(_path))

from edge.app.config_snapshot import snapshot_checksum  # noqa: E402
from edge.app.storage_service import (  # noqa: E402
    StorageEngine,
    StorageHttpServer,
    ensure_token_file,
    read_role_tokens,
)

from app.services.edge_storage_client import (  # noqa: E402
    IPC_ROLE,
    IPC_TOKEN_HEADER,
    EdgeStorageClient,
    StorageUnauthorized,
    StorageUnavailable,
    ipc_token_file,
)


def _snapshot(revision: int) -> dict:
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


@pytest.fixture
def storage(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    """Real Edge Storage IPC server on loopback with an in-tmp token file."""
    token_file = tmp_path / "edge" / "storage-ipc-tokens.json"
    # Guard: the token file and edge DB must stay in tmp_path. This host has a
    # real installation in C:\ProgramData\DataWatch Edge that must not be touched.
    assert token_file.is_relative_to(tmp_path)
    assert "programdata" not in str(token_file).lower()
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN_FILE", str(token_file))
    monkeypatch.delenv("DATAWATCH_EDGE_STORAGE_TOKEN", raising=False)
    monkeypatch.setenv("DATAWATCH_EDGE_DB", str(tmp_path / "edge" / "edge.db"))

    engine = StorageEngine(tmp_path / "edge.db", max_queue=64)
    engine.start()
    server = StorageHttpServer(("127.0.0.1", 0), engine)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        yield {
            "url": f"http://127.0.0.1:{server.server_port}",
            "token_file": token_file,
        }
    finally:
        server.shutdown()
        server.server_close()
        engine.close()
        thread.join(timeout=5)


def test_agent_client_uses_its_own_dedicated_ipc_token(storage) -> None:
    ensure_token_file(storage["token_file"])
    tokens = read_role_tokens(storage["token_file"])
    service_token = os.environ.get("DATAWATCH_AGENT_SERVICE_TOKEN")
    assert service_token is None or tokens[IPC_ROLE] != service_token
    # The Agent must resolve the same deployment token file the edge side creates.
    assert ipc_token_file() == storage["token_file"]

    client = EdgeStorageClient(storage["url"], timeout_seconds=5.0)
    assert client._token == tokens[IPC_ROLE]
    assert client._token != service_token

    assert client.status()["counts"] == {}
    assert client.revision() == 0
    assert client.apply(_snapshot(4)) == 4
    assert client.revision() == 4
    client.refresh_schedule(1_000)


def test_agent_client_without_a_token_is_refused(storage) -> None:
    """No credential at all -> the server rejects instead of trusting loopback."""
    ensure_token_file(storage["token_file"])
    os.environ.pop("DATAWATCH_EDGE_STORAGE_TOKEN", None)
    # Point the client at a token file that holds no "agent" entry.
    empty = storage["token_file"].parent / "empty-tokens.json"
    empty.write_text(json.dumps({"version": 1, "tokens": {"uploader": "0" * 64}}), encoding="utf-8")
    previous = os.environ["DATAWATCH_EDGE_STORAGE_TOKEN_FILE"]
    os.environ["DATAWATCH_EDGE_STORAGE_TOKEN_FILE"] = str(empty)
    try:
        anonymous = EdgeStorageClient(storage["url"], timeout_seconds=5.0)
        assert anonymous._token is None
        # 断言强度不低于原先的 ``RuntimeError + match="Edge Storage unavailable"``：现在要求
        # **更具体的** ``StorageUnauthorized`` 类型与 HTTP 401，并明确它**不是**「不可用」。
        with pytest.raises(StorageUnauthorized, match=r"HTTP 401") as anonymous_error:
            anonymous.status()
        assert isinstance(anonymous_error.value, StorageUnavailable)
        assert "unavailable" not in str(anonymous_error.value)
    finally:
        os.environ["DATAWATCH_EDGE_STORAGE_TOKEN_FILE"] = previous


def test_agent_client_rejects_a_foreign_token(storage) -> None:
    ensure_token_file(storage["token_file"])
    os.environ["DATAWATCH_EDGE_STORAGE_TOKEN"] = "0" * 64
    try:
        impostor = EdgeStorageClient(storage["url"], timeout_seconds=5.0)
        assert impostor._token == "0" * 64
        # 同上：未知令牌也是 401，必须是可区分的 ``StorageUnauthorized`` 而非「不可用」。
        with pytest.raises(StorageUnauthorized, match=r"HTTP 401") as impostor_error:
            impostor.status()
        assert isinstance(impostor_error.value, StorageUnavailable)
        assert "unavailable" not in str(impostor_error.value)
    finally:
        os.environ.pop("DATAWATCH_EDGE_STORAGE_TOKEN", None)


def test_agent_client_separates_auth_failures_from_unreachable_endpoints(
    storage, monkeypatch: pytest.MonkeyPatch
) -> None:
    """P2-D（Agent 侧）：401/403 与「服务不可达」必须是可区分的结果。"""
    ensure_token_file(storage["token_file"])
    tokens = read_role_tokens(storage["token_file"])

    # 403：令牌有效，但该角色无权调用 status（collector 令牌调 agent 的动作）。
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN", tokens["collector"])
    denied = EdgeStorageClient(storage["url"], timeout_seconds=5.0)
    with pytest.raises(StorageUnauthorized, match=r"HTTP 403") as escalation:
        denied.status()
    assert isinstance(escalation.value, StorageUnavailable)
    assert "unavailable" not in str(escalation.value)

    # 不可达：一个确定无人监听的端口仍是「不可用」，但**不是**认证/授权失败。
    monkeypatch.delenv("DATAWATCH_EDGE_STORAGE_TOKEN", raising=False)
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        dead_port = int(probe.getsockname()[1])
    down = EdgeStorageClient(f"http://127.0.0.1:{dead_port}", timeout_seconds=1.0)
    with pytest.raises(StorageUnavailable) as unreachable:
        down.status()
    assert not isinstance(unreachable.value, StorageUnauthorized)
    assert "unavailable" in str(unreachable.value)


def test_agent_token_header_name_matches_the_server_contract(storage) -> None:
    assert IPC_TOKEN_HEADER == "X-DataWatch-Storage-Token"
    assert "agent" in read_role_tokens(ensure_token_file(storage["token_file"]))
