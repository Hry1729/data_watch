"""采集器 Redis JSON 负载的兼容性检查。"""

from __future__ import annotations

import json
import math
import random
import string

import msgspec


def _roundtrip_stdlib(value):
    return json.loads(json.dumps(value, separators=(",", ":")))


def _roundtrip_msgspec(value):
    return msgspec.json.decode(msgspec.json.encode(value))


def test_real_redis_payloads_are_semantically_equivalent():
    latest = {
        "1": {"v": "3.14", "t": 1_750_000_000_000, "q": 192},
        "2": {"v": "桥吊-\\\"状态\n正常", "t": 1_750_000_000_001, "q": 0},
        "3": {"v": "", "t": 0, "q": 255},
    }
    frame = {
        "device_code": "qc100",
        "group_code": "g001",
        "ts": 1_750_000_000_000,
        "scan_rate": 100,
        "values": {key: item["v"] for key, item in latest.items()},
    }

    for payload in (latest, frame, frame["values"]):
        assert _roundtrip_msgspec(payload) == _roundtrip_stdlib(payload)


def test_random_finite_json_values_are_semantically_equivalent():
    rng = random.Random(20260619)
    alphabet = string.ascii_letters + string.digits + "中文\\\"\n\t"

    for _ in range(500):
        value = {
            "text": "".join(rng.choice(alphabet) for _ in range(rng.randrange(0, 40))),
            "none": None,
            "bool": bool(rng.randrange(2)),
            "int": rng.randrange(-(2**53), 2**53),
            "float": rng.uniform(-1e12, 1e12),
            "items": [rng.randrange(-1000, 1000), rng.choice([None, True, False])],
        }
        assert _roundtrip_msgspec(value) == _roundtrip_stdlib(value)


def test_non_finite_floats_are_explicitly_outside_fast_path_contract():
    payload = {"nan": math.nan, "positive": math.inf, "negative": -math.inf}

    stdlib = _roundtrip_stdlib(payload)
    fast = _roundtrip_msgspec(payload)

    assert math.isnan(stdlib["nan"])
    assert math.isinf(stdlib["positive"])
    assert fast == {"nan": None, "positive": None, "negative": None}
