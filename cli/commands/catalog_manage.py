from __future__ import annotations

from typing import Any, Callable, Optional
import typer

from cli.commands.catalog import devices_app, points_app
from cli.commands.common import compact_params, run_authorized
from cli.files import (
    load_json_object,
    load_workbook_rows,
    parse_ids,
    require_confirmation,
    write_download,
)


def install_catalog_management(
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    @devices_app.command("create")
    def device_create(ctx: typer.Context, file: str = typer.Option(..., "--file")) -> None:
        run_authorized(
            ctx,
            "devices create",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "POST", "/api/device-monitor/device", json_body=load_json_object(file)
            ),
        )

    @devices_app.command("update")
    def device_update(
        ctx: typer.Context,
        identifier: str = typer.Argument(...),
        file: str = typer.Option(..., "--file"),
    ) -> None:
        def operation(session) -> Any:
            device = _device_detail(session, identifier)
            return session.request(
                "PUT",
                f"/api/device-monitor/device/{int(device['id'])}",
                json_body=load_json_object(file),
            )

        run_authorized(
            ctx,
            "devices update",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @devices_app.command("enable")
    def device_enable(ctx: typer.Context, identifier: str = typer.Argument(...)) -> None:
        _set_device_status(
            ctx,
            identifier=identifier,
            enabled=True,
            command="devices enable",
            execute=execute,
            get_context=get_context,
        )

    @devices_app.command("disable")
    def device_disable(ctx: typer.Context, identifier: str = typer.Argument(...)) -> None:
        _set_device_status(
            ctx,
            identifier=identifier,
            enabled=False,
            command="devices disable",
            execute=execute,
            get_context=get_context,
        )

    @devices_app.command("import")
    def device_import(ctx: typer.Context, file: str = typer.Option(..., "--file")) -> None:
        run_authorized(
            ctx,
            "devices import",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "POST",
                "/api/device-monitor/device/import",
                json_body={"rows": load_workbook_rows(file, "device")},
            ),
        )

    @devices_app.command("export")
    def device_export(
        ctx: typer.Context,
        output: str = typer.Option(..., "--output"),
        keyword: Optional[str] = typer.Option(None, "--keyword"),
        device_type: Optional[str] = typer.Option(None, "--device-type"),
        device_status: Optional[str] = typer.Option(None, "--device-status"),
    ) -> None:
        def operation(session) -> dict[str, Any]:
            content, filename = session.download(
                "/api/device-monitor/device/export",
                params=compact_params(
                    pageNo=1,
                    pageSize=10000,
                    keyword=keyword,
                    deviceType=device_type,
                    deviceStatus=device_status,
                ),
            )
            return write_download(output, content, filename)

        run_authorized(
            ctx,
            "devices export",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @devices_app.command("delete")
    def device_delete(
        ctx: typer.Context,
        identifier: str = typer.Argument(...),
        confirm: str = typer.Option(..., "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            device = _device_detail(session, identifier)
            require_confirmation(confirm, str(device["deviceCode"]))
            return session.request(
                "DELETE", f"/api/device-monitor/device/{int(device['id'])}"
            )

        run_authorized(
            ctx,
            "devices delete",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @devices_app.command("delete-batch")
    def device_delete_batch(
        ctx: typer.Context,
        ids: str = typer.Option(..., "--ids"),
        confirm: str = typer.Option(..., "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, "batch")
            return session.request(
                "DELETE",
                "/api/device-monitor/device/batch",
                json_body={"ids": parse_ids(ids)},
            )

        run_authorized(
            ctx,
            "devices delete-batch",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @points_app.command("create")
    def point_create(ctx: typer.Context, file: str = typer.Option(..., "--file")) -> None:
        run_authorized(
            ctx,
            "points create",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "POST", "/api/device-monitor/point", json_body=load_json_object(file)
            ),
        )

    @points_app.command("update")
    def point_update(
        ctx: typer.Context,
        point_id: int = typer.Argument(..., min=1),
        file: str = typer.Option(..., "--file"),
    ) -> None:
        run_authorized(
            ctx,
            "points update",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "PUT",
                f"/api/device-monitor/point/{point_id}",
                json_body=load_json_object(file),
            ),
        )

    @points_app.command("enable")
    def point_enable(ctx: typer.Context, point_id: int = typer.Argument(..., min=1)) -> None:
        _set_point_status(
            ctx,
            point_id=point_id,
            enabled=True,
            command="points enable",
            execute=execute,
            get_context=get_context,
        )

    @points_app.command("disable")
    def point_disable(ctx: typer.Context, point_id: int = typer.Argument(..., min=1)) -> None:
        _set_point_status(
            ctx,
            point_id=point_id,
            enabled=False,
            command="points disable",
            execute=execute,
            get_context=get_context,
        )

    @points_app.command("import")
    def point_import(ctx: typer.Context, file: str = typer.Option(..., "--file")) -> None:
        run_authorized(
            ctx,
            "points import",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "POST",
                "/api/device-monitor/point/import",
                json_body={"rows": load_workbook_rows(file, "point")},
            ),
        )

    @points_app.command("template")
    def point_template(
        ctx: typer.Context,
        device: str = typer.Option(..., "--device"),
        output: str = typer.Option(..., "--output"),
    ) -> None:
        def operation(session) -> dict[str, Any]:
            device_code = str(_device_detail(session, device)["deviceCode"])
            content, filename = session.download(
                "/api/device-monitor/point/export",
                params={
                    "deviceCode": device_code,
                    "pageNo": 1,
                    "pageSize": 10000,
                    "template": "true",
                },
            )
            return write_download(output, content, filename)

        run_authorized(
            ctx,
            "points template",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @points_app.command("export")
    def point_export(
        ctx: typer.Context,
        device: str = typer.Option(..., "--device"),
        output: str = typer.Option(..., "--output"),
        keyword: Optional[str] = typer.Option(None, "--keyword"),
    ) -> None:
        def operation(session) -> dict[str, Any]:
            device_code = str(_device_detail(session, device)["deviceCode"])
            content, filename = session.download(
                "/api/device-monitor/point/export",
                params=compact_params(
                    deviceCode=device_code,
                    pageNo=1,
                    pageSize=10000,
                    keyword=keyword,
                ),
            )
            return write_download(output, content, filename)

        run_authorized(
            ctx,
            "points export",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @points_app.command("delete")
    def point_delete(
        ctx: typer.Context,
        point_id: int = typer.Argument(..., min=1),
        confirm: str = typer.Option(..., "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, str(point_id))
            return session.request(
                "DELETE", f"/api/device-monitor/point/{point_id}"
            )

        run_authorized(
            ctx,
            "points delete",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @points_app.command("delete-batch")
    def point_delete_batch(
        ctx: typer.Context,
        ids: str = typer.Option(..., "--ids"),
        confirm: str = typer.Option(..., "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, "batch")
            return session.request(
                "DELETE",
                "/api/device-monitor/point/batch",
                json_body={"ids": parse_ids(ids)},
            )

        run_authorized(
            ctx,
            "points delete-batch",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )


def _device_detail(session, identifier: str) -> dict[str, Any]:
    resolved = session.resolver.resolve_catalog_device(identifier)
    if resolved.get("deviceCode"):
        return resolved
    return session.request(
        "GET", f"/api/device-monitor/device/{int(resolved['id'])}"
    )


def _set_device_status(
    ctx: typer.Context,
    *,
    identifier: str,
    enabled: bool,
    command: str,
    execute: Callable,
    get_context: Callable,
) -> None:
    def operation(session) -> Any:
        device = _device_detail(session, identifier)
        return session.request(
            "PATCH",
            f"/api/device-monitor/device/{int(device['id'])}/status",
            json_body={"enabled": enabled},
        )

    run_authorized(
        ctx,
        command,
        execute=execute,
        get_context=get_context,
        operation=operation,
    )


def _set_point_status(
    ctx: typer.Context,
    *,
    point_id: int,
    enabled: bool,
    command: str,
    execute: Callable,
    get_context: Callable,
) -> None:
    run_authorized(
        ctx,
        command,
        execute=execute,
        get_context=get_context,
        operation=lambda session: session.request(
            "PATCH",
            f"/api/device-monitor/point/{point_id}/status",
            json_body={"enabled": enabled},
        ),
    )
