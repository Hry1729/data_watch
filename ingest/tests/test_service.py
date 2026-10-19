from __future__ import annotations

from dataclasses import replace
from uuid import uuid4

import pytest

from ingest.app.redis_store import CommitUnknownError, RedisProcessResult
from ingest.app.repository import ValidationDecision
from ingest.app.schemas import SessionSwitch, UploadBatch, UploadItem
from ingest.app.service import IngestService


class FakeRepository:
    def __init__(self) -> None:
        self.decisions: dict[int, ValidationDecision] = {}
        self.touched: list[str] = []
        self.batch_validation_calls = 0

    async def get_approved_node_public_keys(self, node_uuid: str) -> list[str]:
        return []

    async def validate_item(self, node_uuid: str, item: UploadItem) -> ValidationDecision:
        return self.decisions.get(item.local_seq, ValidationDecision.accept())

    async def validate_items(
        self, node_uuid: str, items: list[UploadItem]
    ) -> list[ValidationDecision]:
        self.batch_validation_calls += 1
        return [await self.validate_item(node_uuid, item) for item in items]

    async def touch_node(self, node_uuid: str) -> None:
        self.touched.append(node_uuid)


class MemoryStore:
    def __init__(self) -> None:
        self.active: dict[str, str] = {}
        self.hw: dict[tuple[str, str], int] = {}
        self.stream: list[int] = []
        self.fail_seq: int | None = None
        self.batch_calls = 0

    async def process_items(
        self,
        node_uuid: str,
        entries: list[tuple[UploadItem, str]],
        *,
        nonce: str,
    ) -> list[RedisProcessResult]:
        self.batch_calls += 1
        results = []
        for item, action in entries:
            result = await self.process_item(node_uuid, item, action)
            results.append(result)
            if result.status in {
                "seq_reject",
                "commit_unknown",
                "session_obsolete",
            }:
                break
        return results
    async def process_item(
        self, node_uuid: str, item: UploadItem, action: str
    ) -> RedisProcessResult:
        if self.fail_seq == item.local_seq:
            raise CommitUnknownError("redis script failed")
        current = self.active.setdefault(node_uuid, item.upload_session_id)
        if current != item.upload_session_id:
            return RedisProcessResult("session_obsolete")
        key = (node_uuid, item.upload_session_id)
        hw = self.hw.get(key, 0)
        if item.local_seq <= hw:
            return RedisProcessResult("duplicate")
        if item.local_seq > hw + 1:
            return RedisProcessResult("seq_reject")
        self.hw[key] = item.local_seq
        if action == "consume_reject":
            return RedisProcessResult("rejected_consumed")
        self.stream.append(item.local_seq)
        return RedisProcessResult("ack")

    async def switch_session(
        self, node_uuid: str, old_session_id: str, new_session_id: str
    ) -> str:
        current = self.active.get(node_uuid)
        if current is None or current == old_session_id:
            self.active[node_uuid] = new_session_id
            self.hw.setdefault((node_uuid, new_session_id), 0)
            return "switched"
        if current == new_session_id:
            return "already_switched"
        return "session_conflict"

    async def get_active_session(self, node_uuid: str) -> list[str]:
        return self.active.get(node_uuid)


def make_item(seq: int, session_id: str | None = None) -> UploadItem:
    return UploadItem(
        batch_id=str(uuid4()),
        upload_session_id=session_id or str(uuid4()),
        local_seq=seq,
        assignment_epoch=1,
        device_code="QC100",
        device_type="qc",
        group_code="g001",
        scan_rate=100,
        ts=1721800000000 + seq,
        config_version=1,
        values={"1": "12.5"},
    )


def make_batch(*items: UploadItem) -> UploadBatch:
    return UploadBatch(
        node_uuid=str(uuid4()),
        request_id=str(uuid4()),
        items=tuple(items),
    )


