import io
import sys
import unittest
from contextlib import redirect_stderr
from types import SimpleNamespace
from unittest.mock import patch

from inspect_receipt_capacity import inspect_receipts, main


class FakeRedis:
    def __init__(self, pages, memory=None, ttls=None):
        self.pages = list(pages)
        self.memory = memory or {}
        self.ttls = ttls or {}
        self.calls = 0

    def scan(self, cursor, match, count):
        self.calls += 1
        self.last_scan = (cursor, match, count)
        return self.pages.pop(0)

    def memory_usage(self, key):
        return self.memory.get(key)

    def ttl(self, key):
        return self.ttls.get(key, -2)


class ReceiptCapacityTests(unittest.TestCase):
    def test_reports_observed_bytes_and_ttl_range(self):
        client = FakeRedis(
            [(0, [b"r1", b"r2"])],
            memory={b"r1": 80, b"r2": 120},
            ttls={b"r1": 60, b"r2": 600},
        )

        result = inspect_receipts(client)

        self.assertEqual(result["scanned_keys"], 2)
        self.assertEqual(result["measured_keys"], 2)
        self.assertEqual(result["observed_bytes"], 200)
        self.assertEqual((result["ttl_min_seconds"], result["ttl_max_seconds"]), (60, 600))
        self.assertTrue(result["scan_complete"])
        self.assertFalse(result["truncated"])
        self.assertEqual(client.last_scan[1], "collector:receipt:*")

    def test_stops_at_key_limit_and_marks_truncated(self):
        client = FakeRedis([(7, [b"r1", b"r2", b"r3"]), (0, [b"r4"])])

        result = inspect_receipts(client, max_keys=2, max_scan_calls=5)

        self.assertEqual(result["scanned_keys"], 2)
        self.assertEqual(result["scan_calls"], 1)
        self.assertFalse(result["scan_complete"])
        self.assertTrue(result["truncated"])

    def test_stops_at_scan_call_limit(self):
        client = FakeRedis([(1, [b"r1"]), (2, [b"r2"]), (0, [b"r3"])])

        result = inspect_receipts(client, max_keys=10, max_scan_calls=2)

        self.assertEqual(result["scanned_keys"], 2)
        self.assertEqual(result["scan_calls"], 2)
        self.assertFalse(result["scan_complete"])
        self.assertTrue(result["truncated"])

    def test_deduplicates_keys_across_scan_pages(self):
        client = FakeRedis(
            [(1, [b"r1", b"r1"]), (0, [b"r1", b"r2"])],
            memory={b"r1": 10, b"r2": 20},
            ttls={b"r1": 1, b"r2": 2},
        )

        result = inspect_receipts(client)

        self.assertEqual(result["scanned_keys"], 2)
        self.assertEqual(result["observed_bytes"], 30)
        self.assertEqual(result["scan_calls"], 2)

    def test_counts_key_that_disappears_before_measurement(self):
        client = FakeRedis([(0, [b"gone"])])

        result = inspect_receipts(client)

        self.assertEqual(result["scanned_keys"], 1)
        self.assertEqual(result["measured_keys"], 0)
        self.assertEqual(result["missing_keys"], 1)
        self.assertEqual(result["observed_bytes"], 0)
        self.assertIsNone(result["ttl_min_seconds"])

    def test_cli_suppresses_read_error_details_and_returns_failure(self):
        class BrokenRedis(FakeRedis):
            def scan(self, cursor, match, count):
                raise RuntimeError("redis://secret@host/private-key")

        redis_stub = SimpleNamespace(
            Redis=SimpleNamespace(from_url=lambda *args, **kwargs: BrokenRedis([]))
        )
        stderr = io.StringIO()
        with (
            patch.dict("os.environ", {"REDIS_URL": "redis://secret@host"}),
            patch.dict(sys.modules, {"redis": redis_stub}),
            redirect_stderr(stderr),
        ):
            result = main([])

        self.assertEqual(result, 1)
        self.assertNotIn("secret", stderr.getvalue())
        self.assertIn("details suppressed", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
