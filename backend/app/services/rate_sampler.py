"""速率采样器（二期）：读端采样 Redis Stream 累计计数器，算 per-min 速率，存内存滚动缓冲。

- collector 速率 = 各 rt:stream:* 的 XINFO STREAM `entries-added` 累计增量
  （= 写入批次/分钟；每个 entry 是一批点位，无顶层 count 字段，故按批次计）。
- writer 速率 = writer 消费组的 XINFO GROUPS `entries-read` 累计增量
  （= 入库行/分钟；writer 是 1 entry → 1 行 TDengine，故 entries-read 即 rows）。

按 stream key 维护前值（只累加同 key 的正向增量）：新增 stream 首次只建基线、不出尖刺；
某 key 本轮读取失败则本轮不计入、不更新其基线，避免污染速率。纯只读，零改 collector/writer。
趋势点存内存滚动缓冲（重启从空开始，~10min 窗口）。速率卡当前值 = 趋势最新点（同源）。
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
WRITER_UNIT = "rows/min"


class RateSampler:
    def __init__(self) -> None:
        self._collector_points: deque[tuple[str, int]] = deque(maxlen=_MAX_POINTS)
        self._writer_points: deque[tuple[str, int]] = deque(maxlen=_MAX_POINTS)
        # 按 stream key 的上次累计值
        self._prev_added: dict[str, int] = {}
        self._prev_read: dict[str, int] = {}
        self._prev_monotonic: float | None = None

    async def sample(self) -> None:
        """采一次：读各 key 累计值，与上次按 key 比算正向增量之和。首次只建基线不出点。"""
        added, read = await self._read_cumulative()
        now_mono = time.monotonic()

        if self._prev_monotonic is not None:
            dt = now_mono - self._prev_monotonic
            if dt > 0:
                label = now_local().strftime("%H:%M:%S")
                self._collector_points.append((label, self._per_min(self._delta_sum(self._prev_added, added), dt)))
                self._writer_points.append((label, self._per_min(self._delta_sum(self._prev_read, read), dt)))

        self._prev_added = added
        self._prev_read = read
        self._prev_monotonic = now_mono

    def snapshot(self) -> dict[str, Any]:
        return {
            "collector": self._series_payload(self._collector_points, COLLECTOR_UNIT),
            "writer": self._series_payload(self._writer_points, WRITER_UNIT),
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
