"""基于 MySQL 的变量映射缓存。MySQL-backed variable mapping cache.

Maps (device_code, group_code) → GroupMapping which contains:
  - config_version: int
  - child_table: str   e.g. qc100
  - stable: str        e.g. sdqc
  - var_map: dict[var_id → td_column_name]
  - var_type: dict[var_id → var_type_str]

The permanent mapping is retained for decoding historical config versions.
The runtime cache can be explicitly reloaded when a new point is observed.
"""

from __future__ import annotations

import logging
from dataclasses import dataclass, field

import pymysql
import pymysql.cursors

logger = logging.getLogger("writer.mysql_mapper")

_SQL = """
SELECT point_id AS var_id, td_column_name, var_type,
       group_code, device_code, device_type_code,
       stable_name, child_table
FROM point_storage_mapping
WHERE device_code = %s AND group_code = %s
"""


@dataclass
class GroupMapping:
    config_version: int
    child_table: str
    stable: str
    # var_map: var_id → td_column_name。
    # **发布后不得原地修改**：`load()` 每次都新建 dict，`reload()` 整体替换缓存对象。
    # writer/consumer.py 的快路径用 `conversion_var_map is mapping.var_map` 判断
    # "已算好的转换结果是否还对得上当前映射"，一旦允许在已发布的 dict 上 clear()/update()，
    # 身份判断就会失效（旧转换结果会被误判为仍然有效，导致漏点或错列）。
    var_map: dict[str, str] = field(default_factory=dict)   # var_id → td_column_name
    var_type: dict[str, str] = field(default_factory=dict)  # var_id → var_type


class MysqlMapper:
    """组映射数据类和映射器类定义。Synchronous pymysql-backed mapping cache."""

    def __init__(self, mysql_conn: pymysql.connections.Connection) -> None:
        self._conn = mysql_conn
        self._cache: dict[str, GroupMapping] = {}  # key: "device_code:group_code"

    def get_cached(self, device_code: str, group_code: str) -> GroupMapping | None:
        return self._cache.get(f"{device_code}:{group_code}")


    def get_or_load(
        self,
        device_code: str,
        group_code: str,
        stream_config_version: int,
    ) -> GroupMapping | None:
        """返回永久存储映射；stream_config_version 仅保留作接口兼容和日志上下文。"""
        key = f"{device_code}:{group_code}"
        cached = self.get_cached(device_code, group_code)
        if cached is not None:
            return cached
        mapping = self.load(device_code, group_code)
        if mapping is None:
            return None
        mapping.config_version = stream_config_version
        self._cache[key] = mapping
        return mapping

    def reload(
        self,
        device_code: str,
        group_code: str,
        stream_config_version: int,
    ) -> GroupMapping | None:
        """强制从永久 mapping 重载一个设备/分组。"""
        key = f"{device_code}:{group_code}"
        mapping = self.load(device_code, group_code)
        if mapping is None:
            return None
        # 版本只用于日志上下文；永久 mapping 仍负责旧 entry 的解码。
        mapping.config_version = stream_config_version
        self._cache[key] = mapping
        return mapping

    def load(self, device_code: str, group_code: str) -> GroupMapping | None:
        """查询 MySQL 并构建 GroupMapping。出错时返回 None。"""
        try:
            self._ensure_connected()
            with self._conn.cursor(pymysql.cursors.DictCursor) as cur:
                cur.execute(_SQL, (device_code, group_code))
                rows = cur.fetchall()

            if not rows:
                logger.warning(
                    "[mapper] no variables for device=%s group=%s", device_code, group_code
                )
                return None

            first = rows[0]
            device_type = str(first["device_type_code"] or "").lower()
            child_table = str(first["child_table"] or device_code)
            stable = str(first["stable_name"] or f"sd{device_type}")

            var_map: dict[str, str] = {}
            var_type: dict[str, str] = {}
            for row in rows:
                var_id = str(row["var_id"])
                col = row["td_column_name"]
                vtype = row["var_type"] or ""
                if col:
                    var_map[var_id] = col
                var_type[var_id] = vtype

            return GroupMapping(
                config_version=0,
                child_table=child_table,
                stable=stable,
                var_map=var_map,
                var_type=var_type,
            )
        except Exception as exc:
            logger.error(
                "[mapper] load failed device=%s group=%s: %s", device_code, group_code, exc
            )
            return None

    def _ensure_connected(self) -> None:
        """如果 MySQL 连接断开则 ping 并重新连接。"""
        try:
            self._conn.ping(reconnect=True)
        except Exception as exc:
            logger.warning("[mapper] MySQL ping/reconnect failed: %s", exc)
            raise
