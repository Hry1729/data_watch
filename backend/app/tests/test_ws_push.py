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

    async def test_change_notification_only_wakes_matching_subscription(self):
        manager = WsPushManager()
        matching = manager._events.setdefault(("PLC001", "g001"), __import__("asyncio").Event())
        other = manager._events.setdefault(("PLC002", "g001"), __import__("asyncio").Event())

        manager._notify_change('{"device_code":"PLC001","group_code":"g001"}')

        self.assertTrue(matching.is_set())
        self.assertFalse(other.is_set())

    async def test_batched_change_notification_wakes_all_matching_subscriptions(self):
        manager = WsPushManager()
        first = manager._events.setdefault(("PLC001", "g001"), __import__("asyncio").Event())
        second = manager._events.setdefault(("PLC002", "g001"), __import__("asyncio").Event())
        other = manager._events.setdefault(("PLC003", "g001"), __import__("asyncio").Event())

        manager._notify_change(
            '[{"device_code":"PLC001","group_code":"g001"},'
            '{"device_code":"PLC002","group_code":"g001"}]'
        )

        self.assertTrue(first.is_set())
        self.assertTrue(second.is_set())
        self.assertFalse(other.is_set())

    async def test_invalid_change_notification_is_ignored(self):
        manager = WsPushManager()
        event = manager._events.setdefault(("PLC001", "g001"), __import__("asyncio").Event())

        manager._notify_change("not-json")
        manager._notify_change({"device_code": "PLC001"})

        self.assertFalse(event.is_set())

if __name__ == "__main__":
    unittest.main()
