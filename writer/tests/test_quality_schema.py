"""质量存储容量门禁与窄表命名测试。"""

from __future__ import annotations

import quality_schema as qs


def test_parallel_q_fails_at_platform_max_point_count():
    """3072 点 ×2 + ts + tag 超过 4096 → 不得选用平行 `__q`。"""
    needed = qs.columns_needed_for_parallel_q(qs.PLATFORM_MAX_POINT_COUNT)
    assert needed > qs.TDENGINE_MAX_COLUMNS
    assert qs.can_use_parallel_q_columns() is False
    assert qs.resolve_quality_storage_model() == qs.QUALITY_STORAGE_NARROW
    assert qs.QUALITY_STORAGE_MODEL == qs.QUALITY_STORAGE_NARROW


def test_parallel_q_passes_when_point_count_small():
    """较小点组可通过门禁（仅验证公式；生产仍冻结为窄表）。"""
    assert qs.can_use_parallel_q_columns(max_point_count=1000) is True
    assert (
        qs.resolve_quality_storage_model(max_point_count=1000)
        == qs.QUALITY_STORAGE_PARALLEL
    )


def test_quality_naming_helpers():
    assert qs.quality_column_name("qc1") == "qc1__q"
    assert qs.quality_column_name("qc1__q") == "qc1__q"
    assert qs.quality_stable_name("QC") == "sqqc"
    assert qs.quality_child_table_name("qc100") == "qc100_q"


def test_quality_narrow_ddl_has_composite_key():
    """P0-1：窄表 DDL 必须含 var_id COMPOSITE KEY。"""
    sql = qs.quality_narrow_create_sql("`device_data`", "`sqqc`")
    assert "COMPOSITE KEY" in sql
    assert "`var_id` BIGINT COMPOSITE KEY" in sql
    assert "CREATE STABLE IF NOT EXISTS" in sql
    assert "TINYINT UNSIGNED" in sql


def test_describe_detects_var_id_composite_key():
    ok_rows = [
        ("ts", "TIMESTAMP", 8, ""),
        ("var_id", "BIGINT", 8, "COMPOSITE KEY"),
        ("quality", "TINYINT", 1, ""),
        ("device_code", "VARCHAR", 32, "TAG"),
    ]
    bad_rows = [
        ("ts", "TIMESTAMP", 8, ""),
        ("var_id", "BIGINT", 8, ""),
        ("quality", "TINYINT", 1, ""),
    ]
    assert qs.describe_has_var_id_composite_key(ok_rows) is True
    assert qs.describe_has_var_id_composite_key(bad_rows) is False

def test_describe_detects_unsigned_quality_column():
    rows = [
        ("quality", "TINYINT UNSIGNED", 1, ""),
    ]
    assert qs.describe_has_unsigned_quality(rows) is True
    assert qs.describe_has_unsigned_quality(
        [("quality", "TINYINT", 1, "")]
    ) is False
