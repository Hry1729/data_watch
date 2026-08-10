from __future__ import annotations

import os
from typing import Literal


BuildRole = Literal["centralized", "edge", "distributed-center"]
_VALID_ROLES = {"centralized", "edge", "distributed-center"}
_MODULES: dict[BuildRole, tuple[str, ...]] = {
    "centralized": ("collector", "writer"),
    "edge": ("storage", "collector", "uploader"),
    "distributed-center": ("ingest", "writer"),
}


def build_role() -> BuildRole:
    value = os.environ.get("DATAWATCH_ROLE", "centralized").strip() or "centralized"
    if value not in _VALID_ROLES:
        raise RuntimeError(f"unsupported DATAWATCH_ROLE: {value}")
    return value  # type: ignore[return-value]


def managed_module_names(role: BuildRole | None = None) -> tuple[str, ...]:
    return _MODULES[role or build_role()]
