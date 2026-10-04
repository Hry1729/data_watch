"""S1 回归：TDengine tag 字面量必须对**反斜线**也做转义。

背景（缺陷已成立）：
- 旧实现只把单引号加倍（`device_code.replace("'", "''")`）；
- 但 TDengine 3.x 在字符串数据中把**反斜线当转义符**（官方《转义字符》表列出 ``\\``、``\\'``、
  ``\\n`` 等；本项目目标版本 `tdengine/tsdb-amd64:3.4.0.9`）。

因此以 `\\` 结尾或含 `\\'` 的 tag 值会让 `\\'` 被解释成"转义后的单引号"，从而吃掉/逃出结尾引号
——轻则整条 INSERT 报错，重则把后续 SQL 文本吸入字面量。同类缺陷有公开公告（FUXA 的 TDengine
连接器经反斜线绕过转义导致注入）。

本回归只用**纯函数不变式**取证（无需真实 TDengine）：
转义后的文本必须满足 ``^([^'\\\\]|\\\\.)*$`` —— 不存在未转义的引号，也不存在落单的反斜线。
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

WRITER_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = WRITER_ROOT.parent
for _candidate in (WRITER_ROOT, REPO_ROOT):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from tdengine_writer import _tag_literal  # noqa: E402

# 转义后：要么是非引号非反斜线字符，要么是一个完整的 `\X` 转义对。
_ESCAPED_OK = re.compile(r"^([^'\\]|\\.)*$")


def _assert_cannot_terminate(raw: object) -> str:
    escaped = _tag_literal(raw)
    assert _ESCAPED_OK.fullmatch(escaped), f"不变式失败（可能终止字面量）: {escaped!r}"
    # 逐字符独立复核，避免只依赖一条正则
    index = 0
    while index < len(escaped):
        char = escaped[index]
        if char == "\\":
            assert index + 1 < len(escaped), "落单反斜线：会吃掉结尾引号"
            index += 2
            continue
        assert char != "'", "未转义的单引号"
        index += 1
    return escaped


def test_plain_value_is_unchanged() -> None:
    assert _tag_literal("QC100") == "QC100"


def test_single_quote_is_escaped() -> None:
    assert _assert_cannot_terminate("a'b") == "a\\'b"


def test_backslash_is_escaped() -> None:
    assert _assert_cannot_terminate("a\\b") == "a\\\\b"


def test_trailing_backslash_cannot_escape_terminator() -> None:
    # `TAGS ('a\')`：旧实现下结尾引号被吃掉，字面量不终止
    assert _assert_cannot_terminate("a\\") == "a\\\\"


def test_backslash_before_quote_is_neutralised() -> None:
    # 旧实现产出 `a\''`，其中 `\'` 被 TDengine 视为转义引号 ⇒ 结尾引号失去保护
    assert _assert_cannot_terminate("a\\'") == "a\\\\\\'"


def test_classic_injection_payload_is_neutralised() -> None:
    _assert_cannot_terminate("x\\', 1); DROP TABLE sdqc; --")


def test_unicode_identifier_and_empty_are_safe() -> None:
    assert _assert_cannot_terminate("设备-001") == "设备-001"
    assert _assert_cannot_terminate("") == ""


def test_none_becomes_empty() -> None:
    assert _tag_literal(None) == ""


def test_old_implementation_would_violate_the_invariant() -> None:
    """判别力校验：确认该不变式真的能抓住旧的"只加倍单引号"实现（否则测试无意义）。"""
    def _old(value: object) -> str:
        return str(value).replace("'", "''")

    assert not _ESCAPED_OK.fullmatch(_old("a\\'")), "不变式未能识别旧实现的缺陷"
    assert not _ESCAPED_OK.fullmatch(_old("a\\")), "不变式未能识别结尾反斜线"
