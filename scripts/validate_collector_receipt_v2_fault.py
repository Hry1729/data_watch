"""Isolated Redis AOF crash/replay check. Run only against a disposable instance."""
from __future__ import annotations

import argparse
import json
import tempfile
from pathlib import Path
from types import SimpleNamespace
from uuid import uuid4

import redis
from data_watch_shared.stream_envelope import stream_key
from storage.durable_redis_writer import DurableRedisWriter

# 用系统临时目录而不是写死 /tmp：本脚本要能在 Windows 与 Linux 容器里都跑，
# 否则 Windows 上会落到盘符根目录下的 \tmp。
_TEMP = Path(tempfile.gettempdir())
STATE = _TEMP / "dw_receipt_v2_fault_state.json"


def sample(device: str):
    return dict(
        device_code=device, group_code="g", device_type="qc",
        scan_rate_ms=100, ts_ms=1234, config_version=2,
        frame={"ts": 1234, "values": {"1": "7"}},
        stream_values={"1": "7"}, stream_qualities={"1": 192},
        latest_fields={"1": {"v": "7", "t": 1234, "q": 192}},
    )


def writer(url: str, path: Path):
    client = redis.Redis.from_url(url, socket_timeout=5)
    config = SimpleNamespace(redis=SimpleNamespace(stream_shard_count=4, stream_maxlen=0))
    return DurableRedisWriter(client, config, outbox_path=path)


def first(url: str):
    device = "receipt-fault-" + uuid4().hex
    db_path = _TEMP / (device + ".db")
    w = writer(url, db_path)
    w.outbox.record_failure(**sample(device))
    row = w.outbox._conn.execute(
        "SELECT session_id, seq FROM collector_retry_outbox_v2"
    ).fetchone()
    original = w.write_durable_frames

    def lose_reply(rows):
        original(rows)
        raise RuntimeError("simulated client response loss after WAITAOF")

    w.write_durable_frames = lose_reply
    try:
        w.outbox.replay_pending(w)
    except RuntimeError as exc:
        if "simulated client response loss" not in str(exc):
            raise
    else:
        raise AssertionError("response loss injection did not fire")
    key = stream_key(device, "qc", "g", 4)
    before = w.r.xlen(key)
    assert before == 1
    assert w.outbox.count() == 1
    state = dict(db_path=str(db_path), device=device, stream=key,
                 session_id=row["session_id"], seq=row["seq"], before_kill_stream_len=before,
                 before_kill_outbox=w.outbox.count())
    STATE.write_text(json.dumps(state), encoding="utf-8")
    w.close()
    print(json.dumps(state))


def second(url: str):
    state = json.loads(STATE.read_text(encoding="utf-8"))
    w = writer(url, Path(state["db_path"]))
    try:
        assert w.r.xlen(state["stream"]) == 1
        assert w.outbox.count() == 1
        assert w.outbox.replay_pending(w) == 1
        assert w.r.xlen(state["stream"]) == 1
        assert w.outbox.count() == 0
        receipt = "collector:receipt:v2:" + state["session_id"]
        assert w.r.hget(receipt, "high") == b"1"
        assert w.r.ttl(receipt) == -1
        result = dict(state, after_restart_stream_len=1, after_replay_stream_len=1,
                      after_replay_outbox=0, high=1, receipt_ttl=-1)
        print(json.dumps(result))
    finally:
        w.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=("first", "second"))
    parser.add_argument("--redis-url", required=True)
    args = parser.parse_args()
    if args.phase == "first":
        first(args.redis_url)
    else:
        second(args.redis_url)


if __name__ == "__main__":
    main()
