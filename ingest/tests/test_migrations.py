from __future__ import annotations

import pytest

from ingest.app.migrations import (
    MappingBackfillBlocked,
    _BACKFILL_CHECKS,
    _CREATE_TABLES,
    _backfill_mapping,
    _upgrade_sys_node,
)


class FakeConnection:
    def __init__(self) -> None:
        self.begun = 0
        self.committed = 0
        self.rolled_back = 0

    async def begin(self) -> None:
        self.begun += 1

    async def commit(self) -> None:
        self.committed += 1

    async def rollback(self) -> None:
        self.rolled_back += 1


class FakeCursor:
    def __init__(self, fetches: list[list[dict]]) -> None:
        self.fetches = list(fetches)
        self.executed: list[str] = []

    async def execute(self, query: str, params=None) -> None:
        self.executed.append(" ".join(query.split()))

    async def fetchall(self) -> list[dict]:
        return self.fetches.pop(0)


@pytest.mark.asyncio
async def test_upgrade_adds_only_missing_sys_node_columns() -> None:
    cursor = FakeCursor([[{"COLUMN_NAME": "previous_public_key_pem"}]])

    await _upgrade_sys_node(cursor)

    alters = [query for query in cursor.executed if query.startswith("ALTER TABLE")]
    assert all("ADD COLUMN previous_public_key_pem " not in query for query in alters)
    assert any("ADD COLUMN key_version " in query for query in alters)
    assert any("ADD COLUMN read_timeout_ms " in query for query in alters)


@pytest.mark.asyncio
async def test_backfill_promotes_when_all_checks_are_clean() -> None:
    connection = FakeConnection()
    cursor = FakeCursor([[], [], [], [], [], []])

    await _backfill_mapping(connection, cursor)

    assert connection.begun == 1
    assert connection.committed == 1
    assert connection.rolled_back == 0
    assert any(
        query.startswith("INSERT INTO point_storage_mapping (")
        for query in cursor.executed
    )


@pytest.mark.asyncio
async def test_backfill_blocks_and_retains_staging_on_difference() -> None:
    connection = FakeConnection()
    cursor = FakeCursor(
        [[{"point_id": 7, "device_code": "QC100"}], [], [], [], [], []]
    )

    with pytest.raises(MappingBackfillBlocked, match="missing"):
        await _backfill_mapping(connection, cursor)

    assert connection.committed == 1
    assert connection.rolled_back == 0
    deletes = [
        query
        for query in cursor.executed
        if query == "DELETE FROM point_storage_mapping_staging"
    ]
    assert len(deletes) == 1


def test_orphan_check_reads_permanent_mapping_table() -> None:
    query = " ".join(_BACKFILL_CHECKS["orphan"].split())

    assert "FROM point_storage_mapping m" in query
    assert "LEFT JOIN device_variable" in query



def test_distributed_foreign_key_ids_match_signed_mysql_baseline() -> None:
    ddl = " ".join(_CREATE_TABLES)

    assert "device_id BIGINT NOT NULL" in ddl
    assert "point_id BIGINT NOT NULL" in ddl
    assert "device_id BIGINT UNSIGNED" not in ddl
    assert "point_id BIGINT UNSIGNED" not in ddl
