from __future__ import annotations

from cli.permissions import CommandPolicy, build_capabilities, is_command_allowed


def test_auth_marks_do_not_grant_permission_code_policy() -> None:
    policy = CommandPolicy(
        "menus update",
        authenticated=True,
        permissions_any=frozenset({"system:menu:update"}),
    )
    user_info = {
        "userName": "operator",
        "buttons": ["edit"],
        "permissions": [],
    }

    assert is_command_allowed(policy, user_info) is False

    user_info["permissions"] = ["system:menu:update"]
    assert is_command_allowed(policy, user_info) is True


def test_capabilities_reports_allowed_and_denied_commands() -> None:
    policies = (
        CommandPolicy("public"),
        CommandPolicy(
            "monitor",
            authenticated=True,
            permissions_any=frozenset({"device_monitor:monitor:view"}),
        ),
        CommandPolicy(
            "admin-only",
            authenticated=True,
            roles_any=frozenset({"R_ADMIN"}),
        ),
    )
    user_info = {
        "userName": "viewer",
        "roles": ["R_USER"],
        "permissions": ["device_monitor:monitor:view"],
    }

    result = build_capabilities(user_info, policies)

    assert result["availableCommands"] == ["public", "monitor"]
    assert result["deniedCommands"][0]["command"] == "admin-only"
