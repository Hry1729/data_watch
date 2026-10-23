"""points resync：直接改库后按 device_variable 校准存储映射。"""
from __future__ import annotations

from typing import Any
from unittest.mock import MagicMock

import pytest

from app.core.exceptions import ApiError
from app.services.catalog import CatalogService


class _Result:
    def __init__(self, rows=None, scalar=None, lastrowid=0):
        self._rows = rows or []
        self._scalar = scalar
        self.lastrowid = lastrowid

    def mappings(self):
        return self

    def first(self):
        return self._rows[0] if self._rows else None

    def all(self):
        return list(self._rows)

    def scalar_one(self):
        return self._scalar

    def scalar_one_or_none(self):
        return self._scalar


class _FakeSession:
    """按 SQL 片段路由的假会话，记录所有写语句。"""

    def __init__(self, *, device, points, mappings, groups):
        self.device = device
        self.points = points
        self.mappings = mappings
        self.groups = groups
        self.writes: list[tuple[str, dict]] = []
        self.committed = False
        self.rolled_back = False

    async def execute(self, statement, params: dict[str, Any] | None = None):
        sql = " ".join(str(statement).split())
        params = params or {}
        if sql.startswith(("UPDATE", "DELETE", "INSERT")):
            self.writes.append((sql, params))
            return _Result(lastrowid=1)
        if "FROM device AS d" in sql and "LEFT JOIN device_type" in sql:
            return _Result(rows=[self.device] if self.device else [])
        if "information_schema.TABLES" in sql:
            return _Result(scalar=1)
        if "FROM device_variable AS dv" in sql:
            return _Result(rows=self.points)
        if "FROM point_storage_mapping" in sql:
            return _Result(rows=self.mappings)
        if "FROM device_point_group" in sql and "next_column_index" in sql and "FOR UPDATE" not in sql:
            return _Result(rows=self.groups)
        if "SELECT point_group_id, group_index, td_column_name" in sql:
            return _Result(rows=[p for p in self.points if p.get("point_group_id")])
        if "FOR UPDATE" in sql:
            return _Result(rows=[{"id": 9, "max_point_count": 3072, "used_column_count": 2,
                                  "next_column_index": 3}])
        if "TRIM(td_column_name) = :column" in sql:
            return _Result(scalar=None)
        if "sys_node" in sql or "device_assignment" in sql:
            return _Result(scalar=0)
        return _Result(scalar=0)

    async def commit(self):
        self.committed = True

    async def rollback(self):
        self.rolled_back = True


def _point(point_id, col, *, group_id=9, group_index=None, deleted=0, group_code="g001"):
    return {"point_id": point_id, "point_group_id": group_id, "group_index": group_index,
            "td_column_name": col, "var_type": "DINT", "deleted": deleted,
            "group_code": group_code if group_id else None}


def _mapping(point_id, col, stable="sdasc", type_code="asc"):
    return {"point_id": point_id, "device_code": "asc7181", "device_type_code": type_code,
            "stable_name": stable, "child_table": "asc7181", "group_code": "g001",
            "td_column_name": col, "var_type": "DINT"}


def _service(session) -> CatalogService:
    service = CatalogService(session=session, tdengine=MagicMock())
    service._distributed_node_tables_available = _async_false  # type: ignore[method-assign]
    return service


async def _async_false(*_args, **_kwargs):
    return False


DEVICE = {"device_id": 63, "device_code": "asc7181", "type_id": 4, "type_code": " ASC"}


@pytest.mark.asyncio
async def test_check_reports_sql_edit_damage_without_writing():
    session = _FakeSession(
        device=DEVICE,
        points=[_point(101, "asc1", group_index=1), _point(102, " asc2", group_index=None)],
        mappings=[_mapping(55, " asc2", stable="sd asc", type_code=" asc")],  # 删掉的旧点留下的孤儿
        groups=[{"id": 9, "group_code": "g001", "next_column_index": 1}],
    )

    report = await _service(session).resync_point_storage("asc7181", apply=False, actor="admin")

    assert report["applied"] is False
    assert report["typeCodeFixed"] == {"from": " ASC", "to": "ASC"}
    assert report["trimmedColumns"] == [{"pointId": 102, "from": " asc2", "to": "asc2"}]
    assert report["orphansRemoved"] == [55]
    assert sorted(report["inserted"]) == [101, 102]
    assert report["groupCountersFixed"] == [{"groupCode": "g001", "nextColumnIndex": 3}]
    assert session.writes == []
    assert session.committed is False


@pytest.mark.asyncio
async def test_apply_fixes_mapping_and_bumps_config_version():
    session = _FakeSession(
        device=DEVICE,
        points=[_point(101, "asc1", group_index=1), _point(102, " asc2")],
        mappings=[_mapping(55, " asc2", stable="sd asc", type_code=" asc")],
        groups=[{"id": 9, "group_code": "g001", "next_column_index": 1}],
    )

    report = await _service(session).resync_point_storage("asc7181", apply=True, actor="admin")

    assert report["applied"] is True and report["configVersionBumped"] is True
    assert session.committed is True
    sqls = [sql for sql, _ in session.writes]
    assert any(sql.startswith("UPDATE device_type SET type_code = TRIM(type_code)") for sql in sqls)
    assert any("SET td_column_name = TRIM(td_column_name)" in sql for sql in sqls)
    assert any(sql.startswith("DELETE FROM point_storage_mapping") for sql in sqls)
    inserts = [params for sql, params in session.writes if sql.startswith("INSERT INTO point_storage_mapping")]
    assert {p["td_column_name"] for p in inserts} == {"asc1", "asc2"}
    assert {p["stable_name"] for p in inserts} == {"sdasc"}
    assert any("variable_config_version" in sql for sql in sqls)
    assert any(sql.startswith("UPDATE device_point_group SET config_version = config_version + 1") for sql in sqls)


@pytest.mark.asyncio
async def test_apply_allocates_column_for_point_inserted_without_one():
    session = _FakeSession(
        device={**DEVICE, "type_code": "asc"},
        points=[_point(101, "asc1", group_index=1), _point(103, None, group_id=None)],
        mappings=[_mapping(101, "asc1")],
        groups=[{"id": 9, "group_code": "g001", "next_column_index": 2}],
    )

    report = await _service(session).resync_point_storage("asc7181", apply=True, actor="admin")

    assert report["allocated"][0]["pointId"] == 103
    assert report["allocated"][0]["tdColumnName"] == "asc3"
    updates = [params for sql, params in session.writes if sql.startswith("UPDATE device_variable SET point_group_id")]
    assert updates and updates[0]["td_column_name"] == "asc3"


@pytest.mark.asyncio
async def test_clean_device_is_a_no_op():
    session = _FakeSession(
        device={**DEVICE, "type_code": "asc"},
        points=[_point(101, "asc1", group_index=1)],
        mappings=[_mapping(101, "asc1")],
        groups=[{"id": 9, "group_code": "g001", "next_column_index": 2}],
    )

    report = await _service(session).resync_point_storage("asc7181", apply=True, actor="admin")

    assert report["applied"] is False
    assert session.writes == []


@pytest.mark.asyncio
async def test_column_taken_by_other_point_is_reported_not_overwritten():
    session = _FakeSession(
        device={**DEVICE, "type_code": "asc"},
        points=[_point(101, "asc1", group_index=1), _point(102, "asc1", group_index=1)],
        mappings=[_mapping(101, "asc1")],
        groups=[{"id": 9, "group_code": "g001", "next_column_index": 2}],
    )

    report = await _service(session).resync_point_storage("asc7181", apply=False, actor="admin")

    assert report["conflicts"] and report["conflicts"][0]["pointId"] == 102


@pytest.mark.asyncio
async def test_unknown_device_is_404():
    session = _FakeSession(device=None, points=[], mappings=[], groups=[])
    with pytest.raises(ApiError) as exc_info:
        await _service(session).resync_point_storage("nope", apply=False, actor="admin")
    assert exc_info.value.code == 404
