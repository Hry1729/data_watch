from __future__ import annotations

from typing import Any, Callable, Optional

import typer

from cli.commands.common import run_authorized
from cli.commands.diagnostics import collector_config_app, logs_app, policy_app
from cli.files import load_json_object, require_confirmation


debug_app = typer.Typer(help="Control temporary DEBUG log persistence.", no_args_is_help=True)


def install_log_management(
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    logs_app.add_typer(debug_app, name="debug")

    @collector_config_app.command("set")
    def collector_config_set(
        ctx: typer.Context,
        stream_maxlen: int = typer.Option(..., "--stream-maxlen", min=1),
    ) -> None:
        run_authorized(
            ctx,
            "modules collector-config set",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "PUT",
                "/api/device-monitor/collector-config",
                json_body={"streamMaxlen": stream_maxlen},
            ),
        )

    @policy_app.command("set")
    def log_policy_set(
        ctx: typer.Context,
        file: str = typer.Option(..., "--file"),
    ) -> None:
        run_authorized(
            ctx,
            "logs policy set",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "PUT",
                "/api/device-monitor/log/policy",
                json_body=load_json_object(file),
            ),
        )

    @debug_app.command("enable")
    def log_debug_enable(
        ctx: typer.Context,
        minutes: int = typer.Option(30, "--minutes", min=5, max=480),
        reason: Optional[str] = typer.Option(None, "--reason"),
    ) -> None:
        run_authorized(
            ctx,
            "logs debug enable",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "POST",
                "/api/device-monitor/log/debug",
                json_body={
                    "enabled": True,
                    "durationMinutes": minutes,
                    "reason": reason,
                },
            ),
        )

    @debug_app.command("disable")
    def log_debug_disable(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "logs debug disable",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "POST",
                "/api/device-monitor/log/debug",
                json_body={"enabled": False},
            ),
        )

    @logs_app.command("cleanup")
    def log_cleanup(
        ctx: typer.Context,
        before: str = typer.Option(..., "--before"),
        confirm: str = typer.Option("", "--confirm"),
        force: bool = typer.Option(False, "--force"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, "cleanup")
            return session.request(
                "POST",
                "/api/device-monitor/log/cleanup",
                json_body={
                    "mode": "beforeTime",
                    "beforeTime": before,
                    "force": force,
                },
            )

        run_authorized(
            ctx,
            "logs cleanup",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )
