from __future__ import annotations

from collections.abc import Iterable

import aiomysql


_MIGRATION_LOCK = "data_watch_distributed_schema_v1"

_CREATE_TABLES = (
    """
    CREATE TABLE IF NOT EXISTS sys_node (
        node_uuid CHAR(36) NOT NULL,
        node_name VARCHAR(128) NOT NULL,
        status VARCHAR(16) NOT NULL DEFAULT 'pending',
        public_key_pem TEXT,
        previous_public_key_pem TEXT,
        previous_key_expires_at DATETIME(3),
        key_version BIGINT UNSIGNED NOT NULL DEFAULT 1,
        config_version BIGINT UNSIGNED NOT NULL DEFAULT 0,
        read_timeout_ms INT UNSIGNED,
        applied_revision BIGINT UNSIGNED NOT NULL DEFAULT 0,
        outbox_backlog BIGINT UNSIGNED NOT NULL DEFAULT 0,
        outbox_database_bytes BIGINT UNSIGNED NOT NULL DEFAULT 0,
        outbox_disk_used_percent DECIMAL(5,2) NOT NULL DEFAULT 0,
        outbox_watermark VARCHAR(16) NOT NULL DEFAULT 'normal',
        outbox_dropped_records BIGINT UNSIGNED NOT NULL DEFAULT 0,
        collector_status VARCHAR(16) NOT NULL DEFAULT 'unknown',
        uploader_status VARCHAR(16) NOT NULL DEFAULT 'unknown',
        edge_time_ms BIGINT,
        clock_skew_ms BIGINT,
        clock_anomaly TINYINT(1) NOT NULL DEFAULT 0,
        last_error VARCHAR(1000),
        last_seen_at DATETIME(3),
        created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
        updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
            ON UPDATE CURRENT_TIMESTAMP(3),
        PRIMARY KEY (node_uuid),
        KEY idx_sys_node_status (status)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
    """,
    """
    CREATE TABLE IF NOT EXISTS node_registration_code (
        code_hash CHAR(64) NOT NULL,
        node_name VARCHAR(128) NOT NULL,
        expires_at DATETIME(3) NOT NULL,
        used_at DATETIME(3),
        used_by_node_uuid CHAR(36),
        created_by VARCHAR(64) NOT NULL,
        created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
        PRIMARY KEY (code_hash),
        KEY idx_node_registration_expiry (expires_at, used_at)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
    """,
    """
    CREATE TABLE IF NOT EXISTS device_assignment (
        device_id BIGINT NOT NULL,
        node_uuid CHAR(36) NOT NULL,
        assignment_epoch BIGINT UNSIGNED NOT NULL,
        effective_at DATETIME(3) NOT NULL,
        cutover_at DATETIME(3),
        grace_until DATETIME(3),
        status VARCHAR(16) NOT NULL DEFAULT 'pending',
        created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
        updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
            ON UPDATE CURRENT_TIMESTAMP(3),
        PRIMARY KEY (device_id, assignment_epoch),
        KEY idx_assignment_node_status (node_uuid, status),
        KEY idx_assignment_device_cutover (device_id, cutover_at),
        CONSTRAINT fk_assignment_device FOREIGN KEY (device_id) REFERENCES device(id),
        CONSTRAINT fk_assignment_node FOREIGN KEY (node_uuid) REFERENCES sys_node(node_uuid)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
    """,
    """
    CREATE TABLE IF NOT EXISTS point_storage_mapping (
        point_id BIGINT NOT NULL,
        device_code VARCHAR(64) NOT NULL,
        device_type_code VARCHAR(32) NOT NULL,
        stable_name VARCHAR(64) NOT NULL,
        child_table VARCHAR(64) NOT NULL,
        group_code VARCHAR(64) NOT NULL,
        td_column_name VARCHAR(32) NOT NULL,
        var_type VARCHAR(32) NOT NULL,
        created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
        PRIMARY KEY (point_id),
        UNIQUE KEY uq_mapping_device_col (device_code, td_column_name),
        KEY idx_mapping_decode (device_code, group_code, point_id)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
    """,
    """
    CREATE TABLE IF NOT EXISTS point_storage_mapping_staging (
        point_id BIGINT NOT NULL,
        device_code VARCHAR(64) NOT NULL,
        device_type_code VARCHAR(32) NOT NULL,
        stable_name VARCHAR(64) NOT NULL,
        child_table VARCHAR(64) NOT NULL,
        group_code VARCHAR(64) NOT NULL,
        td_column_name VARCHAR(32) NOT NULL,
        var_type VARCHAR(32) NOT NULL,
        created_at DATETIME(3) NOT NULL
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
    """,
)

