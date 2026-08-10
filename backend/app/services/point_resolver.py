from __future__ import annotations

from typing import Any

from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import ApiError


class PointResolverService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session

    async def search(
        self,
        query: str,
        *,
        device_hint: str | None = None,
        limit: int = 20,
    ) -> dict[str, Any]:
        query_text = str(query or "").strip()
        query_key = _lookup_key(query_text)
        if not query_key:
            raise ApiError(400, "query is required")
        if limit < 1 or limit > 50:
            raise ApiError(400, "limit must be between 1 and 50")

        hint_text = str(device_hint or "").strip()
        hint_key = _lookup_key(hint_text)
        rows = await self._fetch_rows(query_key, hint_key)

        candidates: list[dict[str, Any]] = []
        for row in rows:
            point_rank, point_fields = _match_rank(
                query_key,
                {
                    "pointName": row.get("point_name"),
                    "ioAddress": row.get("io_address"),
                    "varSeq": row.get("var_seq"),
                    "description": row.get("description"),
                },
            )
            if point_rank is None:
                continue

            device_rank = 0
            device_fields: list[str] = []
            if hint_key:
                ranked = _match_rank(
                    hint_key,
                    {
                        "deviceName": row.get("device_name"),
                        "deviceCode": row.get("device_code"),
                        "deviceGroupName": row.get("device_group_name"),
                        "deviceTypeName": row.get("device_type_name"),
                    },
                )
                if ranked[0] is None:
                    continue
                device_rank, device_fields = ranked

            candidates.append(
                {
                    "pointId": int(row["point_id"]),
                    "deviceId": int(row["device_id"]),
                    "deviceCode": str(row["device_code"]),
                    "deviceName": str(row.get("device_name") or row["device_code"]),
                    "deviceGroupName": str(row.get("device_group_name") or ""),
                    "deviceTypeName": str(row.get("device_type_name") or ""),
                    "varSeq": int(row["var_seq"]),
                    "pointName": str(row.get("point_name") or row["var_seq"]),
                    "ioAddress": str(row.get("io_address") or ""),
                    "description": str(row.get("description") or ""),
                    "groupCode": str(row.get("point_group_code") or ""),
                    "groupName": str(row.get("point_group_name") or ""),
                    "pointStatus": "enabled" if int(row.get("point_enabled") or 0) == 1 else "disabled",
                    "deviceStatus": "enabled" if int(row.get("device_enabled") or 0) == 1 else "disabled",
                    "matchRank": point_rank * 10 + device_rank,
                    "matchedFields": point_fields + device_fields,
                }
            )

        candidates.sort(
            key=lambda item: (
                int(item["matchRank"]),
                str(item["deviceName"]).casefold(),
                str(item["pointName"]).casefold(),
                int(item["pointId"]),
            )
        )
        best_rank = int(candidates[0]["matchRank"]) if candidates else None
        best_match_count = sum(
            1 for item in candidates if int(item["matchRank"]) == best_rank
        )
        return {
            "query": query_text,
            "deviceHint": hint_text or None,
            "matchCount": len(candidates),
            "bestMatchCount": best_match_count,
            "truncated": len(candidates) > limit,
            "items": candidates[:limit],
        }

    async def _fetch_rows(
        self,
        query_key: str,
        device_hint_key: str,
    ) -> list[dict[str, Any]]:
        normalize = (
            "LOWER(REPLACE(REPLACE(REPLACE(COALESCE({field}, ''), ' ', ''), '_', ''), '-', ''))"
        )
        point_fields = (
            normalize.format(field="dv.var_name"),
            normalize.format(field="dv.var_address"),
            normalize.format(field="dv.description"),
        )
        device_fields = (
            normalize.format(field="d.device_code"),
            normalize.format(field="d.device_name"),
            normalize.format(field="dg.group_name"),
            normalize.format(field="dt.type_name"),
        )
        point_where = " OR ".join(
            [*(f"{field} LIKE :query_like" for field in point_fields), "CAST(dv.var_seq AS CHAR) = :query_key"]
        )
        device_where = " OR ".join(
            f"{field} LIKE :device_hint_like" for field in device_fields
        )
        statement = text(
            f"""
            SELECT
                dv.id AS point_id,
                dv.device_id AS device_id,
                d.device_code AS device_code,
                COALESCE(d.device_name, d.device_code) AS device_name,
                COALESCE(dg.group_name, '') AS device_group_name,
                COALESCE(dt.type_name, dt.type_code, '') AS device_type_name,
                dv.var_seq AS var_seq,
                COALESCE(dv.var_name, CAST(dv.var_seq AS CHAR), '') AS point_name,
                COALESCE(dv.var_address, '') AS io_address,
                COALESCE(dv.description, '') AS description,
                COALESCE(dpg.group_code, '') AS point_group_code,
                COALESCE(dpg.group_name, dpg.group_code, '') AS point_group_name,
                COALESCE(dv.enable, 0) AS point_enabled,
                COALESCE(d.enable, 0) AS device_enabled
            FROM device_variable AS dv
            JOIN device AS d ON d.id = dv.device_id
            LEFT JOIN device_point_group AS dpg ON dpg.id = dv.point_group_id
            LEFT JOIN device_group AS dg ON dg.id = d.group_id
            LEFT JOIN device_type AS dt ON dt.id = d.type_id
            WHERE COALESCE(dv.deleted, 0) = 0
              AND dv.var_seq IS NOT NULL
              AND dv.var_seq > 0
              AND ({point_where})
              AND (:has_device_hint = 0 OR ({device_where}))
            ORDER BY dv.id ASC
            """
        )
        rows = (
            await self.session.execute(
                statement,
                {
                    "query_key": query_key,
                    "query_like": f"%{query_key}%",
                    "has_device_hint": 1 if device_hint_key else 0,
                    "device_hint_like": f"%{device_hint_key}%",
                },
            )
        ).mappings().all()
        return [dict(row) for row in rows]


def _lookup_key(value: Any) -> str:
    return "".join(
        character
        for character in str(value or "").casefold()
        if not character.isspace() and character not in {"_", "-"}
    )


def _match_rank(
    query_key: str,
    fields: dict[str, Any],
) -> tuple[int | None, list[str]]:
    ranked: list[tuple[int, str]] = []
    for field_name, raw_value in fields.items():
        value = _lookup_key(raw_value)
        if not value:
            continue
        if value == query_key:
            ranked.append((0, field_name))
        elif value.startswith(query_key):
            ranked.append((1, field_name))
        elif query_key in value:
            ranked.append((2, field_name))
    if not ranked:
        return None, []
    best_rank = min(rank for rank, _field in ranked)
    return best_rank, [field for rank, field in ranked if rank == best_rank]
