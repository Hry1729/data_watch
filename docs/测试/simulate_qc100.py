"""桥吊100演示数据：每100ms写TDengine历史值/质量以及Redis实时值。"""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
import random
import re
import sys
import time

# 本地运行复用backend配置；Docker启动脚本则在已有后端容器中执行。
for parent in Path(__file__).resolve().parents:
    if (parent / "backend" / "app").is_dir():
        sys.path.insert(0, str(parent / "backend"))
        break
sys.path.insert(0, str(Path.cwd()))

DEVICE = "qc100"
INTERVAL = 0.1
INTEGER_TYPES = {"int", "dint", "byte", "word", "dword", "uint", "udint", "sint", "usint", "lint", "ulint", "short", "long"}
FLOAT_TYPES = {"real", "lreal", "float", "double"}


def identifier(value: str) -> str:
    if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", value):
        raise ValueError("数据库标识符不合法")
    return "`" + value + "`"


def wave(elapsed: float, point_id: int, period: float, noise: float, seed: int) -> float:
    """连续且可重复的多频正弦扰动；每个点拥有自己的周期和相位。"""
    rng = random.Random(seed + point_id)
    phase = rng.uniform(0, 2 * math.pi)
    speed = rng.uniform(0.7, 1.3)
    angle = 2 * math.pi * elapsed * speed / period + phase
    value = 50 + 50 * math.sin(angle)
    value += noise * (0.65 * math.sin(angle * 3.7 + phase) + 0.35 * math.sin(angle * 9.1))
    return round(max(0.0, min(100.0, value)), 3)


def point_value(value: float, var_type: str) -> int | float:
    kind = var_type.lower()
    if kind in {"bool", "boolean"}:
        return int(value >= 50)
    if kind in INTEGER_TYPES:
        return round(value)
    if kind in FLOAT_TYPES:
        return value
    raise ValueError("不支持的点位类型：" + var_type)


def load_points(settings):
    import pymysql
    conn = pymysql.connect(
        host=settings.mysql_host, port=settings.mysql_port,
        user=settings.mysql_user, password=settings.mysql_password,
        database=settings.mysql_database, charset=settings.mysql_charset,
        cursorclass=pymysql.cursors.DictCursor, connect_timeout=5,
    )
    try:
        with conn.cursor() as cursor:
            cursor.execute("""
                SELECT p.point_id, p.group_code, p.td_column_name, p.var_type,
                       p.child_table, p.stable_name, v.is_storage
                FROM point_storage_mapping p
                JOIN device_variable v ON v.id = p.point_id
                JOIN device d ON d.id = v.device_id
                WHERE p.device_code = %s AND v.deleted = 0 AND v.enable = 1
                  AND d.enable = 1 AND d.device_code = %s
                ORDER BY p.point_id
            """, (DEVICE, DEVICE))
            points = list(cursor.fetchall())
    finally:
        conn.close()
    if not points:
        raise ValueError("桥吊100没有启用的映射点位")
    for p in points:
        if p["child_table"] != DEVICE or p["stable_name"] != "sdqc":
            raise ValueError("映射目标不是qc100/sdqc，停止写入")
        identifier(p["td_column_name"])
        point_value(50, p["var_type"])
    columns = [p["td_column_name"] for p in points]
    if len(set(columns)) != len(columns):
        raise ValueError("映射存在重复列，停止写入")
    if not any(p["is_storage"] for p in points):
        raise ValueError("桥吊100没有启用存储的点位")
    return points


