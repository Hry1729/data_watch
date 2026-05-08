"""数据验证器"""

from typing import Any

from core.exceptions import CollectorError

VALID_DATA_TYPES = {"BOOL", "BYTE", "INT", "WORD", "REAL", "DINT", "DWORD", "FLOAT", "STRING"}


def validate_address(address: str) -> bool:
    """验证地址格式"""
    if not address or not isinstance(address, str):
        return False
    # 基本格式检查：不能为空，至少包含字母和数字
    return any(c.isalpha() for c in address) and any(c.isdigit() for c in address)


def validate_value(value: Any, data_type: str) -> bool:
    """验证值与数据类型是否匹配"""
    if value is None:
        return False

    dt = data_type.upper()
    if dt not in VALID_DATA_TYPES:
        return False

    try:
        if dt in ("BOOL",):
            return isinstance(value, (bool, int))
        elif dt in ("INT", "WORD", "DINT", "DWORD", "BYTE"):
            return isinstance(value, int)
        elif dt in ("REAL", "FLOAT"):
            return isinstance(value, (int, float))
        elif dt == "STRING":
            return isinstance(value, str)
    except (ValueError, TypeError):
        return False

    return True


def validate_device_id(device_id: str) -> bool:
    """验证设备ID"""
    if not device_id or not isinstance(device_id, str):
        return False
    return len(device_id) <= 64 and device_id.isidentifier()


def validate_variable_id(variable_id: str) -> bool:
    """验证变量ID"""
    if not variable_id or not isinstance(variable_id, str):
        return False
    return len(variable_id) <= 64
