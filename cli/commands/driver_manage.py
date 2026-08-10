from __future__ import annotations

from typing import Any, Callable, Optional

import typer

from cli.commands.common import run_authorized
from cli.commands.infrastructure import drivers_app
from cli.files import load_json_object, require_confirmation


def install_driver_management(
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    @drivers_app.command("register")
    def driver_register(
        ctx: typer.Context,
        file: str = typer.Option(..., "--file"),
    ) -> None:
        run_authorized(
            ctx,
            "drivers register",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "POST",
                "/api/device-monitor/driver/register",
                json_body=load_json_object(file),
            ),
        )

    @drivers_app.command("uninstall")
    def driver_uninstall(
        ctx: typer.Context,
        name: str = typer.Argument(...),
        driver_id: Optional[str] = typer.Option(None, "--driver-id"),
        confirm: str = typer.Option(..., "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, name)
            payload = {"protocol": name}
            if driver_id:
                payload["driverId"] = driver_id
            return session.request(
                "POST",
                "/api/device-monitor/driver/uninstall",
                json_body=payload,
            )

        run_authorized(
            ctx,
            "drivers uninstall",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @drivers_app.command("uninstall-status")
    def driver_uninstall_status(
        ctx: typer.Context,
        name: str = typer.Argument(...),
    ) -> None:
        run_authorized(
            ctx,
            "drivers uninstall-status",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET",
                "/api/device-monitor/driver/uninstall-status",
                params={"protocol": name},
            ),
        )
