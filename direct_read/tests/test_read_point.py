"""用假驱动覆盖直连读的成功、超时、限流、鉴权、缺失、歧义和拓扑来源。"""

from __future__ import annotations

import asyncio
import builtins
import inspect
import json
import time
from pathlib import Path

import pytest

from direct_read.api import _override_driver_factory, read_point
from direct_read.errors import DirectReadError, TopologyUnavailable
from direct_read.quality import (
    DIRECT_SOURCE,
    QUALITY_GOOD,
    QUALITY_NOT_CONNECTED,
    quality_name,
)
from direct_read.topology import select_target


class FakeDriver:
    def __init__(self, *, value: object = 7, delay: float = 0, error: Exception | None = None) -> None:
        self.value = value
        self.delay = delay
        self.error = error
        self.events: list[str] = []
        self.config = None
        self.wrote = False

    async def connect(self, config):
        self.events.append("connect")
        self.config = dict(config)

    async def disconnect(self):
        self.events.append("disconnect")

    async def read(self, blocks):
        self.events.append("read")
        if self.delay:
            await asyncio.sleep(self.delay)
        if self.error is not None:
            raise self.error
        return {blocks[0].address: [self.value]}

    async def write(self, _points):
        self.wrote = True
        raise AssertionError("直连路径不能写 PLC")


class Clock:
    def __init__(self) -> None:
        self.t = 1_700_000_000.0

    def __call__(self) -> float:
        return self.t

    def advance(self, seconds: float) -> None:
        self.t += seconds


class MemorySource:
    def __init__(self, name: str, devices=None, error: Exception | None = None) -> None:
        self.name = name
        self.devices = devices or []
        self.error = error
        self.calls = 0

    def resolve(self, device: str, point: str):
        self.calls += 1
        if self.error is not None:
            raise self.error
        return select_target(self.devices, device, point)


def _qc100(**overrides):
    device = {
        "deviceId": 7,
        "deviceCode": "QC100",
        "deviceName": "桥吊100",
        "protocol": "s7",
        "endpoint": "10.1.2.3",
        "host": "10.1.2.3",
        "port": 102,
        "rack": 0,
        "slot": 1,
        "enable": 1,
        "points": [
            {
                "pointName": "CraneID",
                "ioAddress": "DB1702.DBW0",
                "dataType": "INT",
                "varSeq": 12,
            }
        ],
    }
    device.update(overrides)
    return device


def _read(tmp_path: Path, **kwargs):
    created: list[FakeDriver] = kwargs.pop("drivers", None)
    if created is None:
        created = []
    driver_options = kwargs.pop("driver_options", {})
    preset = kwargs.pop("driver_factory", None)

    def make_driver():
        driver = FakeDriver(**driver_options)
        created.append(driver)
        return driver

    with _override_driver_factory(preset or make_driver):
        result = read_point(
            kwargs.pop("device", "QC100"),
            kwargs.pop("point", "CraneID"),
            actor=kwargs.pop("actor", "operator"),
            state_dir=kwargs.pop("state_dir", tmp_path),
            audit_path=kwargs.pop("audit_path", tmp_path / "audit.log"),
            now=kwargs.pop("now", None),
            **kwargs,
        )
    return result, created


def test_success_returns_direct_sample_and_disconnects(tmp_path: Path) -> None:
    source = MemorySource("mysql", [_qc100()])
    result, drivers = _read(tmp_path, sources=[source])

    assert result["source"] == DIRECT_SOURCE
    assert result["topologySource"] == "mysql"
    assert result["protocol"] == "s7"
    assert result["value"] == 7
    assert result["quality"] == QUALITY_GOOD
    assert result["qualityName"] == quality_name(QUALITY_GOOD)
    assert result["device"] == {"deviceCode": "QC100", "deviceName": "桥吊100"}
    assert result["point"]["ioAddress"] == "DB1702.DBW0"
    assert result["point"]["varSeq"] == 12
    assert result["connection"] == {"endpoint": "10.1.2.3", "port": 102, "rack": 0, "slot": 1}
    assert drivers[0].events == ["connect", "read", "disconnect"]
    assert drivers[0].wrote is False
    assert 1 <= drivers[0].config["read_timeout_ms"] <= 5000
    assert drivers[0].config["ip"] == "10.1.2.3"
    audit = json.loads((tmp_path / "audit.log").read_text(encoding="utf-8"))
    assert audit["actor"] == "operator"
    assert audit["device"] == "QC100"
    assert audit["point"] == "CraneID"
    assert audit["outcome"] == "ok"
    assert "password" not in audit
    assert "token" not in audit


