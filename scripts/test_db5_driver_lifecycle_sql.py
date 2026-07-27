# -*- coding: utf-8 -*-
"""静态校验 db5_driver_lifecycle.sql 迁移契约（无需真库）。"""

from __future__ import annotations

import re
from pathlib import Path

SQL_PATH = Path(__file__).resolve().parent / "sql" / "db5_driver_lifecycle.sql"


def _sql() -> str:
    return SQL_PATH.read_text(encoding="utf-8")


def test_abort_uses_native_signal_via_procedure_not_prepare():
    """脏数据中止必须走临时过程原生 SIGNAL，禁止 PREPARE SIGNAL。"""
    text = _sql()
    assert "CREATE PROCEDURE assert_db5_driver_data" in text
    assert "CALL assert_db5_driver_data()" in text
    assert "DROP PROCEDURE IF EXISTS assert_db5_driver_data" in text
    # 旧写法：把 SIGNAL 拼进 @sql 再 PREPARE
    assert not re.search(
        r"SET\s+@sql\s*:=\s*IF\([^)]*SIGNAL\s+SQLSTATE",
        text,
        flags=re.IGNORECASE | re.DOTALL,
    )
    assert "SIGNAL SQLSTATE '45000'" in text


def test_idx_pending_requires_non_unique_and_column_order():
    """idx_pending 必须是非唯一索引，且列序 agent_phase, db_phase。"""
    text = _sql()
    # 校验分支与最终断言均要求 NON_UNIQUE = 1
    assert text.count("index_name = 'idx_pending' AND NON_UNIQUE = 1") >= 4
    assert "AND SEQ_IN_INDEX > 2" in text
    # 重建必须是普通 KEY，不能是 UNIQUE
    assert "ADD KEY idx_pending (agent_phase, db_phase)" in text
    assert "ADD UNIQUE KEY idx_pending" not in text
    # 错误定义（含同名 UNIQUE）应先 DROP
    assert "DROP INDEX idx_pending" in text


def test_driver_id_trim_and_uk_semantics_still_present():
    """保留 driver_id trim / uk 唯一性语义，避免回归。"""
    text = _sql()
    assert "driver_id <> TRIM(driver_id)" in text
    assert "uk_installed_driver_id" in text
    assert "NON_UNIQUE = 0" in text


def test_installed_driver_has_persistent_lifecycle_gate():
    text = _sql()
    assert "lifecycle_status ENUM('active', 'uninstalling')" in text
    assert "lifecycle_operation_id CHAR(36)" in text
    assert "column_name = 'lifecycle_status'" in text
    assert "column_name = 'lifecycle_operation_id'" in text
