"""Offline checks for interrupted long runs and bounded stream auditing."""
from __future__ import annotations

import ast
import json
import os
from pathlib import Path
from types import SimpleNamespace

import pytest

from data_watch_shared import acceptance as acc
from data_watch_shared.stream_envelope import stream_key
from scripts import validate_collector_ingest_capacity as collector_script


class PagedRedis:
    def __init__(self, entries):
        self.entries = entries
        self.calls = []

    def xrange(self, key, *, min, max, count):
        self.calls.append((key, min, count))
        rows = self.entries.get(key, [])
        if min.startswith("("):
            rows = [row for row in rows if row[0] > min[1:]]
        return rows[:count]


def _stream(devices, ticks, *, points=8, base_ts=1000, period_ms=100):
    entries = {}
    for device in devices:
        key = stream_key(device, "test", "g", 4)
        for tick in range(ticks):
            frame = acc.build_frame(device, points, tick, period_ms, base_ts)
            fields = {
                "device_code": device, "group_code": "g", "ts": str(frame["ts"]),
                "scan_rate": str(period_ms), "contract_version": "2",
                "values": json.dumps(frame["stream_values"]),
                "qualities": json.dumps(frame["stream_qualities"]),
            }
            entry_id = f"{base_ts + tick * period_ms}-{len(entries.get(key, []))}"
            entries.setdefault(key, []).append((entry_id, fields))
    for rows in entries.values():
        rows.sort(key=lambda row: row[0])
    return entries


def test_stream_audit_pages_and_checks_last_full_frame():
    devices = ["d1", "d2"]
    redis = PagedRedis(_stream(devices, 510))
    result = collector_script.audit_stream_frames(
        redis, sorted(redis.entries), devices, "test", "g", 8, 510, 100, 1000, 97)
    assert result["total"] == 1020
    assert result["missing"] == result["duplicates"] == result["invalid"] == 0
    assert result["problems"] == []
    assert result["audited"] >= 2
    assert len(redis.calls) >= 4 and all(count == 500 for _, _, count in redis.calls)


def test_stream_audit_detects_missing_plus_duplicate_with_same_total():
    devices = ["d1", "d2"]
    entries = _stream(devices, 3)
    key = stream_key("d1", "test", "g", 4)
    rows = entries[key]
    removed = next(index for index, (_, fields) in enumerate(rows)
                   if fields["device_code"] == "d1" and fields["ts"] == "1100")
    rows.pop(removed)
    rows.append(("9999-1", dict(rows[0][1])))
    rows.sort(key=lambda row: row[0])
    result = collector_script.audit_stream_frames(
        PagedRedis(entries), sorted(entries), devices, "test", "g", 8, 3, 100, 1000, 2)
    assert result["total"] == 6
    assert result["missing"] == 1 and result["duplicates"] == 1
    assert result["problems"]


def _pipeline_helpers():
    # The full pipeline module needs MySQL/TDengine drivers. Extract only its pure
    # checks so this regression remains runnable on a host without those services.
    path = Path(__file__).with_name("validate_docker_pipeline.py")
    tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
    names = {"append_progress", "validate_full_field_load", "steady_state_verdict"}
    functions = [node for node in tree.body
                 if isinstance(node, ast.FunctionDef) and node.name in names]
    assert {node.name for node in functions} == names
    namespace = {"Path": Path, "json": json, "os": os,
                 "MAX_FULL_FIELD_CELLS": 2_000_000,
                 "not_tested": acc.not_tested, "PASS": acc.PASS}
    exec(compile(ast.Module(body=functions, type_ignores=[]), str(path), "exec"), namespace)
    return namespace


def test_periodic_progress_survives_without_final_report(tmp_path):
    path = tmp_path / "result.json.progress.jsonl"
    for append in (collector_script.append_progress, _pipeline_helpers()["append_progress"]):
        append(path, {"status": "started", "token": "run-1"})
        append(path, {"status": "in_progress", "sample": {"accepted": 10}})
    events = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]
    assert len(events) == 4
    assert events[-1]["sample"]["accepted"] == 10
    assert not (tmp_path / "result.json").exists()


def test_pipeline_one_steady_window_is_not_tested():
    verdict = _pipeline_helpers()["steady_state_verdict"]({
        "trend": "insufficient_windows", "steady_windows": 1})
    assert verdict["status"] == acc.NOT_TESTED


def test_pipeline_full_field_preflight_rejects_500_point_long_run():
    check = _pipeline_helpers()["validate_full_field_load"]
    check(SimpleNamespace(devices=5, points=100, seconds=60, period_ms=100))
    with pytest.raises(ValueError, match="全字段内存对账"):
        check(SimpleNamespace(devices=20, points=500, seconds=1800, period_ms=100))
