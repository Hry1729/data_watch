from __future__ import annotations

import getpass
import json
import sys
from dataclasses import dataclass
from typing import Any, Callable, Optional, TypeVar
from uuid import uuid4

import jmespath
import typer
from jmespath.exceptions import JMESPathError
from typer._click.exceptions import ClickException as TyperClickException

from cli.client import BackendClient
from cli.config import ConfigStore, RuntimeSettings, is_insecure_transport
from cli.credentials import CredentialStore, StoredCredentials
from cli.files import set_confirmation_bypass
from cli.errors import (
    CliError,
    EXIT_API,
    EXIT_AUTH_REQUIRED,
    EXIT_PARTIAL,
    EXIT_USAGE,
)
from cli.commands.account import install_account_commands
from cli.commands.catalog import install_catalog_commands
from cli.commands.catalog_manage import install_catalog_management
from cli.commands.contracts import install_contract_commands
from cli.commands.driver_manage import install_driver_management
from cli.commands.dashboard import install_dashboard_commands
from cli.commands.diagnostics import install_diagnostic_commands
from cli.commands.infrastructure import install_infrastructure_commands
from cli.commands.history import install_history_commands
from cli.commands.history_manage import install_history_management
from cli.commands.log_manage import install_log_management
from cli.commands.node_manage import install_node_management
from cli.commands.realtime import install_realtime_commands
from cli.commands.system_manage import install_system_management
from cli.commands.update import install_update_commands
from cli.permissions import build_capabilities
from cli.version import CLI_VERSION, OUTPUT_SCHEMA_VERSION


T = TypeVar("T")
_CURRENT_REQUEST_ID: str | None = None
_CURRENT_DEBUG = False


@dataclass
class CliContext:
    profile_override: str | None
    api_url_override: str | None
    timeout_override: float | None
    pretty: bool
    debug: bool
    dry_run: bool
    select: str | None
    idempotency_key: str | None
    assume_yes: bool
    all_pages: bool
    max_items: int
    request_id: str
    config_store: ConfigStore
    credential_store: CredentialStore

    def runtime(self) -> RuntimeSettings:
        return self.config_store.resolve(
            profile_override=self.profile_override,
            api_url_override=self.api_url_override,
            timeout_override=self.timeout_override,
        )


app = typer.Typer(
    name="data-watch",
    help="DataWatch Backend command-line client.",
    no_args_is_help=True,
    pretty_exceptions_enable=False,
    rich_markup_mode=None,
)
config_app = typer.Typer(help="Manage CLI connection profiles.", no_args_is_help=True)
profile_app = typer.Typer(help="Manage named profiles.", no_args_is_help=True)
auth_app = typer.Typer(help="Authenticate and inspect account access.", no_args_is_help=True)
system_app = typer.Typer(help="Inspect Backend system health.", no_args_is_help=True)

app.add_typer(config_app, name="config")
config_app.add_typer(profile_app, name="profile")
app.add_typer(auth_app, name="auth")
app.add_typer(system_app, name="system")


@app.callback()
def root(
    ctx: typer.Context,
    profile: Optional[str] = typer.Option(None, "--profile", help="Use a named profile."),
    api_url: Optional[str] = typer.Option(
        None,
        "--api-url",
        help="Override the Backend URL for this invocation.",
    ),
    timeout: Optional[float] = typer.Option(
        None,
        "--timeout",
        min=0.001,
        max=300.0,
        help="Override the HTTP timeout in seconds.",
    ),
    pretty: bool = typer.Option(False, "--pretty", help="Pretty-print JSON output."),
    debug: bool = typer.Option(False, "--debug", help="Include exception types in errors."),
    dry_run: bool = typer.Option(False, "--dry-run", help="Resolve and validate writes without sending them."),
    select: Optional[str] = typer.Option(None, "--select", help="JMESPath projection for success data."),
    idempotency_key: Optional[str] = typer.Option(None, "--idempotency-key", help="Trace key forwarded with writes."),
    assume_yes: bool = typer.Option(False, "--yes", help="Confirm high-risk commands."),
    all_pages: bool = typer.Option(False, "--all-pages", help="Fetch all pages for paginated list commands."),
    max_items: int = typer.Option(10000, "--max-items", min=1, max=100000),
) -> None:
    global _CURRENT_DEBUG, _CURRENT_REQUEST_ID
    request_id = str(uuid4())
    _CURRENT_DEBUG = debug
    _CURRENT_REQUEST_ID = request_id
    set_confirmation_bypass(assume_yes)
    ctx.obj = CliContext(
        profile_override=profile,
        api_url_override=api_url,
        timeout_override=timeout,
        pretty=pretty,
        debug=debug,
        dry_run=dry_run,
        select=select,
        idempotency_key=idempotency_key,
        assume_yes=assume_yes,
        all_pages=all_pages,
        max_items=max_items,
        request_id=request_id,
        config_store=ConfigStore(),
        credential_store=CredentialStore(),
    )


