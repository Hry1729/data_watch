from __future__ import annotations

import asyncio
import logging
import zlib

from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from time import monotonic
from typing import Protocol

import aiomysql

from .config import IngestSettings
from .migrations import migrate_distributed_schema
from .schemas import UploadItem


_CENTER_TIMEZONE = timezone(timedelta(hours=8))
_TOUCH_INTERVAL_SECONDS = 5.0
logger = logging.getLogger("ingest.repository")


@dataclass(frozen=True, slots=True)
class ValidationDecision:
    action: str
    status_override: str | None = None
    reason: str | None = None

    @classmethod
    def accept(cls, status_override: str | None = None) -> "ValidationDecision":
        return cls(action="accept", status_override=status_override)

    @classmethod
    def reject(cls, reason: str) -> "ValidationDecision":
        return cls(
            action="consume_reject",
            status_override="rejected_consumed",
            reason=reason,
        )


class IngestRepository(Protocol):
    async def get_approved_node_public_keys(self, node_uuid: str) -> list[str]: ...
    async def validate_item(
        self, node_uuid: str, item: UploadItem
    ) -> ValidationDecision: ...
    async def validate_items(
        self, node_uuid: str, items: list[UploadItem]
    ) -> list[ValidationDecision]: ...
    async def touch_node(self, node_uuid: str) -> None: ...

