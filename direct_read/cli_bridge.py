"""把直连错误映射成现有 CLI 退出码。"""

from __future__ import annotations

from cli.errors import (
    EXIT_API,
    EXIT_AUTH_REQUIRED,
    EXIT_NETWORK,
    EXIT_PERMISSION_DENIED,
    EXIT_TIMEOUT,
    EXIT_USAGE,
    CliError,
)

from direct_read.errors import DirectReadError


_EXIT_CODES = {
    "usage_error": EXIT_USAGE,
    "auth_required": EXIT_AUTH_REQUIRED,
    "unauthorized": EXIT_AUTH_REQUIRED,
    "permission_denied": EXIT_PERMISSION_DENIED,
    "timeout": EXIT_TIMEOUT,
    "network_error": EXIT_NETWORK,
}


def as_cli_error(exc: DirectReadError) -> CliError:
    return CliError(
        exc.error_type,
        exc.message,
        _EXIT_CODES.get(exc.error_type, EXIT_API),
        status=exc.status,
        details=dict(exc.details),
    )
