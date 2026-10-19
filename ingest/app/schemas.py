from __future__ import annotations

from dataclasses import dataclass
from typing import Any
from uuid import UUID

MAX_TIMESTAMP_MS = 253402300799999


class RequestValidationError(ValueError):
    # 400 的作用范围：``batch``（与具体记录无关）或 ``item``（某条记录本身非法）。
    scope = "batch"


class ItemValidationError(RequestValidationError):
    scope = "item"


@dataclass(frozen=True, slots=True)
class UploadItem:
    batch_id: str
    upload_session_id: str
    local_seq: int
    assignment_epoch: int
    device_code: str
    device_type: str
    group_code: str
    scan_rate: int
    ts: int
    config_version: int
    values: dict[str, str]
    contract_version: int = 1
    qualities: dict[str, int] | None = None
    envelope_error: str | None = None
    latest_values: dict[str, str] | None = None
    latest_qualities: dict[str, int] | None = None
    latest_timestamps: dict[str, int] | None = None

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> "UploadItem":
        batch_id = _required_text(payload, "batch_id")
        upload_session_id = _required_uuid(payload, "upload_session_id")
        local_seq = _required_positive_int(payload, "local_seq")

        envelope_error = None
        latest_values = None
        latest_qualities = None
        latest_timestamps = None
        try:
            assignment_epoch = _required_non_negative_int(payload, "assignment_epoch")
            device_code = _required_text(payload, "device_code")
            device_type = _required_text(payload, "device_type").lower()
            group_code = _required_text(payload, "group_code")
            scan_rate = _required_positive_int(payload, "scan_rate")
            ts = _required_non_negative_int(payload, "ts")
            if ts > MAX_TIMESTAMP_MS:
                raise RequestValidationError("invalid timestamp range")
            config_version = _required_non_negative_int(payload, "config_version")
            contract_version = int(payload.get("contract_version", 1))
            values = _string_map(payload.get("values"), "values")
            qualities = (
                _int_map(payload.get("qualities"), "qualities")
                if "qualities" in payload
                else None
            )
            if "latest_values" in payload:
                latest_values = _string_map(payload.get("latest_values"), "latest_values")
            if "latest_qualities" in payload:
                latest_qualities = _int_map(payload.get("latest_qualities"), "latest_qualities")
            if "latest_timestamps" in payload:
                latest_timestamps = _int_map(payload["latest_timestamps"], "latest_timestamps")
            # 高-3：坏质量且无值的点只出现在 latest_qualities / latest_timestamps（仅更新质量），
            # 因此要求 latest_values 的键是它们的子集，且两者键集合一致。
            if (
                latest_qualities is not None
                and latest_timestamps is not None
                and set(latest_qualities) != set(latest_timestamps)
            ):
                raise RequestValidationError("latest_qualities and latest_timestamps keys must match")
            for label, mapping in (("latest_qualities", latest_qualities), ("latest_timestamps", latest_timestamps)):
                if mapping is not None and (latest_values is None or not set(latest_values) <= set(mapping)):
                    raise RequestValidationError(f"{label} keys must cover latest_values")
            if latest_qualities is not None and any(v < 0 or v > 65535 for v in latest_qualities.values()):
                raise RequestValidationError("invalid latest quality")
            if latest_timestamps is not None and any(v < 0 or v > MAX_TIMESTAMP_MS for v in latest_timestamps.values()):
                raise RequestValidationError("invalid latest timestamp")

        except (RequestValidationError, TypeError, ValueError) as exc:
            assignment_epoch = _safe_int(payload.get("assignment_epoch"))
            device_code = str(payload.get("device_code") or "")
            device_type = str(payload.get("device_type") or "").lower()
            group_code = str(payload.get("group_code") or "")
            scan_rate = _safe_int(payload.get("scan_rate"))
            ts = _safe_int(payload.get("ts"))
            config_version = _safe_int(payload.get("config_version"))
            contract_version = _safe_int(payload.get("contract_version"), 1)
            values = payload.get("values") if isinstance(payload.get("values"), dict) else {}
            qualities = None
            envelope_error = str(exc)

        return cls(
            batch_id=batch_id,
            upload_session_id=upload_session_id,
            local_seq=local_seq,
            assignment_epoch=assignment_epoch,
            device_code=device_code,
            device_type=device_type,
            group_code=group_code,
            scan_rate=scan_rate,
            ts=ts,
            config_version=config_version,
            values={str(key): str(value) for key, value in values.items()},
            contract_version=contract_version,
            qualities=(
                {str(key): int(value) for key, value in qualities.items()}
                if qualities is not None
                else None
            ),
            envelope_error=envelope_error,
            latest_values=latest_values,
            latest_qualities=latest_qualities,
            latest_timestamps=latest_timestamps,
        )


