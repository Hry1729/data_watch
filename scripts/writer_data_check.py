"""Read-only field spot checks; reference frames travel in memory, not reports.

Executed inside the Backend container. Never ACKs, claims, deletes or writes.
This checks sampled frames, not exactly-once delivery or all historical rows.
"""
from __future__ import annotations

import base64
import json
import math
import os
import sys
import time
import urllib.request


def snapshot() -> dict:
    import redis

    client = redis.Redis(
        host=os.environ.get("REDIS_HOST", "redis"), port=int(os.environ.get("REDIS_PORT", 6379)),
        db=int(os.environ.get("REDIS_DB", 0)), password=os.environ.get("REDIS_PASSWORD") or None,
        decode_responses=True, socket_connect_timeout=3, socket_timeout=3,
    )
    frames, keys, cursor = [], set(), 0
    try:
        deadline = time.monotonic() + 4
        for _ in range(100):
            cursor, found = client.scan(cursor, match="rt:stream:*", count=100)
            keys.update(found)
            if not cursor or len(keys) >= 64 or time.monotonic() >= deadline:
                break
        # One recent frame from each of at most eight streams keeps overhead bounded.
        for key in sorted(keys)[:8]:
            entries = client.xrevrange(key, count=1)
            if entries:
                entry_id, fields = entries[0]
                frames.append({"stream": key, "entry_id": entry_id, "fields": fields})
        return {"available": True, "frames": frames}
    finally:
        client.close()


def quote(name: str) -> str:
    return "`" + str(name).replace("`", "``") + "`"


def td_query(sql: str) -> list:
    scheme = "https" if os.environ.get("TDENGINE_USE_SSL", "false").lower() in {"1", "true", "yes"} else "http"
    host, port = os.environ.get("TDENGINE_HOST", "tdengine"), int(os.environ.get("TDENGINE_PORT", 6041))
    auth = (os.environ.get("TDENGINE_USER", "root") + ":" + os.environ.get("TDENGINE_PASSWORD", "")).encode()
    request = urllib.request.Request(f"{scheme}://{host}:{port}/rest/sql", data=sql.encode(), headers={
        "Authorization": "Basic " + base64.b64encode(auth).decode(), "Content-Type": "text/plain"})
    with urllib.request.urlopen(request, timeout=5) as response:
        raw = response.read(1048577)
    if len(raw) > 1048576:
        raise ValueError("response_too_large")
    result = json.loads(raw)
    if result.get("code") != 0:
        raise RuntimeError("query_unavailable")
    return result.get("data", [])


def check_frame(connection, frame: dict) -> dict:
    import pymysql.cursors

    fields = frame["fields"]
    device, group, ts = fields["device_code"], fields["group_code"], int(fields["ts"])
    if not 0 <= ts <= 9223372036854775807:
        raise ValueError("timestamp_out_of_range")
    raw_values = json.loads(fields["values"])
    version = int(fields.get("contract_version", 1))
    raw_qualities = json.loads(fields.get("qualities", "{}")) if version == 2 else {}
    with connection.cursor(pymysql.cursors.DictCursor) as cursor:
        cursor.execute("SELECT point_id, td_column_name, child_table FROM point_storage_mapping "
                       "WHERE device_code=%s AND group_code=%s", (device, group))
        mapping = cursor.fetchall()
    if not mapping:
        return {"status": "not_tested", "reason": "mapping_unavailable"}
    columns = {str(row["point_id"]): row["td_column_name"] for row in mapping if row["td_column_name"]}
    if any(str(key) not in columns for key in raw_values):
        return {"status": "not_tested", "reason": "mapping_changed_or_incomplete"}
    if version == 2 and (
        {str(key) for key in raw_values} != {str(key) for key in raw_qualities if str(key) in columns}
        or any(type(value) is not int or not 0 <= value <= 255 for value in raw_qualities.values())
    ):
        return {"status": "not_tested", "reason": "invalid_reference_quality"}
    expected = {columns[str(key)]: float(value) for key, value in raw_values.items()}
    if not expected or not all(math.isfinite(value) for value in expected.values()):
        return {"status": "not_tested", "reason": "invalid_reference_values"}
    if len(expected) > 4096:
        raise ValueError("too_many_points")
    db = quote(os.environ.get("TDENGINE_DATABASE", "device_data"))
    table = quote(mapping[0]["child_table"] or device)
    names = list(expected)
    values = td_query(f"SELECT {', '.join(quote(name) for name in names)} FROM {db}.{table} WHERE ts={ts} LIMIT 2")
    if not values:
        return {"status": "not_tested", "reason": "sample_not_landed_yet"}
    value_mismatches = sum(actual is None or not math.isclose(float(actual), expected[name], rel_tol=1e-9, abs_tol=1e-12)
                           for name, actual in zip(names, values[0]))
    value_mismatches += abs(len(names) - len(values[0]))
    duplicates, quality_mismatches = max(0, len(values) - 1), 0
    expected_quality = {int(key): int(value) for key, value in raw_qualities.items() if str(key) in columns}
    if expected_quality:
        ids = ",".join(str(value) for value in expected_quality)
        quality_rows = td_query(f"SELECT var_id,quality FROM {db}.{quote(device.strip() + '_q')} "
                                f"WHERE ts={ts} AND var_id IN ({ids}) LIMIT 8193")
        qualities = {int(row[0]): int(row[1]) for row in quality_rows}
        duplicates += max(0, len(quality_rows) - len(qualities))
        quality_mismatches = sum(qualities.get(key) != value for key, value in expected_quality.items())
    return {"status": "pass" if not value_mismatches + quality_mismatches + duplicates else "fail",
            "value_points": len(expected), "quality_points": len(expected_quality),
            "value_mismatches": value_mismatches, "quality_mismatches": quality_mismatches,
            "duplicate_rows": duplicates}


def verify(reference: dict) -> dict:
    import pymysql

    if not reference.get("available") or not reference.get("frames"):
        return {"status": "not_tested", "reason": "no_reference_frames", "frames": []}
    connection = pymysql.connect(
        host=os.environ.get("MYSQL_HOST", "mysql"), port=int(os.environ.get("MYSQL_PORT", 3306)),
        user=os.environ.get("MYSQL_USER", "root"), password=os.environ.get("MYSQL_PASSWORD", ""),
        database=os.environ.get("MYSQL_DATABASE", "device_watch"), charset="utf8mb4",
        connect_timeout=3, read_timeout=5, write_timeout=5, autocommit=True,
    )
    checked = []
    try:
        for frame in reference["frames"]:
            try:
                outcome = check_frame(connection, frame)
            except Exception as exc:
                outcome = {"status": "not_tested", "reason": type(exc).__name__}
            checked.append({"stream": frame["stream"], "entry_id": frame["entry_id"], **outcome})
    finally:
        connection.close()
    status = "fail" if any(row["status"] == "fail" for row in checked) else (
        "pass" if all(row["status"] == "pass" for row in checked) else "not_tested")
    return {"status": status, "frames": checked,
            "scope": "sampled_frames_only; duplicate writes hidden by upserts remain unproven"}


if __name__ == "__main__":
    try:
        result = snapshot() if sys.argv[1] == "snapshot" else verify(json.load(sys.stdin))
    except Exception as exc:
        result = {"available": False, "status": "not_tested", "reason": type(exc).__name__}
    print(json.dumps(result, ensure_ascii=True))
