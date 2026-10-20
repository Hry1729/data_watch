"""GE SRTP协议驱动实现"""

import asyncio
import os
import re
import sys
import time
from typing import Any, Dict, List

from .transport.driver import GE_SRTP_Driver
from .transport import exceptions as gesrtp_exceptions
from collector.bounded_executor import ExecutorSaturatedError

from core.exceptions import DeviceConnectionError, ReadError
from core.logger import get_logger
from drivers.base import BaseDriver, ReadBlock

logger = get_logger("ge_srtp_driver")


class GESRTPDriver(BaseDriver):
    """GE PLC驱动，基于gesrtp-py"""

    # GE 地址正则：匹配支持的字区和离散区地址。
    _GE_ADDRESS_RE = re.compile(
        r"^%(R|AI|AQ|I|Q|M|T|SA|SB|SC|S|G)(\d+)$", re.IGNORECASE
    )

    def __init__(self, read_executor=None, metrics=None):
        super().__init__()
        # 专用 PLC 读线程池（§7.3）；None 时回退默认 executor，保持原行为与测试兼容。
        self._read_executor = read_executor
        self._driver = None
        self._drivers = []
        self._parallel_channels = 1
        self._batch_deadline_ms = None

        self._metrics = metrics

    def _record_timing(self, name: str, value_ms: float) -> None:
        if self._metrics is not None:
            self._metrics.record_timing(name, value_ms)

    def _increment(self, name: str, amount: int = 1) -> None:
        if self._metrics is not None:
            self._metrics.increment(name, amount)

    def _set_gauge(self, name: str, value: int | float) -> None:
        if self._metrics is not None:
            self._metrics.set_gauge(name, value)

    async def connect(self, config: Dict[str, Any]) -> None:
        """
        连接到GE PLC
        config: {host, port, slot}
        """
        host = config.get("ip", "127.0.0.1")
        port = config.get("port", 18245)
        slot = config.get("slot", 1)
        requested_channels = int(config.get("ge_parallel_channels", 1))
        if requested_channels not in (1, 2):
            raise DeviceConnectionError(
                f"GE parallel channels must be 1 or 2, got {requested_channels}"
            )

        batch_deadline_ms = config.get("ge_batch_deadline_ms")
        if batch_deadline_ms is not None:
            batch_deadline_ms = int(batch_deadline_ms)
            if batch_deadline_ms <= 0:
                raise DeviceConnectionError(
                    f"GE batch deadline must be > 0, got {batch_deadline_ms}"
                )
        self._batch_deadline_ms = batch_deadline_ms
        read_timeout_ms = config.get("read_timeout_ms")
        kwargs = {}
        if read_timeout_ms is not None:
            kwargs["timeout"] = int(read_timeout_ms) / 1000.0
        kwargs["max_payload_bytes"] = config.get("ge_max_payload_bytes", 250)

        self._parallel_channels = requested_channels
        self._drivers = []
        self._driver = None
        loop = asyncio.get_running_loop()
        try:
            primary = GE_SRTP_Driver(host=host, port=port, slot=slot, **kwargs)
            await loop.run_in_executor(self._read_executor, primary.connect)
            self._drivers.append(primary)

            if requested_channels == 2:
                secondary = GE_SRTP_Driver(host=host, port=port, slot=slot, **kwargs)
                try:
                    await loop.run_in_executor(self._read_executor, secondary.connect)
                    self._drivers.append(secondary)
                except Exception as exc:
                    try:
                        await loop.run_in_executor(
                            self._read_executor, secondary.disconnect
                        )
                    except Exception:
                        pass
                    self._increment("ge_srtp_channel_degraded_count")
                    logger.warning(
                        "GE SRTP secondary channel unavailable for %s:%s; "
                        "continuing with one channel: %s",
                        host,
                        port,
                        exc,
                    )

            self._driver = self._drivers[0]
            self._connected = True
            self._set_gauge("ge_srtp_active_channel_count", len(self._drivers))
            logger.info(
                "GE SRTP connected to %s:%s slot=%s channels=%s",
                host,
                port,
                slot,
                len(self._drivers),
            )
        except Exception as exc:
            for driver in self._drivers:
                try:
                    await loop.run_in_executor(self._read_executor, driver.disconnect)
                except Exception:
                    pass
            self._drivers = []
            self._driver = None
            self._connected = False
            self._set_gauge("ge_srtp_active_channel_count", 0)
            raise DeviceConnectionError(f"GE SRTP connect failed: {exc}") from exc

    async def disconnect(self) -> None:
        """断开GE PLC的全部独立连接。"""
        drivers = list(self._drivers or ([self._driver] if self._driver else []))
        loop = asyncio.get_running_loop()
        # 读池饱和时提交被拒绝 ⇒ 底层 disconnect 根本未执行：保留这些通道句柄并透传拥塞，
        # 让 manager 的拥塞重试再次清理（B040）；只移除真正完成清理（或非拥塞失败）的通道。
        pending = []
        saturated = None
        for driver in drivers:
            try:
                await loop.run_in_executor(self._read_executor, driver.disconnect)
            except ExecutorSaturatedError as exc:
                pending.append(driver)
                saturated = exc
            except Exception as exc:
                logger.warning("GE SRTP disconnect error: %s", exc)
        self._connected = False
        self._drivers = pending
        self._driver = pending[0] if pending else None
        self._set_gauge("ge_srtp_active_channel_count", len(pending))
        if saturated is not None:
            logger.warning(
                "GE SRTP disconnect deferred: %d channel(s) rejected by saturated executor",
                len(pending),
            )
            raise saturated
        logger.info("GE SRTP disconnected")

    async def read(self, blocks: List[ReadBlock]) -> Dict[str, Any]:
        """
        批量读取GE PLC数据
        block.address 格式: "%R<num>", "%AI<num>", "%Q<num>" 等
        """
        if not self._connected or not self._driver:
            raise ReadError("GE SRTP driver not connected")

        result = {}
        loop = asyncio.get_event_loop()

        async with self._read_lock:
            batch_started = time.perf_counter()
            deadline_at = (
                batch_started + self._batch_deadline_ms / 1000.0
                if self._batch_deadline_ms is not None
                else None
            )
            failed_blocks = 0
            batch_outcome = "error"
            area_counts = {
                area: 0
                for area in ("R", "AI", "AQ", "I", "Q", "M", "T", "S", "SA", "SB", "SC", "G")
            }
            payload_bytes = 0
            for block in blocks:
                match = self._GE_ADDRESS_RE.match(block.address.upper())
                if match:
                    area_counts[match.group(1).upper()] += 1
                payload_bytes += self._block_payload_bytes(block)

            self._set_gauge("ge_srtp_last_batch_block_count", len(blocks))
            self._set_gauge("ge_srtp_last_batch_payload_bytes", payload_bytes)
            for area, count in area_counts.items():
                self._set_gauge(f"ge_srtp_last_batch_blocks_{area.lower()}", count)

            channels = list(self._drivers or [self._driver])
            try:
                if len(channels) > 1:
                    failed_blocks = await self._read_parallel_blocks(
                        blocks, channels, loop, result, deadline_at
                    )
                else:
                    channel = channels[0]
                    for index, block in enumerate(blocks):
                        if deadline_at is not None and time.perf_counter() >= deadline_at:
                            pending = blocks[index:]
                            for pending_block in pending:
                                result[pending_block.address] = None
                            failed_blocks += len(pending)
                            self._increment("ge_srtp_batch_deadline_count")
                            self._increment(
                                "ge_srtp_batch_deadline_block_count", len(pending)
                            )
                            break
                        block_started = time.perf_counter()
                        try:
                            values = await loop.run_in_executor(
                                self._read_executor,
                                self._read_block,
                                block,
                                channel,
                            )
                            result[block.address] = values
                            self._record_timing(
                                "ge_srtp_block_rtt_ms",
                                (time.perf_counter() - block_started) * 1000,
                            )
                            self._increment("ge_srtp_block_success_count")
                        except ExecutorSaturatedError:
                            self._increment("ge_srtp_block_error_saturated_count")
                            raise
                        except Exception as exc:
                            self._record_timing(
                                "ge_srtp_block_rtt_ms",
                                (time.perf_counter() - block_started) * 1000,
                            )
                            failed_blocks += 1
                            category = self._error_category(exc)
                            self._increment(f"ge_srtp_block_error_{category}_count")
                            if not self._channel_is_connected(channel):
                                self._connected = False
                                self._drivers = []
                                logger.error(
                                    "GE SRTP read block %s failed; connection invalid, "
                                    "aborting batch: %s",
                                    block.address,
                                    exc,
                                )
                                raise
                            logger.error(
                                "GE SRTP read block %s failed: %s", block.address, exc
                            )
                            result[block.address] = None
                batch_outcome = "success" if failed_blocks == 0 else "partial"
            finally:
                self._record_timing(
                    "ge_srtp_batch_ms",
                    (time.perf_counter() - batch_started) * 1000,
                )
                self._set_gauge("ge_srtp_last_batch_failed_block_count", failed_blocks)
                self._increment("ge_srtp_batch_count")
                self._increment(f"ge_srtp_batch_{batch_outcome}_count")

        return result

    @staticmethod
    def _channel_is_connected(driver) -> bool:
        try:
            return bool(driver and driver.is_connected())
        except Exception:
            return False

    async def _read_parallel_blocks(
        self,
        blocks: List[ReadBlock],
        channels: List[Any],
        loop,
        result: Dict[str, Any],
        deadline_at,
    ) -> int:
        """Split one batch across isolated channels and downgrade on fatal failure."""
        assignments = [
            [block for index, block in enumerate(blocks) if index % len(channels) == slot]
            for slot in range(len(channels))
        ]

        async def read_channel(driver, assigned):
            outcome = {
                "driver": driver,
                "fatal": None,
                "retry": [],
                "saturated": None,
                "deadline": [],
            }
            for index, block in enumerate(assigned):
                if deadline_at is not None and time.perf_counter() >= deadline_at:
                    outcome["deadline"] = assigned[index:]
                    for pending_block in outcome["deadline"]:
                        result[pending_block.address] = None
                    break
                block_started = time.perf_counter()
                try:
                    values = await loop.run_in_executor(
                        self._read_executor,
                        self._read_block,
                        block,
                        driver,
                    )
                    result[block.address] = values
                    self._record_timing(
                        "ge_srtp_block_rtt_ms",
                        (time.perf_counter() - block_started) * 1000,
                    )
                    self._increment("ge_srtp_block_success_count")
                except ExecutorSaturatedError as exc:
                    self._increment("ge_srtp_block_error_saturated_count")
                    outcome["saturated"] = exc
                    break
                except Exception as exc:
                    self._record_timing(
                        "ge_srtp_block_rtt_ms",
                        (time.perf_counter() - block_started) * 1000,
                    )
                    category = self._error_category(exc)
                    self._increment(f"ge_srtp_block_error_{category}_count")
                    if not self._channel_is_connected(driver):
                        outcome["fatal"] = exc
                        outcome["retry"] = assigned[index:]
                        break
                    logger.error(
                        "GE SRTP read block %s failed on one channel: %s",
                        block.address,
                        exc,
                    )
                    result[block.address] = None
            return outcome

        outcomes = await asyncio.gather(
            *(
                read_channel(driver, assigned)
                for driver, assigned in zip(channels, assignments)
            )
        )
        fatal_outcomes = [outcome for outcome in outcomes if outcome["fatal"] is not None]
        deadline_blocks = [
            block
            for outcome in outcomes
            for block in outcome["deadline"]
        ]
        if fatal_outcomes:
            survivors = [
                driver for driver in channels if self._channel_is_connected(driver)
            ]
            self._drivers = survivors
            self._driver = survivors[0] if survivors else None
            self._connected = bool(survivors)
            self._set_gauge("ge_srtp_active_channel_count", len(survivors))
            if not survivors:
                raise fatal_outcomes[0]["fatal"]

            retry_blocks = [
                block
                for outcome in fatal_outcomes
                for block in outcome["retry"]
            ]
            self._increment("ge_srtp_channel_degraded_count", len(fatal_outcomes))
            self._increment("ge_srtp_failover_retry_block_count", len(retry_blocks))
            logger.warning(
                "GE SRTP channel failed; downgraded to %s channel(s), retrying %s block(s)",
                len(survivors),
                len(retry_blocks),
            )
            survivor = survivors[0]
            for index, block in enumerate(retry_blocks):
                if deadline_at is not None and time.perf_counter() >= deadline_at:
                    pending = retry_blocks[index:]
                    deadline_blocks.extend(pending)
                    for pending_block in pending:
                        result[pending_block.address] = None
                    break
                block_started = time.perf_counter()
                try:
                    values = await loop.run_in_executor(
                        self._read_executor,
                        self._read_block,
                        block,
                        survivor,
                    )
                    result[block.address] = values
                    self._record_timing(
                        "ge_srtp_block_rtt_ms",
                        (time.perf_counter() - block_started) * 1000,
                    )
                    self._increment("ge_srtp_block_success_count")
                except ExecutorSaturatedError:
                    self._increment("ge_srtp_block_error_saturated_count")
                    raise
                except Exception as exc:
                    self._record_timing(
                        "ge_srtp_block_rtt_ms",
                        (time.perf_counter() - block_started) * 1000,
                    )
                    category = self._error_category(exc)
                    self._increment(f"ge_srtp_block_error_{category}_count")
                    if not self._channel_is_connected(survivor):
                        self._connected = False
                        self._drivers = []
                        self._driver = None
                        self._set_gauge("ge_srtp_active_channel_count", 0)
                        raise
                    logger.error(
                        "GE SRTP failover retry for block %s failed: %s",
                        block.address,
                        exc,
                    )
                    result[block.address] = None

        if deadline_blocks:
            self._increment("ge_srtp_batch_deadline_count")
            self._increment(
                "ge_srtp_batch_deadline_block_count", len(deadline_blocks)
            )

        saturated = next(
            (outcome["saturated"] for outcome in outcomes if outcome["saturated"]),
            None,
        )
        if saturated is not None:
            raise saturated

        return sum(result.get(block.address) is None for block in blocks)


    @classmethod
    def _block_payload_bytes(cls, block: ReadBlock) -> int:
        match = cls._GE_ADDRESS_RE.match(block.address.upper())
        if not match:
            return 0
        area = match.group(1).upper()
        count = max(0, int(block.count))
        if area in ("I", "Q", "M", "T", "S", "SA", "SB", "SC", "G"):
            data_type = str(block.data_type or "").upper()
            return count if data_type == "BYTE" else (count + 7) // 8
        return count * 2

    @staticmethod
    def _error_category(error: Exception) -> str:
        if "PLC returned error (NACK)" in str(error):
            return "plc_nack"
        if isinstance(error, gesrtp_exceptions.TimeoutError):
            return "timeout"
        if isinstance(error, gesrtp_exceptions.ConnectionError):
            return "connection"
        if isinstance(error, gesrtp_exceptions.ProtocolError):
            return "protocol"
        if isinstance(error, (gesrtp_exceptions.PLCError, gesrtp_exceptions.MemoryError)):
            return "plc"
        return "other"

    def _read_block(self, block: ReadBlock, driver=None) -> List[Any]:
        """
        在线程中执行单个 block 读取。

        ReadBlock.address 视作 PLC 逻辑地址字符串，如 "%R00001"、"%AI0001"。
        driver 内部根据地址格式解析区域类型和逻辑编号，转换为 GE 库需要的 protocol offset。

        地址基准（已固化）：全区域 protocol_offset = logical_number - 1
        （对齐前辈 tmeic 项目实盘约定；离散区旧"暂不减 1"会错读相邻点）。
        """
        addr = block.address.upper()
        channel = driver or self._driver
        if channel is None:
            raise ReadError("GE SRTP driver not connected")
        count = block.count

        # 匹配 GE 地址格式：支持寄存器区及全部已接入的离散区。
        match = self._GE_ADDRESS_RE.match(addr)
        if not match:
            raise ReadError(f"Unsupported GE address format: {addr}")

        area = match.group(1).upper()
        logical_number = int(match.group(2))

        # 地址基准转换：全区域一律 -1
        protocol_offset = logical_number - 1

        # 离散区 byte 模式：块地址为 8 对齐的逻辑编号（-1 后即 0 基 bit 索引），count 为字节数。
        # device 合并层当前只生成 BYTE 离散块；下方 bit 路径仅为读取模式兼容
        # （外部构造的 ReadBlock），地址基准与 BYTE/寄存器一致：逻辑编号一律 -1。
        data_type = str(getattr(block, "data_type", "") or "").upper()
        discrete_byte_mode = area in (
            "I", "Q", "M", "T", "S", "SA", "SB", "SC", "G"
        ) and data_type == "BYTE"
        if discrete_byte_mode:
            if protocol_offset % 8 != 0:
                raise ReadError(
                    f"GE discrete byte-mode block {addr} not byte-aligned (bit offset {protocol_offset})"
                )
            protocol_offset //= 8

        # 根据区域类型调用对应的读取方法
        if area == "R":
            values = channel.read_register(protocol_offset, count)
        elif area == "AI":
            values = channel.read_analog_input(protocol_offset, count)
        elif area == "AQ":
            values = channel.read_analog_output(protocol_offset, count)
        elif area == "I":
            mode = 'byte' if discrete_byte_mode else 'bit'
            values = channel.read_discrete_input(protocol_offset, count, mode=mode)
        elif area == "Q":
            mode = 'byte' if discrete_byte_mode else 'bit'
            values = channel.read_discrete_output(protocol_offset, count, mode=mode)
        elif area == "M":
            mode = 'byte' if discrete_byte_mode else 'bit'
            values = channel.read_discrete_internal(protocol_offset, count, mode=mode)
        elif area == "T":
            mode = 'byte' if discrete_byte_mode else 'bit'
            values = channel.read_discrete_temp(protocol_offset, count, mode=mode)
        elif area in ("S", "SA", "SB", "SC"):
            mode = 'byte' if discrete_byte_mode else 'bit'
            values = channel.read_system_memory(
                area, protocol_offset, count, mode=mode
            )
        elif area == "G":
            mode = 'byte' if discrete_byte_mode else 'bit'
            values = channel.read_global_memory(protocol_offset, count, mode=mode)
        else:
            raise ReadError(f"Unsupported GE area: {area}")

        # 确保返回 list
        return values if isinstance(values, list) else [values]


def create_driver(context):
    return GESRTPDriver(read_executor=context.read_executor, metrics=context.metrics)
