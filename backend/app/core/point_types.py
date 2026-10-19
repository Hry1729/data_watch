"""点位数据类型口径（全系统单一来源）。

问题
----
库里的 ``device_variable.var_type`` 写法和界面显示的"数据类型"不是一回事：
OPC 点位常写成 ``Boolean``，S7 字节点位写成 ``BYTE``。以前中心只认 7 个
可创建类型，其余一律回落成 STRING，于是 Boolean/BYTE 在中心被显示成
STRING，点位列表的类型筛选也把它们当成 STRING。

口径
----
* **显示口径**：只归一别名，``BOOLEAN``→``BOOL``、``FLOAT``→``REAL``
  （两者在 S7 驱动里是同一种 4 字节浮点解码）。其余类型按原样显示，
  未识别类型不再伪装成 STRING。
* **筛选口径**：必须和显示口径完全一致，否则筛出来的和显示的对不上。
* 不改库里的 var_type，不改底层解码规则，不改 TDengine 列类型。

``CREATABLE_DATA_TYPES`` 是**可新建/编辑**的物理类型白名单，与显示口径无关：
"创建后不可修改点位物理类型"的约束由它维持，不要为了显示口径放宽它。
"""

from __future__ import annotations

from typing import Any

# 显示口径别名：库里的写法 -> 界面上显示的类型
DISPLAY_ALIASES = {
    "BOOLEAN": "BOOL",
    "FLOAT": "REAL",
}

# 可通过 UI 新建/编辑的物理类型白名单
CREATABLE_DATA_TYPES = ("BOOL", "INT", "DINT", "REAL", "STRING", "WORD", "DWORD")


def normalize_data_type(value: Any) -> str:
    """把库里的 var_type 归一成显示口径。"""
    normalized = "" if value is None else str(value).strip().upper()
    if not normalized:
        return "STRING"
    return DISPLAY_ALIASES.get(normalized, normalized)


def data_type_sql_expression(column: str) -> str:
    """生成与 normalize_data_type 同口径的 SQL 表达式。

    筛选和显示必须用同一套口径，否则 BYTE/Boolean 会被当成 STRING 筛出来。
    """
    base = f"UPPER(COALESCE(NULLIF(TRIM({column}), ''), 'STRING'))"
    cases = " ".join(
        f"WHEN '{alias}' THEN '{canonical}'"
        for alias, canonical in DISPLAY_ALIASES.items()
    )
    return f"CASE {base} {cases} ELSE {base} END"
