from __future__ import annotations

from typing import Any, Callable, Optional

import typer

from cli.commands.common import compact_params, run_authorized
from cli.errors import CliError, EXIT_API, EXIT_USAGE


SYSTEM_ALERT_LEVELS = {"WARN", "ERROR", "CRITICAL"}

alerts_app = typer.Typer(help="Query real system log alerts.", no_args_is_help=True)
modules_app = typer.Typer(help="Inspect collector and writer health.", no_args_is_help=True)
collector_config_app = typer.Typer(help="Inspect collector configuration.", no_args_is_help=True)
logs_app = typer.Typer(help="Query system and operation logs.", no_args_is_help=True)
policy_app = typer.Typer(help="Inspect log retention policy.", no_args_is_help=True)


def install_diagnostic_commands(
    root_app: typer.Typer,
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    root_app.add_typer(alerts_app, name="system-alerts")
    root_app.add_typer(modules_app, name="modules")
    modules_app.add_typer(collector_config_app, name="collector-config")
    root_app.add_typer(logs_app, name="logs")
    logs_app.add_typer(policy_app, name="policy")

    @alerts_app.command("summary")
    def alerts_summary(ctx: typer.Context) -> None:
        def operation(session) -> dict[str, Any]:
            overview = session.request(
                "GET", "/api/device-monitor/dashboard/overview"
            )
            return {
                "period": "last_24_hours",
                "systemAlertTotal": overview.get("systemAlertTotal", 0),
                "systemAlertComparePrev": overview.get(
                    "systemAlertComparePrev", {"countDiff": 0, "ratioDiff": 0}
                ),
                "generatedAt": overview.get("generatedAt"),
                "source": "system_log+operation_log",
            }

        run_authorized(
            ctx,
            "system-alerts summary",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @alerts_app.command("list")
    def alerts_list(
        ctx: typer.Context,
        level: str = typer.Option("ERROR", "--level"),
        page: int = typer.Option(1, "--page", min=1),
        page_size: int = typer.Option(20, "--page-size", min=1, max=500),
        start: Optional[str] = typer.Option(None, "--start"),
        end: Optional[str] = typer.Option(None, "--end"),
        keyword: Optional[str] = typer.Option(None, "--keyword"),
        device: Optional[str] = typer.Option(None, "--device"),
    ) -> None:
        def operation(session) -> Any:
            normalized_level = _system_alert_level(level)
            return session.request(
                "GET",
                "/api/device-monitor/log/list",
                params=compact_params(
                    pageNo=page,
                    pageSize=page_size,
                    level=normalized_level,
                    startTime=start,
                    endTime=end,
                    keyword=keyword,
                    deviceCode=_resolved_device_code(session, device),
                ),
            )

        run_authorized(
            ctx,
            "system-alerts list",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @modules_app.command("status")
    def modules_status(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "modules status",
            execute=execute,
            get_context=get_context,
            operation=_module_status,
        )

    @modules_app.command("collector")
    def modules_collector(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "modules collector",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/module-monitor/collector"
            ),
        )

    @modules_app.command("writer")
    def modules_writer(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "modules writer",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/module-monitor/writer"
            ),
        )

    @modules_app.command("rates")
    def modules_rates(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "modules rates",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/module-monitor/rates"
            ),
        )

    @collector_config_app.command("show")
    def collector_config(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "modules collector-config show",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/collector-config"
            ),
        )

    @logs_app.command("list")
    def logs_list(
        ctx: typer.Context,
        page: int = typer.Option(1, "--page", min=1),
        page_size: int = typer.Option(20, "--page-size", min=1, max=500),
        level: Optional[str] = typer.Option(None, "--level"),
        log_type: Optional[str] = typer.Option(None, "--type"),
        keyword: Optional[str] = typer.Option(None, "--keyword"),
        start: Optional[str] = typer.Option(None, "--start"),
        end: Optional[str] = typer.Option(None, "--end"),
        source: Optional[str] = typer.Option(None, "--source"),
        service: Optional[str] = typer.Option(None, "--service"),
        device: Optional[str] = typer.Option(None, "--device"),
        operator: Optional[str] = typer.Option(None, "--operator"),
        trace_id: Optional[str] = typer.Option(None, "--trace-id"),
    ) -> None:
        run_authorized(
            ctx,
            "logs list",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET",
                "/api/device-monitor/log/list",
                params=compact_params(
                    pageNo=page,
                    pageSize=page_size,
                    level=level,
                    type=log_type,
                    keyword=keyword,
                    startTime=start,
                    endTime=end,
                    source=source,
                    serviceName=service,
                    deviceCode=_resolved_device_code(session, device),
                    operatorName=operator,
                    traceId=trace_id,
                ),
            ),
        )

    @logs_app.command("stats")
    def logs_stats(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "logs stats",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/log/stats"
            ),
        )

    @logs_app.command("tdengine-files")
    def tdengine_files(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "logs tdengine-files",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/log/tdengine/files"
            ),
        )

    @logs_app.command("tdengine-tail")
    def tdengine_tail(
        ctx: typer.Context,
        file: str = typer.Option(..., "--file"),
        lines: int = typer.Option(200, "--lines", min=1, max=5000),
    ) -> None:
        run_authorized(
            ctx,
            "logs tdengine-tail",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET",
                "/api/device-monitor/log/tdengine/tail",
                params={"file": file, "lines": lines},
            ),
        )

    @policy_app.command("show")
    def policy_show(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "logs policy show",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/device-monitor/log/policy"
            ),
        )


