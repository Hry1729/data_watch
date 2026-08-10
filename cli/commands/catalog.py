from __future__ import annotations

from typing import Any, Callable, Optional

import typer

from cli.commands.common import compact_params, run_authorized
from cli.errors import CliError, EXIT_API, EXIT_USAGE
from cli.resolvers import device_lookup_key


DEVICE_STATES = {"enabled", "disabled"}
CONNECT_STATES = {"online", "unstable", "offline", "unknown"}

devices_app = typer.Typer(help="Query device catalog and status.", no_args_is_help=True)
points_app = typer.Typer(help="Query point catalog.", no_args_is_help=True)


def install_catalog_commands(
    root_app: typer.Typer,
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    root_app.add_typer(devices_app, name="devices")
    root_app.add_typer(points_app, name="points")

    @devices_app.command("status")
    def device_status(
        ctx: typer.Context,
        device: Optional[str] = typer.Option(None, "--device"),
    ) -> None:
        def operation(session) -> dict[str, Any]:
            payload = session.request(
                "GET", "/api/device-monitor/module-monitor/device-status"
            )
            if not device:
                return payload
            items = payload.get("items", []) if isinstance(payload, dict) else []
            resolved = _resolve_status_device(items, device)
            return {
                "statusSource": payload.get("statusSource"),
                "item": resolved,
            }

        run_authorized(
            ctx,
            "devices status",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @devices_app.command("list")
    def devices_list(
        ctx: typer.Context,
        page: int = typer.Option(1, "--page", min=1),
        page_size: int = typer.Option(20, "--page-size", min=1, max=500),
        keyword: Optional[str] = typer.Option(None, "--keyword"),
        device_type: Optional[str] = typer.Option(None, "--device-type"),
        device_code: Optional[str] = typer.Option(None, "--device-code"),
        device_name: Optional[str] = typer.Option(None, "--device-name"),
        device_status: Optional[str] = typer.Option(None, "--device-status"),
        connect_status: Optional[str] = typer.Option(
            None, "--connect-status", "--status"
        ),
    ) -> None:
        def operation(session) -> Any:
            normalized_device_status = _validate_choice(
                device_status, DEVICE_STATES, "device-status"
            )
            normalized_connect_status = _validate_choice(
                connect_status, CONNECT_STATES - {"unknown"}, "connect-status"
            )
            return session.request(
                "GET",
                "/api/device-monitor/device/list",
                params=compact_params(
                    pageNo=page,
                    pageSize=page_size,
                    keyword=device_lookup_key(keyword) if keyword else None,
                    deviceType=device_type,
                    deviceCode=device_code,
                    deviceName=device_name,
                    deviceStatus=normalized_device_status,
                    connectStatus=normalized_connect_status,
                ),
            )

        run_authorized(
            ctx,
            "devices list",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @devices_app.command("get")
    def devices_get(ctx: typer.Context, identifier: str = typer.Argument(...)) -> None:
        def operation(session) -> Any:
            resolved = session.resolver.resolve_catalog_device(identifier)
            return session.request(
                "GET", f"/api/device-monitor/device/{int(resolved['id'])}"
            )

        run_authorized(
            ctx,
            "devices get",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @devices_app.command("groups")
    def device_groups(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "devices groups",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/device/group-tree"
            ),
        )

    @points_app.command("resolve")
    def point_resolve(
        ctx: typer.Context,
        query: str = typer.Argument(...),
        device: Optional[str] = typer.Option(None, "--device"),
        limit: int = typer.Option(20, "--limit", min=1, max=50),
    ) -> None:
        def operation(session) -> dict[str, Any]:
            payload = session.resolver.search_business_points(
                query, device_hint=device, limit=limit
            )
            items = payload.get("items", [])
            best_rank = min(
                (int(item.get("matchRank") or 0) for item in items),
                default=None,
            )
            best = [
                item
                for item in items
                if best_rank is not None and int(item.get("matchRank") or 0) == best_rank
            ]
            return {
                **payload,
                "ambiguous": len(best) > 1,
                "selected": best[0] if len(best) == 1 else None,
            }

        run_authorized(
            ctx,
            "points resolve",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @points_app.command("list")
    def points_list(
        ctx: typer.Context,
        device: str = typer.Option(..., "--device"),
        page: int = typer.Option(1, "--page", min=1),
        page_size: int = typer.Option(20, "--page-size", min=1, max=500),
        keyword: Optional[str] = typer.Option(None, "--keyword"),
        group_id: Optional[str] = typer.Option(None, "--group-id"),
        data_type: Optional[str] = typer.Option(None, "--data-type"),
    ) -> None:
        def operation(session) -> Any:
            resolved = session.resolver.resolve_catalog_device(device)
            return session.request(
                "GET",
                "/api/device-monitor/point/list",
                params=compact_params(
                    pageNo=page,
                    pageSize=page_size,
                    deviceCode=resolved.get("deviceCode"),
                    deviceId=int(resolved["id"]),
                    keyword=keyword,
                    groupId=group_id,
                    dataType=data_type,
                ),
            )

        run_authorized(
            ctx,
            "points list",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @points_app.command("get")
    def points_get(ctx: typer.Context, point_id: int = typer.Argument(..., min=1)) -> None:
        run_authorized(
            ctx,
            "points get",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", f"/api/device-monitor/point/{point_id}"
            ),
        )

    @points_app.command("groups")
    def point_groups(
        ctx: typer.Context,
        device: str = typer.Option(..., "--device"),
    ) -> None:
        def operation(session) -> Any:
            resolved = session.resolver.resolve_catalog_device(device)
            identifier = resolved.get("deviceCode") or int(resolved["id"])
            return session.request(
                "GET", f"/api/device-monitor/device/{identifier}/point-groups"
            )

        run_authorized(
            ctx,
            "points groups",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @points_app.command("variables")
    def point_variables(
        ctx: typer.Context,
        device: str = typer.Option(..., "--device"),
        group: str = typer.Option("g001", "--group", hidden=True, help="Advanced group override."),
    ) -> None:
        def operation(session) -> Any:
            resolved = session.resolver.resolve_catalog_device(device)
            identifier = resolved.get("deviceCode") or int(resolved["id"])
            return session.request(
                "GET",
                f"/api/device-monitor/device/{identifier}/variables",
                params={"group_code": group},
            )

        run_authorized(
            ctx,
            "points variables",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )


def _resolve_status_device(items: Any, identifier: str) -> dict[str, Any]:
    if not isinstance(items, list):
        raise CliError("api_error", "设备状态响应格式错误", EXIT_API)
    query = device_lookup_key(identifier)
    ranked: list[tuple[int, dict[str, Any]]] = []
    for item in items:
        if not isinstance(item, dict):
            continue
        ranks = [
            rank
            for field in ("deviceCode", "deviceName", "groupName", "protocol")
            if (rank := _value_rank(query, item.get(field))) is not None
        ]
        if ranks:
            ranked.append((min(ranks), item))
    if not ranked:
        raise CliError(
            "resource_not_found", f"设备不存在: {identifier}", EXIT_API, status=404
        )
    best_rank = min(rank for rank, _item in ranked)
    matches = [item for rank, item in ranked if rank == best_rank]
    if len(matches) != 1:
        raise CliError(
            "ambiguous_resource",
            f"设备名称不唯一: {identifier}",
            EXIT_API,
            status=409,
            details={
                "candidates": [
                    {
                        "deviceName": item.get("deviceName"),
                        "groupName": item.get("groupName"),
                        "deviceCode": item.get("deviceCode"),
                    }
                    for item in matches[:20]
                ]
            },
        )
    return matches[0]


def _lookup_key(value: Any) -> str:
    return "".join(
        character
        for character in str(value or "").casefold()
        if not character.isspace() and character not in {"_", "-"}
    )


def _value_rank(query: str, value: Any) -> int | None:
    candidate = _lookup_key(value)
    if not candidate:
        return None
    if candidate == query:
        return 0
    if candidate.startswith(query):
        return 1
    if query in candidate:
        return 2
    return None


def _validate_choice(
    value: str | None,
    allowed: set[str],
    option_name: str,
) -> str | None:
    if value is None:
        return None
    normalized = value.strip().lower()
    if normalized not in allowed:
        choices = ", ".join(sorted(allowed))
        raise CliError(
            "usage_error",
            f"--{option_name} 仅支持: {choices}",
            EXIT_USAGE,
        )
    return normalized
