"""速率采样器（二期）：读端采样 Redis 累计计数器，算 per-min 速率，存内存滚动缓冲。

四路速率：
- collector 批次 = 各 rt:stream:* 的 XINFO STREAM `entries-added` 累计增量（批次/min）。
- collector 点数 = rt:metrics:collector `stream_points` 累计增量（点/min，collector 写 stream 时 HINCRBY）。
- writer 读取 = writer 消费组的 XINFO GROUPS `entries-read` 累计增量（条/min，从 Redis 读出的 entry 数）。
- writer 入库 = rt:metrics:writer `rows_written` 累计增量（rows/min，TDengine LANDED 行数，writer flush 时 HINCRBY）。

注意：entries-read 是「从 Redis 读出」的条数，不等于 TDengine 落库行数；落库行数看 rows_written。

按 stream key 维护前值（只累加同 key 的正向增量）：新增 stream 首次只建基线、不出尖刺；
某 key 本轮读取失败则本轮不计入、不更新其基线，避免污染速率。metrics counter 为单值累计，
重启归零时该轮不计增量（下轮重建基线）。趋势点存内存滚动缓冲（重启从空开始，~10min 窗口）。
速率卡当前值 = 趋势最新点（同源）。
"""

from __future__ import annotations

import asyncio
from collections import deque
import logging
import time
from typing import Any

from app.core.time_utils import now_local
from app.db.redis import get_redis_client

logger = logging.getLogger("backend.rate_sampler")

SAMPLE_INTERVAL_SECONDS = 10
WINDOW_SECONDS = 600  # 10 分钟
_MAX_POINTS = WINDOW_SECONDS // SAMPLE_INTERVAL_SECONDS  # 60 点

# 只统计 writer 自己的消费组，避免未来出现多消费组时把 rows/min 重复计算。
# 与 writer/config/settings.toml 的 group_name 对应。
WRITER_GROUP_NAME = "writer_cg"

COLLECTOR_UNIT = "批次/min"
WRITER_UNIT = "条/min"  # entries-read：writer 从 Redis 读出的 entry 条数（非落库行数）
COLLECTOR_POINT_UNIT = "点/min"  # stream_points：进入 Redis Stream 的有效点数
WRITER_LANDED_UNIT = "rows/min"  # rows_written：TDengine LANDED 行数

COLLECTOR_METRICS_KEY = "rt:metrics:collector"
WRITER_METRICS_KEY = "rt:metrics:writer"
COLLECTOR_POINT_FIELD = "stream_points"
WRITER_LANDED_FIELD = "rows_written"


