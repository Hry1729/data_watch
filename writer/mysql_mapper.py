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

import time

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

# B29：映射缺失时的**短期负缓存窗口**。持续缺映射时，同一 (device, group) 会被**每一帧**
# 触发一次 reload（PEL 里的条目要一直留着等映射补齐），把 MySQL 请求放大成无界。
# 在该窗口内直接返回 None、**不再查库**，使查询次数变成"每 key 每窗口至多一次"；
# 窗口取小值以保证映射补齐后**能自愈**（另提供 invalidate() 供失效事件快速失效）。
NEGATIVE_CACHE_TTL_S = 5.0


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
        # B29：负缓存（key → 到期时刻，monotonic）。仅在**缺失/查询失败**时写入，
        # 成功加载即清除；窗口内不查库，避免持续缺映射时的请求放大。
        self._negative_until: dict[str, float] = {}
        self._reload_until: dict[str, float] = {}
        self._negative_cache_ttl_s = NEGATIVE_CACHE_TTL_S

    def _negative_active(self, key: str) -> bool:
        deadline = self._negative_until.get(key)
        if deadline is None:
            return False
        if deadline <= time.monotonic():
            self._negative_until.pop(key, None)  # 过期即自愈，不残留
            return False
        return True

    def _mark_negative(self, key: str) -> None:
        self._negative_until[key] = time.monotonic() + self._negative_cache_ttl_s

    def invalidate(self, device_code: str | None = None, group_code: str | None = None) -> None:
        """快速失效正/负缓存（B29）。

        收到"映射已补齐/控制面已变更"的失效事件时调用，避免新点被陈旧负缓存屏蔽。
        不传参数即整体清空。
        """
        if device_code is None or group_code is None:
            self._cache.clear()
            self._negative_until.clear()
            self._reload_until.clear()
            return
        key = f"{device_code}:{group_code}"
        self._cache.pop(key, None)
        self._negative_until.pop(key, None)
        self._reload_until.pop(key, None)

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
        if self._negative_active(key):  # B29：窗口内不重复查库
            return None
        mapping = self.load(device_code, group_code)
        if mapping is None:
            self._mark_negative(key)
            return None
        self._negative_until.pop(key, None)
        mapping.config_version = stream_config_version
        self._cache[key] = mapping
        return mapping

    def reload(
        self,
        device_code: str,
        group_code: str,
        stream_config_version: int,
        missing_points: bool = False,
    ) -> GroupMapping | None:
        """强制从永久 mapping 重载一个设备/分组。"""
        key = f"{device_code}:{group_code}"
        if missing_points:
            now = time.monotonic()
            if self._reload_until.get(key, 0) > now:
                return self.get_cached(device_code, group_code)
            self._reload_until[key] = now + self._negative_cache_ttl_s
        else:
            self._reload_until.pop(key, None)
        # B29：这是**高频**路径（未知 var_id 每帧都会走到这里）。持续缺映射时若不设窗口，
        # 每一帧都会查一次库；窗口内直接返回 None，条目继续留在 PEL 等映射补齐。
        if self._negative_active(key):
            return None
        mapping = self.load(device_code, group_code)
        if mapping is None:
            self._mark_negative(key)
            return None
        self._negative_until.pop(key, None)
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
