from __future__ import annotations

from dataclasses import dataclass
from typing import Any
from uuid import UUID


class RequestValidationError(ValueError):
    pass


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

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> "UploadItem":
        batch_id = _required_text(payload, "batch_id")
        upload_session_id = _required_uuid(payload, "upload_session_id")
        local_seq = _required_positive_int(payload, "local_seq")

        envelope_error = None
        try:
            assignment_epoch = _required_non_negative_int(payload, "assignment_epoch")
            device_code = _required_text(payload, "device_code")
            device_type = _required_text(payload, "device_type").lower()
            group_code = _required_text(payload, "group_code")
            scan_rate = _required_positive_int(payload, "scan_rate")
            ts = _required_non_negative_int(payload, "ts")
            config_version = _required_non_negative_int(payload, "config_version")
            contract_version = int(payload.get("contract_version", 1))
            values = _string_map(payload.get("values"), "values")
            qualities = (
                _int_map(payload.get("qualities"), "qualities")
                if "qualities" in payload
                else None
            )
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
        items = tuple(UploadItem.from_dict(item) for item in raw_items if isinstance(item, dict))
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
