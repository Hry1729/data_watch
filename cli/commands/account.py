from __future__ import annotations

import getpass
import sys
from typing import Callable

import typer

from cli.commands.common import run_authorized
from cli.errors import CliError, EXIT_USAGE


def install_account_commands(
    auth_app: typer.Typer,
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    @auth_app.command("change-password")
    def change_password(
        ctx: typer.Context,
        password_stdin: bool = typer.Option(False, "--password-stdin"),
    ) -> None:
        def operation(session) -> dict[str, bool]:
            old_password, new_password = _read_passwords(password_stdin)
            session.request(
                "POST",
                "/api/user/password/change",
                json_body={
                    "oldPassword": old_password,
                    "newPassword": new_password,
                },
            )
            return {"passwordChanged": True}

        run_authorized(
            ctx,
            "auth change-password",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )


def _read_passwords(password_stdin: bool) -> tuple[str, str]:
    if password_stdin:
        old_password = sys.stdin.readline().rstrip("\r\n")
        new_password = sys.stdin.readline().rstrip("\r\n")
    else:
        try:
            old_password = getpass.getpass("Current password: ", stream=sys.stderr)
            new_password = getpass.getpass("New password: ", stream=sys.stderr)
            repeated = getpass.getpass("Repeat new password: ", stream=sys.stderr)
        except (EOFError, KeyboardInterrupt) as exc:
            raise CliError("usage_error", "未读取到密码", EXIT_USAGE) from exc
        if new_password != repeated:
            raise CliError("usage_error", "两次输入的新密码不一致", EXIT_USAGE)

    if not old_password or not new_password:
        raise CliError("usage_error", "当前密码和新密码不能为空", EXIT_USAGE)
    if len(new_password) < 6:
        raise CliError("usage_error", "新密码长度至少 6 位", EXIT_USAGE)
    return old_password, new_password
