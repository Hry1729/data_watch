from __future__ import annotations

from datetime import datetime

from sqlalchemy import BigInteger, DateTime, ForeignKey, Index, Integer, String, Text, JSON
from sqlalchemy.orm import Mapped, mapped_column

from app.core.time_utils import now_local
from app.models.base import Base


class OperationLog(Base):
    """用户操作审计日志"""
    __tablename__ = "operation_log"
    __table_args__ = (
        Index("idx_operation_log_log_time", "log_time"),
        Index("idx_operation_log_level_log_time", "level", "log_time"),
        Index("idx_operation_log_operator_log_time", "operator_id", "log_time"),
        Index("idx_operation_log_module_log_time", "module_name", "log_time"),
        Index("idx_operation_log_request_id", "request_id"),
        Index("idx_operation_log_result_log_time", "result_status", "log_time"),
    )

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True, autoincrement=True)
    log_time: Mapped[datetime] = mapped_column(DateTime, nullable=False)
    operator_id: Mapped[int | None] = mapped_column(BigInteger, ForeignKey("sys_user.id", ondelete="SET NULL"), nullable=True)
    operator_name: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    module_name: Mapped[str] = mapped_column(String(64), nullable=False)
    action_name: Mapped[str] = mapped_column(String(64), nullable=False)
    level: Mapped[str] = mapped_column(String(16), nullable=False)
    content: Mapped[str] = mapped_column(String(500), nullable=False)
    detail_json: Mapped[dict | None] = mapped_column(JSON, nullable=True)
    request_id: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    ip: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    user_agent: Mapped[str] = mapped_column(String(255), nullable=False, default="")
    biz_type: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    biz_id: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    duration_ms: Mapped[int | None] = mapped_column(Integer, nullable=True)
    result_status: Mapped[str] = mapped_column(String(32), nullable=False, default="")
    create_time: Mapped[datetime] = mapped_column(DateTime, nullable=False, default=now_local)


class SystemLog(Base):
    """系统运行日志"""
    __tablename__ = "system_log"
    __table_args__ = (
        Index("idx_system_log_log_time", "log_time"),
        Index("idx_system_log_level_log_time", "level", "log_time"),
        Index("idx_system_log_log_type_log_time", "log_type", "log_time"),
        Index("idx_system_log_source_log_time", "source", "log_time"),
        Index("idx_system_log_service_log_time", "service_name", "log_time"),
        Index("idx_system_log_device_code_log_time", "device_code", "log_time"),
        Index("idx_system_log_device_log_time", "device_id", "log_time"),
        Index("idx_system_log_trace_id", "trace_id"),
    )

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True, autoincrement=True)
    log_time: Mapped[datetime] = mapped_column(DateTime, nullable=False)
    level: Mapped[str] = mapped_column(String(16), nullable=False)
    log_type: Mapped[str] = mapped_column(String(32), nullable=False)
    source: Mapped[str] = mapped_column(String(64), nullable=False)
    device_id: Mapped[int | None] = mapped_column(BigInteger, nullable=True)
    device_code: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    point_id: Mapped[int | None] = mapped_column(BigInteger, nullable=True)
    host_name: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    service_name: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    content: Mapped[str] = mapped_column(String(500), nullable=False)
    detail_json: Mapped[dict | None] = mapped_column(JSON, nullable=True)
    trace_id: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    exception_stack: Mapped[str | None] = mapped_column(Text, nullable=True)
    elapsed_ms: Mapped[int | None] = mapped_column(Integer, nullable=True)
    create_time: Mapped[datetime] = mapped_column(DateTime, nullable=False, default=now_local)


class LogRetentionPolicy(Base):
    """日志清理策略和 DEBUG 开关配置"""
    __tablename__ = "log_retention_policy"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    enabled: Mapped[bool] = mapped_column(Integer, nullable=False, default=1)
    retention_days: Mapped[int] = mapped_column(Integer, nullable=False, default=7)
    max_total_mb: Mapped[int] = mapped_column(Integer, nullable=False, default=1024)
    cleanup_time: Mapped[str] = mapped_column(String(8), nullable=False, default="03:00")
    capacity_check_interval_minutes: Mapped[int] = mapped_column(Integer, nullable=False, default=30)
    batch_size: Mapped[int] = mapped_column(Integer, nullable=False, default=5000)
    debug_db_enabled: Mapped[bool] = mapped_column(Integer, nullable=False, default=0)
    debug_enabled_until: Mapped[datetime | None] = mapped_column(DateTime, nullable=True)
    debug_reason: Mapped[str] = mapped_column(String(255), nullable=False, default="")
    debug_enabled_by_id: Mapped[int | None] = mapped_column(BigInteger, ForeignKey("sys_user.id", ondelete="SET NULL"), nullable=True)
    debug_enabled_by_name: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    last_cleanup_time: Mapped[datetime | None] = mapped_column(DateTime, nullable=True)
    last_cleanup_result_json: Mapped[dict | None] = mapped_column(JSON, nullable=True)
    update_by_id: Mapped[int | None] = mapped_column(BigInteger, ForeignKey("sys_user.id", ondelete="SET NULL"), nullable=True)
    update_by_name: Mapped[str] = mapped_column(String(64), nullable=False, default="system")
    create_time: Mapped[datetime] = mapped_column(DateTime, nullable=False, default=now_local)
    update_time: Mapped[datetime] = mapped_column(
        DateTime,
        nullable=False,
        default=now_local,
        onupdate=now_local,
    )

