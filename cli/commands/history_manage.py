from __future__ import annotations

from typing import Any, Callable, Optional
from urllib.parse import quote

import typer

from cli.commands.common import run_authorized
from cli.commands.history import history_app
from cli.files import write_download


def install_history_management(
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    @history_app.command("export")
    def history_export(
        ctx: typer.Context,
        points: list[str] = typer.Option(..., "--point"),
        start: str = typer.Option(..., "--start"),
        end: str = typer.Option(..., "--end"),
        device: Optional[str] = typer.Option(None, "--device"),
    ) -> None:
        def operation(session) -> dict[str, Any]:
            resolved_device, resolved_points = session.resolver.resolve_business_points(
                points, device_hint=device
            )
            result = session.request(
                "POST",
                "/api/device-monitor/history/export",
                json_body={
                    "deviceId": int(resolved_device["id"]),
                    "varSeqs": [int(point["varSeq"]) for point in resolved_points],
                    "startTime": start,
                    "endTime": end,
                },
            )
            return {
                "device": resolved_device,
                "points": resolved_points,
                "task": result,
            }

        run_authorized(
            ctx,
            "history export",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @history_app.command("export-status")
    def history_export_status(
        ctx: typer.Context,
        task_id: str = typer.Argument(...),
    ) -> None:
        run_authorized(
            ctx,
            "history export-status",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET",
                f"/api/device-monitor/history/export/{quote(task_id, safe='')}/status",
            ),
        )

    @history_app.command("export-download")
    def history_export_download(
        ctx: typer.Context,
        task_id: str = typer.Argument(...),
        output: str = typer.Option(..., "--output"),
    ) -> None:
        def operation(session) -> dict[str, Any]:
            content, filename = session.download(
                f"/api/device-monitor/history/export/{quote(task_id, safe='')}/download"
            )
            return write_download(output, content, filename)

        run_authorized(
            ctx,
            "history export-download",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )
