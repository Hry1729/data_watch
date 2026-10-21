"""日志定时任务调度器

负责：
1. 每天按 cleanup_time 自动清理日志
2. 每 capacity_check_interval_minutes 检查容量
3. DEBUG 到期自动关闭
4. 定时任务执行结果写入 system_log
"""
from __future__ import annotations

import asyncio
from datetime import date, datetime, timedelta
from typing import Any

from app.core.log_writer import write_system_log
from app.core.logging import get_logger
from app.core.time_utils import now_local
from app.db.mysql import get_mysql_session_factory
from app.models import LogRetentionPolicy, OperationLog, SystemLog
from sqlalchemy import and_, delete, func, select, text

logger = get_logger("backend.log_scheduler")

# 定时清理的轮询粒度（秒）。不再一次睡到目标点，而是最多睡这么久就重新读取策略，
# 这样管理员修改清理时间/启用状态后最迟一个轮询周期内生效。
CLEANUP_POLL_SECONDS = 60


def _parse_cleanup_time(value: Any) -> tuple[int, int]:
    try:
        hour, minute = map(int, str(value or "03:00").split(":"))
    except (ValueError, AttributeError):
        return 3, 0
    if not (0 <= hour <= 23 and 0 <= minute <= 59):
        return 3, 0
    return hour, minute


