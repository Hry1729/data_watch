import os
import sys
import time
import unittest
from unittest.mock import AsyncMock, patch

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.services.ws_push import WsPushManager, _latest_is_stale


class FakeWebSocket:
    def __init__(self):
        self.messages = []

    async def send_json(self, message):
        self.messages.append(message)


class TestWsPushFreshness(unittest.IsolatedAsyncioTestCase):
    def test_latest_timestamp_controls_staleness(self):
        now_ms = time.time() * 1000
        self.assertFalse(_latest_is_stale({"1": {"t": now_ms}}))
        self.assertTrue(_latest_is_stale({"1": {"t": now_ms - 121_000}}))
        self.assertTrue(_latest_is_stale({"1": {"v": 12.3}}))

    async def test_stale_snapshot_is_sent_as_offline_without_old_values(self):
        manager = WsPushManager()
        ws = FakeWebSocket()
        stale = {"1": {"v": 12.3, "t": (time.time() - 121) * 1000, "q": 0}}
        manager._read_latest = AsyncMock(return_value=stale)

        with patch("app.services.ws_push._check_tdengine", AsyncMock(return_value=True)):
            await manager.subscribe(ws, "PLC001", "g001")

        self.assertEqual(ws.messages[0]["values"], {})
        self.assertTrue(ws.messages[0]["deviceOffline"])
        await manager.unsubscribe(ws, "PLC001", "g001")


if __name__ == "__main__":
    unittest.main()
