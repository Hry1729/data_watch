"""B7｜新点位/配置已发布而 Ingest 缓存仍旧，导致永久拒收。

证据等级：单元（`MySQLIngestRepository(None)` + fake `_fetchall`）。
本文件不连接任何 MySQL / Redis；控制面内容由一个可编程替身模拟。
"""

from __future__ import annotations

from datetime import datetime, timezone
from uuid import uuid4

import pytest

from ingest.app.redis_store import RedisProcessResult
from ingest.app.repository import MySQLIngestRepository
from ingest.app.schemas import UploadBatch, UploadItem
from ingest.app.service import IngestService


def _item(
    seq: int,
    *,
    point_id: int = 1,
    config_version: int = 1,
    device_code: str = "QC100",
    device_type: str = "qc",
) -> UploadItem:
    return UploadItem(
        batch_id=str(uuid4()),
        upload_session_id=str(uuid4()),
        local_seq=seq,
        assignment_epoch=1,
        device_code=device_code,
        device_type=device_type,
        group_code="g001",
        scan_rate=100,
        ts=int(datetime.now(timezone.utc).timestamp() * 1000),
        config_version=config_version,
        values={str(point_id): "12.5"},
    )


class FakeControlPlane:
    """控制面库内容的可编程替身（模拟 backend 提交后的 device_point_group / point_storage_mapping）。"""

    def __init__(
        self,
        *,
        config_version: int = 1,
        point_ids: tuple[int, ...] = (1,),
        device_type: str = "qc",
        assignment_present: bool = True,
    ) -> None:
        self.config_version = config_version
        self.point_ids = set(point_ids)
        self.device_type = device_type
        self.assignment_present = assignment_present
        self.node_uuid = str(uuid4())
        self.assignment_reads = 0
        self.mapping_reads = 0

    async def fetchall(self, query: str, params: tuple) -> list[dict]:
        if "FROM device d" in query:
            self.assignment_reads += 1
            if not self.assignment_present:
                return []
            return [
                {
                    "device_id": 1,
                    "device_code": "QC100",
                    "group_code": "g001",
                    "device_type": self.device_type,
                    "current_config_version": self.config_version,
                    "node_uuid": self.node_uuid,
                    "assignment_epoch": 1,
                    "cutover_at": None,
                    "grace_until": None,
                    "assignment_status": "active",
                }
            ]
        self.mapping_reads += 1
        return [
            {"device_code": "QC100", "group_code": "g001", "point_id": point_id}
            for point_id in sorted(self.point_ids)
        ]


def _repository(db: FakeControlPlane) -> MySQLIngestRepository:
    # TTL 取极大值：整个用例期间缓存都不会自然过期，等价于「通知丢失 + 缓存未失效」。
    repository = MySQLIngestRepository(None, control_cache_ttl_seconds=3600.0)  # type: ignore[arg-type]
    repository._fetchall = db.fetchall  # type: ignore[method-assign]
    return repository


async def _warm_cache(
    repository: MySQLIngestRepository, db: FakeControlPlane
) -> None:
    decisions = await repository.validate_items(
        db.node_uuid, [_item(1, point_id=1, config_version=db.config_version)]
    )
    assert [decision.action for decision in decisions] == ["accept"]


@pytest.mark.asyncio
async def test_lost_invalidation_notification_still_accepts_new_point() -> None:
    """验收①/③：旧缓存 + 通知丢失，新点位的新版本帧不得 rejected_consumed。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,))
    repository = _repository(db)
    await _warm_cache(repository, db)

    # 控制面已提交新点位与新版本；失效通知丢失（本用例从不调用 invalidate_control_cache）
    db.config_version = 2
    db.point_ids = {1, 2}

    decisions = await repository.validate_items(
        db.node_uuid, [_item(2, point_id=2, config_version=2)]
    )

    assert [decision.action for decision in decisions] == ["accept"]
    assert (db.assignment_reads, db.mapping_reads) == (2, 2)


@pytest.mark.asyncio
async def test_lost_invalidation_notification_still_accepts_new_config_version() -> None:
    """验收②：仅版本号前移（点位不变）也不得被判 unknown_config_version。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,))
    repository = _repository(db)
    await _warm_cache(repository, db)

    db.config_version = 2

    decisions = await repository.validate_items(
        db.node_uuid, [_item(2, point_id=1, config_version=2)]
    )

    assert [decision.action for decision in decisions] == ["accept"]


