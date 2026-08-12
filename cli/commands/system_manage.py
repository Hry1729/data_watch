from __future__ import annotations

import getpass
import sys
from typing import Any, Callable, Optional

import typer

from cli.commands.common import compact_params, run_authorized
from cli.errors import CliError, EXIT_USAGE
from cli.files import load_json_object, require_confirmation


users_app = typer.Typer(help="Manage user accounts.", no_args_is_help=True)
roles_app = typer.Typer(help="Manage roles and grants.", no_args_is_help=True)
grants_app = typer.Typer(help="Inspect and update role grants.", no_args_is_help=True)
menus_app = typer.Typer(help="Manage menus.", no_args_is_help=True)
menu_permissions_app = typer.Typer(
    help="Manage menu permission definitions.", no_args_is_help=True
)


def install_system_management(
    root_app: typer.Typer,
    *,
    execute: Callable,
    get_context: Callable,
) -> None:
    root_app.add_typer(users_app, name="users")
    root_app.add_typer(roles_app, name="roles")
    roles_app.add_typer(grants_app, name="grants")
    root_app.add_typer(menus_app, name="menus")
    menus_app.add_typer(menu_permissions_app, name="permissions")

    _install_user_commands(execute=execute, get_context=get_context)
    _install_role_commands(execute=execute, get_context=get_context)
    _install_menu_commands(execute=execute, get_context=get_context)


