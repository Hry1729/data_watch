from __future__ import annotations

import getpass
import json
import sys
from dataclasses import dataclass
from typing import Any, Callable, Optional, TypeVar

import typer

from cli.client import BackendClient
from cli.config import ConfigStore, RuntimeSettings
from cli.credentials import CredentialStore, StoredCredentials
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
from cli.permissions import build_capabilities


CLI_VERSION = "0.1.0"
T = TypeVar("T")


@dataclass
class CliContext:
    profile_override: str | None
    api_url_override: str | None
    timeout_override: float | None
    pretty: bool
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
) -> None:
    ctx.obj = CliContext(
        profile_override=profile,
        api_url_override=api_url,
        timeout_override=timeout,
        pretty=pretty,
        config_store=ConfigStore(),
        credential_store=CredentialStore(),
    )


def _context(ctx: typer.Context) -> CliContext:
    state = ctx.find_root().obj
    if not isinstance(state, CliContext):
        raise CliError("internal_error", "CLI 上下文未初始化", EXIT_API)
    return state


def _write_json(payload: dict[str, Any], *, pretty: bool, error: bool = False) -> None:
    text = json.dumps(
        payload,
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
    except CliError as exc:
        _write_json(
            {"ok": False, "command": command, "error": exc.to_dict()},
            pretty=state.pretty,
            error=True,
        )
        raise typer.Exit(exc.exit_code) from exc
    except Exception as exc:
        error = CliError("internal_error", "CLI 执行失败", EXIT_API)
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
        error = CliError("internal_error", "CLI 执行失败", EXIT_API)
        _write_json(
            {"ok": False, "command": command, "error": error.to_dict()},
            pretty=False,
            error=True,
        )
        raise typer.Exit(error.exit_code) from exc

def _load_credentials(state: CliContext, runtime: RuntimeSettings) -> StoredCredentials:
    credentials = state.credential_store.load(runtime.profile)
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
            },
        }

    _execute(ctx, "config show", operation)


@config_app.command("set-url")
def config_set_url(ctx: typer.Context, url: str = typer.Argument(...)) -> None:
    def operation() -> dict[str, Any]:
        state = _context(ctx)
        return state.config_store.set_url(url, profile=state.profile_override)

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
        )
        return {
            "profile": runtime.profile,
            "baseUrl": runtime.base_url,
            "user": user_info,
        }

    _execute(ctx, "auth login", operation)


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


@app.command("version")
def version(ctx: typer.Context) -> None:
    _execute(ctx, "version", lambda: {"version": CLI_VERSION})


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
    _configure_standard_streams()
    app()


if __name__ == "__main__":
    main()