@pytest.mark.asyncio
async def test_warm_legal_frame_does_not_re_read() -> None:
    """并发 miss 成本控制：缓存命中且判定为合法时不得产生任何额外查库。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,))
    repository = _repository(db)
    await _warm_cache(repository, db)

    decisions = await repository.validate_items(
        db.node_uuid, [_item(2, point_id=1, config_version=1)]
    )

    assert [decision.action for decision in decisions] == ["accept"]
    assert (db.assignment_reads, db.mapping_reads) == (1, 1)


@pytest.mark.asyncio
async def test_fresh_miss_illegal_point_does_not_re_read_again() -> None:
    """并发 miss：本次已权威查库的 key 不得因驳回而立刻再查一次。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,))
    repository = _repository(db)

    decisions = await repository.validate_items(
        db.node_uuid, [_item(1, point_id=99, config_version=1)]
    )

    assert [decision.status_override for decision in decisions] == ["rejected_consumed"]
    assert decisions[0].reason == "malformed_envelope:unknown_var_id"
    assert (db.assignment_reads, db.mapping_reads) == (1, 1)


@pytest.mark.asyncio
async def test_repeated_illegal_frames_are_bounded_to_one_reread() -> None:
    """受控权威重读：同一 key 在一个 TTL 窗口内最多额外重读一次。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,))
    repository = _repository(db)
    await _warm_cache(repository, db)

    for seq in range(2, 22):
        decisions = await repository.validate_items(
            db.node_uuid, [_item(seq, point_id=99, config_version=1)]
        )
        assert [decision.status_override for decision in decisions] == [
            "rejected_consumed"
        ]

    # 暖缓存各 1 次；非法点位只触发 mapping 命名空间的一次权威重读，20 帧内不再增加
    assert (db.assignment_reads, db.mapping_reads) == (1, 2)


@pytest.mark.asyncio
async def test_illegal_point_reread_does_not_block_version_reread() -> None:
    """两个缓存命名空间的重读配额相互独立：mapping 上的非法帧不得拖累随后的版本帧。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,))
    repository = _repository(db)
    await _warm_cache(repository, db)

    illegal = await repository.validate_items(
        db.node_uuid, [_item(2, point_id=99, config_version=1)]
    )
    assert illegal[0].status_override == "rejected_consumed"

    # 紧接着控制面提交了新版本、且通知丢失
    db.config_version = 2
    legal = await repository.validate_items(
        db.node_uuid, [_item(3, point_id=1, config_version=2)]
    )

    assert [decision.action for decision in legal] == ["accept"]


@pytest.mark.asyncio
async def test_point_outside_mapping_is_still_permanently_rejected() -> None:
    """真正非法的点位（映射表中不存在）仍必须永久拒绝。"""
    db = FakeControlPlane(config_version=2, point_ids=(1,))
    repository = _repository(db)
    await _warm_cache(repository, db)

    decisions = await repository.validate_items(
        db.node_uuid, [_item(2, point_id=99, config_version=2)]
    )

    assert decisions[0].action == "consume_reject"
    assert decisions[0].status_override == "rejected_consumed"
    assert decisions[0].reason == "malformed_envelope:unknown_var_id"


@pytest.mark.asyncio
async def test_future_config_version_is_still_permanently_rejected() -> None:
    """版本号领先于控制面（真正非法）仍必须永久拒绝，不得被重读放宽。"""
    db = FakeControlPlane(config_version=2, point_ids=(1,))
    repository = _repository(db)
    await _warm_cache(repository, db)

    decisions = await repository.validate_items(
        db.node_uuid, [_item(2, point_id=1, config_version=99)]
    )

    assert decisions[0].status_override == "rejected_consumed"
    assert decisions[0].reason == "unknown_config_version"


