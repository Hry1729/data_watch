"""驱动生命周期：async Agent 调用与 driver_operation 补偿单测。"""

from __future__ import annotations

import inspect
import os
import sys
import unittest
from unittest.mock import AsyncMock, MagicMock, patch

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.core.exceptions import ApiError
from app.services.driver_lifecycle import AgentOutcomeUnknown, DriverLifecycleService


def _mapping_result(rows):
    """构造 sqlalchemy Result.mappings() 风格 mock。"""
    result = MagicMock()
    mappings = MagicMock()
    mappings.all.return_value = rows
    mappings.first.return_value = rows[0] if rows else None
    result.mappings.return_value = mappings
    return result


def _rowcount_result(rowcount: int):
    result = MagicMock()
    result.rowcount = rowcount
    return result


class TestDriverLifecycleHttpx(unittest.IsolatedAsyncioTestCase):
    def test_driver_mutations_are_audited(self):
        from app.api.routes.catalog import driver_register, driver_uninstall

        register_audit = inspect.getclosurevars(driver_register).nonlocals
        uninstall_audit = inspect.getclosurevars(driver_uninstall).nonlocals

        self.assertEqual(register_audit["module_name"], "driver")
        self.assertEqual(register_audit["action_name"], "register")
        self.assertEqual(uninstall_audit["module_name"], "driver")
        self.assertEqual(uninstall_audit["action_name"], "uninstall")

    def test_uninstall_has_no_client_compensation_bypass(self):
        parameters = inspect.signature(DriverLifecycleService.uninstall).parameters
        self.assertNotIn("compensate", parameters)

    async def test_audit_failure_does_not_replace_business_result_or_error(self):
        from app.core.audit import audit_log

        @audit_log(module_name="driver", action_name="test")
        async def successful():
            return {"ok": True}

        @audit_log(module_name="driver", action_name="test")
        async def failed():
            raise ValueError("business error")

        audit_failure = AsyncMock(side_effect=RuntimeError("audit unavailable"))
        with patch("app.core.audit.write_operation_log", new=audit_failure):
            self.assertEqual(await successful(), {"ok": True})
            with self.assertRaisesRegex(ValueError, "business error"):
                await failed()

    async def test_uninstall_status_reads_persistent_unknown_operation(self):
        session = AsyncMock()
        session.execute = AsyncMock(
            return_value=_mapping_result(
                [
                    {
                        "lifecycle_status": "uninstalling",
                        "driver_id": "drv1",
                        "operation_id": "op-1",
                        "agent_phase": "unknown",
                        "db_phase": "pending",
                        "created_at": "2026-07-26 10:00:00",
                        "updated_at": "2026-07-26 10:01:00",
                    }
                ]
            )
        )

        result = await DriverLifecycleService(session).uninstall_status("ext")

        self.assertEqual(result["operation"]["operation_id"], "op-1")
        self.assertEqual(result["operation"]["status"], "unknown")
        sql = str(session.execute.await_args.args[0])
        self.assertIn("lifecycle_operation_id", sql)

    async def test_register_uses_loaded_agent_metadata(self):
        session = AsyncMock()
        service = DriverLifecycleService(session)
        apply_db = AsyncMock(
            return_value={"id": 1, "protocol": "ext", "driverId": "drv1"}
        )
        with patch("app.services.driver_lifecycle._agent_token", return_value="svc"), patch.object(
            service,
            "_list_agent_drivers",
            new=AsyncMock(
                return_value=[
                    {
                        "id": "drv1",
                        "protocol": "ext",
                        "status": "loaded",
                        "version": "2.0.0",
                        "sha256": "agent-sha",
                        "name": "Agent Driver",
                    }
                ]
            ),
        ), patch.object(service, "_fetch_protocol_row", new=AsyncMock(return_value=None)), patch.object(
            service, "_begin_operation", new=AsyncMock(return_value="op-1")
        ), patch.object(service, "_apply_register_db", new=apply_db), patch.object(
            service, "_update_operation_phases", new=AsyncMock()
        ):
            await service.register_protocol(
                {
                    "protocol": "ext",
                    "driverId": "drv1",
                    "version": "forged",
                    "sha256": "forged",
                }
            )

        assert apply_db.await_args.kwargs["version"] == "2.0.0"
        assert apply_db.await_args.kwargs["sha256"] == "agent-sha"

    async def test_register_rejects_agent_load_failed(self):
        service = DriverLifecycleService(AsyncMock())
        with patch("app.services.driver_lifecycle._agent_token", return_value="svc"), patch.object(
            service,
            "_list_agent_drivers",
            new=AsyncMock(
                return_value=[
                    {
                        "id": "drv1",
                        "protocol": "ext",
                        "status": "load_failed",
                        "error": "bad import",
                    }
                ]
            ),
        ):
            with self.assertRaises(ApiError) as ctx:
                await service.register_protocol({"protocol": "ext", "driverId": "drv1"})

        self.assertEqual(ctx.exception.code, 409)
        self.assertIn("bad import", ctx.exception.message)

    async def test_register_rejects_protocol_longer_than_comm_protocol_column(self):
        service = DriverLifecycleService(AsyncMock())
        with self.assertRaises(ApiError) as ctx:
            await service.register_protocol(
                {"protocol": "p" * 21, "driverId": "drv1"}
            )

        self.assertEqual(ctx.exception.code, 400)
        self.assertIn("20", ctx.exception.message)

    async def test_register_rejects_agent_metadata_longer_than_database_columns(self):
        service = DriverLifecycleService(AsyncMock())
        agent_driver = {
            "id": "drv1",
            "protocol": "ext",
            "status": "loaded",
            "version": "1.0.0",
            "description": "d" * 256,
        }
        with patch("app.services.driver_lifecycle._agent_token", return_value="svc"), patch.object(
            service,
            "_list_agent_drivers",
            new=AsyncMock(return_value=[agent_driver]),
        ):
            with self.assertRaises(ApiError) as ctx:
                await service.register_protocol({"protocol": "ext", "driverId": "drv1"})

        self.assertEqual(ctx.exception.code, 400)
        self.assertIn("255", ctx.exception.message)

    async def test_call_agent_uninstall_timeout_maps_to_unknown(self):
        session = AsyncMock()
        service = DriverLifecycleService(session)
        with patch("app.services.driver_lifecycle.httpx.AsyncClient") as client_cls:
            client = AsyncMock()
            client.__aenter__ = AsyncMock(return_value=client)
            client.__aexit__ = AsyncMock(return_value=None)
            import httpx

            client.delete = AsyncMock(side_effect=httpx.TimeoutException("timeout"))
            client_cls.return_value = client
            with self.assertRaises(AgentOutcomeUnknown) as ctx:
                await service._call_agent_uninstall("drv1", "token")
            self.assertEqual(ctx.exception.code, 503)
            self.assertIn("超时", ctx.exception.message)

    async def test_uninstall_timeout_persists_agent_phase_unknown(self):
        """DELETE 已发出但响应超时：记 unknown，供启动 reconcile，不可当 failed 清掉。"""
        session = AsyncMock()
        service = DriverLifecycleService(session)
        session.execute = AsyncMock(
            side_effect=[
                _mapping_result(  # protocol row
                    [{"id": 1, "protocol": "ext", "description": "x", "port": 0}]
                ),
                _mapping_result([]),  # update phases unknown
            ]
        )
        session.commit = AsyncMock()

        with patch.object(
            service,
            "_begin_uninstall_operation",
            new=AsyncMock(return_value="op-1"),
        ), patch.object(
            service,
            "_call_agent_uninstall",
            new=AsyncMock(side_effect=AgentOutcomeUnknown(503, "调用 Agent 卸载超时（30s）")),
        ), patch("app.services.driver_lifecycle._agent_token", return_value="tok"):
            with self.assertRaises(AgentOutcomeUnknown):
                await service.uninstall(protocol="ext", driver_id="drv1")

        self.assertTrue(any("unknown" in str(c) for c in session.execute.await_args_list))
        session.commit.assert_awaited()

    async def test_uninstall_definite_agent_failure_is_terminal(self):
        session = AsyncMock()
        service = DriverLifecycleService(session)
        session.execute = AsyncMock(
            side_effect=[
                _mapping_result(
                    [{"id": 1, "protocol": "ext", "description": "x", "port": 0}]
                ),
                _mapping_result([]),  # release persistent lifecycle gate
                _mapping_result([]),  # update failed/failed
            ]
        )

        with patch.object(
            service,
            "_begin_uninstall_operation",
            new=AsyncMock(return_value="op-1"),
        ), patch.object(
            service,
            "_call_agent_uninstall",
            new=AsyncMock(side_effect=ApiError(502, "Agent rejected uninstall")),
        ), patch("app.services.driver_lifecycle._agent_token", return_value="tok"):
            with self.assertRaises(ApiError):
                await service.uninstall(protocol="ext", driver_id="drv1")

        phase_call = session.execute.await_args_list[-1]
        self.assertEqual(phase_call.args[1]["agent_phase"], "failed")
        self.assertEqual(phase_call.args[1]["db_phase"], "failed")
        session.commit.assert_awaited()

    async def test_404_still_listed_raises_502(self):
        session = AsyncMock()
        service = DriverLifecycleService(session)

        delete_resp = MagicMock()
        delete_resp.status_code = 404
        delete_resp.text = '{"detail":"not found"}'
        delete_resp.json.return_value = {"detail": "not found"}

        list_resp = MagicMock()
        list_resp.raise_for_status = MagicMock()
        list_resp.json.return_value = {"drivers": [{"id": "drv1"}]}

        with patch("app.services.driver_lifecycle.httpx.AsyncClient") as client_cls:
            client = AsyncMock()
            client.__aenter__ = AsyncMock(return_value=client)
            client.__aexit__ = AsyncMock(return_value=None)
            client.delete = AsyncMock(return_value=delete_resp)
            client.get = AsyncMock(return_value=list_resp)
            client_cls.return_value = client
            with self.assertRaises(ApiError) as ctx:
                await service._call_agent_uninstall("drv1", "token")
            self.assertEqual(ctx.exception.code, 502)
            self.assertIn("仍在列表中", ctx.exception.message)

    async def test_404_absent_returns_absent(self):
        session = AsyncMock()
        service = DriverLifecycleService(session)

        delete_resp = MagicMock()
        delete_resp.status_code = 404
        delete_resp.text = '{"detail":"gone"}'
        delete_resp.json.return_value = {"detail": "gone"}

        list_resp = MagicMock()
        list_resp.raise_for_status = MagicMock()
        list_resp.json.return_value = {"drivers": [{"id": "other"}]}

        with patch("app.services.driver_lifecycle.httpx.AsyncClient") as client_cls:
            client = AsyncMock()
            client.__aenter__ = AsyncMock(return_value=client)
            client.__aexit__ = AsyncMock(return_value=None)
            client.delete = AsyncMock(return_value=delete_resp)
            client.get = AsyncMock(return_value=list_resp)
            client_cls.return_value = client
            result = await service._call_agent_uninstall("drv1", "token")
            self.assertEqual(result["status"], "absent")

    async def test_uninstall_cancelled_marks_unknown_and_clears_gate(self):
        """取消 Agent DELETE task：清理门闩并尽力写入 unknown，再重新抛出。"""
        import asyncio

        session = AsyncMock()
        service = DriverLifecycleService(session)
        session.execute = AsyncMock(
            side_effect=[
                _mapping_result(
                    [{"id": 1, "protocol": "ext", "description": "x", "port": 0}]
                ),
                _mapping_result([]),  # update unknown
            ]
        )
        session.commit = AsyncMock()
        session.rollback = AsyncMock()

        with patch.object(
            service,
            "_begin_uninstall_operation",
            new=AsyncMock(return_value="op-1"),
        ), patch.object(
            service,
            "_call_agent_uninstall",
            new=AsyncMock(side_effect=asyncio.CancelledError()),
        ), patch("app.services.driver_lifecycle._agent_token", return_value="tok"):
            with self.assertRaises(asyncio.CancelledError):
                await service.uninstall(protocol="ext", driver_id="drv1")

        from app.services import driver_lifecycle as mod

        with mod._operations_lock:
            self.assertIsNone(mod._operations.get("ext"))
        self.assertTrue(any("unknown" in str(c) for c in session.execute.await_args_list))

    async def test_uninstall_cancelled_during_db_execute_clears_gate(self):
        """Agent 已成功、DB DELETE 被取消：清门闩并补写 success/pending，不写 unknown。"""
        import asyncio

        session = AsyncMock()
        service = DriverLifecycleService(session)
        session.execute = AsyncMock(
            side_effect=[
                _mapping_result(
                    [{"id": 1, "protocol": "ext", "description": "x", "port": 0}]
                ),
                _mapping_result([]),  # agent success + db pending
                asyncio.CancelledError(),  # DELETE protocol 时取消
            ]
        )
        session.commit = AsyncMock()
        session.rollback = AsyncMock()

        mark_unknown = AsyncMock()
        mark_success = AsyncMock()
        with patch.object(
            service,
            "_begin_uninstall_operation",
            new=AsyncMock(return_value="op-1"),
        ), patch.object(
            service,
            "_call_agent_uninstall",
            new=AsyncMock(return_value={"httpStatus": 200, "data": {}}),
        ), patch.object(
            service,
            "_mark_operation_unknown_best_effort",
            new=mark_unknown,
        ), patch.object(
            service,
            "_mark_operation_success_pending_best_effort",
            new=mark_success,
        ), patch("app.services.driver_lifecycle._agent_token", return_value="tok"):
            with self.assertRaises(asyncio.CancelledError):
                await service.uninstall(protocol="ext", driver_id="drv1")

        from app.services import driver_lifecycle as mod

        with mod._operations_lock:
            self.assertIsNone(mod._operations.get("ext"))
        mark_unknown.assert_not_awaited()
        mark_success.assert_awaited()

    async def test_uninstall_cancelled_during_success_pending_update(self):
        """Agent 返回后、更新 success/pending 时取消：按 Agent 已成功补偿。"""
        import asyncio

        session = AsyncMock()
        service = DriverLifecycleService(session)
        session.execute = AsyncMock(
            side_effect=[
                _mapping_result(
                    [{"id": 1, "protocol": "ext", "description": "x", "port": 0}]
                ),
                asyncio.CancelledError(),  # update success/pending 取消
            ]
        )
        session.commit = AsyncMock()
        session.rollback = AsyncMock()
        mark_unknown = AsyncMock()
        mark_success = AsyncMock()

        with patch.object(
            service,
            "_begin_uninstall_operation",
            new=AsyncMock(return_value="op-1"),
        ), patch.object(
            service,
            "_call_agent_uninstall",
            new=AsyncMock(return_value={"ok": True}),
        ), patch.object(
            service,
            "_mark_operation_unknown_best_effort",
            new=mark_unknown,
        ), patch.object(
            service,
            "_mark_operation_success_pending_best_effort",
            new=mark_success,
        ), patch("app.services.driver_lifecycle._agent_token", return_value="tok"):
            with self.assertRaises(asyncio.CancelledError):
                await service.uninstall(protocol="ext", driver_id="drv1")

        mark_unknown.assert_not_awaited()
        mark_success.assert_awaited()

    async def test_uninstall_cancelled_during_first_commit_after_agent(self):
        """Agent 返回后首次 commit success/pending 取消：补写 success/pending。"""
        import asyncio

        session = AsyncMock()
        service = DriverLifecycleService(session)
        session.execute = AsyncMock(
            side_effect=[
                _mapping_result(
                    [{"id": 1, "protocol": "ext", "description": "x", "port": 0}]
                ),
                _mapping_result([]),  # update success/pending
            ]
        )
        session.commit = AsyncMock(side_effect=asyncio.CancelledError())
        session.rollback = AsyncMock()
        mark_unknown = AsyncMock()
        mark_success = AsyncMock()

        with patch.object(
            service,
            "_begin_uninstall_operation",
            new=AsyncMock(return_value="op-1"),
        ), patch.object(
            service,
            "_call_agent_uninstall",
            new=AsyncMock(return_value={"ok": True}),
        ), patch.object(
            service,
            "_mark_operation_unknown_best_effort",
            new=mark_unknown,
        ), patch.object(
            service,
            "_mark_operation_success_pending_best_effort",
            new=mark_success,
        ), patch("app.services.driver_lifecycle._agent_token", return_value="tok"):
            with self.assertRaises(asyncio.CancelledError):
                await service.uninstall(protocol="ext", driver_id="drv1")

        mark_unknown.assert_not_awaited()
        mark_success.assert_awaited()

    async def test_begin_uninstall_locks_protocol_and_persists_gate(self):
        session = AsyncMock()
        service = DriverLifecycleService(session)
        session.execute = AsyncMock(
            side_effect=[
                _mapping_result([]),
                _mapping_result(
                    [{"lifecycle_status": "active", "lifecycle_operation_id": None}]
                ),
                _mapping_result([{"cnt": 0}]),
                _mapping_result([]),
                _mapping_result([]),
            ]
        )

        operation_id = await service._begin_uninstall_operation(
            protocol="ext", protocol_id=1, driver_id="drv1"
        )

        self.assertTrue(operation_id)
        sql_calls = "\n".join(
            str(call.args[0]) for call in session.execute.await_args_list
        )
        self.assertIn("FOR UPDATE", sql_calls)
        self.assertIn("lifecycle_status = 'uninstalling'", sql_calls)
        self.assertNotIn("DELETE FROM driver_operation", sql_calls)
        session.commit.assert_awaited_once()

    async def test_begin_uninstall_rejects_bound_device_before_operation(self):
        session = AsyncMock()
        service = DriverLifecycleService(session)
        session.execute = AsyncMock(
            side_effect=[
                _mapping_result([]),
                _mapping_result(
                    [{"lifecycle_status": "active", "lifecycle_operation_id": None}]
                ),
                _mapping_result([{"cnt": 1}]),
            ]
        )

        with self.assertRaises(ApiError) as ctx:
            await service._begin_uninstall_operation(
                protocol="ext", protocol_id=1, driver_id="drv1"
            )

        self.assertEqual(ctx.exception.code, 409)
        session.rollback.assert_awaited_once()
        session.commit.assert_not_awaited()

    async def test_cleanup_limits_batches_per_run(self):
        session = AsyncMock()
        session.execute = AsyncMock(return_value=_rowcount_result(1000))
        service = DriverLifecycleService(session)

        deleted = await service._purge_terminal_operations(max_batches=2)

        self.assertEqual(deleted, 2000)
        self.assertEqual(session.execute.await_count, 2)
        self.assertEqual(session.commit.await_count, 2)


