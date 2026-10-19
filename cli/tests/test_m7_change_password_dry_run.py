"""M7 回归：`auth change-password --dry-run` 不得输出明文密码，也不得报告"已修改"。

修复前：``_redact`` 只按完整键名匹配 password/token/...，``oldPassword`` / ``newPassword``
原样进入 dry-run 输出；``operation`` 不看返回值，固定返回 ``{"passwordChanged": true}``。
"""

from __future__ import annotations

import json
from typing import Any

from typer.testing import CliRunner

import cli.commands.common as common_module
import cli.main as main_module
import cli.session as session_module
from cli.config import RuntimeSettings
from cli.credentials import StoredCredentials
from cli.session import AuthorizedSession, _redact

runner = CliRunner()


class _Store:
    def load(self, _profile: str, *, expected_base_url: str | None = None) -> StoredCredentials:
        return StoredCredentials(token="Bearer token", base_url=expected_base_url)


class _DryState:
    credential_store = _Store()
    dry_run = True
    idempotency_key = None
    all_pages = False
    max_items = 10000

    def runtime(self) -> RuntimeSettings:
        return RuntimeSettings("development", "http://backend.local", 5)


class _NoNetworkClient:
    sent: list[Any] = []

    def __init__(self, *_args, **_kwargs) -> None:
        pass

    def close(self) -> None:
        pass

    def request(self, *args: Any, **kwargs: Any) -> Any:
        type(self).sent.append((args, kwargs))
        return {"sent": True}


class _DrySession:
    """CLI 侧的会话替身：写请求交给真实的 AuthorizedSession（dry-run 模式）处理。"""

    def __init__(self, _state) -> None:
        self._inner = AuthorizedSession(_DryState())

    def __enter__(self) -> "_DrySession":
        self._inner.__enter__()
        return self

    def __exit__(self, *args: object) -> None:
        self._inner.__exit__(*args)

    def authorize(self, _command: str) -> dict[str, Any]:
        return {"permissions": [], "roles": ["R_USER"]}

    def request(self, method: str, path: str, *, params=None, json_body=None):
        return self._inner.request(method, path, params=params, json_body=json_body)


def test_redact_covers_password_variants() -> None:
    redacted = _redact(
        {
            "oldPassword": "a",
            "newPassword": "b",
            "temporaryPassword": "c",
            "nested": [{"password": "d", "name": "keep"}],
        }
    )
    assert redacted == {
        "oldPassword": "***",
        "newPassword": "***",
        "temporaryPassword": "***",
        "nested": [{"password": "***", "name": "keep"}],
    }


def test_change_password_dry_run_is_preview_without_plaintext(monkeypatch) -> None:
    _NoNetworkClient.sent = []
    monkeypatch.setattr(session_module, "BackendClient", _NoNetworkClient)
    monkeypatch.setattr(common_module, "AuthorizedSession", _DrySession)

    result = runner.invoke(
        main_module.app,
        ["--dry-run", "auth", "change-password", "--password-stdin"],
        input="old-secret\nnew-secret\n",
    )

    assert result.exit_code == 0, result.output
    assert "old-secret" not in result.output
    assert "new-secret" not in result.output
    assert _NoNetworkClient.sent == []
    payload = json.loads(result.stdout)
    data = payload.get("data", payload)
    assert data.get("passwordChanged") is not True
    assert data["dryRun"] is True
    assert data["request"]["jsonBody"] == {"oldPassword": "***", "newPassword": "***"}
