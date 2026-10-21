#!/usr/bin/env python3
"""DB-3: 为存量设备初始化 g001 点位分组，并填充 device_variable 的分组映射字段。

幂等性保证：
  - device_point_group 使用 INSERT IGNORE（已存在则跳过）
  - device_variable 更新条件为 WHERE td_column_name IS NULL（已填充则跳过）
  - 可安全重复执行

执行前提：
  - DB-1 (device_point_group 表) 已创建
  - DB-2 (device_variable 新字段) 已执行
  - DB-4 (device.variable_config_version) 已执行

用法：
  python scripts/migrate_point_groups.py
  python scripts/migrate_point_groups.py --host 127.0.0.1 --port 3306 --user root --password xxx --database device_watch
"""

from __future__ import annotations

import argparse
import sys

try:
    import pymysql
    import pymysql.cursors
except ImportError:
    print("ERROR: PyMySQL not installed. Run: pip install PyMySQL", file=sys.stderr)
    sys.exit(1)


# 单个点位分组的 TDengine 列容量（v0001..v3072）。
GROUP_CAPACITY = 3072


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Migrate point groups for all devices")
    p.add_argument("--host",     default="127.0.0.1")
    p.add_argument("--port",     type=int, default=3306)
    p.add_argument("--user",     default="root")
    p.add_argument("--password", default="19990910")
    p.add_argument("--database", default="device_watch")
    p.add_argument("--dry-run",  action="store_true",
                   help="Print what would be done without writing")
    return p.parse_args()


def check_columns_exist(cursor) -> tuple[bool, bool, bool]:
    """Return (point_group_id_exists, td_column_name_exists, variable_config_version_exists)."""
    cursor.execute("""
        SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'device_variable'
          AND COLUMN_NAME IN ('point_group_id', 'td_column_name', 'enable', 'deleted')
    """)
    dv_cols = {row["COLUMN_NAME"] for row in cursor.fetchall()}

    cursor.execute("""
        SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'device'
          AND COLUMN_NAME = 'variable_config_version'
    """)
    dev_cols = {row["COLUMN_NAME"] for row in cursor.fetchall()}

    cursor.execute("""
        SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'device_point_group'
    """)
    has_dpg = cursor.fetchone() is not None

    return has_dpg, ("td_column_name" in dv_cols), ("variable_config_version" in dev_cols)


