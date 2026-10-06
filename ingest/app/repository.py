from __future__ import annotations

import asyncio
import logging
import zlib

from collections import OrderedDict
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
# B26：验签前公钥缓存的**容量上限**。未认证请求可以携带任意唯一 node_uuid，若不设上限，
# 缓存会随唯一 UUID 数量无界增长（内存放大）。上限内按 LRU 淘汰，过期项在触及上限时清理。
_PUBLIC_KEY_CACHE_MAX = 4096
_CONTROL_CACHE_MAX = 4096
# 这几类驳回可能只是控制面已提交、而本进程控制缓存落后的结果：
# 永久化（rejected_consumed）之前必须对决定该判定的那个缓存命名空间做一次受控权威重读。
_UNKNOWN_VAR_ID = "malformed_envelope:unknown_var_id"
_UNKNOWN_CONFIG_VERSION = "unknown_config_version"
_UNKNOWN_DEVICE = "unknown_device"
_DEVICE_TYPE_MISMATCH = "malformed_envelope:device_type_mismatch"
# 按命名空间精确归因（决定该判定的缓存只有一个）：
# - `unknown_var_id` ← `_mapping_cache`（`point_storage_mapping` 行缺失）；
# - `unknown_device` ← `_assignment_cache` 的否定缓存 `(expiry, None)`（assignment 行缺失）；
# - `device_type_mismatch` ← `_assignment_cache` 行里的 `device.device_type`；
# - `unknown_config_version` ← `_assignment_cache` 行里的 `device_point_group.config_version`。
_MAPPING_REREADABLE_REASONS = frozenset({_UNKNOWN_VAR_ID})
_ASSIGNMENT_REREADABLE_REASONS = frozenset(
    {_UNKNOWN_DEVICE, _DEVICE_TYPE_MISMATCH, _UNKNOWN_CONFIG_VERSION}
)
_REREADABLE_REASONS = _MAPPING_REREADABLE_REASONS | _ASSIGNMENT_REREADABLE_REASONS
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
        self._last_touch: OrderedDict[str, float] = OrderedDict()
        self._touch_tasks: set[asyncio.Task[None]] = set()
        self._control_cache_ttl_seconds = control_cache_ttl_seconds
        # B26：`OrderedDict` 以支持 LRU 提升与有界淘汰（正/负结果都缓存，但总量有上限）。
        self._public_key_cache: OrderedDict[str, tuple[float, list[str]]] = OrderedDict()
        self._assignment_cache: OrderedDict[
            tuple[str, str, int], tuple[float, dict | None]
        ] = OrderedDict()
        self._mapping_cache: OrderedDict[
            tuple[str, str], tuple[float, frozenset[int]]
        ] = OrderedDict()
        self._reread_at: OrderedDict[object, float] = OrderedDict()

    @staticmethod
    def _cache_store(cache: OrderedDict, key, value) -> None:
        cache[key] = value
        cache.move_to_end(key)
        while len(cache) > _CONTROL_CACHE_MAX:
            cache.popitem(last=False)

    @classmethod
    async def create(
        cls,
        settings: IngestSettings,
        *,
        migrate_schema: bool = True,
    ) -> "MySQLIngestRepository":
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
        if migrate_schema:
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
    def _evict_public_key_cache(self, now: float) -> None:
        """B26：把公钥缓存收敛到上限以内——先清过期项，再按 LRU 淘汰。

        刻意**只在触及上限时**做清理：未认证请求可用唯一 UUID 洪泛，若每次写入都全表扫描，
        清理本身就会变成 O(n²) 的放大面。TTL 与 LRU 都不削弱轮换/吊销语义——
        `invalidate_control_cache()` 与单点 `pop()` 仍然立即失效。
        """
        if len(self._public_key_cache) <= _PUBLIC_KEY_CACHE_MAX:
            return
        for key in [k for k, (expiry, _keys) in self._public_key_cache.items() if expiry <= now]:
            self._public_key_cache.pop(key, None)
        while len(self._public_key_cache) > _PUBLIC_KEY_CACHE_MAX:
            self._public_key_cache.popitem(last=False)  # 淘汰最久未使用

    async def get_approved_node_public_keys(self, node_uuid: str) -> list[str]:
        now = monotonic()
        cached = self._public_key_cache.get(node_uuid)
        if cached is not None and cached[0] > now:
            self._public_key_cache.move_to_end(node_uuid)  # LRU：命中即提升
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
        self._public_key_cache.move_to_end(node_uuid)
        self._evict_public_key_cache(now)
        return list(keys)
    async def touch_node(self, node_uuid: str) -> None:
        now = monotonic()
        if now - self._last_touch.get(node_uuid, 0.0) < _TOUCH_INTERVAL_SECONDS:
            return
        self._cache_store(self._last_touch, node_uuid, now)
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

        assignments, fetched_assignments = await self._assignment_rows(assignment_keys)
        mapped, fetched_mappings = await self._mapping_point_sets(set(mapping_points))

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

        suspects = [
            (index, item, point_ids)
            for index, item, point_ids in valid
            if decisions[index] is not None
            and decisions[index].reason in _REREADABLE_REASONS
        ]
        if suspects:
            await self._reread_and_revalidate(
                node_uuid,
                valid,
                decisions,
                assignments,
                fetched_assignments,
                mapped,
                fetched_mappings,
            )

        return [
            decision or ValidationDecision.reject("malformed_envelope")
            for decision in decisions
        ]

    async def _reread_and_revalidate(
        self,
        node_uuid: str,
        valid: list[tuple[int, UploadItem, set[int]]],
        decisions: list[ValidationDecision | None],
        assignments: dict[tuple[str, str, int], dict | None],
        fetched_assignments: set[tuple[str, str, int]],
        mapped: dict[tuple[str, str], set[int]],
        fetched_mappings: set[tuple[str, str]],
    ) -> None:
        """永久驳回前的受控权威重读，按缓存命名空间分轮。

        - `unknown_var_id` 只可能由 mapping 缓存落后造成；`unknown_device`（assignment
          行的否定缓存 `(expiry, None)`）、`device_type_mismatch`、`unknown_config_version`
          只可能由 assignment 缓存落后造成，因此每轮只重读决定该判定的那一个命名空间。
        - 每轮顺序固定为 mapping → assignment：新点位帧会先撞 mapping 缺失，权威补上映射
          后才暴露版本落后（`repository.py` 的映射校验在版本校验之前）；assignment 轮修好后
          还可能暴露 mapping 落后，故最多两轮收口。
        - 只重读「本次由缓存命中」的 key（并发 miss 时本次读到的就是权威值）；
          同一 key 在一个 TTL 窗口内至多重读一次，真正的非法帧不会造成无界查询。
        """
        for _ in range(2):
            reread = await self._reread_mappings(
                node_uuid, valid, decisions, assignments, mapped, fetched_mappings
            )
            if await self._reread_assignments(
                node_uuid, valid, decisions, assignments, mapped, fetched_assignments
            ):
                reread = True
            if not reread:
                break

    async def _reread_mappings(
        self,
        node_uuid: str,
        valid: list[tuple[int, UploadItem, set[int]]],
        decisions: list[ValidationDecision | None],
        assignments: dict[tuple[str, str, int], dict | None],
        mapped: dict[tuple[str, str], set[int]],
        fetched_mappings: set[tuple[str, str]],
    ) -> bool:
        """重读 mapping 命名空间；返回是否真的发起了一次权威重读。"""
        mapping_keys: set[tuple[str, str]] = set()
        for index, item, _ in valid:
            if not self._matches_reason(decisions, index, _MAPPING_REREADABLE_REASONS):
                continue
            key = (item.device_code, item.group_code)
            if key not in fetched_mappings and self._claim_reread(("mapping", *key)):
                mapping_keys.add(key)
        if not mapping_keys:
            return False
        refreshed_mappings, _ = await self._mapping_point_sets(
            mapping_keys, force_keys=mapping_keys
        )
        mapped.update(refreshed_mappings)
        self._revalidate_reasons(
            node_uuid, valid, decisions, assignments, mapped, _MAPPING_REREADABLE_REASONS
        )
        return True

    async def _reread_assignments(
        self,
        node_uuid: str,
        valid: list[tuple[int, UploadItem, set[int]]],
        decisions: list[ValidationDecision | None],
        assignments: dict[tuple[str, str, int], dict | None],
        mapped: dict[tuple[str, str], set[int]],
        fetched_assignments: set[tuple[str, str, int]],
    ) -> bool:
        """重读 assignment 命名空间；返回是否真的发起了一次权威重读。"""
        assignment_keys: set[tuple[str, str, int]] = set()
        for index, item, _ in valid:
            if not self._matches_reason(
                decisions, index, _ASSIGNMENT_REREADABLE_REASONS
            ):
                continue
            key = (item.device_code, item.group_code, item.assignment_epoch)
            if key not in fetched_assignments and self._claim_reread(
                ("assignment", *key)
            ):
                assignment_keys.add(key)
        if not assignment_keys:
            return False
        refreshed_assignments, _ = await self._assignment_rows(
            assignment_keys, force_keys=assignment_keys
        )
        assignments.update(refreshed_assignments)
        self._revalidate_reasons(
            node_uuid, valid, decisions, assignments, mapped, _ASSIGNMENT_REREADABLE_REASONS
        )
        return True

    @staticmethod
    def _matches_reason(
        decisions: list[ValidationDecision | None],
        index: int,
        reasons: frozenset[str],
    ) -> bool:
        decision = decisions[index]
        return decision is not None and decision.reason in reasons

    def _revalidate_reasons(
        self,
        node_uuid: str,
        valid: list[tuple[int, UploadItem, set[int]]],
        decisions: list[ValidationDecision | None],
        assignments: dict[tuple[str, str, int], dict | None],
        mapped: dict[tuple[str, str], set[int]],
        reasons: frozenset[str],
    ) -> None:
        for index, item, point_ids in valid:
            if not self._matches_reason(decisions, index, reasons):
                continue
            decisions[index] = self._validate_item_row(
                node_uuid,
                item,
                assignments.get(
                    (item.device_code, item.group_code, item.assignment_epoch)
                ),
                mapped.get((item.device_code, item.group_code), set()),
                point_ids,
            )

    def _claim_reread(self, key: object) -> bool:
        now = monotonic()
        last = self._reread_at.get(key)
        if last is not None and now - last < self._control_cache_ttl_seconds:
            return False
        self._cache_store(self._reread_at, key, now)
        return True

    async def _assignment_rows(
        self,
        keys: set[tuple[str, str, int]],
        *,
        force_keys: set[tuple[str, str, int]] | None = None,
    ) -> tuple[dict[tuple[str, str, int], dict | None], set[tuple[str, str, int]]]:
        now = monotonic()
        forced = force_keys or set()
        result: dict[tuple[str, str, int], dict | None] = {}
        missing: set[tuple[str, str, int]] = set()
        for key in keys:
            cached = self._assignment_cache.get(key)
            if key not in forced and cached is not None and cached[0] > now:
                result[key] = cached[1]
                self._assignment_cache.move_to_end(key)
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
                self._cache_store(self._assignment_cache, key, (
                    self._cache_expiry("assignment", key),
                    value,
                ))
                result[key] = value
        return result, set(missing)

    async def _mapping_point_sets(
        self,
        keys: set[tuple[str, str]],
        *,
        force_keys: set[tuple[str, str]] | None = None,
    ) -> tuple[dict[tuple[str, str], set[int]], set[tuple[str, str]]]:
        now = monotonic()
        forced = force_keys or set()
        result: dict[tuple[str, str], set[int]] = {}
        missing: set[tuple[str, str]] = set()
        for key in keys:
            cached = self._mapping_cache.get(key)
            if key not in forced and cached is not None and cached[0] > now:
                result[key] = set(cached[1])
                self._mapping_cache.move_to_end(key)
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
                self._cache_store(self._mapping_cache, key, (
                    self._cache_expiry("mapping", key),
                    value,
                ))
                result[key] = set(value)
        return result, set(missing)

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
        try:
            sample_at = datetime.fromtimestamp(sample_ts_ms / 1000, tz=timezone.utc)
        except (ValueError, OSError, OverflowError):
            return ValidationDecision.reject("malformed_envelope:invalid_timestamp")
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
