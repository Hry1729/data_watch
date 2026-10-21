"""H3 / Edge 16：Ingest 与 Writer 的 Redis DB、stream 分片数必须由 Agent 从同一配置来源下发。"""
from __future__ import annotations

from app.routers import admin


def _cfg(redis_extra: dict) -> dict:
    return {
        "mysql": {"host": "db", "port": 3306, "user": "root", "database": "dw"},
        "redis": {"host": "rds", "port": 6380, **redis_extra},
        "tdengine": {"host": "td", "port": 6041, "user": "root", "database": "dd"},
    }


def test_ingest_and_writer_share_redis_db_and_shard_count(monkeypatch):
    monkeypatch.setattr(admin, "load_config", lambda: _cfg({"db": 2, "stream_shard_count": 8}))
    monkeypatch.setattr(admin, "dpapi_decrypt", lambda _v: "")

    ingest = admin._ingest_env()
    writer = admin._writer_env()

    # 变量名与 ingest/app/config.py 实际读取的一致
    assert ingest["INGEST_REDIS_URL"] == "redis://rds:6380/2"
    assert ingest["INGEST_STREAM_SHARD_COUNT"] == "8"
    # 与 writer/main.py 的 DATAWATCH_REDIS_<FIELD> 覆盖约定一致
    assert writer["DATAWATCH_REDIS_DB"] == "2"
    assert writer["DATAWATCH_REDIS_STREAM_SHARD_COUNT"] == "8"


def test_defaults_match_both_sides(monkeypatch):
    monkeypatch.setattr(admin, "load_config", lambda: _cfg({}))
    monkeypatch.setattr(admin, "dpapi_decrypt", lambda _v: "")

    ingest = admin._ingest_env()
    writer = admin._writer_env()

    assert ingest["INGEST_REDIS_URL"].endswith("/0")
    assert writer["DATAWATCH_REDIS_DB"] == "0"
    assert ingest["INGEST_STREAM_SHARD_COUNT"] == writer["DATAWATCH_REDIS_STREAM_SHARD_COUNT"] == "4"


def test_center_collector_env_shares_stream_contract_with_writer(monkeypatch):
    """B013：中心式 Collector 与 Writer 的 Redis DB/分片数必须完全一致。"""
    monkeypatch.setattr(admin, "load_config", lambda: _cfg({"db": 3, "stream_shard_count": 8}))
    monkeypatch.setattr(admin, "dpapi_decrypt", lambda _v: "")
    monkeypatch.setattr(admin, "build_role", lambda: "center")
    collector = admin._collector_env()
    writer = admin._writer_env()
    assert collector["DATAWATCH_REDIS_DB"] == writer["DATAWATCH_REDIS_DB"] == "3"
    assert (
        collector["DATAWATCH_REDIS_STREAM_SHARD_COUNT"]
        == writer["DATAWATCH_REDIS_STREAM_SHARD_COUNT"]
        == "8"
    )
    for key in ("DATAWATCH_REDIS_HOST", "DATAWATCH_REDIS_PORT", "DATAWATCH_REDIS_PASSWORD"):
        assert collector[key] == writer[key]
