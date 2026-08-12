from __future__ import annotations

from collections.abc import Iterable
from typing import Any, Callable, Optional

import typer

from cli.commands.common import run_authorized
from cli.errors import CliError, EXIT_API, EXIT_USAGE
from cli.realtime import RealtimeWatcher
from cli.session import AuthorizedSession


def install_realtime_commands(
    root_app: typer.Typer,
    *,
    execute: Callable,
    execute_stream: Callable,
    get_context: Callable,
) -> None:
    realtime_app = typer.Typer(help="Query and watch realtime data.", no_args_is_help=True)
    root_app.add_typer(realtime_app, name="realtime")

    @realtime_app.command("tree")
    def tree(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "realtime tree",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/monitor/device-tree"
            ),
        )

    @realtime_app.command("get")
    def get_realtime(
        ctx: typer.Context,
        point: Optional[str] = typer.Option(None, "--point"),
        device: Optional[str] = typer.Option(None, "--device"),
        group: Optional[str] = typer.Option(
            None, "--group", hidden=True, help="Advanced group override."
        ),
    ) -> None:
        def operation(session) -> dict[str, Any]:
            resolved_device, resolved_point, group_code = _resolve_realtime_target(
                session,
                point=point,
                device=device,
                group=group,
            )
            payload = session.request(
                "GET",
                "/api/device-monitor/monitor/realtime-init",
                params={
                    "deviceId": int(resolved_device["deviceId"]),
                    "groupCode": group_code,
                },
            )
            if resolved_point is None:
                return payload
            filtered = _filter_point(payload, str(resolved_point["pointId"]))
            return {**filtered, "resolvedTarget": _public_target(resolved_point)}

        run_authorized(
            ctx,
            "realtime get",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @realtime_app.command("window")
    def window(
        ctx: typer.Context,
        point: Optional[str] = typer.Option(None, "--point"),
        device: Optional[str] = typer.Option(None, "--device"),
        group: Optional[str] = typer.Option(
            None, "--group", hidden=True, help="Advanced group override."
        ),
        seconds: float = typer.Option(10.0, "--seconds", min=0.1, max=300),
        max_events: int = typer.Option(1000, "--max-events", min=1, max=10000),
    ) -> None:
        def operation(session) -> dict[str, Any]:
            resolved_device, resolved_point, group_code = _resolve_realtime_target(
                session,
                point=point,
                device=device,
                group=group,
            )
            if session.runtime is None or session.credentials is None:
                raise CliError("internal_error", "认证会话未初始化", EXIT_API)
            watcher = RealtimeWatcher(
                session.runtime.base_url,
                session.credentials.token,
                timeout_seconds=session.runtime.timeout_seconds,
            )
            events = list(
                watcher.events(
                    str(resolved_device["deviceCode"]),
                    group_code,
                    point_id=(
                        int(resolved_point["pointId"])
                        if resolved_point is not None
                        else None
                    ),
                    max_events=max_events,
                    duration_seconds=seconds,
                )
            )
            return {
                "windowSeconds": seconds,
                "eventCount": len(events),
                "device": {
                    "deviceCode": resolved_device.get("deviceCode"),
                    "deviceName": resolved_device.get("deviceName"),
                },
                "resolvedTarget": (
                    _public_target(resolved_point) if resolved_point is not None else None
                ),
                "events": events,
            }

        run_authorized(
            ctx,
            "realtime window",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @realtime_app.command("watch")
    def watch(
        ctx: typer.Context,
        point: Optional[str] = typer.Option(None, "--point"),
        device: Optional[str] = typer.Option(None, "--device"),
        group: Optional[str] = typer.Option(
            None, "--group", hidden=True, help="Advanced group override."
        ),
        max_events: int = typer.Option(0, "--max-events", min=0),
        idle_timeout: float = typer.Option(0, "--idle-timeout", min=0),
    ) -> None:
        def operation() -> Iterable[dict[str, Any]]:
            with AuthorizedSession(get_context(ctx)) as session:
                session.authorize("realtime watch")
                resolved_device, resolved_point, group_code = _resolve_realtime_target(
                    session,
                    point=point,
                    device=device,
                    group=group,
                )
                if session.runtime is None or session.credentials is None:
                    raise CliError("internal_error", "认证会话未初始化", EXIT_API)
                watcher = RealtimeWatcher(
                    session.runtime.base_url,
                    session.credentials.token,
                    timeout_seconds=session.runtime.timeout_seconds,
                )
                for event in watcher.events(
                    str(resolved_device["deviceCode"]),
                    group_code,
                    point_id=(
                        int(resolved_point["pointId"])
                        if resolved_point is not None
                        else None
                    ),
                    max_events=max_events,
                    idle_timeout=idle_timeout or None,
                ):
                    if resolved_point is not None:
                        yield {**event, "resolvedTarget": _public_target(resolved_point)}
                    else:
                        yield event

        execute_stream(ctx, "realtime watch", operation)


def _resolve_realtime_target(
    session,
    *,
    point: str | None,
    device: str | None,
    group: str | None,
) -> tuple[dict[str, Any], dict[str, Any] | None, str]:
    if point:
        resolved_device, resolved_points = session.resolver.resolve_business_points(
            [point], device_hint=device
        )
        resolved_point = resolved_points[0]
        group_code = str(group or resolved_point.get("groupCode") or "").strip()
        if not group_code:
            raise CliError(
                "resource_unavailable",
                "该点位没有有效采集组，无法读取实时数据",
                EXIT_API,
                status=409,
                details={"target": _public_target(resolved_point)},
            )
        if resolved_point.get("pointStatus") == "disabled":
            raise CliError(
                "resource_unavailable",
                "该点位已停用，无法读取实时数据",
                EXIT_API,
                status=409,
                details={"target": _public_target(resolved_point)},
            )
        return resolved_device, resolved_point, group_code

    if not device:
        raise CliError(
            "usage_error", "请提供 --point，或至少提供 --device", EXIT_USAGE
        )
    resolved_device = session.resolver.resolve_monitor_device(device)
    return resolved_device, None, str(group or "g001")


def _public_target(point: dict[str, Any]) -> dict[str, Any]:
    return {
        "deviceName": point.get("deviceName"),
        "deviceGroupName": point.get("deviceGroupName"),
        "pointName": point.get("pointName"),
        "ioAddress": point.get("ioAddress"),
        "description": point.get("description"),
    }


def _filter_point(payload: Any, identifier: str) -> dict[str, Any]:
    if not isinstance(payload, dict):
        raise CliError("api_error", "实时数据响应格式错误", EXIT_API)
    items = payload.get("data")
    if not isinstance(items, list):
        raise CliError("api_error", "实时点位响应格式错误", EXIT_API)
    key = _lookup_key(identifier)
    matches = [
        item
        for item in items
        if isinstance(item, dict)
        and key
        in {
            _lookup_key(item.get("pointId")),
            _lookup_key(item.get("varSeq")),
            _lookup_key(item.get("pointName")),
            _lookup_key(item.get("ioAddress")),
        }
    ]
    if not matches:
        raise CliError(
            "resource_not_found",
            f"实时点位不存在: {identifier}",
            EXIT_API,
            status=404,
        )
    if len(matches) > 1:
        raise CliError(
            "ambiguous_resource",
            f"实时点位标识不唯一: {identifier}",
            EXIT_API,
            status=409,
        )
    return {**payload, "data": matches, "pointFilter": identifier}


def _lookup_key(value: Any) -> str:
    return "".join(
        character
        for character in str(value or "").casefold()
        if not character.isspace() and character not in {"_", "-"}
    )
