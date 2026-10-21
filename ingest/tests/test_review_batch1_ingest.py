"""审查批次1（中心 Ingest 侧）回归：B003 / B026 / B029 / B022。"""

from __future__ import annotations

import json
from uuid import uuid4

import pytest
from redis.exceptions import ResponseError

from ingest.app.redis_store import (
    INGEST_BATCH_LUA,
    IngestRedisStore,
    TransientStoreError,
)
from ingest.app.schemas import UploadBatch
from ingest.app.service import IngestService
from ingest.tests.test_b7_authoritative_reread import (
    FakeControlPlane,
    _AckStore,
    _item,
    _repository,
    _warm_cache,
)
from ingest.tests.test_redis_store import FakeRedis, _settings


# ── B003：会话切换 ACK 前要求 AOF 持久化确认 ─────────────────────────────────


class _SwitchRedis(FakeRedis):
    def __init__(self, result: str = "switched") -> None:
        super().__init__()
        self.eval_result = result
        self.waitaof_calls = 0

    def execute_command(self, *args):
        self.waitaof_calls += 1
        return super().execute_command(*args)


@pytest.mark.asyncio
async def test_b003_switch_waits_for_aof_before_ack() -> None:
    client = _SwitchRedis()
    store = IngestRedisStore(client, _settings())

    assert await store.switch_session(str(uuid4()), str(uuid4()), str(uuid4())) == "switched"
    assert client.waitaof_calls == 1


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "persisted",
    [
        [0, 0],
        ResponseError("ERR WAITAOF cannot be used when numlocal is set but appendonly is disabled."),
    ],
    ids=["unconfirmed", "aof-disabled"],
)
async def test_b003_unconfirmed_switch_is_not_acknowledged(persisted) -> None:
    client = _SwitchRedis()
    client.persisted = persisted
    store = IngestRedisStore(client, _settings())

    with pytest.raises(TransientStoreError):
        await store.switch_session(str(uuid4()), str(uuid4()), str(uuid4()))


@pytest.mark.asyncio
async def test_b003_redis_without_waitaof_falls_back_to_eval_result() -> None:
    client = _SwitchRedis("already_switched")
    client.persisted = ResponseError("ERR unknown command 'WAITAOF', with args beginning with: ")
    store = IngestRedisStore(client, _settings())

    assert await store.switch_session(str(uuid4()), str(uuid4()), str(uuid4())) == "already_switched"


@pytest.mark.asyncio
async def test_b003_conflict_needs_no_durability() -> None:
    client = _SwitchRedis("session_conflict")
    client.persisted = [0, 0]
    store = IngestRedisStore(client, _settings())

    assert await store.switch_session(str(uuid4()), str(uuid4()), str(uuid4())) == "session_conflict"


# ── B026：限流窗口内的第二次扩容不能被永久驳回 ───────────────────────────────


@pytest.mark.asyncio
async def test_b026_second_expansion_within_ttl_is_retry_later_not_rejected() -> None:
    db = FakeControlPlane(config_version=1, point_ids=(1,))
    repository = _repository(db)
    await _warm_cache(repository, db)

    # 第一次扩容：权威重读后接受
    db.point_ids = {1, 2}
    first = await repository.validate_items(db.node_uuid, [_item(2, point_id=2)])
    assert [decision.action for decision in first] == ["accept"]

    # 同一 TTL 窗口内再次扩容：重读被限流，旧缓存不能把新点位判死
    db.point_ids = {1, 2, 3}
    second = await repository.validate_items(db.node_uuid, [_item(3, point_id=3)])
    assert [decision.action for decision in second] == ["retry_later"]
    assert second[0].status_override == "retry_later"

    # 限流窗口过去后权威确认，合法数据被接受
    repository._reread_at.clear()
    third = await repository.validate_items(db.node_uuid, [_item(3, point_id=3)])
    assert [decision.action for decision in third] == ["accept"]


@pytest.mark.asyncio
async def test_b026_service_stops_batch_at_retry_later() -> None:
    db = FakeControlPlane(config_version=1, point_ids=(1,))
    repository = _repository(db)
    await _warm_cache(repository, db)
    db.point_ids = {1, 2}
    await repository.validate_items(db.node_uuid, [_item(2, point_id=2)])
    db.point_ids = {1, 2, 3}

    session = str(uuid4())
    items = [_item(1, point_id=1), _item(2, point_id=3), _item(3, point_id=1)]
    payload = {
        "request_id": str(uuid4()),
        "node_uuid": db.node_uuid,
        "items": [
            {
                **{
                    key: getattr(item, key)
                    for key in (
                        "batch_id",
                        "local_seq",
                        "assignment_epoch",
                        "device_code",
                        "device_type",
                        "group_code",
                        "scan_rate",
                        "ts",
                        "config_version",
                        "values",
                    )
                },
                "upload_session_id": session,
            }
            for item in items
        ],
    }
    store = _AckStore()
    service = IngestService(repository, store, max_batch_items=10)

    results = await service.ingest(UploadBatch.from_dict(payload))

    assert store.actions == ["accept", "retry_later", "accept"]
    assert [result.status for result in results] == ["ack", "retry_later"]
    assert results[1].reason == "malformed_envelope:unknown_var_id"


def test_b026_lua_does_not_consume_retry_later_items() -> None:
    branch = INGEST_BATCH_LUA.split('elseif item.action == "retry_later" then', 1)[1]
    branch = branch.split("elseif", 1)[0]
    assert 'table.insert(results, "retry_later")' in branch
    assert "break" in branch
    assert "SET" not in branch


