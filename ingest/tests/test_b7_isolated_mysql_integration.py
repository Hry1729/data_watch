"""B7｜隔离 MySQL 集成验证（真实 aiomysql 连接 + 真实表 + 真实提交）。

只在显式开启且目标确认为隔离实例时运行：
    $env:INGEST_B7_ISOLATED_MYSQL='1'
    $env:MYSQL_HOST='127.0.0.1'; $env:MYSQL_PORT='13306'
    $env:MYSQL_USER='root'; $env:MYSQL_PASSWORD='TESTONLY_sentinel_root_pw'
    $env:MYSQL_DATABASE='device_watch_test'

护栏（见 docs/整改记录/隔离测试环境.md §5.2：仓库内没有 MySQL 侧守卫）：
- 未显式开启 → skip，绝不触碰默认的 127.0.0.1:3306 / device_watch。
- 已开启但目标不是 13306 + device_watch_test + TESTONLY_ 哨兵口令 → 直接失败。
本用例只读写自建的 B7 测试行，结束时全部清理。
"""

from __future__ import annotations

import os
from datetime import datetime, timezone
from types import SimpleNamespace
from uuid import uuid4

import pymysql
import pytest

from ingest.app.repository import MySQLIngestRepository
from ingest.app.schemas import UploadItem

_OPT_IN_ENV = "INGEST_B7_ISOLATED_MYSQL"
_SENTINEL_HOST = "127.0.0.1"
_SENTINEL_PORT = 13306
_SENTINEL_DATABASE = "device_watch_test"
_SENTINEL_PASSWORD_PREFIX = "TESTONLY_"

_NODE_UUID = "b7b7b7b7-0000-4000-8000-00000000b7b7"
_DEVICE_CODE = "B7ITQC100"
_DEVICE_TYPE_CODE = "b7itqc"
_DEVICE_GROUP_CODE = "B7ITGRP"
_PROTOCOL = "b7itproto"
_GROUP_CODE = "g901"
_POINT_ID_EXISTING = 990000001
_POINT_ID_NEW = 990000002
_POINT_ID_NEVER_MAPPED = 990000099


def _settings() -> SimpleNamespace:
    if os.environ.get(_OPT_IN_ENV) != "1":
        pytest.skip(
            f"隔离集成用例已跳过：设置 {_OPT_IN_ENV}=1 并指向 13306/device_watch_test 后重跑"
        )
    host = os.environ.get("MYSQL_HOST", "")
    port = int(os.environ.get("MYSQL_PORT") or 0)
    database = os.environ.get("MYSQL_DATABASE", "")
    password = os.environ.get("MYSQL_PASSWORD", "")
    if host != _SENTINEL_HOST or port != _SENTINEL_PORT or database != _SENTINEL_DATABASE:
        raise RuntimeError(
            "拒绝运行：目标不是隔离测试实例 "
            f"({_SENTINEL_HOST}:{_SENTINEL_PORT}/{_SENTINEL_DATABASE})，"
            f"实际 {host}:{port}/{database}"
        )
    if not password.startswith(_SENTINEL_PASSWORD_PREFIX):
        raise RuntimeError("拒绝运行：口令不是 TESTONLY_ 哨兵值")
    return SimpleNamespace(
        mysql_host=host,
        mysql_port=port,
        mysql_user=os.environ.get("MYSQL_USER", ""),
        mysql_password=password,
        mysql_database=database,
        mysql_pool_maxsize=2,
        control_cache_ttl_ms=3_600_000,
    )


def _connect() -> pymysql.connections.Connection:
    return pymysql.connect(
        host=os.environ["MYSQL_HOST"],
        port=int(os.environ["MYSQL_PORT"]),
        user=os.environ["MYSQL_USER"],
        password=os.environ["MYSQL_PASSWORD"],
        database=os.environ["MYSQL_DATABASE"],
        charset="utf8mb4",
        autocommit=True,
    )


