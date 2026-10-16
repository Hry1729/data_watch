import copy
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from pipeline_diagnostics import (
    bounded_scan, counter_rate, histogram_window, markdown, normalize_timings,
    redis_probe, resource_summary, summarize, summarize_logs, writer_timing_window,
)


def iso(value):
    return datetime.fromtimestamp(value, timezone.utc).isoformat()


def fixture():
    rows = []
    for at, added, read, lag, pending in ((1000, 100, 80, 20, 10), (1060, 220, 140, 80, 10)):
        rows.append({"at": iso(at), "probe": {"at": at, "redis": {
            "available": True, "server": {"run_id": "one"}, "streams_truncated": False,
            "streams": [{"key": "rt:stream:test:g001:0", "entries_added": added,
                         "entries_read": read, "lag": lag, "pending": pending, "group_found": True}],
            "dlq": [{"key": "rt:dlq:test:g001", "count": 1274, "recent": []}],
        }}, "logs": []})
    return {"started_at": iso(1000), "ended_at": iso(1060), "samples": rows}


class PipelineDiagnosticsTests(unittest.TestCase):
    @unittest.skipUnless(os.name == "nt", "Windows PowerShell startup")
    def test_default_output_directory_is_resolved_after_parameter_binding(self):
        source = Path(__file__).with_name("diagnose_pipeline.ps1").read_text(encoding="utf-8-sig")
        startup = source.split("if (-not (Test-Path -LiteralPath $helper))", 1)[0]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            script_dir = root / "目标机 诊断"
            script_dir.mkdir()
            script = script_dir / "diagnose_pipeline.ps1"
            script.write_text(startup + "\n[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)\n"
                              "@{output_root=$OutputRoot; helper=$helper} | ConvertTo-Json\n",
                              encoding="utf-8-sig")
            for extra_args, expected in (([], script_dir / "pipeline-diagnostics"),
                                         (["-OutputRoot", str(root / "自定义 输出")], root / "自定义 输出")):
                result = subprocess.run([shutil.which("powershell.exe"), "-NoProfile", "-ExecutionPolicy", "Bypass",
                                         "-File", str(script), *extra_args], cwd=root,
                                        stdout=subprocess.PIPE, stderr=subprocess.PIPE)
                self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
                # The fixture emits UTF-8 even under legacy console encodings.
                output = json.loads(result.stdout.decode("utf-8", errors="replace"))
                self.assertEqual(Path(output["output_root"]), expected)
                self.assertEqual(Path(output["helper"]), script_dir / "pipeline_diagnostics.py")

    def test_stream_rates_and_backlog_use_frames_and_preserve_old_dlq(self):
        result = summarize(fixture())
        self.assertEqual(result["redis"]["input_frames_per_s"], 2)
        self.assertEqual(result["redis"]["new_read_frames_per_s"], 1)
        self.assertEqual(result["redis"]["backlog_change"], 60)
        self.assertEqual(result["dlq_net_growth"]["delta"], 0)
        self.assertFalse(any("失败隔离队列净增加" in item for item in result["findings"]))
        self.assertIn("未领取 + 未确认积压净变化：60", markdown(result))

    def test_missing_lag_or_group_and_truncated_scan_are_unknown(self):
        for field, value in (("lag", None), ("pending", None)):
            data = fixture()
            data["samples"][-1]["probe"]["redis"]["streams"][0][field] = value
            result = summarize(data)
            self.assertIsNone(result["redis"]["total_lag"])
            self.assertIsNone(result["redis"]["input_frames_per_s"])
        data = fixture()
        data["samples"][-1]["probe"]["redis"]["streams_truncated"] = True
        self.assertIsNone(summarize(data)["redis"]["total_lag"])

    def test_pending_transfer_does_not_look_like_backlog_draining(self):
        data = fixture()
        latest = data["samples"][-1]["probe"]["redis"]["streams"][0]
        latest.update(lag=0, pending=30)
        self.assertEqual(summarize(data)["redis"]["backlog_change"], 0)

    def test_redis_restart_even_with_increasing_counter_invalidates_rate(self):
        data = fixture()
        data["samples"][-1]["probe"]["redis"]["server"]["run_id"] = "two"
        result = summarize(data)
        self.assertIsNone(result["redis"]["input_frames_per_s"])
        self.assertIsNone(result["redis"]["backlog_change"])
        self.assertEqual(result["redis"]["streams"][0]["input_rate"]["resets"], 1)
        self.assertEqual(counter_rate([(0, {"n": 9}), (10, {"n": 2})], "n")["resets"], 1)

    def test_histogram_uses_new_operations_not_lifetime_p95(self):
        result = histogram_window([(1000, {"td": {"count": 10, "average": 100}}),
                                   (1060, {"td": {"count": 12, "average": 90}})], "td")
        self.assertEqual(result, {"count": 2, "total_ms": 80, "average_ms": 40})

    def test_duplicate_and_stale_logs_do_not_count_as_fresh_measurements(self):
        record = {"at": iso(700), "payload": {"timings": {"read_ms": {"count": 10, "average_ms": 20}}}}
        rows = [{"logs": [{"module": "collector", "path": "测试/current.log", "snapshots": [record]}]}] * 3
        result = summarize_logs(rows, 1000, 1060)[0]
        self.assertFalse(result["fresh"])
        self.assertEqual(result["snapshot_count"], 0)
        self.assertEqual(result["gauges"], {})

    def test_shared_writer_loop_is_not_multiplied_by_worker_count(self):
        worker = {"timings": {"event_loop_lag_ms": {"count": 10, "average": 2},
                               "td_execute_ms": {"count": 5, "average": 8}}}
        result = normalize_timings({"workers": [worker, copy.deepcopy(worker)]})
        self.assertEqual(result["event_loop_lag_ms"]["count"], 10)
        self.assertEqual(result["td_execute_ms"]["count"], 10)

    def test_nested_writer_totals_are_not_fake_histograms(self):
        payload = {"timings": {"stages_ms": {"read_claim_ms": 10},
                               "executor_ms": {"ack": {"calls": 2}},
                               "stage_containment": {"loop_iteration_ms": ["read_block_ms"]}}}
        self.assertEqual(normalize_timings(payload), {})

    def test_writer_stage_and_executor_window_subtracts_each_worker(self):
        def payload(loop, calls, call_ms, incomplete):
            worker = {"consumer_name": "one", "timings": {
                "stages_ms": {"loop_iteration_ms": loop, "read_block_ms": loop / 2},
                "stage_containment": {"loop_iteration_ms": ["read_block_ms"]},
                "executor_ms": {"ack": {"calls": calls, "call_ms": call_ms,
                                          "incomplete_calls": incomplete}}}}
            other = copy.deepcopy(worker)
            other["consumer_name"] = "two"
            return {"workers": [worker, other]}
        result = writer_timing_window([(1000, payload(100, 5, 50, 0)),
                                       (1060, payload(200, 7, 80, 1))])
        self.assertEqual(result["stages"]["loop_iteration_ms"]["total_ms"], 200)
        self.assertEqual(result["stages"]["read_block_ms"]["loop_percent"], 50)
        self.assertEqual(result["executors"]["ack"]["calls"], 4)
        self.assertEqual(result["executors"]["ack"]["average_ms"]["call_ms"], 15)
        self.assertEqual(result["executors"]["ack"]["incomplete_calls"], 2)

    def test_writer_restart_is_not_hidden_by_another_workers_growth(self):
        def payload(a, b):
            return {"workers": [{"consumer_name": name, "timings": {
                "stages_ms": {"loop_iteration_ms": value}, "executor_ms": {}}}
                for name, value in (("one", a), ("two", b))]}
        result = writer_timing_window([(1000, payload(100, 100)), (1060, payload(10, 300))])
        self.assertEqual(result["stages"]["loop_iteration_ms"]["resets"], 1)
        self.assertIsNone(result["stages"]["loop_iteration_ms"]["total_ms"])

    def test_report_includes_completed_and_incomplete_executor_calls(self):
        data = fixture()
        for index, row in enumerate(data["samples"]):
            payload = {"consumer_name": "one", "counters": {}, "timings": {
                "stages_ms": {"loop_iteration_ms": index * 100, "read_block_ms": index * 40},
                "stage_containment": {"loop_iteration_ms": ["read_block_ms"]},
                "executor_ms": {"td_execute": {"calls": index * 2, "call_ms": index * 30,
                                                 "incomplete_calls": index}}}}
            row["logs"] = [{"module": "writer", "path": "writer.log", "snapshots": [
                {"at": row["at"], "payload": payload}]}]
        report = summarize(data)
        self.assertEqual(report["logs"][0]["writer_timing"]["worker_count"], 1)
        self.assertIn("Writer 执行器拆分", markdown(report))
        self.assertIn("read_block_ms", markdown(report))

    def test_fresh_writer_timings_and_new_retries_appear_in_report(self):
        data = fixture()
        for index, row in enumerate(data["samples"]):
            timing = {"count": 10 + index * 2, "average": 40}
            payload = {"workers": [{"timings": {"td_execute_ms": timing}}],
                       "aggregate": {"counters": {"committed_value_rows": index * 60, "retry_schema_rows": index * 3}, "gauges": {"buffer_rows": 10}}}
            row["logs"] = [{"module": "writer", "path": "C:/测试/runtime/logs/writer/current.log",
                            "snapshots": [{"at": row["at"], "payload": payload}]}]
        result = summarize(data)
        writer = result["logs"][0]
        self.assertEqual(writer["timings"]["td_execute_ms"]["average_ms"], 40)
        self.assertEqual(writer["counter_deltas"]["committed_value_rows"]["per_second"], 1)
        self.assertIn("TDengine 执行写入", markdown(result))
        self.assertTrue(any("retry_schema_rows" in item for item in result["findings"]))

    def test_partial_probe_failure_does_not_report_empty_queues_as_healthy(self):
        data = fixture()
        data["samples"] = [{"probe_error": "probe_unavailable", "logs": []}]
        result = summarize(data)
        self.assertIsNone(result["redis"]["total_lag"])
        self.assertTrue(result["evidence_gaps"])

    def test_last_probe_outage_does_not_reuse_old_queue_counts_as_current(self):
        data = fixture()
        data["samples"].append({"probe": {"at": 1061, "redis": {"available": False, "type": "ConnectionError"}}})
        result = summarize(data)
        self.assertIsNone(result["redis"]["total_lag"])
        self.assertIsNone(result["redis"]["input_frames_per_s"])
        self.assertTrue(any("ConnectionError" in gap for gap in result["evidence_gaps"]))

    def test_writer_aggregate_is_not_mixed_with_individual_worker_snapshots(self):
        data = fixture()
        for index, row in enumerate(data["samples"]):
            total = {"aggregate": {"counters": {"committed_value_rows": index * 60}},
                     "workers": [{"timings": {"td_execute_ms": {"count": 10 + index, "average": 5}}}]}
            worker = {"consumer_name": "one", "counters": {"committed_value_rows": 9000 + index},
                      "timings": {"td_execute_ms": {"count": 9000, "average": 900}}}
            row["logs"] = [{"module": "writer", "path": "writer.log", "snapshots": [
                {"at": row["at"], "payload": worker}, {"at": row["at"], "payload": total}]}]
        result = summarize(data)["logs"][0]
        self.assertEqual(result["scope"], "all_workers")
        self.assertEqual(result["counter_deltas"]["committed_value_rows"]["per_second"], 1)
        self.assertEqual(result["timings"]["td_execute_ms"]["average_ms"], 5)

    def test_mixed_old_worker_logs_without_total_are_explicitly_unknown(self):
        rows = [{"logs": [{"module": "writer", "path": "writer.log", "snapshots": [
            {"at": iso(1000), "payload": {"consumer_name": "one", "counters": {"rows_written": 10}}},
            {"at": iso(1060), "payload": {"consumer_name": "two", "counters": {"rows_written": 30}}}]}]}]
        result = summarize_logs(rows, 1000, 1060)[0]
        self.assertEqual(result["scope"], "mixed_workers_without_aggregate")
        self.assertFalse(result["fresh"])
        self.assertEqual(result["counter_deltas"], {})

    def test_redis_probe_only_reads_and_does_not_export_frame_or_secret_data(self):
        import redis

        class Client:
            def ping(self):
                return True

            def close(self):
                pass

            def pipeline(self, **kwargs):
                return Pipe()

            def scan(self, cursor, match, count):
                return 0, ["rt:dlq:test:g001"] if "dlq" in match else ["rt:stream:test:g001:0"]

            def llen(self, key):
                return 1

            def lrange(self, key, start, end):
                return [json.dumps({"entry_id": "1-0", "last_error": "timeout password=SECRET",
                                    "data": {"values": "PRIVATE_FRAME"}})]

            def slowlog_get(self, count):
                return [{"id": 1, "start_time": 1000, "duration": 2000, "command": b"AUTH SECRET"}]

        class Pipe:
            def __init__(self):
                self.calls = []

            def __getattr__(self, name):
                if name not in {"info", "hgetall", "xinfo_stream", "xinfo_groups", "xpending"}:
                    raise AssertionError("Mutation attempted: " + name)
                def record(*args):
                    self.calls.append(name)
                return record

            def execute(self, **kwargs):
                values = {"info": {}, "hgetall": {"rows_written": "1"},
                          "xinfo_stream": {"length": 1, "entries-added": 1,
                                           "first-entry": ["1-0", {"values": "PRIVATE_FRAME"}]},
                          "xinfo_groups": [{"name": "writer_cg", "pending": 1, "lag": 0}],
                          "xpending": {"min": "1-0"}}
                return [values[name] for name in self.calls]

        with patch.object(redis, "Redis", return_value=Client()):
            result = redis_probe("writer_cg", 64)
        self.assertTrue(result["available"])
        self.assertNotIn("SECRET", json.dumps(result))
        self.assertNotIn("PRIVATE_FRAME", json.dumps(result))
        self.assertEqual(result["slowlog"][0]["command"], "AUTH")

    def test_scan_is_bounded_and_marks_partial_results(self):
        class Client:
            def scan(self, *args, **kwargs):
                return 1, ["rt:stream:a", "rt:stream:b"]
        keys, truncated = bounded_scan(Client(), "rt:stream:*", 1, 0)
        self.assertEqual(keys, ["rt:stream:a"])
        self.assertTrue(truncated)

    def test_host_disk_latency_and_reused_pid_are_handled(self):
        rows = []
        for at, counter, base, started in ((1000, 10000, 10, "old"), (1060, 30000, 20, "new")):
            rows.append({"at": iso(at), "processes": [{"name": "writer", "pid": 5, "started_at": started,
                                                        "cpu_seconds": counter, "rss_bytes": 1048576}],
                         "disk_raw": [{"AvgDisksecPerRead": counter, "AvgDisksecPerRead_Base": base,
                                       "AvgDisksecPerWrite": counter, "AvgDisksecPerWrite_Base": base,
                                       "Frequency_PerfTime": 1000000}]})
        result = resource_summary(rows)
        self.assertEqual(result["max_interval_disk_write_ms"], 2)
        self.assertTrue(all(process["cpu_percent_one_core"] is None for process in result["processes"]))

    def test_probe_error_does_not_export_secrets(self):
        import redis
        with patch.object(redis, "Redis", side_effect=RuntimeError("password=SECRET")):
            # The outer sample catches constructor errors without exporting their string.
            from pipeline_diagnostics import sample
            with patch("pipeline_diagnostics.http_probe", return_value={"available": False}):
                result = sample("writer_cg", 5)
        self.assertNotIn("SECRET", json.dumps(result))


if __name__ == "__main__":
    unittest.main()