_SYS_NODE_COLUMNS = {
    "previous_public_key_pem": "TEXT AFTER public_key_pem",
    "previous_key_expires_at": "DATETIME(3) AFTER previous_public_key_pem",
    "key_version": "BIGINT UNSIGNED NOT NULL DEFAULT 1 AFTER previous_key_expires_at",
    "read_timeout_ms": "INT UNSIGNED AFTER config_version",
    "applied_revision": "BIGINT UNSIGNED NOT NULL DEFAULT 0 AFTER read_timeout_ms",
    "outbox_backlog": "BIGINT UNSIGNED NOT NULL DEFAULT 0 AFTER applied_revision",
    "outbox_database_bytes": "BIGINT UNSIGNED NOT NULL DEFAULT 0 AFTER outbox_backlog",
    "outbox_disk_used_percent": "DECIMAL(5,2) NOT NULL DEFAULT 0 AFTER outbox_database_bytes",
    "outbox_watermark": "VARCHAR(16) NOT NULL DEFAULT 'normal' AFTER outbox_disk_used_percent",
    "outbox_dropped_records": "BIGINT UNSIGNED NOT NULL DEFAULT 0 AFTER outbox_watermark",
    "collector_status": "VARCHAR(16) NOT NULL DEFAULT 'unknown' AFTER outbox_dropped_records",
    "uploader_status": "VARCHAR(16) NOT NULL DEFAULT 'unknown' AFTER collector_status",
    "edge_time_ms": "BIGINT AFTER uploader_status",
    "clock_skew_ms": "BIGINT AFTER edge_time_ms",
    "clock_anomaly": "TINYINT(1) NOT NULL DEFAULT 0 AFTER clock_skew_ms",
    "last_error": "VARCHAR(1000) AFTER clock_anomaly",
}

_INSERT_STAGING = """
INSERT INTO point_storage_mapping_staging (
    point_id, device_code, device_type_code, stable_name,
    child_table, group_code, td_column_name, var_type, created_at
)
SELECT
    dv.id, d.device_code, LOWER(dt.type_code), CONCAT('sd', LOWER(dt.type_code)),
    d.device_code, dpg.group_code, dv.td_column_name,
    COALESCE(dv.var_type, ''), CURRENT_TIMESTAMP(3)
FROM device_variable dv
JOIN device_point_group dpg ON dpg.id = dv.point_group_id
JOIN device d ON d.id = dpg.device_id
JOIN device_type dt ON dt.id = d.type_id
WHERE d.device_code IS NOT NULL AND d.device_code <> ''
  AND dt.type_code IS NOT NULL AND dt.type_code <> ''
  AND dpg.group_code IS NOT NULL AND dpg.group_code <> ''
  AND dv.td_column_name IS NOT NULL AND dv.td_column_name <> ''
"""

