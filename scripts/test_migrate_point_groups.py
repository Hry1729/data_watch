"""B018 回归：存量点分组迁移首次建组不得重复计算 used_column_count，且拒绝超容量迁移。"""
from __future__ import annotations

import importlib.util
import sys
import types
from pathlib import Path

import pytest

if "pymysql" not in sys.modules:  # 纯逻辑测试不需要真实驱动
    fake = types.ModuleType("pymysql")
    fake.cursors = types.SimpleNamespace(DictCursor=object)
    sys.modules["pymysql"] = fake
    sys.modules["pymysql.cursors"] = fake.cursors

_SPEC = importlib.util.spec_from_file_location(
    "migrate_point_groups", Path(__file__).with_name("migrate_point_groups.py")
)
mpg = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(mpg)


class FakeDb:
    """按 SQL 片段模拟脚本用到的语句（单设备）。"""

    def __init__(self, point_count: int, group: dict | None = None, mapped: int = 0):
        self.vars = [
            {"id": i + 1, "var_seq": i + 1, "td_column_name": None, "group_index": None}
            for i in range(point_count)
        ]
        for var in self.vars[:mapped]:
            var["td_column_name"] = "preset"
        self.group = group
        self.committed = False
        self.rolled_back = False

    def cursor(self):
        return FakeCursor(self)

    def commit(self):
        self.committed = True

    def rollback(self):
        self.rolled_back = True


class FakeCursor:
    def __init__(self, db: FakeDb):
        self.db = db
        self._rows: list[dict] = []
        self.lastrowid = None

    def __enter__(self):
        return self

    def __exit__(self, *_a):
        return False

    def execute(self, sql: str, params=()):
        q = " ".join(sql.split())
        db = self.db
        if "INFORMATION_SCHEMA.COLUMNS" in q and "device_variable" in q:
            self._rows = [{"COLUMN_NAME": "td_column_name"}]
        elif "INFORMATION_SCHEMA.COLUMNS" in q:
            self._rows = [{"COLUMN_NAME": "variable_config_version"}]
        elif "INFORMATION_SCHEMA.TABLES" in q:
            self._rows = [{"TABLE_NAME": "device_point_group"}]
        elif q.startswith("SELECT d.id AS device_id"):
            self._rows = [{"device_id": 1, "device_code": "D1", "type_code": "QC"}]
        elif q.startswith("SELECT id, next_column_index FROM device_point_group"):
            self._rows = [db.group] if db.group else []
        elif q.startswith("SELECT COUNT(*)"):
            self._rows = [{"cnt": len(db.vars)}]
        elif q.startswith("INSERT INTO device_point_group"):
            device_id, capacity, active = params
            db.group = {
                "id": 10,
                "max_point_count": capacity,
                "active_point_count": active,
                "used_column_count": 0,
                "next_column_index": 1,
            }
            self.lastrowid = 10
        elif q.startswith("SELECT id, var_seq FROM device_variable"):
            self._rows = [dict(v) for v in db.vars if v["td_column_name"] is None]
        elif q.startswith("SELECT used_column_count, max_point_count"):
            self._rows = [dict(db.group)]
        elif q.startswith("UPDATE device_variable"):
            group_id, idx, td_col, var_id = params
            var = next(v for v in db.vars if v["id"] == var_id)
            var.update(group_index=idx, td_column_name=td_col)
        elif q.startswith("UPDATE device_point_group"):
            used, nxt, _gid = params
            db.group.update(used_column_count=used, next_column_index=nxt)
        else:  # pragma: no cover - 新增 SQL 时提醒补桩
            raise AssertionError(f"unexpected SQL: {q}")

    def fetchall(self):
        return self._rows

    def fetchone(self):
        return self._rows[0] if self._rows else None


@pytest.mark.parametrize("count", [3, 1537, 3072])
def test_new_group_counts_match_allocated_columns(count):
    db = FakeDb(count)
    mpg.migrate(db, dry_run=False)
    cols = [v["td_column_name"] for v in db.vars]
    assert cols[0] == "v0001"
    assert cols[-1] == f"v{count:04d}"
    assert db.group["used_column_count"] == count
    assert db.group["next_column_index"] == count + 1
    assert db.group["active_point_count"] == count
    assert db.committed

    # 重复执行幂等
    mpg.migrate(db, dry_run=False)
    assert db.group["used_column_count"] == count
    assert [v["td_column_name"] for v in db.vars] == cols


def test_existing_partial_group_continues_after_used_columns():
    group = {"id": 10, "max_point_count": 3072, "active_point_count": 2,
             "used_column_count": 2, "next_column_index": 3}
    db = FakeDb(5, group=group, mapped=2)
    mpg.migrate(db, dry_run=False)
    assert [v["td_column_name"] for v in db.vars[2:]] == ["v0003", "v0004", "v0005"]
    assert db.group["used_column_count"] == 5
    assert db.group["next_column_index"] == 6


def test_over_capacity_migration_is_refused_without_commit():
    db = FakeDb(3073)
    with pytest.raises(SystemExit):
        mpg.migrate(db, dry_run=False)
    assert db.rolled_back and not db.committed
    assert all(v["td_column_name"] is None for v in db.vars)


# ── 审查 R2：dry-run 对尚无 g001 的设备也要做容量校验 ────────────────────────


def test_dry_run_over_capacity_without_group_fails_like_real_run(capsys):
    db = FakeDb(3073)
    with pytest.raises(SystemExit) as exc:
        mpg.migrate(db, dry_run=True)
    assert exc.value.code == 1
    assert "exceeds g001 capacity 3072" in capsys.readouterr().err
    # dry-run 不写任何东西
    assert db.group is None and not db.committed and not db.rolled_back
    assert all(v["td_column_name"] is None for v in db.vars)


@pytest.mark.parametrize("count", [3, 3072])
def test_dry_run_within_capacity_without_group_previews_without_writes(capsys, count):
    db = FakeDb(count)
    mpg.migrate(db, dry_run=True)
    out = capsys.readouterr().out
    assert "would create g001" in out
    assert f"would map {count} vars" in out
    assert db.group is None and not db.committed
    assert all(v["td_column_name"] is None for v in db.vars)