@pytest.mark.asyncio
async def test_node_mismatch_is_still_permanently_rejected() -> None:
    """越权：节点 UUID 不匹配的帧仍必须永久拒绝。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,))
    repository = _repository(db)
    await _warm_cache(repository, db)

    decisions = await repository.validate_items(
        str(uuid4()), [_item(2, point_id=1, config_version=1)]
    )

    assert decisions[0].status_override == "rejected_consumed"
    assert decisions[0].reason == "stale_epoch"


@pytest.mark.asyncio
async def test_every_worker_cache_self_heals_without_pubsub_delivery() -> None:
    """验收④：多 worker（进程内独立缓存）各自权威重读，不依赖 pub/sub 送达。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,))
    workers = [_repository(db) for _ in range(3)]
    for worker in workers:
        await _warm_cache(worker, db)

    db.config_version = 2
    db.point_ids = {1, 2}
    reads_before = (db.assignment_reads, db.mapping_reads)

    for worker in workers:
        decisions = await worker.validate_items(
            db.node_uuid, [_item(2, point_id=2, config_version=2)]
        )
        assert [decision.action for decision in decisions] == ["accept"]

    # 每个 worker 各自完成一次权威重读（3 个 worker → 3 次），互不依赖 pub/sub 送达
    assert db.assignment_reads - reads_before[0] == len(workers)
    assert db.mapping_reads - reads_before[1] == len(workers)


def test_mappings_and_assignments_scope_clear_control_caches() -> None:
    """契约：控制面发布的 scope 必须能清掉 B7 涉及的两个缓存命名空间。"""
    repository = MySQLIngestRepository(None)  # type: ignore[arg-type]
    repository._assignment_cache[("QC100", "g001", 1)] = (100.0, None)
    repository._mapping_cache[("QC100", "g001")] = (100.0, frozenset({1}))

    repository.invalidate_control_cache({"scope": "assignments"})
    assert repository._assignment_cache == {}
    assert repository._mapping_cache

    repository._assignment_cache[("QC100", "g001", 1)] = (100.0, None)
    repository.invalidate_control_cache({"scope": "mappings"})
    assert repository._mapping_cache == {}


class _AckStore:
    """最小 IngestStore 替身：记录收到的 action，全部回 ack。"""

    def __init__(self) -> None:
        self.actions: list[str] = []

    async def process_items(
        self, node_uuid: str, entries: list[tuple[UploadItem, str]], *, nonce: str
    ) -> list[RedisProcessResult]:
        self.actions = [action for _, action in entries]
        return [RedisProcessResult("ack") for _ in entries]

    async def switch_session(
        self, node_uuid: str, old_session_id: str, new_session_id: str
    ) -> str:
        return "switched"

    async def get_active_session(self, node_uuid: str) -> str | None:
        return None


@pytest.mark.asyncio
async def test_service_entry_returns_ack_not_rejected_consumed() -> None:
    """服务层入口（IngestService.ingest）对落后缓存下的合法帧必须返回 ack。

    这是清单验收「合法帧不得 rejected_consumed」在真实服务入口上的断言：
    上游 edge 只把 rejected_consumed 记为 dead（edge/app/uploader.py:326-339）。
    """
    db = FakeControlPlane(config_version=1, point_ids=(1,))
    repository = _repository(db)
    await _warm_cache(repository, db)

    db.config_version = 2
    db.point_ids = {1, 2}

    store = _AckStore()
    service = IngestService(repository, store, max_batch_items=10)
    batch = UploadBatch(
        node_uuid=db.node_uuid,
        request_id=str(uuid4()),
        items=(_item(2, point_id=2, config_version=2),),
    )

    results = await service.ingest(batch)

    assert [result.status for result in results] == ["ack"]
    assert store.actions == ["accept"]


# ---------------------------------------------------------------------------
# 补齐轮（B7 补齐）：两条同类漏覆盖分支 —— `unknown_device`（assignment 负面缓存）
# 与 `malformed_envelope:device_type_mismatch`（update_device 改 device_type）。
# 这两条 reason 在补齐前不在可重读集合内，控制面已提交 + 失效通知丢失时合法帧会被
# 永久拒收（判 `rejected_consumed` → edge 记 `dead`）；补齐后走既有 assignment 轮。
# 全部为纯单元（不连 MySQL / Redis），CI 无隔离库也可运行。
# ---------------------------------------------------------------------------


