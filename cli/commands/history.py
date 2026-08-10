from __future__ import annotations

from typing import Any, Callable, Optional

import typer

from cli.commands.common import run_authorized


history_app = typer.Typer(help="Query historical data.", no_args_is_help=True)


def install_history_commands(
    root_app: typer.Typer,
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    root_app.add_typer(history_app, name="history")

    @history_app.command("devices")
    def devices(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "history devices",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/history/devices"
            ),
        )

    @history_app.command("device-types")
    def device_types(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "history device-types",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/history/device-types"
            ),
        )

    @history_app.command("points")
    def points(
        ctx: typer.Context,
        device: str = typer.Option(..., "--device"),
    ) -> None:
        def operation(session) -> dict[str, Any]:
            resolved_device = session.resolver.resolve_history_device(device)
            device_id = int(resolved_device["id"])
            return {
                "device": resolved_device,
                "points": session.resolver.history_points_for_device(device_id),
            }

        run_authorized(
            ctx,
            "history points",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @history_app.command("query")
    def query(
        ctx: typer.Context,
        points: list[str] = typer.Option(..., "--point"),
        start: str = typer.Option(..., "--start"),
        end: str = typer.Option(..., "--end"),
        device: Optional[str] = typer.Option(None, "--device"),
    ) -> None:
        _run_multi_point_query(
            ctx,
            command="history query",
            path="/api/device-monitor/history/query",
            device=device,
            points=points,
            start=start,
            end=end,
            extra_params=[],
            execute=execute,
            get_context=get_context,
        )

    @history_app.command("chart")
    def chart(
        ctx: typer.Context,
        points: list[str] = typer.Option(..., "--point"),
        start: str = typer.Option(..., "--start"),
        end: str = typer.Option(..., "--end"),
        device: Optional[str] = typer.Option(None, "--device"),
        interval: str = typer.Option("auto", "--interval"),
        aggregate: str = typer.Option("avg", "--aggregate"),
    ) -> None:
        _run_multi_point_query(
            ctx,
            command="history chart",
            path="/api/device-monitor/history/chart",
            device=device,
            points=points,
            start=start,
            end=end,
            extra_params=[("interval", interval), ("aggregate", aggregate)],
            execute=execute,
            get_context=get_context,
        )

    @history_app.command("raw")
    def raw(
        ctx: typer.Context,
        point: str = typer.Option(..., "--point"),
        start: str = typer.Option(..., "--start"),
        end: str = typer.Option(..., "--end"),
        device: Optional[str] = typer.Option(None, "--device"),
        page_size: int = typer.Option(1000, "--page-size", min=1, max=5000),
        cursor: Optional[str] = typer.Option(None, "--cursor"),
    ) -> None:
        def operation(session) -> dict[str, Any]:
            resolved_device, resolved_points = session.resolver.resolve_business_points(
                [point], device_hint=device
            )
            resolved_point = resolved_points[0]
            params = {
                "deviceId": int(resolved_device["id"]),
                "varSeq": int(resolved_point["varSeq"]),
                "startTime": start,
                "endTime": end,
                "pageSize": page_size,
            }
            if cursor:
                params["cursorTs"] = cursor
            result = session.request(
                "GET", "/api/device-monitor/history/raw", params=params
            )
            return {
                "device": resolved_device,
                "point": resolved_point,
                "result": result,
            }

        run_authorized(
            ctx,
            "history raw",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @history_app.command("matrix")
    def matrix(
        ctx: typer.Context,
        points: list[str] = typer.Option(..., "--point"),
        start: str = typer.Option(..., "--start"),
        end: str = typer.Option(..., "--end"),
        device: Optional[str] = typer.Option(None, "--device"),
        align: str = typer.Option("1s", "--align"),
        page_size: int = typer.Option(200, "--page-size", min=1, max=500),
        cursor: Optional[str] = typer.Option(None, "--cursor"),
    ) -> None:
        extra: list[tuple[str, Any]] = [
            ("alignGranularity", align),
            ("pageSize", page_size),
        ]
        if cursor:
            extra.append(("cursorTs", cursor))
        _run_multi_point_query(
            ctx,
            command="history matrix",
            path="/api/device-monitor/history/raw-matrix",
            device=device,
            points=points,
            start=start,
            end=end,
            extra_params=extra,
            execute=execute,
            get_context=get_context,
        )


def _run_multi_point_query(
    ctx: typer.Context,
    *,
    command: str,
    path: str,
    device: str | None,
    points: list[str],
    start: str,
    end: str,
    extra_params: list[tuple[str, Any]],
    execute: Callable,
    get_context: Callable,
) -> None:
    def operation(session) -> dict[str, Any]:
        resolved_device, resolved_points = session.resolver.resolve_business_points(
            points, device_hint=device
        )
        params: list[tuple[str, Any]] = [
            ("deviceId", int(resolved_device["id"])),
            ("startTime", start),
            ("endTime", end),
            *extra_params,
            *(("varSeqs", int(point["varSeq"])) for point in resolved_points),
        ]
        result = session.request("GET", path, params=params)
        return {
            "device": resolved_device,
            "points": resolved_points,
            "result": result,
        }

    run_authorized(
        ctx,
        command,
        execute=execute,
        get_context=get_context,
        operation=operation,
    )
