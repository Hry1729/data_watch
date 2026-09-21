import asyncio
import os
import sys
import time
import unittest
from unittest.mock import AsyncMock, patch

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.services.ws_push import WsPushManager, _coalesce_ws_message, _latest_is_stale


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

        for _ in range(20):
            if ws.messages:
                break
            await asyncio.sleep(0.01)
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

    def test_diff_coalesce_keeps_all_points(self):
        first = {"type": "diff", "values": {"1": {"v": 1}}}
        second = {"type": "diff", "values": {"2": {"v": 2}, "1": {"v": 9}}}
        merged = _coalesce_ws_message(first, second)
        self.assertEqual(merged["values"], {"1": {"v": 9}, "2": {"v": 2}})
        offline = _coalesce_ws_message(merged, {"type": "offline", "device_code": "PLC001"})
        self.assertEqual(offline["type"], "offline")

    async def test_slow_client_does_not_block_fast_client(self):
        manager = WsPushManager(send_timeout_seconds=0.05)
        fast = FakeWebSocket()
        hang_after_snapshot = asyncio.Event()

        class SlowWebSocket:
            def __init__(self):
                self.messages = []

            async def send_json(self, message):
                self.messages.append(message)
                if message.get("type") != "snapshot":
                    hang_after_snapshot.set()
                    await asyncio.sleep(10)

        slow = SlowWebSocket()
        latest = {"1": {"v": 1, "t": time.time() * 1000}}
        manager._read_latest = AsyncMock(return_value=latest)
        with patch("app.services.ws_push._check_tdengine", AsyncMock(return_value=True)):
            await manager.subscribe(fast, "PLC001", "g001")
            await manager.subscribe(slow, "PLC001", "g001")
        for _ in range(50):
            if fast.messages and slow.messages:
                break
            await asyncio.sleep(0.01)

        await manager._broadcast(
            ("PLC001", "g001"),
            {"type": "diff", "device_code": "PLC001", "group_code": "g001", "values": {"2": {"v": 2}}},
        )
        await asyncio.wait_for(hang_after_snapshot.wait(), timeout=1)
        for _ in range(20):
            if any(msg.get("type") == "diff" for msg in fast.messages):
                break
            await asyncio.sleep(0.02)
        self.assertTrue(any(msg.get("type") == "diff" for msg in fast.messages))
        await asyncio.sleep(0.12)
        self.assertNotIn(slow, manager._subscribers.get(("PLC001", "g001"), set()))
        self.assertLessEqual(manager.sender_task_count(), 1)
        await manager.remove_all_subscriptions(fast)

    async def test_pending_queue_is_bounded_per_subscription_and_revoke_drops_it(self):
        manager = WsPushManager(send_timeout_seconds=0.2)
        gate = asyncio.Event()

        class SlowWebSocket:
            async def send_json(self, message):
                if message.get("type") != "snapshot":
                    await gate.wait()

        ws = SlowWebSocket()
        manager._read_latest = AsyncMock(return_value={"1": {"v": 1, "t": time.time() * 1000}})
        with patch("app.services.ws_push._check_tdengine", AsyncMock(return_value=True)):
            await manager.subscribe(ws, "PLC001", "g001")

        await manager._broadcast(
            ("PLC001", "g001"),
            {"type": "diff", "values": {"1": {"v": 1}}},
        )
        await asyncio.sleep(0.02)
        await manager._broadcast(
            ("PLC001", "g001"),
            {"type": "diff", "values": {"2": {"v": 2}}},
        )
        await manager._broadcast(
            ("PLC001", "g001"),
            {"type": "diff", "values": {"3": {"v": 3}}},
        )
        self.assertLessEqual(manager.pending_count(ws), 1)
        pending = manager._senders[ws]._pending[("PLC001", "g001")]
        self.assertEqual(pending["values"]["2"]["v"], 2)
        self.assertEqual(pending["values"]["3"]["v"], 3)
        await manager.revoke_connection(ws)
        self.assertEqual(manager.pending_count(ws), 0)
        self.assertEqual(manager.sender_task_count(), 0)
        gate.set()

if __name__ == "__main__":
    unittest.main()


class TestUnsubscribeInFlight(unittest.IsolatedAsyncioTestCase):
    async def test_unsubscribe_cancels_only_target_send(self):
        from app.services.ws_push import _ConnectionSender
        entered=asyncio.Event();cancelled=asyncio.Event();messages=[]
        class WS:
            async def send_json(self,msg):
                if msg["device_code"]=="slow":
                    entered.set()
                    try:await asyncio.Event().wait()
                    finally:cancelled.set()
                else:messages.append(msg)
        sender=_ConnectionSender(WS(),send_timeout=5,on_dead=AsyncMock())
        sender.enqueue(("slow","g"),{"device_code":"slow"})
        await entered.wait()
        sender.enqueue(("fast","g"),{"device_code":"fast"})
        sender.drop_key(("slow","g"))
        await asyncio.wait_for(cancelled.wait(),1)
        for _ in range(20):
            if messages:break
            await asyncio.sleep(.01)
        self.assertEqual(messages,[{"device_code":"fast","values":None}])
        await sender.close()
