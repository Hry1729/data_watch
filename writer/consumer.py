"""Redis Stream 消费者，批量写入 TDengine。Redis Stream consumer that batch-writes to TDengine.

Each stream entry (produced by the collector) has these fields:
    device_code, device_type, group_code, scan_rate, ts,
    config_version, values (JSON str of {var_id: str_value})
    可选 v2：contract_version=2, qualities (JSON str of {var_id: int})

Processing pipeline per entry:
    1. Parse fields
    2. Get GroupMapping from MysqlMapper (permanent cache by device/group; reload on unknown var)
    3. Translate var_id→td_column_name and accumulate into buffer
    4. On flush: write_multi_table (chunked) → XACK rows of successful chunks
    5. Only confirmed malformed or bad data is moved to DLQ before XACK

Pending (unacked) message recovery:
    On startup and periodically, XAUTOCLAIM (Redis >= 6.2) or
    XPENDING + XCLAIM is used to reclaim idle messages from this or
    crashed consumers and process them.
"""

from __future__ import annotations

import asyncio
import json
import logging
import math
import time
from dataclasses import dataclass, field

import redis

from dlq import (
    DEFAULT_DLQ_ALERT_THRESHOLD,
    DEFAULT_DLQ_MAX_SIZE,
    DEFAULT_DLQ_WARN_ENTRIES,
    push_dlq,
)
from mysql_mapper import MysqlMapper
from performance import WriterPerformanceMetrics
from quality_codes import is_valid_quality
from tdengine_writer import BAD_DATA, LANDED, RETRY, TDengineWriter

logger = logging.getLogger("writer.consumer")


# ---------------------------------------------------------------------------
# 内部缓冲行
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
    # var_id → 平台质量码；空表示不写质量窄表（v1 / legacy unknown）
    qualities: dict[str, int] = field(default_factory=dict)
    entry_data: dict = field(default_factory=dict)  # 原始 stream 字段，BAD_DATA 进 DLQ 时需要
    delivery_count: int = 1
    payload_bytes: int = 0
    # 连续 RETRY 次数。达到上限后该行移入隔离区，不再占用主缓冲容量、
    # 也不再拖住同 worker 其它 stream（仍在 PEL 中，可被按时回收）。
    retry_attempts: int = 0


@dataclass
class _PreparedPayload:
    payload_bytes: int
    decoded: bool = True
    raw_values: object = None
    values_error: Exception | None = None
    raw_qualities: object = None
    qualities_error: Exception | None = None
    values_parse_ms: float = 0.0
    qualities_parse_ms: float = 0.0


def _prepare_payload_batch(
    entries: list[dict], byte_budget: int,
) -> list[_PreparedPayload]:
    """Decode only payloads that fit the reserved raw-byte budget."""
    prepared = []
    remaining = max(0, byte_budget)
    for entry in entries:
        if not isinstance(entry, dict):
            prepared.append(_PreparedPayload(0))
            continue
        payload_bytes = sum(
            len(str(key).encode("utf-8")) + len(str(value).encode("utf-8"))
            for key, value in entry.items()
        )
        if payload_bytes > remaining:
            prepared.append(_PreparedPayload(payload_bytes, decoded=False))
            remaining = 0
            continue
        remaining -= payload_bytes
        item = _PreparedPayload(payload_bytes)
        started = time.perf_counter()
        try:
            item.raw_values = json.loads(entry.get("values"))
        except Exception as exc:
            item.values_error = exc
        item.values_parse_ms = (time.perf_counter() - started) * 1000
        if isinstance(item.raw_values, dict):
            try:
                version = int(entry.get("contract_version", 1))
            except (ValueError, TypeError):
                version = None
            if version == 2:
                started = time.perf_counter()
                try:
                    qualities_raw = entry.get("qualities", "{}")
                    if isinstance(qualities_raw, (bytes, bytearray)):
                        qualities_raw = qualities_raw.decode("utf-8")
                    item.raw_qualities = (
                        json.loads(qualities_raw)
                        if isinstance(qualities_raw, str) else qualities_raw
                    )
                except Exception as exc:
                    item.qualities_error = exc
                item.qualities_parse_ms = (time.perf_counter() - started) * 1000
        prepared.append(item)
    return prepared


# ---------------------------------------------------------------------------
# 流消费者
# ---------------------------------------------------------------------------

