from __future__ import annotations

from typing import Any, Callable

import typer
from typer.main import get_command

from cli.errors import CliError, EXIT_USAGE
from cli.schema import command_schemas, find_command_schema


def install_contract_commands(
    app: typer.Typer,
    *,
    execute: Callable[..., Any],
) -> None:
    commands_app = typer.Typer(help="Inspect the machine command contract.", no_args_is_help=True)
    app.add_typer(commands_app, name="commands")

    @commands_app.command("list")
    def list_commands(ctx: typer.Context) -> None:
        execute(
            ctx,
            "commands list",
            lambda: {"commands": command_schemas(get_command(app))},
        )

    @commands_app.command("schema")
    def command_schema(
        ctx: typer.Context,
        command: list[str] = typer.Argument(...),
    ) -> None:
        def operation() -> dict[str, Any]:
            command_name = " ".join(command)
            schema = find_command_schema(get_command(app), command_name)
            if schema is None:
                raise CliError(
                    "usage_error",
                    f"命令不存在: {command_name}",
                    EXIT_USAGE,
                )
            return schema

        execute(ctx, "commands schema", operation)