@pytest.mark.asyncio
async def test_missing_assignment_recovers_when_assignment_is_created() -> None:
    """`unknown_device`：assignment 缺失被负面缓存 → 控制面补建 + 通知丢失 → 合法帧 accept。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,), assignment_present=False)
    repository = _repository(db)
    node = db.node_uuid

    first = await repository.validate_items(
        node, [_item(1, point_id=1, config_version=1)]
    )
    assert first[0].reason == "unknown_device"
    assert first[0].status_override == "rejected_consumed"
    assert (db.assignment_reads, db.mapping_reads) == (1, 1)

    # 控制面已提交 assignment（同 node / 同 epoch）；本用例从不调用 invalidate_control_cache
    db.assignment_present = True

    second = await repository.validate_items(
        node, [_item(2, point_id=1, config_version=1)]
    )
    assert [decision.action for decision in second] == ["accept"]
    # 权威重读精确发生在 assignment 命名空间，mapping 不增
    assert (db.assignment_reads, db.mapping_reads) == (2, 1)


@pytest.mark.asyncio
async def test_device_type_change_recovers_without_notification() -> None:
    """`device_type_mismatch`：update_device 改 device_type + 通知丢失 → 合法帧 accept。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,), device_type="qc")
    repository = _repository(db)
    node = db.node_uuid

    warm = await repository.validate_items(
        node, [_item(1, point_id=1, config_version=1, device_type="qc")]
    )
    assert [decision.action for decision in warm] == ["accept"]

    db.device_type = "qc2"  # 控制面改了 device_type；通知丢失

    second = await repository.validate_items(
        node, [_item(2, point_id=1, config_version=1, device_type="qc2")]
    )
    assert [decision.action for decision in second] == ["accept"]
    assert (db.assignment_reads, db.mapping_reads) == (2, 1)


@pytest.mark.asyncio
async def test_missing_assignment_is_still_rejected_after_reread() -> None:
    """反向：设备确实不存在（真非法）→ 权威重读发生（+1）后仍 rejected_consumed。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,), assignment_present=False)
    repository = _repository(db)
    node = db.node_uuid

    await repository.validate_items(node, [_item(1, point_id=1, config_version=1)])

    second = await repository.validate_items(
        node, [_item(2, point_id=1, config_version=1)]
    )

    assert second[0].action == "consume_reject"
    assert second[0].status_override == "rejected_consumed"
    assert second[0].reason == "unknown_device"
    assert (db.assignment_reads, db.mapping_reads) == (2, 1)


@pytest.mark.asyncio
async def test_genuine_device_type_mismatch_is_still_rejected_after_reread() -> None:
    """反向：真正的 device_type 不匹配 → 权威重读发生（+1）后仍 rejected_consumed。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,), device_type="qc")
    repository = _repository(db)
    node = db.node_uuid

    await repository.validate_items(node, [_item(1, point_id=1, config_version=1)])

    decisions = await repository.validate_items(
        node, [_item(2, point_id=1, config_version=1, device_type="evil")]
    )

    assert decisions[0].action == "consume_reject"
    assert decisions[0].status_override == "rejected_consumed"
    assert decisions[0].reason == "malformed_envelope:device_type_mismatch"
    assert (db.assignment_reads, db.mapping_reads) == (2, 1)


@pytest.mark.asyncio
async def test_node_mismatch_stays_rejected_without_any_reread() -> None:
    """反向：越权（node_uuid 不匹配）刻意不在重读集合内 → 仍拒绝且两个命名空间 0 增量。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,))
    repository = _repository(db)

    await repository.validate_items(
        db.node_uuid, [_item(1, point_id=1, config_version=1)]
    )

    decisions = await repository.validate_items(
        str(uuid4()), [_item(2, point_id=1, config_version=1)]
    )

    assert decisions[0].status_override == "rejected_consumed"
    assert decisions[0].reason == "stale_epoch"
    assert (db.assignment_reads, db.mapping_reads) == (1, 1)


@pytest.mark.asyncio
async def test_missing_assignment_reread_is_bounded_to_one_per_window() -> None:
    """受控性：`unknown_device` 的真非法重复帧，同一 key 每 TTL 窗口最多额外重读一次。"""
    db = FakeControlPlane(config_version=1, point_ids=(1,), assignment_present=False)
    repository = _repository(db)
    node = db.node_uuid

    await repository.validate_items(node, [_item(1, point_id=1, config_version=1)])

    for seq in range(2, 22):
        decisions = await repository.validate_items(
            node, [_item(seq, point_id=1, config_version=1)]
        )
        assert [decision.status_override for decision in decisions] == [
            "rejected_consumed"
        ]

    assert (db.assignment_reads, db.mapping_reads) == (2, 1)