def _context(ctx: typer.Context) -> CliContext:
    state = ctx.find_root().obj
    if not isinstance(state, CliContext):
        raise CliError("internal_error", "CLI 上下文未初始化", EXIT_API)
    return state


def _write_json(payload: dict[str, Any], *, pretty: bool, error: bool = False) -> None:
    enriched = {
        "schemaVersion": OUTPUT_SCHEMA_VERSION,
        "cliVersion": CLI_VERSION,
        "requestId": _CURRENT_REQUEST_ID or str(uuid4()),
        **payload,
    }
    text = json.dumps(
        enriched,
        ensure_ascii=False,
        indent=2 if pretty else None,
        separators=None if pretty else (",", ":"),
    )
    stream = sys.stderr if error else sys.stdout
    stream.write(text + "\n")


def _execute(ctx: typer.Context, command: str, operation: Callable[[], T]) -> T:
    state = _context(ctx)
    try:
        data = operation()
        data = _select_data(state, data)
    except CliError as exc:
        _write_json(
            {"ok": False, "command": command, "error": exc.to_dict()},
            pretty=state.pretty,
            error=True,
        )
        raise typer.Exit(exc.exit_code) from exc
    except Exception as exc:
        error = CliError(
            "internal_error",
            "CLI 执行失败",
            EXIT_API,
            details={"exceptionType": type(exc).__name__} if state.debug else None,
        )
        _write_json(
            {"ok": False, "command": command, "error": error.to_dict()},
            pretty=state.pretty,
            error=True,
        )
        raise typer.Exit(error.exit_code) from exc

    _write_json(
        {"ok": True, "command": command, "data": data},
        pretty=state.pretty,
    )
    return data


def _execute_stream(
    ctx: typer.Context,
    command: str,
    operation: Callable[[], Any],
) -> None:
    state = _context(ctx)
    try:
        for data in operation():
            data = _select_data(state, data)
            _write_json(
                {"ok": True, "command": command, "data": data},
                pretty=False,
            )
    except KeyboardInterrupt as exc:
        error = CliError("cancelled", "实时订阅已取消", 130)
        _write_json(
            {"ok": False, "command": command, "error": error.to_dict()},
            pretty=False,
            error=True,
        )
        raise typer.Exit(error.exit_code) from exc
    except CliError as exc:
        _write_json(
            {"ok": False, "command": command, "error": exc.to_dict()},
            pretty=False,
            error=True,
        )
        raise typer.Exit(exc.exit_code) from exc
    except Exception as exc:
        error = CliError(
            "internal_error",
            "CLI 执行失败",
            EXIT_API,
            details={"exceptionType": type(exc).__name__} if state.debug else None,
        )
        _write_json(
            {"ok": False, "command": command, "error": error.to_dict()},
            pretty=False,
            error=True,
        )
        raise typer.Exit(error.exit_code) from exc

def _select_data(state: CliContext, data: Any) -> Any:
    if not state.select:
        return data
    try:
        return jmespath.search(state.select, data)
    except JMESPathError as exc:
        raise CliError(
            "usage_error",
            f"--select 表达式无效: {exc}",
            EXIT_USAGE,
        ) from exc


def _load_credentials(state: CliContext, runtime: RuntimeSettings) -> StoredCredentials:
    credentials = state.credential_store.load(
        runtime.profile,
        expected_base_url=runtime.base_url,
    )
    if credentials is None:
        raise CliError("auth_required", "请先执行 auth login", EXIT_AUTH_REQUIRED)
    return credentials


def _read_password(password_stdin: bool) -> str:
    if password_stdin:
        password = sys.stdin.read().rstrip("\r\n")
    else:
        try:
            password = getpass.getpass("Password: ", stream=sys.stderr)
        except (EOFError, KeyboardInterrupt) as exc:
            raise CliError("usage_error", "未读取到密码", EXIT_USAGE) from exc
    if not password:
        raise CliError("usage_error", "密码不能为空", EXIT_USAGE)
    return password


@config_app.command("show")
def config_show(ctx: typer.Context) -> None:
    def operation() -> dict[str, Any]:
        state = _context(ctx)
        runtime = state.runtime()
        return {
            **state.config_store.public_document(),
            "runtime": {
                "profile": runtime.profile,
                "baseUrl": runtime.base_url,
                "timeoutSeconds": runtime.timeout_seconds,
                "insecureTransport": is_insecure_transport(runtime.base_url),
            },
        }

    _execute(ctx, "config show", operation)


