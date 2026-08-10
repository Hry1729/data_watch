from __future__ import annotations

from typing import Callable, Optional

import typer

from cli.commands.common import compact_params, run_authorized



drivers_app = typer.Typer(help="Query driver catalog.", no_args_is_help=True)
nodes_app = typer.Typer(help="Query distributed edge nodes.", no_args_is_help=True)


def install_infrastructure_commands(
    root_app: typer.Typer,
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    root_app.add_typer(drivers_app, name="drivers")
    root_app.add_typer(nodes_app, name="nodes")

    @drivers_app.command("list")
    def drivers_list(
        ctx: typer.Context,
        page: int = typer.Option(1, "--page", min=1),
        page_size: int = typer.Option(20, "--page-size", min=1, max=500),
        keyword: Optional[str] = typer.Option(None, "--keyword"),
    ) -> None:
        run_authorized(
            ctx,
            "drivers list",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET",
                "/api/device-monitor/driver/list",
                params=compact_params(
                    pageNo=page,
                    pageSize=page_size,
                    keyword=keyword,
                ),
            ),
        )

    @drivers_app.command("options")
    def driver_options(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "drivers options",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/driver/options"
            ),
        )

    @nodes_app.command("list")
    def nodes_list(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "nodes list",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request("GET", "/api/distributed/nodes"),
        )

    @nodes_app.command("occupancy")
    def nodes_occupancy(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "nodes occupancy",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/distributed/assignments/occupancy"
            ),
        )

    @nodes_app.command("assignments")
    def node_assignments(
        ctx: typer.Context,
        node_uuid: str = typer.Argument(...),
    ) -> None:
        run_authorized(
            ctx,
            "nodes assignments",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", f"/api/distributed/nodes/{node_uuid}/assignments"
            ),
        )
