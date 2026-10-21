from __future__ import annotations

from typing import Any, Callable

import typer

from cli.errors import CliError, EXIT_USAGE
from cli.update_manager import UpdateManager


def install_update_commands(
    app: typer.Typer,
    *,
    execute: Callable[..., Any],
    get_context: Callable[[typer.Context], Any],
) -> None:
    update_app = typer.Typer(help="Manage signed CLI updates.", no_args_is_help=True)
    app.add_typer(update_app, name="update")

    def manager(ctx: typer.Context) -> tuple[Any, Any, UpdateManager]:
        state = get_context(ctx)
        runtime = state.runtime()
        client = state.client_factory(
            runtime.base_url,
            timeout_seconds=runtime.timeout_seconds,
        ) if hasattr(state, "client_factory") else None
        if client is None:
            from cli.client import BackendClient

            client = BackendClient(
                runtime.base_url,
                timeout_seconds=runtime.timeout_seconds,
            )
        return runtime, client, UpdateManager(client)

    def dry_run(ctx: typer.Context) -> bool:
        return bool(getattr(get_context(ctx), "dry_run", False))

    def preview(command: str, local: list[str], **extra: Any) -> dict[str, Any]:
        """B017：自更新不经 AuthorizedSession，--dry-run 时只返回本地副作用预览。"""
        return {
            "dryRun": True,
            "command": command,
            "remoteEffects": [],
            "localEffects": local,
            **extra,
        }

    @update_app.command("check")
    def check(
        ctx: typer.Context,
        channel: str = typer.Option("stable", "--channel"),
    ) -> None:
        def operation() -> dict[str, Any]:
            runtime, client, updater = manager(ctx)
            try:
                return {"baseUrl": runtime.base_url, **updater.check(channel=channel)}
            finally:
                client.close()

        execute(ctx, "update check", operation)

    @update_app.command("download")
    def download(
        ctx: typer.Context,
        channel: str = typer.Option("stable", "--channel"),
    ) -> None:
        def operation() -> dict[str, Any]:
            runtime, client, updater = manager(ctx)
            try:
                if dry_run(ctx):
                    checked = updater.check(channel=channel)  # 只读 GET
                    return preview(
                        "update download",
                        ["下载并校验更新包到安装目录 downloads/"],
                        baseUrl=runtime.base_url,
                        **checked,
                    )
                return {"baseUrl": runtime.base_url, **updater.download(channel=channel)}
            finally:
                client.close()

        execute(ctx, "update download", operation)

    @update_app.command("apply")
    def apply_update(
        ctx: typer.Context,
        channel: str = typer.Option("stable", "--channel"),
        yes: bool = typer.Option(False, "--yes", help="Confirm the update."),
    ) -> None:
        def operation() -> dict[str, Any]:
            if not (yes or get_context(ctx).assume_yes):
                raise CliError("confirmation_required", "更新需要 --yes 确认", EXIT_USAGE)
            runtime, client, updater = manager(ctx)
            try:
                if dry_run(ctx):
                    checked = updater.check(channel=channel)  # 只读 GET
                    return preview(
                        "update apply",
                        ["下载更新包", "启动二进制交换脚本替换 current，旧版本保留为 previous"],
                        baseUrl=runtime.base_url,
                        **checked,
                    )
                return {"baseUrl": runtime.base_url, **updater.apply(channel=channel)}
            finally:
                client.close()

        execute(ctx, "update apply", operation)

    @update_app.command("rollback")
    def rollback(
        ctx: typer.Context,
        yes: bool = typer.Option(False, "--yes", help="Confirm the rollback."),
    ) -> None:
        def operation() -> dict[str, Any]:
            if not (yes or get_context(ctx).assume_yes):
                raise CliError("confirmation_required", "回滚需要 --yes 确认", EXIT_USAGE)
            if dry_run(ctx):
                return preview(
                    "update rollback",
                    ["启动二进制交换脚本，以 previous 替换 current"],
                    baseUrl=get_context(ctx).runtime().base_url,
                )
            runtime, client, updater = manager(ctx)
            try:
                return {"baseUrl": runtime.base_url, **updater.rollback()}
            finally:
                client.close()

        execute(ctx, "update rollback", operation)
