"""Redis Stream consumer that batch-writes to TDengine.

Each stream entry (produced by the collector) has these fields:
    device_code, device_type, group_code, scan_rate, ts,
    config_version, values (JSON str of {var_id: str_value})

Processing pipeline per entry:
    1. Parse fields
    2. Get GroupMapping from MysqlMapper (cached by config_version)
    3. Translate var_id→td_column_name and accumulate into buffer
    4. On flush: write_batch → XACK on success; skip XACK on failure
    5. Entries exceeding pending_max_retry → DLQ + XACK

Pending (unacked) message recovery:
    On startup and periodically, XAUTOCLAIM (Redis >= 6.2) or
    XPENDING + XCLAIM is used to reclaim idle messages from this or
    crashed consumers and process them.
"""

from __future__ import annotations

import asyncio
import json
import logging
import time
from dataclasses import dataclass, field

import redis

from dlq import push_dlq
from mysql_mapper import MysqlMapper
from tdengine_writer import TDengineWriter

logger = logging.getLogger("writer.consumer")


# ---------------------------------------------------------------------------
# Internal buffer row
# ---------------------------------------------------------------------------

@dataclass
class _BufferedRow:
    entry_id: str
    stream_key: str
    device_code: str
    device_type: str
    group_code: str
    child_table: str
    stable: str
    ts: int
    columns: dict[str, float]
    delivery_count: int = 1


# ---------------------------------------------------------------------------
# StreamConsumer
# ---------------------------------------------------------------------------