def _cleanup(connection: pymysql.connections.Connection) -> None:
    with connection.cursor() as cursor:
        cursor.execute(
            "DELETE FROM point_storage_mapping WHERE device_code = %s", (_DEVICE_CODE,)
        )
        cursor.execute(
            "DELETE da FROM device_assignment da JOIN device d ON d.id = da.device_id "
            "WHERE d.device_code = %s",
            (_DEVICE_CODE,),
        )
        cursor.execute(
            "DELETE dpg FROM device_point_group dpg JOIN device d ON d.id = dpg.device_id "
            "WHERE d.device_code = %s",
            (_DEVICE_CODE,),
        )
        cursor.execute(
            "DELETE dv FROM device_variable dv JOIN device d ON d.id = dv.device_id "
            "WHERE d.device_code = %s",
            (_DEVICE_CODE,),
        )
        cursor.execute("DELETE FROM device WHERE device_code = %s", (_DEVICE_CODE,))
        cursor.execute("DELETE FROM device_type WHERE type_code = %s", (_DEVICE_TYPE_CODE,))
        cursor.execute("DELETE FROM sys_node WHERE node_uuid = %s", (_NODE_UUID,))
        cursor.execute("DELETE FROM device_group WHERE group_code = %s", (_DEVICE_GROUP_CODE,))
        cursor.execute("DELETE FROM comm_protocol WHERE protocol = %s", (_PROTOCOL,))


def _seed(connection: pymysql.connections.Connection) -> None:
    now = datetime.now(timezone.utc).replace(tzinfo=None)
    with connection.cursor() as cursor:
        cursor.execute(
            "INSERT INTO sys_node (node_uuid, node_name, status) VALUES (%s, %s, 'approved')",
            (_NODE_UUID, "B7 isolated integration"),
        )
        cursor.execute(
            "INSERT INTO device_type (type_code, type_name, create_time) VALUES (%s, %s, %s)",
            (_DEVICE_TYPE_CODE, "B7 isolated type", now),
        )
        cursor.execute(
            "INSERT INTO device_group (id, group_code, group_name, create_time) "
            "VALUES (%s, %s, %s, %s)",
            (990001, _DEVICE_GROUP_CODE, "B7 group", now),
        )
        cursor.execute(
            "INSERT INTO comm_protocol (id, protocol, create_time) VALUES (%s, %s, %s)",
            (990001, _PROTOCOL, now),
        )
        cursor.execute(
            "INSERT INTO device (device_code, device_name, type_id, group_id, enable, status, "
            "protocol_id, endpoint, rack, slot, scan_cycle, create_time) "
            "SELECT %s, %s, dt.id, 990001, 1, 0, 990001, '127.0.0.1', 0, 1, 1000, %s "
            "FROM device_type dt WHERE dt.type_code = %s",
            (_DEVICE_CODE, "B7 device", now, _DEVICE_TYPE_CODE),
        )
        cursor.execute(
            "INSERT INTO device_point_group (device_id, group_no, group_code, status, config_version) "
            "SELECT d.id, 1, %s, 'active', 1 FROM device d WHERE d.device_code = %s",
            (_GROUP_CODE, _DEVICE_CODE),
        )
        cursor.execute(
            "INSERT INTO device_assignment "
            "(device_id, node_uuid, assignment_epoch, effective_at, cutover_at, grace_until, status) "
            "SELECT d.id, %s, 1, %s, NULL, NULL, 'active' FROM device d WHERE d.device_code = %s",
            (_NODE_UUID, now, _DEVICE_CODE),
        )
        cursor.execute(
            "INSERT INTO point_storage_mapping "
            "(point_id, device_code, device_type_code, stable_name, child_table, group_code, "
            "td_column_name, var_type) VALUES (%s, %s, %s, %s, %s, %s, %s, %s)",
            (
                _POINT_ID_EXISTING,
                _DEVICE_CODE,
                _DEVICE_TYPE_CODE,
                f"sd{_DEVICE_TYPE_CODE}",
                _DEVICE_CODE,
                _GROUP_CODE,
                "b7col1",
                "REAL",
            ),
        )


def _item(seq: int, *, point_id: int, config_version: int) -> UploadItem:
    return UploadItem(
        batch_id=str(uuid4()),
        upload_session_id=str(uuid4()),
        local_seq=seq,
        assignment_epoch=1,
        device_code=_DEVICE_CODE,
        device_type=_DEVICE_TYPE_CODE,
        group_code=_GROUP_CODE,
        scan_rate=100,
        ts=int(datetime.now(timezone.utc).timestamp() * 1000),
        config_version=config_version,
        values={str(point_id): "12.5"},
    )


