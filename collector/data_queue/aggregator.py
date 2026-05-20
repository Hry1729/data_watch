"""数据聚合器"""

import asyncio
import inspect
import time
from datetime import datetime
from typing import Any, Callable, Dict, List, Optional
import threading

from core.config import CollectorConfig
from core.logger import get_logger
from data_queue.buffer import DataBuffer, DataPoint

logger = get_logger("aggregator")


class DataAggregator:
    """
    数据聚合器：核心组件
    功能：
    1. 值变化判断（去重）：值不变的不存储
    2. 批量缓存：攒够batch_size或flush_interval触发写入
    3. 时间窗口控制：超过max_window_seconds强制写入
    4. 写入失败恢复：失败时回滚状态，避免数据丢失
    5. 缓存上限保护：超限时丢弃最旧数据并记录
    6. 连续失败退避：连续写入失败后暂停缓存
    """

    # 缓存上限（为 batch_size 的倍数）
    MAX_CACHE_MULTIPLIER = 10
    # 连续失败阈值，超过后暂停缓存
    MAX_CONSECUTIVE_FAILURES = 5

    def __init__(self, config: CollectorConfig, buffer: DataBuffer):
        self.config = config
        self.buffer = buffer
        self._batch_size = config.batch_size
        self._flush_interval = config.flush_interval
        self._max_window = config.max_window_seconds
        self._max_cache_size = config.batch_size * self.MAX_CACHE_MULTIPLIER

        # 已确认的上次存储值/质量和时间（写入成功后更新）
        self._confirmed_values: Dict[str, Any] = {}
        self._confirmed_time: Dict[str, float] = {}
        # 待确认的上次存储值/质量和时间（入缓存时记录，用于回滚）
        self._pending_values: Dict[str, Any] = {}
        self._pending_time: Dict[str, float] = {}
        self._values_lock = threading.Lock()

        # 批量缓存
        self._cache: List[DataPoint] = []

        # 输出回调 - 用于将flush的数据传递给写入器
        self._output_callback: Optional[Callable[[List[DataPoint]], Any]] = None

        # 统计
        self._total_deduped = 0  # 去重跳过次数
        self._total_flushed = 0  # 刷新次数
        self._total_written = 0  # 总写入条数
        self._total_failed = 0   # 写入失败次数
        self._total_cache_dropped = 0  # 缓存超限丢弃次数

        # 连续失败计数
        self._consecutive_failures = 0
        self._cache_paused = False  # 暂停缓存标记

        # 运行控制
        self._running = False

    def set_output_callback(self, callback: Callable[[List[DataPoint]], Any]) -> None:
        """设置输出回调，用于将flush的数据传递给写入器"""
        self._output_callback = callback

    def should_store(
        self, variable_id: str, value: Any, quality: int = 0
    ) -> bool:
        """
        判断是否需要存储
        值和质量均未变化且未超时 -> 跳过

        优先基于待写入状态判断；没有待写入值时再看已确认状态。
        这样同一批次内相同值不会重复入缓存，写入失败后仍会恢复 pending 状态等待重试。
        """
        now = time.time()
        last_time = 0
        last_val = None
        current_val = (value, quality)

        # 线程安全地获取上次待确认/已确认的值。
        with self._values_lock:
            if variable_id in self._pending_values:
                last_time = self._pending_time.get(variable_id, 0)
                last_val = self._pending_values.get(variable_id)
            else:
                last_time = self._confirmed_time.get(variable_id, 0)
                last_val = self._confirmed_values.get(variable_id)

        # 超过最大窗口，强制写入
        if now - last_time >= self._max_window:
            return True

        # 值或质量发生变化才存储
        if current_val != last_val:
            return True

        # 值和质量相同且未超时，跳过
        self._total_deduped += 1
        return False

    def add(self, point: DataPoint) -> None:
        """
        添加数据点到聚合器
        经过值变化判断后加入缓存，记录待确认状态
        """
        point_state = (point.value, point.quality)
        if not self.should_store(point.variable_id, point.value, point.quality):
            return

        # 线程安全地更新缓存和待确认状态
        with self._values_lock:
            # 检查缓存是否暂停（连续失败过多）
            if self._cache_paused:
                self._total_cache_dropped += 1
                return

            # 检查缓存上限，超限时丢弃最旧的点
            if len(self._cache) >= self._max_cache_size:
                dropped = self._cache.pop(0)
                self._total_cache_dropped += 1
                logger.warning(
                    "Aggregator cache full (%d/%d), dropping oldest point: %s/%s",
                    len(self._cache),
                    self._max_cache_size,
                    dropped.device_id,
                    dropped.variable_id,
                )

            self._cache.append(point)
            # 记录待确认状态（写入成功后会确认）
            self._pending_values[point.variable_id] = point_state
            self._pending_time[point.variable_id] = time.time()

    def add_batch(self, points: List[DataPoint]) -> None:
        """批量添加"""
        for point in points:
            self.add(point)

    def is_ready(self) -> bool:
        """是否达到写入条件"""
        return len(self._cache) >= self._batch_size

    def flush(self) -> List[DataPoint]:
        """
        刷新缓存，返回待写入的数据
        """
        if not self._cache:
            return []

        batch = self._cache[:]
        self._cache.clear()
        self._total_flushed += 1
        return batch

    def _confirm_pending_state(self) -> None:
        """确认待写入数据的状态（写入成功后调用）"""
        with self._values_lock:
            for var_id, value in self._pending_values.items():
                self._confirmed_values[var_id] = value
            for var_id, ts in self._pending_time.items():
                self._confirmed_time[var_id] = ts
            self._pending_values.clear()
            self._pending_time.clear()

    def _rollback_pending_state(self) -> None:
        """回滚待写入数据的状态（写入失败后调用）"""
        with self._values_lock:
            self._pending_values.clear()
            self._pending_time.clear()

    def _restore_batch_to_cache(self, batch: List[DataPoint]) -> None:
        """将失败的 batch 放回缓存头部（写入失败后调用）"""
        if not batch:
            return
        with self._values_lock:
            # 将 batch 放回缓存头部
            self._cache = batch + self._cache
            # 恢复 pending 状态
            for point in batch:
                self._pending_values[point.variable_id] = (point.value, point.quality)
                if isinstance(point.timestamp, datetime):
                    pending_time = point.timestamp.timestamp()
                elif isinstance(point.timestamp, (int, float)):
                    ts = float(point.timestamp)
                    pending_time = ts / 1000.0 if ts > 1_000_000_000_000 else ts
                else:
                    pending_time = time.time()
                self._pending_time[point.variable_id] = pending_time
            logger.warning("写入失败的 batch 已放回缓存，等待重试 batch_size=%d", len(batch))

    async def flush_to_output(self) -> int:
        """
        刷新缓存并同步提交到输出回调

        返回：成功写入的条数
        """
        batch = self.flush()
        if not batch:
            return 0

        if self._output_callback:
            try:
                # 同步等待写入完成
                if inspect.iscoroutinefunction(self._output_callback):
                    await self._output_callback(batch)
                else:
                    # 回调返回 Task 时，等待完成
                    result = self._output_callback(batch)
                    if asyncio.isfuture(result) or isinstance(result, asyncio.Task):
                        await result

                # 写入成功，确认状态并重置失败计数
                self._confirm_pending_state()
                self._total_written += len(batch)
                self._consecutive_failures = 0
                self._cache_paused = False
                return len(batch)
            except Exception as e:
                # 写入失败，将 batch 放回缓存并恢复 pending 状态
                self._total_failed += 1
                self._consecutive_failures += 1

                # 检查是否需要暂停缓存
                if self._consecutive_failures >= self.MAX_CONSECUTIVE_FAILURES:
                    self._cache_paused = True
                    logger.error(
                        "聚合器连续写入失败 %d 次，暂停缓存接收新数据",
                        self._consecutive_failures,
                    )

                # 检查缓存是否超限，超限则丢弃最旧的点
                if len(batch) + len(self._cache) > self._max_cache_size:
                    overflow = len(batch) + len(self._cache) - self._max_cache_size
                    self._cache = self._cache[overflow:]
                    self._total_cache_dropped += overflow
                    logger.warning(
                        "缓存超限，丢弃 %d 个最旧点以容纳失败 batch",
                        overflow,
                    )

                self._restore_batch_to_cache(batch)
                logger.error(
                    "聚合器写入失败，batch 已放回缓存 batch_size=%s error=%s",
                    len(batch),
                    e,
                )
                raise
        return len(batch)

    async def run(self) -> None:
        """
        聚合器主循环
        1. 从buffer取数据
        2. 聚合去重
        3. 达到条件就flush到输出回调（同步等待）
        """
        self._running = True
        logger.info("DataAggregator started")

        last_flush = time.time()

        while self._running:
            try:
                # 非阻塞检查队列
                if not self.buffer.empty():
                    points = await self.buffer.get_batch(self._batch_size)
                    self.add_batch(points)

                # 检查是否需要flush
                now = time.time()
                if self.is_ready() or (now - last_flush >= self._flush_interval and self._cache):
                    try:
                        await self.flush_to_output()
                        last_flush = now
                    except Exception:
                        # 写入失败已记录，继续运行
                        pass

                # 短暂休眠避免空转
                await asyncio.sleep(0.05)

            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error("DataAggregator error: %s", e)
                await asyncio.sleep(1)

        self._running = False
        logger.info("DataAggregator stopped")

    def stop(self) -> None:
        """停止聚合器"""
        self._running = False

    @property
    def cache_size(self) -> int:
        return len(self._cache)

    @property
    def stats(self) -> Dict[str, int]:
        return {
            "cache_size": self.cache_size,
            "max_cache_size": self._max_cache_size,
            "total_deduped": self._total_deduped,
            "total_flushed": self._total_flushed,
            "total_written": self._total_written,
            "total_failed": self._total_failed,
            "total_cache_dropped": self._total_cache_dropped,
            "consecutive_failures": self._consecutive_failures,
            "cache_paused": self._cache_paused,
        }
