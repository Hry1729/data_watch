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


def _write_two_profiles(config_path) -> None:
    config_path.write_text(
        json.dumps(
            {
                "currentProfile": "development",
                "profiles": {
                    "development": {"baseUrl": "http://127.0.0.1:8000", "timeoutSeconds": 15},
                    "staging": {"baseUrl": "http://staging.local", "timeoutSeconds": 15},
                },
            }
        ),
        encoding="utf-8",
    )


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
    # 两个 profile：delete 的真实命令在只剩一个 profile 时会失败（审查 R2：dry-run 也一样）。
    _write_two_profiles(config_path)
    before = config_path.read_bytes()

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


# ── 审查 R2：dry-run 必须做与真实命令相同的校验（只是不落盘） ─────────────────


@pytest.mark.parametrize(
    "argv,needs_single_profile",
    [
        (["config", "set-url", "not-a-url"], False),
        (["config", "set-url", "ftp://x.local"], False),
        (["config", "profile", "create", "bad name!", "--url", "http://p.local"], False),
        (["config", "profile", "create", "staging", "--url", "http://p.local"], False),  # 已存在
        (["config", "profile", "create", "p3", "--url", "nope"], False),
        (["config", "profile", "update", "missing", "--url", "http://u.local"], False),
        (["config", "profile", "update", "staging"], False),  # 缺 --url/--timeout
        (["config", "profile", "update", "staging", "--url", "nope"], False),
        (["config", "profile", "delete", "missing"], False),
        (["config", "profile", "delete", "development"], True),  # 唯一的 profile
        (["config", "profile", "use", "missing"], False),
    ],
)
def test_dry_run_fails_exactly_like_real_command(argv, needs_single_profile, _isolate):
    config_path = _isolate / "cli-config.json"
    if not needs_single_profile:
        _write_two_profiles(config_path)
    before = config_path.read_bytes() if config_path.exists() else None

    real = runner.invoke(main_module.app, argv)
    assert real.exit_code != 0, real.stdout + real.stderr
    after_real = config_path.read_bytes() if config_path.exists() else None
    assert after_real == before

    dry = runner.invoke(main_module.app, ["--dry-run", *argv])
    assert dry.exit_code == real.exit_code, dry.stdout + dry.stderr
    assert dry.stdout == ""  # 没有成功预览
    real_error = json.loads(real.stderr)["error"]
    dry_error = json.loads(dry.stderr)["error"]
    assert (dry_error["type"], dry_error["message"]) == (real_error["type"], real_error["message"])
    after = config_path.read_bytes() if config_path.exists() else None
    assert after == before
    assert CALLS == []


def test_dry_run_preview_reports_normalized_result_without_saving(_isolate):
    config_path = _isolate / "cli-config.json"
    _write_two_profiles(config_path)
    before = config_path.read_bytes()

    result = runner.invoke(main_module.app, ["--dry-run", "config", "set-url", " http://new.local:9000/ "])
    assert result.exit_code == 0, result.stdout
    data = json.loads(result.stdout)["data"]
    assert data["newUrl"] == "http://new.local:9000"
    assert data["result"]["profiles"]["development"]["baseUrl"] == "http://new.local:9000"
    assert config_path.read_bytes() == before

    # URL 不变时不应预告清除凭据
    same = runner.invoke(main_module.app, ["--dry-run", "config", "set-url", "http://127.0.0.1:8000"])
    assert not any("凭据" in effect for effect in json.loads(same.stdout)["data"]["localEffects"])
