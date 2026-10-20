"""B017 回归：--dry-run 对不经 AuthorizedSession 的写路径（注销/登录/刷新、本地 profile、
自更新）必须只返回预览，网络写、凭据写、配置写、更新执行计数全部为零。"""

from __future__ import annotations

import json
from typing import Any

import pytest
from typer.testing import CliRunner

import cli.commands.update as update_module
import cli.main as main_module
from cli.credentials import StoredCredentials

runner = CliRunner()
CALLS: list[str] = []


class _CredStore:
    def __init__(self) -> None:
        pass

    def load(self, _profile: str, *, expected_base_url: str | None = None) -> StoredCredentials:
        return StoredCredentials(
            token="Bearer t",
            refresh_token="r",
            user_name="op",
            base_url=expected_base_url,
            source="keyring",
        )

    def save(self, *_a: Any, **_k: Any) -> None:
        CALLS.append("credentials.save")

    def clear(self, *_a: Any, **_k: Any) -> bool:
        CALLS.append("credentials.clear")
        return True


class _Client:
    def __init__(self, *_a: Any, **_k: Any) -> None:
        self.token = None

    def __enter__(self) -> "_Client":
        return self

    def __exit__(self, *_a: object) -> None:
        pass

    def close(self) -> None:
        pass

    def login(self, *_a: Any) -> dict[str, str]:
        CALLS.append("remote.login")
        return {"token": "x"}

    def logout(self) -> None:
        CALLS.append("remote.logout")

    def refresh(self) -> None:
        CALLS.append("remote.refresh")

    def get_user_info(self) -> dict[str, Any]:
        return {"userName": "op"}


class _Updater:
    def __init__(self, _client: Any) -> None:
        pass

    def check(self, *, channel: str = "stable") -> dict[str, Any]:
        return {"currentVersion": "1", "latestVersion": "2", "updateAvailable": True, "channel": channel}

    def download(self, **_k: Any) -> dict[str, Any]:
        CALLS.append("update.download")
        return {}

    def apply(self, **_k: Any) -> dict[str, Any]:
        CALLS.append("update.apply")
        return {}

    def rollback(self) -> dict[str, Any]:
        CALLS.append("update.rollback")
        return {}


@pytest.fixture(autouse=True)
def _isolate(tmp_path, monkeypatch):
    CALLS.clear()
    monkeypatch.setenv("DATAWATCH_CONFIG_FILE", str(tmp_path / "cli-config.json"))
    monkeypatch.setattr(main_module, "CredentialStore", _CredStore)
    monkeypatch.setattr(main_module, "BackendClient", _Client)
    monkeypatch.setattr("cli.client.BackendClient", _Client)
    monkeypatch.setattr(update_module, "UpdateManager", _Updater)
    yield tmp_path


@pytest.mark.parametrize(
    "argv",
    [
        ["auth", "logout"],
        ["auth", "refresh"],
        ["auth", "login", "-u", "op", "--password-stdin"],
        ["config", "set-url", "http://other.local:8000"],
        ["config", "profile", "create", "p2", "--url", "http://p2.local"],
        ["config", "profile", "update", "development", "--url", "http://u.local"],
        ["config", "profile", "delete", "development"],
        ["config", "profile", "use", "development"],
        ["update", "download"],
        ["update", "apply", "--yes"],
        ["update", "rollback", "--yes"],
    ],
)
def test_dry_run_has_zero_side_effects(argv, _isolate):
    config_path = _isolate / "cli-config.json"
    before = config_path.read_bytes() if config_path.exists() else None

    result = runner.invoke(main_module.app, ["--dry-run", *argv], input="secret\n")

    assert result.exit_code == 0, result.stdout + result.stderr
    payload = json.loads(result.stdout)
    assert payload["data"]["dryRun"] is True
    assert CALLS == []
    after = config_path.read_bytes() if config_path.exists() else None
    assert after == before


def test_logout_without_dry_run_still_executes(_isolate):
    result = runner.invoke(main_module.app, ["auth", "logout"])
    assert result.exit_code == 0, result.stdout + result.stderr
    assert CALLS == ["remote.logout", "credentials.clear"]
