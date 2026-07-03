from __future__ import annotations

from datetime import datetime

from sqlalchemy import BigInteger, DateTime, Integer, text
from sqlalchemy.orm import Mapped, mapped_column

from app.core.time_utils import now_local
from app.models.base import Base


class CollectorSetting(Base):
    """collector 运行参数配置（单行，id=1）。app 端可配，collector 启动读取覆盖 settings.toml。"""
    __tablename__ = "collector_setting"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    stream_maxlen: Mapped[int] = mapped_column(Integer, nullable=False, default=50000, server_default=text("50000"))
    update_time: Mapped[datetime] = mapped_column(DateTime, nullable=False, default=now_local, onupdate=now_local)