def test_field_name_resolves_to_device_code(tmp_path: Path) -> None:
    source = MemorySource("mysql", [_qc100()])
    result, _drivers = _read(tmp_path, device="桥吊100", sources=[source])

    assert result["device"]["deviceCode"] == "QC100"
    assert result["topologySource"] == "mysql"


def test_timeout_disconnects_and_sets_not_connected(tmp_path: Path) -> None:
    source = MemorySource("mysql", [_qc100()])
    with pytest.raises(DirectReadError) as captured:
        _read(
            tmp_path,
            sources=[source],
            timeout_ms=50,
            driver_options={"delay": 2},
        )

    assert captured.value.error_type == "timeout"
    assert captured.value.details["source"] == DIRECT_SOURCE
    assert captured.value.details["quality"] == QUALITY_NOT_CONNECTED
    assert captured.value.details["qualityName"] == "NOT_CONNECTED"
    assert captured.value.details["topologySource"] == "mysql"


def test_timeout_still_disconnects(tmp_path: Path) -> None:
    source = MemorySource("mysql", [_qc100()])
    created: list[FakeDriver] = []
    # 总预算从拓扑解析就开始算。50ms 在慢的 Windows runner 上会在创建驱动前耗尽，
    # 这条用例就看不到「读超时之后仍然断开」。1000ms 仍小于驱动的 2 秒延迟，超时点不变。
    with pytest.raises(DirectReadError):
        _read(
            tmp_path,
            sources=[source],
            timeout_ms=1000,
            driver_options={"delay": 2},
            drivers=created,
        )
    assert created[0].events[0] == "connect"
    assert created[0].events[-1] == "disconnect"
    assert created[0].wrote is False


def test_rate_limit_blocks_second_read_of_same_device(tmp_path: Path) -> None:
    source = MemorySource("mysql", [_qc100()])
    clock = Clock()
    created: list[FakeDriver] = []
    _read(tmp_path, sources=[source], now=clock, drivers=created)
    with pytest.raises(DirectReadError) as captured:
        _read(tmp_path, sources=[source], now=clock, drivers=created, device="桥吊100")

    assert captured.value.error_type == "rate_limited"
    assert captured.value.details["limit"] == "device_interval"
    assert captured.value.details["endpoint"] == "10.1.2.3:102"
    assert captured.value.details["deviceCode"] == "QC100"
    assert captured.value.details["source"] == DIRECT_SOURCE
    assert captured.value.details["quality"] == QUALITY_NOT_CONNECTED
    assert len(created) == 1
    clock.advance(0.5)
    _read(tmp_path, sources=[source], now=clock, drivers=created)
    assert len(created) == 2


def test_auth_refused_without_actor_does_not_touch_driver_or_topology(tmp_path: Path) -> None:
    source = MemorySource("mysql", [_qc100()])

    def factory():
        raise AssertionError("未登录不能创建驱动")

    with pytest.raises(DirectReadError) as captured:
        with _override_driver_factory(factory):
            read_point(
                "QC100",
                "CraneID",
                actor="  ",
                sources=[source],
                state_dir=tmp_path,
                audit_path=tmp_path / "audit.log",
            )

    assert captured.value.error_type == "auth_required"
    assert captured.value.details["source"] == DIRECT_SOURCE
    assert source.calls == 0
    audit = json.loads((tmp_path / "audit.log").read_text(encoding="utf-8"))
    assert audit["outcome"] == "auth_required"
    assert audit["actor"] is None


def test_read_point_has_no_auth_bypass_parameter() -> None:
    names = set(inspect.signature(read_point).parameters)
    assert names.isdisjoint(
        {"allowlist", "anonymous", "skip_auth", "token", "password", "driver_factory"}
    )


def test_device_not_found(tmp_path: Path) -> None:
    source = MemorySource("mysql", [_qc100()])

    def factory():
        raise AssertionError("找不到设备时不能连接 PLC")

    with pytest.raises(DirectReadError) as captured:
        with _override_driver_factory(factory):
            read_point(
                "QC999",
                "CraneID",
                actor="operator",
                sources=[source],
                state_dir=tmp_path,
                audit_path=tmp_path / "audit.log",
            )

    assert captured.value.error_type == "resource_not_found"
    assert captured.value.status == 404
    assert captured.value.details["topologySource"] == "mysql"
    assert captured.value.details["source"] == DIRECT_SOURCE