class _ReadCounter:
    def __init__(self) -> None:
        self.assignment = 0
        self.mapping = 0

    def wrap(self, repository: MySQLIngestRepository) -> None:
        real_fetchall = repository._fetchall

        async def counting_fetchall(query: str, params: tuple) -> list[dict]:
            if "FROM device d" in query:
                self.assignment += 1
            else:
                self.mapping += 1
            return await real_fetchall(query, params)

        repository._fetchall = counting_fetchall  # type: ignore[method-assign]


@pytest.mark.asyncio
async def test_real_mysql_commit_behind_stale_cache_is_not_permanently_rejected() -> None:
    settings = _settings()
    connection = _connect()
    repository = None
    try:
        _cleanup(connection)
        _seed(connection)

        repository = await MySQLIngestRepository.create(settings, migrate_schema=False)
        counter = _ReadCounter()
        counter.wrap(repository)

        # 1) 暖缓存：旧版本、旧映射，合法帧被接受
        warm = await repository.validate_items(
            _NODE_UUID, [_item(1, point_id=_POINT_ID_EXISTING, config_version=1)]
        )
        assert [decision.action for decision in warm] == ["accept"]
        assert (counter.assignment, counter.mapping) == (1, 1)

        # 2) 控制面真实提交：新点位 + config_version 前移（且故意不发任何失效通知）
        with connection.cursor() as cursor:
            cursor.execute(
                "INSERT INTO point_storage_mapping "
                "(point_id, device_code, device_type_code, stable_name, child_table, group_code, "
                "td_column_name, var_type) VALUES (%s, %s, %s, %s, %s, %s, %s, %s)",
                (
                    _POINT_ID_NEW,
                    _DEVICE_CODE,
                    _DEVICE_TYPE_CODE,
                    f"sd{_DEVICE_TYPE_CODE}",
                    _DEVICE_CODE,
                    _GROUP_CODE,
                    "b7col2",
                    "REAL",
                ),
            )
            cursor.execute(
                "UPDATE device_point_group dpg JOIN device d ON d.id = dpg.device_id "
                "SET dpg.config_version = 2 WHERE d.device_code = %s AND dpg.group_code = %s",
                (_DEVICE_CODE, _GROUP_CODE),
            )

        # 3) 缓存仍旧，但合法帧不得被永久拒收
        fresh = await repository.validate_items(
            _NODE_UUID, [_item(2, point_id=_POINT_ID_NEW, config_version=2)]
        )
        assert [decision.action for decision in fresh] == ["accept"]
        assert (counter.assignment, counter.mapping) == (2, 2)

        # 4) 真正非法的点位（库内从未存在）仍必须永久拒绝
        illegal = await repository.validate_items(
            _NODE_UUID, [_item(3, point_id=_POINT_ID_NEVER_MAPPED, config_version=2)]
        )
        assert illegal[0].action == "consume_reject"
        assert illegal[0].status_override == "rejected_consumed"
        assert illegal[0].reason == "malformed_envelope:unknown_var_id"

        # 5) 通知正常送达时不应产生额外权威重读
        with connection.cursor() as cursor:
            cursor.execute(
                "UPDATE device_point_group dpg JOIN device d ON d.id = dpg.device_id "
                "SET dpg.config_version = 3 WHERE d.device_code = %s AND dpg.group_code = %s",
                (_DEVICE_CODE, _GROUP_CODE),
            )
        repository.invalidate_control_cache({"scope": "assignments"})
        repository.invalidate_control_cache({"scope": "mappings"})
        reads_before = (counter.assignment, counter.mapping)
        notified = await repository.validate_items(
            _NODE_UUID, [_item(4, point_id=_POINT_ID_NEW, config_version=3)]
        )
        assert [decision.action for decision in notified] == ["accept"]
        # 通知送达 → 缓存已被清空，只需一次正常的 miss 读库；不得再叠加一次权威重读
        assert (counter.assignment, counter.mapping) == (
            reads_before[0] + 1,
            reads_before[1] + 1,
        )

        # 6) 数据守恒：Ingest 校验路径不得写入任何业务行
        with connection.cursor() as cursor:
            cursor.execute(
                "SELECT COUNT(*) FROM point_storage_mapping WHERE device_code = %s",
                (_DEVICE_CODE,),
            )
            assert cursor.fetchone()[0] == 2
    finally:
        if repository is not None:
            await repository.close()
        _cleanup(connection)
        connection.close()