class MySQLIngestRepository:
    def __init__(
        self,
        pool: aiomysql.Pool,
        *,
        control_cache_ttl_seconds: float = 1.0,
    ) -> None:
        self._pool = pool
        self._last_touch: dict[str, float] = {}
        self._touch_tasks: set[asyncio.Task[None]] = set()
        self._control_cache_ttl_seconds = control_cache_ttl_seconds
        self._public_key_cache: dict[str, tuple[float, list[str]]] = {}
        self._assignment_cache: dict[
            tuple[str, str, int], tuple[float, dict | None]
        ] = {}
        self._mapping_cache: dict[
            tuple[str, str], tuple[float, frozenset[int]]
        ] = {}

    @classmethod
    async def create(cls, settings: IngestSettings) -> "MySQLIngestRepository":
        pool = await aiomysql.create_pool(
            host=settings.mysql_host,
            port=settings.mysql_port,
            user=settings.mysql_user,
            password=settings.mysql_password,
            db=settings.mysql_database,
            charset="utf8mb4",
            autocommit=True,
            minsize=1,
            maxsize=settings.mysql_pool_maxsize,
        )
        try:
            await migrate_distributed_schema(pool)
        except Exception:
            pool.close()
            await pool.wait_closed()
            raise
        return cls(
            pool,
            control_cache_ttl_seconds=settings.control_cache_ttl_ms / 1000,
        )

    async def close(self) -> None:
        if self._touch_tasks:
            await asyncio.gather(*tuple(self._touch_tasks))
        self._pool.close()
        await self._pool.wait_closed()

    def invalidate_control_cache(self, event: dict) -> None:
        scope = str(event.get("scope") or "")
        if scope == "all":
            self._public_key_cache.clear()
            self._assignment_cache.clear()
            self._mapping_cache.clear()
            return
        if scope == "node":
            node_uuid = str(event.get("node_uuid") or "")
            if node_uuid:
                self._public_key_cache.pop(node_uuid, None)
            if event.get("assignments"):
                self._assignment_cache.clear()
            return
        if scope == "assignments":
            self._assignment_cache.clear()
            return
        if scope == "mappings":
            self._mapping_cache.clear()
    async def get_approved_node_public_keys(self, node_uuid: str) -> list[str]:
        now = monotonic()
        cached = self._public_key_cache.get(node_uuid)
        if cached is not None and cached[0] > now:
            return list(cached[1])

        row = await self._fetchone(
            """
            SELECT public_key_pem, previous_public_key_pem,
                   previous_key_expires_at
            FROM sys_node
            WHERE node_uuid = %s AND status = 'approved'
            """,
            (node_uuid,),
        )
        keys: list[str] = []
        if row:
            current = row.get("public_key_pem")
            if current:
                keys.append(str(current))
            previous = row.get("previous_public_key_pem")
            expires_at = row.get("previous_key_expires_at")
            center_now = datetime.now(_CENTER_TIMEZONE).replace(tzinfo=None)
            if previous and expires_at is not None and expires_at >= center_now:
                keys.append(str(previous))
        self._public_key_cache[node_uuid] = (
            self._cache_expiry("node", node_uuid),
            keys,
        )
        return list(keys)
    async def touch_node(self, node_uuid: str) -> None:
        now = monotonic()
        if now - self._last_touch.get(node_uuid, 0.0) < _TOUCH_INTERVAL_SECONDS:
            return
        self._last_touch[node_uuid] = now
        task = asyncio.create_task(self._touch_node_in_background(node_uuid, now))
        self._touch_tasks.add(task)
        task.add_done_callback(self._touch_tasks.discard)

    async def _touch_node_in_background(self, node_uuid: str, touched_at: float) -> None:
        try:
            await self._update_last_seen(node_uuid)
        except Exception:
            if self._last_touch.get(node_uuid) == touched_at:
                self._last_touch.pop(node_uuid, None)
            logger.warning("failed to update node last_seen_at: %s", node_uuid, exc_info=True)

    async def _update_last_seen(self, node_uuid: str) -> None:
        async with self._pool.acquire() as connection:
            async with connection.cursor() as cursor:
                await cursor.execute(
                    "UPDATE sys_node SET last_seen_at = CURRENT_TIMESTAMP(3) WHERE node_uuid = %s",
                    (node_uuid,),
                )

    async def validate_item(
        self, node_uuid: str, item: UploadItem
    ) -> ValidationDecision:
        return (await self.validate_items(node_uuid, [item]))[0]

    async def validate_items(
        self, node_uuid: str, items: list[UploadItem]
    ) -> list[ValidationDecision]:
        if not items:
            return []

        decisions: list[ValidationDecision | None] = [None] * len(items)
        valid: list[tuple[int, UploadItem, set[int]]] = []
        assignment_keys: set[tuple[str, str, int]] = set()
        mapping_points: dict[tuple[str, str], set[int]] = {}

        for index, item in enumerate(items):
            if item.envelope_error:
                decisions[index] = ValidationDecision.reject(
                    f"malformed_envelope:{item.envelope_error}"
                )
                continue
            point_ids = self._point_ids(item)
            if point_ids is None:
                decisions[index] = ValidationDecision.reject("malformed_envelope:var_id")
                continue
            valid.append((index, item, point_ids))
            assignment_keys.add(
                (item.device_code, item.group_code, item.assignment_epoch)
            )
            if point_ids:
                mapping_points.setdefault(
                    (item.device_code, item.group_code), set()
                ).update(point_ids)

        assignments = await self._assignment_rows(assignment_keys)
        mapped = await self._mapping_point_sets(set(mapping_points))

        for index, item, point_ids in valid:
            row = assignments.get(
                (item.device_code, item.group_code, item.assignment_epoch)
            )
            decisions[index] = self._validate_item_row(
                node_uuid,
                item,
                row,
                mapped.get((item.device_code, item.group_code), set()),
                point_ids,
            )

        return [
            decision or ValidationDecision.reject("malformed_envelope")
            for decision in decisions
        ]

    async def _assignment_rows(
        self, keys: set[tuple[str, str, int]]
    ) -> dict[tuple[str, str, int], dict | None]:
        now = monotonic()
        result: dict[tuple[str, str, int], dict | None] = {}
        missing: set[tuple[str, str, int]] = set()
        for key in keys:
            cached = self._assignment_cache.get(key)
            if cached is not None and cached[0] > now:
                result[key] = cached[1]
            else:
                missing.add(key)

        if missing:
            rows = await self._fetch_assignment_rows(missing)
            fetched = {
                (
                    str(row.get("device_code") or ""),
                    str(row.get("group_code") or ""),
                    int(row.get("assignment_epoch") or 0),
                ): row
                for row in rows
            }
            for key in missing:
                value = fetched.get(key)
                self._assignment_cache[key] = (
                    self._cache_expiry("assignment", key),
                    value,
                )
                result[key] = value
        return result

    async def _mapping_point_sets(
        self, keys: set[tuple[str, str]]
    ) -> dict[tuple[str, str], set[int]]:
        now = monotonic()
        result: dict[tuple[str, str], set[int]] = {}
        missing: set[tuple[str, str]] = set()
        for key in keys:
            cached = self._mapping_cache.get(key)
            if cached is not None and cached[0] > now:
                result[key] = set(cached[1])
            else:
                missing.add(key)

        if missing:
            rows = await self._fetch_mapping_rows(missing)
            fetched: dict[tuple[str, str], set[int]] = {
                key: set() for key in missing
            }
            for row in rows:
                key = (
                    str(row.get("device_code") or ""),
                    str(row.get("group_code") or ""),
                )
                if key in fetched:
                    fetched[key].add(int(row["point_id"]))
            for key in missing:
                value = frozenset(fetched[key])
                self._mapping_cache[key] = (
                    self._cache_expiry("mapping", key),
                    value,
                )
                result[key] = set(value)
        return result

    def _cache_expiry(self, namespace: str, key: object) -> float:
        checksum = zlib.crc32(f"{namespace}:{key!r}".encode("utf-8"))
        jitter = 0.8 + (checksum % 4001) / 10000
        return monotonic() + self._control_cache_ttl_seconds * jitter

    async def _fetch_assignment_rows(
        self, keys: set[tuple[str, str, int]]
    ) -> list[dict]:
        if not keys:
            return []
        clauses = []
        params: list[object] = []
        for device_code, group_code, assignment_epoch in sorted(keys):
            clauses.append(
                "(d.device_code=%s AND dpg.group_code=%s AND da.assignment_epoch=%s)"
            )
            params.extend((device_code, group_code, assignment_epoch))
        return await self._fetchall(
            f"""
            SELECT
                d.id AS device_id,
                d.device_code,
                dpg.group_code,
                LOWER(dt.type_code) AS device_type,
                dpg.config_version AS current_config_version,
                da.node_uuid,
                da.assignment_epoch,
                da.cutover_at,
                da.grace_until,
                da.status AS assignment_status
            FROM device d
            JOIN device_type dt ON dt.id = d.type_id
            JOIN device_point_group dpg ON dpg.device_id = d.id
            JOIN device_assignment da ON da.device_id = d.id
            WHERE {" OR ".join(clauses)}
            """,
            tuple(params),
        )

    async def _fetch_mapping_rows(
        self, keys: set[tuple[str, str]]
    ) -> list[dict]:
        if not keys:
            return []
        clauses = []
        params: list[object] = []
        for device_code, group_code in sorted(keys):
            clauses.append("(device_code=%s AND group_code=%s)")
            params.extend((device_code, group_code))
        return await self._fetchall(
            f"""
            SELECT device_code, group_code, point_id
            FROM point_storage_mapping
            WHERE {" OR ".join(clauses)}
            """,
            tuple(params),
        )
    def _validate_item_row(
        self,
        node_uuid: str,
        item: UploadItem,
        row: dict | None,
        mapped_point_ids: set[int],
        point_ids: set[int],
    ) -> ValidationDecision:
        if not row:
            return ValidationDecision.reject("unknown_device")
        if str(row.get("device_type") or "").lower() != item.device_type.lower():
            return ValidationDecision.reject("malformed_envelope:device_type_mismatch")
        if str(row.get("node_uuid") or "") != node_uuid:
            return ValidationDecision.reject("stale_epoch")

        time_decision = self._validate_assignment_window(row, item.ts)
        if time_decision is not None:
            return time_decision
        if mapped_point_ids.intersection(point_ids) != point_ids:
            return ValidationDecision.reject("malformed_envelope:unknown_var_id")

        current_version = int(row.get("current_config_version") or 0)
        if item.config_version > current_version:
            return ValidationDecision.reject("unknown_config_version")
        if item.config_version < current_version:
            return ValidationDecision.accept("stale_config_warning")
        return ValidationDecision.accept()

    @staticmethod
    def _validate_assignment_window(
        row: dict, sample_ts_ms: int
    ) -> ValidationDecision | None:
        status = str(row.get("assignment_status") or "")
        cutover_at = _as_utc(row.get("cutover_at"))
        grace_until = _as_utc(row.get("grace_until"))
        sample_at = datetime.fromtimestamp(sample_ts_ms / 1000, tz=timezone.utc)
        now = datetime.now(timezone.utc)

        if status == "active":
            if cutover_at is not None and sample_at < cutover_at:
                return ValidationDecision.reject("out_of_time_window")
            return None
        if status == "retired":
            if cutover_at is None or sample_at >= cutover_at:
                return ValidationDecision.reject("out_of_time_window")
            if grace_until is not None and now > grace_until:
                return ValidationDecision.reject("out_of_time_window")
            return None
        return ValidationDecision.reject("stale_epoch")

    @staticmethod
    def _point_ids(item: UploadItem) -> set[int] | None:
        try:
            value_ids = {int(value) for value in item.values}
            quality_ids = (
                {int(value) for value in item.qualities}
                if item.qualities is not None
                else value_ids
            )
        except (TypeError, ValueError):
            return None
        if value_ids != quality_ids:
            return None
        return value_ids

    async def _mapped_point_ids(
        self,
        device_code: str,
        group_code: str,
        point_ids: set[int],
    ) -> set[int]:
        placeholders = ",".join(["%s"] * len(point_ids))
        rows = await self._fetchall(
            f"""
            SELECT point_id
            FROM point_storage_mapping
            WHERE device_code = %s AND group_code = %s
              AND point_id IN ({placeholders})
            """,
            (device_code, group_code, *sorted(point_ids)),
        )
        return {int(row["point_id"]) for row in rows}

    async def _fetchone(self, query: str, params: tuple) -> dict | None:
        async with self._pool.acquire() as connection:
            async with connection.cursor(aiomysql.DictCursor) as cursor:
                await cursor.execute(query, params)
                return await cursor.fetchone()

    async def _fetchall(self, query: str, params: tuple) -> list[dict]:
        async with self._pool.acquire() as connection:
            async with connection.cursor(aiomysql.DictCursor) as cursor:
                await cursor.execute(query, params)
                return list(await cursor.fetchall())


def _as_utc(value) -> datetime | None:
    if value is None:
        return None
    if value.tzinfo is None:
        return value.replace(tzinfo=_CENTER_TIMEZONE).astimezone(timezone.utc)
    return value.astimezone(timezone.utc)