def build_frame(points, timestamp, elapsed, period, noise, seed, database):
    values = {p["point_id"]: point_value(wave(elapsed, p["point_id"], period, noise, seed), p["var_type"]) for p in points}
    stored = [p for p in points if p["is_storage"]]
    columns = ",".join(identifier(p["td_column_name"]) for p in stored)
    data = ",".join(str(values[p["point_id"]]) for p in stored)
    quality = " ".join(f"({timestamp},{int(p['point_id'])},0)" for p in stored)
    db = identifier(database)
    sql = (f"INSERT INTO {db}.`qc100` (`ts`,{columns}) VALUES ({timestamp},{data}) "
           f"{db}.`qc100_q` (`ts`,`var_id`,`quality`) VALUES {quality}")
    latest = {}
    for p in points:
        latest.setdefault(p["group_code"], {})[str(p["point_id"])] = json.dumps(
            {"v": str(values[p["point_id"]]), "t": timestamp, "q": 0}, separators=(",", ":"))
    return sql, latest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--duration", type=float, default=0, help="运行秒数，0=持续运行，Ctrl+C停止")
    parser.add_argument("--period", type=float, default=12, help="基础波动周期，默认12秒")
    parser.add_argument("--noise", type=float, default=12, help="扰动幅度，默认12，最终仍限制在0..100")
    parser.add_argument("--seed", type=int, default=100)
    parser.add_argument("--dry-run", action="store_true", help="只验证连接/映射/表结构，不写数据")
    args = parser.parse_args()
    if not math.isfinite(args.duration) or args.duration < 0:
        parser.error("duration必须是有限非负数")
    if not math.isfinite(args.period) or args.period <= 0:
        parser.error("period必须是有限正数")
    if not math.isfinite(args.noise) or not 0 <= args.noise <= 50:
        parser.error("noise必须在0..50之间")

    import httpx
    import redis
    from app.core.config import get_settings
    settings = get_settings()
    points = load_points(settings)
    base = ("https" if settings.tdengine_use_ssl else "http") + f"://{settings.tdengine_host}:{settings.tdengine_port}/rest/sql"
    client = httpx.Client(auth=(settings.tdengine_user, settings.tdengine_password), timeout=5, trust_env=False)
    cache = redis.Redis(host=settings.redis_host, port=settings.redis_port, db=settings.redis_db,
                        password=settings.redis_password or None, socket_timeout=5, socket_connect_timeout=5)

    def query(sql):
        response = client.post(base, content=sql.encode("utf-8"))
        response.raise_for_status()
        result = response.json()
        if result.get("code") != 0:
            raise RuntimeError(f"TDengine查询失败 code={result.get('code')}: {result.get('desc')}")
        return result.get("data", [])

    frames = missed = 0
    started = time.monotonic()
    try:
        db = identifier(settings.tdengine_database)
        schema = query(f"DESCRIBE {db}.`qc100`")
        columns = {row[0]: row[1].upper() for row in schema}
        if any(columns.get(p["td_column_name"]) != "DOUBLE" for p in points if p["is_storage"]):
            raise ValueError("qc100字段与当前映射不一致，请先恢复结构")
        quality_schema = query(f"DESCRIBE {db}.`qc100_q`")
        var_id = next((row for row in quality_schema if row[0] == "var_id"), [])
        if "COMPOSITE" not in " ".join(map(str, var_id)).upper():
            raise ValueError("qc100_q缺少var_id复合主键")
        cache.ping()
        print(f"[DEMO] 仅桥吊100(qc100)：{len(points)}点，存储{sum(bool(p['is_storage']) for p in points)}点；100ms/帧。", flush=True)
        if args.dry_run:
            print("连接、映射及表结构检查通过；没有写入数据。", flush=True)
            return
        print("正在写入模拟值。请勿同时启动其他qc100数据源；Ctrl+C停止。", flush=True)
        started = time.monotonic()
        deadline = started
        next_report = started + 5
        last_timestamp = 0
        while args.duration == 0 or time.monotonic() - started < args.duration:
            now = time.monotonic()
            if now < deadline:
                time.sleep(min(deadline - now, max(0, started + args.duration - now)) if args.duration else deadline - now)
            now = time.monotonic()
            if args.duration and now - started >= args.duration:
                break
            timestamp = max(time.time_ns() // 1_000_000, last_timestamp + 1)
            last_timestamp = timestamp
            sql, latest = build_frame(points, timestamp, now - started, args.period, args.noise, args.seed, settings.tdengine_database)
            query(sql)
            # 先写历史，成功后更新latest；不向Stream重复投递。
            with cache.pipeline(transaction=False) as pipe:
                for group, mapping in latest.items():
                    pipe.hset(f"rt:latest:{DEVICE}:{group}", mapping=mapping)
                    pipe.publish("rt:change", json.dumps({"device_code": DEVICE, "group_code": group}))
                pipe.execute()
            frames += 1
            deadline += INTERVAL
            finished = time.monotonic()
            if finished > deadline:
                skipped = math.floor((finished - deadline) / INTERVAL) + 1
                deadline += skipped * INTERVAL
                missed += skipped
            if finished >= next_report:
                print(f"[DEMO] {frames}帧，平均{frames / (finished - started):.1f}帧/秒，慢写跳过{missed}个周期", flush=True)
                next_report = finished + 5
    except KeyboardInterrupt:
        print("\n已停止。", flush=True)
    finally:
        cache.close()
        client.close()
        if frames:
            print(f"完成：{frames}帧，运行{time.monotonic() - started:.2f}秒；演示数据保留。", flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"停止写入：{type(exc).__name__}: {exc}", file=sys.stderr)
        sys.exit(1)
