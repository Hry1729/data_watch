from __future__ import annotations

from typing import Any, Callable

import typer

from cli.session import AuthorizedSession


def run_authorized(
    ctx: typer.Context,
    command: str,
    *,
    execute: Callable[[typer.Context, str, Callable[[], Any]], Any],
    get_context: Callable[[typer.Context], Any],
    operation: Callable[[AuthorizedSession], Any],
) -> None:
    def guarded_operation() -> Any:
        with AuthorizedSession(get_context(ctx)) as session:
            session.authorize(command)
            return operation(session)

    execute(ctx, command, guarded_operation)


def compact_params(**values: Any) -> dict[str, Any]:
    return {key: value for key, value in values.items() if value is not None}