@dataclass(frozen=True, slots=True)
class UploadBatch:
    node_uuid: str
    request_id: str
    items: tuple[UploadItem, ...]

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> "UploadBatch":
        node_uuid = _required_uuid(payload, "node_uuid")
        request_id = _required_text(payload, "request_id")
        raw_items = payload.get("items")
        if not isinstance(raw_items, list) or not raw_items:
            raise RequestValidationError("items must be a non-empty array")
        try:
            items = tuple(UploadItem.from_dict(item) for item in raw_items if isinstance(item, dict))
        except RequestValidationError as exc:
            raise ItemValidationError(str(exc)) from exc
        if len(items) != len(raw_items):
            raise RequestValidationError("each item must be an object")
        sessions = {item.upload_session_id for item in items}
        if len(sessions) != 1:
            raise RequestValidationError("one request may contain only one upload_session_id")
        seqs = [item.local_seq for item in items]
        if len(set(seqs)) != len(seqs):
            raise RequestValidationError("local_seq must be unique within a request")
        return cls(node_uuid=node_uuid, request_id=request_id, items=items)


@dataclass(frozen=True, slots=True)
class AckResult:
    batch_id: str
    upload_session_id: str
    local_seq: int
    status: str
    reason: str | None = None

    def to_dict(self) -> dict[str, Any]:
        data = {
            "batch_id": self.batch_id,
            "upload_session_id": self.upload_session_id,
            "local_seq": self.local_seq,
            "status": self.status,
        }
        if self.status in {"ack", "duplicate", "stale_config_warning"}:
            data["durability"] = "redis-aof"
        if self.reason:
            data["reason"] = self.reason
        return data


@dataclass(frozen=True, slots=True)
class SessionSwitch:
    node_uuid: str
    old_session_id: str
    new_session_id: str

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> "SessionSwitch":
        return cls(
            node_uuid=_required_uuid(payload, "node_uuid"),
            old_session_id=_required_uuid(payload, "old_session_id"),
            new_session_id=_required_uuid(payload, "new_session_id"),
        )


def _required_text(payload: dict[str, Any], name: str) -> str:
    value = str(payload.get(name) or "").strip()
    if not value:
        raise RequestValidationError(f"{name} is required")
    return value


def _required_uuid(payload: dict[str, Any], name: str) -> str:
    value = _required_text(payload, name)
    try:
        return str(UUID(value))
    except ValueError as exc:
        raise RequestValidationError(f"{name} must be a UUID") from exc


def _required_positive_int(payload: dict[str, Any], name: str) -> int:
    value = _safe_int(payload.get(name))
    if value <= 0:
        raise RequestValidationError(f"{name} must be a positive integer")
    return value


def _required_non_negative_int(payload: dict[str, Any], name: str) -> int:
    value = _safe_int(payload.get(name), -1)
    if value < 0:
        raise RequestValidationError(f"{name} must be a non-negative integer")
    return value


def _safe_int(value: Any, default: int = 0) -> int:
    try:
        if isinstance(value, bool):
            raise ValueError
        return int(value)
    except (TypeError, ValueError):
        return default


def _string_map(value: Any, name: str) -> dict[str, str]:
    if not isinstance(value, dict):
        raise RequestValidationError(f"{name} must be an object")
    return {str(key): str(item) for key, item in value.items()}


def _int_map(value: Any, name: str) -> dict[str, int]:
    if not isinstance(value, dict):
        raise RequestValidationError(f"{name} must be an object")
    try:
        return {str(key): int(item) for key, item in value.items()}
    except (TypeError, ValueError) as exc:
        raise RequestValidationError(f"{name} values must be integers") from exc
