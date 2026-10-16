"""CI 冒烟：workers=2 时 schema 预热完成、/healthz ready，并成功 ACK 一批上传。

在 ingest 镜像里执行（自带 pymysql / redis / cryptography）：

    python3.14t /app/ingest/ci/smoke_upload.py prepare
    python3.14t /app/ingest/ci/smoke_upload.py verify
"""

from __future__ import annotations

import base64
import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from uuid import uuid4

_REPO_ROOT = Path(__file__).resolve().parents[2]
if str(_REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(_REPO_ROOT))

import pymysql
import redis
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.serialization import Encoding, PublicFormat

from ingest.app.security import canonical_json, signing_message

NODE_UUID = "11111111-1111-4111-8111-111111111111"
DEVICE_CODE = "QC100"
DEVICE_TYPE = "qc"
GROUP_CODE = "g001"
POINT_ID = 1


def _mysql():
    return pymysql.connect(
        host=os.environ.get("MYSQL_HOST", "127.0.0.1"),
        port=int(os.environ.get("MYSQL_PORT", "3306")),
        user=os.environ.get("MYSQL_USER", "root"),
        password=os.environ.get("MYSQL_PASSWORD", ""),
        database=os.environ.get("MYSQL_DATABASE", "device_watch"),
        charset="utf8mb4",
        autocommit=True,
    )


