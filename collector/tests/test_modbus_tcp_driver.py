"""Modbus TCP 外置驱动测试（mock client，不依赖真机）"""

import sys
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

# 驱动包不在 collector 默认 PYTHONPATH，测试时加入
_MODBUS_PKG_ROOT = Path(__file__).resolve().parents[2] / "packages" / "modbus_tcp"
if str(_MODBUS_PKG_ROOT) not in sys.path:
    sys.path.insert(0, str(_MODBUS_PKG_ROOT))

from modbus_tcp.address import AddressParseError, parse_address  # noqa: E402
from modbus_tcp.driver import ModbusTcpDriver, create_driver  # noqa: E402
from modbus_tcp.planner import (  # noqa: E402
    build_read_plan,
    decode_read_results,
    plan_read_blocks,
)
from core.exceptions import ReadError  # noqa: E402
from drivers.base import ReadBlock  # noqa: E402
from drivers.context import DriverContext  # noqa: E402

_UNSET = object()


def _make_driver():
    ctx = DriverContext(
        protocol="modbus_tcp",
        driver_id="modbus_tcp",
        driver_version="1.0.0",
        driver_api="1.0",
        logger=None,
    )
    return create_driver(ctx)


def test_invalid_address_raises_address_parse_error():
    with pytest.raises(AddressParseError, match=r"C\{N\} / HR\{N\}"):
        parse_address("HR-1")


class FakeModbusResponse:
    def __init__(
        self,
        bits=_UNSET,
        registers=_UNSET,
        is_error=False,
        *,
        exception_code=None,
        function_code=None,
        device_id=None,
        transaction_id=None,
    ):
        self.bits = [] if bits is _UNSET else bits
        self.registers = [] if registers is _UNSET else registers
        self._is_error = is_error
        self.exception_code = exception_code
        self.function_code = function_code
        self.device_id = device_id
        self.transaction_id = transaction_id

    def isError(self):
        return self._is_error


@pytest.mark.parametrize(
    "address,expected",
    [
        ("coil:0", ("coil", 0)),
        ("holding:100", ("holding", 100)),
        ("C0", ("coil", 0)),
        ("HR10", ("holding", 10)),
    ],
)
def test_parse_address(address, expected):
    assert parse_address(address) == expected


def test_plan_merges_contiguous_same_fc_and_unit():
    blocks = [
        ReadBlock("coil:0", count=2),
        ReadBlock("coil:2", count=1),
        ReadBlock("holding:10", count=2),
    ]
    plan = plan_read_blocks(blocks, unit_id=1)
    assert len(plan) == 2
    assert plan[0]["fc"] == 1
    assert plan[0]["start"] == 0
    assert plan[0]["count"] == 3
    assert plan[1]["fc"] == 3
    assert plan[1]["start"] == 10
    assert plan[1]["count"] == 2


def test_build_read_plan_decodes_short_addresses_to_variable_ids():
    """C0/HR10 经计划、驱动读取和解码后必须回到唯一变量标识。"""
    variables = [
        {"variable_id": "v_coil", "address": "C0", "data_type": "BOOL"},
        {"variable_id": "v_coil_2", "address": "C1", "data_type": "BOOL"},
        {"variable_id": "v_hr", "address": "HR10", "data_type": "WORD"},
    ]

    blocks, result_map = build_read_plan(variables, unit_id=1)

    assert [(block.address, block.start, block.count, block.data_type) for block in blocks] == [
        ("C0", 0, 2, "BOOL"),
        ("HR10", 10, 1, "WORD"),
    ]
    decoded = decode_read_results({"C0": [True, False], "HR10": [42]}, result_map)
    assert decoded == {"v_coil": True, "v_coil_2": False, "v_hr": 42}


@pytest.mark.parametrize(
    "address,data_type",
    [
        ("C0", "WORD"),
        ("HR10", "BOOL"),
    ],
)
def test_build_read_plan_rejects_invalid_area_data_type(address, data_type):
    with pytest.raises(ValueError, match="只支持"):
        build_read_plan(
            [{"variable_id": "bad", "address": address, "data_type": data_type}]
        )


@pytest.mark.parametrize("count", [0, -1, "bad", True])
def test_build_read_plan_rejects_invalid_count(count):
    with pytest.raises(ValueError, match="count"):
        build_read_plan(
            [
                {
                    "variable_id": "bad_count",
                    "address": "C0",
                    "data_type": "BOOL",
                    "count": count,
                }
            ]
        )