def test_ambiguous_point_does_not_pick_the_first(tmp_path: Path) -> None:
    device = _qc100()
    device["points"] = [
        {"pointName": "CraneA", "ioAddress": "DB1.DBW0", "dataType": "INT", "varSeq": 1},
        {"pointName": "CraneB", "ioAddress": "DB1.DBW2", "dataType": "INT", "varSeq": 2},
    ]
    source = MemorySource("snapshot", [device])

    def factory():
        raise AssertionError("歧义时不能连接 PLC")

    with pytest.raises(DirectReadError) as captured:
        with _override_driver_factory(factory):
            read_point(
                "QC100",
                "Crane",
                actor="operator",
                sources=[source],
                state_dir=tmp_path,
                audit_path=tmp_path / "audit.log",
            )

    assert captured.value.error_type == "ambiguous_resource"
    assert captured.value.status == 409
    names = {item["pointName"] for item in captured.value.details["candidates"]}
    assert names == {"CraneA", "CraneB"}
    assert captured.value.details["topologySource"] == "snapshot"


def test_mysql_source_wins_when_available(tmp_path: Path) -> None:
    mysql = MemorySource("mysql", [_qc100(endpoint="10.1.2.3", host="10.1.2.3")])
    snapshot = MemorySource("snapshot", [_qc100(endpoint="10.9.9.9", host="10.9.9.9")])
    result, _drivers = _read(tmp_path, sources=[mysql, snapshot])

    assert result["topologySource"] == "mysql"
    assert result["connection"]["endpoint"] == "10.1.2.3"
    assert snapshot.calls == 0


def test_snapshot_used_only_when_mysql_unavailable(tmp_path: Path) -> None:
    mysql = MemorySource("mysql", error=TopologyUnavailable("数据库连不上"))
    snapshot = MemorySource("snapshot", [_qc100(endpoint="10.8.8.8", host="10.8.8.8")])
    result, _drivers = _read(tmp_path, sources=[mysql, snapshot])

    assert result["topologySource"] == "snapshot"
    assert result["connection"]["endpoint"] == "10.8.8.8"
    assert mysql.calls == 1


