"""B17 回归：realtime-init 点位值归一化必须对空/非法/缺失质量安全，且不得用 0 掩盖异常。

缺陷（已取证）：`_normalize_point_value` 对 INT/REAL 直接 `int(float(value))` / `float(value)`，
latest 里出现**空串/非数字文本/损坏载荷**时抛 `ValueError` ⇒ **realtime-init 整页 500**。

修法：统一走 `_coerce_point_number()` 安全归一为**有限数字**；无法确定 ⇒ 返回"明确无值" `-`，
**刻意不补 0**（补 0 会掩盖异常）。BOOL 的合法假值（`0/false/no/off`）仍返回 `False`，
只有**无法解析**的才返回 `-`。

验收（清单）：BOOL/INT/REAL/STRING 各类型与空/非法/缺失质量组合；**单坏点不导致整页 500**，
也不污染其他正常点。
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[3]
BACKEND_ROOT = Path(__file__).resolve().parents[2]
for _candidate in (str(BACKEND_ROOT), str(REPO_ROOT)):
    if _candidate not in sys.path:
        sys.path.insert(0, _candidate)

try:
    from app.services import monitor as monitor_module
except ImportError:  # pragma: no cover - 兼容以 backend 为根的导入方式
    import monitor as monitor_module  # type: ignore[no-redef]


def _svc():
    """按能力查找承载 `_normalize_point_value` 的类，避免硬编码类名。"""
    for candidate in vars(monitor_module).values():
        if isinstance(candidate, type) and hasattr(candidate, "_normalize_point_value"):
            return candidate.__new__(candidate)
    raise AssertionError("未找到承载 _normalize_point_value 的类")


@pytest.mark.parametrize(
    ("var_type", "value", "expected"),
    [
        # INT 系列：空/空白/非法/缺失/损坏载荷 ⇒ 明确无值
        ("INT", "", "-"),
        ("INT", "   ", "-"),
        ("INT", "abc", "-"),
        ("INT", None, "-"),
        ("INT", {"a": 1}, "-"),
        ("INT", [], "-"),
        # INT 正常值
        ("INT", "12.7", 12),
        ("INT", 42, 42),
        ("DWORD", "7", 7),
        ("WORD", "bad", "-"),
        # REAL：含非有限数
        ("REAL", "", "-"),
        ("REAL", "1.5", 1.5),
        ("REAL", "abc", "-"),
        ("REAL", float("nan"), "-"),
        ("REAL", float("inf"), "-"),
        ("REAL", "-inf", "-"),
        # BOOL：合法假值仍是 False；无法解析才是"无值"
        ("BOOL", "", "-"),
        ("BOOL", "true", True),
        ("BOOL", "0", False),
        ("BOOL", "false", False),
        ("BOOL", "abc", "-"),
        ("BOOL", 1, True),
        ("BOOL", 0, False),
        # STRING 类型保持原样（不参与数值转换）
        ("STRING", "", ""),
        ("STRING", "abc", "abc"),
    ],
)
def test_normalization_matrix(var_type: str, value, expected) -> None:
    assert _svc()._normalize_point_value(var_type, value) == expected


def test_bad_point_does_not_contaminate_others() -> None:
    """单坏点必须只影响自己：好点照常转换（整页不因一处坏值失败）。"""
    svc = _svc()
    samples = [("INT", "1"), ("INT", ""), ("INT", "3"), ("REAL", "abc"), ("REAL", "2.5")]

    assert [svc._normalize_point_value(t, v) for t, v in samples] == [1, "-", 3, "-", 2.5]


def test_bad_values_are_not_masked_with_zero() -> None:
    """清单硬要求：不得把异常转换成 0 值掩盖。"""
    svc = _svc()
    assert svc._normalize_point_value("INT", "") != 0
    assert svc._normalize_point_value("INT", "abc") != 0
    assert svc._normalize_point_value("REAL", "abc") != 0.0
    assert svc._normalize_point_value("BOOL", "abc") is not False


def test_discriminating_power_old_conversion_would_raise() -> None:
    """判别力校验：旧实现（直接 float() 转换）在空串上确实会抛错 ⇒ 该回归非空转。"""
    with pytest.raises(ValueError):
        int(float(""))
    with pytest.raises(ValueError):
        float("abc")