def migrate(conn, dry_run: bool) -> None:
    with conn.cursor() as cur:
        has_dpg, has_td_col, has_cfg_ver = check_columns_exist(cur)
        if not has_dpg:
            print("ERROR: device_point_group table not found. Run DB-1 first.", file=sys.stderr)
            sys.exit(1)
        if not has_td_col:
            print("ERROR: device_variable.td_column_name not found. Run DB-2 first.", file=sys.stderr)
            sys.exit(1)
        if not has_cfg_ver:
            print("ERROR: device.variable_config_version not found. Run DB-4 first.", file=sys.stderr)
            sys.exit(1)

        # Load all devices with their type_code
        cur.execute("""
            SELECT d.id AS device_id, d.device_code, dt.type_code
            FROM device d
            JOIN device_type dt ON dt.id = d.type_id
            ORDER BY d.id
        """)
        devices = cur.fetchall()
        print(f"Found {len(devices)} devices to process")

        created = skipped = updated_vars = 0

        for dev in devices:
            device_id   = dev["device_id"]
            device_code = dev["device_code"]

            # ── 1. 检查 device_point_group 是否已存在 ──────────────────────────
            cur.execute(
                "SELECT id, next_column_index FROM device_point_group "
                "WHERE device_id = %s AND group_code = 'g001'",
                (device_id,)
            )
            existing_group = cur.fetchone()

            if existing_group:
                group_id = existing_group["id"]
                skipped += 1
            else:
                # ── 2. 统计该设备的 device_variable 数量 ──────────────────────
                cur.execute(
                    "SELECT COUNT(*) AS cnt FROM device_variable WHERE device_id = %s",
                    (device_id,)
                )
                point_count = cur.fetchone()["cnt"]

                if not dry_run:
                    # B018：新组列计数从 0 起，由第 3 步按实际分配的列统一设置
                    # used/next，避免先置 N 再以 N+1 起分配导致计数翻倍。
                    cur.execute("""
                        INSERT INTO device_point_group
                            (device_id, group_no, group_code, group_name,
                             max_point_count, active_point_count,
                             used_column_count, next_column_index,
                             status, config_version)
                        VALUES (%s, 1, 'g001', NULL,
                                %s, %s,
                                0, 1,
                                'active', 1)
                    """, (device_id, GROUP_CAPACITY, point_count))
                    group_id = cur.lastrowid
                    created += 1
                    print(f"  [created] device={device_code} group_id={group_id} points={point_count}")
                else:
                    # 审查 R2：dry-run 不建组，但仍须按“新组 used=0、容量 GROUP_CAPACITY”
                    # 走下面同一套映射与容量校验，否则超容量设备会得到成功的预览。
                    group_id = None
                    print(f"  [dry-run] would create g001 for device={device_code} points={point_count}")

            # ── 3. 填充 device_variable 的分组映射字段（幂等：只处理 td_column_name IS NULL）
            cur.execute("""
                SELECT id, var_seq
                FROM device_variable
                WHERE device_id = %s AND td_column_name IS NULL
                ORDER BY var_seq
            """, (device_id,))
            unmapped_vars = cur.fetchall()

            if not unmapped_vars:
                continue

            # 计算本轮起始 group_index（接续已分配的列位）
            if group_id is None:
                row = {"used_column_count": 0, "max_point_count": GROUP_CAPACITY}  # dry-run 预计新组
            else:
                cur.execute(
                    "SELECT used_column_count, max_point_count FROM device_point_group WHERE id = %s",
                    (group_id,)
                )
                row = cur.fetchone()
            start_index = (int(row["used_column_count"] or 0) + 1) if row else 1
            capacity = int((row or {}).get("max_point_count") or GROUP_CAPACITY)
            last_index = start_index - 1 + len(unmapped_vars)
            if last_index > capacity:
                # B018：拒绝超出分组列容量的迁移，整个事务回滚，不写入越界列。
                if not dry_run:
                    conn.rollback()
                print(
                    f"ERROR: device={device_code} needs columns up to v{last_index:04d}, "
                    f"exceeds g001 capacity {capacity}; "
                    + ("dry-run: the real migration would abort here."
                       if dry_run else "migration aborted (no changes committed)."),
                    file=sys.stderr,
                )
                sys.exit(1)

            if not dry_run:
                for offset, var in enumerate(unmapped_vars):
                    idx = start_index + offset
                    td_col = f"v{idx:04d}"
                    cur.execute("""
                        UPDATE device_variable
                        SET point_group_id = %s, group_index = %s, td_column_name = %s
                        WHERE id = %s AND td_column_name IS NULL
                    """, (group_id, idx, td_col, var["id"]))
                    updated_vars += 1

                # 更新分组计数
                new_used = last_index
                cur.execute("""
                    UPDATE device_point_group
                    SET used_column_count = %s, next_column_index = %s
                    WHERE id = %s
                """, (new_used, new_used + 1, group_id))
            else:
                print(f"  [dry-run] would map {len(unmapped_vars)} vars for device={device_code}")

        if not dry_run:
            conn.commit()

        print(f"\nDone. groups created={created} skipped={skipped} vars_mapped={updated_vars}")


def main() -> int:
    args = parse_args()
    conn = pymysql.connect(
        host=args.host,
        port=args.port,
        user=args.user,
        password=args.password,
        database=args.database,
        charset="utf8mb4",
        cursorclass=pymysql.cursors.DictCursor,
        autocommit=False,
    )
    try:
        migrate(conn, dry_run=args.dry_run)
    finally:
        conn.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