@config_app.command("set-url")
def config_set_url(ctx: typer.Context, url: str = typer.Argument(...)) -> None:
    def operation() -> dict[str, Any]:
        state = _context(ctx)
        document = state.config_store.public_document()
        profile = state.profile_override or str(document["currentProfile"])
        if profile not in document["profiles"]:
            raise CliError("config_error", f"Profile 不存在: {profile}", EXIT_USAGE)
        old_url = str(document["profiles"][profile]["baseUrl"])
        result = state.config_store.set_url(url, profile=profile)
        new_url = str(result["profiles"][profile]["baseUrl"])
        credentials_removed = (
            state.credential_store.clear(profile) if old_url != new_url else False
        )
        return {**result, "credentialsRemoved": credentials_removed}

    _execute(ctx, "config set-url", operation)


@profile_app.command("list")
def profile_list(ctx: typer.Context) -> None:
    def operation() -> dict[str, Any]:
        document = _context(ctx).config_store.public_document()
        current = document["currentProfile"]
        profiles = [
            {"name": name, "current": name == current, **settings}
            for name, settings in document["profiles"].items()
        ]
        return {"currentProfile": current, "profiles": profiles}

    _execute(ctx, "config profile list", operation)


@profile_app.command("create")
def profile_create(
    ctx: typer.Context,
    name: str = typer.Argument(...),
    url: str = typer.Option(..., "--url", help="Backend HTTP(S) URL."),
    timeout: float = typer.Option(15.0, "--timeout", min=0.001, max=300.0),
) -> None:
    _execute(
        ctx,
        "config profile create",
        lambda: _context(ctx).config_store.create_profile(
            name,
            url,
            timeout_seconds=timeout,
        ),
    )


@profile_app.command("update")
def profile_update(
    ctx: typer.Context,
    name: str = typer.Argument(...),
    url: Optional[str] = typer.Option(None, "--url"),
    timeout: Optional[float] = typer.Option(None, "--timeout", min=0.001, max=300.0),
) -> None:
    def operation() -> dict[str, Any]:
        state = _context(ctx)
        before = state.config_store.public_document()
        old_url = str(before["profiles"].get(name, {}).get("baseUrl") or "")
        result = state.config_store.update_profile(
            name,
            url=url,
            timeout_seconds=timeout,
        )
        new_url = str(result["profiles"][name]["baseUrl"])
        credentials_removed = (
            state.credential_store.clear(name) if old_url and old_url != new_url else False
        )
        return {**result, "credentialsRemoved": credentials_removed}

    _execute(ctx, "config profile update", operation)


@profile_app.command("delete")
def profile_delete(ctx: typer.Context, name: str = typer.Argument(...)) -> None:
    def operation() -> dict[str, Any]:
        state = _context(ctx)
        result = state.config_store.delete_profile(name)
        return {
            **result,
            "credentialsRemoved": state.credential_store.clear(name),
        }

    _execute(ctx, "config profile delete", operation)


@profile_app.command("use")
def profile_use(ctx: typer.Context, name: str = typer.Argument(...)) -> None:
    _execute(
        ctx,
        "config profile use",
        lambda: _context(ctx).config_store.use_profile(name),
    )


@auth_app.command("login")
def auth_login(
    ctx: typer.Context,
    username: str = typer.Option(..., "--username", "-u"),
    password_stdin: bool = typer.Option(
        False,
        "--password-stdin",
        help="Read the password from standard input.",
    ),
) -> None:
    def operation() -> dict[str, Any]:
        state = _context(ctx)
        runtime = state.runtime()
        password = _read_password(password_stdin)
        with BackendClient(
            runtime.base_url,
            timeout_seconds=runtime.timeout_seconds,
        ) as client:
            login_data = client.login(username, password)
            token = str(login_data["token"])
            client.token = token
            user_info = client.get_user_info()

        state.credential_store.save(
            runtime.profile,
            token=token,
            refresh_token=str(login_data.get("refreshToken") or "").strip() or None,
            user_name=str(user_info.get("userName") or username),
            base_url=runtime.base_url,
        )
        return {
            "profile": runtime.profile,
            "baseUrl": runtime.base_url,
            "insecureTransport": is_insecure_transport(runtime.base_url),
            "user": user_info,
        }

    _execute(ctx, "auth login", operation)


