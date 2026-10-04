"""S2 回归：TDengine 错误分类矩阵 —— 子串启发式必须保持"安全方向"。

清单的硬约束：**不得为减少重试把未知错误一律判为 BAD_DATA**；
验收：连接断开 / schema / 数据越界 / SQL 超长 / **未知** / **含关键字但不同类型**的异常，
**分类不导致未确认数据被终结**。

实现事实（诊断所得）：
- `_DATA_ERROR_KEYWORDS` 只认 TDengine 的三个**具体错误码**（`0x0216` 解析期坏值、`0x060b`
  运行期逐行错如越界 ts、`0x0219` SQL 过长），**故意不匹配** `invalid`/`out of range`/`syntax`
  等通用英文词——它们同样出现在 `0x80000112`（配置越界）、`0x80000115~11E`（协议/版本错）
  这类瞬时错误里；
- 调用点判定顺序为 `exc is None or _is_connection_error(exc) or not _is_data_error(exc)`
  ⇒ **连接判定优先**、**未知错误一律 RETRY**；真坏行另有 `delivery_count` 超限的 DLQ 兜底。

本回归把这套"安全方向"**锁住**：一旦有人放宽关键字表或调换判定顺序，就会被立刻抓住。
"""

from __future__ import annotations

import pathlib
import sys
from pathlib import Path

import pytest

WRITER_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = WRITER_ROOT.parent
for _candidate in (WRITER_ROOT, REPO_ROOT):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from tdengine_writer import (  # noqa: E402
    _CONNECTION_ERROR_KEYWORDS,
    _DATA_ERROR_KEYWORDS,
    _is_connection_error,
    _is_data_error,
)

_DATA_CODES = ("0x0216", "0x060b", "0x0219")

# 通用/瞬时错误：**不得**被判为坏数据，否则未确认数据会被终结
_GENERIC_OR_TRANSIENT = (
    "invalid value for column",
    "value out of range",
    "syntax error near 'from'",
    "0x80000112 configuration out of range",
    "0x80000115 protocol version error",
    "0x8000011e unsupported feature",
    "unexpected internal error",
)


@pytest.mark.parametrize("code", _DATA_CODES)
def test_specific_error_codes_are_data_errors(code: str) -> None:
    assert _is_data_error(Exception(f"TDengine error {code}: detail"))


@pytest.mark.parametrize("text", _GENERIC_OR_TRANSIENT)
def test_generic_or_transient_text_is_not_a_data_error(text: str) -> None:
    assert not _is_data_error(Exception(text)), text


def test_unknown_and_none_are_not_data_errors() -> None:
    assert not _is_data_error(Exception("nothing familiar here"))
    assert not _is_data_error(None)


def test_connection_error_wins_over_a_data_code() -> None:
    """含数据码但**同时**含连接关键字 ⇒ 必须按连接处理（RETRY，绝不进 DLQ）。"""
    exc = Exception("connection reset by peer while sending (0x0216)")
    assert _is_connection_error(exc)
    # 复现调用点的早退条件：连接优先 ⇒ 不进入 BAD_DATA 分支
    assert _is_connection_error(exc) or not _is_data_error(exc)


@pytest.mark.parametrize("keyword", _CONNECTION_ERROR_KEYWORDS)
def test_every_connection_keyword_is_recognised(keyword: str) -> None:
    assert _is_connection_error(Exception(f"socket {keyword} happened"))


def test_keyword_tables_contain_no_overly_generic_word() -> None:
    """过泛词指"单独出现不能证明任何一侧"的词。

    - 出现在 **连接** 表里：会把数据错误吞成无限 RETRY（隔离失效，方向仍安全）；
    - 出现在 **数据** 表里：会把瞬时错误误判为坏数据并**终结未确认数据**（危险方向）。

    注意：`unavailable`/`denied` 之类的连接类词**不**属于过泛词——它们真能指示连接/权限异常，
    且判成 RETRY 是安全方向，故不禁止出现在连接表。
    """
    generic = {
        "error", "fail", "failed", "failure", "exception",
        "invalid", "bad", "unknown",
    }
    assert not (set(_CONNECTION_ERROR_KEYWORDS) & generic)
    assert not (set(_DATA_ERROR_KEYWORDS) & generic)
    # 数据表还要排除"连接类/权限类"这类明显不指示坏数据的词
    assert not (set(_DATA_ERROR_KEYWORDS) & {"unavailable", "denied", "timeout", "reset"})


def test_data_keyword_table_is_exactly_the_three_codes() -> None:
    """把"只认具体错误码"本身作为契约锁定（放宽必须是有意识的改动）。"""
    assert set(_DATA_ERROR_KEYWORDS) == set(_DATA_CODES)


def test_source_keeps_unknown_errors_retryable() -> None:
    """源码级护栏：调用点必须保留"未知 ⇒ RETRY"的早退，且不得先判 BAD_DATA。"""
    source = pathlib.Path(WRITER_ROOT / "tdengine_writer.py").read_text(encoding="utf-8")
    assert "not _is_data_error(exc)" in source, "缺少'未知错误 ⇒ RETRY'的早退条件"
    # BAD_DATA 只应出现在通过早退之后的位置
    early_exit = source.index("not _is_data_error(exc)")
    assert source.index("BAD_DATA, elapsed") > early_exit, "BAD_DATA 出现在早退之前"
