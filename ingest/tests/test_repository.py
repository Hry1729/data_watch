from __future__ import annotations

import asyncio
import os
from datetime import datetime, timezone
from types import SimpleNamespace
from unittest.mock import AsyncMock
from uuid import uuid4

import pytest

import ingest.app.repository as repository_module
from ingest.app.repository import MySQLIngestRepository
from ingest.app.schemas import UploadBatch, UploadItem


@pytest.mark.parametrize("timestamp", [1721800000000000, 10**1000])
def test_invalid_timestamp_is_a_single_item_rejection(timestamp):
    decision = MySQLIngestRepository._validate_assignment_window({"assignment_status": "active"}, timestamp)
    assert decision.reason == "malformed_envelope:invalid_timestamp"
    assert MySQLIngestRepository._validate_assignment_window({"assignment_status": "active"}, 1721800000000) is None


@pytest.mark.asyncio
async def test_repository_create_can_skip_worker_schema_migration(monkeypatch) -> None:
    class FakePool:
        def __init__(self) -> None:
            self.closed = False

        def close(self) -> None:
            self.closed = True

        async def wait_closed(self) -> None:
            return None

    pool = FakePool()
    create_pool = AsyncMock(return_value=pool)
    migrate = AsyncMock()
    monkeypatch.setattr(repository_module.aiomysql, "create_pool", create_pool)
    monkeypatch.setattr(repository_module, "migrate_distributed_schema", migrate)
    settings = SimpleNamespace(
        mysql_host="127.0.0.1",
        mysql_port=3306,
        mysql_user="root",
        mysql_password="secret",
        mysql_database="device_watch",
        mysql_pool_maxsize=5,
        control_cache_ttl_ms=30_000,
    )

    repository = await MySQLIngestRepository.create(
        settings,
        migrate_schema=False,
    )

    migrate.assert_not_awaited()
    assert repository._pool is pool
    await repository.close()
    assert pool.closed is True


@pytest.mark.asyncio
async def test_parent_schema_preflight_marks_workers_ready(monkeypatch) -> None:
    import ingest.main as ingest_entrypoint

    repository = SimpleNamespace(close=AsyncMock())
    create = AsyncMock(return_value=repository)
    monkeypatch.delenv(ingest_entrypoint._SCHEMA_READY_ENV, raising=False)
    monkeypatch.setattr(
        ingest_entrypoint,
        "MySQLIngestRepository",
        SimpleNamespace(create=create),
    )
    settings = SimpleNamespace()

    await ingest_entrypoint._prepare_schema_for_workers(settings)

    create.assert_awaited_once_with(settings)
    repository.close.assert_awaited_once_with()
    assert os.environ[ingest_entrypoint._SCHEMA_READY_ENV] == "1"

def test_invalid_quality_value_becomes_malformed_envelope() -> None:
    payload = {
        "node_uuid": str(uuid4()),
        "request_id": str(uuid4()),
        "items": [
            {
                "batch_id": str(uuid4()),
                "upload_session_id": str(uuid4()),
                "local_seq": 1,
                "assignment_epoch": 1,
                "device_code": "QC100",
                "device_type": "qc",
                "group_code": "g001",
                "scan_rate": 100,
                "ts": 1721800000000,
                "config_version": 1,
                "contract_version": 2,
                "values": {"1": "12.5"},
                "qualities": {"1": "bad"},
            }
        ],
    }

    item = UploadBatch.from_dict(payload).items[0]

    assert item.qualities is None
    assert "qualities values" in (item.envelope_error or "")


def test_assignment_window_uses_center_plus_eight_datetime() -> None:
    cutover_center = datetime(2026, 8, 1, 8, 0, 0)
    cutover_utc_ms = int(
        datetime(2026, 8, 1, 0, 0, 0, tzinfo=timezone.utc).timestamp() * 1000
    )
    active = {
        "assignment_status": "active",
        "cutover_at": cutover_center,
        "grace_until": None,
    }
    retired = {
        "assignment_status": "retired",
        "cutover_at": cutover_center,
        "grace_until": datetime(2099, 1, 1, 8, 0, 0),
    }

    assert (
        MySQLIngestRepository._validate_assignment_window(active, cutover_utc_ms)
        is None
    )
    assert MySQLIngestRepository._validate_assignment_window(
        active, cutover_utc_ms - 1
    ).reason == "out_of_time_window"
    assert (
        MySQLIngestRepository._validate_assignment_window(
            retired, cutover_utc_ms - 1
        )
        is None
    )
    assert MySQLIngestRepository._validate_assignment_window(
        retired, cutover_utc_ms
    ).reason == "out_of_time_window"

def _item(seq: int, device_code: str, point_id: int) -> UploadItem:
    return UploadItem(
        batch_id=str(uuid4()),
        upload_session_id=str(uuid4()),
        local_seq=seq,
        assignment_epoch=1,
        device_code=device_code,
        device_type="qc",
        group_code="g001",
        scan_rate=100,
        ts=int(datetime.now(timezone.utc).timestamp() * 1000),
        config_version=1,
        values={str(point_id): "12.5"},
    )


