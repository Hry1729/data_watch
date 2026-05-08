"""辅助函数"""

import re
from typing import Any, Dict, List, Tuple

_DB_ADDRESS_RE = re.compile(
    r"^DB(?P<db>\d+)\.(?P<access>DBX|DBB|DBW|DBD)(?P<byte>\d+)(?:\.(?P<bit>\d+))?$",
    re.IGNORECASE,
)
_S7_AREA_RE = re.compile(
    r"^%?(?P<area>I|Q|M)(?P<width>B|W|D)?(?P<byte>\d+)(?:\.(?P<bit>\d+))?$",
    re.IGNORECASE,
)


def merge_continuous_blocks(
    variables: List[Dict[str, Any]],
) -> List[Tuple[str, int]]:
    """
    将连续地址的变量合并为读取块
    Args:
        variables: [{"address": "DB398.DBD0", ...}, ...]
    Returns:
        [(start_address, count), ...]
    """
    if not variables:
        return []

    # 按地址类型和偏移量排序
    sorted_vars = sorted(variables, key=lambda x: (
        x.get("area", ""),
        x.get("offset", 0)
    ))

    merged = []
    current_block = None

    for var in sorted_vars:
        area = var.get("area", "")
        offset = var.get("offset", 0)

        if current_block is None:
            current_block = {
                "start_address": f"{area}{offset}",
                "count": 1,
                "area": area,
                "start_offset": offset,
                "data_type": var.get("data_type", "INT")
            }
        else:
            # 检查是否连续且类型相同
            if (area == current_block["area"] and
                offset == current_block["start_offset"] + current_block["count"] and
                var.get("data_type", "INT") == current_block["data_type"]):
                # 合并到当前块
                current_block["count"] += 1
            else:
                # 保存当前块，开始新块
                merged.append((
                    current_block["start_address"],
                    current_block["count"],
                    current_block["data_type"]
                ))
                current_block = {
                    "start_address": f"{area}{offset}",
                    "count": 1,
                    "area": area,
                    "start_offset": offset,
                    "data_type": var.get("data_type", "INT")
                }

    # 保存最后一个块
    if current_block:
        merged.append((
            current_block["start_address"],
            current_block["count"],
            current_block["data_type"]
        ))

    return merged


def parse_address(address: str) -> Dict[str, Any]:
    """
    解析地址字符串
    Examples:
        "DB1.DBX0.0" -> {"type": "DB", "db": 1, "access": "DBX", "offset": 0, "bit": 0}
        "DB100.DBB10" -> {"type": "DB", "db": 100, "access": "DBB", "offset": 10}
        "%R100" -> {"type": "R", "offset": 100}
    """
    addr = address.upper()

    if addr.startswith("DB"):
        match = _DB_ADDRESS_RE.match(addr)
        if not match:
            return {"type": "UNKNOWN", "raw": address}
        return {
            "type": "DB",
            "db": int(match.group("db")),
            "access": match.group("access").upper(),
            "offset": int(match.group("byte")),
            "bit": int(match.group("bit")) if match.group("bit") is not None else None,
        }
    area_match = _S7_AREA_RE.match(addr)
    if area_match:
        return {
            "type": area_match.group("area").upper(),
            "width": (area_match.group("width") or "").upper() or None,
            "offset": int(area_match.group("byte")),
            "bit": int(area_match.group("bit")) if area_match.group("bit") is not None else None,
        }
    elif addr.startswith("%R"):
        return {"type": "R", "offset": int(addr[2:])}
    elif addr.startswith("%AI"):
        return {"type": "AI", "offset": int(addr[3:]) - 1}
    elif addr.startswith("%Q"):
        return {"type": "Q", "offset": int(addr[2:])}
    elif addr.startswith("%I"):
        return {"type": "I", "offset": int(addr[2:])}
    elif addr.startswith("M"):
        return {"type": "M", "offset": int(addr[1:])}
    else:
        return {"type": "UNKNOWN", "raw": address}
