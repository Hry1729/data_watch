"""``data-watch plc get``：薄封装，取数不走 Backend。"""

from __future__ import annotations

from typing import Any, Callable, Optional

import typer

from cli.errors import EXIT_USAGE, CliError
from cli.session import AuthorizedSession
from direct_read.api import read_point
from direct_read.audit import record_audit
from direct_read.cli_bridge import as_cli_error
from direct_read.errors import DirectReadError
from direct_read.quality import DIRECT_SOURCE, QUALITY_NOT_CONNECTED, quality_name


def install_plc_commands(
    root_app: typer.Typer,
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    plc_app = typer.Typer(
        help="Read one live PLC point directly, without the platform cache.",
        no_args_is_help=True,
    )
    root_app.add_typer(plc_app, name="plc")

    @plc_app.command("get")
    def plc_get(
        ctx: typer.Context,
        device: Optional[str] = typer.Option(
            None, "--device", help="Device code, field name, or numeric id."
        ),
        point: Optional[str] = typer.Option(
            None, "--point", help="Point name, IO address, or var seq."
        ),
        timeout: float = typer.Option(
            5.0,
            "--timeout",
            min=0.001,
            max=30.0,
            help="Total PLC read budget in seconds. Independent of the HTTP timeout.",
        ),
        topology: str = typer.Option(
            "auto",
            "--topology",
            help="auto, mysql, snapshot, or explicit.",
        ),
        snapshot_path: Optional[str] = typer.Option(
            None, "--snapshot", help="Local topology snapshot used when MySQL is unavailable."
        ),
        endpoint: Optional[str] = typer.Option(
            None, "--endpoint", help="Explicit PLC host or host:port."
        ),
        address: Optional[str] = typer.Option(
            None, "--address", help="Explicit PLC address, for example DB1.DBW0."
        ),
        protocol: Optional[str] = typer.Option(None, "--protocol"),
        rack: Optional[int] = typer.Option(None, "--rack"),
        slot: Optional[int] = typer.Option(None, "--slot"),
        data_type: Optional[str] = typer.Option(None, "--type"),
        port: Optional[int] = typer.Option(None, "--port"),
    ) -> None:
        def operation() -> dict[str, Any]:
            if not str(device or "").strip() or not str(point or "").strip():
                error = CliError(
                    "usage_error",
                    "需要同时提供 --device 和 --point",
                    EXIT_USAGE,
                )
                _mark_direct(error)
                raise error
            return _read_authorized(
                get_context(ctx),
                device=str(device).strip(),
                point=str(point).strip(),
                timeout_ms=_timeout_ms(timeout),
                topology=topology,
                snapshot_path=snapshot_path,
                endpoint=endpoint,
                address=address,
                protocol=protocol,
                rack=rack,
                slot=slot,
                data_type=data_type,
                port=port,
            )

        execute(ctx, "plc get", operation)


def _read_authorized(state: Any, **kwargs: Any) -> dict[str, Any]:
    device = kwargs["device"]
    point = kwargs["point"]
    session = AuthorizedSession(state)
    try:
        entered = session.__enter__()
    except CliError as exc:
        _mark_direct(exc)
        _audit_refusal(exc, actor=None, device=device, point=point, secrets=[])
        raise
    try:
        try:
            user = entered.authorize("plc get")
        except CliError as exc:
            _mark_direct(exc)
            _audit_refusal(
                exc,
                actor=_actor_name(entered),
                device=device,
                point=point,
                secrets=_credential_secrets(entered),
            )
            raise
        actor = str((user or {}).get("userName") or "").strip() or "authenticated"
        try:
            return read_point(
                device,
                point,
                actor=actor,
                timeout_ms=kwargs["timeout_ms"],
                topology=kwargs["topology"],
                snapshot_path=kwargs["snapshot_path"],
                endpoint=kwargs["endpoint"],
                address=kwargs["address"],
                protocol=kwargs["protocol"],
                rack=kwargs["rack"],
                slot=kwargs["slot"],
                data_type=kwargs["data_type"],
                port=kwargs["port"],
            )
        except DirectReadError as exc:
            raise as_cli_error(exc) from exc
    finally:
        session.__exit__(None, None, None)


def _timeout_ms(timeout_seconds: float) -> int:
    return max(1, min(30_000, int(round(float(timeout_seconds) * 1000))))


def _mark_direct(exc: CliError) -> None:
    details = dict(exc.details or {})
    details.setdefault("source", DIRECT_SOURCE)
    if "quality" not in details:
        details["quality"] = QUALITY_NOT_CONNECTED
        details["qualityName"] = quality_name(QUALITY_NOT_CONNECTED)
    exc.details = details


def _audit_refusal(
    exc: CliError,
    *,
    actor: str | None,
    device: str,
    point: str,
    secrets: list[str],
) -> None:
    record_audit(
        actor=actor,
        device=device,
        point=point,
        outcome=exc.error_type,
        error_type=exc.error_type,
        topology_source=None,
        extra_secrets=secrets,
    )


def _actor_name(session: AuthorizedSession) -> str | None:
    try:
        info = session.user_info()
    except Exception:
        return None
    if not isinstance(info, dict):
        return None
    name = str(info.get("userName") or "").strip()
    return name or None


def _credential_secrets(session: AuthorizedSession) -> list[str]:
    credentials = getattr(session, "credentials", None)
    if credentials is None:
        return []
    values = [getattr(credentials, "token", None), getattr(credentials, "refresh_token", None)]
    return [str(value) for value in values if value and len(str(value)) >= 4]