@pytest.mark.asyncio
async def test_strict_sequence_duplicate_and_gap() -> None:
    repository = FakeRepository()
    store = MemoryStore()
    service = IngestService(repository, store, max_batch_items=10)
    node_uuid = str(uuid4())
    session_id = str(uuid4())
    first = make_item(1, session_id)

    assert (
        await service.ingest(UploadBatch(node_uuid, str(uuid4()), (first,)))
    )[0].status == "ack"
    assert (
        await service.ingest(UploadBatch(node_uuid, str(uuid4()), (first,)))
    )[0].status == "duplicate"
    assert (
        await service.ingest(
            UploadBatch(node_uuid, str(uuid4()), (make_item(3, session_id),))
        )
    )[0].status == "seq_reject"


@pytest.mark.asyncio
async def test_rejected_consumed_advances_watermark() -> None:
    repository = FakeRepository()
    repository.decisions[1] = ValidationDecision.reject("unknown_device")
    store = MemoryStore()
    service = IngestService(repository, store, max_batch_items=10)
    session_id = str(uuid4())
    batch = make_batch(make_item(1, session_id), make_item(2, session_id))

    results = await service.ingest(batch)

    assert [result.status for result in results] == ["rejected_consumed", "ack"]
    assert store.stream == [2]
    assert store.batch_calls == 1
    assert repository.batch_validation_calls == 1


@pytest.mark.asyncio
async def test_stale_config_warning_is_accepted() -> None:
    repository = FakeRepository()
    repository.decisions[1] = ValidationDecision.accept("stale_config_warning")
    store = MemoryStore()
    service = IngestService(repository, store, max_batch_items=10)

    results = await service.ingest(make_batch(make_item(1)))

    assert results[0].status == "stale_config_warning"
    assert store.stream == [1]


@pytest.mark.asyncio
async def test_session_switch_fences_old_session() -> None:
    repository = FakeRepository()
    store = MemoryStore()
    service = IngestService(repository, store, max_batch_items=10)
    node_uuid = str(uuid4())
    old_session = str(uuid4())
    new_session = str(uuid4())
    batch = UploadBatch(node_uuid, str(uuid4()), (make_item(1, old_session),))
    assert (await service.ingest(batch))[0].status == "ack"

    status = await service.switch_session(
        SessionSwitch(node_uuid, old_session, new_session)
    )
    old_result = await service.ingest(
        UploadBatch(node_uuid, str(uuid4()), (make_item(2, old_session),))
    )

    assert status == "switched"
    assert old_result[0].status == "session_obsolete"


@pytest.mark.asyncio
async def test_commit_unknown_stops_batch() -> None:
    repository = FakeRepository()
    store = MemoryStore()
    store.fail_seq = 1
    service = IngestService(repository, store, max_batch_items=10)
    session_id = str(uuid4())

    results = await service.ingest(
        make_batch(make_item(1, session_id), make_item(2, session_id))
    )

    assert [result.status for result in results] == ["commit_unknown"]


@pytest.mark.asyncio
async def test_transient_store_failure_is_not_turned_into_commit_unknown() -> None:
    """S1：暂时性持久化故障向上抛给 HTTP 层（503），不作为逐条 commit_unknown 返回。"""
    from ingest.app.redis_store import TransientStoreError

    class TransientStore(MemoryStore):
        async def process_items(self, node_uuid, entries, *, nonce):
            raise TransientStoreError("Redis AOF persistence not confirmed")

    service = IngestService(FakeRepository(), TransientStore(), max_batch_items=10)
    session_id = str(uuid4())
    with pytest.raises(TransientStoreError):
        await service.ingest(make_batch(make_item(1, session_id)))


@pytest.mark.asyncio
async def test_malformed_envelope_is_consumed() -> None:
    repository = FakeRepository()
    store = MemoryStore()
    service = IngestService(repository, store, max_batch_items=10)
    item = replace(make_item(1), contract_version=2, qualities={"2": 0})

    results = await service.ingest(make_batch(item))

    assert results[0].status == "rejected_consumed"
    assert "malformed_envelope" in (results[0].reason or "")
