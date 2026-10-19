from __future__ import annotations

from typing import Any, Callable

import typer

from cli.commands.common import run_authorized
from cli.commands.infrastructure import nodes_app
from cli.files import load_json_object, require_confirmation


def install_node_management(
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    @nodes_app.command("assign")
    def node_assign(
        ctx: typer.Context,
        node_uuid: str = typer.Argument(...),
        devices: list[str] = typer.Option(..., "--device"),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            return _replace_assignments(
                session,
                node_uuid=node_uuid,
                devices=devices,
                confirm=confirm,
                mode="add",
            )

        run_authorized(
            ctx,
            "nodes assign",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @nodes_app.command("unassign")
    def node_unassign(
        ctx: typer.Context,
        node_uuid: str = typer.Argument(...),
        devices: list[str] = typer.Option(..., "--device"),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        run_authorized(
            ctx,
            "nodes unassign",
            execute=execute,
            get_context=get_context,
            operation=lambda session: _replace_assignments(
                session,
                node_uuid=node_uuid,
                devices=devices,
                confirm=confirm,
                mode="remove",
            ),
        )

    @nodes_app.command("assign-set")
    def node_assign_set(
        ctx: typer.Context,
        node_uuid: str = typer.Argument(...),
        devices: list[str] = typer.Option([], "--device"),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        run_authorized(
            ctx,
            "nodes assign-set",
            execute=execute,
            get_context=get_context,
            operation=lambda session: _replace_assignments(
                session,
                node_uuid=node_uuid,
                devices=devices,
                confirm=confirm,
                mode="set",
            ),
        )

    @nodes_app.command("enable")
    def node_enable(
        ctx: typer.Context,
        node_uuid: str = typer.Argument(...),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        _set_node_status(
            ctx,
            node_uuid=node_uuid,
            status="approved",
            confirm=confirm,
            command="nodes enable",
            execute=execute,
            get_context=get_context,
        )

    @nodes_app.command("disable")
    def node_disable(
        ctx: typer.Context,
        node_uuid: str = typer.Argument(...),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        _set_node_status(
            ctx,
            node_uuid=node_uuid,
            status="revoked",
            confirm=confirm,
            command="nodes disable",
            execute=execute,
            get_context=get_context,
        )

    @nodes_app.command("settings")
    def node_settings(
        ctx: typer.Context,
        node_uuid: str = typer.Argument(...),
        file: str = typer.Option(..., "--file"),
    ) -> None:
        run_authorized(
            ctx,
            "nodes settings",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "PATCH",
                f"/api/distributed/nodes/{node_uuid}/settings",
                json_body=load_json_object(file),
            ),
        )

    @nodes_app.command("registration-code")
    def node_registration_code(
        ctx: typer.Context,
        name: str = typer.Option(..., "--name"),
        expires: int = typer.Option(30, "--expires", min=1, max=1440),
    ) -> None:
        run_authorized(
            ctx,
            "nodes registration-code",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "POST",
                "/api/distributed/nodes/registration-codes",
                json_body={"nodeName": name, "expiresMinutes": expires},
            ),
        )

    @nodes_app.command("sync-device-status")
    def node_sync_device_status(
        ctx: typer.Context,
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, "sync")
            return session.request(
                "POST", "/api/distributed/nodes/sync-device-status"
            )

        run_authorized(
            ctx,
            "nodes sync-device-status",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )




def _replace_assignments(
    session,
    *,
    node_uuid: str,
    devices: list[str],
    confirm: str,
    mode: str,
) -> Any:
    require_confirmation(confirm, node_uuid)
    requested_ids = {
        int(session.resolver.resolve_catalog_device(device)["id"])
        for device in devices
    }
    # M4：assign/unassign 用增量字段，由后端在节点行锁内合并当前分配，
    # 不再"先 GET 再整体 PUT"，并发操作不会互相覆盖。
    body_key = {
        "set": "deviceIds",
        "add": "addDeviceIds",
        "remove": "removeDeviceIds",
    }[mode]
    return session.request(
        "PUT",
        f"/api/distributed/nodes/{node_uuid}/assignments",
        json_body={body_key: sorted(requested_ids)},
    )

def _set_node_status(
    ctx: typer.Context,
    *,
    node_uuid: str,
    status: str,
    confirm: str,
    command: str,
    execute: Callable,
    get_context: Callable,
) -> None:
    def operation(session) -> Any:
        require_confirmation(confirm, node_uuid)
        return session.request(
            "PATCH",
            f"/api/distributed/nodes/{node_uuid}/status",
            json_body={"status": status},
        )

    run_authorized(
        ctx,
        command,
        execute=execute,
        get_context=get_context,
        operation=operation,
    )

