"""数据库配置加载模块（从 MySQL 读取设备和点位配置）。"""

from __future__ import annotations

import asyncio
import socket
import threading
from dataclasses import dataclass
from datetime import datetime
from typing import Any, Callable, Dict, List, Optional, Tuple
from contextlib import asynccontextmanager
from urllib.parse import urlparse

from sqlalchemy import JSON, Text, select, update
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine
from sqlalchemy.orm import Mapped, declarative_base, mapped_column

from core.config import CollectorConfig, DeviceConfig, MySQLConfig, VariableConfig
from core.exceptions import ConfigError
from core.logger import get_logger

logger = get_logger("config_db")

Base = declarative_base()


class DBCommProtocol(Base):
    __tablename__ = "comm_protocol"

    id: Mapped[int] = mapped_column(primary_key=True)
    protocol: Mapped[str] = mapped_column()
    port: Mapped[Optional[int]] = mapped_column(nullable=True)
    server: Mapped[Optional[str]] = mapped_column(nullable=True)  # OPC DA ProgID 等


class DBDevice(Base):
    __tablename__ = "device"

    id: Mapped[int] = mapped_column(primary_key=True)
    device_code: Mapped[str] = mapped_column()
    device_name: Mapped[Optional[str]] = mapped_column(nullable=True)
    enable: Mapped[Optional[int]] = mapped_column(nullable=True)
    status: Mapped[int] = mapped_column()
    protocol_id: Mapped[int] = mapped_column()
    type_id: Mapped[Optional[int]] = mapped_column(nullable=True)
    endpoint: Mapped[str] = mapped_column()
    rack: Mapped[int] = mapped_column()
    slot: Mapped[int] = mapped_column()
    scan_cycle: Mapped[int] = mapped_column(default=1000)
    update_time: Mapped[Optional[datetime]] = mapped_column(nullable=True)
    variable_config_version: Mapped[Optional[int]] = mapped_column(nullable=True)


class DBDeviceVariable(Base):
    __tablename__ = "device_variable"

    id: Mapped[int] = mapped_column(primary_key=True)
    device_id: Mapped[int] = mapped_column()
    point_group_id: Mapped[Optional[int]] = mapped_column(nullable=True)
    var_seq: Mapped[int] = mapped_column()
    var_name: Mapped[Optional[str]] = mapped_column(nullable=True)
    io_type: Mapped[Optional[str]] = mapped_column(nullable=True)
    var_type: Mapped[str] = mapped_column()
    var_address: Mapped[str] = mapped_column()
    scan_rate: Mapped[Optional[int]] = mapped_column(nullable=True)
    is_alarm: Mapped[Optional[int]] = mapped_column(nullable=True)
    is_storage: Mapped[int] = mapped_column()
    enable: Mapped[Optional[int]] = mapped_column(nullable=True)
    deleted: Mapped[Optional[int]] = mapped_column(nullable=True)


class DBDevicePointGroup(Base):
    __tablename__ = "device_point_group"

    id: Mapped[int] = mapped_column(primary_key=True)
    device_id: Mapped[int] = mapped_column()
    group_code: Mapped[str] = mapped_column()


class DBDeviceType(Base):
    __tablename__ = "device_type"

    id: Mapped[int] = mapped_column(primary_key=True)
    type_code: Mapped[Optional[str]] = mapped_column(nullable=True)
    type_name: Mapped[Optional[str]] = mapped_column(nullable=True)


class DBPLCConnectionLog(Base):
    __tablename__ = "plc_connection_log"

    id: Mapped[int] = mapped_column(primary_key=True)
    device_id: Mapped[Optional[int]] = mapped_column(nullable=True)
    event_type: Mapped[Optional[str]] = mapped_column(nullable=True)
    status: Mapped[Optional[int]] = mapped_column(nullable=True)
    # 注意：数据库字段名为 messsage（有拼写错误），此处映射保持一致
    # 待数据库迁移后可改为 message
    messsage: Mapped[Optional[str]] = mapped_column(nullable=True)
    create_time: Mapped[Optional[datetime]] = mapped_column(nullable=True)


