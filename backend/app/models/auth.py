from __future__ import annotations

from datetime import datetime

from sqlalchemy import BigInteger, Boolean, DateTime, ForeignKey, Index, Integer, String, Text, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column

from app.core.time_utils import now_local
from app.models.base import Base


class SysUser(Base):
    __tablename__ = "sys_user"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True, autoincrement=True)
    user_name: Mapped[str] = mapped_column(String(64), unique=True, nullable=False)
    password_hash: Mapped[str] = mapped_column(String(255), nullable=False)
    nick_name: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    email: Mapped[str] = mapped_column(String(128), nullable=False, default="")
    phone: Mapped[str] = mapped_column(String(32), nullable=False, default="")
    gender: Mapped[str] = mapped_column(String(16), nullable=False, default="unknown")
    avatar: Mapped[str] = mapped_column(String(255), nullable=False, default="")
    status: Mapped[str] = mapped_column(String(16), nullable=False, default="1")
    last_login_time: Mapped[datetime | None] = mapped_column(DateTime, nullable=True)
    last_login_ip: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    create_by: Mapped[str] = mapped_column(String(64), nullable=False, default="system")
    create_time: Mapped[datetime] = mapped_column(DateTime, nullable=False, default=now_local)
    update_by: Mapped[str] = mapped_column(String(64), nullable=False, default="system")
    update_time: Mapped[datetime] = mapped_column(
        DateTime,
        nullable=False,
        default=now_local,
        onupdate=now_local,
    )
    deleted: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)


class SysRole(Base):
    __tablename__ = "sys_role"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True, autoincrement=True)
    role_code: Mapped[str] = mapped_column(String(64), unique=True, nullable=False)
    role_name: Mapped[str] = mapped_column(String(64), nullable=False)
    description: Mapped[str] = mapped_column(String(255), nullable=False, default="")
    enabled: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)
    sort: Mapped[int] = mapped_column(Integer, nullable=False, default=1)
    create_time: Mapped[datetime] = mapped_column(DateTime, nullable=False, default=now_local)
    update_time: Mapped[datetime] = mapped_column(
        DateTime,
        nullable=False,
        default=now_local,
        onupdate=now_local,
    )


class SysUserRole(Base):
    __tablename__ = "sys_user_role"
    __table_args__ = (
        UniqueConstraint("user_id", "role_id", name="uq_sys_user_role_user_role"),
        Index("idx_sys_user_role_user_id", "user_id"),
        Index("idx_sys_user_role_role_id", "role_id"),
    )

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True, autoincrement=True)
    user_id: Mapped[int] = mapped_column(BigInteger, ForeignKey("sys_user.id", ondelete="CASCADE"), nullable=False)
    role_id: Mapped[int] = mapped_column(BigInteger, ForeignKey("sys_role.id", ondelete="CASCADE"), nullable=False)


class SysMenu(Base):
    __tablename__ = "sys_menu"
    __table_args__ = (
        Index("idx_sys_menu_parent_id", "parent_id"),
        Index("idx_sys_menu_sort", "sort"),
    )

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True, autoincrement=True)
    parent_id: Mapped[int | None] = mapped_column(BigInteger, ForeignKey("sys_menu.id", ondelete="CASCADE"), nullable=True)
    name: Mapped[str] = mapped_column(String(128), unique=True, nullable=False)
    path: Mapped[str] = mapped_column(String(255), nullable=False, default="")
    component: Mapped[str] = mapped_column(String(255), nullable=False, default="")
    title_key: Mapped[str] = mapped_column(String(255), nullable=False)
    icon: Mapped[str] = mapped_column(String(128), nullable=False, default="")
    menu_type: Mapped[str] = mapped_column(String(32), nullable=False, default="menu")
    link: Mapped[str] = mapped_column(String(255), nullable=False, default="")
    is_iframe: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)
    keep_alive: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)
    is_hide: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)
    is_hide_tab: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)
    fixed_tab: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)
    active_path: Mapped[str] = mapped_column(String(255), nullable=False, default="")
    is_full_page: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)
    sort: Mapped[int] = mapped_column(Integer, nullable=False, default=1)
    enabled: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)


class SysPermission(Base):
    __tablename__ = "sys_permission"
    __table_args__ = (
        Index("idx_sys_permission_menu_id", "menu_id"),
        Index("idx_sys_permission_type", "permission_type"),
    )

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True, autoincrement=True)
    permission_code: Mapped[str] = mapped_column(String(128), unique=True, nullable=False)
    permission_name: Mapped[str] = mapped_column(String(128), nullable=False)
    permission_type: Mapped[str] = mapped_column(String(32), nullable=False, default="action")
    menu_id: Mapped[int | None] = mapped_column(BigInteger, ForeignKey("sys_menu.id", ondelete="SET NULL"), nullable=True)
    auth_mark: Mapped[str] = mapped_column(String(128), nullable=False, default="")
    description: Mapped[str] = mapped_column(Text, nullable=False, default="")
    sort: Mapped[int] = mapped_column(Integer, nullable=False, default=1)
    enabled: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)


class SysRoleMenu(Base):
    __tablename__ = "sys_role_menu"
    __table_args__ = (
        UniqueConstraint("role_id", "menu_id", name="uq_sys_role_menu_role_menu"),
        Index("idx_sys_role_menu_role_id", "role_id"),
        Index("idx_sys_role_menu_menu_id", "menu_id"),
    )

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True, autoincrement=True)
    role_id: Mapped[int] = mapped_column(BigInteger, ForeignKey("sys_role.id", ondelete="CASCADE"), nullable=False)
    menu_id: Mapped[int] = mapped_column(BigInteger, ForeignKey("sys_menu.id", ondelete="CASCADE"), nullable=False)


class SysRolePermission(Base):
    __tablename__ = "sys_role_permission"
    __table_args__ = (
        UniqueConstraint("role_id", "permission_id", name="uq_sys_role_permission_role_permission"),
        Index("idx_sys_role_permission_role_id", "role_id"),
        Index("idx_sys_role_permission_permission_id", "permission_id"),
    )

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True, autoincrement=True)
    role_id: Mapped[int] = mapped_column(BigInteger, ForeignKey("sys_role.id", ondelete="CASCADE"), nullable=False)
    permission_id: Mapped[int] = mapped_column(
        BigInteger,
        ForeignKey("sys_permission.id", ondelete="CASCADE"),
        nullable=False,
    )


class SysDataPermission(Base):
    __tablename__ = "sys_data_permission"
    __table_args__ = (
        Index("idx_sys_data_permission_role_id", "role_id"),
        Index("idx_sys_data_permission_scope", "scope_type", "scope_value"),
    )

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True, autoincrement=True)
    role_id: Mapped[int] = mapped_column(BigInteger, ForeignKey("sys_role.id", ondelete="CASCADE"), nullable=False)
    scope_type: Mapped[str] = mapped_column(String(32), nullable=False, default="all")
    scope_value: Mapped[str] = mapped_column(String(128), nullable=False, default="*")
    enabled: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)