# ── B029：latest 旁路键与历史键同一成员校验 ─────────────────────────────────


@pytest.mark.asyncio
async def test_b029_latest_key_outside_mapping_is_rejected() -> None:
    db = FakeControlPlane(config_version=1, point_ids=(1, 2))
    repository = _repository(db)
    base = _item(1, point_id=1)

    legal = type(base)(
        **{
            **{field: getattr(base, field) for field in base.__dataclass_fields__},
            "latest_values": {"1": "12.5", "2": "abc"},
            "latest_qualities": {"1": 0, "2": 0},
            "latest_timestamps": {"1": base.ts, "2": base.ts},
        }
    )
    outside = type(base)(
        **{
            **{field: getattr(base, field) for field in base.__dataclass_fields__},
            "latest_values": {"1": "12.5", "999": "1"},
            "latest_qualities": {"1": 0, "999": 0},
            "latest_timestamps": {"1": base.ts, "999": base.ts},
        }
    )

    decisions = await repository.validate_items(db.node_uuid, [legal])
    assert [decision.action for decision in decisions] == ["accept"]
    decisions = await repository.validate_items(db.node_uuid, [outside])
    assert decisions[0].status_override == "rejected_consumed"
    assert decisions[0].reason == "malformed_envelope:unknown_var_id"


# ── B022（中心侧）：缺值的坏质量点不伪造 v ──────────────────────────────────


@pytest.mark.asyncio
async def test_b022_all_bad_quality_points_carry_no_value() -> None:
    client = FakeRedis()
    base = _item(1, point_id=1)
    entry = type(base)(
        **{
            **{field: getattr(base, field) for field in base.__dataclass_fields__},
            "values": {},
            "latest_values": {},
            "latest_qualities": {"1": 5, "2": 7},
            "latest_timestamps": {"1": base.ts, "2": base.ts},
        }
    )
    await IngestRedisStore(client, _settings()).process_item(str(uuid4()), entry, "accept")
    updates = json.loads(client.eval_call[2][-3])[0]["latest_updates"]
    assert updates == {
        "1": {"t": base.ts, "q": 5, "e": 1},
        "2": {"t": base.ts, "q": 7, "e": 1},
    }


# ── PR #26 审查：部署前提 Redis >= 7.2 + appendonly yes，启动/健康检查要能发现 ──


class _InfoRedis:
    def __init__(self, version: str = "7.4.1", aof_enabled=1, error: Exception | None = None):
        self.version = version
        self.aof_enabled = aof_enabled
        self.error = error

    async def info(self, section: str):
        if self.error is not None:
            raise self.error
        if section == "server":
            return {"redis_version": self.version}
        assert section == "persistence"
        return {"aof_enabled": self.aof_enabled}


@pytest.mark.asyncio
async def test_pr26_redis_requirements_ok() -> None:
    report = await IngestRedisStore(_InfoRedis(), _settings()).check_persistence_requirements()
    assert report.verified and report.healthy


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("client", "needle"),
    [
        (_InfoRedis(version="7.0.15"), "older than 7.2"),
        (_InfoRedis(version="6.2.14", aof_enabled=1), "WAITAOF is unsupported"),
        (_InfoRedis(aof_enabled=0), "appendonly yes"),
        (_InfoRedis(aof_enabled="0"), "AOF is disabled"),
        (_InfoRedis(version="weird"), "cannot parse"),
    ],
    ids=["7.0", "6.2", "aof-off-int", "aof-off-str", "unparsable"],
)
async def test_pr26_redis_requirement_violations_are_reported(client, needle) -> None:
    report = await IngestRedisStore(client, _settings()).check_persistence_requirements()
    assert report.verified
    assert not report.healthy
    assert any(needle in problem for problem in report.problems)


@pytest.mark.asyncio
async def test_pr26_old_redis_with_aof_off_reports_both() -> None:
    report = await IngestRedisStore(
        _InfoRedis(version="7.0.0", aof_enabled=0), _settings()
    ).check_persistence_requirements()
    assert len(report.problems) == 2


@pytest.mark.asyncio
async def test_pr26_unverifiable_redis_is_not_a_violation() -> None:
    report = await IngestRedisStore(
        _InfoRedis(error=ResponseError("NOPERM this user has no permissions to run 'info'")),
        _settings(),
    ).check_persistence_requirements()
    assert report.verified is False
    assert report.healthy
    assert "NOPERM" in report.detail


@pytest.mark.asyncio
async def test_pr26_healthz_is_unhealthy_when_requirements_are_violated() -> None:
    from types import SimpleNamespace

    from ingest.app import main as ingest_main

    store = IngestRedisStore(_InfoRedis(version="7.0.0"), _settings())
    calls = {"n": 0}

    async def provider():
        calls["n"] += 1
        return SimpleNamespace(store=store)

    check = ingest_main._RedisRequirementCheck(provider)
    response = ingest_main._health_response(_settings(), await check.report())
    assert response.status_code == 503
    body = json.loads(response.body)
    assert body["status"] == "unhealthy"
    assert any("7.2" in problem for problem in body["redis_requirements"])
    await check.report()
    assert calls["n"] == 1  # 30s 内复用结论

    healthy = IngestRedisStore(_InfoRedis(), _settings())
    ok = ingest_main._health_response(
        _settings(), await healthy.check_persistence_requirements()
    )
    assert ok["status"] == "ready" and ok["redis_requirements"] == "ok"
    unknown = ingest_main._health_response(_settings(), None)
    assert unknown["status"] == "ready" and unknown["redis_requirements"] == "unverified"
