"""六条边界里不依赖真 PLC 的护栏。"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from direct_read import audit as audit_module
from direct_read import driver_loader, runtime
from direct_read.api import read_point
from direct_read.errors import DirectReadError
from direct_read.limits import RateLimiter
from direct_read.quality import (
    QUALITY_BAD,
    QUALITY_CONFIG_DECODE_ERROR,
    QUALITY_GOOD,
    QUALITY_NOT_CONNECTED,
    QUALITY_STALE,
    QUALITY_UNCERTAIN,
)
from direct_read.topology import (
    DEVICE_SQL,
    POINT_SQL,
    MysqlSettings,
    ensure_read_only_account,
    mysql_settings_from_env,
    normalize_device_row,
    query_topology,
)


def test_quality_values_match_collector_module() -> None:
    import importlib.util

    path = Path(__file__).resolve().parents[2] / "collector" / "core" / "quality_codes.py"
    spec = importlib.util.spec_from_file_location("collector_quality_codes_for_test", path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    assert QUALITY_GOOD == module.QUALITY_GOOD == 0
    assert QUALITY_UNCERTAIN == module.QUALITY_UNCERTAIN == 2
    assert QUALITY_BAD == module.QUALITY_BAD == 5
    assert QUALITY_STALE == module.QUALITY_STALE == 6
    assert QUALITY_NOT_CONNECTED == module.QUALITY_NOT_CONNECTED == 7
    assert QUALITY_CONFIG_DECODE_ERROR == module.QUALITY_CONFIG_DECODE_ERROR == 8


def test_linux_is_outside_mvp(monkeypatch) -> None:
    monkeypatch.setattr(runtime.sys, "platform", "linux")
    assert runtime.mvp_runtime_supported() is False


def test_windows_314_without_free_threading_is_outside_mvp(monkeypatch) -> None:
    monkeypatch.setattr(runtime.sys, "platform", "win32")
    monkeypatch.setattr(runtime.sys, "version_info", (3, 14, 0, "final", 0))
    monkeypatch.setattr(runtime.sys, "abiflags", "", raising=False)
    monkeypatch.setattr(runtime.sysconfig, "get_config_var", lambda _name: 0)
    assert runtime.mvp_runtime_supported() is False


def test_windows_314t_is_mvp(monkeypatch) -> None:
    monkeypatch.setattr(runtime.sys, "platform", "win32")
    monkeypatch.setattr(runtime.sys, "version_info", (3, 14, 0, "final", 0))
    monkeypatch.setattr(runtime.sys, "abiflags", "t", raising=False)
    assert runtime.mvp_runtime_supported() is True


def test_unsupported_runtime_does_not_load_registry(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setattr(runtime, "mvp_runtime_supported", lambda: False)
    called = []
    monkeypatch.setattr(driver_loader, "load_registry", lambda: called.append("load"))

    class Source:
        name = "explicit"

        def resolve(self, device: str, point: str):
            from direct_read.topology import ExplicitTopology

            return ExplicitTopology(
                endpoint="10.1.2.3",
                address="DB1.DBW0",
                protocol="s7",
                rack=0,
                slot=1,
                data_type="INT",
                port=None,
                required=True,
            ).resolve(device, point)

    with pytest.raises(DirectReadError) as captured:
        read_point(
            "QC100",
            "CraneID",
            actor="operator",
            sources=[Source()],
            state_dir=tmp_path,
            audit_path=tmp_path / "audit.log",
        )

    assert captured.value.error_type == "unsupported_runtime"
    assert called == []
    assert captured.value.details["source"] == "direct_plc"


def test_sql_constants_are_select_only() -> None:
    import re

    for statement in (DEVICE_SQL, POINT_SQL):
        upper = statement.upper()
        assert upper.startswith("SELECT")
        for banned in ("INSERT", "UPDATE", "DELETE", "REPLACE", "TRUNCATE", "DROP", "ALTER", "CREATE"):
            assert re.search(rf"\b{banned}\b", upper) is None


def test_query_uses_read_only_session_and_parameterized_points() -> None:
    devices = [
        {
            "id": 7,
            "device_code": "QC100",
            "device_name": "桥吊100",
            "endpoint": "10.1.2.3:1102",
            "rack": None,
            "slot": None,
            "protocol": "S7",
            "port": 102,
        },
        {
            "id": 8,
            "device_code": "QC200",
            "device_name": "桥吊200",
            "endpoint": "10.9.9.9",
            "rack": 0,
            "slot": 1,
            "protocol": "S7",
            "port": 102,
        },
    ]
    cursor = _Cursor(
        grants=[
            "GRANT USAGE ON *.* TO `ro`@`%`",
            "GRANT SELECT ON `device_watch`.* TO `ro`@`%`",
        ],
        devices=devices,
        points=[
            {
                "id": 9,
                "var_seq": 12,
                "var_name": "CraneID",
                "var_type": "int",
                "var_address": "DB1702.DBW0",
            }
        ],
    )

    target = query_topology(cursor, "QC100", "CraneID")

    assert [item[0].split()[0].upper() for item in cursor.statements[:2]] == ["SET", "SHOW"]
    assert all(
        statement[0].lstrip().upper().startswith(("SET SESSION", "SHOW ", "SELECT"))
        for statement in cursor.statements
    )
    point_sql = [item for item in cursor.statements if "device_variable" in item[0]]
    assert point_sql == [(POINT_SQL, (7,))]
    assert target.protocol == "s7"
    assert target.host == "10.1.2.3"
    assert target.port == 1102
    assert target.rack == 0
    assert target.slot == 1
    assert target.point_name == "CraneID"
    assert target.data_type == "INT"
    assert target.io_address == "DB1702.DBW0"


def test_writable_grant_stops_before_device_query() -> None:
    cursor = _Cursor(
        grants=["GRANT ALL PRIVILEGES ON *.* TO `root`@`localhost`"],
        devices=[],
        points=[],
    )
    with pytest.raises(DirectReadError) as captured:
        query_topology(cursor, "QC100", "CraneID")
    assert captured.value.error_type == "config_error"
    assert not any("FROM device AS" in statement[0] for statement in cursor.statements)


@pytest.mark.parametrize(
    "grant",
    [
        "GRANT SELECT, INSERT ON `device_watch`.* TO `rw`@`%`",
        "GRANT ALL ON `device_watch`.* TO `rw`@`%`",
        "not a grant",
    ],
)
def test_non_read_only_grants_are_rejected(grant: str) -> None:
    with pytest.raises(DirectReadError) as captured:
        ensure_read_only_account([grant])
    assert captured.value.error_type == "config_error"


def test_mysql_settings_do_not_default_to_root_and_hide_password() -> None:
    assert mysql_settings_from_env({}) is None
    settings = mysql_settings_from_env(
        {
            "DATAWATCH_MYSQL_HOST": "10.0.0.5",
            "DATAWATCH_MYSQL_USER": "ro",
            "DATAWATCH_MYSQL_DATABASE": "device_watch",
            "DATAWATCH_MYSQL_PASSWORD": "super-secret-db-password",
        }
    )
    assert isinstance(settings, MysqlSettings)
    assert settings.user == "ro"
    assert "super-secret-db-password" not in repr(settings)


def test_normalize_keeps_explicit_slot_zero() -> None:
    device = normalize_device_row(
        {
            "id": 1,
            "device_code": "QC100",
            "device_name": "桥吊100",
            "endpoint": "10.0.0.8",
            "rack": 0,
            "slot": 0,
            "protocol": "snap7",
            "port": None,
        }
    )
    assert device["slot"] == 0
    assert device["rack"] == 0
    assert device["protocol"] == "s7"
    assert device["port"] == 102


def test_audit_scrubs_password_and_token(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setenv("DATAWATCH_MYSQL_PASSWORD", "super-secret-db-password")
    monkeypatch.setenv("DATAWATCH_TOKEN", "super-secret-token-value")
    path = tmp_path / "audit.log"
    audit_module.record_audit(
        actor="operator",
        device="super-secret-db-password",
        point="super-secret-token-value",
        outcome="ok",
        audit_path=path,
        extra_secrets=["refresh-token-should-not-leak"],
    )
    text = path.read_text(encoding="utf-8")
    assert "super-secret-db-password" not in text
    assert "super-secret-token-value" not in text
    record = json.loads(text)
    assert set(record) <= audit_module.audit_record_keys()
    assert "password" not in record
    assert "token" not in record


def test_limit_floors_cannot_be_weakened(tmp_path: Path) -> None:
    now = {"t": 1_700_000_000.0}
    limiter = RateLimiter(
        tmp_path,
        now=lambda: now["t"],
        min_interval_s=0,
        max_concurrency=9,
    )
    assert limiter.min_interval_s == 0.5
    assert limiter.max_concurrency == 2
    limiter.acquire("QC100").release()
    with pytest.raises(DirectReadError) as captured:
        limiter.acquire("QC100")
    assert captured.value.details["limit"] == "device_interval"


def test_windows_pid_check_uses_open_process_not_kill(monkeypatch) -> None:
    from direct_read import limits

    killed: list[tuple[int, int]] = []
    holder: dict[str, _Kernel] = {}

    def boom(pid: int, sig: int) -> None:
        killed.append((pid, sig))
        raise AssertionError("Windows 不能用 os.kill 探测进程")

    monkeypatch.setattr(limits.sys, "platform", "win32")
    monkeypatch.setattr(limits.os, "kill", boom)
    monkeypatch.setattr(limits, "_kernel32", lambda: holder["kernel"])
    monkeypatch.setattr(limits, "_last_winerror", lambda: holder["kernel"].last_error)

    holder["kernel"] = _Kernel(handle=50, exit_code=259)
    assert limits._pid_alive(4242) is True
    assert holder["kernel"].open_access == 0x1000
    assert holder["kernel"].closed == [50]

    holder["kernel"] = _Kernel(handle=51, exit_code=0)
    assert limits._pid_alive(4242) is False
    assert holder["kernel"].closed == [51]

    holder["kernel"] = _Kernel(handle=0, last_error=5)
    assert limits._pid_alive(4242) is True

    holder["kernel"] = _Kernel(handle=0, last_error=87)
    assert limits._pid_alive(4242) is False

    assert limits._pid_alive(0) is False
    assert killed == []


def test_windows_stale_slot_is_purged_without_os_kill(monkeypatch, tmp_path: Path) -> None:
    from direct_read import limits

    killed: list[tuple[int, int]] = []

    def boom(pid: int, sig: int) -> None:
        killed.append((pid, sig))
        raise AssertionError("Windows 不能用 os.kill 探测进程")

    monkeypatch.setattr(limits.sys, "platform", "win32")
    monkeypatch.setattr(limits.os, "kill", boom)
    monkeypatch.setattr(limits, "_lock", lambda _handle: None)
    monkeypatch.setattr(limits, "_unlock", lambda _handle: None)
    kernel = _Kernel(handle=7, exit_code=0)
    monkeypatch.setattr(limits, "_kernel32", lambda: kernel)
    monkeypatch.setattr(limits, "_last_winerror", lambda: kernel.last_error)
    limiter = RateLimiter(tmp_path, now=lambda: 2_000.0)
    limiter.slot_dir.mkdir(parents=True, exist_ok=True)
    stale = limiter.slot_dir / "old.slot"
    stale.write_text(
        json.dumps({"pid": 99999, "started": 2_000.0}),
        encoding="utf-8",
    )

    permit = limiter.acquire("10.0.0.8:102")
    permit.release()

    assert killed == []
    assert kernel.open_access == 0x1000
    assert not stale.exists()


def test_posix_liveness_uses_os_kill(monkeypatch) -> None:
    from direct_read import limits

    calls: list[tuple[int, int]] = []
    opened: list[int] = []
    monkeypatch.setattr(limits.sys, "platform", "linux")
    monkeypatch.setattr(limits.os, "kill", lambda pid, sig: calls.append((pid, sig)))
    monkeypatch.setattr(limits, "_kernel32", lambda: opened.append(1))

    assert limits._pid_alive(5) is True
    assert calls == [(5, 0)]
    assert opened == []


def test_concurrency_limit(tmp_path: Path) -> None:
    clock = lambda: 1_700_000_000.0  # noqa: E731
    limiter = RateLimiter(tmp_path, now=clock, max_concurrency=1)
    first = limiter.acquire("QC100")
    with pytest.raises(DirectReadError) as captured:
        limiter.acquire("QC200")
    assert captured.value.error_type == "rate_limited"
    assert captured.value.details["limit"] == "concurrency"
    assert captured.value.details["maxConcurrency"] == 1
    first.release()
    limiter.acquire("QC200").release()


def test_package_does_not_import_platform_stores() -> None:
    root = Path(__file__).resolve().parents[1]
    for path in root.glob("*.py"):
        for line in path.read_text(encoding="utf-8").splitlines():
            stripped = line.strip()
            if not stripped.startswith(("import ", "from ")):
                continue
            lowered = stripped.lower()
            assert "redis" not in lowered
            assert "tdengine" not in lowered
            assert "taos" not in lowered


def test_session_never_calls_driver_write() -> None:
    source = (Path(__file__).resolve().parents[1] / "session.py").read_text(encoding="utf-8")
    assert ".write(" not in source
    assert "driver.write" not in source


class _Kernel:
    def __init__(self, *, handle: int, exit_code: int = 259, last_error: int = 0) -> None:
        self.handle = handle
        self.exit_code = exit_code
        self.last_error = last_error
        self.closed: list[int] = []
        self.open_access: int | None = None

    def OpenProcess(self, access: int, inherit: bool, pid: int) -> int:
        self.open_access = access
        return self.handle

    def GetExitCodeProcess(self, handle: int, out) -> int:
        out._obj.value = self.exit_code
        return 1

    def CloseHandle(self, handle: int) -> int:
        self.closed.append(handle)
        return 1


class _Cursor:
    def __init__(self, *, grants, devices, points) -> None:
        self.grants = grants
        self.devices = devices
        self.points = points
        self.statements: list[tuple[str, object]] = []
        self._last = ""

    def execute(self, sql, args=None) -> None:
        self.statements.append((sql, args))
        self._last = sql

    def fetchall(self):
        if self._last.startswith("SHOW"):
            return [{"Grant": grant} for grant in self.grants]
        if "FROM device AS" in self._last:
            return self.devices
        if "FROM device_variable" in self._last:
            return self.points
        raise AssertionError(self._last)
