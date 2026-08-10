from __future__ import annotations

import json
from typing import Any

from typer.testing import CliRunner

import cli.commands.common as common_module
import cli.main as main_module
from cli.permissions import get_command_policy, is_command_allowed


runner = CliRunner()


class FakeResolver:
    def resolve_catalog_device(self, identifier: str) -> dict[str, Any]:
        assert identifier == "PLC002"
        return {"id": 7, "deviceCode": identifier}


class FakeAuthorizedSession:
    requests: list[tuple[str, str, Any]] = []

    def __init__(self, _state) -> None:
        self.resolver = FakeResolver()

    def __enter__(self) -> "FakeAuthorizedSession":
        return self

    def __exit__(self, *_args: object) -> None:
        pass

    def authorize(self, _command: str) -> dict[str, Any]:
        return {"permissions": [], "roles": ["R_SUPER"]}

    def request(self, method: str, path: str, *, params=None, json_body=None):
        type(self).requests.append((method, path, json_body))
        if method == "GET" and path.endswith("/assignments"):
            return {"items": [{"deviceId": 3, "deviceCode": "PLC001"}]}
        return {"updated": True}


def _use_fake_session(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)


def test_node_assign_merges_existing_assignments(monkeypatch) -> None:
    _use_fake_session(monkeypatch)
    node_uuid = "11111111-1111-1111-1111-111111111111"

    result = runner.invoke(
        main_module.app,
        [
            "nodes",
            "assign",
            node_uuid,
            "--device",
            "PLC002",
            "--confirm",
            node_uuid,
        ],
    )

    assert result.exit_code == 0
    assert FakeAuthorizedSession.requests == [
        ("GET", f"/api/distributed/nodes/{node_uuid}/assignments", None),
        (
            "PUT",
            f"/api/distributed/nodes/{node_uuid}/assignments",
            {"deviceIds": [3, 7]},
        ),
    ]


def test_wrong_node_confirmation_never_sends_write(monkeypatch) -> None:
    _use_fake_session(monkeypatch)

    result = runner.invoke(
        main_module.app,
        ["nodes", "disable", "node-1", "--confirm", "wrong"],
    )

    assert result.exit_code == 2
    assert FakeAuthorizedSession.requests == []


def test_wrong_log_cleanup_confirmation_never_sends_write(monkeypatch) -> None:
    _use_fake_session(monkeypatch)

    result = runner.invoke(
        main_module.app,
        [
            "logs",
            "cleanup",
            "--before",
            "2026-08-01T00:00:00",
            "--confirm",
            "wrong",
        ],
    )

    assert result.exit_code == 2
    assert FakeAuthorizedSession.requests == []


def test_collector_config_set_uses_dedicated_write_contract(monkeypatch) -> None:
    _use_fake_session(monkeypatch)

    result = runner.invoke(
        main_module.app,
        ["modules", "collector-config", "set", "--stream-maxlen", "250000"],
    )

    assert result.exit_code == 0
    assert FakeAuthorizedSession.requests == [
        (
            "PUT",
            "/api/device-monitor/collector-config",
            {"streamMaxlen": 250000},
        )
    ]
    policy = get_command_policy("modules collector-config set")
    assert policy is not None
    assert policy.permissions_any == frozenset(
        {"device_monitor:collector_config:update"}
    )


def test_user_disable_uses_backend_status_value(monkeypatch) -> None:
    _use_fake_session(monkeypatch)

    result = runner.invoke(
        main_module.app,
        ["users", "disable", "9", "--confirm", "9"],
    )

    assert result.exit_code == 0
    assert FakeAuthorizedSession.requests == [
        ("PATCH", "/api/user/9/status", {"status": "2"})
    ]


def test_role_grant_set_loads_json_contract(tmp_path, monkeypatch) -> None:
    _use_fake_session(monkeypatch)
    payload_file = tmp_path / "grant.json"
    payload_file.write_text(
        json.dumps({"menuIds": [1], "permissionIds": [2], "dataScopes": []}),
        encoding="utf-8",
    )

    result = runner.invoke(
        main_module.app,
        [
            "roles",
            "grants",
            "set",
            "5",
            "--file",
            str(payload_file),
            "--confirm",
            "5",
        ],
    )

    assert result.exit_code == 0
    assert FakeAuthorizedSession.requests == [
        (
            "PUT",
            "/api/role/5/grant",
            {"menuIds": [1], "permissionIds": [2], "dataScopes": []},
        )
    ]


def test_change_password_stdin_does_not_echo_secrets(monkeypatch) -> None:
    _use_fake_session(monkeypatch)

    result = runner.invoke(
        main_module.app,
        ["auth", "change-password", "--password-stdin"],
        input="old-secret\nnew-secret\n",
    )

    assert result.exit_code == 0
    assert "old-secret" not in result.stdout
    assert "new-secret" not in result.stdout
    assert FakeAuthorizedSession.requests == [
        (
            "POST",
            "/api/user/password/change",
            {"oldPassword": "old-secret", "newPassword": "new-secret"},
        )
    ]


def test_node_and_system_policies_distinguish_three_levels() -> None:
    node_policy = get_command_policy("nodes assign")
    user_policy = get_command_policy("users list")
    assert node_policy is not None
    assert user_policy is not None

    regular = {
        "roles": ["R_USER"],
        "permissions": ["system:user:view"],
    }
    admin = {"roles": ["R_ADMIN"], "permissions": []}
    superuser = {"roles": ["R_SUPER"], "permissions": []}

    assert not is_command_allowed(node_policy, regular)
    assert is_command_allowed(node_policy, admin)
    assert is_command_allowed(node_policy, superuser)
    assert is_command_allowed(user_policy, regular)
    assert not is_command_allowed(user_policy, admin)
