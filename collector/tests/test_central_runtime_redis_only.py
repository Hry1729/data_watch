"""中心式采集：**真实运行**测试（不是 AST/构造器模拟）。

构造真实 CollectorApp 且**不传 writer_factory**，用 fake 驱动跑一轮真实的
采集 → 解析 → 分组 → Redis 写入 → 正常关闭，并断言：

  * 实际使用 RedisWriter（storage_path = "redis_only"），不是 Durable；
  * 数据真的写进了 Redis（latest / Stream 有内容）；
  * 不创建采样 SQLite（无 .db / -wal / -shm / owner lock）；
  * 不启动 Durable 回放线程，也不产生 receipt Lua / WAITAOF 调用；
  * **把 DurableRedisWriter 构造器换成"调用即失败"后，这一轮仍能跑完**。

需要一个隔离 Redis（环境变量 DW_CENTRAL_TEST_REDIS_URL 指定，形如 redis://127.0.0.1:56393/0）。
未配置时跳过，并在跳过原因里写明需要隔离 Redis。
"""

from __future__ import annotations

import asyncio
import json
import os
from pathlib import Path
import shutil
import threading

import pytest

from core.config import CollectorConfig, DeviceConfig, RedisConfig, VariableConfig
from main import CollectorApp
from storage.redis_writer import RedisWriter

FAKE_DEVICE = "QC_FAKE100"


def _redis_target() -> tuple[str, int, int]:
    url = os.environ.get("DW_CENTRAL_TEST_REDIS_URL", "")
    if not url:
        pytest.skip("需要隔离 Redis：设置 DW_CENTRAL_TEST_REDIS_URL=redis://127.0.0.1:<port>/0")
    from urllib.parse import urlparse
    parsed = urlparse(url)
    assert parsed.scheme == "redis" and parsed.hostname in {"127.0.0.1", "localhost"}
    assert parsed.port and parsed.port != 6379, "必须显式指定隔离 Redis 端口，禁止业务默认端口"
    return parsed.hostname, parsed.port, int((parsed.path or "/0").lstrip("/") or 0)


def _build_config(polling: bool) -> CollectorConfig:
    host, port, db = _redis_target()
    return CollectorConfig(
        redis=RedisConfig(host=host, port=port, db=db, stream_shard_count=4, stream_maxlen=0),
        devices=[DeviceConfig(FAKE_DEVICE, "QC", "s7", "127.0.0.1", scan_rate=0.1)] if polling else [],
        variables=[
            VariableConfig("900001", FAKE_DEVICE, "DB100.DBW0", "INT", 0.1, True, 1),
            VariableConfig("900002", FAKE_DEVICE, "DB100.DBW2", "INT", 0.1, True, 2),
        ] if polling else [],
        enable_fake_bootstrap=not polling,
        fake_mode_active=not polling,
    )


def _sqlite_artifacts(root: Path) -> list[str]:
    patterns = ("*.db", "*.db-wal", "*.db-shm", "*.sqlite", "*.sqlite-wal", "*.sqlite-shm", "*.lock")
    found = []
    for pattern in patterns:
        found.extend(str(p) for p in root.rglob(pattern))
    return found


def _durable_threads() -> list[str]:
    banned = ("collector-outbox", "collector-sqlite", "durable", "outbox", "replay")
    return [t.name for t in threading.enumerate() if any(b in t.name.lower() for b in banned)]


