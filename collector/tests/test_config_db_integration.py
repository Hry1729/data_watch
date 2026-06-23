"""Config DB integration smoke tests against a real MySQL database.

Default test runs skip this file. To execute:

    $env:RUN_DB_INTEGRATION = "1"
    $env:DATAWATCH_MYSQL_HOST = "127.0.0.1"
    $env:DATAWATCH_MYSQL_PORT = "3306"
    $env:DATAWATCH_MYSQL_USER = "root"
    $env:DATAWATCH_MYSQL_PASSWORD = "***"
    $env:DATAWATCH_MYSQL_DATABASE = "device_watch"
    pytest tests/test_config_db_integration.py -m integration -v

These tests pin the real SQL contract after migrating ConfigDBManager from
SQLAlchemy to aiomysql: aliases, LEFT JOINs, JSON columns, the misspelled
plc_connection_log.messsage column, and explicit transactions.

Caution: run against a test/staging database or with the collector stopped.
update_device_status writes a system_log row and the cleanup deletes recent
collector.device rows matching "integration_test"; if a live collector is
writing logs for the same device in the same window, those rows could be
deleted too. Do not run against a busy production database.
"""

from __future__ import annotations

import json
import os
import uuid
from contextlib import asynccontextmanager

import pytest

from core.config_db import ConfigDBManager, DatabaseConfig, DeviceSignature


pytestmark = [
    pytest.mark.integration,
    pytest.mark.asyncio,
    pytest.mark.skipif(
        os.getenv("RUN_DB_INTEGRATION") != "1",
        reason="requires RUN_DB_INTEGRATION=1 and a real MySQL database",
    ),
]


def _db_config_from_env() -> DatabaseConfig:
    return DatabaseConfig(
        host=os.getenv("DATAWATCH_MYSQL_HOST", "127.0.0.1"),
        port=int(os.getenv("DATAWATCH_MYSQL_PORT", "3306")),
        user=os.getenv("DATAWATCH_MYSQL_USER", "root"),
        password=os.getenv("DATAWATCH_MYSQL_PASSWORD", ""),
        database=os.getenv("DATAWATCH_MYSQL_DATABASE", "device_watch"),
        charset=os.getenv("DATAWATCH_MYSQL_CHARSET", "utf8mb4"),
    )


@asynccontextmanager
async def _manager():
    manager = ConfigDBManager(_db_config_from_env())
    try:
        yield manager
    finally:
        await manager.close()


async def test_load_devices_variables_groups_protocols():
    """Device/protocol/type and variable/group joins load with real SQL aliases."""
    async with _manager() as manager:
        devices, variables = await manager.load_devices_and_variables()

        assert isinstance(devices, list)
        assert isinstance(variables, list)
        if not devices:
            pytest.skip("no enabled device rows available")

        for device in devices:
            assert device.device_id
            assert device.protocol
            assert device.device_pk and device.device_pk > 0

        device_ids = {device.device_id for device in devices}
        for variable in variables:
            assert variable.device_id in device_ids
            assert variable.group_code


async def test_get_device_config_versions_shape():
    """Runtime reload polling signature uses real device columns and aliases."""
    async with _manager() as manager:
        versions = await manager.get_device_config_versions()

        assert isinstance(versions, dict)
        for device_code, signature in versions.items():
            assert device_code
            assert isinstance(signature, DeviceSignature)
            assert isinstance(signature.config_version, int)
            assert isinstance(signature.endpoint, str)
            assert isinstance(signature.protocol_id, int)
            assert isinstance(signature.rack, int)
            assert isinstance(signature.slot, int)
            assert isinstance(signature.scan_cycle, int)


async def test_write_system_log_chinese_json_roundtrip_and_cleanup():
    """system_log JSON column stores and reads Chinese keys/values safely."""
    trace_id = f"itest-{uuid.uuid4().hex}"

    async with _manager() as manager:
        ok = await manager.write_system_log(
            level="INFO",
            log_type="test",
            source="db.integration.smoke",
            content="集成冒烟中文内容",
            detail_json={"事件": "上线", "值": 123},
            trace_id=trace_id,
        )
        try:
            assert ok is True

            row = await manager._fetchone(
                """
                SELECT content AS content, detail_json AS detail_json
                FROM system_log
                WHERE trace_id = %s
                LIMIT 1
                """,
                (trace_id,),
            )
            assert row is not None
            assert row["content"] == "集成冒烟中文内容"

            detail = row["detail_json"]
            if isinstance(detail, (str, bytes, bytearray)):
                detail = json.loads(detail)
            assert detail == {"事件": "上线", "值": 123}
        finally:
            await manager._execute("DELETE FROM system_log WHERE trace_id = %s", (trace_id,))

        gone = await manager._fetchone(
            "SELECT 1 AS x FROM system_log WHERE trace_id = %s LIMIT 1",
            (trace_id,),
        )
        assert gone is None


async def test_update_device_status_atomic_and_cleanup():
    """update_device_status exercises select/update/insert and cleans up its rows."""
    async with _manager() as manager:
        versions = await manager.get_device_config_versions()
        if not versions:
            pytest.skip("no enabled device rows available")

        device_code = next(iter(versions))
        before = await manager._fetchone(
            """
            SELECT id AS id, status AS status, update_time AS update_time
            FROM device
            WHERE device_code = %s
            LIMIT 1
            """,
            (device_code,),
        )
        assert before is not None

        before_log_id = (
            await manager._fetchone("SELECT COALESCE(MAX(id), 0) AS max_id FROM plc_connection_log")
        )["max_id"]
        before_sys_id = (
            await manager._fetchone("SELECT COALESCE(MAX(id), 0) AS max_id FROM system_log")
        )["max_id"]

        try:
            await manager.update_device_status(
                device_code,
                status=int(before["status"]),
                message="db integration smoke",
                event_type="integration_test",
            )

            log_row = await manager._fetchone(
                """
                SELECT event_type AS event_type
                FROM plc_connection_log
                WHERE device_id = %s AND id > %s
                ORDER BY id DESC
                LIMIT 1
                """,
                (before["id"], before_log_id),
            )
            assert log_row is not None
            assert log_row["event_type"] == "integration_test"
        finally:
            await manager._execute(
                "DELETE FROM plc_connection_log WHERE id > %s AND event_type = %s",
                (before_log_id, "integration_test"),
            )
            await manager._execute(
                """
                DELETE FROM system_log
                WHERE id > %s
                  AND source = %s
                  AND device_code = %s
                  AND content LIKE %s
                """,
                (before_sys_id, "collector.device", device_code, "%integration_test%"),
            )
            await manager._execute(
                "UPDATE device SET status = %s, update_time = %s WHERE id = %s",
                (int(before["status"]), before["update_time"], before["id"]),
            )