def test_auto_does_not_fall_back_to_explicit_endpoint(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.delenv("DATAWATCH_MYSQL_HOST", raising=False)
    monkeypatch.delenv("DATAWATCH_MYSQL_USER", raising=False)
    monkeypatch.delenv("DATAWATCH_MYSQL_DATABASE", raising=False)
    monkeypatch.delenv("DATAWATCH_DATA_ROOT", raising=False)
    created: list[FakeDriver] = []
    with pytest.raises(DirectReadError) as captured:
        _read(
            tmp_path,
            topology="auto",
            endpoint="10.6.6.6",
            address="DB1.DBW0",
            drivers=created,
        )

    assert captured.value.error_type == "topology_unavailable"
    assert created == []
    assert "10.6.6.6" not in json.dumps(captured.value.details, ensure_ascii=False)


def test_explicit_topology_reads_the_given_endpoint(tmp_path: Path) -> None:
    result, drivers = _read(
        tmp_path,
        topology="explicit",
        endpoint="10.7.7.7:1102",
        address="DB1.DBW4",
        protocol="s7",
        rack=0,
        slot=2,
        point="Speed",
    )

    assert result["topologySource"] == "explicit"
    assert result["connection"] == {"endpoint": "10.7.7.7", "port": 1102, "rack": 0, "slot": 2}
    assert result["point"]["ioAddress"] == "DB1.DBW4"
    assert drivers[0].config["port"] == 1102
    assert drivers[0].config["slot"] == 2


def test_explicit_rate_limit_is_keyed_by_host_port(tmp_path: Path) -> None:
    clock = Clock()
    created: list[FakeDriver] = []
    _read(
        tmp_path,
        topology="explicit",
        endpoint="10.1.2.3:1102",
        address="DB1.DBW0",
        device="CraneA",
        point="Speed",
        now=clock,
        drivers=created,
    )
    with pytest.raises(DirectReadError) as captured:
        _read(
            tmp_path,
            topology="explicit",
            endpoint="10.1.2.3:1102",
            address="DB1.DBW2",
            device="CraneB",
            point="Torque",
            now=clock,
            drivers=created,
        )

    assert captured.value.error_type == "rate_limited"
    assert captured.value.details["limit"] == "device_interval"
    assert captured.value.details["endpoint"] == "10.1.2.3:1102"
    assert captured.value.details["deviceCode"] == "CraneB"
    assert len(created) == 1
    _read(
        tmp_path,
        topology="explicit",
        endpoint="10.9.9.9:1102",
        address="DB1.DBW0",
        device="CraneA",
        point="Speed",
        now=clock,
        drivers=created,
    )
    assert len(created) == 2


def test_missing_pymysql_falls_back_to_snapshot_not_explicit(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setenv("DATAWATCH_MYSQL_HOST", "127.0.0.1")
    monkeypatch.setenv("DATAWATCH_MYSQL_USER", "ro")
    monkeypatch.setenv("DATAWATCH_MYSQL_DATABASE", "device_watch")
    snapshot = tmp_path / "topology.json"
    snapshot.write_text(
        json.dumps({"devices": [_qc100()]}, ensure_ascii=False),
        encoding="utf-8",
    )
    real_import = builtins.__import__

    def blocked(name, globals=None, locals=None, fromlist=(), level=0):
        if name == "pymysql" or name.startswith("pymysql."):
            raise ImportError("pymysql missing")
        return real_import(name, globals, locals, fromlist, level)

    monkeypatch.setattr(builtins, "__import__", blocked)
    result, _drivers = _read(
        tmp_path,
        topology="auto",
        snapshot_path=str(snapshot),
        endpoint="10.6.6.6",
        address="DB9.DBW9",
    )

    assert result["topologySource"] == "snapshot"
    assert result["connection"]["endpoint"] == "10.1.2.3"
    assert result["point"]["ioAddress"] == "DB1702.DBW0"


def test_mysql_overrun_is_timeout_not_snapshot(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setenv("DATAWATCH_MYSQL_HOST", "127.0.0.1")
    monkeypatch.setenv("DATAWATCH_MYSQL_USER", "ro")
    monkeypatch.setenv("DATAWATCH_MYSQL_DATABASE", "device_watch")
    snapshot = tmp_path / "topology.json"
    snapshot.write_text(
        json.dumps({"devices": [_qc100()]}, ensure_ascii=False),
        encoding="utf-8",
    )
    import sys
    import types

    fake = types.ModuleType("pymysql")

    def connect(**_kwargs):
        time.sleep(1.5)
        return object()

    fake.connect = connect
    cursors = types.ModuleType("pymysql.cursors")
    cursors.DictCursor = object
    monkeypatch.setitem(sys.modules, "pymysql", fake)
    monkeypatch.setitem(sys.modules, "pymysql.cursors", cursors)
    started = time.monotonic()
    with pytest.raises(DirectReadError) as captured:
        _read(
            tmp_path,
            topology="auto",
            snapshot_path=str(snapshot),
            timeout_ms=150,
            endpoint="10.6.6.6",
            address="DB1.DBW0",
        )
    elapsed = time.monotonic() - started

    assert captured.value.error_type == "timeout"
    assert elapsed < 0.8


def test_hung_disconnect_stays_inside_total_timeout(tmp_path: Path) -> None:
    source = MemorySource("mysql", [_qc100()])
    created: list[FakeDriver] = []

    class Hung(FakeDriver):
        async def disconnect(self):
            self.events.append("disconnect")
            await asyncio.sleep(5)

    def factory():
        driver = Hung(delay=2)
        created.append(driver)
        return driver

    started = time.monotonic()
    with pytest.raises(DirectReadError) as captured:
        _read(
            tmp_path,
            sources=[source],
            timeout_ms=80,
            driver_factory=factory,
            drivers=created,
        )

    assert captured.value.error_type == "timeout"
    assert time.monotonic() - started < 1.0
    assert created[0].events[-1] == "disconnect"


def test_writable_mysql_account_does_not_fall_back(tmp_path: Path) -> None:
    class WritableMysql:
        name = "mysql"

        def resolve(self, _device: str, _point: str):
            raise DirectReadError(
                "config_error",
                "MySQL 拓扑账号含写权限，直连只接受只读账号",
            )

    snapshot = MemorySource("snapshot", [_qc100()])
    with pytest.raises(DirectReadError) as captured:
        with _override_driver_factory(lambda: (_ for _ in ()).throw(AssertionError("不能连接"))):
            read_point(
                "QC100",
                "CraneID",
                actor="operator",
                sources=[WritableMysql(), snapshot],
                state_dir=tmp_path,
                audit_path=tmp_path / "audit.log",
            )

    assert captured.value.error_type == "config_error"
    assert snapshot.calls == 0


def test_unsupported_protocol_does_not_connect(tmp_path: Path) -> None:
    source = MemorySource("mysql", [_qc100(protocol="ge_srtp")])

    def factory():
        raise AssertionError("非 S7 不能创建驱动")

    with pytest.raises(DirectReadError) as captured:
        with _override_driver_factory(factory):
            read_point(
                "QC100",
                "CraneID",
                actor="operator",
                sources=[source],
                state_dir=tmp_path,
                audit_path=tmp_path / "audit.log",
            )

    assert captured.value.error_type == "unsupported_protocol"
    assert captured.value.details["source"] == DIRECT_SOURCE
