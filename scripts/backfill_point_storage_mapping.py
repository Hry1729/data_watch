from __future__ import annotations

import os
import sys

import pymysql
import pymysql.cursors


INSERT_STAGING_SQL = """
INSERT INTO point_storage_mapping_staging (
    point_id, device_code, device_type_code, stable_name,
    child_table, group_code, td_column_name, var_type, created_at
)
SELECT
    dv.id,
    d.device_code,
    LOWER(dt.type_code),
    CONCAT('sd', LOWER(dt.type_code)),
    d.device_code,
    dpg.group_code,
    dv.td_column_name,
    COALESCE(dv.var_type, ''),
    CURRENT_TIMESTAMP(3)
FROM device_variable dv
JOIN device_point_group dpg ON dpg.id = dv.point_group_id
JOIN device d ON d.id = dpg.device_id
JOIN device_type dt ON dt.id = d.type_id
WHERE d.device_code IS NOT NULL AND d.device_code <> ''
  AND dt.type_code IS NOT NULL AND dt.type_code <> ''
  AND dpg.group_code IS NOT NULL AND dpg.group_code <> ''
  AND dv.td_column_name IS NOT NULL AND dv.td_column_name <> ''
"""

CHECKS = {
    "MISSING": """
        SELECT dv.id AS point_id, d.device_code
        FROM device_variable dv
        JOIN device_point_group dpg ON dpg.id = dv.point_group_id
        JOIN device d ON d.id = dpg.device_id
        LEFT JOIN point_storage_mapping_staging s ON s.point_id = dv.id
        WHERE s.point_id IS NULL
        LIMIT 100
    """,
    "POINT_ID_CONFLICT": """
        SELECT point_id, COUNT(*) AS row_count
        FROM point_storage_mapping_staging
        GROUP BY point_id
        HAVING COUNT(*) > 1
        LIMIT 100
    """,
    "COLUMN_CONFLICT": """
        SELECT device_code, td_column_name, COUNT(*) AS row_count
        FROM point_storage_mapping_staging
        GROUP BY device_code, td_column_name
        HAVING COUNT(*) > 1
        LIMIT 100
    """,
    "ORPHAN": """
        SELECT m.point_id, m.device_code
        FROM point_storage_mapping m
        LEFT JOIN device_variable dv ON dv.id = m.point_id
        WHERE dv.id IS NULL
        LIMIT 100
    """,
    "MISMATCH": """
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
    "EXISTING_COLUMN_CONFLICT": """
        SELECT s.point_id, s.device_code, s.td_column_name
        FROM point_storage_mapping_staging s
        JOIN point_storage_mapping m
          ON m.device_code = s.device_code
         AND m.td_column_name = s.td_column_name
         AND m.point_id <> s.point_id
        LIMIT 100
    """,
}

PROMOTE_SQL = """
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


def main() -> int:
    connection = pymysql.connect(
        host=os.getenv("MYSQL_HOST", "127.0.0.1"),
        port=int(os.getenv("MYSQL_PORT", "3306")),
        user=os.getenv("MYSQL_USER", "root"),
        password=os.getenv("MYSQL_PASSWORD", ""),
        database=os.getenv("MYSQL_DATABASE", "device_watch"),
        charset="utf8mb4",
        cursorclass=pymysql.cursors.DictCursor,
        autocommit=False,
    )
    lock_acquired = False
    try:
        with connection.cursor() as cursor:
            cursor.execute("SELECT GET_LOCK('data_watch_mapping_backfill', 10) AS acquired")
            lock_acquired = bool(cursor.fetchone()["acquired"])
            if not lock_acquired:
                print("BLOCKED: another mapping backfill is running", file=sys.stderr)
                return 2

            cursor.execute("DELETE FROM point_storage_mapping_staging")
            cursor.execute(INSERT_STAGING_SQL)

            differences: list[tuple[str, dict]] = []
            for name, query in CHECKS.items():
                cursor.execute(query)
                differences.extend((name, row) for row in cursor.fetchall())

            if differences:
                connection.commit()
                for name, row in differences:
                    print(f"{name}: {row}", file=sys.stderr)
                print(
                    f"BLOCKED: {len(differences)} mapping differences; staging retained",
                    file=sys.stderr,
                )
                return 1

            cursor.execute(PROMOTE_SQL)
            promoted = cursor.rowcount
            cursor.execute("DELETE FROM point_storage_mapping_staging")
            connection.commit()
            print(f"OK: promoted {promoted} immutable point mappings")
            return 0
    except Exception:
        connection.rollback()
        raise
    finally:
        if lock_acquired:
            with connection.cursor() as cursor:
                cursor.execute("SELECT RELEASE_LOCK('data_watch_mapping_backfill')")
        connection.close()


if __name__ == "__main__":
    raise SystemExit(main())
