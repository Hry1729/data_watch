"""Modbus 地址解析。

约定（零基）：
- coil:N / C{N}      → FC1 读线圈，N 为线圈地址
- holding:N / HR{N}  → FC3 读保持寄存器，N 为寄存器地址
"""

from __future__ import annotations

import re

# coil:0 / holding:100
_COLON_RE = re.compile(r"^(coil|holding):(\d+)$", re.IGNORECASE)
# C0 / HR10
_SHORT_RE = re.compile(r"^(C|HR)(\d+)$", re.IGNORECASE)


class AddressParseError(ValueError):
    """地址格式非法。"""


def parse_address(address: str) -> tuple[str, int]:
    """解析 Modbus 地址，返回 (区域, 零基偏移)。"""
    text = (address or "").strip()
    if not text:
        raise AddressParseError("地址不能为空")

    match = _COLON_RE.match(text)
    if match:
        area = match.group(1).lower()
        return area, int(match.group(2))

    match = _SHORT_RE.match(text)
    if match:
        prefix = match.group(1).upper()
        area = "coil" if prefix == "C" else "holding"
        return area, int(match.group(2))

    raise AddressParseError(
        f"不支持的 Modbus 地址 {address!r}，请使用 coil:N / holding:N 或 C{{N}} / HR{{N}}"
    )
