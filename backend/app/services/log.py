from __future__ import annotations

import asyncio
import time
from collections import defaultdict
from datetime import datetime, timedelta
from typing import Any

from sqlalchemy import and_, func, or_, select, text, delete
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import ApiError
from app.core.time_utils import format_local_datetime, now_local, parse_local_datetime
from app.models import LogRetentionPolicy, OperationLog, SystemLog


SUPPORTED_LOG_LEVELS = {"DEBUG", "INFO", "WARN", "ERROR", "CRITICAL"}
SUPPORTED_LOG_TYPES = {"system", "auth", "operation", "collect", "storage", "frontend", "security", "alarm"}
SUPPORTED_LOG_KINDS = {"system", "operation", "all"}


class RateLimiter:
    """简单的令牌桶限流器（带最大 key 数限制）"""

    def __init__(self, max_requests: int, window_seconds: int = 60, max_keys: int = 10000):
        self.max_requests = max_requests
        self.window_seconds = window_seconds
        self.max_keys = max_keys
        self._requests: dict[str, list[float]] = defaultdict(list)
        self._last_cleanup = time.time()

    def _cleanup_expired(self) -> None:
        """周期性清理过期和超量的 key"""
        now = time.time()
        # 每 10 秒清理一次
        if now - self._last_cleanup < 10:
            return
        self._last_cleanup = now

        cutoff = now - self.window_seconds
        # 清理每个 key 内的过期请求
        expired_keys = []
        for key in list(self._requests.keys()):
            self._requests[key] = [t for t in self._requests[key] if t > cutoff]
            if not self._requests[key]:
                expired_keys.append(key)

        # 删除空 key
        for key in expired_keys:
            del self._requests[key]

        # 如果 key 数超限，删除最旧的 key
        if len(self._requests) > self.max_keys:
            # 按 key 最后访问时间排序，删除最旧的
            sorted_keys = sorted(
                self._requests.keys(),
                key=lambda k: self._requests[k][-1] if self._requests[k] else 0
            )
            for key in sorted_keys[: len(self._requests) - self.max_keys]:
                del self._requests[key]

    def is_allowed(self, key: str) -> bool:
        """检查是否允许请求"""
        now = time.time()
        cutoff = now - self.window_seconds

        # 周期性清理
        self._cleanup_expired()

        # 清理过期的请求记录
        self._requests[key] = [t for t in self._requests[key] if t > cutoff]

        if len(self._requests[key]) >= self.max_requests:
            return False

        # 记录这次请求
        self._requests[key].append(now)
        return True

    def get_remaining(self, key: str) -> int:
        """获取剩余配额"""
        now = time.time()
        cutoff = now - self.window_seconds
        self._requests[key] = [t for t in self._requests[key] if t > cutoff]
        return max(0, self.max_requests - len(self._requests[key]))


# 全局限流器实例
# 每个来源每分钟最多 30 条日志（防止错误风暴）
_client_log_limiter = RateLimiter(max_requests=30, window_seconds=60)


