from __future__ import annotations

import re
from typing import Any, Protocol

from cli.errors import CliError, EXIT_API, EXIT_USAGE


class RequestSession(Protocol):
    def request(
        self,
        method: str,
        path: str,
        *,
        params: Any = None,
        json_body: dict[str, Any] | None = None,
    ) -> Any: ...


class Resolver:
    def __init__(self, session: RequestSession) -> None:
        self.session = session
        self._catalog_devices: dict[str, dict[str, Any]] = {}
        self._history_devices: list[dict[str, Any]] | None = None
        self._history_points: dict[int, list[dict[str, Any]]] = {}
        self._monitor_devices: list[dict[str, Any]] | None = None
        self._business_point_searches: dict[tuple[str, str, int], dict[str, Any]] = {}

    def resolve_catalog_device(self, identifier: str | int) -> dict[str, Any]:
        normalized = self._require_identifier(identifier, "设备标识")
        if normalized.isdigit():
            return {"id": self._positive_int(normalized, "deviceId")}

        lookup_identifier = device_lookup_key(normalized)
        cache_key = lookup_identifier
        cached = self._catalog_devices.get(cache_key)
        if cached is not None:
            return cached

        payload = self.session.request(
            "GET",
            "/api/device-monitor/device/list",
            params={"keyword": lookup_identifier, "pageNo": 1, "pageSize": 50},
        )
        resolved = self._ranked_unique(
            self._page_items(payload),
            "设备",
            normalized,
            fields=("deviceCode", "deviceName", "groupName", "deviceType"),
            match_key=lookup_identifier,
        )
        self._catalog_devices[cache_key] = resolved
        return resolved

    def resolve_history_device(self, identifier: str | int) -> dict[str, Any]:
        normalized = self._require_identifier(identifier, "设备标识")
        devices = self._load_history_devices()
        if normalized.isdigit():
            device_id = self._positive_int(normalized, "deviceId")
            matches = [item for item in devices if int(item.get("id") or 0) == device_id]
            return self._unique(matches, "历史设备", normalized)
        lookup_identifier = device_lookup_key(normalized)
        return self._ranked_unique(
            devices,
            "历史设备",
            normalized,
            fields=("deviceCode", "deviceName", "name", "groupName", "typeName"),
            match_key=lookup_identifier,
        )

    def history_points_for_device(self, device_id: int) -> list[dict[str, Any]]:
        if device_id not in self._history_points:
            payload = self.session.request(
                "GET",
                "/api/device-monitor/history/points",
                params={"deviceId": device_id},
            )
            if not isinstance(payload, list):
                raise CliError("api_error", "历史点位响应格式错误", EXIT_API)
            self._history_points[device_id] = [
                item for item in payload if isinstance(item, dict)
            ]
        return self._history_points[device_id]

    def resolve_history_points(
        self,
        device_id: int,
        identifiers: list[str],
    ) -> list[dict[str, Any]]:
        if not identifiers:
            raise CliError("usage_error", "至少需要一个 --point", EXIT_USAGE)
        points = self.history_points_for_device(device_id)
        resolved: list[dict[str, Any]] = []
        seen: set[int] = set()
        for raw_identifier in identifiers:
            identifier = self._require_identifier(raw_identifier, "点位标识")
            if identifier.isdigit():
                var_seq = self._positive_int(identifier, "varSeq")
                matches = [
                    item for item in points if int(item.get("varSeq") or 0) == var_seq
                ]
                point = self._unique(matches, "历史点位", identifier)
            else:
                point = self._ranked_unique(
                    points,
                    "历史点位",
                    identifier,
                    fields=("pointName", "ioAddress"),
                )
            var_seq = int(point["varSeq"])
            if var_seq not in seen:
                resolved.append(point)
                seen.add(var_seq)
        return resolved

    def resolve_monitor_device(self, identifier: str | int) -> dict[str, Any]:
        normalized = self._require_identifier(identifier, "设备标识")
        devices = self._load_monitor_devices()
        if normalized.isdigit():
            device_id = self._positive_int(normalized, "deviceId")
            matches = [
                item for item in devices if int(item.get("deviceId") or 0) == device_id
            ]
            return self._unique(matches, "监控设备", normalized)
        lookup_identifier = device_lookup_key(normalized)
        return self._ranked_unique(
            devices,
            "监控设备",
            normalized,
            fields=("deviceCode", "deviceName", "name", "groupName"),
            match_key=lookup_identifier,
        )

    def search_business_points(
        self,
        query: str,
        *,
        device_hint: str | None = None,
        limit: int = 20,
    ) -> dict[str, Any]:
        normalized_query = self._require_identifier(query, "点位标识")
        normalized_hint = str(device_hint or "").strip()
        device_hint_key = device_lookup_key(normalized_hint)
        cache_key = (_lookup_key(normalized_query), device_hint_key, limit)
        cached = self._business_point_searches.get(cache_key)
        if cached is not None:
            return cached

        params: dict[str, Any] = {"query": normalized_query, "limit": limit}
        if device_hint_key:
            params["deviceHint"] = device_hint_key
        payload = self.session.request(
            "GET",
            "/api/device-monitor/monitor/point-resolve",
            params=params,
        )
        if not isinstance(payload, dict) or not isinstance(payload.get("items"), list):
            raise CliError("api_error", "点位解析响应格式错误", EXIT_API)
        result = {
            **payload,
            "deviceHint": normalized_hint or None,
            "items": [item for item in payload["items"] if isinstance(item, dict)],
        }
        self._business_point_searches[cache_key] = result
        return result

    def resolve_business_points(
        self,
        identifiers: list[str],
        *,
        device_hint: str | None = None,
    ) -> tuple[dict[str, Any], list[dict[str, Any]]]:
        if not identifiers:
            raise CliError("usage_error", "至少需要一个 --point", EXIT_USAGE)

        points: list[dict[str, Any]] = []
        seen: set[int] = set()
        for identifier in identifiers:
            payload = self.search_business_points(identifier, device_hint=device_hint)
            point = self._select_business_point(payload, identifier, device_hint)
            point_id = int(point["pointId"])
            if point_id not in seen:
                points.append(point)
                seen.add(point_id)

        device_ids = {int(point["deviceId"]) for point in points}
        if len(device_ids) != 1:
            raise CliError(
                "ambiguous_resource",
                "所选点位属于不同设备，请增加 --device 业务名称限定",
                EXIT_API,
                status=409,
                details={"candidates": [self._candidate_summary(point) for point in points]},
            )

        first = points[0]
        device = {
            "id": int(first["deviceId"]),
            "deviceId": int(first["deviceId"]),
            "deviceCode": str(first["deviceCode"]),
            "deviceName": str(first.get("deviceName") or first["deviceCode"]),
            "groupName": str(first.get("deviceGroupName") or ""),
            "typeName": str(first.get("deviceTypeName") or ""),
        }
        return device, points

    def _load_history_devices(self) -> list[dict[str, Any]]:
        if self._history_devices is None:
            payload = self.session.request(
                "GET", "/api/device-monitor/history/devices"
            )
            if not isinstance(payload, list):
                raise CliError("api_error", "历史设备响应格式错误", EXIT_API)
            self._history_devices = [item for item in payload if isinstance(item, dict)]
        return self._history_devices

    def _load_monitor_devices(self) -> list[dict[str, Any]]:
        if self._monitor_devices is None:
            payload = self.session.request(
                "GET", "/api/device-monitor/monitor/device-tree"
            )
            self._monitor_devices = self._flatten_devices(payload)
        return self._monitor_devices

    @classmethod
    def _flatten_devices(cls, payload: Any) -> list[dict[str, Any]]:
        devices: list[dict[str, Any]] = []
        if not isinstance(payload, list):
            return devices
        stack: list[tuple[Any, str]] = [(item, "") for item in payload]
        while stack:
            item, parent_name = stack.pop(0)
            if not isinstance(item, dict):
                continue
            current_name = str(item.get("name") or "")
            children = item.get("children")
            if isinstance(children, list):
                stack[0:0] = [(child, current_name or parent_name) for child in children]
            if item.get("nodeType") == "device" or item.get("deviceId") is not None:
                devices.append({**item, "groupName": item.get("groupName") or parent_name})
        return devices

    @classmethod
    def _select_business_point(
        cls,
        payload: dict[str, Any],
        identifier: str,
        device_hint: str | None,
    ) -> dict[str, Any]:
        items = [item for item in payload.get("items", []) if isinstance(item, dict)]
        if not items:
            raise CliError(
                "resource_not_found",
                f"未找到业务点位: {identifier}",
                EXIT_API,
                status=404,
                details={"query": identifier, "deviceHint": device_hint},
            )
        best_rank = min(int(item.get("matchRank") or 0) for item in items)
        matches = [item for item in items if int(item.get("matchRank") or 0) == best_rank]
        if len(matches) != 1:
            raise CliError(
                "ambiguous_resource",
                f"业务点位标识不唯一: {identifier}",
                EXIT_API,
                status=409,
                details={
                    "query": identifier,
                    "deviceHint": device_hint,
                    "candidates": [cls._candidate_summary(item) for item in matches[:20]],
                },
            )
        return matches[0]

    @staticmethod
    def _candidate_summary(item: dict[str, Any]) -> dict[str, Any]:
        return {
            "deviceName": item.get("deviceName"),
            "pointName": item.get("pointName"),
            "ioAddress": item.get("ioAddress"),
            "description": item.get("description"),
            "deviceCode": item.get("deviceCode"),
            "varSeq": item.get("varSeq"),
        }

    @staticmethod
    def _page_items(payload: Any) -> list[dict[str, Any]]:
        if not isinstance(payload, dict):
            return []
        raw_items = payload.get("list") or payload.get("records") or []
        if not isinstance(raw_items, list):
            return []
        return [item for item in raw_items if isinstance(item, dict)]

    @staticmethod
    def _require_identifier(identifier: str | int, label: str) -> str:
        normalized = str(identifier).strip()
        if not normalized:
            raise CliError("usage_error", f"{label}不能为空", EXIT_USAGE)
        return normalized

    @staticmethod
    def _positive_int(value: str, label: str) -> int:
        parsed = int(value)
        if parsed <= 0:
            raise CliError("usage_error", f"{label} 必须为正整数", EXIT_USAGE)
        return parsed

    @classmethod
    def _ranked_unique(
        cls,
        items: list[dict[str, Any]],
        resource_name: str,
        identifier: str,
        *,
        fields: tuple[str, ...],
        match_key: str | None = None,
    ) -> dict[str, Any]:
        query_key = match_key or _lookup_key(identifier)
        ranked: list[tuple[int, dict[str, Any]]] = []
        for item in items:
            field_ranks = [
                rank
                for field in fields
                if (rank := _value_rank(query_key, item.get(field))) is not None
            ]
            if field_ranks:
                ranked.append((min(field_ranks), item))
        if not ranked:
            return cls._unique([], resource_name, identifier)
        best_rank = min(rank for rank, _item in ranked)
        return cls._unique(
            [item for rank, item in ranked if rank == best_rank],
            resource_name,
            identifier,
        )

    @staticmethod
    def _unique(
        matches: list[dict[str, Any]],
        resource_name: str,
        identifier: str,
    ) -> dict[str, Any]:
        if not matches:
            raise CliError(
                "resource_not_found",
                f"{resource_name}不存在: {identifier}",
                EXIT_API,
                status=404,
            )
        if len(matches) > 1:
            raise CliError(
                "ambiguous_resource",
                f"{resource_name}标识不唯一: {identifier}",
                EXIT_API,
                status=409,
                details={
                    "candidates": [Resolver._candidate_summary(item) for item in matches[:20]]
                },
            )
        return matches[0]


def _lookup_key(value: Any) -> str:
    return "".join(
        character
        for character in str(value or "").casefold()
        if not character.isspace() and character not in {"_", "-"}
    )


def device_lookup_key(value: Any) -> str:
    normalized = _lookup_key(value)
    for source, replacement in (
        ("轨道吊", "asc"),
        ("桥吊", "qc"),
        ("陆侧", "l"),
        ("海侧", "s"),
    ):
        normalized = normalized.replace(source, replacement)
    side_match = re.fullmatch(r"asc(\d+)([ls])", normalized)
    if side_match:
        side_code = "1" if side_match.group(2) == "l" else "2"
        return f"asc{side_match.group(1)}{side_code}"
    return normalized


def _value_rank(query_key: str, value: Any) -> int | None:
    value_key = _lookup_key(value)
    if not value_key:
        return None
    if value_key == query_key:
        return 0
    if value_key.startswith(query_key):
        return 1
    if query_key in value_key:
        return 2
    return None
