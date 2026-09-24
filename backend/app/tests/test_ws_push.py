import asyncio
import os
import sys
import time
import unittest
from unittest.mock import AsyncMock, patch

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.services.ws_push import WsPushManager, _coalesce_ws_message, _latest_is_stale, parse_subscription_point_ids


class FakeWebSocket:
    def __init__(self):
        self.messages = []

    async def send_json(self, message):
        self.messages.append(message)


class TestWsPushFreshness(unittest.IsolatedAsyncioTestCase):
    def test_subscription_point_ids_reject_unicode_and_oversized_numbers(self):
        self.assertIsNone(parse_subscription_point_ids(None))
        self.assertEqual(parse_subscription_point_ids(["001", 2]), {"1", "2"})
        for raw in (["²"], ["9" * 4301], [True], [0], [2**63], "1"):
            with self.subTest(raw=str(raw)[:20]), self.assertRaises(ValueError):
                parse_subscription_point_ids(raw)

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

    async def test_selected_points_share_read_but_receive_filtered_updates(self):
        manager = WsPushManager(min_push_interval_seconds=0.01, full_sync_interval_seconds=5)
        first, second = FakeWebSocket(), FakeWebSocket()
        now = time.time() * 1000
        manager._read_latest = AsyncMock(return_value={
            "1": {"v": 1, "q": 1, "t": now},
            "2": {"v": 2, "q": 1, "t": now},
        })
        with patch("app.services.ws_push._check_tdengine", AsyncMock(return_value=True)):
            await manager.subscribe(first, "PLC001", "g001", {"1"}, supports_freshness=True)
            await manager.subscribe(second, "PLC001", "g001", {"2"}, supports_freshness=True)
        for _ in range(30):
            if first.messages and second.messages:
                break
            await asyncio.sleep(0.01)
        manager._read_latest.return_value = {
            "1": {"v": 3, "q": 1, "t": now + 100},
            "2": {"v": 2, "q": 1, "t": now + 100},
        }
        manager._notify_change('{"device_code":"PLC001","group_code":"g001"}')
        for _ in range(50):
            if len(first.messages) >= 2 and len(second.messages) >= 2:
                break
            await asyncio.sleep(0.01)
        self.assertEqual(first.messages[0]["values"].keys(), {"1"})
        self.assertEqual(second.messages[0]["values"].keys(), {"2"})
        self.assertEqual(first.messages[-1]["values"].keys(), {"1"})
        self.assertEqual(second.messages[-1]["values"], {})
        self.assertEqual(second.messages[-1]["freshness"], {"2": now + 100})
        self.assertEqual(len(manager._tasks), 1)
        await manager.remove_all_subscriptions(first)
        await manager.remove_all_subscriptions(second)

    async def test_selected_redis_read_uses_hmget_and_legacy_uses_hgetall(self):
        from types import SimpleNamespace

        client = SimpleNamespace(
            hmget=AsyncMock(return_value=[
                '{"v": 7, "q": 1, "t": 123}',
                None,
            ]),
            hgetall=AsyncMock(return_value={"1": '{"v": 7, "q": 1, "t": 123}'}),
        )
        manager = WsPushManager()
        with patch("app.db.redis.get_redis_client", AsyncMock(return_value=client)):
            selected = await manager._read_latest("PLC001", "g001", {"1", "2"})
            self.assertEqual(selected, {"1": {"v": 7, "q": 1, "t": 123}})
            client.hmget.assert_awaited_once_with("rt:latest:PLC001:g001", ["1", "2"])
            client.hgetall.assert_not_awaited()
            all_points = await manager._read_latest("PLC001", "g001")
            self.assertEqual(all_points, selected)
            client.hgetall.assert_awaited_once_with("rt:latest:PLC001:g001")

    async def test_new_selected_subscriber_keeps_existing_snapshot_baseline(self):
        manager = WsPushManager(min_push_interval_seconds=0.01)
        first, second = FakeWebSocket(), FakeWebSocket()
        now = time.time() * 1000
        latest = {
            "1": {"v": 1, "q": 1, "t": now},
            "2": {"v": 2, "q": 1, "t": now},
        }

        async def read_selected(_device, _group, point_ids=None):
            return {key: value for key, value in latest.items()
                    if point_ids is None or key in point_ids}

        manager._read_latest = AsyncMock(side_effect=read_selected)
        with patch("app.services.ws_push._check_tdengine", AsyncMock(return_value=True)):
            await manager.subscribe(first, "PLC001", "g001", {"1"})
            for _ in range(30):
                if first.messages:
                    break
                await asyncio.sleep(0.01)
            latest["1"] = {"v": 3, "q": 1, "t": now + 100}
            await manager.subscribe(second, "PLC001", "g001", {"2"})
            for _ in range(50):
                if any(msg.get("values", {}).get("1", {}).get("v") == 3
                       for msg in first.messages):
                    break
                await asyncio.sleep(0.01)
        self.assertEqual(second.messages[0]["values"], {"2": latest["2"]})
        self.assertEqual(manager._selected_point_ids(("PLC001", "g001")), {"1", "2"})
        self.assertTrue(any(msg.get("values", {}).get("1", {}).get("v") == 3
                            for msg in first.messages))
        await manager.unsubscribe(second, "PLC001", "g001")
        self.assertEqual(manager._selected_point_ids(("PLC001", "g001")), {"1"})
        await manager.unsubscribe(first, "PLC001", "g001")

    async def test_new_subscriber_during_old_read_does_not_get_false_offline(self):
        manager = WsPushManager(min_push_interval_seconds=0.01)
        first, second = FakeWebSocket(), FakeWebSocket()
        now = time.time() * 1000
        fresh = {"v": 1, "q": 1, "t": now}
        stale = {"v": 1, "q": 1, "t": now - 121_000}
        second_fresh = {"v": 2, "q": 1, "t": now}
        old_read_started = asyncio.Event()
        release_old_read = asyncio.Event()
        union_read = asyncio.Event()
        first_point_reads = 0

        async def read_selected(_device, _group, point_ids=None):
            nonlocal first_point_reads
            if point_ids == {"1"}:
                first_point_reads += 1
                if first_point_reads == 2:
                    old_read_started.set()
                    await release_old_read.wait()
                    return {"1": stale}
                return {"1": fresh}
            if point_ids == {"2"}:
                return {"2": second_fresh}
            assert point_ids == {"1", "2"}
            union_read.set()
            return {"1": stale, "2": second_fresh}

        manager._read_latest = AsyncMock(side_effect=read_selected)
        with patch("app.services.ws_push._check_tdengine", AsyncMock(return_value=True)):
            await manager.subscribe(first, "PLC001", "g001", {"1"})
            manager._notify_change('{"device_code":"PLC001","group_code":"g001"}')
            await asyncio.wait_for(old_read_started.wait(), 1)
            await manager.subscribe(second, "PLC001", "g001", {"2"})
            for _ in range(30):
                if second.messages:
                    break
                await asyncio.sleep(0.01)
            self.assertEqual(second.messages[0]["values"], {"2": second_fresh})
            release_old_read.set()
            await asyncio.wait_for(union_read.wait(), 1)
            await asyncio.sleep(0.05)
        self.assertEqual(manager._snapshots[("PLC001", "g001")],
                         {"1": stale, "2": second_fresh})
        self.assertFalse(any(msg["type"] == "offline" for msg in second.messages))
        await manager.remove_all_subscriptions(first)
        await manager.remove_all_subscriptions(second)

    async def test_legacy_client_gets_full_entry_when_only_timestamp_changes(self):
        manager = WsPushManager(min_push_interval_seconds=0.01)
        ws = FakeWebSocket()
        now = time.time() * 1000
        manager._read_latest = AsyncMock(return_value={"1": {"v": 5, "q": 1, "t": now}})
        with patch("app.services.ws_push._check_tdengine", AsyncMock(return_value=True)):
            await manager.subscribe(ws, "PLC001", "g001")
        manager._read_latest.return_value = {"1": {"v": 5, "q": 1, "t": now + 100}}
        manager._notify_change('{"device_code":"PLC001","group_code":"g001"}')
        for _ in range(50):
            if any(message["type"] == "diff" for message in ws.messages):
                break
            await asyncio.sleep(0.01)
        diff = next(message for message in ws.messages if message["type"] == "diff")
        self.assertEqual(diff["values"]["1"]["t"], now + 100)
        self.assertNotIn("freshness", diff)
        await manager.remove_all_subscriptions(ws)

    async def test_selected_stale_point_recovers_with_snapshot_when_group_stays_online(self):
        manager = WsPushManager(min_push_interval_seconds=0.01)
        ws = FakeWebSocket()
        now = time.time() * 1000
        old = now - 121_000
        manager._read_latest = AsyncMock(return_value={
            "1": {"v": 5, "q": 1, "t": old},
            "2": {"v": 9, "q": 1, "t": now},
        })
        with patch("app.services.ws_push._check_tdengine", AsyncMock(return_value=True)):
            await manager.subscribe(ws, "PLC001", "g001", {"1"}, supports_freshness=True)
        for _ in range(30):
            if ws.messages:
                break
            await asyncio.sleep(0.01)
        self.assertEqual(ws.messages[0]["values"], {})
        self.assertTrue(ws.messages[0]["deviceOffline"])
        manager._read_latest.return_value = {
            "1": {"v": 5, "q": 1, "t": now + 100},
            "2": {"v": 9, "q": 1, "t": now + 100},
        }
        manager._notify_change('{"device_code":"PLC001","group_code":"g001"}')
        for _ in range(50):
            if len(ws.messages) >= 2:
                break
            await asyncio.sleep(0.01)
        self.assertEqual(ws.messages[-1]["type"], "snapshot")
        self.assertEqual(ws.messages[-1]["values"]["1"]["v"], 5)
        self.assertFalse(ws.messages[-1]["deviceOffline"])
        await manager.remove_all_subscriptions(ws)

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