def prepare() -> None:
    """迁移要引用 device，回填要读点位表。这些表先建空的，数据等预热之后再写。"""
    statements = (
        """
        CREATE TABLE IF NOT EXISTS device_type (
            id BIGINT NOT NULL PRIMARY KEY,
            type_code VARCHAR(32) NOT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS device (
            id BIGINT NOT NULL PRIMARY KEY,
            device_code VARCHAR(64) NOT NULL,
            type_id BIGINT NOT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS device_point_group (
            id BIGINT NOT NULL PRIMARY KEY,
            device_id BIGINT NOT NULL,
            group_code VARCHAR(64) NOT NULL,
            config_version BIGINT NOT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS device_variable (
            id BIGINT NOT NULL PRIMARY KEY,
            point_group_id BIGINT NOT NULL,
            td_column_name VARCHAR(32) NULL,
            var_type VARCHAR(32) NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
    )
    connection = _mysql()
    try:
        with connection.cursor() as cursor:
            for statement in statements:
                cursor.execute(statement)
    finally:
        connection.close()
    print("prepare ok")


def _wait_healthz(base_url: str) -> dict:
    deadline = time.monotonic() + 60
    last_error = "no response"
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(f"{base_url}/healthz", timeout=3) as response:
                body = json.loads(response.read().decode("utf-8"))
        except Exception as exc:
            last_error = str(exc)
            time.sleep(1)
            continue
        if body.get("status") == "ready":
            return body
        last_error = json.dumps(body, ensure_ascii=False)
        time.sleep(1)
    raise SystemExit(f"/healthz 未就绪: {last_error}")


def _assert_schema_warm() -> None:
    connection = _mysql()
    try:
        with connection.cursor() as cursor:
            cursor.execute(
                """
                SELECT version FROM data_watch_schema_migration
                WHERE component = 'distributed_ingest'
                """
            )
            row = cursor.fetchone()
    finally:
        connection.close()
    if not row or int(row[0]) < 2:
        raise SystemExit(f"schema 预热未完成: {row}")
    print(f"schema warm version={row[0]}")


def _enable_redis_aof() -> None:
    client = redis.Redis.from_url(
        os.environ.get("INGEST_REDIS_URL", "redis://127.0.0.1:6379/0"),
        socket_connect_timeout=3,
    )
    client.config_set("appendonly", "yes")
    print("redis appendonly", client.config_get("appendonly"))


def _seed(public_key_pem: str) -> None:
    connection = _mysql()
    try:
        with connection.cursor() as cursor:
            cursor.execute("DELETE FROM point_storage_mapping WHERE device_code = %s", (DEVICE_CODE,))
            cursor.execute(
                "DELETE FROM device_assignment WHERE node_uuid = %s",
                (NODE_UUID,),
            )
            cursor.execute("DELETE FROM device_point_group WHERE group_code = %s", (GROUP_CODE,))
            cursor.execute("DELETE FROM device WHERE device_code = %s", (DEVICE_CODE,))
            cursor.execute("DELETE FROM device_type WHERE type_code = %s", (DEVICE_TYPE,))
            cursor.execute("DELETE FROM sys_node WHERE node_uuid = %s", (NODE_UUID,))
            cursor.execute(
                """
                INSERT INTO device_type (id, type_code) VALUES (%s, %s)
                """,
                (1, DEVICE_TYPE),
            )
            cursor.execute(
                """
                INSERT INTO device (id, device_code, type_id) VALUES (%s, %s, %s)
                """,
                (1, DEVICE_CODE, 1),
            )
            cursor.execute(
                """
                INSERT INTO device_point_group (id, device_id, group_code, config_version)
                VALUES (%s, %s, %s, %s)
                """,
                (1, 1, GROUP_CODE, 1),
            )
            cursor.execute(
                """
                INSERT INTO sys_node (node_uuid, node_name, status, public_key_pem)
                VALUES (%s, %s, 'approved', %s)
                """,
                (NODE_UUID, "ci-ingest", public_key_pem),
            )
            cursor.execute(
                """
                INSERT INTO device_assignment (
                    device_id, node_uuid, assignment_epoch, effective_at, status
                ) VALUES (%s, %s, 1, CURRENT_TIMESTAMP(3), 'active')
                """,
                (1, NODE_UUID),
            )
            cursor.execute(
                """
                INSERT INTO point_storage_mapping (
                    point_id, device_code, device_type_code, stable_name,
                    child_table, group_code, td_column_name, var_type
                ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (
                    POINT_ID,
                    DEVICE_CODE,
                    DEVICE_TYPE,
                    "sdqc",
                    DEVICE_CODE,
                    GROUP_CODE,
                    "c1",
                    "REAL",
                ),
            )
    finally:
        connection.close()


def _post_batch(base_url: str, private_key: Ed25519PrivateKey) -> dict:
    session_id = str(uuid4())
    payload = {
        "node_uuid": NODE_UUID,
        "request_id": str(uuid4()),
        "items": [
            {
                "batch_id": str(uuid4()),
                "upload_session_id": session_id,
                "local_seq": 1,
                "assignment_epoch": 1,
                "device_code": DEVICE_CODE,
                "device_type": DEVICE_TYPE,
                "group_code": GROUP_CODE,
                "scan_rate": 100,
                "ts": int(time.time() * 1000),
                "config_version": 1,
                "values": {str(POINT_ID): "12.5"},
            }
        ],
    }
    body = canonical_json(payload)
    timestamp = str(int(time.time()))
    nonce = str(uuid4())
    signature = base64.b64encode(
        private_key.sign(signing_message("POST", "/ingest", timestamp, nonce, body))
    ).decode("ascii")
    request = urllib.request.Request(
        f"{base_url}/ingest",
        data=body,
        method="POST",
        headers={
            "content-type": "application/json",
            "x-datawatch-timestamp": timestamp,
            "x-datawatch-nonce": nonce,
            "x-datawatch-signature": signature,
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            raw = response.read().decode("utf-8")
            status = response.status
    except urllib.error.HTTPError as exc:
        raw = exc.read().decode("utf-8", errors="replace")
        raise SystemExit(f"POST /ingest HTTP {exc.code}: {raw}") from exc
    parsed = json.loads(raw)
    results = parsed.get("results") or []
    if status != 200 or not results or results[0].get("status") != "ack":
        raise SystemExit(f"上传未被 ACK: HTTP {status} {raw}")
    print("upload ack", raw)
    return parsed


def verify() -> None:
    base_url = os.environ.get("INGEST_URL", "http://127.0.0.1:8010").rstrip("/")
    health = _wait_healthz(base_url)
    if int(health.get("workers") or 0) != 2:
        raise SystemExit(f"/healthz workers 不是 2: {health}")
    print("healthz", json.dumps(health, ensure_ascii=False))
    _assert_schema_warm()
    _enable_redis_aof()
    private_key = Ed25519PrivateKey.generate()
    public_pem = private_key.public_key().public_bytes(
        Encoding.PEM, PublicFormat.SubjectPublicKeyInfo
    ).decode("utf-8")
    _seed(public_pem)
    _post_batch(base_url, private_key)


def main() -> None:
    command = sys.argv[1] if len(sys.argv) > 1 else "verify"
    if command == "prepare":
        prepare()
        return
    if command == "verify":
        verify()
        return
    raise SystemExit("用法: smoke_upload.py prepare|verify")


if __name__ == "__main__":
    main()