class DBSystemLog(Base):
    __tablename__ = "system_log"

    id: Mapped[int] = mapped_column(primary_key=True)
    log_time: Mapped[datetime] = mapped_column()
    level: Mapped[str] = mapped_column()
    log_type: Mapped[str] = mapped_column()
    source: Mapped[str] = mapped_column()
    device_id: Mapped[Optional[int]] = mapped_column(nullable=True)
    device_code: Mapped[str] = mapped_column(default="")
    point_id: Mapped[Optional[int]] = mapped_column(nullable=True)
    host_name: Mapped[str] = mapped_column(default="")
    service_name: Mapped[str] = mapped_column(default="")
    content: Mapped[str] = mapped_column()
    detail_json: Mapped[Optional[Dict[str, Any]]] = mapped_column(JSON, nullable=True)
    trace_id: Mapped[str] = mapped_column(default="")
    exception_stack: Mapped[Optional[str]] = mapped_column(Text, nullable=True)
    elapsed_ms: Mapped[Optional[int]] = mapped_column(nullable=True)
    create_time: Mapped[datetime] = mapped_column(default=datetime.now)


class DBLogRetentionPolicy(Base):
    __tablename__ = "log_retention_policy"

    id: Mapped[int] = mapped_column(primary_key=True)
    debug_db_enabled: Mapped[int] = mapped_column(default=0)
    debug_enabled_until: Mapped[Optional[datetime]] = mapped_column(nullable=True)


@dataclass
class DatabaseConfig:
    """数据库连接配置"""

    url: str = "mysql+aiomysql://root:password@127.0.0.1:3306/device_watch?charset=utf8mb4"
    pool_size: int = 10
    max_overflow: int = 20
    pool_timeout: int = 30
    pool_recycle: int = 3600
    echo: bool = False

    @classmethod
    def from_mysql_config(cls, mysql: MySQLConfig, echo: bool = False) -> "DatabaseConfig":
        return cls(
            url=mysql.to_sqlalchemy_url(async_mode=True),
            pool_size=mysql.pool_size,
            max_overflow=mysql.max_overflow,
            pool_timeout=mysql.pool_timeout,
            pool_recycle=mysql.pool_recycle,
            echo=echo,
        )