@pytest.mark.parametrize(
    ("address", "count"),
    [
        ("HR65536", 1),
        ("holding:65536", 1),
        ("HR65535", 2),
        ("C65535", 2),
    ],
)
def test_build_read_plan_rejects_address_overflow(address, count):
    data_type = "BOOL" if address.upper().startswith("C") else "WORD"
    with pytest.raises(ValueError, match="越界"):
        build_read_plan(
            [
                {
                    "variable_id": "overflow",
                    "address": address,
                    "data_type": data_type,
                    "count": count,
                }
            ]
        )


@pytest.mark.parametrize(
    "address,data_type,count,limit",
    [
        ("C0", "BOOL", 2000, 2000),
        ("C0", "BOOL", 2001, 2000),
        ("HR0", "WORD", 125, 125),
        ("HR0", "WORD", 126, 125),
    ],
)
def test_build_read_plan_splits_at_modbus_request_limits(
    address, data_type, count, limit
):
    blocks, result_map = build_read_plan(
        [{"variable_id": "wide", "address": address, "data_type": data_type, "count": count}]
    )

    assert all(block.count <= limit for block in blocks)
    assert sum(block.count for block in blocks) == count
    decoded = decode_read_results(
        {block.address: list(range(block.start, block.start + block.count)) for block in blocks},
        result_map,
    )
    assert decoded["wide"] == list(range(count))


@pytest.mark.asyncio
async def test_read_coils_and_holding_registers():
    driver = _make_driver()
    mock_client = MagicMock()
    mock_client.connected = True
    mock_client.read_coils = AsyncMock(
        return_value=FakeModbusResponse(bits=[True, False, True])
    )
    mock_client.read_holding_registers = AsyncMock(
        return_value=FakeModbusResponse(registers=[100, 200])
    )
    driver._client = mock_client
    driver._connected = True
    driver._unit_id = 1

    blocks = [
        ReadBlock("coil:0", count=3, data_type="BOOL"),
        ReadBlock("holding:10", count=2, data_type="WORD"),
    ]
    result = await driver.read(blocks)

    # 结果键保持原始 ReadBlock.address；count>1 返回列表
    assert result["coil:0"] == [True, False, True]
    assert result["holding:10"] == [100, 200]
    assert "coil:1" not in result
    assert "holding:11" not in result
    mock_client.read_coils.assert_awaited_once_with(0, count=3, device_id=1)
    mock_client.read_holding_registers.assert_awaited_once_with(10, count=2, device_id=1)


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("fc", "block", "client_method", "function_code"),
    [
        (1, ReadBlock("C10", count=2, data_type="BOOL"), "read_coils", 0x81),
        (3, ReadBlock("HR10", count=2, data_type="WORD"), "read_holding_registers", 0x83),
    ],
)
async def test_read_error_response_includes_safe_protocol_diagnostics(
    fc, block, client_method, function_code
):
    driver = _make_driver()
    mock_client = MagicMock()
    mock_client.connected = True
    setattr(
        mock_client,
        client_method,
        AsyncMock(
            return_value=FakeModbusResponse(
                is_error=True,
                exception_code=2,
                function_code=function_code,
                device_id=1,
                transaction_id=7,
            )
        ),
    )
    driver._client = mock_client
    driver._connected = True
    driver._unit_id = 1

    with pytest.raises(ReadError) as exc_info:
        await driver.read([block])

    message = str(exc_info.value)
    assert f"FC{fc}" in message
    assert "address=10" in message
    assert "count=2" in message
    assert "exception_code=2" in message
    assert f"function_code={function_code}" in message
    assert "device_id=1" in message
    assert "transaction_id=7" in message


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("fc", "block", "client_method", "payload_name", "payload"),
    [
        (
            1,
            ReadBlock("C10", count=3, data_type="BOOL"),
            "read_coils",
            "bits",
            [True, False],
        ),
        (
            3,
            ReadBlock("HR10", count=3, data_type="WORD"),
            "read_holding_registers",
            "registers",
            [10, 20],
        ),
    ],
)
async def test_read_rejects_short_response_before_indexing(
    fc, block, client_method, payload_name, payload
):
    driver = _make_driver()
    mock_client = MagicMock()
    mock_client.connected = True
    response = FakeModbusResponse()
    setattr(response, payload_name, payload)
    setattr(mock_client, client_method, AsyncMock(return_value=response))
    driver._client = mock_client
    driver._connected = True
    driver._unit_id = 1

    with pytest.raises(ReadError) as exc_info:
        await driver.read([block])

    message = str(exc_info.value)
    assert f"FC{fc}" in message
    assert "start=10" in message
    assert "expected=3" in message
    assert "actual=2" in message


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("fc", "block", "client_method", "payload_name", "payload"),
    [
        (1, ReadBlock("C10", count=1, data_type="BOOL"), "read_coils", "bits", None),
        (1, ReadBlock("C10", count=1, data_type="BOOL"), "read_coils", "bits", 123),
        (
            3,
            ReadBlock("HR10", count=1, data_type="WORD"),
            "read_holding_registers",
            "registers",
            None,
        ),
        (
            3,
            ReadBlock("HR10", count=1, data_type="WORD"),
            "read_holding_registers",
            "registers",
            object(),
        ),
    ],
)
async def test_read_rejects_non_iterable_payload(
    fc, block, client_method, payload_name, payload
):
    driver = _make_driver()
    mock_client = MagicMock()
    mock_client.connected = True
    response = FakeModbusResponse()
    setattr(response, payload_name, payload)
    setattr(mock_client, client_method, AsyncMock(return_value=response))
    driver._client = mock_client
    driver._connected = True
    driver._unit_id = 1

    with pytest.raises(ReadError, match=rf"FC{fc}.*不可迭代.*address=10"):
        await driver.read([block])


