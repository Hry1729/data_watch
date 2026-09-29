from __future__ import annotations

import asyncio
import os
import sys
import unittest
from unittest.mock import patch

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.core.request_metrics import _current, observe_http, record_operation


class TestRequestMetrics(unittest.IsolatedAsyncioTestCase):
    async def test_concurrent_requests_have_independent_timings_and_ids(self):
        async def run(duration):
            sent = []

            async def dispatch(scope, receive, send):
                record_operation("mysql", duration)
                await asyncio.sleep(0)
                await send({"type": "http.response.start", "status": 200, "headers": []})
                await send({"type": "http.response.body", "body": b"ok"})

            async def send(message):
                sent.append(message)

            await observe_http(dispatch, {"type": "http", "method": "GET", "path": "/api/device-monitor/point/list"}, None, send)
            self.assertIsNone(_current.get())
            return dict(sent[0]["headers"])

        with patch("app.core.request_metrics.logger.info") as log:
            first, second = await asyncio.gather(run(0.01), run(0.02))
        self.assertNotEqual(first[b"x-request-id"], second[b"x-request-id"])
        self.assertIn(b"server-timing", first)
        self.assertEqual([call.args[-2] for call in log.call_args_list], [{"mysql": 10.0}, {"mysql": 20.0}])

    async def test_exception_is_preserved_and_context_reset(self):
        async def dispatch(*args):
            record_operation("redis", 0.01)
            raise RuntimeError("query failed")

        with patch("app.core.request_metrics.logger.warning") as log:
            with self.assertRaisesRegex(RuntimeError, "query failed"):
                await observe_http(dispatch, {"type": "http", "method": "GET", "path": "/api/device-monitor/device/list"}, None, None)
        self.assertIsNone(_current.get())
        self.assertEqual(log.call_args.args[4], 500)

    async def test_other_paths_are_untouched(self):
        async def dispatch(scope, receive, send):
            self.assertIsNone(_current.get())
            await send({"type": "http.response.start", "status": 200, "headers": []})

        sent = []

        async def send(message):
            sent.append(message)

        await observe_http(dispatch, {"type": "http", "path": "/health"}, None, send)
        self.assertEqual(sent[0]["headers"], [])