@pytest.mark.parametrize("polling", [False, True], ids=["no-devices-bootstrap", "s7-polling"])
def test_central_real_run_writes_redis_without_outbox(tmp_path, monkeypatch, activated_test_packages, polling, request):
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    monkeypatch.setenv("DATAWATCH_HOME", str(tmp_path))
    monkeypatch.setenv("DATAWATCH_COLLECTOR_STATUS_PORT", "0")
    # 禁用 Durable 构造器：中心运行绝不允许调用它
    from storage.durable_redis_writer import DurableRedisWriter
    from storage.collector_outbox import CollectorRetryOutbox

    def _boom(*_args, **_kwargs):
        raise AssertionError("中心默认路径不得构造 DurableRedisWriter")

    monkeypatch.setattr(DurableRedisWriter, "__init__", _boom)
    monkeypatch.setattr(CollectorRetryOutbox, "__init__", _boom)

    import storage.redis_client as redis_client_module
    monkeypatch.setattr(redis_client_module, "_pool", None)
    host, port, db = _redis_target()
    import redis as redis_module
    client = redis_module.Redis(host=host, port=port, db=db, decode_responses=True,
                                socket_connect_timeout=2, socket_timeout=2)
    request.addfinalizer(client.close)
    assert client.dbsize() == 0, "测试只允许使用空的隔离 Redis 数据库"
    before_waitaof = int(client.info("commandstats").get("cmdstat_waitaof", {}).get("calls", 0))

    if polling:
        # Keep real registry discovery, S7 planning and decoding in a temporary activation root.
        shutil.copytree(activated_test_packages / "drivers" / "s7", tmp_path / "drivers" / "s7")
    app = CollectorApp(_build_config(polling))          # 不传 writer_factory
    calls = {"connect": 0, "read": 0, "disconnect": 0}
    if polling:
        registration = app.device_manager._resolve_driver_registry().get("s7")
        assert registration is not None
        driver_class = registration.factory.__globals__["S7Driver"]

        async def connect(driver, config):
            calls["connect"] += 1
            driver._connected = True

        async def read(driver, blocks):
            calls["read"] += 1
            assert len(blocks) == 1  # two adjacent INTs use the real merged S7 plan
            return {blocks[0].address: bytes([0, 42, 0, 84])}

        async def disconnect(driver):
            calls["disconnect"] += 1
            driver._connected = False

        monkeypatch.setattr(driver_class, "connect", connect)
        monkeypatch.setattr(driver_class, "read", read)
        monkeypatch.setattr(driver_class, "disconnect", disconnect)

    pubsub = client.pubsub()
    request.addfinalizer(pubsub.close)
    pubsub.subscribe("rt:change")
    assert pubsub.get_message(timeout=2)["type"] == "subscribe"

    async def _run() -> None:
        # start() owns the service lifetime and returns only after stop().
        service = asyncio.create_task(app.start())
        try:
            async def _wait_for_data():
                while app._performance_task is None or not client.keys(f"rt:latest:{FAKE_DEVICE}:*"):
                    if service.done():
                        await service
                        pytest.fail("Collector exited before producing data")
                    await asyncio.sleep(0.05)
            await asyncio.wait_for(_wait_for_data(), timeout=15)
            assert not service.done()
            assert _sqlite_artifacts(tmp_path) == []
            assert _durable_threads() == []
        finally:
            app.stop()
            try:
                await asyncio.wait_for(service, timeout=20)
            finally:
                await asyncio.wait_for(app.shutdown(), timeout=20)
        assert app._closed

    asyncio.run(_run())
    if polling:
        assert calls["connect"] == calls["disconnect"] == 1
        assert calls["read"] >= 1

    # 1) 真实存储选择
    assert isinstance(app.redis_writer, RedisWriter), f"实际用的是 {type(app.redis_writer).__name__}"
    assert app.redis_writer.storage_path == "redis_only"

    # 2) 数据真的进了 Redis（latest 与 window；经真实 writer 调用）
    latest_keys = client.keys(f"rt:latest:{FAKE_DEVICE}:*")
    assert latest_keys, "中心直写后 latest 哈希应存在"
    hashes = [client.hgetall(key) for key in latest_keys]
    assert any(hashes), "latest 哈希不应为空"
    window_keys = client.keys(f"rt:window:{FAKE_DEVICE}:*")
    assert window_keys, "中心直写后 window 应有内容"
    assert any(client.llen(key) > 0 for key in window_keys), "window 列表不应为空"
    expected = {"900001": "42", "900002": "84"} if polling else {"900001": "23.4", "900002": "67.8"}
    latest = hashes[0]
    assert {key: json.loads(value)["v"] for key, value in latest.items()} == expected
    assert all(json.loads(value)["q"] == 0 for value in latest.values())
    frames = [json.loads(value) for key in window_keys for value in client.lrange(key, 0, -1)]
    assert all(frame["values"] == expected for frame in frames)
    entries = [fields for key in client.keys("rt:stream:*") for _, fields in client.xrange(key)
               if fields["device_code"] == FAKE_DEVICE]
    assert entries
    assert all(json.loads(entry["values"]) == expected for entry in entries)
    assert all(json.loads(entry["qualities"]) == {key: 0 for key in expected} for entry in entries)
    assert all(entry["contract_version"] == "2" for entry in entries)
    notification = pubsub.get_message(ignore_subscribe_messages=True, timeout=2)
    assert notification and json.loads(notification["data"])["device_code"] == FAKE_DEVICE

    # 3) 不创建采样 SQLite
    assert _sqlite_artifacts(tmp_path) == [], "中心路径不得创建采样 SQLite（含 WAL/SHM/lock）"

    # 4) 不启动 Durable 回放线程
    assert _durable_threads() == [], "中心路径不得启动 Durable 回放/Outbox 线程"

    # 5) 不产生 receipt Lua / WAITAOF 调用
    stats = client.info("commandstats")
    after_waitaof = int(stats.get("cmdstat_waitaof", {}).get("calls", 0))
    assert after_waitaof == before_waitaof, "中心直写路径不应等待 WAITAOF"
    assert int(stats.get("cmdstat_evalsha", {}).get("calls", 0)) == 0, "中心直写不应调用 receipt Lua"

    client.flushdb()


def test_edge_entry_still_injects_outbox_writer():
    """分布式入口仍通过 writer_factory 注入 EdgeOutboxWriter（不受中心恢复影响）。"""
    import ast
    from pathlib import Path as _Path

    repo = _Path(__file__).resolve().parents[2]
    source = (repo / "edge" / "collector_main.py").read_text(encoding="utf-8")
    tree = ast.parse(source)
    returned = [
        ast.unparse(node.value)
        for node in ast.walk(tree)
        if isinstance(node, ast.FunctionDef) and node.name == "_writer_factory"
        for node in node.body if isinstance(node, ast.Return)
    ]
    assert any("EdgeOutboxWriter" in item and "EdgeStorageClient" in item for item in returned), returned
    assert "writer_factory=_writer_factory" in source