class TestDriverLifecycleReconcile(unittest.IsolatedAsyncioTestCase):
    async def test_reconcile_finalizes_failed_pending(self):
        session = AsyncMock()
        service = DriverLifecycleService(session)
        pending_row = {
            "operation_id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "driver_id": "drv1",
            "operation_type": "uninstall",
            "target_version": None,
            "agent_phase": "failed",
            "db_phase": "pending",
        }
        session.execute = AsyncMock(
            side_effect=[
                _rowcount_result(0),  # purge committed operations
                _mapping_result([]),  # installed_driver orphans
                _mapping_result([pending_row]),  # SELECT pending
                _mapping_result([]),  # release persistent lifecycle gate
                _mapping_result([]),  # update failed/failed terminal phase
            ]
        )
        session.commit = AsyncMock()

        stats = await service.reconcile()
        self.assertEqual(stats["cleaned"], 0)
        self.assertEqual(stats["retained"], 1)
        self.assertEqual(stats["retried"], 0)
        session.commit.assert_awaited()
        sql_calls = "\n".join(
            str(call.args[0]) for call in session.execute.await_args_list
        )
        self.assertIn("db_phase = :db_phase", sql_calls)
        phase_call = session.execute.await_args_list[-1]
        self.assertEqual(phase_call.args[1]["agent_phase"], "failed")
        self.assertEqual(phase_call.args[1]["db_phase"], "failed")

    async def test_cleanup_only_targets_old_terminal_operations(self):
        from app.services import driver_lifecycle as lifecycle_module

        session = AsyncMock()
        session.execute = AsyncMock(return_value=_rowcount_result(2))
        service = DriverLifecycleService(session)

        deleted = await service._purge_terminal_operations()

        self.assertEqual(deleted, 2)
        self.assertEqual(lifecycle_module._DRIVER_OPERATION_RETENTION_DAYS, 90)
        sql = str(session.execute.await_args.args[0])
        self.assertIn("agent_phase = 'success'", sql)
        self.assertIn("db_phase = 'committed'", sql)
        self.assertIn("agent_phase = 'failed'", sql)
        self.assertIn("db_phase = 'failed'", sql)
        self.assertIn("updated_at < :cutoff", sql)

    async def test_reconcile_reports_installed_driver_orphans_without_mutation(self):
        session = AsyncMock()
        session.execute = AsyncMock(
            side_effect=[
                _rowcount_result(0),
                _mapping_result([{"protocol": "lost_proto", "driver_id": "drv1"}]),
                _mapping_result([]),
            ]
        )
        service = DriverLifecycleService(session)

        with self.assertLogs("app.services.driver_lifecycle", level="ERROR") as logs:
            stats = await service.reconcile()

        self.assertEqual(stats["orphans"], 1)
        self.assertTrue(any("lost_proto" in entry for entry in logs.output))
        sql_calls = "\n".join(
            str(call.args[0]) for call in session.execute.await_args_list
        )
        self.assertIn("LEFT JOIN comm_protocol", sql_calls)
        self.assertNotIn("DELETE FROM installed_driver", sql_calls)

    async def test_reconcile_retries_install_success_pending(self):
        session = AsyncMock()
        service = DriverLifecycleService(session)
        pending_row = {
            "operation_id": "11111111-2222-3333-4444-555555555555",
            "driver_id": "ext_drv",
            "operation_type": "install",
            "target_version": "1.0.0",
            "agent_phase": "success",
            "db_phase": "pending",
        }

        session.execute = AsyncMock(
            side_effect=[
                _rowcount_result(0),
                _mapping_result([]),
                _mapping_result([pending_row]),  # SELECT
                _mapping_result([]),  # fetch protocol
                _mapping_result([{"max_id": 3}]),
                _mapping_result([]),  # insert protocol
                _mapping_result([]),  # upsert installed
                _mapping_result([]),  # update phases
            ]
        )
        session.commit = AsyncMock()
        session.rollback = AsyncMock()

        with patch.object(
            service,
            "_fetch_agent_driver",
            new=AsyncMock(
                return_value={
                    "id": "ext_drv",
                    "protocol": "ext_proto",
                    "name": "Ext",
                    "description": "Ext Driver",
                    "version": "1.0.0",
                    "sha256": "a" * 64,
                }
            ),
        ):
            stats = await service.reconcile()

        self.assertEqual(stats["retried"], 1)
        self.assertEqual(stats["errors"], 0)

    async def test_reconcile_retries_uninstall_db_pending(self):
        session = AsyncMock()
        service = DriverLifecycleService(session)
        pending_row = {
            "operation_id": "99999999-8888-7777-6666-555555555555",
            "driver_id": "ext_drv",
            "operation_type": "uninstall",
            "target_version": None,
            "agent_phase": "success",
            "db_phase": "pending",
        }

        session.execute = AsyncMock(
            side_effect=[
                _rowcount_result(0),
                _mapping_result([]),
                _mapping_result([pending_row]),
                _mapping_result(
                    [{"protocol": "ext_proto", "driver_id": "ext_drv", "driver_version": "1", "driver_sha256": None}]
                ),
                _mapping_result(
                    [{"id": 9, "protocol": "ext_proto", "description": "x", "port": 0}]
                ),
                _mapping_result([]),  # DELETE protocol
                _mapping_result([]),  # DELETE installed
                _mapping_result([]),  # update phases
            ]
        )
        session.commit = AsyncMock()

        stats = await service.reconcile()
        self.assertEqual(stats["retried"], 1)

    async def test_reconcile_pending_pending_uninstall_when_agent_absent(self):
        """pending+pending：Agent 已无驱动 → 标 success 并补删 MySQL。"""
        session = AsyncMock()
        service = DriverLifecycleService(session)
        pending_row = {
            "operation_id": "aaaaaaaa-bbbb-cccc-dddd-ffffffffffff",
            "driver_id": "ext_drv",
            "operation_type": "uninstall",
            "target_version": None,
            "agent_phase": "pending",
            "db_phase": "pending",
        }
        session.execute = AsyncMock(
            side_effect=[
                _rowcount_result(0),
                _mapping_result([]),
                _mapping_result([pending_row]),  # SELECT
                _mapping_result([]),  # update success/pending
                _mapping_result(
                    [{"protocol": "ext_proto", "driver_id": "ext_drv", "driver_version": "1", "driver_sha256": None}]
                ),
                _mapping_result(
                    [{"id": 9, "protocol": "ext_proto", "description": "x", "port": 0}]
                ),
                _mapping_result([]),  # DELETE protocol
                _mapping_result([]),  # DELETE installed
                _mapping_result([]),  # update committed
            ]
        )
        session.commit = AsyncMock()

        with patch.object(
            service, "_probe_agent_driver", new=AsyncMock(return_value="absent")
        ), patch("app.services.driver_lifecycle._agent_token", return_value="tok"):
            stats = await service.reconcile()

        self.assertEqual(stats["retried"], 1)
        self.assertEqual(stats["skipped"], 0)

    async def test_reconcile_unknown_keeps_operation_when_agent_unreachable(self):
        """reconcile 时 Agent 不可达：必须保留 operation，不能清理。"""
        session = AsyncMock()
        service = DriverLifecycleService(session)
        pending_row = {
            "operation_id": "bbbbbbbb-cccc-dddd-eeee-ffffffffffff",
            "driver_id": "ext_drv",
            "operation_type": "uninstall",
            "target_version": None,
            "agent_phase": "unknown",
            "db_phase": "pending",
        }
        session.execute = AsyncMock(
            side_effect=[
                _rowcount_result(0),
                _mapping_result([]),
                _mapping_result([pending_row]),  # SELECT
            ]
        )
        session.commit = AsyncMock()

        with patch.object(
            service, "_probe_agent_driver", new=AsyncMock(return_value="unreachable")
        ), patch("app.services.driver_lifecycle._agent_token", return_value="tok"):
            stats = await service.reconcile()

        self.assertEqual(stats["skipped"], 1)
        self.assertEqual(stats["cleaned"], 0)
        self.assertEqual(stats["retried"], 0)


if __name__ == "__main__":
    unittest.main()
