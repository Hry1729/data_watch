"""Golden cases captured before P2: area bounds, gaps, payload caps and decoding."""
import importlib
import json
from pathlib import Path
import pytest
from device.device import Variable

CASES = json.loads((Path(__file__).parent / "fixtures/protocol_migration.json").read_text(encoding="utf-8"))

@pytest.mark.parametrize("case", CASES)
def test_package_matches_pre_migration_plan_and_values(case):
    planner = importlib.import_module(f"packages.{case['protocol']}.{case['protocol']}.planner")
    variables = [Variable(str(i), address, kind) for i, (address, kind) in enumerate(case["variables"])]
    blocks, mapping = planner.build_read_plan(variables, connection_config=case["config"])
    assert [[block.address, block.count, block.data_type] for block in blocks] == case["blocks"]
    assert mapping == case["mapping"]
    assert planner.decode_read_results(case["results"], mapping) == case["decoded"]
