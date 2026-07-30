"""Collector 自身版本号。

外置驱动可在 driver.toml 声明 min_collector_version，加载时与此常量比对。
与 installer/DataWatch.iss 的 MyAppVersion 同步维护——两者代表同一个产品版本。
"""

from __future__ import annotations

__version__ = "0.1.0"


def parse_semver(value: str) -> tuple[int, int, int]:
    """解析 SemVer X.Y.Z 为可比较的整数三元组。"""
    parts = value.split(".")
    if len(parts) != 3:
        raise ValueError(f"不是合法的 SemVer X.Y.Z: {value!r}")
    return (int(parts[0]), int(parts[1]), int(parts[2]))


def current_version_tuple() -> tuple[int, int, int]:
    return parse_semver(__version__)