@pytest.mark.asyncio
async def test_validate_items_uses_two_queries_for_multiple_items() -> None:
    node_uuid = str(uuid4())
    items = [_item(1, "QC100", 1), _item(2, "QC101", 2)]
    repository = MySQLIngestRepository(None)  # type: ignore[arg-type]
    calls = []

    async def fetchall(query: str, params: tuple) -> list[dict]:
        calls.append((query, params))
        if "FROM device d" in query:
            return [
                {
                    "device_id": index,
                    "device_code": item.device_code,
                    "group_code": item.group_code,
                    "device_type": "qc",
                    "current_config_version": 1,
                    "node_uuid": node_uuid,
                    "assignment_epoch": 1,
                    "cutover_at": None,
                    "grace_until": None,
                    "assignment_status": "active",
                }
                for index, item in enumerate(items, start=1)
            ]
        return [
            {
                "device_code": item.device_code,
                "group_code": item.group_code,
                "point_id": int(next(iter(item.values))),
            }
            for item in items
        ]

    repository._fetchall = fetchall  # type: ignore[method-assign]
    decisions = await repository.validate_items(node_uuid, items)
    cached_decisions = await repository.validate_items(node_uuid, items)

    assert [decision.action for decision in decisions] == ["accept", "accept"]
    assert [decision.action for decision in cached_decisions] == ["accept", "accept"]
    assert len(calls) == 2
    assert "FROM device d" in calls[0][0]
    assert "FROM point_storage_mapping" in calls[1][0]


@pytest.mark.asyncio
async def test_touch_node_is_throttled_per_node() -> None:
    repository = MySQLIngestRepository(None)  # type: ignore[arg-type]
    updates = []

    async def update(node_uuid: str) -> None:
        updates.append(node_uuid)

    repository._update_last_seen = update  # type: ignore[method-assign]
    node_uuid = str(uuid4())
    await repository.touch_node(node_uuid)
    await repository.touch_node(node_uuid)
    repository._last_touch[node_uuid] -= 6.0
    await repository.touch_node(node_uuid)
    await asyncio.sleep(0)

    assert updates == [node_uuid, node_uuid]


@pytest.mark.asyncio
async def test_public_key_cache_expires_and_refetches() -> None:
    repository = MySQLIngestRepository(None)  # type: ignore[arg-type]
    calls = []

    async def fetchone(query: str, params: tuple) -> dict:
        calls.append((query, params))
        return {
            "public_key_pem": "current-key",
            "previous_public_key_pem": None,
            "previous_key_expires_at": None,
        }

    repository._fetchone = fetchone  # type: ignore[method-assign]
    node_uuid = str(uuid4())
    assert await repository.get_approved_node_public_keys(node_uuid) == ["current-key"]
    assert await repository.get_approved_node_public_keys(node_uuid) == ["current-key"]
    assert len(calls) == 1

    _, keys = repository._public_key_cache[node_uuid]
    repository._public_key_cache[node_uuid] = (0.0, keys)
    assert await repository.get_approved_node_public_keys(node_uuid) == ["current-key"]
    assert len(calls) == 2

@pytest.mark.asyncio
async def test_public_key_ttl_starts_after_slow_query(monkeypatch) -> None:
    repository = MySQLIngestRepository(
        None, control_cache_ttl_seconds=1.0
    )  # type: ignore[arg-type]
    times = iter((10.0, 20.0))
    monkeypatch.setattr(repository_module, "monotonic", lambda: next(times))

    async def fetchone(query: str, params: tuple) -> dict:
        return {
            "public_key_pem": "current-key",
            "previous_public_key_pem": None,
            "previous_key_expires_at": None,
        }

    repository._fetchone = fetchone  # type: ignore[method-assign]
    node_uuid = str(uuid4())
    await repository.get_approved_node_public_keys(node_uuid)

    expires_at = repository._public_key_cache[node_uuid][0]
    assert 20.8 <= expires_at <= 21.2

def test_control_invalidation_clears_requested_cache_scope() -> None:
    repository = MySQLIngestRepository(None)  # type: ignore[arg-type]
    repository._public_key_cache["node-1"] = (100.0, ["key"])
    repository._assignment_cache[("D1", "g001", 1)] = (100.0, {})
    repository._mapping_cache[("D1", "g001")] = (100.0, frozenset({1}))

    repository.invalidate_control_cache(
        {"scope": "node", "node_uuid": "node-1", "assignments": True}
    )
    assert repository._public_key_cache == {}
    assert repository._assignment_cache == {}
    assert repository._mapping_cache

    repository.invalidate_control_cache({"scope": "mappings"})
    assert repository._mapping_cache == {}
