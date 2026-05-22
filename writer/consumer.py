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
                            self._handle_entry(
                                stream_key=stream_key,
                                entry_id=entry_id,
                                entry_data=entry_data,
                                delivery_count=1,
                            )
            else:
                logger.warning(
                    "[consumer] buffer full (%d/%d rows), pausing xreadgroup until flush clears",
                    len(self._buffer),
                    self._max_buffer_rows,
                )
                await asyncio.sleep(self._flush_interval_s)

            # --- Flush if time or buffer threshold reached ---
            now = time.monotonic()
            buffer_size = len(self._buffer)
            if buffer_size >= self._max_buffer_rows or (
                buffer_size > 0 and now - self._last_flush >= self._flush_interval_s
            ):
                await self._flush()

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
            for entry_id, entry_data in claimed_entries:
                delivery_count = self._get_delivery_count(stream_key, entry_id)
                self._handle_entry(stream_key, entry_id, entry_data, delivery_count)

            if claimed_entries:
                await self._flush()

            if next_id in ("0-0", "0"):
                break
            start_id = next_id

    async def _drain_with_xpending_xclaim(self, stream_key: str, consumer_name: str) -> None:
        loop = asyncio.get_running_loop()
        pending_entries = await loop.run_in_executor(
            None,
            lambda: self._r.xpending_range(
                stream_key,
                self._group_name,
                min="-",
                max="+",
                count=self._batch_count,
            ),
        )
        if not pending_entries:
            return

        eligible_ids = [
            p["message_id"]
            for p in pending_entries
            if p.get("time_since_delivered", 0) >= self._pending_min_idle_ms
        ]
        if not eligible_ids:
            return

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
            # delivery_count comes from xpending_range
            dc = next(
                (p["times_delivered"] for p in pending_entries if p["message_id"] == entry_id),
                1,
            )
            self._handle_entry(stream_key, entry_id, entry_data, dc)

        if self._buffer:
            await self._flush()

    def _get_delivery_count(self, stream_key: str, entry_id: str) -> int:
        """Fetch delivery count for a single entry via XPENDING."""
        try:
            result = self._r.xpending_range(
                stream_key, self._group_name, min=entry_id, max=entry_id, count=1
            )
            if result:
                return int(result[0].get("times_delivered", 1))
        except Exception:
            pass
        return 1

    # ------------------------------------------------------------------
    # Entry parsing
    # ------------------------------------------------------------------

    def _handle_entry(
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

        # --- Get variable mapping ---
        mapping = self._mapper.get_or_load(device_code, group_code, config_version)
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

    def _ack_single(self, stream_key: str, entry_id: str) -> None:
        try:
            self._r.xack(stream_key, self._group_name, entry_id)
        except Exception as exc:
            logger.warning("[consumer] xack single failed %s: %s", entry_id, exc)

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