class LogScheduler:
    """日志定时任务调度器"""

    def __init__(self):
        self._running = False
        self._tasks: list[asyncio.Task] = []
        self._last_capacity_check: datetime | None = None
        # 已执行过定时清理的日期（每天至多一次）。
        self._last_scheduled_cleanup_date: date | None = None
        # 启动时今天的清理点已过：今天不补跑（保持原有语义），除非之后策略发生变化。
        self._cleanup_skip_date: date | None = None
        # 上一次轮询看到的 (enabled, (hour, minute))，用于识别策略变化。
        self._observed_cleanup_policy: tuple[bool, tuple[int, int]] | None = None

    async def start(self) -> None:
        """启动定时任务"""
        if self._running:
            return

        self._running = True
        logger.info("Log scheduler started")

        # 启动三个定时任务
        self._tasks = [
            asyncio.create_task(self._cleanup_scheduler()),
            asyncio.create_task(self._capacity_checker()),
            asyncio.create_task(self._debug_expiration_checker()),
        ]

        # 记录启动日志
        await write_system_log(
            level="INFO",
            log_type="system",
            source="backend.scheduler",
            content="日志定时任务调度器启动",
            detail={"tasks": ["cleanup", "capacity_check", "debug_expiration"]},
        )

    async def stop(self) -> None:
        """停止定时任务"""
        self._running = False
        for task in self._tasks:
            task.cancel()
            try:
                await task
            except asyncio.CancelledError:
                pass
        self._tasks = []

        logger.info("Log scheduler stopped")

        # 记录停止日志
        await write_system_log(
            level="INFO",
            log_type="system",
            source="backend.scheduler",
            content="日志定时任务调度器停止",
        )

    def _cleanup_decision(self, policy: Any, now: datetime) -> tuple[bool, float]:
        """根据最新策略判断此刻是否应执行定时清理，返回 (是否执行, 下次轮询前的睡眠秒数)。

        规则：
        - 每天至多执行一次定时清理；
        - 到达/越过今天的清理时间且今天尚未执行 → 执行；
        - 调度器启动时今天的清理点已过 → 今天不补跑（与旧行为一致）；
        - 但若运行中策略变化（改清理时间、重新启用）使今天的清理点已过且今天尚未执行，
          则立即补跑，而不是像旧实现那样跳过当天（PR #23 审查意见）。
        """
        hour, minute = _parse_cleanup_time(getattr(policy, "cleanup_time", None))
        enabled = bool(getattr(policy, "enabled", False))
        signature = (enabled, (hour, minute))
        today = now.date()
        today_target = now.replace(hour=hour, minute=minute, second=0, microsecond=0)

        first_observation = self._observed_cleanup_policy is None
        changed = not first_observation and signature != self._observed_cleanup_policy
        self._observed_cleanup_policy = signature
        if first_observation:
            last_time = getattr(policy, "last_cleanup_time", None)
            last_result = getattr(policy, "last_cleanup_result_json", None) or {}
            if (
                isinstance(last_time, datetime)
                and isinstance(last_result, dict)
                and last_result.get("triggerMode") == "scheduled"
            ):
                self._last_scheduled_cleanup_date = last_time.date()
            if now >= today_target:
                self._cleanup_skip_date = today
        elif changed:
            self._cleanup_skip_date = None

        if not enabled:
            return False, float(CLEANUP_POLL_SECONDS)

        if now >= today_target:
            if self._last_scheduled_cleanup_date != today and self._cleanup_skip_date != today:
                return True, 0.0
            next_target = today_target + timedelta(days=1)
        else:
            next_target = today_target
        wait = (next_target - now).total_seconds()
        return False, float(max(1.0, min(CLEANUP_POLL_SECONDS, wait)))

    async def _cleanup_scheduler(self) -> None:
        """每天定时清理任务

        根据 policy.cleanup_time 在指定时间执行清理。每次轮询都重新读取策略（B008），
        因此禁用、改时间、改留存天数都按最新值生效。
        """
        while self._running:
            try:
                policy = await self._get_policy()
                if not self._running:
                    break
                now = now_local()
                due, sleep_seconds = self._cleanup_decision(policy, now)
                if due:
                    # 先记日期再执行：执行失败也不在同一天内每分钟重试（与旧行为一致）。
                    self._last_scheduled_cleanup_date = now.date()
                    await self._execute_cleanup(policy)
                    continue
                await asyncio.sleep(sleep_seconds)

            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error(f"Cleanup scheduler error: {e}")
                await write_system_log(
                    level="ERROR",
                    log_type="system",
                    source="backend.scheduler",
                    content=f"定时清理任务异常: {str(e)[:200]}",
                    exception_stack=str(e),
                )
                await asyncio.sleep(60)

    async def _capacity_checker(self) -> None:
        """容量检查任务

        每 capacity_check_interval_minutes 检查一次日志容量，
        如果超过 max_total_mb 则触发强制清理。
        """
        while self._running:
            try:
                policy = await self._get_policy()
                interval_minutes = policy.capacity_check_interval_minutes or 30

                # 等待间隔时间
                await asyncio.sleep(interval_minutes * 60)

                if not self._running:
                    break

                # B008：睡醒后重新读取策略（容量上限/批量大小可能已修改），不用睡前快照。
                policy = await self._get_policy()

                # 检查容量
                await self._check_capacity(policy)

            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error(f"Capacity checker error: {e}")
                await asyncio.sleep(60)

    async def _debug_expiration_checker(self) -> None:
        """DEBUG 过期检查任务

        每分钟检查一次 DEBUG 是否过期，过期则自动关闭。
        """
        while self._running:
            try:
                await asyncio.sleep(60)
                if not self._running:
                    break

                # 检查 DEBUG 是否过期
                await self._check_debug_expiration()

            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error(f"Debug expiration checker error: {e}")
                await asyncio.sleep(60)

    async def _execute_cleanup(self, policy: LogRetentionPolicy) -> None:
        """执行日志清理"""
        start_time = now_local()
        retention_days = policy.retention_days or 7
        batch_size = policy.batch_size or 5000
        cutoff_time = start_time - timedelta(days=retention_days)

        # 保护最近24小时
        recent_cutoff = start_time - timedelta(hours=24)
        effective_cutoff = min(cutoff_time, recent_cutoff)

        deleted_op = 0
        deleted_sys = 0

        session_factory = get_mysql_session_factory()
        async with session_factory() as session:
            # 删除 operation_log
            deleted_op = await self._batch_delete(
                session, OperationLog, effective_cutoff, batch_size
            )

            # 删除 system_log
            deleted_sys = await self._batch_delete(
                session, SystemLog, effective_cutoff, batch_size
            )

            # 更新清理记录
            policy = await session.get(LogRetentionPolicy, 1)
            if policy:
                policy.last_cleanup_time = start_time
                policy.last_cleanup_result_json = {
                    "deletedOperationLogs": deleted_op,
                    "deletedSystemLogs": deleted_sys,
                    "triggerMode": "scheduled",
                    "cutoffTime": cutoff_time.isoformat(),
                    "effectiveCutoff": effective_cutoff.isoformat(),
                }
                await session.commit()

        end_time = now_local()
        duration_ms = int((end_time - start_time).total_seconds() * 1000)

        # 记录清理结果
        await write_system_log(
            level="INFO",
            log_type="system",
            source="backend.scheduler",
            content=f"定时清理完成: 删除 {deleted_op + deleted_sys} 条日志",
            detail={
                "deletedOperationLogs": deleted_op,
                "deletedSystemLogs": deleted_sys,
                "retentionDays": retention_days,
                "durationMs": duration_ms,
            },
            elapsed_ms=duration_ms,
        )

        logger.info(f"Auto cleanup completed: {deleted_op} operation logs, {deleted_sys} system logs")

    async def _batch_delete(
        self,
        session,
        model: type,
        cutoff_time: datetime,
        batch_size: int,
    ) -> int:
        """分批删除日志"""
        total_deleted = 0

        while True:
            # 查询要删除的 ID
            id_stmt = (
                select(model.id)
                .where(model.log_time < cutoff_time)
                .order_by(model.log_time.asc(), model.id.asc())
                .limit(batch_size)
            )
            ids = (await session.execute(id_stmt)).scalars().all()

            if not ids:
                break

            # 批量删除
            result = await session.execute(delete(model).where(model.id.in_(ids)))
            deleted = result.rowcount or len(ids)
            total_deleted += deleted
            await session.commit()

            if deleted == 0 or len(ids) < batch_size:
                break

        return total_deleted

    async def _check_capacity(self, policy: LogRetentionPolicy) -> None:
        """检查日志容量，超限时按容量目标清理"""
        try:
            session_factory = get_mysql_session_factory()
            async with session_factory() as session:
                # 查询表大小
                result = await session.execute(
                    text("""
                        SELECT
                            COALESCE(SUM(DATA_LENGTH + INDEX_LENGTH) / 1024 / 1024, 0) AS size_mb
                        FROM information_schema.TABLES
                        WHERE TABLE_SCHEMA = DATABASE()
                          AND TABLE_NAME IN ('operation_log', 'system_log')
                    """)
                )
                row = result.mappings().one()
                current_size_mb = float(row["size_mb"] or 0)

                max_size_mb = policy.max_total_mb or 1024

                if current_size_mb > max_size_mb:
                    # 超过容量上限，触发容量模式清理
                    await write_system_log(
                        level="WARN",
                        log_type="system",
                        source="backend.scheduler",
                        content=f"日志容量超限: {current_size_mb:.2f}MB > {max_size_mb}MB，触发容量清理",
                        detail={
                            "currentSizeMb": round(current_size_mb, 2),
                            "maxSizeMb": max_size_mb,
                        },
                    )

                    # 执行容量模式清理，目标是降到阈值以下
                    await self._execute_capacity_cleanup(session, policy, current_size_mb, max_size_mb)

        except Exception as e:
            logger.error(f"Capacity check error: {e}")
            await write_system_log(
                level="ERROR",
                log_type="system",
                source="backend.scheduler",
                content=f"容量检查异常: {str(e)[:200]}",
                exception_stack=str(e),
            )

    async def _execute_capacity_cleanup(
        self,
        session,
        policy: LogRetentionPolicy,
        current_size_mb: float,
        max_size_mb: float,
    ) -> None:
        """执行容量模式清理

        注意：InnoDB 删除行后物理空间不会立即释放，因此不使用 DATA_LENGTH 作为循环条件。
        改用基于行数的估算：根据超出比例估算需要删除的行数，执行一轮删除后等待下次调度评估。
        """
        batch_size = policy.batch_size or 5000
        # 保护最近24小时
        recent_cutoff = now_local() - timedelta(hours=24)

        # 目标是降到阈值的 90%
        target_size_mb = max_size_mb * 0.9

        # 计算超出比例
        excess_ratio = (current_size_mb - target_size_mb) / current_size_mb
        if excess_ratio <= 0:
            return

        # 获取当前总行数（可删除范围内的）
        op_count = await session.scalar(
            select(func.count(OperationLog.id)).where(OperationLog.log_time < recent_cutoff)
        ) or 0
        sys_count = await session.scalar(
            select(func.count(SystemLog.id)).where(SystemLog.log_time < recent_cutoff)
        ) or 0
        total_deletable = op_count + sys_count

        if total_deletable == 0:
            # 没有可删除的日志
            await write_system_log(
                level="WARN",
                log_type="system",
                source="backend.scheduler",
                content=f"容量超限但无可删除日志（全部在24小时保护期内）",
                detail={
                    "currentSizeMb": round(current_size_mb, 2),
                    "maxSizeMb": max_size_mb,
                    "targetSizeMb": round(target_size_mb, 2),
                },
            )
            return

        # 估算需要删除的行数（超出比例 × 可删除总数）
        # 为避免过度删除，最多删除超出比例的 1.2 倍
        estimated_delete_count = int(total_deletable * excess_ratio * 1.2)
        # 但至少删除 batch_size，保证有实际效果
        estimated_delete_count = max(estimated_delete_count, batch_size)

        total_deleted_op = 0
        total_deleted_sys = 0
        start_time = now_local()
        total_deleted = 0

        # 删除日志，直到达到估算数量或没有可删的
        while total_deleted < estimated_delete_count:
            deleted_this_round = 0
            remaining = estimated_delete_count - total_deleted

            if remaining <= 0:
                break

            # 删除 operation_log，limit 不超过 remaining
            op_limit = min(batch_size, remaining)
            id_stmt = (
                select(OperationLog.id)
                .where(OperationLog.log_time < recent_cutoff)
                .order_by(OperationLog.log_time.asc(), OperationLog.id.asc())
                .limit(op_limit)
            )
            ids = (await session.execute(id_stmt)).scalars().all()
            if ids:
                result = await session.execute(delete(OperationLog).where(OperationLog.id.in_(ids)))
                rowcount = result.rowcount
                deleted = rowcount if rowcount is not None and rowcount >= 0 else len(ids)
                deleted_this_round += deleted
                total_deleted_op += deleted
                total_deleted += deleted

            # 删完 operation_log 后重新检查剩余配额
            remaining = estimated_delete_count - total_deleted
            if remaining <= 0:
                await session.commit()
                break

            # 删除 system_log，limit 不超过 remaining
            sys_limit = min(batch_size, remaining)
            id_stmt = (
                select(SystemLog.id)
                .where(SystemLog.log_time < recent_cutoff)
                .order_by(SystemLog.log_time.asc(), SystemLog.id.asc())
                .limit(sys_limit)
            )
            ids = (await session.execute(id_stmt)).scalars().all()
            if ids:
                result = await session.execute(delete(SystemLog).where(SystemLog.id.in_(ids)))
                rowcount = result.rowcount
                deleted = rowcount if rowcount is not None and rowcount >= 0 else len(ids)
                deleted_this_round += deleted
                total_deleted_sys += deleted
                total_deleted += deleted

            await session.commit()

            # 只有本轮两张表合计删除 0 条才退出
            if deleted_this_round == 0:
                break

        end_time = now_local()
        duration_ms = int((end_time - start_time).total_seconds() * 1000)

        # 更新清理记录
        policy = await session.get(LogRetentionPolicy, 1)
        if policy:
            policy.last_cleanup_time = start_time
            policy.last_cleanup_result_json = {
                "deletedOperationLogs": total_deleted_op,
                "deletedSystemLogs": total_deleted_sys,
                "triggerMode": "capacity",
                "sizeBeforeMb": round(current_size_mb, 2),
                "targetSizeMb": round(target_size_mb, 2),
                "estimatedDeleteCount": estimated_delete_count,
                "actualDeletedCount": total_deleted,
                "excessRatio": round(excess_ratio, 4),
                "durationMs": duration_ms,
            }
            await session.commit()

        await write_system_log(
            level="INFO",
            log_type="system",
            source="backend.scheduler",
            content=f"容量清理完成: 删除 {total_deleted} 条（预估 {estimated_delete_count} 条）",
            detail={
                "deletedOperationLogs": total_deleted_op,
                "deletedSystemLogs": total_deleted_sys,
                "sizeBeforeMb": round(current_size_mb, 2),
                "targetSizeMb": round(target_size_mb, 2),
                "excessRatio": round(excess_ratio, 4),
                "durationMs": duration_ms,
            },
            elapsed_ms=duration_ms,
        )

        logger.info(
            f"Capacity cleanup completed: {total_deleted} logs deleted "
            f"(estimated {estimated_delete_count}), excess_ratio={excess_ratio:.2%}"
        )

    async def _check_debug_expiration(self) -> None:
        """检查 DEBUG 是否过期"""
        try:
            session_factory = get_mysql_session_factory()
            async with session_factory() as session:
                policy = await session.get(LogRetentionPolicy, 1)
                if not policy or not policy.debug_db_enabled:
                    return

                if policy.debug_enabled_until:
                    if now_local() >= policy.debug_enabled_until:
                        # DEBUG 已过期，自动关闭
                        policy.debug_db_enabled = False
                        policy.debug_enabled_until = None
                        await session.commit()

                        await write_system_log(
                            level="INFO",
                            log_type="system",
                            source="backend.scheduler",
                            content="DEBUG 日志落库已自动关闭（到期）",
                            detail={
                                "debugReason": policy.debug_reason,
                                "debugEnabledByName": policy.debug_enabled_by_name,
                            },
                        )

                        logger.info("DEBUG logging auto-disabled (expired)")

        except Exception as e:
            logger.error(f"Debug expiration check error: {e}")

    async def _get_policy(self) -> LogRetentionPolicy:
        """获取日志策略"""
        session_factory = get_mysql_session_factory()
        async with session_factory() as session:
            policy = await session.get(LogRetentionPolicy, 1)
            if policy is None:
                policy = LogRetentionPolicy(id=1)
                session.add(policy)
                await session.commit()
                await session.refresh(policy)
            return policy


# 全局调度器实例
_scheduler: LogScheduler | None = None


async def start_log_scheduler() -> None:
    """启动日志调度器"""
    global _scheduler
    if _scheduler is None:
        _scheduler = LogScheduler()
    await _scheduler.start()


async def stop_log_scheduler() -> None:
    """停止日志调度器"""
    global _scheduler
    if _scheduler is not None:
        await _scheduler.stop()