class StreamConsumer:
    def __init__(
        self,
        config: dict,
        mapper: MysqlMapper,
        writer: TDengineWriter,
        r: redis.Redis,
        syslog=None,
        metrics: WriterPerformanceMetrics | None = None,
        performance_log_interval_s: float = 60.0,
    ) -> None:
        self._config = config
        self._mapper = mapper
        self._writer = writer
        self._r = r
        self._syslog = syslog
        self._metrics = metrics or WriterPerformanceMetrics()
        self._performance_log_interval_s = performance_log_interval_s
        self._last_performance_log = time.monotonic()

        w = config["writer"]
        rc = config["redis"]
        self._flush_interval_s: float = w["flush_interval_ms"] / 1000.0
        self._batch_count: int = w["batch_count"]
        self._max_buffer_rows: int = w["max_buffer_rows"]
        self._max_buffer_bytes: int = max(1, int(w.get("max_buffer_bytes", 32 * 1024 * 1024)))
        self._buffer_bytes = 0
        self._inflight_bytes = 0
        self._inflight_source_ms: int | None = None
        self._estimated_entry_bytes = 1024
        self._pending_min_idle_ms: int = w["pending_min_idle_ms"]
        self._dlq_max_size: int = max(0, int(w.get("dlq_max_size", DEFAULT_DLQ_MAX_SIZE)))
        self._dlq_alert_threshold: float = min(
            1.0,
            max(
                0.0,
                float(w.get("dlq_alert_threshold", DEFAULT_DLQ_ALERT_THRESHOLD)),
            ),
        )
        self._dlq_warn_entries: int = max(
            0,
            int(w.get("dlq_warn_entries", DEFAULT_DLQ_WARN_ENTRIES)),
        )
        self._group_name: str = rc["group_name"]
        self._consumer_name: str = rc["consumer_name"]

        self._buffer: list[_BufferedRow] = []
        # 缓冲中 (stream_key, entry_id) 集合：flush 失败行既留缓冲又留 PEL，
        # drain 重新领取同一消息时靠此集合防止重复入缓冲、重复写库（DW-02）。
        self._buffered_keys: set[tuple[str, str]] = set()
        self._queued_stream_keys: list[str] = []
        self._overdelivered_ids: dict[str, set[str]] = {}
        self._overdelivered_cursor = 0
        self._read_cursor: int = 0
        self._inflight_rows: int = 0
        self._flush_lock = asyncio.Lock()
        self._last_flush: float = time.monotonic()
        self._last_pending_drain: float = time.monotonic()
        self._pending_drain_interval_s: float = max(1.0, float(w.get("pending_drain_interval_s", 5.0)))
        self._pending_cycle_budget_s = max(0.001, float(w.get("pending_cycle_budget_ms", 100)) / 1000.0)
        self._pending_cursors: dict[str, str] = {}
        self._pending_stream_cursor = 0
        self._ack_retry: dict[str, set[str]] = {}  # stream_key → 待重试 XACK 的 entry_id (M5)
        self._delete_retry: dict[str, set[str]] = {}  # 已终态且已 ACK，待重试 XDEL 的 entry_id
        self._ack_retry_cursor = 0
        self._delete_retry_cursor = 0
        self._consecutive_flush_fail: int = 0  # M6：连续 flush 失败次数，用于退避与日志降噪
        # 持续 RETRY（有恢复希望但一直没有恢复）的行的隔离区。
        # 作用范围严格限定：这些行仍保留在 Redis PEL 中、仍可被按时回收，
        # 只是从主缓冲里挪出来，避免一条长期不恢复的行一直占着容量、
        # 进而抑制同 worker 其它 stream 的读取。超出上限时恢复背压（停止读取），
        # 因此隔离区不会无界增长。
        self._isolated: list[_BufferedRow] = []
        self._isolated_keys: set[tuple[str, str]] = set()
        self._max_isolated_rows: int = max(64, self._max_buffer_rows // 4)
        self._max_row_retry: int = max(1, int(w.get("max_row_retry", 3)))
        # 防刷库：坏数据洪峰下 DLQ 事件可能逐条触发，各自 ≥60s 限流写 system_log
        self._last_dlq_moved_log: float | None = None
        self._last_dlq_failed_log: float | None = None

    def _push_dlq(self, **kwargs) -> bool:
        """按 writer 配置写入 DLQ，并按保留模式发出容量告警。"""
        return push_dlq(
            self._r,
            max_size=self._dlq_max_size,
            alert_threshold=self._dlq_alert_threshold,
            warn_entries=self._dlq_warn_entries,
            **kwargs,
        )

    async def _run_blocking(self, func, *args, **kwargs):
        """在默认执行器运行同步 Redis/DLQ 操作，避免阻塞消费事件循环。"""
        loop = asyncio.get_running_loop()
        return await loop.run_in_executor(
            None, lambda: func(*args, **kwargs)
        )

    def _remaining_capacity(self) -> int:
        """Bound all locally owned identities, including terminal Redis retries."""
        occupied = len(self._buffer) + self._inflight_rows + len(self._isolated)
        if self._ack_retry or self._delete_retry:
            terminal_keys = {
                (stream_key, entry_id)
                for retry in (self._ack_retry, self._delete_retry)
                for stream_key, ids in retry.items()
                for entry_id in ids
            }
            # A row can briefly be both in-flight and awaiting its XACK.
            terminal_keys.difference_update(self._buffered_keys)
            occupied += len(terminal_keys)
        return self._max_buffer_rows - occupied

    def _readmit_isolated(self) -> None:
        """把隔离区最老的行放回主缓冲，按回收周期重试。

        隔离区只是"不再占用主缓冲容量、不再抑制其它 stream 读取"，
        不是放弃这些数据：它们始终留在 Redis PEL 中，并按 `pending_drain_interval_s`
        的节奏被放回来重试一次。若仍失败，`_flush` 会再次把它们隔离。
        """
        if not self._isolated:
            return
        budget = max(1, min(len(self._isolated), self._batch_count))
        readmitted = self._isolated[:budget]
        del self._isolated[:budget]
        for row in readmitted:
            self._isolated_keys.discard((row.stream_key, row.entry_id))
        self._buffer = readmitted + self._buffer
        logger.info(
            "[consumer] re-admitting %d isolated row(s) for retry (remaining isolated=%d)",
            len(readmitted), len(self._isolated),
        )

    def oldest_buffered_source_age_ms(self, now_ms: int) -> int | None:
        source_times = [row.ts for row in self._buffer]
        if self._inflight_source_ms is not None:
            source_times.append(self._inflight_source_ms)
        return max(0, now_ms - min(source_times)) if source_times else None

    def queue_stream_key(self, stream_key: str) -> None:
        """由范围发现任务通知；只有 run_loop 可以接管流和缓冲区。"""
        if stream_key not in self._queued_stream_keys:
            self._queued_stream_keys.append(stream_key)

    async def _recover_overdelivered(self) -> None:
        # XREADGROUP should not overdeliver, but keep any returned excess IDs
        # explicit in PEL and reclaim a bounded amount per loop.
        keys = list(self._overdelivered_ids)
        if not keys:
            return
        start = self._overdelivered_cursor % len(keys)
        deadline = time.monotonic() + self._pending_cycle_budget_s
        remaining = min(self._batch_count, self._remaining_capacity())
        visited = 0
        for offset in range(len(keys)):
            if remaining <= 0 or time.monotonic() >= deadline:
                break
            stream_key = keys[(start + offset) % len(keys)]
            visited += 1
            ids = self._overdelivered_ids.get(stream_key)
            if not ids:
                continue
            requested = list(ids)[:remaining]
            try:
                claimed = await self._run_blocking(
                    self._r.xclaim,
                    stream_key,
                    self._group_name,
                    self._consumer_name,
                    min_idle_time=0,
                    message_ids=requested,
                )
            except Exception:
                logger.warning("[consumer] overflow reclaim failed for %s", stream_key, exc_info=True)
                continue
            remaining -= len(requested)
            returned = {entry_id for entry_id, _ in claimed}
            for entry_id in requested:
                if entry_id not in returned:
                    ids.discard(entry_id)
            for entry_id, entry_data in claimed:
                ids.discard(entry_id)
                await self._handle_entry(stream_key, entry_id, entry_data, 1)
            if not ids:
                del self._overdelivered_ids[stream_key]
        self._overdelivered_cursor = (start + (visited or 1)) % len(keys)

    def _next_read_scope(self, stream_keys: list[str]) -> tuple[dict[str, str], int]:
        """XREADGROUP 的 COUNT 对每个流生效，故先分配全局容量。"""
        available = self._remaining_capacity()
        byte_capacity = (self._max_buffer_bytes - self._buffer_bytes - self._inflight_bytes)
        if available <= 0 or byte_capacity <= 0 or not stream_keys:
            return {}, 0
        available = min(available, max(1, byte_capacity // self._estimated_entry_bytes))
        count = min(len(stream_keys), available)
        start = self._read_cursor % len(stream_keys)
        selected = [stream_keys[(start + offset) % len(stream_keys)] for offset in range(count)]
        self._read_cursor = (start + count) % len(stream_keys)
        return {key: ">" for key in selected}, min(self._batch_count, available // count)

    def _move_entry_to_dlq(
        self,
        stream_key: str,
        entry_id: str,
        entry_data: object,
        device_type: object,
        group_code: object,
        delivery_count: int,
        last_error: str,
    ) -> bool:
        """Malformed entry 先落 DLQ，只有落盘成功才 ACK。"""
        data = (
            dict(entry_data)
            if isinstance(entry_data, dict)
            else {"raw_entry": str(entry_data)}
        )
        moved = self._push_dlq(
            device_type=str(device_type or "unknown"),
            group_code=str(group_code or "unknown"),
            entry_id=entry_id,
            stream_key=stream_key,
            data=data,
            retry_count=delivery_count,
            last_error=last_error,
        )
        if moved:
            self._ack_single(stream_key, entry_id)
        else:
            logger.error("[consumer] DLQ failed for malformed entry %s, not acking", entry_id)
        return moved

    # ------------------------------------------------------------------
    # 公共入口点
    # ------------------------------------------------------------------

    async def run_loop(self, stream_keys: list[str]) -> None:
        """主消费循环。运行直到被取消。"""
        if not stream_keys:
            raise ValueError("stream_keys is empty — check writer config device_types/group_codes")
        await self._run_blocking(self._ensure_consumer_groups, stream_keys)

        # Bound startup PEL work; remaining entries continue in periodic cycles.
        startup_deadline = time.monotonic() + self._pending_cycle_budget_s
        for sk in stream_keys:
            if time.monotonic() >= startup_deadline:
                break
            await self.drain_pending(
                sk, self._consumer_name,
                max_entries=self._batch_count, deadline=startup_deadline,
            )

        logger.info(
            "[consumer] starting main loop, %d stream key(s), consumer=%s",
            len(stream_keys),
            self._consumer_name,
        )

        block_ms = max(int(self._flush_interval_s * 1000 / 2), 50)

        while True:
            # Join one discovered stream per cycle so a large scope refresh cannot
            # monopolize this worker before its existing streams are serviced.
            if self._queued_stream_keys:
                stream_key = self._queued_stream_keys.pop(0)
                if stream_key in stream_keys:
                    continue
                await self._run_blocking(self._ensure_consumer_groups, [stream_key])
                await self.drain_pending(
                    stream_key, self._consumer_name, max_entries=self._batch_count,
                    deadline=time.monotonic() + self._pending_cycle_budget_s,
                )
                stream_keys.append(stream_key)

            await self._recover_overdelivered()

            # --- 首先重试之前失败的 XACK（M5）---
            # 必须早于 drain：否则已成功落库但 XACK 失败、又满足 idle 条件的条目
            # 会被 drain reclaim 触发重写，违背 M5 「避免重复写入」的目标。
            retry_deadline = time.monotonic() + self._pending_cycle_budget_s
            await self._run_blocking(
                self._retry_pending_acks, self._batch_count, retry_deadline
            )
            await self._run_blocking(
                self._retry_pending_deletes, self._batch_count, retry_deadline
            )

            # --- 定期回收空闲的待处理消息（S2）---
            if time.monotonic() - self._last_pending_drain >= self._pending_drain_interval_s:
                start = self._pending_stream_cursor % len(stream_keys)
                deadline = time.monotonic() + self._pending_cycle_budget_s
                visited = 0
                for offset in range(len(stream_keys)):
                    if time.monotonic() >= deadline:
                        break
                    sk = stream_keys[(start + offset) % len(stream_keys)]
                    visited += 1
                    await self.drain_pending(
                        sk, self._consumer_name,
                        max_entries=self._batch_count, deadline=deadline,
                    )
                self._pending_stream_cursor = (
                    start + (visited if visited < len(stream_keys) else 1)
                ) % len(stream_keys)
                self._last_pending_drain = time.monotonic()

            # --- 读取新消息（缓冲区满时跳过以施加背压；count 按剩余容量截断）---
            streams_arg, read_count = self._next_read_scope(stream_keys)
            if streams_arg:
                try:
                    response = await asyncio.get_running_loop().run_in_executor(
                        None,
                        lambda: self._r.xreadgroup(
                            groupname=self._group_name,
                            consumername=self._consumer_name,
                            streams=streams_arg,
                            count=read_count,
                            block=block_ms,
                        ),
                    )
                except Exception as exc:
                    logger.error("[consumer] xreadgroup error: %s", exc)
                    await asyncio.sleep(1)
                    continue

                if response:
                    batch = [
                        (stream_key, entry_id, entry_data)
                        for stream_key, entries in response
                        for entry_id, entry_data in entries
                    ]
                    # The reserved XREADGROUP COUNT bounds preparation; a server
                    # overdelivery stays explicit in PEL without extra decoding.
                    prepare_count = min(len(batch), read_count * len(streams_arg))
                    prepared = await self._run_blocking(
                        _prepare_payload_batch,
                        [entry_data for _, _, entry_data in batch[:prepare_count]],
                        max(0, self._max_buffer_bytes - self._buffer_bytes - self._inflight_bytes),
                    )
                    for index, (stream_key, entry_id, entry_data) in enumerate(batch):
                        try:
                            await self._handle_entry(
                                stream_key=stream_key,
                                entry_id=entry_id,
                                entry_data=entry_data,
                                delivery_count=1,
                                prepared=prepared[index] if index < prepare_count else None,
                            )
                        except Exception as exc:
                            # Keep in PEL for a later retry; only confirmed bad data enters DLQ.
                            logger.error(
                                "[consumer] unexpected error handling entry %s, skipping: %s",
                                entry_id,
                                exc,
                            )
                        if index % 8 == 7:
                            await asyncio.sleep(0)
            else:
                # L1: buffer 满 → 跳过 xreadgroup，让底部 flush 块立即触发（buffer_size >= max）。
                # 失败时由 M6 退避兜底，原先的 flush_interval_s sleep 多余且增加最多 500ms 落库延迟。
                logger.debug(
                    "[consumer] local identity capacity full (%d rows, %d ACK, %d DELETE retries)",
                    len(self._buffer),
                    sum(len(ids) for ids in self._ack_retry.values()),
                    sum(len(ids) for ids in self._delete_retry.values()),
                )
                if not self._buffer:
                    await asyncio.sleep(0.05)

            # 隔离区里的行在主缓冲一空时尽快重试，避免恢复被拖到下一个回收周期。
            # 仍在失败的会再次被隔离，且有重试次数上限兜底，不会形成热循环。
            if not self._buffer and not self._inflight_rows and self._isolated:
                self._readmit_isolated()

            # --- 达到时间或缓冲区阈值时刷新 ---
            now = time.monotonic()
            buffer_size = len(self._buffer)
            if (buffer_size > 0 and not streams_arg) or buffer_size >= self._max_buffer_rows or (
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
                    if self._consecutive_flush_fail == 1 and self._syslog is not None:
                        self._syslog.emit(
                            level="WARN",
                            log_type="storage",
                            source="writer.consumer",
                            content="flush degraded: TDengine write failing, backing off",
                        )
                    await asyncio.sleep(backoff)
                else:
                    if self._consecutive_flush_fail > 0:
                        logger.info(
                            "[consumer] flush recovered after %d failures",
                            self._consecutive_flush_fail,
                        )
                        if self._syslog is not None:
                            self._syslog.emit(
                                level="INFO",
                                log_type="storage",
                                source="writer.consumer",
                                content="flush recovered",
                            )
                    self._consecutive_flush_fail = 0

            self._log_performance_if_due()

    # ------------------------------------------------------------------
    # 待处理消息恢复
    # ------------------------------------------------------------------

    async def drain_pending(
        self, stream_key: str, consumer_name: str, max_entries: int | None = None,
        deadline: float | None = None,
    ) -> None:
        """回收空闲的待处理消息并进行处理。首先尝试 XAUTOCLAIM（Redis >= 6.2）；回退到 XPENDING + XCLAIM。"""
        logger.info("[consumer] draining pending for stream %s", stream_key)
        # 隔离区里的行也属于待处理集合。必须在计算领取额度之前放回主缓冲：
        # 领取条数按剩余容量算，隔离行占着额度会把读取压成 0，自己永远轮不到重试。
        if not self._buffer and not self._inflight_rows and self._isolated:
            self._readmit_isolated()
        try:
            await self._drain_with_xautoclaim(stream_key, consumer_name, max_entries, deadline)
        except Exception as exc:
            if "unknown command" in str(exc).lower() or "xautoclaim" in str(exc).lower():
                logger.info(
                    "[consumer] XAUTOCLAIM not supported, falling back to XPENDING+XCLAIM: %s",
                    exc,
                )
                await self._drain_with_xpending_xclaim(stream_key, consumer_name, max_entries, deadline)
            else:
                logger.warning("[consumer] drain_pending error: %s", exc)

    async def _drain_with_xautoclaim(
        self, stream_key: str, consumer_name: str, max_entries: int | None = None,
        deadline: float | None = None,
    ) -> None:
        loop = asyncio.get_running_loop()
        start_id = self._pending_cursors.get(stream_key, "0-0") if max_entries else "0-0"
        processed = 0
        pages = 0
        while True:
            if (max_entries is not None and (processed >= max_entries or pages >= 2)) or (
                deadline is not None and time.monotonic() >= deadline
            ):
                self._pending_cursors[stream_key] = start_id
                break
            # DW-02：回收路径与新消息读取共用容量预算。缓冲满先 flush 腾空间，
            # 仍无空间则停止翻页（未领取条目留 PEL 等下一轮 drain），不超预算领取。
            if self._remaining_capacity() <= 0:
                await self._flush()
                if self._remaining_capacity() <= 0:
                    logger.info(
                        "[consumer] buffer full, pausing pending reclaim for %s", stream_key
                    )
                    break
            claim_count = min(
                self._batch_count, self._remaining_capacity(),
                max_entries - processed if max_entries is not None else self._batch_count,
            )
            result = await loop.run_in_executor(
                None,
                lambda sid=start_id, cnt=claim_count: self._r.xautoclaim(
                    stream_key,
                    self._group_name,
                    consumer_name,
                    min_idle_time=self._pending_min_idle_ms,
                    start_id=sid,
                    count=cnt,
                ),
            )
            # 结果为 (next_start_id, [(entry_id, data), ...], deleted_ids)
            next_id, claimed_entries, _ = result
            pages += 1
            processed += len(claimed_entries)
            delivery_counts = await self._run_blocking(
                self._get_delivery_counts,
                stream_key,
                [entry_id for entry_id, _ in claimed_entries],
            )
            for entry_id, entry_data in claimed_entries:
                try:
                    delivery_count = delivery_counts.get(entry_id, 1)
                    await self._handle_entry(stream_key, entry_id, entry_data, delivery_count)
                except Exception as exc:
                    # Keep in PEL for a later retry; only confirmed bad data enters DLQ.
                    logger.error(
                        "[consumer] unexpected error handling pending entry %s, skipping: %s",
                        entry_id, exc,
                    )

            if claimed_entries:
                await self._flush()

            if next_id in ("0-0", "0"):
                self._pending_cursors[stream_key] = "0-0"
                break
            start_id = next_id

    async def _drain_with_xpending_xclaim(
        self, stream_key: str, consumer_name: str, max_entries: int | None = None,
        deadline: float | None = None,
    ) -> None:
        loop = asyncio.get_running_loop()
        cursor = self._pending_cursors.get(stream_key, "-") if max_entries else "-"
        processed = 0
        pages = 0
        while True:
            if (max_entries is not None and (processed >= max_entries or pages >= 2)) or (
                deadline is not None and time.monotonic() >= deadline
            ):
                self._pending_cursors[stream_key] = cursor
                break
            # DW-02：与 XAUTOCLAIM 路径共用容量预算；无空间先 flush，仍满则停。
            if self._remaining_capacity() <= 0:
                await self._flush()
                if self._remaining_capacity() <= 0:
                    logger.info(
                        "[consumer] buffer full, pausing pending reclaim for %s", stream_key
                    )
                    break
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
                self._pending_cursors[stream_key] = "-"
                break
            pages += 1

            eligible_ids = [
                p["message_id"]
                for p in pending_entries
                if p.get("time_since_delivered", 0) >= self._pending_min_idle_ms
            ]
            # DW-02：领取数量按剩余容量截断；本页未领取的条目留在 PEL，
            # 等下一轮 drain 回收，不静默丢弃。
            claim_ids = eligible_ids[: min(
                self._remaining_capacity(),
                max_entries - processed if max_entries is not None else self._batch_count,
            )]
            processed += len(claim_ids)
            truncated = len(claim_ids) < len(eligible_ids)
            if claim_ids:
                claimed = await loop.run_in_executor(
                    None,
                    lambda ids=claim_ids: self._r.xclaim(
                        stream_key,
                        self._group_name,
                        consumer_name,
                        min_idle_time=self._pending_min_idle_ms,
                        message_ids=ids,
                    ),
                )
                for entry_id, entry_data in claimed:
                    try:
                        # delivery_count 来自 xpending_range
                        dc = next(
                            (p["times_delivered"] for p in pending_entries if p["message_id"] == entry_id),
                            1,
                        )
                        await self._handle_entry(stream_key, entry_id, entry_data, dc)
                    except Exception as exc:
                        # Keep in PEL for a later retry; only confirmed bad data enters DLQ.
                        logger.error(
                            "[consumer] unexpected error handling pending entry %s, skipping: %s",
                            entry_id, exc,
                        )

                if claimed:
                    await self._flush()

            if truncated:
                self._pending_cursors[stream_key] = (
                    f"({claim_ids[-1]}" if claim_ids else cursor
                )
                # 本页有未领取条目，停在当前游标，留给下一轮 drain，避免翻页跳过。
                break

            last_id = pending_entries[-1]["message_id"]
            cursor = f"({last_id}"  # exclusive：下一页从此 ID 之后开始
            if len(pending_entries) < self._batch_count:
                self._pending_cursors[stream_key] = "-"
                break  # 已是最后一页

    def _get_delivery_counts(self, stream_key: str, entry_ids: list[str]) -> dict[str, int]:
        """通过一次 XPENDING 调用获取一批条目的投递计数。"""
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
    # 条目解析
    # ------------------------------------------------------------------

    async def _handle_entry(
        self,
        stream_key: str,
        entry_id: str,
        entry_data: dict,
        delivery_count: int,
        prepared: _PreparedPayload | None = None,
    ) -> None:
        key = (stream_key, entry_id)
        if entry_id in self._ack_retry.get(stream_key, ()):
            # TDengine already committed this frame; only its XACK is outstanding.
            return
        if key in self._buffered_keys or key in self._isolated_keys:
            # flush 失败行既在缓冲又在 PEL，drain 重新领取同一条目时直接跳过，
            # 防止重复入缓冲、重复写库（DW-02）。
            logger.debug("[consumer] entry %s already buffered, skipping duplicate", entry_id)
            return

        payload_bytes = prepared.payload_bytes if prepared is not None else (
            sum(
                len(str(key).encode("utf-8")) + len(str(value).encode("utf-8"))
                for key, value in entry_data.items()
            )
            if isinstance(entry_data, dict) else 0
        )
        byte_capacity = self._max_buffer_bytes - self._buffer_bytes - self._inflight_bytes
        if self._remaining_capacity() <= 0 or (
            payload_bytes > byte_capacity
            and (self._buffer or self._inflight_rows)
        ):
            self._overdelivered_ids.setdefault(stream_key, set()).add(entry_id)
            return  # Already delivered by Redis; reclaim as soon as capacity returns.

        if prepared is not None and not prepared.decoded:
            prepared = None
        if not isinstance(entry_data, dict):
            await self._run_blocking(
                self._move_entry_to_dlq,
                stream_key,
                entry_id,
                entry_data,
                "unknown",
                "unknown",
                delivery_count,
                "entry_data is not a mapping",
            )
            return

        device_code = entry_data.get("device_code", "")
        device_type = entry_data.get("device_type", "")
        group_code = entry_data.get("group_code", "")
        ts_raw = entry_data.get("ts", "")
        config_version_raw = entry_data.get("config_version", "0")
        values_raw = entry_data.get("values")

        # --- 解析必填字段 ---
        if (
            not isinstance(device_code, str)
            or not device_code.strip()
            or not isinstance(group_code, str)
            or not group_code.strip()
        ):
            await self._run_blocking(
                self._move_entry_to_dlq,
                stream_key,
                entry_id,
                entry_data,
                device_type,
                group_code,
                delivery_count,
                "missing or invalid device_code/group_code",
            )
            return

        try:
            config_version = int(config_version_raw)
        except (ValueError, TypeError):
            await self._run_blocking(
                self._move_entry_to_dlq,
                stream_key,
                entry_id,
                entry_data,
                device_type,
                group_code,
                delivery_count,
                f"config_version_invalid: {config_version_raw!r}",
            )
            return

        try:
            ts = int(ts_raw)
        except (ValueError, TypeError):
            await self._run_blocking(
                self._move_entry_to_dlq,
                stream_key,
                entry_id,
                entry_data,
                device_type,
                group_code,
                delivery_count,
                f"timestamp_invalid: {ts_raw!r}",
            )
            return

        parse_started = time.perf_counter()
        try:
            if prepared is not None:
                if prepared.values_error is not None:
                    raise prepared.values_error
                raw_values = prepared.raw_values
                self._metrics.record_parse(prepared.values_parse_ms)
            else:
                raw_values: dict[str, str] = json.loads(values_raw)
                self._metrics.record_parse((time.perf_counter() - parse_started) * 1000)
        except Exception as exc:
            logger.warning("[consumer] invalid values JSON for entry %s: %s", entry_id, exc)
            await self._run_blocking(
                self._move_entry_to_dlq,
                stream_key,
                entry_id,
                entry_data,
                device_type,
                group_code,
                delivery_count,
                f"values_json_invalid: {exc}",
            )
            return
        if not isinstance(raw_values, dict):
            logger.warning(
                "[consumer] values is not a JSON object for entry %s: %r", entry_id, raw_values
            )
            await self._run_blocking(
                self._move_entry_to_dlq,
                stream_key,
                entry_id,
                entry_data,
                device_type,
                group_code,
                delivery_count,
                "values is not a JSON object",
            )
            return

        # 缓存命中走事件循环内快路径；只有首次 MySQL 加载进入 executor。
        mapping = self._mapper.get_cached(device_code, group_code)
        cache_hit = mapping is not None
        if mapping is None:
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

        # 永久 mapping 不按 config_version 失效，以便解码历史 entry；但同组新增点
        # 需要让已热缓存的 worker 重新读取一次永久 mapping。若仍未知，保留 entry
        # 在 PEL，不能只写旧点后 ACK 造成新点静默漏写。
        unmapped_var_ids = [
            str(var_id)
            for var_id in raw_values
            if not mapping.var_map.get(str(var_id))
        ]
        if unmapped_var_ids and cache_hit:
            mapping = await asyncio.get_running_loop().run_in_executor(
                None,
                self._mapper.reload,
                device_code,
                group_code,
                config_version,
            )
            if mapping is None:
                logger.warning(
                    "[consumer] mapping reload failed for device=%s group=%s "
                    "unknown var_ids=%s, leaving entry %s pending",
                    device_code,
                    group_code,
                    unmapped_var_ids,
                    entry_id,
                )
                return
            unmapped_var_ids = [
                var_id
                for var_id in unmapped_var_ids
                if not mapping.var_map.get(var_id)
            ]
        if unmapped_var_ids:
            logger.warning(
                "[consumer] unknown var_ids after mapping load for "
                "device=%s group=%s: %s; leaving entry %s pending",
                device_code,
                group_code,
                unmapped_var_ids,
                entry_id,
            )
            return

        # --- 将 var_id 转换为 td_column_name，带类型强制转换 ---
        # 任一非法值整 entry 隔离：禁止写假 0.0，推 DLQ 后再决定是否 ACK
        columns: dict[str, float] = {}
        bad_var_ids: list[str] = []
        parse_started = time.perf_counter()
        for var_id, str_val in raw_values.items():
            col_name = mapping.var_map.get(var_id)
            if not col_name:
                continue
            try:
                num = float(str_val)
            except (ValueError, TypeError):
                bad_var_ids.append(str(var_id))
                continue
            if not math.isfinite(num):
                bad_var_ids.append(str(var_id))
                continue
            columns[col_name] = num
        self._metrics.record_parse((time.perf_counter() - parse_started) * 1000)

        if bad_var_ids:
            last_error = f"type_conversion failed var_ids={','.join(bad_var_ids)}"
            moved = await self._run_blocking(
                self._push_dlq,
                device_type=device_type or "unknown",
                group_code=group_code or "unknown",
                entry_id=entry_id,
                stream_key=stream_key,
                data=dict(entry_data),
                retry_count=delivery_count,
                last_error=last_error,
            )
            if moved:
                await self._run_blocking(self._ack_single, stream_key, entry_id)
            else:
                logger.error(
                    "[consumer] DLQ failed for type_conversion entry %s, not acking",
                    entry_id,
                )
            return

        if not columns:
            logger.debug("[consumer] entry %s has no mappable columns, acking", entry_id)
            await self._run_blocking(self._ack_single, stream_key, entry_id)
            return

        # --- Stream 质量：v1 不写窄表；v2 严格校验后写入 ---
        qualities: dict[str, int] = {}
        if "contract_version" not in entry_data:
            contract_version = 1
        else:
            try:
                contract_version = int(entry_data["contract_version"])
            except (ValueError, TypeError):
                moved = await self._run_blocking(
                    self._push_dlq,
                    device_type=device_type or "unknown",
                    group_code=group_code or "unknown",
                    entry_id=entry_id,
                    stream_key=stream_key,
                    data=dict(entry_data),
                    retry_count=delivery_count,
                    last_error=(
                        f"Unsupported contract_version: {entry_data.get('contract_version')!r}"
                    ),
                )
                if moved:
                    await self._run_blocking(self._ack_single, stream_key, entry_id)
                return
            if contract_version not in (1, 2):
                moved = await self._run_blocking(
                    self._push_dlq,
                    device_type=device_type or "unknown",
                    group_code=group_code or "unknown",
                    entry_id=entry_id,
                    stream_key=stream_key,
                    data=dict(entry_data),
                    retry_count=delivery_count,
                    last_error=f"Unsupported contract_version: {contract_version}",
                )
                if moved:
                    await self._run_blocking(self._ack_single, stream_key, entry_id)
                return

        if contract_version == 2:
            qualities_raw = entry_data.get("qualities", "{}")
            parse_started = time.perf_counter()
            try:
                if prepared is not None:
                    if prepared.qualities_error is not None:
                        raise prepared.qualities_error
                    raw_qualities = prepared.raw_qualities
                    self._metrics.record_parse(prepared.qualities_parse_ms)
                else:
                    if isinstance(qualities_raw, (bytes, bytearray)):
                        qualities_raw = qualities_raw.decode("utf-8")
                    raw_qualities = (
                        json.loads(qualities_raw)
                        if isinstance(qualities_raw, str)
                        else qualities_raw
                    )
                    self._metrics.record_parse((time.perf_counter() - parse_started) * 1000)
            except Exception as exc:
                logger.warning(
                    "[consumer] invalid qualities JSON for entry %s: %s", entry_id, exc
                )
                moved = await self._run_blocking(
                    self._push_dlq,
                    device_type=device_type or "unknown",
                    group_code=group_code or "unknown",
                    entry_id=entry_id,
                    stream_key=stream_key,
                    data=dict(entry_data),
                    retry_count=delivery_count,
                    last_error=f"qualities_json_invalid: {exc}",
                )
                if moved:
                    await self._run_blocking(self._ack_single, stream_key, entry_id)
                return
            if not isinstance(raw_qualities, dict):
                moved = await self._run_blocking(
                    self._push_dlq,
                    device_type=device_type or "unknown",
                    group_code=group_code or "unknown",
                    entry_id=entry_id,
                    stream_key=stream_key,
                    data=dict(entry_data),
                    retry_count=delivery_count,
                    last_error="qualities is not a JSON object",
                )
                if moved:
                    await self._run_blocking(self._ack_single, stream_key, entry_id)
                return

            # 过滤非存储点后，values 与 qualities 键集合必须对齐（设计契约）
            raw_qualities_norm = {str(k): v for k, v in raw_qualities.items()}
            stored_value_ids = {
                str(vid) for vid in raw_values if vid in mapping.var_map
            }
            stored_quality_ids = {
                vid for vid in raw_qualities_norm if vid in mapping.var_map
            }
            if stored_value_ids != stored_quality_ids:
                moved = await self._run_blocking(
                    self._push_dlq,
                    device_type=device_type or "unknown",
                    group_code=group_code or "unknown",
                    entry_id=entry_id,
                    stream_key=stream_key,
                    data=dict(entry_data),
                    retry_count=delivery_count,
                    last_error=(
                        f"quality_key_mismatch values={sorted(stored_value_ids)} "
                        f"qualities={sorted(stored_quality_ids)}"
                    ),
                )
                if moved:
                    await self._run_blocking(self._ack_single, stream_key, entry_id)
                return

            bad_quality_var_ids: list[str] = []
            for var_id in stored_value_ids:
                q_code = raw_qualities_norm[var_id]
                if not is_valid_quality(q_code):
                    bad_quality_var_ids.append(str(var_id))
                    continue
                qualities[str(var_id)] = int(q_code)

            if bad_quality_var_ids:
                last_error = (
                    f"invalid_quality var_ids={','.join(bad_quality_var_ids)}"
                )
                moved = await self._run_blocking(
                    self._push_dlq,
                    device_type=device_type or "unknown",
                    group_code=group_code or "unknown",
                    entry_id=entry_id,
                    stream_key=stream_key,
                    data=dict(entry_data),
                    retry_count=delivery_count,
                    last_error=last_error,
                )
                if moved:
                    await self._run_blocking(self._ack_single, stream_key, entry_id)
                else:
                    logger.error(
                        "[consumer] DLQ failed for invalid_quality entry %s, not acking",
                        entry_id,
                    )
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
                qualities=qualities,
                entry_data=dict(entry_data),
                delivery_count=delivery_count,
                payload_bytes=payload_bytes,
            )
        )
        self._buffer_bytes += payload_bytes
        self._metrics.record_payload_bytes(payload_bytes)
        self._metrics.set_buffer_bytes(self._buffer_bytes + self._inflight_bytes)
        self._estimated_entry_bytes = (7 * self._estimated_entry_bytes + payload_bytes) // 8 or 1
        self._buffered_keys.add(key)
        self._metrics.set_buffer_rows(len(self._buffer))

    # ------------------------------------------------------------------
    # 刷新
    # ------------------------------------------------------------------

    async def _flush(self) -> None:
        async with self._flush_lock:
            await self._flush_owned()

    async def _flush_owned(self) -> None:
        if not self._buffer:
            self._last_flush = time.monotonic()
            return

        flush_started = time.perf_counter()
        buffered = self._buffer
        self._buffer = []
        self._inflight_rows = len(buffered)
        self._inflight_source_ms = min(row.ts for row in buffered)
        self._inflight_bytes = self._buffer_bytes
        self._buffer_bytes = 0
        self._metrics.set_buffer_bytes(self._inflight_bytes)
        flush_rows = len(buffered)
        completed = False
        try:
            rows = [
                {
                    "child_table": row.child_table,
                    "stable": row.stable,
                    "device_code": row.device_code,
                    "ts": row.ts,
                    "columns": row.columns,
                    "qualities": row.qualities,
                }
                for row in buffered
            ]
            chunk_results = await self._writer.write_multi_table(rows)
            retry_reasons = getattr(self._writer, "_last_retry_reasons", {})
            if not isinstance(retry_reasons, dict):
                retry_reasons = {}
            local_retry = getattr(self._writer, "_last_local_retry_indices", set())
            if not isinstance(local_retry, set):
                local_retry = set()
            ack_ms = 0.0
            quality_rows_count = 0
            covered = [index for indices, _, _ in chunk_results for index in indices]
            if sorted(covered) != list(range(flush_rows)):
                raise RuntimeError("TDengine writer returned incomplete or duplicate row outcomes")

            to_ack: list[_BufferedRow] = []
            failed_rows: list[_BufferedRow] = []
            isolated: list[_BufferedRow] = []
            landed_rows_count = 0
            for indices, status, elapsed_ms in chunk_results:
                if status not in (LANDED, BAD_DATA, RETRY):
                    raise RuntimeError(f"invalid TDengine row outcome: {status!r}")
                chunk_rows = [buffered[index] for index in indices]
                self._metrics.record_write_batch(elapsed_ms, len(chunk_rows), status == LANDED)
                if status == LANDED:
                    to_ack.extend(chunk_rows)
                    landed_rows_count += len(chunk_rows)
                    quality_rows_count += sum(len(row.qualities) for row in chunk_rows)
                elif status == BAD_DATA:
                    for row in chunk_rows:
                        if await self._run_blocking(self._push_row_to_dlq, row):
                            to_ack.append(row)
                        else:
                            failed_rows.append(row)
                else:
                    for index, row in zip(indices, chunk_rows):
                        reason = retry_reasons.get(index, "unknown")
                        self._metrics.record_retry(reason)
                        row.retry_attempts += 1
                        # schema/size 类重试（0x0219 过长、二分深度用尽）从来不是
                        # "再试一次就好"：它们会一直失败，留在主缓冲只会占容量并拖住
                        # 同 worker 其它 stream。按既有契约从第一次起就只留在 PEL，
                        # 走隔离区、按回收周期重试，不占主缓冲。
                        # `_last_local_retry_indices` 是 writer 标记"本次未执行的本地
                        # 重试行"的既有接口，单独看 retry_reasons 会漏掉它。
                        if (reason == "size" or index in local_retry
                                or row.retry_attempts >= self._max_row_retry):
                            isolated.append(row)
                        else:
                            logger.warning(
                                "[consumer] transient retry %d/%d deferred in PEL: %s %s",
                                row.retry_attempts, self._max_row_retry,
                                row.stream_key, row.entry_id,
                            )
                            failed_rows.append(row)

            if to_ack:
                ack_started = time.perf_counter()
                await self._run_blocking(self._ack_rows, to_ack)
                ack_ms = (time.perf_counter() - ack_started) * 1000
            def stat(name: str) -> float:
                value = getattr(self._writer, name, 0)
                return float(value) if isinstance(value, (int, float)) else 0.0

            sql_sizes = getattr(self._writer, "_last_sql_statement_bytes", [])
            if not isinstance(sql_sizes, (list, tuple)):
                sql_sizes = []
            self._metrics.record_pipeline(
                build_ms=stat("_last_build_ms"),
                build_wait_ms=stat("_last_build_wait_ms"),
                execute_ms=stat("_last_execute_ms"),
                ack_ms=ack_ms,
                sql_bytes=int(stat("_last_sql_bytes")),
                sql_statement_bytes=sql_sizes,
                bisect_attempts=int(stat("_last_bisect_attempts")),
                value_rows=landed_rows_count,
                quality_rows=quality_rows_count,
                sql_value_rows_attempted=int(stat("_last_sql_value_rows_attempted")),
                sql_quality_rows_attempted=int(stat("_last_sql_quality_rows_attempted")),
                sql_value_rows_succeeded=int(stat("_last_sql_value_rows_succeeded")),
                sql_quality_rows_succeeded=int(stat("_last_sql_quality_rows_succeeded")),
            )
            if landed_rows_count > 0:
                try:
                    await self._run_blocking(
                        self._r.hincrby, "rt:metrics:writer", "rows_written", landed_rows_count
                    )
                except Exception:
                    logger.warning("[consumer] metrics hincrby failed", exc_info=True)
            self._buffer = failed_rows + self._buffer
            if isolated:
                # 隔离区超限时不再接收新的隔离行，改为留在主缓冲里形成背压。
                room = self._max_isolated_rows - len(self._isolated)
                if room > 0:
                    accepted_isolated = isolated[:room]
                    self._isolated.extend(accepted_isolated)
                    self._isolated_keys.update(
                        (row.stream_key, row.entry_id) for row in accepted_isolated
                    )
                    for row in accepted_isolated:
                        logger.warning(
                            "[consumer] row isolated after %d retries (still in PEL, "
                            "no longer blocking other streams): %s %s",
                            row.retry_attempts, row.stream_key, row.entry_id,
                        )
                    isolated = isolated[room:]
                if isolated:
                    self._buffer = isolated + self._buffer
                elif len(self._isolated) >= self._max_isolated_rows:
                    logger.error(
                        "[consumer] isolated retry set at capacity (%d rows); "
                        "restoring backpressure instead of growing unbounded",
                        len(self._isolated),
                    )
            completed = True
        finally:
            # Cancellation and invariant errors must leave every uncommitted
            # source entry tracked in memory and in its Redis PEL.
            if not completed:
                self._buffer = buffered + self._buffer
                self._metrics.record_write_batch(
                    (time.perf_counter() - flush_started) * 1000, flush_rows, False
                )
            self._inflight_rows = 0
            self._inflight_bytes = 0
            self._inflight_source_ms = None
            self._buffer_bytes = sum(row.payload_bytes for row in self._buffer)
            self._metrics.set_buffer_bytes(self._buffer_bytes)
            self._buffered_keys = {
                (row.stream_key, row.entry_id) for row in self._buffer
            }
            self._metrics.set_buffer_rows(len(self._buffer), len(self._isolated))
            self._metrics.record_flush(
                (time.perf_counter() - flush_started) * 1000, flush_rows
            )
            self._last_flush = time.monotonic()

    def _log_performance_if_due(self) -> None:
        now = time.monotonic()
        if now - self._last_performance_log < self._performance_log_interval_s:
            return
        logger.info(
            "[performance] %s",
            json.dumps(
                {"consumer_name": self._consumer_name, **self._metrics.snapshot()},
                separators=(",", ":"),
            ),
        )
        self._last_performance_log = now

    def _push_row_to_dlq(self, row: _BufferedRow) -> bool:
        """Push a binary-search-isolated bad row to the DLQ. Returns True if stored.

        调用方只在返回 True 时 ACK；False 时该行留 buffer 重试（硬约束②：DLQ
        成功才 ACK，推送失败不丢数据）。syslog 与 exceeded-max-retry 路径同口径限流。
        """
        moved = self._push_dlq(
            device_type=row.device_type or "unknown",
            group_code=row.group_code or "unknown",
            entry_id=row.entry_id,
            stream_key=row.stream_key,
            data=row.entry_data,
            retry_count=row.delivery_count,
            last_error="bad data isolated by binary-search degrade",
        )
        if moved:
            if self._syslog is not None:
                now = time.monotonic()
                if self._last_dlq_moved_log is None or now - self._last_dlq_moved_log >= 60.0:
                    self._last_dlq_moved_log = now
                    self._syslog.emit(
                        level="ERROR",
                        log_type="storage",
                        source="writer.dlq",
                        content=(
                            f"bad-data entry {row.entry_id} moved to DLQ: "
                            f"{row.device_type or 'unknown'}/{row.group_code or 'unknown'}"
                        ),
                    )
        else:
            logger.error(
                "[consumer] DLQ push failed for bad-data entry %s, keeping buffered", row.entry_id
            )
            if self._syslog is not None:
                now = time.monotonic()
                if self._last_dlq_failed_log is None or now - self._last_dlq_failed_log >= 60.0:
                    self._last_dlq_failed_log = now
                    self._syslog.emit(
                        level="ERROR",
                        log_type="storage",
                        source="writer.dlq",
                        content=(
                            f"DLQ push failed for bad-data entry {row.entry_id}: "
                            f"{row.device_type or 'unknown'}/{row.group_code or 'unknown'}"
                        ),
                    )
        return moved

    def _ack_rows(self, rows: list[_BufferedRow]) -> None:
        """按 stream_key 分组后 XACK 确认行。"""
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
                self._delete_ids(stream_key, chunk)
            except Exception as exc:
                logger.warning("[consumer] xack failed for %s: %s", stream_key, exc)
                self._ack_retry.setdefault(stream_key, set()).update(chunk)

    def _ack_single(self, stream_key: str, entry_id: str) -> None:
        try:
            self._r.xack(stream_key, self._group_name, entry_id)
            self._delete_ids(stream_key, [entry_id])
        except Exception as exc:
            logger.warning("[consumer] xack single failed %s: %s", entry_id, exc)
            self._ack_retry.setdefault(stream_key, set()).add(entry_id)

    def _retry_pending_acks(
        self, max_ids: int | None = None, deadline: float | None = None,
    ) -> None:
        """Retry landed rows' XACKs in bounded, round-robin stream batches."""
        keys = list(self._ack_retry)
        if not keys:
            return
        remaining = max_ids if max_ids is not None else sum(len(ids) for ids in self._ack_retry.values())
        start = self._ack_retry_cursor % len(keys)
        visited = 0
        for offset in range(len(keys)):
            if remaining <= 0 or (deadline is not None and time.monotonic() >= deadline):
                break
            stream_key = keys[(start + offset) % len(keys)]
            visited += 1
            pending = self._ack_retry.get(stream_key)
            if not pending:
                continue
            chunk = list(pending)[: min(500, remaining)]
            try:
                self._r.xack(stream_key, self._group_name, *chunk)
                pending.difference_update(chunk)
                self._delete_ids(stream_key, chunk)
            except Exception as exc:
                logger.warning(
                    "[consumer] xack retry still failing for %s (%d ids pending): %s",
                    stream_key, len(pending), exc,
                )
            remaining -= len(chunk)
            if not pending:
                del self._ack_retry[stream_key]
        self._ack_retry_cursor = (start + (visited or 1)) % len(keys)

    def _delete_ids(self, stream_key: str, ids: list[str]) -> None:
        """删除已经终态并完成 XACK 的 Stream 条目。"""
        chunk_size = 500
        for i in range(0, len(ids), chunk_size):
            chunk = ids[i : i + chunk_size]
            try:
                self._r.xdel(stream_key, *chunk)
            except Exception as exc:
                logger.warning("[consumer] xdel failed for %s: %s", stream_key, exc)
                self._delete_retry.setdefault(stream_key, set()).update(chunk)

    def _retry_pending_deletes(
        self, max_ids: int | None = None, deadline: float | None = None,
    ) -> None:
        """Retry terminal XDELs without stalling new consumption."""
        keys = list(self._delete_retry)
        if not keys:
            return
        remaining = max_ids if max_ids is not None else sum(len(ids) for ids in self._delete_retry.values())
        start = self._delete_retry_cursor % len(keys)
        visited = 0
        for offset in range(len(keys)):
            if remaining <= 0 or (deadline is not None and time.monotonic() >= deadline):
                break
            stream_key = keys[(start + offset) % len(keys)]
            visited += 1
            pending = self._delete_retry.get(stream_key)
            if not pending:
                continue
            chunk = list(pending)[: min(500, remaining)]
            try:
                self._r.xdel(stream_key, *chunk)
                pending.difference_update(chunk)
            except Exception as exc:
                logger.warning(
                    "[consumer] xdel retry still failing for %s (%d ids pending): %s",
                    stream_key, len(pending), exc,
                )
            remaining -= len(chunk)
            if not pending:
                del self._delete_retry[stream_key]
        self._delete_retry_cursor = (start + (visited or 1)) % len(keys)

    # ------------------------------------------------------------------
    # 设置辅助函数
    # ------------------------------------------------------------------

    def _ensure_consumer_groups(self, stream_keys: list[str]) -> None:
        """如果不存在，为所有流键创建消费者组（MKSTREAM）。"""
        for sk in stream_keys:
            try:
                self._r.xgroup_create(sk, self._group_name, id="0", mkstream=True)
                logger.info("[consumer] created consumer group '%s' on %s", self._group_name, sk)
            except redis.exceptions.ResponseError as exc:
                if "BUSYGROUP" in str(exc):
                    pass  # already exists, fine
                else:
                    logger.warning("[consumer] xgroup_create error for %s: %s", sk, exc)
