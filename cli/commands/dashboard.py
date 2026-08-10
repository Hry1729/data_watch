from __future__ import annotations

from typing import Any, Callable, Optional

import typer

from cli.commands.common import compact_params, run_authorized


def install_dashboard_commands(
    root_app: typer.Typer,
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    dashboard_app = typer.Typer(help="Query dashboard metrics.", no_args_is_help=True)
    root_app.add_typer(dashboard_app, name="dashboard")

    @dashboard_app.command("overview")
    def overview(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "dashboard overview",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/dashboard/overview"
            ),
        )

    @dashboard_app.command("trend")
    def trend(
        ctx: typer.Context,
        range_type: str = typer.Option("24h", "--range"),
        granularity: str = typer.Option("1h", "--granularity"),
        metrics: Optional[list[str]] = typer.Option(None, "--metric"),
    ) -> None:
        run_authorized(
            ctx,
            "dashboard trend",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET",
                "/api/device-monitor/dashboard/trend",
                params=compact_params(
                    rangeType=range_type,
                    granularity=granularity,
                    metricKeys=metrics,
                ),
            ),
        )

    @dashboard_app.command("device-types")
    def device_types(
        ctx: typer.Context,
        top: int = typer.Option(10, "--top", min=1, max=100),
    ) -> None:
        run_authorized(
            ctx,
            "dashboard device-types",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET",
                "/api/device-monitor/dashboard/device-type-distribution",
                params={"topN": top},
            ),
        )

    @dashboard_app.command("connection-health")
    def connection_health(
        ctx: typer.Context,
        range_type: str = typer.Option("24h", "--range"),
        top: int = typer.Option(10, "--top", min=1, max=100),
        include_offline: bool = typer.Option(True, "--include-offline/--online-only"),
    ) -> None:
        run_authorized(
            ctx,
            "dashboard connection-health",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET",
                "/api/device-monitor/dashboard/device-connection-health",
                params={
                    "statRange": range_type,
                    "topN": top,
                    "includeOffline": str(include_offline).lower(),
                },
            ),
        )

    @dashboard_app.command("pipeline-health")
    def pipeline_health(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "dashboard pipeline-health",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/dashboard/edge-pipeline-health"
            ),
        )
