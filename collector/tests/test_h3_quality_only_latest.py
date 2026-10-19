"""高-3 回归：value=None + 坏质量的样本必须把质量带到 latest（中心可见），且不伪造值。

修复前 ``_parse_result`` 对 value=None 直接 ``continue``（非映射路径连告警都没有），
OPC DA Bad 质量（VT_EMPTY）等场景下 latest 停在上一次的 Good 值、q 仍为 0。

现在：
- value=None + GOOD ⇒ 本轮无样本，仍跳过；
- value=None + 坏质量 ⇒ 发出 value=None 的点；``_write_to_redis`` 写一条只有 t/q、
  没有 "v" 的 latest 字段（不伪造 0 / ""），不进 stream，也不进 window 的 values；
- 边缘 outbox 按 '"v" in field' 把它排除出 latest_values，latest_qualities /
  latest_timestamps 照常带上（不会出现 "None" 字符串）。
"""

from __future__ import annotations

from datetime import datetime
from types import SimpleNamespace
from unittest.mock import MagicMock

import msgspec

from collector.executor import TaskExecutor
from core.quality_codes import QUALITY_BAD, QUALITY_GOOD
from data_queue.types import DataPoint
from drivers.base import DriverSample
from storage.edge_outbox_writer import EdgeOutboxWriter
from edge.app.outbox import OutboxStore

NOW = datetime(2026, 10, 4, 0, 0, 0)
NOW_MS = int(NOW.timestamp() * 1000)


def _variable(variable_id, address, data_type="REAL", is_storage=True):
    return SimpleNamespace(
        variable_id=variable_id,
        address=address,
        data_type=data_type,
        is_storage=is_storage,
        group_code="g001",
        group_config_version=3,
    )


def _executor(writer=None) -> TaskExecutor:
    return TaskExecutor(redis_writer=writer or MagicMock())


def test_unmapped_bad_quality_none_emits_quality_point():
    scan_task = SimpleNamespace(
        result_map={},
        variables=[_variable("bad", "A1"), _variable("nosample", "A2"), _variable("good", "A3")],
    )
    result = {
        "A1": DriverSample(None, quality=QUALITY_BAD),
        "A2": DriverSample(None),
        "A3": DriverSample(1.5),
    }
    points = _executor()._parse_result("DEV1", scan_task, result, native_quality=True)

    by_id = {p.variable_id: p for p in points}
    assert set(by_id) == {"bad", "good"}, "GOOD 的 None 仍是本轮无样本"
    assert by_id["bad"].value is None
    assert by_id["bad"].quality == QUALITY_BAD


def test_quality_only_latest_field_has_no_fabricated_value():
    writer = MagicMock()
    executor = _executor(writer)
    device = SimpleNamespace(device_id="DEV1", device_type="QC")
    scan_task = SimpleNamespace(
        scan_rate=0.1,
        variables=[_variable("bad", "A1"), _variable("good", "A2")],
    )
    executor._write_to_redis(device, scan_task, [
        DataPoint("DEV1", "bad", None, timestamp=NOW, quality=QUALITY_BAD),
        DataPoint("DEV1", "good", 0, timestamp=NOW, quality=QUALITY_GOOD),
    ])

    args = writer.write_window_stream.call_args.args
    frame, stream_values, stream_qualities = args[6], args[7], args[8]
    assert frame["values"] == {"good": "0"}
    assert stream_values == {"good": "0"}
    assert stream_qualities == {"good": QUALITY_GOOD}

    fields = writer.write_latest.call_args.args[2]
    assert fields["bad"] == {"t": NOW_MS, "q": QUALITY_BAD}, "不得伪造 0 / \"\""
    assert fields["good"] == {"v": "0", "t": NOW_MS, "q": QUALITY_GOOD}


def test_edge_outbox_uploads_quality_without_fake_value(tmp_path):
    store = OutboxStore(tmp_path / "edge.db")
    store.configure_assignment("DEV1", 5, 100, 1)
    executor = _executor(EdgeOutboxWriter(store))
    device = SimpleNamespace(device_id="DEV1", device_type="QC")
    scan_task = SimpleNamespace(
        scan_rate=0.1,
        variables=[_variable("bad", "A1"), _variable("good", "A2")],
    )
    try:
        executor._write_to_redis(device, scan_task, [
            DataPoint("DEV1", "bad", None, timestamp=NOW, quality=QUALITY_BAD),
            DataPoint("DEV1", "good", 2.5, timestamp=NOW, quality=QUALITY_GOOD),
        ])
        row = store._connection.execute("SELECT payload FROM outbox").fetchone()
        payload = msgspec.json.decode(bytes(row["payload"]))
        local = store.read_local_latest("DEV1", "g001")
    finally:
        store.close()

    assert payload["values"] == {"good": "2.5"}
    assert payload["latest_values"] == {"good": "2.5"}
    assert payload["latest_qualities"] == {"bad": QUALITY_BAD, "good": QUALITY_GOOD}
    assert payload["latest_timestamps"]["bad"] == NOW_MS
    assert local["bad"] == {"t": NOW_MS, "q": QUALITY_BAD}