class LogService:
    """日志管理服务"""

    def __init__(self, session: AsyncSession):
        self.session = session

    # ==================== 日志列表查询 ====================

    async def list_logs(self, params: dict[str, Any]) -> dict[str, Any]:
        """查询日志列表，合并返回 operation_log 和 system_log"""
        page_no = self._to_positive_int(params.get("pageNo") or params.get("current"), default=1)
        page_size = self._to_positive_int(params.get("pageSize") or params.get("size"), default=20)
        kind = self._normalize_kind(params.get("kind"))
        level = self._normalize_level(params.get("level"))
        log_type = self._normalize_type(params.get("type"))
        source = str(params.get("source") or "").strip()
        service_name = str(params.get("serviceName") or "").strip()
        device_code = str(params.get("deviceCode") or "").strip()
        operator_name = str(params.get("operatorName") or "").strip()
        request_id = str(params.get("requestId") or "").strip()
        trace_id = str(params.get("traceId") or "").strip()
        result_status = str(params.get("resultStatus") or "").strip()
        keyword = str(params.get("keyword") or "").strip()
        start_time = self._coerce_datetime(params.get("startTime"))
        end_time = self._coerce_datetime(params.get("endTime"))

        # 如果未指定时间范围，默认最近24小时
        if start_time is None:
            start_time = now_local() - timedelta(hours=24)
        if end_time is None:
            end_time = now_local()

        # 单表模式：直接分页查询
        if kind == "operation":
            items, total = await self._query_operation_logs(
                page_no=page_no,
                page_size=page_size,
                level=level,
                log_type=log_type,
                source=source,
                service_name=service_name,
                device_code=device_code,
                operator_name=operator_name,
                request_id=request_id,
                result_status=result_status,
                keyword=keyword,
                start_time=start_time,
                end_time=end_time,
            )
            return self._build_page_payload(items, total, page_no, page_size)

        if kind == "system":
            items, total = await self._query_system_logs(
                page_no=page_no,
                page_size=page_size,
                level=level,
                log_type=log_type,
                source=source,
                service_name=service_name,
                device_code=device_code,
                trace_id=trace_id,
                keyword=keyword,
                start_time=start_time,
                end_time=end_time,
            )
            return self._build_page_payload(items, total, page_no, page_size)

        # all 模式：两表各取足够多的数据，合并后全局切片
        # 为保证分页正确，每表取 offset + limit 条
        fetch_size = (page_no * page_size)
        op_items, op_total = await self._query_operation_logs(
            page_no=1,
            page_size=fetch_size,
            level=level,
            log_type=log_type,
            source=source,
            service_name=service_name,
            device_code=device_code,
            operator_name=operator_name,
            request_id=request_id,
            result_status=result_status,
            keyword=keyword,
            start_time=start_time,
            end_time=end_time,
        )
        sys_items, sys_total = await self._query_system_logs(
            page_no=1,
            page_size=fetch_size,
            level=level,
            log_type=log_type,
            source=source,
            service_name=service_name,
            device_code=device_code,
            trace_id=trace_id,
            keyword=keyword,
            start_time=start_time,
            end_time=end_time,
        )

        # 合并并按时间排序
        all_items = op_items + sys_items
        all_items.sort(key=lambda x: x["time"], reverse=True)

        # 全局分页切片
        offset = (page_no - 1) * page_size
        items = all_items[offset:offset + page_size]
        total = op_total + sys_total

        return self._build_page_payload(items, total, page_no, page_size)

    async def _query_operation_logs(
        self,
        *,
        page_no: int,
        page_size: int,
        level: str | None,
        log_type: str | None,
        source: str,
        service_name: str,
        device_code: str,
        operator_name: str,
        request_id: str,
        result_status: str,
        keyword: str,
        start_time: datetime,
        end_time: datetime,
    ) -> tuple[list[dict[str, Any]], int]:
        """查询 operation_log 表"""
        conditions = [
            OperationLog.log_time >= start_time,
            OperationLog.log_time <= end_time,
        ]

        if level:
            conditions.append(OperationLog.level == level)
        if operator_name:
            conditions.append(OperationLog.operator_name.like(f"%{operator_name}%"))
        if request_id:
            conditions.append(OperationLog.request_id == request_id)
        if result_status:
            conditions.append(OperationLog.result_status == result_status)
        if keyword:
            conditions.append(
                or_(
                    OperationLog.content.like(f"%{keyword}%"),
                    OperationLog.module_name.like(f"%{keyword}%"),
                    OperationLog.action_name.like(f"%{keyword}%"),
                )
            )

        # 计算总数
        count_stmt = select(func.count(OperationLog.id)).where(and_(*conditions))
        total = (await self.session.execute(count_stmt)).scalar_one() or 0

        # 分页查询
        offset = (page_no - 1) * page_size
        stmt = (
            select(OperationLog)
            .where(and_(*conditions))
            .order_by(OperationLog.log_time.desc())
            .offset(offset)
            .limit(page_size)
        )

        rows = (await self.session.execute(stmt)).scalars().all()
        items = [self._serialize_operation_log(row) for row in rows]
        return items, total

    async def _query_system_logs(
        self,
        *,
        page_no: int,
        page_size: int,
        level: str | None,
        log_type: str | None,
        source: str,
        service_name: str,
        device_code: str,
        trace_id: str,
        keyword: str,
        start_time: datetime,
        end_time: datetime,
    ) -> tuple[list[dict[str, Any]], int]:
        """查询 system_log 表"""
        conditions = [
            SystemLog.log_time >= start_time,
            SystemLog.log_time <= end_time,
        ]

        if level:
            conditions.append(SystemLog.level == level)
        if log_type:
            conditions.append(SystemLog.log_type == log_type)
        if source:
            conditions.append(SystemLog.source.like(f"%{source}%"))
        if service_name:
            conditions.append(SystemLog.service_name == service_name)
        if device_code:
            conditions.append(SystemLog.device_code.like(f"%{device_code}%"))
        if trace_id:
            conditions.append(SystemLog.trace_id == trace_id)
        if keyword:
            conditions.append(
                or_(
                    SystemLog.content.like(f"%{keyword}%"),
                    SystemLog.source.like(f"%{keyword}%"),
                    SystemLog.device_code.like(f"%{keyword}%"),
                )
            )

        # 注意：DEBUG 过滤只在写入侧控制，查询侧允许查看历史 DEBUG 日志

        # 计算总数
        count_stmt = select(func.count(SystemLog.id)).where(and_(*conditions))
        total = (await self.session.execute(count_stmt)).scalar_one() or 0

        # 分页查询
        offset = (page_no - 1) * page_size
        stmt = (
            select(SystemLog)
            .where(and_(*conditions))
            .order_by(SystemLog.log_time.desc())
            .offset(offset)
            .limit(page_size)
        )

        rows = (await self.session.execute(stmt)).scalars().all()
        items = [self._serialize_system_log(row) for row in rows]
        return items, total

    def _serialize_operation_log(self, row: OperationLog) -> dict[str, Any]:
        return {
            "id": row.id,
            "kind": "operation",
            "time": self._format_datetime(row.log_time),
            "level": row.level,
            "type": "operation",
            "source": row.module_name,
            "serviceName": "",
            "moduleName": row.module_name,
            "actionName": row.action_name,
            "operatorName": row.operator_name,
            "deviceCode": "",
            "content": row.content,
            "resultStatus": row.result_status,
            "durationMs": row.duration_ms,
            "requestId": row.request_id,
            "traceId": row.request_id,
            "detail": row.detail_json,
        }

    def _serialize_system_log(self, row: SystemLog) -> dict[str, Any]:
        return {
            "id": row.id,
            "kind": "system",
            "time": self._format_datetime(row.log_time),
            "level": row.level,
            "type": row.log_type,
            "source": row.source,
            "serviceName": row.service_name,
            "moduleName": "",
            "actionName": "",
            "operatorName": "system",
            "deviceCode": row.device_code,
            "content": row.content,
            "resultStatus": "",
            "durationMs": row.elapsed_ms,
            "requestId": "",
            "traceId": row.trace_id,
            "detail": row.detail_json,
            "exceptionStack": row.exception_stack,
        }

    # ==================== 日志统计 ====================

    async def get_stats(self) -> dict[str, Any]:
        """获取日志统计数据"""
        now = now_local()
        today_start = now.replace(hour=0, minute=0, second=0, microsecond=0)
        yesterday_start = today_start - timedelta(days=1)

        # 今日日志总数
        today_op_count = await self._count_logs(OperationLog, today_start, now)
        today_sys_count = await self._count_logs(SystemLog, today_start, now)
        today_total = today_op_count + today_sys_count

        # 昨日日志总数
        yesterday_op_count = await self._count_logs(OperationLog, yesterday_start, today_start)
        yesterday_sys_count = await self._count_logs(SystemLog, yesterday_start, today_start)
        yesterday_total = yesterday_op_count + yesterday_sys_count

        # 今日异常数（WARN、ERROR、CRITICAL）
        today_warn_count = await self._count_logs_by_levels(
            [OperationLog, SystemLog],
            ["WARN", "ERROR", "CRITICAL"],
            today_start,
            now,
        )

        # DEBUG 状态
        policy = await self.get_policy()
        debug_enabled = bool(policy.debug_db_enabled)
        debug_remaining_seconds = 0
        if debug_enabled and policy.debug_enabled_until:
            remaining = (policy.debug_enabled_until - now).total_seconds()
            if remaining > 0:
                debug_remaining_seconds = int(remaining)
            else:
                debug_enabled = False

        # 日志总容量（估算）
        total_size_mb = await self._estimate_log_size_mb()

        # 最近清理信息
        last_cleanup_time = self._format_datetime(policy.last_cleanup_time) if policy.last_cleanup_time else ""
        last_cleanup_deleted_total = 0
        if policy.last_cleanup_result_json:
            result = policy.last_cleanup_result_json
            last_cleanup_deleted_total = int(result.get("deletedOperationLogs", 0)) + int(result.get("deletedSystemLogs", 0))

        # 级别趋势（最近24小时）
        level_trend = await self._get_level_trend(today_start, now)

        # 类型占比
        type_ratio = await self._get_type_ratio(today_start, now)

        # 来源占比
        source_ratio = await self._get_source_ratio(today_start, now)

        return {
            "todayTotal": today_total,
            "yesterdayTotal": yesterday_total,
            "todayWarnTotal": today_warn_count,
            "todayErrorTotal": await self._count_logs_by_levels([OperationLog, SystemLog], ["ERROR", "CRITICAL"], today_start, now),
            "todayCriticalTotal": await self._count_logs_by_levels([OperationLog, SystemLog], ["CRITICAL"], today_start, now),
            "debugEnabled": debug_enabled,
            "debugRemainingSeconds": debug_remaining_seconds,
            "debugEnabledUntil": self._format_datetime(policy.debug_enabled_until),
            "totalSizeMb": total_size_mb,
            "maxTotalMb": policy.max_total_mb,
            "lastCleanupTime": last_cleanup_time,
            "lastCleanupDeletedTotal": last_cleanup_deleted_total,
            "levelTrend": level_trend,
            "typeRatio": type_ratio,
            "sourceRatio": source_ratio,
        }

    async def _count_logs(self, model: type, start_time: datetime, end_time: datetime) -> int:
        """统计指定时间范围内的日志数量"""
        stmt = select(func.count(model.id)).where(
            and_(
                model.log_time >= start_time,
                model.log_time <= end_time,
            )
        )
        return (await self.session.execute(stmt)).scalar_one() or 0

    async def _count_logs_by_levels(
        self,
        models: list[type],
        levels: list[str],
        start_time: datetime,
        end_time: datetime,
    ) -> int:
        """统计指定级别的日志数量"""
        total = 0
        for model in models:
            stmt = select(func.count(model.id)).where(
                and_(
                    model.log_time >= start_time,
                    model.log_time <= end_time,
                    model.level.in_(levels),
                )
            )
            total += (await self.session.execute(stmt)).scalar_one() or 0
        return total

    async def _estimate_log_size_mb(self) -> float:
        """估算日志表大小（MB）"""
        try:
            # 查询表大小
            result = await self.session.execute(
                text("""
                    SELECT
                        COALESCE(SUM(DATA_LENGTH + INDEX_LENGTH) / 1024 / 1024, 0) AS size_mb
                    FROM information_schema.TABLES
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME IN ('operation_log', 'system_log')
                """)
            )
            row = result.mappings().one()
            return round(float(row["size_mb"] or 0), 2)
        except Exception:
            return 0.0

    async def _get_level_trend(self, start_time: datetime, end_time: datetime) -> list[dict[str, Any]]:
        """获取日志级别趋势"""
        levels = ["INFO", "WARN", "ERROR", "CRITICAL", "DEBUG"]
        result: list[dict[str, Any]] = []

        # 生成每小时的时间桶
        hours = []
        current = start_time
        while current < end_time:
            hours.append(current)
            current += timedelta(hours=1)

        # 统计每小时的各级别数量
        for level in levels:
            op_counts = await self._get_hourly_counts(OperationLog, level, start_time, end_time)
            sys_counts = await self._get_hourly_counts(SystemLog, level, start_time, end_time)

            for i, hour in enumerate(hours):
                total = op_counts.get(hour, 0) + sys_counts.get(hour, 0)
                if i < len(result):
                    result[i][level.lower()] = total
                else:
                    result.append({
                        "time": self._format_datetime(hour),
                        "info": 0,
                        "warn": 0,
                        "error": 0,
                        "critical": 0,
                        "debug": 0,
                        level.lower(): total,
                    })

        return result

    async def _get_hourly_counts(
        self,
        model: type,
        level: str,
        start_time: datetime,
        end_time: datetime,
    ) -> dict[datetime, int]:
        """按小时统计指定级别的日志数量"""
        stmt = (
            select(
                func.date_format(model.log_time, "%Y-%m-%d %H:00:00").label("hour"),
                func.count(model.id).label("count"),
            )
            .where(
                and_(
                    model.log_time >= start_time,
                    model.log_time <= end_time,
                    model.level == level,
                )
            )
            .group_by(text("hour"))
        )

        rows = (await self.session.execute(stmt)).mappings().all()
        return {
            datetime.strptime(row["hour"], "%Y-%m-%d %H:%M:%S"): int(row["count"])
            for row in rows
        }

    async def _get_type_ratio(self, start_time: datetime, end_time: datetime) -> list[dict[str, Any]]:
        """获取日志类型占比"""
        types = ["system", "auth", "operation", "collect", "storage", "frontend", "security", "alarm"]
        result: list[dict[str, Any]] = []

        for log_type in types:
            count = await self._count_logs_by_type(log_type, start_time, end_time)
            if count > 0:
                result.append({
                    "type": log_type,
                    "count": count,
                })

        total = sum(item["count"] for item in result)
        for item in result:
            item["ratio"] = round((item["count"] / total) * 100, 2) if total > 0 else 0

        return result

    async def _count_logs_by_type(self, log_type: str, start_time: datetime, end_time: datetime) -> int:
        """按类型统计日志数量"""
        if log_type == "operation":
            return await self._count_logs(OperationLog, start_time, end_time)
        else:
            stmt = select(func.count(SystemLog.id)).where(
                and_(
                    SystemLog.log_time >= start_time,
                    SystemLog.log_time <= end_time,
                    SystemLog.log_type == log_type,
                )
            )
            return (await self.session.execute(stmt)).scalar_one() or 0

    async def _get_source_ratio(self, start_time: datetime, end_time: datetime) -> list[dict[str, Any]]:
        """获取日志来源占比"""
        stmt = (
            select(
                SystemLog.source,
                func.count(SystemLog.id).label("count"),
            )
            .where(
                and_(
                    SystemLog.log_time >= start_time,
                    SystemLog.log_time <= end_time,
                )
            )
            .group_by(SystemLog.source)
            .order_by(text("count DESC"))
        )

        rows = (await self.session.execute(stmt)).mappings().all()
        total = sum(int(row["count"] or 0) for row in rows)

        return [
            {
                "source": row["source"] or "unknown",
                "count": int(row["count"] or 0),
                "ratio": round((int(row["count"] or 0) / total) * 100, 2) if total > 0 else 0,
            }
            for row in rows
        ]

    # ==================== 策略管理 ====================

    async def get_policy(self) -> LogRetentionPolicy:
        """获取日志策略配置"""
        stmt = select(LogRetentionPolicy).where(LogRetentionPolicy.id == 1)
        policy = await self.session.scalar(stmt)
        if policy is None:
            # 创建默认策略
            policy = LogRetentionPolicy(id=1)
            self.session.add(policy)
            await self.session.commit()
            await self.session.refresh(policy)
        return policy

    async def update_policy(self, params: dict[str, Any], operator_id: int | None, operator_name: str) -> LogRetentionPolicy:
        """更新日志策略配置"""
        policy = await self.get_policy()

        if "enabled" in params:
            policy.enabled = self._to_bool(params["enabled"], default=True)
        if "retentionDays" in params:
            days = self._to_positive_int(params["retentionDays"], default=7)
            if days < 1:
                raise ApiError(400, "保留天数不能小于 1 天")
            policy.retention_days = days
        if "maxTotalMb" in params:
            mb = self._to_positive_int(params["maxTotalMb"], default=1024)
            if mb < 100:
                raise ApiError(400, "容量上限不能小于 100MB")
            policy.max_total_mb = mb
        if "cleanupTime" in params:
            policy.cleanup_time = str(params["cleanupTime"]).strip()[:8]
        if "capacityCheckIntervalMinutes" in params:
            policy.capacity_check_interval_minutes = self._to_positive_int(params["capacityCheckIntervalMinutes"], default=30)
        if "batchSize" in params:
            policy.batch_size = self._to_positive_int(params["batchSize"], default=5000)

        policy.update_by_id = operator_id
        policy.update_by_name = operator_name
        policy.update_time = now_local()

        await self.session.commit()
        await self.session.refresh(policy)

        # 记录操作日志
        await self._write_operation_log(
            operator_id=operator_id,
            operator_name=operator_name,
            module_name="log",
            action_name="update_policy",
            level="INFO",
            content="更新日志清理策略配置",
            detail=params,
        )

        return policy

    # ==================== DEBUG 开关 ====================

    async def toggle_debug(
        self,
        enabled: bool,
        duration_minutes: int | None,
        reason: str | None,
        operator_id: int | None,
        operator_name: str,
    ) -> LogRetentionPolicy:
        """切换 DEBUG 落库开关"""
        policy = await self.get_policy()

        if enabled:
            if not reason or not reason.strip():
                raise ApiError(400, "开启 DEBUG 必须填写原因")
            duration = duration_minutes or 60
            if duration < 5 or duration > 480:
                raise ApiError(400, "开启时长必须在 5-480 分钟之间")
            policy.debug_db_enabled = True
            policy.debug_enabled_until = now_local() + timedelta(minutes=duration)
            policy.debug_reason = reason.strip()
            policy.debug_enabled_by_id = operator_id
            policy.debug_enabled_by_name = operator_name
            content = f"开启 DEBUG 日志落库，时长 {duration} 分钟，原因：{reason}"
        else:
            policy.debug_db_enabled = False
            policy.debug_enabled_until = None
            policy.debug_reason = ""
            content = "关闭 DEBUG 日志落库"

        policy.update_by_id = operator_id
        policy.update_by_name = operator_name
        policy.update_time = now_local()

        await self.session.commit()
        await self.session.refresh(policy)

        # 记录操作日志
        await self._write_operation_log(
            operator_id=operator_id,
            operator_name=operator_name,
            module_name="log",
            action_name="toggle_debug",
            level="WARN" if enabled else "INFO",
            content=content,
            detail={"enabled": enabled, "durationMinutes": duration_minutes, "reason": reason},
        )

        # 同时记录系统运行日志
        await self._write_system_log(
            level="WARN" if enabled else "INFO",
            log_type="system",
            source="backend.debug",
            content=content,
            detail={"operatorName": operator_name, "enabled": enabled},
        )

        return policy

    # ==================== 手动清理 ====================

    async def cleanup_logs(
        self,
        mode: str,
        before_time: datetime | None = None,
        start_time: datetime | None = None,
        end_time: datetime | None = None,
        force: bool = False,
        operator_id: int | None = None,
        operator_name: str = "system",
    ) -> dict[str, Any]:
        """手动清理日志"""
        policy = await self.get_policy()

        # 确定清理范围
        if mode == "policy":
            # 按策略清理
            cutoff_time = now_local() - timedelta(days=policy.retention_days)
        elif mode == "beforeTime":
            if not before_time:
                raise ApiError(400, "beforeTime 参数必填")
            cutoff_time = before_time
        elif mode == "range":
            if not start_time or not end_time:
                raise ApiError(400, "startTime 和 endTime 参数必填")
            cutoff_time = None  # 使用时间范围
        elif mode == "currentFilter":
            # 使用当前筛选条件，这里简化为按策略清理
            cutoff_time = now_local() - timedelta(days=policy.retention_days)
        else:
            raise ApiError(400, "无效的清理模式")

        # 保护最近24小时
        recent_cutoff = now_local() - timedelta(hours=24)
        if mode in ("policy", "beforeTime"):
            if cutoff_time and cutoff_time > recent_cutoff and not force:
                raise ApiError(400, "不允许清理最近 24 小时的日志，如需强制清理请设置 force=true")

        # 记录清理前状态
        size_before = await self._estimate_log_size_mb()

        # 执行清理
        deleted_op = 0
        deleted_sys = 0

        if mode == "range" and start_time and end_time:
            # 按时间范围清理
            deleted_op = await self._delete_logs_in_range(OperationLog, start_time, end_time, policy.batch_size, force, recent_cutoff)
            deleted_sys = await self._delete_logs_in_range(SystemLog, start_time, end_time, policy.batch_size, force, recent_cutoff)
        elif cutoff_time:
            # 按截止时间清理
            deleted_op = await self._delete_logs_before_time(OperationLog, cutoff_time, policy.batch_size, force, recent_cutoff)
            deleted_sys = await self._delete_logs_before_time(SystemLog, cutoff_time, policy.batch_size, force, recent_cutoff)

        await self.session.commit()

        # 更新清理记录
        size_after = await self._estimate_log_size_mb()
        cleanup_result = {
            "deletedOperationLogs": deleted_op,
            "deletedSystemLogs": deleted_sys,
            "sizeBeforeMb": size_before,
            "sizeAfterMb": size_after,
            "triggeredBy": operator_name,
            "triggerMode": "manual",
            "force": force,
        }

        policy.last_cleanup_time = now_local()
        policy.last_cleanup_result_json = cleanup_result
        policy.update_by_id = operator_id
        policy.update_by_name = operator_name
        await self.session.commit()

        # 记录操作日志
        await self._write_operation_log(
            operator_id=operator_id,
            operator_name=operator_name,
            module_name="log",
            action_name="cleanup",
            level="WARN",
            content=f"手动清理日志完成，删除 {deleted_op + deleted_sys} 条",
            detail=cleanup_result,
        )

        # 同时记录系统运行日志
        await self._write_system_log(
            level="WARN",
            log_type="system",
            source="backend.cleanup",
            content=f"手动清理日志完成，删除 {deleted_op + deleted_sys} 条",
            detail={"operatorName": operator_name, **cleanup_result},
        )

        return cleanup_result

    async def _delete_logs_before_time(
        self,
        model: type,
        cutoff_time: datetime,
        batch_size: int,
        force: bool,
        recent_cutoff: datetime,
    ) -> int:
        """删除指定时间之前的日志"""
        conditions = [model.log_time < cutoff_time]
        if not force:
            conditions.append(model.log_time < recent_cutoff)
        return await self._delete_logs_by_conditions(model, conditions, batch_size)


    async def _delete_logs_in_range(
        self,
        model: type,
        start_time: datetime,
        end_time: datetime,
        batch_size: int,
        force: bool,
        recent_cutoff: datetime,
    ) -> int:
        """删除指定时间范围内的日志"""
        conditions = [
            model.log_time >= start_time,
            model.log_time <= end_time,
        ]
        if not force:
            conditions.append(model.log_time < recent_cutoff)
        return await self._delete_logs_by_conditions(model, conditions, batch_size)


    async def _delete_logs_by_conditions(
        self,
        model: type,
        conditions: list[Any],
        batch_size: int,
    ) -> int:
        """按 id 分批删除日志，避免依赖 DELETE ... LIMIT 的方言特性。"""
        total_deleted = 0
        effective_batch_size = max(1, batch_size)

        while True:
            id_stmt = (
                select(model.id)
                .where(and_(*conditions))
                .order_by(model.log_time.asc(), model.id.asc())
                .limit(effective_batch_size)
            )
            ids = (await self.session.execute(id_stmt)).scalars().all()
            if not ids:
                break

            result = await self.session.execute(delete(model).where(model.id.in_(ids)))
            rowcount = result.rowcount
            deleted = rowcount if rowcount is not None and rowcount >= 0 else len(ids)
            total_deleted += deleted
            await self.session.commit()

            if deleted == 0 or len(ids) < effective_batch_size:
                break

        return total_deleted

    # ==================== 前端日志上报 ====================

    # 前端日志来源白名单，不在白名单内的统一归为 frontend-runtime
    FRONTEND_SOURCE_WHITELIST = {
        "frontend-runtime",
        "frontend-http",
        "frontend-vue",
        "frontend-resource",
        "frontend-manual",
    }

    async def report_client_log(self, params: dict[str, Any]) -> dict[str, Any]:
        """前端客户端日志上报（带限流）

        Returns:
            dict: 包含 accepted 状态和 remaining 配额
        """
        level = self._normalize_level(params.get("level"))
        if level not in ("WARN", "ERROR", "CRITICAL"):
            # 只接受 WARN 及以上级别
            return {"accepted": False, "reason": "level_not_allowed", "remaining": 0}

        # 截断字段防止数据库溢出（数据库 source/trace_id 限制 64 字符）
        raw_source = str(params.get("source") or "frontend-runtime")[:64]
        # 白名单校验，不在白名单内的统一归为 frontend-runtime，防止匿名请求撑大限流器
        source = raw_source if raw_source in self.FRONTEND_SOURCE_WHITELIST else "frontend-runtime"
        client_ip = str(params.get("clientIp") or "unknown")[:64]
        trace_id = str(params.get("traceId") or "")[:64]

        # 限流 key 使用归一化后的 source
        rate_key = f"{source}:{client_ip}"

        # 检查限流
        if not _client_log_limiter.is_allowed(rate_key):
            remaining = _client_log_limiter.get_remaining(rate_key)
            return {"accepted": False, "reason": "rate_limited", "remaining": remaining}

        log_entry = SystemLog(
            log_time=now_local(),
            level=level,
            log_type="frontend",
            source=source,
            device_code="",
            host_name="",
            service_name="frontend",
            content=str(params.get("content") or "")[:500],
            detail_json=params.get("detail"),
            trace_id=trace_id,
            exception_stack=str(params.get("exceptionStack") or "")[:65535] if params.get("exceptionStack") else None,
        )

        self.session.add(log_entry)
        await self.session.commit()

        remaining = _client_log_limiter.get_remaining(rate_key)
        return {"accepted": True, "remaining": remaining}

    # ==================== 内部操作日志写入 ====================

    async def _write_system_log(
        self,
        *,
        level: str,
        log_type: str,
        source: str,
        content: str,
        detail: dict[str, Any] | None = None,
        trace_id: str = "",
        exception_stack: str | None = None,
        elapsed_ms: int | None = None,
    ) -> None:
        """写入系统运行日志（内部方法，使用当前 session）"""
        log_entry = SystemLog(
            log_time=now_local(),
            level=level,
            log_type=log_type,
            source=source,
            device_code="",
            host_name="",
            service_name="backend",
            content=content[:500],
            detail_json=detail,
            trace_id=trace_id,
            exception_stack=exception_stack[:65535] if exception_stack else None,
            elapsed_ms=elapsed_ms,
        )
        self.session.add(log_entry)
        await self.session.commit()

    async def _write_operation_log(
        self,
        *,
        operator_id: int | None,
        operator_name: str,
        module_name: str,
        action_name: str,
        level: str,
        content: str,
        detail: dict[str, Any] | None = None,
        request_id: str = "",
        ip: str = "",
        user_agent: str = "",
        biz_type: str = "",
        biz_id: str = "",
        duration_ms: int | None = None,
        result_status: str = "success",
    ) -> None:
        """写入操作日志"""
        log_entry = OperationLog(
            log_time=now_local(),
            operator_id=operator_id,
            operator_name=operator_name,
            module_name=module_name,
            action_name=action_name,
            level=level,
            content=content[:500],
            detail_json=detail,
            request_id=request_id,
            ip=ip,
            user_agent=user_agent,
            biz_type=biz_type,
            biz_id=biz_id,
            duration_ms=duration_ms,
            result_status=result_status,
        )

        self.session.add(log_entry)
        await self.session.commit()

    # ==================== 工具方法 ====================

    def _normalize_kind(self, value: Any) -> str:
        normalized = str(value or "all").strip().lower()
        if normalized not in SUPPORTED_LOG_KINDS:
            normalized = "all"
        return normalized

    def _normalize_level(self, value: Any) -> str:
        normalized = str(value or "").strip().upper()
        if normalized not in SUPPORTED_LOG_LEVELS:
            normalized = ""
        return normalized

    def _normalize_type(self, value: Any) -> str:
        normalized = str(value or "").strip().lower()
        if normalized not in SUPPORTED_LOG_TYPES:
            normalized = ""
        return normalized

    def _to_positive_int(self, value: Any, *, default: int) -> int:
        if value in (None, ""):
            return default
        try:
            normalized = int(value)
        except (TypeError, ValueError):
            return default
        if normalized <= 0:
            return default
        return normalized

    def _to_bool(self, value: Any, *, default: bool) -> bool:
        if value in (None, ""):
            return default
        if isinstance(value, bool):
            return value
        normalized = str(value).strip().lower()
        if normalized in {"1", "true", "yes", "on"}:
            return True
        if normalized in {"0", "false", "no", "off"}:
            return False
        return default

    def _coerce_datetime(self, value: Any) -> datetime | None:
        return parse_local_datetime(value)

    def _format_datetime(self, value: datetime | None) -> str:
        return format_local_datetime(value)

    def _build_page_payload(
        self,
        items: list[dict[str, Any]],
        total: int,
        page_no: int,
        page_size: int,
    ) -> dict[str, Any]:
        return {
            "list": items,
            "records": items,
            "total": total,
            "pageNo": page_no,
            "pageSize": page_size,
            "current": page_no,
            "size": page_size,
            "pageNum": page_no,
        }