def _resolved_device_code(session, identifier: str | None) -> str | None:
    if identifier is None:
        return None
    catalog_allowed = session.has_any_permission("device_monitor:device:view")
    if catalog_allowed:
        resolved = session.resolver.resolve_catalog_device(identifier)
    else:
        resolved = session.resolver.resolve_monitor_device(identifier)
    device_code = resolved.get("deviceCode")
    if not device_code and catalog_allowed:
        detail = session.request(
            "GET", f"/api/device-monitor/device/{int(resolved['id'])}"
        )
        device_code = detail.get("deviceCode") if isinstance(detail, dict) else None
    if not device_code:
        raise CliError("api_error", "设备响应缺少 deviceCode", EXIT_API)
    return str(device_code)


def _system_alert_level(level: str) -> str:
    normalized = level.strip().upper()
    if normalized not in SYSTEM_ALERT_LEVELS:
        raise CliError(
            "usage_error",
            "--level 仅支持 WARN、ERROR 或 CRITICAL",
            EXIT_USAGE,
        )
    return normalized


def _module_status(session) -> dict[str, Any]:
    checks: list[dict[str, Any]] = []
    reasons: list[str] = []

    _capture_check(
        session,
        checks,
        reasons,
        name="backend",
        path="/healthz",
        evaluator=lambda _data: ("healthy", []),
    )
    _capture_check(
        session,
        checks,
        reasons,
        name="collector",
        path="/api/device-monitor/module-monitor/collector",
        evaluator=_collector_level,
    )
    _capture_check(
        session,
        checks,
        reasons,
        name="writer",
        path="/api/device-monitor/module-monitor/writer",
        evaluator=_writer_level,
    )
    _capture_check(
        session,
        checks,
        reasons,
        name="rates",
        path="/api/device-monitor/module-monitor/rates",
        evaluator=lambda _data: ("healthy", []),
    )

    if session.has_any_permission("device_monitor:dashboard:view"):
        _capture_check(
            session,
            checks,
            reasons,
            name="edgePipeline",
            path="/api/device-monitor/dashboard/edge-pipeline-health",
            evaluator=_pipeline_level,
        )
    else:
        reason = "缺少 device_monitor:dashboard:view，边缘链路未检查"
        checks.append({"name": "edgePipeline", "status": "not_checked"})
        reasons.append(reason)

    statuses = {str(check["status"]) for check in checks}
    if "down" in statuses:
        overall = "down"
    elif statuses.intersection({"degraded", "not_checked"}):
        overall = "degraded"
    else:
        overall = "healthy"
    return {"status": overall, "checks": checks, "reasons": reasons}


def _capture_check(
    session,
    checks: list[dict[str, Any]],
    reasons: list[str],
    *,
    name: str,
    path: str,
    evaluator: Callable[[Any], tuple[str, list[str]]],
) -> None:
    try:
        data = session.request("GET", path)
        status, check_reasons = evaluator(data)
        checks.append({"name": name, "status": status, "data": data})
        reasons.extend(check_reasons)
    except CliError as exc:
        reason = f"{name}: {exc.message}"
        checks.append(
            {"name": name, "status": "down", "error": exc.to_dict()}
        )
        reasons.append(reason)


def _collector_level(data: Any) -> tuple[str, list[str]]:
    if not isinstance(data, dict):
        return "down", ["collector: 指标格式错误"]
    total = int(data.get("totalDevices") or 0)
    online = int(data.get("onlineDevices") or 0)
    if total > 0 and online == 0:
        return "down", ["collector: 没有设备处于 online"]
    if online < total:
        return "degraded", [f"collector: {total - online} 台设备未处于 online"]
    return "healthy", []


def _writer_level(data: Any) -> tuple[str, list[str]]:
    if not isinstance(data, dict):
        return "down", ["writer: 指标格式错误"]
    backlog = any(
        isinstance(stream, dict) and stream.get("status") == "backlog"
        for stream in data.get("streams", [])
    )
    dlq_total = int(data.get("dlqTotal") or 0)
    if backlog or dlq_total > 0:
        return "degraded", [
            f"writer: backlog={str(backlog).lower()}, dlqTotal={dlq_total}"
        ]
    return "healthy", []


def _pipeline_level(data: Any) -> tuple[str, list[str]]:
    if not isinstance(data, dict):
        return "down", ["edgePipeline: 指标格式错误"]
    statuses = {
        str(data.get("collector", {}).get("status") or "unknown"),
        str(data.get("uploader", {}).get("status") or "unknown"),
    }
    if "critical" in statuses:
        return "down", ["edgePipeline: 存在 critical 模块"]
    if statuses.intersection({"warning", "unknown"}):
        return "degraded", ["edgePipeline: 存在 warning 或 unknown 模块"]
    return "healthy", []
