from __future__ import annotations

import argparse

import pymysql
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Seed the isolated Ingest capacity fixture.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=3306)
    parser.add_argument("--user", default="root")
    parser.add_argument("--password", default="")
    parser.add_argument("--database", required=True)
    parser.add_argument("--nodes", type=int, default=100)
    parser.add_argument("--node-offset", type=int, default=0)
    parser.add_argument("--items-per-node", type=int, default=10)
    parser.add_argument("--private-key-hex", default="01" * 32)
    return parser.parse_args()


def node_uuid(index: int) -> str:
    return f"00000000-0000-0000-0000-{index + 1:012x}"


def public_key_pem(private_key_hex: str) -> str:
    private_key = Ed25519PrivateKey.from_private_bytes(bytes.fromhex(private_key_hex))
    return private_key.public_key().public_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PublicFormat.SubjectPublicKeyInfo,
    ).decode("ascii")


def seed(args: argparse.Namespace) -> tuple[int, int]:
    if args.nodes <= 0 or args.items_per_node <= 0 or args.node_offset < 0:
        raise ValueError("nodes/items-per-node must be positive and node-offset non-negative")

    connection = pymysql.connect(
        host=args.host,
        port=args.port,
        user=args.user,
        password=args.password,
        database=args.database,
        charset="utf8mb4",
        autocommit=False,
    )
    node_rows = []
    device_rows = []
    assignment_rows = []
    group_rows = []
    variable_rows = []
    mapping_rows = []
    pem = public_key_pem(args.private_key_hex)

    for node_index in range(args.node_offset, args.node_offset + args.nodes):
        uuid = node_uuid(node_index)
        node_rows.append((uuid, f"bench-{node_index:03d}", "approved", pem))
        for item_index in range(args.items_per_node):
            point_id = node_index * args.items_per_node + item_index + 1
            device_code = f"N{node_index:03d}D{item_index:02d}"
            column_name = f"p{point_id:05d}"
            device_rows.append((point_id, device_code, 1))
            assignment_rows.append((point_id, uuid, 1, "active"))
            group_rows.append((point_id, point_id, "g001", 1))
            variable_rows.append((point_id, point_id, column_name, "REAL", 0, 1))
            mapping_rows.append(
                (
                    point_id,
                    device_code,
                    "bench",
                    "sdbench",
                    device_code,
                    "g001",
                    column_name,
                    "REAL",
                )
            )

    try:
        with connection.cursor() as cursor:
            cursor.execute(
                "INSERT INTO device_type (id, type_code) VALUES (1, 'bench') "
                "ON DUPLICATE KEY UPDATE type_code=VALUES(type_code)"
            )
            cursor.executemany(
                "INSERT INTO sys_node (node_uuid, node_name, status, public_key_pem) "
                "VALUES (%s, %s, %s, %s) ON DUPLICATE KEY UPDATE "
                "node_name=VALUES(node_name), status=VALUES(status), "
                "public_key_pem=VALUES(public_key_pem)",
                node_rows,
            )
            cursor.executemany(
                "INSERT INTO device (id, device_code, type_id) VALUES (%s, %s, %s) "
                "ON DUPLICATE KEY UPDATE device_code=VALUES(device_code), type_id=VALUES(type_id)",
                device_rows,
            )
            cursor.executemany(
                "INSERT INTO device_point_group (id, device_id, group_code, config_version) "
                "VALUES (%s, %s, %s, %s) ON DUPLICATE KEY UPDATE "
                "device_id=VALUES(device_id), group_code=VALUES(group_code), "
                "config_version=VALUES(config_version)",
                group_rows,
            )
            cursor.executemany(
                "INSERT INTO device_variable "
                "(id, point_group_id, td_column_name, var_type, deleted, enable) "
                "VALUES (%s, %s, %s, %s, %s, %s) ON DUPLICATE KEY UPDATE "
                "point_group_id=VALUES(point_group_id), "
                "td_column_name=VALUES(td_column_name), var_type=VALUES(var_type), "
                "deleted=VALUES(deleted), enable=VALUES(enable)",
                variable_rows,
            )
            cursor.executemany(
                "INSERT INTO device_assignment "
                "(device_id, node_uuid, assignment_epoch, effective_at, status) "
                "VALUES (%s, %s, %s, NOW(3), %s) ON DUPLICATE KEY UPDATE "
                "node_uuid=VALUES(node_uuid), effective_at=VALUES(effective_at), "
                "status=VALUES(status)",
                assignment_rows,
            )
            cursor.executemany(
                "INSERT INTO point_storage_mapping "
                "(point_id, device_code, device_type_code, stable_name, child_table, "
                "group_code, td_column_name, var_type) "
                "VALUES (%s, %s, %s, %s, %s, %s, %s, %s) "
                "ON DUPLICATE KEY UPDATE device_code=VALUES(device_code), "
                "device_type_code=VALUES(device_type_code), stable_name=VALUES(stable_name), "
                "child_table=VALUES(child_table), group_code=VALUES(group_code), "
                "td_column_name=VALUES(td_column_name), var_type=VALUES(var_type)",
                mapping_rows,
            )
        connection.commit()
    except Exception:
        connection.rollback()
        raise
    finally:
        connection.close()

    return len(node_rows), len(device_rows)


def main() -> int:
    args = parse_args()
    nodes, devices = seed(args)
    print(f"seeded_nodes={nodes} seeded_devices={devices}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
