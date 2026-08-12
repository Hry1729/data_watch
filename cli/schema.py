from __future__ import annotations

from typing import Any

import click

from cli.permissions import get_command_policy


def command_schemas(root_command: click.Command) -> list[dict[str, Any]]:
    schemas: list[dict[str, Any]] = []
    _walk(root_command, [], schemas)
    return schemas


def find_command_schema(root_command: click.Command, command: str) -> dict[str, Any] | None:
    normalized = " ".join(command.split())
    return next(
        (item for item in command_schemas(root_command) if item["command"] == normalized),
        None,
    )


def _walk(
    command: click.Command,
    path: list[str],
    output: list[dict[str, Any]],
) -> None:
    children = getattr(command, "commands", None)
    if isinstance(children, dict) and children:
        for name, child in sorted(children.items()):
            _walk(child, [*path, name], output)
        return
    if not path:
        return
    command_name = " ".join(path)
    policy = get_command_policy(command_name)
    output.append(
        {
            "command": command_name,
            "help": command.help or "",
            "parameters": [_parameter_schema(parameter) for parameter in command.params],
            "access": {
                "authenticated": bool(policy and policy.authenticated),
                "permissionsAny": sorted(policy.permissions_any) if policy else [],
                "rolesAny": sorted(policy.roles_any) if policy else [],
            },
        }
    )


def _parameter_schema(parameter: click.Parameter) -> dict[str, Any]:
    option_names = list(getattr(parameter, "opts", ()) or ())
    is_option = bool(option_names)
    result: dict[str, Any] = {
        "name": parameter.name,
        "kind": "option" if is_option else "argument",
        "required": bool(parameter.required),
        "type": parameter.type.name,
        "multiple": bool(getattr(parameter, "multiple", False)),
    }
    if is_option:
        result["options"] = option_names
        result["help"] = getattr(parameter, "help", None) or ""
    if parameter.default not in (None, ()):
        result["default"] = parameter.default
    return result