@pytest.mark.asyncio
async def test_read_short_address_keys_preserved_for_parse_result():
    """interim：短格式 C0/HR10 结果键与 Variable.address 一致，供 _parse_result 查找。

    Task7 接入 result_map/decoder 后主路径将改走 variable_id；本契约键语义不得再改。
    """
    from collector.executor import TaskExecutor
    from device.device import ScanTask, Variable

    driver = _make_driver()
    mock_client = MagicMock()
    mock_client.connected = True
    mock_client.read_coils = AsyncMock(return_value=FakeModbusResponse(bits=[True]))
    mock_client.read_holding_registers = AsyncMock(
        return_value=FakeModbusResponse(registers=[42])
    )
    driver._client = mock_client
    driver._connected = True
    driver._unit_id = 1

    blocks = [
        ReadBlock("C0", count=1, data_type="BOOL"),
        ReadBlock("HR10", count=1, data_type="WORD"),
    ]
    result = await driver.read(blocks)

    assert result["C0"] is True
    assert result["HR10"] == 42
    assert "coil:0" not in result
    assert "holding:10" not in result

    scan_task = ScanTask(
        scan_rate=1.0,
        variables=[
            Variable(variable_id="v_coil", address="C0", data_type="BOOL"),
            Variable(variable_id="v_hr", address="HR10", data_type="WORD"),
        ],
    )
    points = TaskExecutor._parse_result(
        TaskExecutor.__new__(TaskExecutor),
        "dev1",
        scan_task,
        result,
    )
    by_var = {p.variable_id: p.value for p in points}
    assert by_var["v_coil"] is True
    assert by_var["v_hr"] == 42


@pytest.mark.asyncio
async def test_read_plan_to_decoder_to_datapoint_emits_one_sample_per_variable():
    """贯穿回归：结果映射优先，坏点缺失时不得伪造零值。"""
    from collector.executor import TaskExecutor
    from device.device import ScanTask, Variable

    driver = _make_driver()
    mock_client = MagicMock()
    mock_client.connected = True
    mock_client.read_coils = AsyncMock(return_value=FakeModbusResponse(bits=[True]))
    mock_client.read_holding_registers = AsyncMock(
        return_value=FakeModbusResponse(registers=[42])
    )
    driver._client = mock_client
    driver._connected = True

    variables = [
        Variable(variable_id="v_coil", address="C0", data_type="BOOL"),
        Variable(variable_id="v_hr", address="HR10", data_type="WORD"),
    ]
    blocks, result_map = build_read_plan(variables)
    result = await driver.read(blocks)
    scan_task = ScanTask(
        scan_rate=1.0,
        variables=variables,
        read_blocks=blocks,
        result_map=result_map,
        decode_read_results=decode_read_results,
    )

    points = TaskExecutor._parse_result(
        TaskExecutor.__new__(TaskExecutor), "dev1", scan_task, result
    )

    assert {(point.variable_id, point.value) for point in points} == {
        ("v_coil", True),
        ("v_hr", 42),
    }
    assert len(points) == 2
    bad_points = TaskExecutor._parse_result(
        TaskExecutor.__new__(TaskExecutor), "dev1", scan_task, {"C0": [True]}
    )
    assert [(point.variable_id, point.value) for point in bad_points] == [
        ("v_coil", True)
    ]


