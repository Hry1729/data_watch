"""S3-2 回归：Edge Storage IPC 令牌的轮换与**及时吊销**（无需重启 Storage）。

修复前 `IpcAuthenticator` 在进程启动时一次性缓存令牌，泄露的令牌只能靠重启 Storage 失效。
现在：服务端在令牌文件变化后（节流 ≤1s）重载并立即拒绝旧令牌；客户端在 401 时重读一次，
使长驻的 Collector/Uploader/Agent 无需重启即可吸收轮换。
"""

from __future__ import annotations

import sys
import time
from pathlib import Path

import httpx
import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT))

from edge.app.storage_client import (  # noqa: E402
    IPC_TOKEN_FILE_ENV,
    IPC_TOKEN_HEADER,
    EdgeStorageClient,
    read_role_tokens,
)
from edge.app.storage_service import IpcAuthenticator, ensure_token_file  # noqa: E402


def _token_file(tmp_path: Path) -> Path:
    return tmp_path / "storage-ipc-tokens.json"


def _wait_past_reload_throttle() -> None:
    time.sleep(IpcAuthenticator._RELOAD_INTERVAL_SECONDS + 0.1)


def test_rotate_regenerates_all_role_tokens(tmp_path):
    path = _token_file(tmp_path)
    before = read_role_tokens(ensure_token_file(path))
    after = read_role_tokens(ensure_token_file(path, rotate=True))
    assert set(before) == set(after), "轮换必须保持角色集合不变"
    assert all(before[role] != after[role] for role in before), "每个角色的令牌都应更换"
    assert before != after


def test_rotate_does_not_change_file_when_not_requested(tmp_path):
    path = _token_file(tmp_path)
    first = read_role_tokens(ensure_token_file(path))
    second = read_role_tokens(ensure_token_file(path))  # 默认不轮换
    assert first == second


def test_authenticator_revokes_old_token_without_restart(tmp_path):
    path = _token_file(tmp_path)
    auth = IpcAuthenticator.from_file(path)
    old = read_role_tokens(path)
    assert auth.role_for(old["collector"]) == "collector"

    ensure_token_file(path, rotate=True)
    new = read_role_tokens(path)
    _wait_past_reload_throttle()

    # 同一个 authenticator 实例（模拟运行中的 Storage），旧令牌必须已被吊销。
    assert auth.role_for(old["collector"]) is None
    assert auth.role_for(old["uploader"]) is None
    assert auth.role_for(old["agent"]) is None
    # 新令牌立即可用，且角色映射正确。
    assert auth.role_for(new["collector"]) == "collector"
    assert auth.role_for(new["agent"]) == "agent"


def test_authenticator_keeps_previous_tokens_when_file_is_corrupt(tmp_path):
    """重载失败时保留旧令牌：既不 fail-open，也不把所有人锁在外面。"""
    path = _token_file(tmp_path)
    auth = IpcAuthenticator.from_file(path)
    old = read_role_tokens(path)

    path.write_text("{ not json", encoding="utf-8")
    _wait_past_reload_throttle()
    assert auth.role_for(old["collector"]) == "collector"


class _StubHttp:
    """最小 httpx.Client 替身：按序返回预设响应并记录请求头。"""

    def __init__(self, responses):
        self._responses = list(responses)
        self.headers_seen: list[dict[str, str]] = []

    def post(self, path, content=None, headers=None):  # noqa: D401
        self.headers_seen.append(dict(headers or {}))
        return self._responses.pop(0)


def _response(status: int, content: bytes) -> httpx.Response:
    return httpx.Response(
        status,
        content=content,
        request=httpx.Request("POST", "http://127.0.0.1:7073/v1/command"),
    )


def test_client_reloads_rotated_token_after_401(tmp_path, monkeypatch):
    path = _token_file(tmp_path)
    monkeypatch.setenv(IPC_TOKEN_FILE_ENV, str(path))
    monkeypatch.delenv("DATAWATCH_EDGE_STORAGE_TOKEN", raising=False)
    old = read_role_tokens(ensure_token_file(path))

    stub = _StubHttp(
        [
            _response(401, b"{}"),
            _response(200, b'{"ok": true, "data": {"pong": 1}}'),
        ]
    )
    client = EdgeStorageClient(role="collector", client=stub)

    # 模拟运维在客户端运行期间轮换令牌（旧令牌随即被服务端吊销）。
    ensure_token_file(path, rotate=True)
    new = read_role_tokens(path)
    assert new["collector"] != old["collector"]

    assert client._command("outbox.snapshot") == {"pong": 1}
    assert len(stub.headers_seen) == 2, "401 之后应当重试一次"
    assert stub.headers_seen[0][IPC_TOKEN_HEADER] == old["collector"]
    assert stub.headers_seen[1][IPC_TOKEN_HEADER] == new["collector"]


def test_client_does_not_retry_role_overreach_403(tmp_path, monkeypatch):
    """403 是角色越权，重读令牌无意义，必须立即抛出而不是重试。"""
    path = _token_file(tmp_path)
    monkeypatch.setenv(IPC_TOKEN_FILE_ENV, str(path))
    ensure_token_file(path)

    stub = _StubHttp([_response(403, b"{}")])
    client = EdgeStorageClient(role="collector", client=stub)
    with pytest.raises(Exception) as excinfo:
        client._command("config.apply", {"snapshot": {}})
    assert "403" in str(excinfo.value)
    assert len(stub.headers_seen) == 1, "403 不得重试"


def test_client_with_injected_token_does_not_reload(tmp_path, monkeypatch):
    """显式注入的令牌不属于部署文件，不应被自动替换。"""
    path = _token_file(tmp_path)
    monkeypatch.setenv(IPC_TOKEN_FILE_ENV, str(path))
    ensure_token_file(path)

    stub = _StubHttp([_response(401, b"{}")])
    client = EdgeStorageClient(role="collector", token="injected-token", client=stub)
    with pytest.raises(Exception) as excinfo:
        client._command("outbox.snapshot")
    assert "401" in str(excinfo.value)
    assert stub.headers_seen[0][IPC_TOKEN_HEADER] == "injected-token"