@auth_app.command("refresh")
def auth_refresh(ctx: typer.Context) -> None:
    def operation() -> dict[str, Any]:
        state = _context(ctx)
        runtime = state.runtime()
        credentials = _load_credentials(state, runtime)
        with BackendClient(
            runtime.base_url,
            token=credentials.token,
            refresh_token=credentials.refresh_token,
            timeout_seconds=runtime.timeout_seconds,
        ) as client:
            client.refresh()
            user_info = client.get_user_info()
            if credentials.source == "keyring":
                state.credential_store.save(
                    runtime.profile,
                    token=str(client.token),
                    refresh_token=client.refresh_token,
                    user_name=str(user_info.get("userName") or credentials.user_name or ""),
                    base_url=runtime.base_url,
                )
        return {
            "profile": runtime.profile,
            "baseUrl": runtime.base_url,
            "credentialSource": credentials.source,
            "rotated": True,
            "user": user_info,
        }

    _execute(ctx, "auth refresh", operation)


@auth_app.command("whoami")
def auth_whoami(ctx: typer.Context) -> None:
    def operation() -> dict[str, Any]:
        state = _context(ctx)
        runtime = state.runtime()
        credentials = _load_credentials(state, runtime)
        with BackendClient(
            runtime.base_url,
            token=credentials.token,
            timeout_seconds=runtime.timeout_seconds,
        ) as client:
            user_info = client.get_user_info()
        return {
            "profile": runtime.profile,
            "baseUrl": runtime.base_url,
            "credentialSource": credentials.source,
            "credentialBaseUrl": credentials.base_url,
            "user": user_info,
        }

    _execute(ctx, "auth whoami", operation)


@auth_app.command("capabilities")
def auth_capabilities(ctx: typer.Context) -> None:
    def operation() -> dict[str, Any]:
        state = _context(ctx)
        runtime = state.runtime()
        credentials = _load_credentials(state, runtime)
        with BackendClient(
            runtime.base_url,
            token=credentials.token,
            timeout_seconds=runtime.timeout_seconds,
        ) as client:
            user_info = client.get_user_info()
        return {
            "profile": runtime.profile,
            **build_capabilities(user_info),
        }

    _execute(ctx, "auth capabilities", operation)


@auth_app.command("logout")
def auth_logout(ctx: typer.Context) -> None:
    def operation() -> dict[str, Any]:
        state = _context(ctx)
        runtime = state.runtime()
        credentials = _load_credentials(state, runtime)
        remote_error: CliError | None = None
        try:
            with BackendClient(
                runtime.base_url,
                token=credentials.token,
                timeout_seconds=runtime.timeout_seconds,
            ) as client:
                client.logout()
        except CliError as exc:
            remote_error = exc

        credentials_removed = False
        if credentials.source == "keyring":
            credentials_removed = state.credential_store.clear(runtime.profile)

        if remote_error is not None:
            raise CliError(
                "partial_failure",
                "本地凭据已处理，但 Backend 注销失败",
                EXIT_PARTIAL,
                details={
                    "credentialsRemoved": credentials_removed,
                    "remoteError": remote_error.to_dict(),
                },
            )

        return {
            "profile": runtime.profile,
            "credentialsRemoved": credentials_removed,
            "environmentTokenActive": credentials.source == "environment",
        }

    _execute(ctx, "auth logout", operation)


@system_app.command("compatibility")
def system_compatibility(ctx: typer.Context) -> None:
    def operation() -> dict[str, Any]:
        runtime = _context(ctx).runtime()
        with BackendClient(
            runtime.base_url,
            timeout_seconds=runtime.timeout_seconds,
        ) as client:
            compatibility = client.compatibility()
        return {
            "profile": runtime.profile,
            "baseUrl": runtime.base_url,
            "insecureTransport": is_insecure_transport(runtime.base_url),
            "local": {
                "cliVersion": CLI_VERSION,
                "outputSchemaVersion": OUTPUT_SCHEMA_VERSION,
            },
            "backend": compatibility,
        }

    _execute(ctx, "system compatibility", operation)


@system_app.command("health")
def system_health(ctx: typer.Context) -> None:
    def operation() -> dict[str, Any]:
        runtime = _context(ctx).runtime()
        with BackendClient(
            runtime.base_url,
            timeout_seconds=runtime.timeout_seconds,
        ) as client:
            health = client.health()
        return {
            "profile": runtime.profile,
            "baseUrl": runtime.base_url,
            "health": health,
        }

    _execute(ctx, "system health", operation)