class RateSampler:
    def __init__(self) -> None:
        self._collector_points: deque[tuple[str, int]] = deque(maxlen=_MAX_POINTS)
        self._writer_points: deque[tuple[str, int]] = deque(maxlen=_MAX_POINTS)
        self._collector_point_points: deque[tuple[str, int]] = deque(maxlen=_MAX_POINTS)
        self._writer_landed_points: deque[tuple[str, int]] = deque(maxlen=_MAX_POINTS)
        # collectorPoints 60s 窗口的累计值缓冲（collector 60s 上报，10s 采样算 60s 增量 = 点/min）
        self._collector_point_totals: deque[int] = deque(maxlen=_MAX_POINTS)
        # 按 stream key 的上次累计值
        self._prev_added: dict[str, int] = {}
        self._prev_read: dict[str, int] = {}
        # metrics counter 单值累计
        self._prev_writer_landed_total: int | None = None
        self._prev_monotonic: float | None = None

    async def sample(self) -> None:
        """采一次：读各 key 累计值与 metrics counter，与上次比算正向增量。首次只建基线不出点。"""
        added, read = await self._read_cumulative()
        collector_point_total, writer_landed_total = await self._read_metrics_counters()
        now_mono = time.monotonic()

        if self._prev_monotonic is not None:
            dt = now_mono - self._prev_monotonic
            if dt > 0:
                label = now_local().strftime("%H:%M:%S")
                self._collector_points.append((label, self._per_min(self._delta_sum(self._prev_added, added), dt)))
                self._writer_points.append((label, self._per_min(self._delta_sum(self._prev_read, read), dt)))
                # collectorPoints 60s 窗口：collector 60s 上报累计值，10s 采样算 60s 增量 = 点/min。
                # 不用相邻 10s 增量（会高估 6 倍），用当前 - 6 个采样前（60s）= 真实 1 分钟增量。
                if collector_point_total is not None:
                    self._collector_point_totals.append(collector_point_total)
                    if len(self._collector_point_totals) >= 7:
                        window_delta = collector_point_total - self._collector_point_totals[-7]
                        self._collector_point_points.append((label, max(0, window_delta)))
                # writerLanded 10s 相邻增量（writer flush 低频 ~2/秒，10s 采样准）
                if writer_landed_total is not None and self._prev_writer_landed_total is not None:
                    self._writer_landed_points.append((label, self._per_min(self._delta_scalar(self._prev_writer_landed_total, writer_landed_total), dt)))

        self._prev_added = added
        self._prev_read = read
        # 仅读成功时更新基线；失败轮保留旧基线，下轮恢复后算的是真实增量而非历史累计
        if writer_landed_total is not None:
            self._prev_writer_landed_total = writer_landed_total
        self._prev_monotonic = now_mono

    def snapshot(self) -> dict[str, Any]:
        return {
            "collector": self._series_payload(self._collector_points, COLLECTOR_UNIT),
            "writer": self._series_payload(self._writer_points, WRITER_UNIT),
            "collectorPoints": self._series_payload(self._collector_point_points, COLLECTOR_POINT_UNIT),
            "writerLanded": self._series_payload(self._writer_landed_points, WRITER_LANDED_UNIT),
        }

    @staticmethod
    def _delta_sum(prev: dict[str, int], cur: dict[str, int]) -> int:
        """按 key 累加正向增量；新增 key（不在 prev）只建基线不计；累计回退的 key 跳过。"""
        total = 0
        for key, cur_val in cur.items():
            prev_val = prev.get(key)
            if prev_val is not None and cur_val >= prev_val:
                total += cur_val - prev_val
        return total

    @staticmethod
    def _delta_scalar(prev: int | None, cur: int) -> int:
        """单值累计的正面增量；首次（prev=None）或回退（重启归零）时计 0，下轮重建基线。"""
        if prev is not None and cur >= prev:
            return cur - prev
        return 0

    @staticmethod
    def _per_min(delta: int, dt_seconds: float) -> int:
        return round(max(0, delta) / dt_seconds * 60)

    @staticmethod
    def _series_payload(points: deque[tuple[str, int]], unit: str) -> dict[str, Any]:
        series = [{"t": t, "v": v} for t, v in points]
        return {
            "unit": unit,
            "current": series[-1]["v"] if series else None,
            "series": series,
        }

    async def _read_cumulative(self) -> tuple[dict[str, int], dict[str, int]]:
        """读各 rt:stream:* 的 entries-added 与 writer 组 entries-read。

        每个 key 的两项各自独立提交：某项读取失败则该 key 本轮不进对应 map
        （下一轮会被当作新 key 重建基线，不会产生补偿尖刺）。
        """
        r = await get_redis_client()
        added: dict[str, int] = {}
        read: dict[str, int] = {}
        async for key in r.scan_iter(match="rt:stream:*", count=100):
            try:
                info = await r.xinfo_stream(key)
                added[key] = int(info.get("entries-added") or 0)
            except Exception:
                pass
            try:
                groups = await r.xinfo_groups(key)
                total = 0
                for group in groups:
                    if group.get("name") != WRITER_GROUP_NAME:
                        continue
                    entries_read = group.get("entries-read")
                    total += int(entries_read) if entries_read is not None else 0
                read[key] = total
            except Exception:
                pass
        return added, read

    async def _read_metrics_counters(self) -> tuple[int | None, int | None]:
        """读 collector 点数 / writer 入库 累计计数器（单值 HGET）。失败返回 None，
        sample() 据此跳过本轮增量且不更新基线，避免恢复后把历史累计量一次性算成速率尖刺。"""
        r = await get_redis_client()
        collector_points = await self._hget_int(r, COLLECTOR_METRICS_KEY, COLLECTOR_POINT_FIELD)
        writer_landed = await self._hget_int(r, WRITER_METRICS_KEY, WRITER_LANDED_FIELD)
        return collector_points, writer_landed

    @staticmethod
    async def _hget_int(r: Any, key: str, field: str) -> int | None:
        try:
            v = await r.hget(key, field)
            return int(v) if v is not None else None
        except Exception:
            return None


_sampler: RateSampler | None = None
_task: asyncio.Task[None] | None = None


def get_rate_sampler() -> RateSampler:
    global _sampler
    if _sampler is None:
        _sampler = RateSampler()
    return _sampler


async def _run_loop() -> None:
    sampler = get_rate_sampler()
    while True:
        try:
            await sampler.sample()
        except asyncio.CancelledError:
            raise
        except Exception as exc:  # 单次失败不终止循环
            logger.warning("[rate_sampler] sample error: %s", exc)
        await asyncio.sleep(SAMPLE_INTERVAL_SECONDS)


async def start_rate_sampler() -> None:
    global _task
    if _task is not None and not _task.done():
        return
    _task = asyncio.create_task(_run_loop())


async def stop_rate_sampler() -> None:
    global _task
    if _task is None:
        return
    _task.cancel()
    try:
        await _task
    except asyncio.CancelledError:
        pass
    _task = None
