from __future__ import annotations

from dataclasses import dataclass
from typing import Any


EXIT_USAGE = 2
EXIT_AUTH_REQUIRED = 3
EXIT_PERMISSION_DENIED = 4
EXIT_NETWORK = 5
EXIT_API = 6
EXIT_PARTIAL = 7
EXIT_TIMEOUT = 8


@dataclass
class CliError(Exception):
    error_type: str
    message: str
    exit_code: int
    status: int | None = None
    details: dict[str, Any] | None = None

    def __post_init__(self) -> None:
        super().__init__(self.message)

    def to_dict(self) -> dict[str, Any]:
        payload: dict[str, Any] = {
            "type": self.error_type,
            "message": self.message,
        }
        if self.status is not None:
            payload["status"] = self.status
        if self.details:
            payload["details"] = self.details
        return payload
