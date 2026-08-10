from __future__ import annotations

import click
from typer.main import get_command

from cli.main import app
from cli.permissions import COMMAND_POLICY_MAP


def test_every_leaf_command_has_exactly_one_permission_policy() -> None:
    commands = set(_leaf_commands(get_command(app)))

    assert commands == set(COMMAND_POLICY_MAP)


def _leaf_commands(command: click.Command, prefix: str = ""):
    children = getattr(command, "commands", None)
    if not isinstance(children, dict):
        yield prefix
        return
    for name, child in children.items():
        child_name = f"{prefix} {name}".strip()
        yield from _leaf_commands(child, child_name)
