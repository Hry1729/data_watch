"""Shared contracts used by Data Watch runtime components."""

from .stream_envelope import (
    build_latest_updates,
    build_stream_fields,
    decode_latest_value,
    encode_latest_mapping,
    latest_key,
    shard_number,
    stream_key,
    validate_envelope,
)

__all__ = [
    "build_latest_updates",
    "build_stream_fields",
    "decode_latest_value",
    "encode_latest_mapping",
    "latest_key",
    "shard_number",
    "stream_key",
    "validate_envelope",
]