def test_parse_result_uses_task_decoder_without_protocol_field():
    """ScanTask 已挂 decoder 时，result_map 无需 protocol 字段也应走 decoder。"""
    from collector.executor import TaskExecutor
    from device.device import ScanTask, Variable

    variables = [
        Variable(variable_id="v_coil", address="C0", data_type="BOOL"),
        Variable(variable_id="v_hr", address="HR10", data_type="WORD"),
    ]
    # 合法 ResultBinding 形态：无 protocol / 无 block_address
    result_map = {
        "v_coil": {"address": "C0", "offset": 0},
        "v_hr": {"address": "HR10", "offset": 0},
    }

    def _decode(result, bindings):
        assert bindings is result_map
        out = {}
        if "C0" in result:
            out["v_coil"] = result["C0"][0] if isinstance(result["C0"], list) else result["C0"]
        if "HR10" in result:
            out["v_hr"] = result["HR10"][0] if isinstance(result["HR10"], list) else result["HR10"]
        return out

    scan_task = ScanTask(
        scan_rate=1.0,
        variables=variables,
        result_map=result_map,
        decode_read_results=_decode,
    )
    points = TaskExecutor._parse_result(
        TaskExecutor.__new__(TaskExecutor),
        "dev1",
        scan_task,
        {"C0": [True], "HR10": [42]},
    )
    assert {(p.variable_id, p.value) for p in points} == {
        ("v_coil", True),
        ("v_hr", 42),
    }


@pytest.mark.asyncio
async def test_connect_failure_closes_and_clears_client():
    driver = _make_driver()
    with patch("modbus_tcp.driver.AsyncModbusTcpClient") as mock_cls:
        mock_client = MagicMock()
        mock_client.connect = AsyncMock(return_value=False)
        mock_client.connected = False
        mock_client.close = MagicMock()
        mock_cls.return_value = mock_client

        with pytest.raises(Exception):
            await driver.connect({"ip": "10.0.0.1", "port": 502})

    mock_client.close.assert_called_once()
    assert driver._client is None
    assert driver._connected is False


@pytest.mark.asyncio
async def test_disconnect_calls_sync_close_without_await():
    driver = _make_driver()
    mock_client = MagicMock()
    mock_client.close = MagicMock(return_value=None)
    driver._client = mock_client
    driver._connected = True

    await driver.disconnect()

    mock_client.close.assert_called_once()
    assert driver._client is None
    assert driver._connected is False


@pytest.mark.asyncio
async def test_disconnect_is_idempotent():
    """重复 disconnect 必须安全，不得因二次清理抛错。"""
    driver = _make_driver()
    mock_client = MagicMock()
    mock_client.close = MagicMock(return_value=None)
    driver._client = mock_client
    driver._connected = True

    await driver.disconnect()
    await driver.disconnect()

    mock_client.close.assert_called_once()
    assert driver._client is None
    assert driver._connected is False


@pytest.mark.asyncio
async def test_ping_is_transport_only_and_never_reads_hr0():
    driver = _make_driver()
    mock_client = MagicMock()
    mock_client.connected = True
    mock_client.read_holding_registers = AsyncMock()
    driver._client = mock_client
    driver._connected = True

    assert await driver.ping() is True
    mock_client.read_holding_registers.assert_not_awaited()


@pytest.mark.asyncio
async def test_connect_rejects_unit_id_out_of_range():
    driver = _make_driver()
    with patch("modbus_tcp.driver.AsyncModbusTcpClient") as mock_cls:
        with pytest.raises(Exception, match="1-247"):
            await driver.connect({"ip": "10.0.0.1", "port": 502, "unit_id": 0})
    mock_cls.assert_not_called()
    assert driver._client is None
    assert driver._connected is False


@pytest.mark.asyncio
async def test_connect_uses_async_modbus_client():
    driver = _make_driver()
    with patch("modbus_tcp.driver.AsyncModbusTcpClient") as mock_cls:
        mock_client = MagicMock()
        mock_client.connect = AsyncMock(return_value=True)
        mock_client.connected = True
        mock_cls.return_value = mock_client

        await driver.connect({"ip": "192.168.1.10", "port": 502, "unit_id": 2})

    mock_cls.assert_called_once_with(host="192.168.1.10", port=502)
    assert driver._connected is True
    assert driver._unit_id == 2