_BACKFILL_CHECKS = {
    "missing": """
        SELECT dv.id AS point_id, d.device_code
        FROM device_variable dv
        JOIN device_point_group dpg ON dpg.id = dv.point_group_id
        JOIN device d ON d.id = dpg.device_id
        LEFT JOIN point_storage_mapping_staging s ON s.point_id = dv.id
        WHERE s.point_id IS NULL LIMIT 100
    """,
    "point_id_conflict": """
        SELECT point_id, COUNT(*) AS row_count
        FROM point_storage_mapping_staging
        GROUP BY point_id HAVING COUNT(*) > 1 LIMIT 100
    """,
    "column_conflict": """
        SELECT device_code, td_column_name, COUNT(*) AS row_count
        FROM point_storage_mapping_staging
        GROUP BY device_code, td_column_name HAVING COUNT(*) > 1 LIMIT 100
    """,
    "orphan": """
        SELECT m.point_id, m.device_code
        FROM point_storage_mapping m
        LEFT JOIN device_variable dv ON dv.id = m.point_id
        WHERE dv.id IS NULL LIMIT 100
    """,
    "mismatch": """
        SELECT s.point_id, s.device_code
        FROM point_storage_mapping_staging s
        JOIN point_storage_mapping m ON m.point_id = s.point_id
        WHERE m.device_code <> s.device_code
           OR m.device_type_code <> s.device_type_code
           OR m.stable_name <> s.stable_name
           OR m.child_table <> s.child_table
           OR m.group_code <> s.group_code
           OR m.td_column_name <> s.td_column_name
           OR m.var_type <> s.var_type
        LIMIT 100
    """,
    "existing_column_conflict": """
        SELECT s.point_id, s.device_code, s.td_column_name
        FROM point_storage_mapping_staging s
        JOIN point_storage_mapping m
          ON m.device_code = s.device_code
         AND m.td_column_name = s.td_column_name
         AND m.point_id <> s.point_id
        LIMIT 100
    """,
}

_PROMOTE = """
INSERT INTO point_storage_mapping (
    point_id, device_code, device_type_code, stable_name,
    child_table, group_code, td_column_name, var_type, created_at
)
SELECT
    s.point_id, s.device_code, s.device_type_code, s.stable_name,
    s.child_table, s.group_code, s.td_column_name, s.var_type, s.created_at
FROM point_storage_mapping_staging s
LEFT JOIN point_storage_mapping m ON m.point_id = s.point_id
WHERE m.point_id IS NULL
"""


class MappingBackfillBlocked(RuntimeError):
    pass


async def migrate_distributed_schema(pool: aiomysql.Pool) -> None:
    async with pool.acquire() as connection:
        async with connection.cursor(aiomysql.DictCursor) as cursor:
            await cursor.execute("SELECT GET_LOCK(%s, 30) AS acquired", (_MIGRATION_LOCK,))
            row = await cursor.fetchone()
            if not row or int(row.get("acquired") or 0) != 1:
                raise RuntimeError("distributed schema migration lock timeout")
            try:
                await _create_tables(cursor)
                await _upgrade_sys_node(cursor)
                await _backfill_mapping(connection, cursor)
            finally:
                await cursor.execute("SELECT RELEASE_LOCK(%s)", (_MIGRATION_LOCK,))


async def _create_tables(cursor) -> None:
    for statement in _CREATE_TABLES:
        await cursor.execute(statement)


async def _upgrade_sys_node(cursor) -> None:
    await cursor.execute(
        """
        SELECT COLUMN_NAME
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_node'
        """
    )
    existing = {str(row["COLUMN_NAME"]) for row in await cursor.fetchall()}
    for name, definition in _SYS_NODE_COLUMNS.items():
        if name not in existing:
            await cursor.execute(f"ALTER TABLE sys_node ADD COLUMN {name} {definition}")


async def _backfill_mapping(connection, cursor) -> None:
    await connection.begin()
    try:
        await cursor.execute("DELETE FROM point_storage_mapping_staging")
        await cursor.execute(_INSERT_STAGING)
        differences: list[str] = []
        for name, query in _BACKFILL_CHECKS.items():
            await cursor.execute(query)
            differences.extend(_format_differences(name, await cursor.fetchall()))
        if differences:
            await connection.commit()
            preview = "; ".join(differences[:10])
            raise MappingBackfillBlocked(
                f"point_storage_mapping backfill blocked ({len(differences)} differences): {preview}"
            )
        await cursor.execute(_PROMOTE)
        await cursor.execute("DELETE FROM point_storage_mapping_staging")
        await connection.commit()
    except MappingBackfillBlocked:
        raise
    except Exception:
        await connection.rollback()
        raise


def _format_differences(name: str, rows: Iterable[dict]) -> list[str]:
    return [f"{name}={dict(row)}" for row in rows]