class ConfigDBManager:
    """配置数据库管理器。"""

    def __init__(self, db_config: DatabaseConfig):
        self.db_config = db_config
        self.engine = create_async_engine(
            db_config.url,
            pool_size=db_config.pool_size,
            max_overflow=db_config.max_overflow,
            pool_timeout=db_config.pool_timeout,
            pool_recycle=db_config.pool_recycle,
            echo=db_config.echo,
        )
        self.SessionLocal = async_sessionmaker(self.engine, expire_on_commit=False, class_=AsyncSession)

        self._config_cache: Dict[str, Any] = {}
        self._cache_lock = threading.RLock()
        self._watch_task: Optional[asyncio.Task] = None
        self._config_changed_callbacks: List[Callable[[CollectorConfig], None]] = []

    @asynccontextmanager
    async def get_session(self):
        async with self.SessionLocal() as session:
            try:
                yield session
            except Exception:
                await session.rollback()
                raise
            finally:
                await session.close()

    async def close(self) -> None:
        """释放数据库连接池。"""
        await self.engine.dispose()

    @staticmethod
    def _normalize_protocol(protocol_name: Optional[str], comm_type: Optional[str] = None) -> str:
        raw = (protocol_name or comm_type or "").strip().lower()
        if not raw:
            return "s7"
        if raw in {"s7", "snap7", "siemens", "siemens-snap7", "simens-snap7"}:
            return "s7"
        if raw in {"ge_srtp", "gesrtp", "ge-srtp"} or ("ge" in raw and "srtp" in raw):
            return "ge_srtp"
        if raw in {"opc", "opcua", "opc_ua", "opc ua", "opc-ua"}:
            return "opcua"
        if raw in {"http_push", "http push", "http-push", "push"}:
            return "http_push"
        if raw in {"opcda_sidecar", "opc_da_sidecar", "opc da sidecar", "opcda-sidecar",
                   "opc-da-sidecar"}:
            return "opcda_sidecar"
        if raw in {"opcda_center", "opc_da_center", "opc da center", "opcda-center",
                   "opc-da-center"}:
            return "opcda_center"
        if raw in {"opcda", "opc_da", "opc da", "opc-da"}:
            return "opcda"
        return raw

    @staticmethod
    def _normalize_scan_rate(value: Optional[int], default_seconds: float = 1.0) -> float:
        """兼容毫秒/秒混用配置，最终返回秒，允许亚秒采集。"""
        if value is None:
            return max(0.001, float(default_seconds))

        v = float(value)
        if v <= 0:
            return max(0.001, float(default_seconds))
        if v >= 100:
            # 约定 >=100 视为毫秒
            return max(0.001, v / 1000.0)
        return v

    @staticmethod
    def _default_port_for_protocol(protocol: str) -> int:
        p = protocol.lower()
        if p == "ge_srtp":
            return 18245
        if p in {"opc", "opcua", "opc_ua"}:
            return 4840
        return 102

    @staticmethod
    def _parse_endpoint(endpoint: str, default_port: int) -> Tuple[str, int]:
        text = (endpoint or "").strip()
        if not text:
            return "127.0.0.1", default_port

        if "://" in text:
            parsed = urlparse(text)
            host = parsed.hostname or "127.0.0.1"
            port = parsed.port or default_port
            return host, int(port)

        if text.count(":") == 1:
            host, port_text = text.rsplit(":", 1)
            try:
                return host.strip(), int(port_text.strip())
            except ValueError:
                return text, default_port

        return text, default_port

    async def load_devices_and_variables(self) -> Tuple[List[DeviceConfig], List[VariableConfig]]:
        """从 MySQL 的 device/device_variable/comm_protocol/device_type 读取配置。"""
        async with self.get_session() as session:
            device_rows = (
                await session.execute(
                    select(
                        DBDevice.id,
                        DBDevice.device_code,
                        DBDevice.endpoint,
                        DBDevice.rack,
                        DBDevice.slot,
                        DBDevice.scan_cycle,
                        DBDevice.protocol_id,
                        DBDevice.type_id,
                        DBDevice.variable_config_version,
                        DBCommProtocol.protocol,
                        DBCommProtocol.port,
                        DBCommProtocol.server,
                        DBDeviceType.type_code,
                    )
                    .outerjoin(DBCommProtocol, DBDevice.protocol_id == DBCommProtocol.id)
                    .outerjoin(DBDeviceType, DBDevice.type_id == DBDeviceType.id)
                    .where(DBDevice.enable == 1)
                    .order_by(DBDevice.device_code)
                )
            ).all()

            if not device_rows:
                logger.warning(
                    "No enabled devices found in MySQL table `device` (enable=1); collector will start in idle mode."
                )
                return [], []

            devices: List[DeviceConfig] = []
            by_device_pk: Dict[int, DeviceConfig] = {}

            for row in device_rows:
                protocol = self._normalize_protocol(row.protocol)
                default_port = int(row.port or self._default_port_for_protocol(protocol))
                ip, port = self._parse_endpoint(row.endpoint, default_port)
                scan_rate = self._normalize_scan_rate(row.scan_cycle, default_seconds=1)
                device_type = (row.type_code or "UNKNOWN").strip() if row.type_code else "UNKNOWN"

                device_cfg = DeviceConfig(
                    device_id=row.device_code,
                    device_type=device_type,
                    protocol=protocol,
                    ip=ip,
                    endpoint=row.endpoint or "",
                    port=port,
                    rack=int(row.rack or 0),
                    slot=int(row.slot or 1),
                    scan_rate=scan_rate,
                    device_pk=int(row.id),
                    opc_server=row.server,
                    config_version=int(row.variable_config_version or 1),
                )
                devices.append(device_cfg)
                by_device_pk[int(row.id)] = device_cfg

            variable_rows = (
                await session.execute(
                    select(
                        DBDeviceVariable.id,
                        DBDeviceVariable.device_id,
                        DBDeviceVariable.var_seq,
                        DBDeviceVariable.var_type,
                        DBDeviceVariable.var_address,
                        DBDeviceVariable.scan_rate,
                        DBDeviceVariable.is_storage,
                        DBDevicePointGroup.group_code,
                        DBDevicePointGroup.config_version,
                    )
                    .outerjoin(
                        DBDevicePointGroup,
                        DBDeviceVariable.point_group_id == DBDevicePointGroup.id,
                    )
                    .where(DBDeviceVariable.enable == 1, DBDeviceVariable.deleted == 0)
                    .order_by(DBDeviceVariable.device_id, DBDeviceVariable.id)
                )
            ).all()

            variables: List[VariableConfig] = []
            for row in variable_rows:
                device_cfg = by_device_pk.get(int(row.device_id))
                if device_cfg is None:
                    continue

                scan_rate = self._normalize_scan_rate(
                    row.scan_rate,
                    default_seconds=device_cfg.scan_rate,
                )
                variable_id = str(row.id)
                var_seq = int(row.var_seq or 0)
                if var_seq <= 0:
                    raise ConfigError(f"Variable {variable_id} missing canonical var_seq")

                variables.append(
                    VariableConfig(
                        variable_id=variable_id,
                        device_id=device_cfg.device_id,
                        address=row.var_address,
                        data_type=(row.var_type or "INT").upper(),
                        scan_rate=scan_rate,
                        is_storage=bool(row.is_storage),
                        var_seq=var_seq,
                        group_code=str(row.group_code or "g001"),
                        group_config_version=int(row.config_version or 1),
                    )
                )

            if not variables:
                logger.warning(
                    "No enabled variables found in MySQL table `device_variable`; collector will start without collection tasks."
                )
                return devices, []

            logger.info("Loaded %s devices and %s variables from MySQL.", len(devices), len(variables))
            return devices, variables

    async def load_config(self, base_config: Optional[CollectorConfig] = None) -> CollectorConfig:
        """在基础配置上注入 MySQL 中的设备和点位。"""
        config = base_config or CollectorConfig()
        devices, variables = await self.load_devices_and_variables()
        config.devices = devices
        config.variables = variables

        with self._cache_lock:
            self._config_cache = {
                "devices_count": len(devices),
                "variables_count": len(variables),
                "last_updated": datetime.now(),
            }
        return config

    def add_config_changed_callback(self, callback: Callable[[CollectorConfig], None]) -> None:
        self._config_changed_callbacks.append(callback)

    def remove_config_changed_callback(self, callback: Callable[[CollectorConfig], None]) -> None:
        if callback in self._config_changed_callbacks:
            self._config_changed_callbacks.remove(callback)

    async def get_device_status(self) -> Dict[str, int]:
        """统计在线/离线设备。"""
        async with self.get_session() as session:
            status_rows = (await session.execute(select(DBDevice.status))).scalars().all()
            online = sum(1 for s in status_rows if s == 1)
            total = len(status_rows)
            return {"online": online, "offline": total - online, "total": total}

    @staticmethod
    def _clip_text(value: Any, max_length: int) -> str:
        text = "" if value is None else str(value)
        if len(text) <= max_length:
            return text
        return text[: max_length - 3] + "..."

    @staticmethod
    def _normalize_system_log_level(level: str) -> str:
        normalized = (level or "INFO").strip().upper()
        if normalized == "WARNING":
            return "WARN"
        if normalized not in {"DEBUG", "INFO", "WARN", "ERROR", "CRITICAL"}:
            return "INFO"
        return normalized

    async def _is_debug_db_enabled(self) -> bool:
        try:
            async with self.get_session() as session:
                policy = (
                    await session.execute(
                        select(DBLogRetentionPolicy).where(DBLogRetentionPolicy.id == 1)
                    )
                ).scalar_one_or_none()
                if policy is None or not int(policy.debug_db_enabled or 0):
                    return False
                if policy.debug_enabled_until is None:
                    return True
                return policy.debug_enabled_until > datetime.now()
        except Exception as exc:
            logger.debug("Failed to read DEBUG log policy: %s", exc)
            return False

    async def write_system_log(
        self,
        *,
        level: str,
        log_type: str,
        source: str,
        content: str,
        detail_json: Optional[Dict[str, Any]] = None,
        device_id: Optional[int] = None,
        device_code: str = "",
        point_id: Optional[int] = None,
        host_name: Optional[str] = None,
        service_name: str = "collector",
        trace_id: str = "",
        exception_stack: Optional[str] = None,
        elapsed_ms: Optional[int] = None,
    ) -> bool:
        """Best-effort write to system_log. Never let logging break collection."""
        normalized_level = self._normalize_system_log_level(level)
        if normalized_level == "DEBUG" and not await self._is_debug_db_enabled():
            return False

        try:
            async with self.get_session() as session:
                session.add(
                    DBSystemLog(
                        log_time=datetime.now(),
                        level=normalized_level,
                        log_type=self._clip_text(log_type or "system", 16),
                        source=self._clip_text(source or "collector", 64),
                        device_id=device_id,
                        device_code=self._clip_text(device_code, 64),
                        point_id=point_id,
                        host_name=self._clip_text(host_name or socket.gethostname(), 64),
                        service_name=self._clip_text(service_name or "collector", 64),
                        content=self._clip_text(content, 500),
                        detail_json=detail_json or None,
                        trace_id=self._clip_text(trace_id, 64),
                        exception_stack=exception_stack,
                        elapsed_ms=elapsed_ms,
                        create_time=datetime.now(),
                    )
                )
                await session.commit()
            return True
        except Exception as exc:
            logger.warning("Failed to write system_log level=%s source=%s: %s", normalized_level, source, exc)
            return False

    @staticmethod
    def _device_status_system_log_level(status: int, event_type: str, message: str) -> str:
        lower_message = (message or "").lower()
        if status == 1:
            return "INFO"
        if event_type == "disconnect" and "failed" not in lower_message:
            return "INFO"
        if event_type == "timeout" or status == 2:
            return "WARN"
        if "failed" in lower_message or "error" in lower_message:
            return "ERROR"
        return "WARN"

    async def update_device_status(
        self,
        device_code: str,
        status: int,
        message: str = "",
        event_type: str = "status_update",
    ) -> None:
        """更新设备状态，并记录连接历史与统一系统日志。"""
        system_log_payload: Optional[Dict[str, Any]] = None
        async with self.get_session() as session:
            device_row = (
                await session.execute(select(DBDevice).where(DBDevice.device_code == device_code))
            ).scalar_one_or_none()
            if device_row is None:
                logger.warning("Device not found when updating status: %s", device_code)
                return

            now = datetime.now()
            await session.execute(
                update(DBDevice)
                .where(DBDevice.id == device_row.id)
                .values(status=status, update_time=now)
            )

            log_row = DBPLCConnectionLog(
                device_id=device_row.id,
                event_type=event_type,
                status=status,
                messsage=message,
                create_time=now,
            )
            session.add(log_row)
            await session.commit()

            level = self._device_status_system_log_level(status, event_type, message)
            system_log_payload = {
                "level": level,
                "log_type": "collect",
                "source": "collector.device",
                "device_id": int(device_row.id),
                "device_code": device_row.device_code,
                "content": f"Device {device_row.device_code} {event_type}: {message or status}",
                "detail_json": {
                    "event_type": event_type,
                    "status": status,
                    "message": message,
                },
            }

        if system_log_payload is not None:
            await self.write_system_log(**system_log_payload)

    async def get_device_config_versions(self) -> Dict[str, int]:
        """返回所有启用设备的 {device_code: variable_config_version}。"""
        async with self.get_session() as session:
            rows = (await session.execute(
                select(DBDevice.device_code, DBDevice.variable_config_version)
                .where(DBDevice.enable == 1)
            )).all()
            return {row.device_code: int(row.variable_config_version or 1) for row in rows}

    async def load_variables_for_device(
        self, device_pk: int, device_cfg: "DeviceConfig"
    ) -> List[VariableConfig]:
        """重新加载单台设备的变量配置。"""
        async with self.get_session() as session:
            rows = (await session.execute(
                select(
                    DBDeviceVariable.id,
                    DBDeviceVariable.var_seq,
                    DBDeviceVariable.var_type,
                    DBDeviceVariable.var_address,
                    DBDeviceVariable.scan_rate,
                    DBDeviceVariable.is_storage,
                    DBDevicePointGroup.group_code,
                )
                .outerjoin(DBDevicePointGroup, DBDeviceVariable.point_group_id == DBDevicePointGroup.id)
                .where(
                    DBDeviceVariable.device_id == device_pk,
                    DBDeviceVariable.enable == 1,
                    DBDeviceVariable.deleted == 0,
                )
                .order_by(DBDeviceVariable.id)
            )).all()
            variables = []
            for row in rows:
                scan_rate = self._normalize_scan_rate(row.scan_rate, device_cfg.scan_rate)
                variables.append(VariableConfig(
                    variable_id=str(row.id),
                    device_id=device_cfg.device_id,
                    address=row.var_address,
                    data_type=(row.var_type or "INT").upper(),
                    scan_rate=scan_rate,
                    is_storage=bool(row.is_storage),
                    var_seq=int(row.var_seq or 0),
                    group_code=str(row.group_code or "g001"),
                ))
            return variables


_config_manager: Optional[ConfigDBManager] = None


def get_config_manager() -> ConfigDBManager:
    global _config_manager
    if _config_manager is None:
        raise ValueError("Config manager not initialized")
    return _config_manager


def init_config_manager(db_config: DatabaseConfig) -> ConfigDBManager:
    global _config_manager
    _config_manager = ConfigDBManager(db_config)
    return _config_manager
