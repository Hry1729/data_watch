"""ConfigDB scan_rate 规范化测试。"""

from unittest.mock import patch

import pytest

from core.config_db import ConfigDBManager, DeviceSignature


@pytest.mark.parametrize(
    ("raw_value", "expected"),
    [
        (100, 0.1),
        (500, 0.5),
        (1000, 1.0),
        (1500, 1.5),
        (1, 1.0),
    ],
)
def test_normalize_scan_rate_keeps_millisecond_precision(raw_value, expected):
    assert ConfigDBManager._normalize_scan_rate(raw_value) == pytest.approx(expected)


def test_normalize_scan_rate_uses_default_for_missing_or_invalid_values():
    assert ConfigDBManager._normalize_scan_rate(None, default_seconds=0.5) == pytest.approx(0.5)
    assert ConfigDBManager._normalize_scan_rate(0, default_seconds=0.5) == pytest.approx(0.5)


def test_normalize_slot_defaults_only_when_none():
    """slot=0 不得被 or 1 吞掉；仅 None 默认 1。"""
    assert ConfigDBManager._normalize_slot(None) == 1
    assert ConfigDBManager._normalize_slot(1) == 1
    assert ConfigDBManager._normalize_slot(2) == 2
    assert ConfigDBManager._normalize_slot(0) == 0
    assert ConfigDBManager._normalize_slot(247) == 247


def test_modbus_tcp_default_port_is_502():
    assert ConfigDBManager._default_port_for_protocol("modbus_tcp") == 502


@pytest.mark.asyncio
async def test_get_device_config_versions_preserves_slot_zero():
    rows = [
        {
            "device_code": "MB001",
            "variable_config_version": 1,
            "endpoint": "10.0.0.3",
            "protocol_id": 5,
            "rack": 0,
            "slot": 0,
            "scan_cycle": 1000,
        },
    ]

    async def _fetchall(_sql, _params=()):
        return rows

    manager = ConfigDBManager.__new__(ConfigDBManager)
    manager._fetchall = _fetchall

    versions = await manager.get_device_config_versions()
    assert versions["MB001"].slot == 0


def test_device_signature_device_fields_ignore_config_version():
    old = DeviceSignature(1, "10.0.0.1", 1, 0, 1, 1000)
    new_version = DeviceSignature(2, "10.0.0.1", 1, 0, 1, 1000)
    new_endpoint = DeviceSignature(2, "10.0.0.2", 1, 0, 1, 1000)

    assert old.device_fields_changed(new_version) is False
    assert old.device_fields_changed(new_endpoint) is True


@pytest.mark.asyncio
async def test_get_device_config_versions_returns_device_signatures():
    rows = [
        {
            "device_code": "PLC001",
            "variable_config_version": 7,
            "endpoint": "10.0.0.1",
            "protocol_id": 3,
            "rack": 0,
            "slot": 2,
            "scan_cycle": 500,
        },
        {
            "device_code": "PLC002",
            "variable_config_version": None,
            "endpoint": None,
            "protocol_id": None,
            "rack": None,
            "slot": None,
            "scan_cycle": None,
        },
    ]

    async def _fetchall(_sql, _params=()):
        return rows

    manager = ConfigDBManager.__new__(ConfigDBManager)
    manager._fetchall = _fetchall

    versions = await manager.get_device_config_versions()

    assert versions["PLC001"] == DeviceSignature(7, "10.0.0.1", 3, 0, 2, 500)
    assert versions["PLC002"] == DeviceSignature(1, "", 0, 0, 1, 1000)


# ---------------------------------------------------------------------------
# C5: is_storage 变量缺 td_column_name 时，加载源头打告警（不阻断采集）
# ---------------------------------------------------------------------------


def _make_manager_with_rows(variable_rows):
    device_rows = [
        {
            "id": 1,
            "device_code": "AGV801",
            "endpoint": "127.0.0.1:8090",
            "rack": 0,
            "slot": 1,
            "scan_cycle": 1000,
            "protocol_id": 1,
            "type_id": 1,
            "variable_config_version": 1,
            "protocol": "opcda_sidecar",
            "port": 8090,
            "server": None,
            "type_code": "AGV",
        }
    ]
    results = [device_rows, variable_rows]

    class _Conn:
        async def begin(self):
            return None

        async def commit(self):
            return None

        async def rollback(self):
            return None

    class _Acquire:
        async def __aenter__(self):
            return _Conn()

        async def __aexit__(self, exc_type, exc, tb):
            return None

    class _Pool:
        def acquire(self):
            return _Acquire()

    manager = ConfigDBManager.__new__(ConfigDBManager)
    manager._calls = 0

    async def _ensure_pool():
        return _Pool()

    async def _fetchall_on_conn(_conn, _sql, _params=()):
        rows = results[manager._calls]
        manager._calls += 1
        return rows

    manager._ensure_pool = _ensure_pool
    manager._fetchall_on_conn = _fetchall_on_conn
    return manager


def _variable_row(var_id, td_column_name, is_storage=1):
    return {
        "id": var_id,
        "device_id": 1,
        "var_seq": var_id,
        "var_type": "REAL",
        "var_address": f"OPD_Server.AGV801.ViewMain.Tag{var_id}",
        "scan_rate": 100,
        "is_storage": is_storage,
        "td_column_name": td_column_name,
        "group_code": "g001",
        "config_version": 1,
    }


@pytest.mark.asyncio
async def test_load_warns_on_storage_variable_missing_td_column():
    """is_storage=1 但缺 td_column_name → 打告警，但仍加载（不阻断采集）。"""
    manager = _make_manager_with_rows([
        _variable_row(10, "col_a"),   # 有列映射
        _variable_row(11, None),      # is_storage 但缺列映射
        _variable_row(12, ""),        # 空字符串也算缺
    ])

    with patch("core.config_db.logger") as mock_logger:
        devices, variables = await manager.load_devices_and_variables()

    assert len(variables) == 3  # 不阻断：三条都加载
    warn_msgs = [call.args[0] for call in mock_logger.warning.call_args_list]
    assert any("missing td_column_name" in msg for msg in warn_msgs)
    # 告警计数应为 2（var 11、var 12），不含有列映射的 var 10
    missing_call = next(
        call for call in mock_logger.warning.call_args_list
        if "missing td_column_name" in call.args[0]
    )
    assert missing_call.args[1] == 2


@pytest.mark.asyncio
async def test_load_no_warn_when_td_column_present():
    """全部 is_storage 变量都有 td_column_name → 不打缺列告警。"""
    manager = _make_manager_with_rows([
        _variable_row(10, "col_a"),
        _variable_row(11, "col_b"),
        _variable_row(12, None, is_storage=0),  # 非存储点缺列不告警
    ])

    with patch("core.config_db.logger") as mock_logger:
        devices, variables = await manager.load_devices_and_variables()

    assert len(variables) == 3
    warn_msgs = [call.args[0] for call in mock_logger.warning.call_args_list]
    assert not any("missing td_column_name" in msg for msg in warn_msgs)