def _install_user_commands(*, execute: Callable, get_context: Callable) -> None:
    @users_app.command("list")
    def user_list(
        ctx: typer.Context,
        page: int = typer.Option(1, "--page", min=1),
        page_size: int = typer.Option(20, "--page-size", min=1, max=500),
        username: Optional[str] = typer.Option(None, "--username"),
        gender: Optional[str] = typer.Option(None, "--gender"),
        phone: Optional[str] = typer.Option(None, "--phone"),
        email: Optional[str] = typer.Option(None, "--email"),
        status: Optional[str] = typer.Option(None, "--status"),
    ) -> None:
        run_authorized(
            ctx,
            "users list",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET",
                "/api/user/list",
                params=compact_params(
                    current=page,
                    size=page_size,
                    userName=username,
                    userGender=gender,
                    userPhone=phone,
                    userEmail=email,
                    status=status,
                ),
            ),
        )

    @users_app.command("get")
    def user_get(ctx: typer.Context, user_id: int = typer.Argument(..., min=1)) -> None:
        run_authorized(
            ctx,
            "users get",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request("GET", f"/api/user/{user_id}"),
        )

    @users_app.command("create")
    def user_create(ctx: typer.Context, file: str = typer.Option(..., "--file")) -> None:
        run_authorized(
            ctx,
            "users create",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "POST", "/api/user", json_body=load_json_object(file)
            ),
        )

    @users_app.command("update")
    def user_update(
        ctx: typer.Context,
        user_id: int = typer.Argument(..., min=1),
        file: str = typer.Option(..., "--file"),
    ) -> None:
        run_authorized(
            ctx,
            "users update",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "PUT", f"/api/user/{user_id}", json_body=load_json_object(file)
            ),
        )

    @users_app.command("enable")
    def user_enable(ctx: typer.Context, user_id: int = typer.Argument(..., min=1)) -> None:
        _set_user_status(
            ctx,
            user_id=user_id,
            status="1",
            command="users enable",
            execute=execute,
            get_context=get_context,
        )

    @users_app.command("disable")
    def user_disable(
        ctx: typer.Context,
        user_id: int = typer.Argument(..., min=1),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, str(user_id))
            return session.request(
                "PATCH", f"/api/user/{user_id}/status", json_body={"status": "2"}
            )

        run_authorized(
            ctx,
            "users disable",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @users_app.command("reset-password")
    def user_reset_password(
        ctx: typer.Context,
        user_id: int = typer.Argument(..., min=1),
        password_stdin: bool = typer.Option(False, "--password-stdin"),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, str(user_id))
            password = _read_password(password_stdin, prompt="New password: ")
            return session.request(
                "PATCH",
                f"/api/user/{user_id}/password/reset",
                json_body={"password": password},
            )

        run_authorized(
            ctx,
            "users reset-password",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @users_app.command("delete")
    def user_delete(
        ctx: typer.Context,
        user_id: int = typer.Argument(..., min=1),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, str(user_id))
            return session.request("DELETE", f"/api/user/{user_id}")

        run_authorized(
            ctx,
            "users delete",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )


def _install_role_commands(*, execute: Callable, get_context: Callable) -> None:
    @roles_app.command("list")
    def role_list(
        ctx: typer.Context,
        page: int = typer.Option(1, "--page", min=1),
        page_size: int = typer.Option(20, "--page-size", min=1, max=500),
        name: Optional[str] = typer.Option(None, "--name"),
        code: Optional[str] = typer.Option(None, "--code"),
        description: Optional[str] = typer.Option(None, "--description"),
        enabled: Optional[bool] = typer.Option(None, "--enabled/--disabled"),
        start: Optional[str] = typer.Option(None, "--start"),
        end: Optional[str] = typer.Option(None, "--end"),
    ) -> None:
        run_authorized(
            ctx,
            "roles list",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET",
                "/api/role/list",
                params=compact_params(
                    current=page,
                    size=page_size,
                    roleName=name,
                    roleCode=code,
                    description=description,
                    enabled=enabled,
                    startTime=start,
                    endTime=end,
                ),
            ),
        )

    @roles_app.command("options")
    def role_options(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "roles options",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request("GET", "/api/role/options"),
        )

    @roles_app.command("get")
    def role_get(ctx: typer.Context, role_id: int = typer.Argument(..., min=1)) -> None:
        run_authorized(
            ctx,
            "roles get",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request("GET", f"/api/role/{role_id}"),
        )

    @roles_app.command("create")
    def role_create(ctx: typer.Context, file: str = typer.Option(..., "--file")) -> None:
        run_authorized(
            ctx,
            "roles create",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "POST", "/api/role", json_body=load_json_object(file)
            ),
        )

    @roles_app.command("update")
    def role_update(
        ctx: typer.Context,
        role_id: int = typer.Argument(..., min=1),
        file: str = typer.Option(..., "--file"),
    ) -> None:
        run_authorized(
            ctx,
            "roles update",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "PUT", f"/api/role/{role_id}", json_body=load_json_object(file)
            ),
        )

    @roles_app.command("enable")
    def role_enable(ctx: typer.Context, role_id: int = typer.Argument(..., min=1)) -> None:
        _set_role_status(
            ctx,
            role_id=role_id,
            enabled=True,
            command="roles enable",
            execute=execute,
            get_context=get_context,
        )

    @roles_app.command("disable")
    def role_disable(
        ctx: typer.Context,
        role_id: int = typer.Argument(..., min=1),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, str(role_id))
            return session.request(
                "PATCH",
                f"/api/role/{role_id}/status",
                json_body={"enabled": False},
            )

        run_authorized(
            ctx,
            "roles disable",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @roles_app.command("delete")
    def role_delete(
        ctx: typer.Context,
        role_id: int = typer.Argument(..., min=1),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, str(role_id))
            return session.request("DELETE", f"/api/role/{role_id}")

        run_authorized(
            ctx,
            "roles delete",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @grants_app.command("get")
    def role_grant_get(
        ctx: typer.Context, role_id: int = typer.Argument(..., min=1)
    ) -> None:
        run_authorized(
            ctx,
            "roles grants get",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", f"/api/role/{role_id}/grant"
            ),
        )

    @grants_app.command("set")
    def role_grant_set(
        ctx: typer.Context,
        role_id: int = typer.Argument(..., min=1),
        file: str = typer.Option(..., "--file"),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, str(role_id))
            return session.request(
                "PUT",
                f"/api/role/{role_id}/grant",
                json_body=load_json_object(file),
            )

        run_authorized(
            ctx,
            "roles grants set",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )


def _install_menu_commands(*, execute: Callable, get_context: Callable) -> None:
    @menus_app.command("current")
    def menu_current(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "menus current",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request("GET", "/api/v3/system/menus"),
        )

    @menus_app.command("list")
    def menu_list(ctx: typer.Context) -> None:
        run_authorized(
            ctx,
            "menus list",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", "/api/v3/system/menus/admin"
            ),
        )

    @menus_app.command("create")
    def menu_create(ctx: typer.Context, file: str = typer.Option(..., "--file")) -> None:
        run_authorized(
            ctx,
            "menus create",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "POST", "/api/v3/system/menu", json_body=load_json_object(file)
            ),
        )

    @menus_app.command("update")
    def menu_update(
        ctx: typer.Context,
        menu_id: int = typer.Argument(..., min=1),
        file: str = typer.Option(..., "--file"),
    ) -> None:
        run_authorized(
            ctx,
            "menus update",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "PUT",
                f"/api/v3/system/menu/{menu_id}",
                json_body=load_json_object(file),
            ),
        )

    @menus_app.command("delete")
    def menu_delete(
        ctx: typer.Context,
        menu_id: int = typer.Argument(..., min=1),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, str(menu_id))
            return session.request("DELETE", f"/api/v3/system/menu/{menu_id}")

        run_authorized(
            ctx,
            "menus delete",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )

    @menu_permissions_app.command("list")
    def menu_permission_list(
        ctx: typer.Context, menu_id: int = typer.Argument(..., min=1)
    ) -> None:
        run_authorized(
            ctx,
            "menus permissions list",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "GET", f"/api/v3/system/menu/{menu_id}/permissions"
            ),
        )

    @menu_permissions_app.command("create")
    def menu_permission_create(
        ctx: typer.Context,
        menu_id: int = typer.Argument(..., min=1),
        file: str = typer.Option(..., "--file"),
    ) -> None:
        run_authorized(
            ctx,
            "menus permissions create",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "POST",
                f"/api/v3/system/menu/{menu_id}/permissions",
                json_body=load_json_object(file),
            ),
        )

    @menu_permissions_app.command("update")
    def menu_permission_update(
        ctx: typer.Context,
        menu_id: int = typer.Argument(..., min=1),
        permission_id: int = typer.Argument(..., min=1),
        file: str = typer.Option(..., "--file"),
    ) -> None:
        run_authorized(
            ctx,
            "menus permissions update",
            execute=execute,
            get_context=get_context,
            operation=lambda session: session.request(
                "PUT",
                f"/api/v3/system/menu/{menu_id}/permissions/{permission_id}",
                json_body=load_json_object(file),
            ),
        )

    @menu_permissions_app.command("delete")
    def menu_permission_delete(
        ctx: typer.Context,
        menu_id: int = typer.Argument(..., min=1),
        permission_id: int = typer.Argument(..., min=1),
        confirm: str = typer.Option("", "--confirm"),
    ) -> None:
        def operation(session) -> Any:
            require_confirmation(confirm, str(permission_id))
            return session.request(
                "DELETE",
                f"/api/v3/system/menu/{menu_id}/permissions/{permission_id}",
            )

        run_authorized(
            ctx,
            "menus permissions delete",
            execute=execute,
            get_context=get_context,
            operation=operation,
        )


def _set_user_status(
    ctx: typer.Context,
    *,
    user_id: int,
    status: str,
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
            "PATCH", f"/api/user/{user_id}/status", json_body={"status": status}
        ),
    )


def _set_role_status(
    ctx: typer.Context,
    *,
    role_id: int,
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
            f"/api/role/{role_id}/status",
            json_body={"enabled": enabled},
        ),
    )


def _read_password(password_stdin: bool, *, prompt: str) -> str:
    if password_stdin:
        password = sys.stdin.readline().rstrip("\r\n")
    else:
        try:
            password = getpass.getpass(prompt, stream=sys.stderr)
        except (EOFError, KeyboardInterrupt) as exc:
            raise CliError("usage_error", "未读取到密码", EXIT_USAGE) from exc
    if not password:
        raise CliError("usage_error", "密码不能为空", EXIT_USAGE)
    return password
