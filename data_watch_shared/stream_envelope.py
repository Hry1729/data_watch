"""Redis StreamEnvelope v1/v2 key and serialization contract."""

from __future__ import annotations

import zlib
from collections.abc import Mapping
from typing import Any

import msgspec

CONTRACT_VERSION_V1 = 1
CONTRACT_VERSION_V2 = 2


def shard_number(device_code: str, shard_count: int) -> int:
    if shard_count <= 0:
        raise ValueError("shard_count must be positive")
    return zlib.crc32(device_code.encode("utf-8")) % shard_count


def stream_key(device_code: str, device_type: str, group_code: str, shard_count: int) -> str:
    return f"rt:stream:{device_type}:{group_code}:{shard_number(device_code, shard_count)}"


def latest_key(device_code: str, group_code: str) -> str:
    return f"rt:latest:{device_code}:{group_code}"


def validate_envelope(
    *,
    device_code: str,
    device_type: str,
    group_code: str,
    scan_rate_ms: int,
    ts: int,
    config_version: int,
    values: Mapping[str, str],
    contract_version: int = CONTRACT_VERSION_V1,
    qualities: Mapping[str, int] | None = None,
) -> None:
    if not device_code or not device_type or not group_code:
        raise ValueError("device_code, device_type and group_code are required")
    if scan_rate_ms <= 0:
        raise ValueError("scan_rate_ms must be positive")
    if ts < 0 or config_version < 0:
        raise ValueError("ts and config_version must be non-negative")
    if contract_version not in (CONTRACT_VERSION_V1, CONTRACT_VERSION_V2):
        raise ValueError(f"unsupported contract_version: {contract_version}")
    if contract_version == CONTRACT_VERSION_V1:
        if qualities is not None:
            raise ValueError("qualities are only valid for contract_version=2")
        return
    if qualities is None:
        raise ValueError("qualities are required for contract_version=2")
    if {str(key) for key in values} != {str(key) for key in qualities}:
        raise ValueError("values and qualities keys must match")


def build_stream_fields(
    *,
    device_code: str,
    device_type: str,
    group_code: str,
    scan_rate_ms: int,
    ts: int,
    config_version: int,
    values: Mapping[str, str],
    qualities: Mapping[str, int] | None = None,
    validate: bool = False,
) -> dict[str, str | bytes]:
    contract_version = CONTRACT_VERSION_V2 if qualities is not None else CONTRACT_VERSION_V1
    if validate:
        validate_envelope(
            device_code=device_code,
            device_type=device_type,
            group_code=group_code,
            scan_rate_ms=scan_rate_ms,
            ts=ts,
            config_version=config_version,
            values=values,
            contract_version=contract_version,
            qualities=qualities,
        )
    fields: dict[str, str | bytes] = {
        "device_code": device_code,
        "device_type": device_type,
        "group_code": group_code,
        "scan_rate": str(scan_rate_ms),
        "ts": str(ts),
        "config_version": str(config_version),
        "values": msgspec.json.encode(values),
    }
    if qualities is not None:
        fields["contract_version"] = "2"
        fields["qualities"] = msgspec.json.encode(qualities)
    return fields


def build_latest_updates(
    values: Mapping[str, str],
    *,
    ts: int,
    assignment_epoch: int,
    qualities: Mapping[str, int] | None = None,
) -> dict[str, dict[str, Any]]:
    qualities = qualities or {}
    return {
        str(var_id): {
            "v": value,
            "t": ts,
            "q": int(qualities.get(str(var_id), qualities.get(var_id, 0))),
            "e": assignment_epoch,
        }
        for var_id, value in values.items()
    }


def encode_latest_mapping(fields: Mapping[str, Mapping[str, Any]]) -> dict[str, bytes]:
    return {str(var_id): msgspec.json.encode(value) for var_id, value in fields.items()}


def decode_latest_value(raw: str | bytes | bytearray) -> dict[str, Any]:
    value = msgspec.json.decode(raw)
    if not isinstance(value, dict):
        raise ValueError("latest value must be a JSON object")
    value.setdefault("q", 0)
    value.setdefault("e", 0)
    return value
