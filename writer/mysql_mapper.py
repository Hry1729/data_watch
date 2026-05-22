"""MySQL-backed variable mapping cache.

Maps (device_code, group_code) → GroupMapping which contains:
  - config_version: int
  - child_table: str   e.g. qc100
  - stable: str        e.g. sdqc
  - var_map: dict[var_id → td_column_name]
  - var_type: dict[var_id → var_type_str]

Cache is invalidated when the stream's config_version changes.
"""

from __future__ import annotations

import logging
from dataclasses import dataclass, field

import pymysql
import pymysql.cursors

logger = logging.getLogger("writer.mysql_mapper")

_SQL = """
SELECT dv.id AS var_id, dv.td_column_name, dv.var_type,
       dpg.config_version, dpg.group_code,
       d.device_code, dt.type_code AS device_type_code
FROM device_variable dv
JOIN device_point_group dpg ON dv.point_group_id = dpg.id
JOIN device d ON dpg.device_id = d.id
JOIN device_type dt ON dt.id = d.type_id
WHERE d.device_code = %s AND dpg.group_code = %s
  AND dv.deleted = 0 AND dv.enable = 1
"""


@dataclass
class GroupMapping:
    config_version: int
    child_table: str
    stable: str
    var_map: dict[str, str] = field(default_factory=dict)   # var_id → td_column_name
    var_type: dict[str, str] = field(default_factory=dict)  # var_id → var_type


class MysqlMapper:
    """Synchronous pymysql-backed mapping cache."""

    def __init__(self, mysql_conn: pymysql.connections.Connection) -> None:
        self._conn = mysql_conn
        self._cache: dict[str, GroupMapping] = {}  # key: "device_code:group_code"

    def get_or_load(
        self,
        device_code: str,
        group_code: str,
        stream_config_version: int,
    ) -> GroupMapping | None:
        """Return cached mapping if config_version matches, otherwise reload."""
        key = f"{device_code}:{group_code}"
        cached = self._cache.get(key)
        if cached is not None and cached.config_version == stream_config_version:
            return cached
        mapping = self.load(device_code, group_code)
        if mapping is None:
            return None
        if mapping.config_version != stream_config_version:
            logger.warning(
                "[mapper] config_version mismatch device=%s group=%s stream=%s mysql=%s",
                device_code,
                group_code,
                stream_config_version,
                mapping.config_version,
            )
            return None
        self._cache[key] = mapping
        return mapping

    def load(self, device_code: str, group_code: str) -> GroupMapping | None:
        """Query MySQL and build a GroupMapping. Returns None on error."""
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
            config_version = int(first["config_version"] or 0)
            device_type = str(first["device_type_code"] or "").lower()
            child_table = device_code
            stable = f"sd{device_type.lower()}"

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
                config_version=config_version,
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
        """Ping and reconnect if the MySQL connection was dropped."""
        try:
            self._conn.ping(reconnect=True)
        except Exception as exc:
            logger.warning("[mapper] MySQL ping/reconnect failed: %s", exc)
            raise
