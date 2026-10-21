"""B075 回归：AGV 配置生成器末尾验证 SQL 不得含未展开的花括号占位符。"""
from __future__ import annotations

import importlib.util
from pathlib import Path

_SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "generate_agv_opcda_config.py"


def _load():
    spec = importlib.util.spec_from_file_location("generate_agv_opcda_config", _SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_verification_sql_uses_numeric_device_id_bounds(monkeypatch, tmp_path):
    gen = _load()
    monkeypatch.setattr(gen, "CSV_DIR", tmp_path)  # 无 CSV → 空点位，只验证 SQL 文本
    sql = gen.generate_sql()
    assert "{DEVICE_ID_START" not in sql
    start = gen.DEVICE_ID_START
    end = start + len(gen.AGV_DEVICES)
    assert f"WHERE device_id >= {start} AND device_id < {end}" in sql
    assert (start, end) == (100000, 100010)