@app.command("doctor")
def doctor(ctx: typer.Context) -> None:
    def operation() -> dict[str, Any]:
        state = _context(ctx)
        runtime = state.runtime()
        checks: dict[str, Any] = {
            "configuration": {
                "ok": True,
                "profile": runtime.profile,
                "baseUrl": runtime.base_url,
                "insecureTransport": is_insecure_transport(runtime.base_url),
            }
        }
        with BackendClient(
            runtime.base_url,
            timeout_seconds=runtime.timeout_seconds,
        ) as client:
            checks["backend"] = {"ok": True, "health": client.health()}
            checks["compatibility"] = {"ok": True, "data": client.compatibility()}

        try:
            credentials = state.credential_store.load(
                runtime.profile,
                expected_base_url=runtime.base_url,
            )
            if credentials is None:
                checks["authentication"] = {"ok": False, "status": "not_logged_in"}
            else:
                with BackendClient(
                    runtime.base_url,
                    token=credentials.token,
                    timeout_seconds=runtime.timeout_seconds,
                ) as client:
                    user_info = client.get_user_info()
                checks["authentication"] = {
                    "ok": True,
                    "credentialSource": credentials.source,
                    "userName": user_info.get("userName"),
                }
        except CliError as exc:
            checks["authentication"] = {"ok": False, "error": exc.to_dict()}

        return {
            "overallOk": checks["backend"]["ok"] and checks["compatibility"]["ok"],
            "checks": checks,
        }

    result = _execute(ctx, "doctor", operation)
    # B1：`doctor` 用 payload 表达失败（`overallOk` / 各项 `ok`），而生产入口只认退出码——
    # 若不在此转换，失败时 shell 仍看到 0，即"显式失败退出码在生产入口丢失"。
    # 注意**不能**把 `_execute` 的返回值整体当作退出码：它是业务 payload（dict），
    # `SystemExit(payload)` 会被当成异常信息打印并退 1。此处先由 `_execute` 输出 JSON 包络
    # （保持"stdout 单一包络"），再**按失败项**抛出对应退出码。
    # 真正不可达时 `client.health()` 已抛 `CliError` 并由 `_execute` 映射，不会走到这里。
    checks = (result or {}).get("checks") or {}
    if not (checks.get("authentication") or {}).get("ok"):
        raise typer.Exit(EXIT_AUTH_REQUIRED)
    if not (result or {}).get("overallOk"):
        raise typer.Exit(EXIT_API)


@app.command("version")
def version(ctx: typer.Context) -> None:
    _execute(
        ctx,
        "version",
        lambda: {
            "version": CLI_VERSION,
            "outputSchemaVersion": OUTPUT_SCHEMA_VERSION,
        },
    )


install_contract_commands(app, execute=_execute)
install_dashboard_commands(app, execute=_execute, get_context=_context)
install_catalog_commands(app, execute=_execute, get_context=_context)
install_history_commands(app, execute=_execute, get_context=_context)
install_diagnostic_commands(app, execute=_execute, get_context=_context)
install_infrastructure_commands(app, execute=_execute, get_context=_context)
install_catalog_management(execute=_execute, get_context=_context)
install_history_management(execute=_execute, get_context=_context)
install_driver_management(execute=_execute, get_context=_context)
install_node_management(execute=_execute, get_context=_context)
install_log_management(execute=_execute, get_context=_context)
install_system_management(app, execute=_execute, get_context=_context)
install_account_commands(auth_app, execute=_execute, get_context=_context)
install_update_commands(app, execute=_execute, get_context=_context)
install_realtime_commands(
    app,
    execute=_execute,
    execute_stream=_execute_stream,
    get_context=_context,
)


def _configure_standard_streams() -> None:
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if callable(reconfigure):
            reconfigure(encoding="utf-8")


def main() -> None:
    global _CURRENT_DEBUG, _CURRENT_REQUEST_ID
    _configure_standard_streams()
    _CURRENT_REQUEST_ID = str(uuid4())
    _CURRENT_DEBUG = "--debug" in sys.argv[1:]
    pretty = "--pretty" in sys.argv[1:]
    try:
        app(standalone_mode=False)
    except typer.Exit as exc:
        raise SystemExit(exc.exit_code) from exc
    except typer.Abort as exc:
        error = CliError("usage_error", "操作已取消", EXIT_USAGE)
        _write_json(
            {"ok": False, "command": "cli", "error": error.to_dict()},
            pretty=pretty,
            error=True,
        )
        raise SystemExit(error.exit_code) from exc
    except TyperClickException as exc:
        error = CliError("usage_error", exc.format_message(), EXIT_USAGE)
        _write_json(
            {"ok": False, "command": "cli", "error": error.to_dict()},
            pretty=pretty,
            error=True,
        )
        raise SystemExit(error.exit_code) from exc


if __name__ == "__main__":
    main()
