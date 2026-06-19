import json
import tempfile
import unittest
from pathlib import Path

from summarize_performance_snapshots import extract_snapshots, summarize


class PerformanceSnapshotSummaryTests(unittest.TestCase):
    def test_extracts_collector_and_writer_markers_and_skips_bad_json(self):
        with tempfile.TemporaryDirectory() as directory:
            log_path = Path(directory) / "current.log"
            first = {"timings": {"read_ms": {"count": 1}}}
            second = {"timings": {"write_batch_ms": {"count": 2}}}
            log_path.write_text(
                "prefix Performance snapshot: " + json.dumps(first) + "\n"
                "prefix [performance] not-json\n"
                "prefix [performance] " + json.dumps(second) + "\n",
                encoding="utf-8",
            )

            snapshots = extract_snapshots(log_path)
            result = summarize(log_path)

            self.assertEqual(snapshots, [first, second])
            self.assertEqual(result["snapshot_count"], 2)
            self.assertEqual(result["last"], second)


if __name__ == "__main__":
    unittest.main()
