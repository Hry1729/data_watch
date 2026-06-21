from __future__ import annotations

from dataclasses import FrozenInstanceError

import pytest

from collector.task import CollectTask
from device.device import Device, Variable


def test_collect_task_freezes_scan_metadata() -> None:
    device = Device("qc100", "qc", "s7", {})
    variable = Variable(
        variable_id="1",
        address="DB1.DBD0",
        data_type="REAL",
        scan_rate=0.1,
        group_code="g001",
        group_config_version=3,
    )
    device.add_variable(variable)
    device.build_scan_tasks()
    source = device.scan_tasks[0]
    block_address = source.read_blocks[0].address
    source.result_map = {
        variable.variable_id: {
            "block_address": block_address,
            "word_offset": 0,
        }
    }

    task = CollectTask(device=device, scan_task=source)

    variable.address = "DB9.DBD0"
    variable.group_code = "changed"
    source.read_blocks[0].address = "DB9.DBD0"
    source.result_map[variable.variable_id]["block_address"] = "DB9.DBD0"
    source.variables.clear()
    source.read_blocks.clear()

    assert len(task.scan_task.variables) == 1
    assert task.scan_task.variables[0].address == "DB1.DBD0"
    assert task.scan_task.variables[0].group_code == "g001"
    assert len(task.scan_task.read_blocks) == 1
    assert task.scan_task.read_blocks[0].address == block_address
    assert task.scan_task.result_map["1"]["block_address"] == block_address

    with pytest.raises(FrozenInstanceError):
        task.scan_task.variables[0].address = "DB2.DBD0"
    with pytest.raises(TypeError):
        task.scan_task.result_map["1"]["block_address"] = "DB2.DBD0"
