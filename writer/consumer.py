"""Redis Stream 消费者，批量写入 TDengine。Redis Stream consumer that batch-writes to TDengine.

Each stream entry (produced by the collector) has these fields:
    device_code, device_type, group_code, scan_rate, ts,
    config_version, values (JSON str of {var_id: str_value})
    可选 v2：contract_version=2, qualities (JSON str of {var_id: int})

Processing pipeline per entry:
    1. Parse fields
    2. Get GroupMapping from MysqlMapper (cached by config_version)
    3. Translate var_id→td_column_name and accumulate into buffer
    4. On flush: write_multi_table (chunked) → XACK rows of successful chunks
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
import math
import time
from dataclasses import dataclass, field

import redis

from dlq import push_dlq
from mysql_mapper import MysqlMapper
from performance import WriterPerformanceMetrics
from quality_codes import is_valid_quality
from tdengine_writer import BAD_DATA, LANDED, TDengineWriter

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
        self._pending_min_idle_ms: int = w["pending_min_idle_ms"]
        self._pending_max_retry: int = w["pending_max_retry"]
        self._group_name: str = rc["group_name"]
        self._consumer_name: str = rc["consumer_name"]

        self._buffer: list[_BufferedRow] = []
        self._last_flush: float = time.monotonic()
        self._last_pending_drain: float = time.monotonic()
        self._pending_drain_interval_s: float = 60.0
        self._ack_retry: dict[str, set[str]] = {}  # stream_key → 待重试 XACK 的 entry_id (M5)
        self._delete_retry: dict[str, set[str]] = {}  # 已终态且已 ACK，待重试 XDEL 的 entry_id
        self._consecutive_flush_fail: int = 0  # M6：连续 flush 失败次数，用于退避与日志降噪
        # 防刷库：坏数据洪峰下 DLQ 事件可能逐条触发，各自 ≥60s 限流写 system_log
        self._last_dlq_moved_log: float | None = None
        self._last_dlq_failed_log: float | None = None

    # ------------------------------------------------------------------
    # 公共入口点
    # ------------------------------------------------------------------

    async def run_loop(self, stream_keys: list[str]) -> None:
        """主消费循环。运行直到被取消。"""
        if not stream_keys:
            raise ValueError("stream_keys is empty — check writer config device_types/group_codes")
        self._ensure_consumer_groups(stream_keys)

        # 排空上次运行/崩溃消费者遗留的待处理消息。
        for sk in stream_keys:
            await self.drain_pending(sk, self._consumer_name)

        logger.info(
            "[consumer] starting main loop, %d stream key(s), consumer=%s",
            len(stream_keys),
            self._consumer_name,
        )

        block_ms = max(int(self._flush_interval_s * 1000 / 2), 50)

        while True:
            # --- 首先重试之前失败的 XACK（M5）---
            # 必须早于 drain：否则已成功落库但 XACK 失败、又满足 idle 条件的条目
            # 会被 drain reclaim 触发重写，违背 M5 「避免重复写入」的目标。
            self._retry_pending_acks()
            self._retry_pending_deletes()

            # --- 定期回收空闲的待处理消息（S2）---
            if time.monotonic() - self._last_pending_drain >= self._pending_drain_interval_s:
                for sk in stream_keys:
                    await self.drain_pending(sk, self._consumer_name)
                self._last_pending_drain = time.monotonic()

            # --- 读取新消息（缓冲区满时跳过以施加背压）---
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

            # --- 达到时间或缓冲区阈值时刷新 ---
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

    async def drain_pending(self, stream_key: str, consumer_name: str) -> None:
        """回收空闲的待处理消息并进行处理。首先尝试 XAUTOCLAIM（Redis >= 6.2）；回退到 XPENDING + XCLAIM。"""
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
            # 结果为 (next_start_id, [(entry_id, data), ...], deleted_ids)
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
                        # delivery_count 来自 xpending_range
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
    ) -> None:
        device_code = entry_data.get("device_code", "")
        device_type = entry_data.get("device_type", "")
        group_code = entry_data.get("group_code", "")
        ts_raw = entry_data.get("ts", "")
        config_version_raw = entry_data.get("config_version", "0")
        values_raw = entry_data.get("values", "{}")

        # --- 首先检查死信队列（DLQ）---
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
                if self._syslog is not None:
                    now = time.monotonic()
                    if self._last_dlq_moved_log is None or now - self._last_dlq_moved_log >= 60.0:
                        self._last_dlq_moved_log = now
                        self._syslog.emit(
                            level="ERROR",
                            log_type="storage",
                            source="writer.dlq",
                            content=(
                                f"entry {entry_id} moved to DLQ: "
                                f"{device_type or 'unknown'}/{group_code or 'unknown'} "
                                f"after {delivery_count} retries"
                            ),
                        )
            else:
                logger.error("[consumer] DLQ failed for entry %s, not acking", entry_id)
                if self._syslog is not None:
                    now = time.monotonic()
                    if self._last_dlq_failed_log is None or now - self._last_dlq_failed_log >= 60.0:
                        self._last_dlq_failed_log = now
                        self._syslog.emit(
                            level="ERROR",
                            log_type="storage",
                            source="writer.dlq",
                            content=(
                                f"DLQ push failed for entry {entry_id}: "
                                f"{device_type or 'unknown'}/{group_code or 'unknown'}"
                            ),
                        )
            return

        # --- 解析必填字段 ---
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

        # 缓存命中走事件循环内快路径；只有首次 MySQL 加载进入 executor。
        mapping = self._mapper.get_cached(device_code, group_code)
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

        # --- 将 var_id 转换为 td_column_name，带类型强制转换 ---
        # 任一非法值整 entry 隔离：禁止写假 0.0，推 DLQ 后再决定是否 ACK
        columns: dict[str, float] = {}
        bad_var_ids: list[str] = []
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

        if bad_var_ids:
            last_error = f"type_conversion failed var_ids={','.join(bad_var_ids)}"
            moved = push_dlq(
                self._r,
                device_type=device_type or "unknown",
                group_code=group_code or "unknown",
                entry_id=entry_id,
                stream_key=stream_key,
                data=dict(entry_data),
                retry_count=delivery_count,
                last_error=last_error,
            )
            if moved:
                self._ack_single(stream_key, entry_id)
            else:
                logger.error(
                    "[consumer] DLQ failed for type_conversion entry %s, not acking",
                    entry_id,
                )
            return

        if not columns:
            logger.debug("[consumer] entry %s has no mappable columns, acking", entry_id)
            self._ack_single(stream_key, entry_id)
            return

        # --- Stream 质量：v1 不写窄表；v2 严格校验后写入 ---
        qualities: dict[str, int] = {}
        if "contract_version" not in entry_data:
            contract_version = 1
        else:
            try:
                contract_version = int(entry_data["contract_version"])
            except (ValueError, TypeError):
                moved = push_dlq(
                    self._r,
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
                    self._ack_single(stream_key, entry_id)
                return
            if contract_version not in (1, 2):
                moved = push_dlq(
                    self._r,
                    device_type=device_type or "unknown",
                    group_code=group_code or "unknown",
                    entry_id=entry_id,
                    stream_key=stream_key,
                    data=dict(entry_data),
                    retry_count=delivery_count,
                    last_error=f"Unsupported contract_version: {contract_version}",
                )
                if moved:
                    self._ack_single(stream_key, entry_id)
                return

        if contract_version == 2:
            qualities_raw = entry_data.get("qualities", "{}")
            try:
                if isinstance(qualities_raw, (bytes, bytearray)):
                    qualities_raw = qualities_raw.decode("utf-8")
                raw_qualities = (
                    json.loads(qualities_raw)
                    if isinstance(qualities_raw, str)
                    else qualities_raw
                )
            except Exception as exc:
                logger.warning(
                    "[consumer] invalid qualities JSON for entry %s: %s", entry_id, exc
                )
                moved = push_dlq(
                    self._r,
                    device_type=device_type or "unknown",
                    group_code=group_code or "unknown",
                    entry_id=entry_id,
                    stream_key=stream_key,
                    data=dict(entry_data),
                    retry_count=delivery_count,
                    last_error=f"qualities_json_invalid: {exc}",
                )
                if moved:
                    self._ack_single(stream_key, entry_id)
                return
            if not isinstance(raw_qualities, dict):
                moved = push_dlq(
                    self._r,
                    device_type=device_type or "unknown",
                    group_code=group_code or "unknown",
                    entry_id=entry_id,
                    stream_key=stream_key,
                    data=dict(entry_data),
                    retry_count=delivery_count,
                    last_error="qualities is not a JSON object",
                )
                if moved:
                    self._ack_single(stream_key, entry_id)
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
                moved = push_dlq(
                    self._r,
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
                    self._ack_single(stream_key, entry_id)
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
                moved = push_dlq(
                    self._r,
                    device_type=device_type or "unknown",
                    group_code=group_code or "unknown",
                    entry_id=entry_id,
                    stream_key=stream_key,
                    data=dict(entry_data),
                    retry_count=delivery_count,
                    last_error=last_error,
                )
                if moved:
                    self._ack_single(stream_key, entry_id)
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
            )
        )
        self._metrics.set_buffer_rows(len(self._buffer))

    # ------------------------------------------------------------------
    # 刷新
    # ------------------------------------------------------------------

    async def _flush(self) -> None:
        if not self._buffer:
            self._last_flush = time.monotonic()
            return

        flush_started = time.perf_counter()
        buffered = self._buffer
        flush_rows = len(buffered)
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

        try:
            # 跨子表分块写：305 子表不再串行落库，而是 ~3MB 分块的多表 INSERT。
            chunk_results = await self._writer.write_multi_table(rows)
        except Exception:
            # 整体异常（罕见：_execute 已自吞连接错返回 False）。如实计失败、
            # 整批留 buffer，并向上抛交 run_loop 退避处理。
            self._metrics.record_write_batch(
                (time.perf_counter() - flush_started) * 1000, flush_rows, False
            )
            raise

        # 按行级状态分流（§7.6 二分降级）：LANDED 与「成功进 DLQ 的坏行」才 ACK；
        # RETRY 与「DLQ 推送失败的坏行」留 buffer 重试，绝不丢数据（硬约束②）。
        to_ack: list[_BufferedRow] = []
        failed_rows: list[_BufferedRow] = []
        landed_rows_count = 0
        for indices, status, elapsed_ms in chunk_results:
            chunk_rows = [buffered[i] for i in indices]
            self._metrics.record_write_batch(elapsed_ms, len(chunk_rows), status == LANDED)
            if status == LANDED:
                to_ack.extend(chunk_rows)
                landed_rows_count += len(chunk_rows)
            elif status == BAD_DATA:
                for row in chunk_rows:
                    if self._push_row_to_dlq(row):
                        to_ack.append(row)
                    else:
                        failed_rows.append(row)
            else:  # RETRY：连接错/未知错/二分超限 —— 不 ACK、不进 DLQ
                logger.error(
                    "[consumer] multi-table chunk retryable (%d rows) — not acking, will retry",
                    len(chunk_rows),
                )
                failed_rows.extend(chunk_rows)

        if to_ack:
            self._ack_rows(to_ack)
        # 上报本 flush 的 LANDED rows 到 Redis metrics counter（供 backend rate_sampler 算入库速率）。
        # 失败必须吞掉：metrics 上报不得影响 XACK/TDengine 主流程。
        if landed_rows_count > 0:
            try:
                self._r.hincrby("rt:metrics:writer", "rows_written", landed_rows_count)
            except Exception:
                logger.warning("[consumer] metrics hincrby failed", exc_info=True)
        self._buffer = failed_rows
        self._metrics.set_buffer_rows(len(self._buffer))
        self._metrics.record_flush(
            (time.perf_counter() - flush_started) * 1000,
            flush_rows,
        )
        self._last_flush = time.monotonic()

    def _log_performance_if_due(self) -> None:
        now = time.monotonic()
        if now - self._last_performance_log < self._performance_log_interval_s:
            return
        logger.info(
            "[performance] %s",
            json.dumps(self._metrics.snapshot(), separators=(",", ":")),
        )
        self._last_performance_log = now

    def _push_row_to_dlq(self, row: _BufferedRow) -> bool:
        """Push a binary-search-isolated bad row to the DLQ. Returns True if stored.

        调用方只在返回 True 时 ACK；False 时该行留 buffer 重试（硬约束②：DLQ
        成功才 ACK，推送失败不丢数据）。syslog 与 exceeded-max-retry 路径同口径限流。
        """
        moved = push_dlq(
            self._r,
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
                    self._delete_ids(stream_key, chunk)
                except Exception as exc:
                    logger.warning(
                        "[consumer] xack retry still failing for %s (%d ids pending): %s",
                        stream_key, len(pending), exc,
                    )
                    break  # 本轮放弃后续 chunk，等下一轮再试
            if not pending:
                del self._ack_retry[stream_key]

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

    def _retry_pending_deletes(self) -> None:
        """重试已 ACK 条目的 XDEL；失败不会触发 TDengine 重写。"""
        if not self._delete_retry:
            return
        chunk_size = 500
        for stream_key in list(self._delete_retry.keys()):
            pending = self._delete_retry[stream_key]
            ids = list(pending)
            for i in range(0, len(ids), chunk_size):
                chunk = ids[i : i + chunk_size]
                try:
                    self._r.xdel(stream_key, *chunk)
                    pending.difference_update(chunk)
                except Exception as exc:
                    logger.warning(
                        "[consumer] xdel retry still failing for %s (%d ids pending): %s",
                        stream_key, len(pending), exc,
                    )
                    break
            if not pending:
                del self._delete_retry[stream_key]
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