class StreamConsumer:
    def __init__(
        self,
        config: dict,
        mapper: MysqlMapper,
        writer: TDengineWriter,
        r: redis.Redis,
    ) -> None:
        self._config = config
        self._mapper = mapper
        self._writer = writer
        self._r = r

        w = config["writer"]
        rc = config["redis"]
        self._flush_interval_s: float = w["flush_interval_ms"] / 1000.0
        self._batch_count: int = w["batch_count"]
        self._max_buffer_rows: int = w["max_buffer_rows"]
        self._pending_min_idle_ms: int = w["pending_min_idle_ms"]
        self._pending_max_retry: int = w["pending_max_retry"]
        self._group_name: str = rc["group_name"]
        self._consumer_name: str = rc["consumer_name"]

        self._buffer: list[_BufferedRow] = []
        self._last_flush: float = time.monotonic()
        self._last_pending_drain: float = time.monotonic()
        self._pending_drain_interval_s: float = 60.0
        self._ack_retry: dict[str, set[str]] = {}  # stream_key → 待重试 XACK 的 entry_id (M5)
        self._consecutive_flush_fail: int = 0  # M6：连续 flush 失败次数，用于退避与日志降噪

    # ------------------------------------------------------------------
    # Public entry point
    # ------------------------------------------------------------------

    async def run_loop(self, stream_keys: list[str]) -> None:
        """Main consumption loop. Runs until cancelled."""
        if not stream_keys:
            raise ValueError("stream_keys is empty — check writer config device_types/group_codes")
        self._ensure_consumer_groups(stream_keys)

        # Drain any pending messages left from a previous run / crashed consumer.
        for sk in stream_keys:
            await self.drain_pending(sk, self._consumer_name)

        logger.info(
            "[consumer] starting main loop, %d stream key(s), consumer=%s",
            len(stream_keys),
            self._consumer_name,
        )

        block_ms = max(int(self._flush_interval_s * 1000 / 2), 50)

        while True:
            # --- Retry previously failed XACKs first (M5) ---
            # 必须早于 drain：否则已成功落库但 XACK 失败、又满足 idle 条件的条目
            # 会被 drain reclaim 触发重写，违背 M5 「避免重复写入」的目标。
            self._retry_pending_acks()

            # --- Periodically reclaim idle pending messages (S2) ---
            if time.monotonic() - self._last_pending_drain >= self._pending_drain_interval_s:
                for sk in stream_keys:
                    await self.drain_pending(sk, self._consumer_name)
                self._last_pending_drain = time.monotonic()

            # --- Read new messages (skip when buffer is full to apply back-pressure) ---
            if len(self._buffer) < self._max_buffer_rows:
                streams_arg = {sk: ">" for sk in stream_keys}
                try:
                    response = await asyncio.get_running_loop().run_in_executor(
                        None,
                        lambda: self._r.xreadgroup(
                            groupname=self._group_name,
                            consumername=self._consumer_name,
                            streams=streams_arg,
                            count=self._batch_count,
                            block=block_ms,
                        ),
                    )
                except Exception as exc:
                    logger.error("[consumer] xreadgroup error: %s", exc)
                    await asyncio.sleep(1)
                    continue

                if response:
                    for stream_key, entries in response:
                        for entry_id, entry_data in entries:
                            try:
                                await self._handle_entry(
                                    stream_key=stream_key,
                                    entry_id=entry_id,
                                    entry_data=entry_data,
                                    delivery_count=1,
                                )
                            except Exception as exc:
                                # 不 XACK：留 PEL，由后续 drain 重试，达上限后进 DLQ
                                logger.error(
                                    "[consumer] unexpected error handling entry %s, skipping: %s",
                                    entry_id,
                                    exc,
                                )
            else:
                # L1: buffer 满 → 跳过 xreadgroup，让底部 flush 块立即触发（buffer_size >= max）。
                # 失败时由 M6 退避兜底，原先的 flush_interval_s sleep 多余且增加最多 500ms 落库延迟。
                logger.warning(
                    "[consumer] buffer full (%d/%d rows), flushing immediately without reading new messages",
                    len(self._buffer),
                    self._max_buffer_rows,
                )

            # --- Flush if time or buffer threshold reached ---
            now = time.monotonic()
            buffer_size = len(self._buffer)
            if buffer_size >= self._max_buffer_rows or (
                buffer_size > 0 and now - self._last_flush >= self._flush_interval_s
            ):
                await self._flush()
                # M6: 连续失败递增退避 + 日志降噪
                if self._buffer:
                    self._consecutive_flush_fail += 1
                    backoff = min(1.0 * self._consecutive_flush_fail, 10.0)
                    if self._consecutive_flush_fail == 1 or self._consecutive_flush_fail % 10 == 0:
                        logger.warning(
                            "[consumer] flush failed %d times consecutively, backing off %.1fs",
                            self._consecutive_flush_fail, backoff,
                        )
                    await asyncio.sleep(backoff)
                else:
                    if self._consecutive_flush_fail > 0:
                        logger.info(
                            "[consumer] flush recovered after %d failures",
                            self._consecutive_flush_fail,
                        )
                    self._consecutive_flush_fail = 0

    # ------------------------------------------------------------------
    # Pending message recovery
    # ------------------------------------------------------------------

    async def drain_pending(self, stream_key: str, consumer_name: str) -> None:
        """Reclaim idle pending messages and process them.

        Tries XAUTOCLAIM first (Redis >= 6.2); falls back to XPENDING + XCLAIM.
        """
        logger.info("[consumer] draining pending for stream %s", stream_key)
        try:
            await self._drain_with_xautoclaim(stream_key, consumer_name)
        except Exception as exc:
            if "unknown command" in str(exc).lower() or "xautoclaim" in str(exc).lower():
                logger.info(
                    "[consumer] XAUTOCLAIM not supported, falling back to XPENDING+XCLAIM: %s",
                    exc,
                )
                await self._drain_with_xpending_xclaim(stream_key, consumer_name)
            else:
                logger.warning("[consumer] drain_pending error: %s", exc)

    async def _drain_with_xautoclaim(self, stream_key: str, consumer_name: str) -> None:
        loop = asyncio.get_running_loop()
        start_id = "0-0"
        while True:
            result = await loop.run_in_executor(
                None,
                lambda: self._r.xautoclaim(
                    stream_key,
                    self._group_name,
                    consumer_name,
                    min_idle_time=self._pending_min_idle_ms,
                    start_id=start_id,
                    count=self._batch_count,
                ),
            )
            # result is (next_start_id, [(entry_id, data), ...], deleted_ids)
            next_id, claimed_entries, _ = result
            delivery_counts = self._get_delivery_counts(stream_key, [entry_id for entry_id, _ in claimed_entries])
            for entry_id, entry_data in claimed_entries:
                try:
                    delivery_count = delivery_counts.get(entry_id, 1)
                    await self._handle_entry(stream_key, entry_id, entry_data, delivery_count)
                except Exception as exc:
                    # 不 XACK：留 PEL，下一轮 drain 重试，达上限进 DLQ
                    logger.error(
                        "[consumer] unexpected error handling pending entry %s, skipping: %s",
                        entry_id, exc,
                    )

            if claimed_entries:
                await self._flush()

            if next_id in ("0-0", "0"):
                break
            start_id = next_id

    async def _drain_with_xpending_xclaim(self, stream_key: str, consumer_name: str) -> None:
        loop = asyncio.get_running_loop()
        cursor = "-"
        while True:
            pending_entries = await loop.run_in_executor(
                None,
                lambda: self._r.xpending_range(
                    stream_key,
                    self._group_name,
                    min=cursor,
                    max="+",
                    count=self._batch_count,
                ),
            )
            if not pending_entries:
                break

            eligible_ids = [
                p["message_id"]
                for p in pending_entries
                if p.get("time_since_delivered", 0) >= self._pending_min_idle_ms
            ]
            if eligible_ids:
                claimed = await loop.run_in_executor(
                    None,
                    lambda: self._r.xclaim(
                        stream_key,
                        self._group_name,
                        consumer_name,
                        min_idle_time=self._pending_min_idle_ms,
                        message_ids=eligible_ids,
                    ),
                )
                for entry_id, entry_data in claimed:
                    try:
                        # delivery_count comes from xpending_range
                        dc = next(
                            (p["times_delivered"] for p in pending_entries if p["message_id"] == entry_id),
                            1,
                        )
                        await self._handle_entry(stream_key, entry_id, entry_data, dc)
                    except Exception as exc:
                        # 不 XACK：留 PEL，下一轮 drain 重试，达上限进 DLQ
                        logger.error(
                            "[consumer] unexpected error handling pending entry %s, skipping: %s",
                            entry_id, exc,
                        )

                if claimed:
                    await self._flush()

            last_id = pending_entries[-1]["message_id"]
            cursor = f"({last_id}"  # exclusive：下一页从此 ID 之后开始
            if len(pending_entries) < self._batch_count:
                break  # 已是最后一页

    def _get_delivery_counts(self, stream_key: str, entry_ids: list[str]) -> dict[str, int]:
        """Fetch delivery counts for a batch of entries via one XPENDING call."""
        if not entry_ids:
            return {}
        try:
            first_id = entry_ids[0]
            last_id = entry_ids[-1]
            result = self._r.xpending_range(
                stream_key,
                self._group_name,
                min=first_id,
                max=last_id,
                count=len(entry_ids),
            )
            if result:
                return {
                    p["message_id"]: int(p.get("times_delivered", 1))
                    for p in result
                }
        except Exception:
            pass
        return {}

    # ------------------------------------------------------------------
    # Entry parsing
    # ------------------------------------------------------------------

    async def _handle_entry(
        self,
        stream_key: str,
        entry_id: str,
        entry_data: dict,
        delivery_count: int,
    ) -> None:
        device_code = entry_data.get("device_code", "")
        device_type = entry_data.get("device_type", "")
        group_code = entry_data.get("group_code", "")
        ts_raw = entry_data.get("ts", "")
        config_version_raw = entry_data.get("config_version", "0")
        values_raw = entry_data.get("values", "{}")

        # --- DLQ check first ---
        if delivery_count > self._pending_max_retry:
            moved = push_dlq(
                self._r,
                device_type=device_type or "unknown",
                group_code=group_code or "unknown",
                entry_id=entry_id,
                stream_key=stream_key,
                data=dict(entry_data),
                retry_count=delivery_count,
                last_error="exceeded max retry",
            )
            if moved:
                self._ack_single(stream_key, entry_id)
            else:
                logger.error("[consumer] DLQ failed for entry %s, not acking", entry_id)
            return

        # --- Parse required fields ---
        if not device_code or not group_code:
            logger.warning("[consumer] skipping entry %s: missing device_code/group_code", entry_id)
            self._ack_single(stream_key, entry_id)
            return

        try:
            config_version = int(config_version_raw)
        except (ValueError, TypeError):
            config_version = 0

        try:
            ts = int(ts_raw)
        except (ValueError, TypeError):
            ts = int(time.time() * 1000)

        try:
            raw_values: dict[str, str] = json.loads(values_raw)
        except Exception as exc:
            logger.warning("[consumer] invalid values JSON for entry %s: %s", entry_id, exc)
            self._ack_single(stream_key, entry_id)
            return
        if not isinstance(raw_values, dict):
            logger.warning(
                "[consumer] values is not a JSON object for entry %s: %r", entry_id, raw_values
            )
            self._ack_single(stream_key, entry_id)
            return

        # --- Get variable mapping (M3: 把同步 MySQL 查询挪到 executor，避免阻塞事件循环) ---
        mapping = await asyncio.get_running_loop().run_in_executor(
            None,
            self._mapper.get_or_load,
            device_code,
            group_code,
            config_version,
        )
        if mapping is None:
            logger.warning(
                "[consumer] no current mapping for device=%s group=%s version=%s, leaving entry %s pending",
                device_code,
                group_code,
                config_version,
                entry_id,
            )
            return

        # --- Translate var_id → td_column_name with type coercion ---
        columns: dict[str, float] = {}
        for var_id, str_val in raw_values.items():
            col_name = mapping.var_map.get(var_id)
            if not col_name:
                continue
            try:
                columns[col_name] = float(str_val)
            except (ValueError, TypeError):
                columns[col_name] = 0.0

        if not columns:
            logger.debug("[consumer] entry %s has no mappable columns, acking", entry_id)
            self._ack_single(stream_key, entry_id)
            return

        self._buffer.append(
            _BufferedRow(
                entry_id=entry_id,
                stream_key=stream_key,
                device_code=device_code,
                device_type=device_type,
                group_code=group_code,
                child_table=mapping.child_table,
                stable=mapping.stable,
                ts=ts,
                columns=columns,
                delivery_count=delivery_count,
            )
        )

    # ------------------------------------------------------------------
    # Flush
    # ------------------------------------------------------------------

    async def _flush(self) -> None:
        if not self._buffer:
            self._last_flush = time.monotonic()
            return

        by_table: dict[str, list[_BufferedRow]] = {}
        for row in self._buffer:
            by_table.setdefault(row.child_table, []).append(row)

        failed_rows: list[_BufferedRow] = []
        for child_table, buffered_rows in by_table.items():
            rows = [
                {
                    "child_table": row.child_table,
                    "stable": row.stable,
                    "device_code": row.device_code,
                    "ts": row.ts,
                    "columns": row.columns,
                }
                for row in buffered_rows
            ]
            ok = await self._writer.write_batch(rows)
            if ok:
                self._ack_rows(buffered_rows)
            else:
                logger.error(
                    "[consumer] write_batch failed for table %s (%d rows) — not acking, will retry",
                    child_table,
                    len(buffered_rows),
                )
                failed_rows.extend(buffered_rows)

        self._buffer = failed_rows
        self._last_flush = time.monotonic()

    def _ack_rows(self, rows: list[_BufferedRow]) -> None:
        """XACK rows, grouped by stream_key."""
        by_key: dict[str, list[str]] = {}
        for row in rows:
            by_key.setdefault(row.stream_key, []).append(row.entry_id)

        for sk, ids in by_key.items():
            self._ack_ids(sk, ids)

    def _ack_ids(self, stream_key: str, ids: list[str]) -> None:
        chunk_size = 500
        for i in range(0, len(ids), chunk_size):
            chunk = ids[i : i + chunk_size]
            try:
                self._r.xack(stream_key, self._group_name, *chunk)
            except Exception as exc:
                logger.warning("[consumer] xack failed for %s: %s", stream_key, exc)
                self._ack_retry.setdefault(stream_key, set()).update(chunk)

    def _ack_single(self, stream_key: str, entry_id: str) -> None:
        try:
            self._r.xack(stream_key, self._group_name, entry_id)
        except Exception as exc:
            logger.warning("[consumer] xack single failed %s: %s", entry_id, exc)
            self._ack_retry.setdefault(stream_key, set()).add(entry_id)

    def _retry_pending_acks(self) -> None:
        """Re-attempt XACKs that previously failed (M5).

        行已成功落 TDengine，仅补 XACK 关闭 PEL 占位，避免后续 drain
        重新 claim 这些条目，触发 _handle_entry 重写。XACK 是 idempotent，
        即使中间已被 drain 一次也安全。
        """
        if not self._ack_retry:
            return
        chunk_size = 500
        for stream_key in list(self._ack_retry.keys()):
            pending = self._ack_retry[stream_key]
            ids = list(pending)
            for i in range(0, len(ids), chunk_size):
                chunk = ids[i : i + chunk_size]
                try:
                    self._r.xack(stream_key, self._group_name, *chunk)
                    pending.difference_update(chunk)
                except Exception as exc:
                    logger.warning(
                        "[consumer] xack retry still failing for %s (%d ids pending): %s",
                        stream_key, len(pending), exc,
                    )
                    break  # 本轮放弃后续 chunk，等下一轮再试
            if not pending:
                del self._ack_retry[stream_key]

    # ------------------------------------------------------------------
    # Setup helpers
    # ------------------------------------------------------------------

    def _ensure_consumer_groups(self, stream_keys: list[str]) -> None:
        """Create consumer groups (MKSTREAM) for all stream keys if not present."""
        for sk in stream_keys:
            try:
                self._r.xgroup_create(sk, self._group_name, id="0", mkstream=True)
                logger.info("[consumer] created consumer group '%s' on %s", self._group_name, sk)
            except redis.exceptions.ResponseError as exc:
                if "BUSYGROUP" in str(exc):
                    pass  # already exists, fine
                else:
                    logger.warning("[consumer] xgroup_create error for %s: %s", sk, exc)
