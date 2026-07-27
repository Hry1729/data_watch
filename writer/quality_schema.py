"""质量存储模型选择与窄表命名（Q1）。

容量门禁（设计 §8.1）：
  TDengine 单表上限 4096 列（含 tag）；平台 max_point_count 默认 3072。
  平行质量列（值列 + `__q`）在满配下约 6145 列，不通过门禁 → 采用质量窄表。
"""

from __future__ import annotations

# TDengine 3.x：单表/超级表最大列数（含 tag）
TDENGINE_MAX_COLUMNS = 4096

# 与 catalog / device_point_group.max_point_count 默认一致
PLATFORM_MAX_POINT_COUNT = 3072

# 宽表固定占用：ts + device_code tag
_WIDE_FIXED_COLUMNS = 2

QUALITY_COL_SUFFIX = "__q"

# 窄表：每采样点一行（ts, var_id, quality）
QUALITY_STORAGE_NARROW = "narrow_table"
QUALITY_STORAGE_PARALLEL = "parallel_q"


def columns_needed_for_parallel_q(value_column_count: int) -> int:
    """平行 `__q` 列方案所需总列数（ts + 值列 + 质量列 + 1 tag）。"""
    n = int(value_column_count)
    if n < 0:
        raise ValueError("value_column_count must be >= 0")
    return _WIDE_FIXED_COLUMNS + n * 2


def can_use_parallel_q_columns(
    max_point_count: int = PLATFORM_MAX_POINT_COUNT,
    tdengine_max_columns: int = TDENGINE_MAX_COLUMNS,
) -> bool:
    """最大点组配置下，平行质量列是否仍不超过 TDengine 列上限。"""
    return columns_needed_for_parallel_q(max_point_count) <= tdengine_max_columns


def resolve_quality_storage_model(
    max_point_count: int = PLATFORM_MAX_POINT_COUNT,
    tdengine_max_columns: int = TDENGINE_MAX_COLUMNS,
) -> str:
    """门禁结果：通过用 parallel_q，否则用窄表。"""
    if can_use_parallel_q_columns(max_point_count, tdengine_max_columns):
        return QUALITY_STORAGE_PARALLEL
    return QUALITY_STORAGE_NARROW


# Q1 冻结：按平台默认门禁，选用窄表（禁止同时实现两种模型）
QUALITY_STORAGE_MODEL = resolve_quality_storage_model()


def quality_column_name(value_column: str) -> str:
    """由值列名生成平行质量列名（仅平行模型使用；命名统一在此，禁止业务侧拼写）。"""
    name = str(value_column or "").strip()
    if not name:
        raise ValueError("value_column cannot be empty")
    if name.endswith(QUALITY_COL_SUFFIX):
        return name
    return f"{name}{QUALITY_COL_SUFFIX}"


def quality_stable_name(device_type: str) -> str:
    """质量窄表超级表名，与值表 `sd{type}` 平行为 `sq{type}`。"""
    return f"sq{str(device_type or '').lower()}"


def quality_child_table_name(device_code: str) -> str:
    """质量窄表子表名：`{device_code}_q`。"""
    code = str(device_code or "").strip()
    if not code:
        raise ValueError("device_code cannot be empty")
    return f"{code}_q"


# 窄表必须用 (ts, var_id) 复合主键，否则同 ts 多点质量会互相覆盖
QUALITY_NARROW_COMPOSITE_KEY_COL = "var_id"

QUALITY_NARROW_STABLE_DDL = (
    "CREATE STABLE IF NOT EXISTS {db}.{stable} "
    "(`ts` TIMESTAMP, `var_id` BIGINT COMPOSITE KEY, `quality` TINYINT) "
    "TAGS (`device_code` VARCHAR(32))"
)

QUALITY_NARROW_MIGRATION_HINT = (
    "质量窄表缺少 var_id COMPOSITE KEY，同时间戳多点质量会冲突。"
    "请迁移：CREATE 新超级表（含 COMPOSITE KEY）→ INSERT SELECT → RENAME；"
    "详见 docs/审查修复指南-20260724.md P0-1。"
)


class QualitySchemaError(RuntimeError):
    """质量窄表 schema 与契约不符（缺复合主键等）。"""


def quality_narrow_create_sql(database: str, stable: str) -> str:
    """生成带 COMPOSITE KEY 的质量超级表 DDL。"""
    db = str(database or "").strip()
    st = str(stable or "").strip()
    if not db or not st:
        raise ValueError("database/stable cannot be empty")
    # 调用方负责对标识符加反引号；此处仅拼装契约 DDL 模板占位
    return QUALITY_NARROW_STABLE_DDL.format(db=db, stable=st)


def describe_has_var_id_composite_key(describe_rows: list) -> bool:
    """根据 DESCRIBE 结果判断 var_id 是否为 COMPOSITE KEY。

    兼容不同连接器行格式：常见为 (field, type, length, note, ...)，
    note 中含 COMPOSITE KEY / COMPOSITEKEY / PRIMARY KEY 等标记。
    """
    for row in describe_rows or []:
        if not row:
            continue
        col_name = str(row[0] or "").strip().lower()
        if col_name != QUALITY_NARROW_COMPOSITE_KEY_COL:
            continue
        note = " ".join(str(item or "") for item in row[1:]).upper()
        note_compact = note.replace(" ", "").replace("_", "")
        if "COMPOSITEKEY" in note_compact:
            return True
        if "COMPOSITE" in note and "KEY" in note:
            return True
    return False
