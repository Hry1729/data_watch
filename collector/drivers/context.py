"""外置驱动工厂上下文（驱动 API v1 冻结契约）。"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any, Optional


@dataclass(frozen=True)
class DriverContext:
    """平台注入给外置驱动工厂的只读依赖。"""

    protocol: str
    driver_id: str
    driver_version: str
    driver_api: str
    logger: Any
    read_executor: Any = None
    data_root: Optional[Path] = None
