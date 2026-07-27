"""平台质量码冻结常量（与 writer / backend 同值）。"""

from __future__ import annotations

# 源码可证明
QUALITY_GOOD = 0
QUALITY_UNCERTAIN = 2

# 保留码：允许出现在接口中，但不擅自业务命名
RESERVED_QUALITY_CODES = frozenset({1, 3, 4})

# 新增平台状态（从 5 起冻结）
QUALITY_BAD = 5
QUALITY_STALE = 6
QUALITY_NOT_CONNECTED = 7
QUALITY_CONFIG_DECODE_ERROR = 8

_KNOWN_BUSINESS_CODES = frozenset(
    {
        QUALITY_GOOD,
        QUALITY_UNCERTAIN,
        QUALITY_BAD,
        QUALITY_STALE,
        QUALITY_NOT_CONNECTED,
        QUALITY_CONFIG_DECODE_ERROR,
    }
)


def is_known_quality(code: object) -> bool:
    """是否为已命名的平台业务质量码（不含保留码 1/3/4）；严格非 bool 的 int。"""
    if type(code) is not int:
        return False
    return code in _KNOWN_BUSINESS_CODES


_VALID_QUALITY_CODES = _KNOWN_BUSINESS_CODES | RESERVED_QUALITY_CODES


def is_valid_quality(code: object) -> bool:
    """业务码 ∪ 保留码；严格为非 bool 的 int（拒绝 True/False/浮点）。"""
    if type(code) is not int:
        return False
    return code in _VALID_QUALITY_CODES
