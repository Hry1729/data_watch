"""数据库配置加载模块（从 MySQL 读取设备和点位配置）。"""

from __future__ import annotations

import asyncio
import json
import socket
import threading
from dataclasses import dataclass
from datetime import datetime
from typing import Any, Callable, Dict, List, NamedTuple, Optional, Sequence, Tuple
from urllib.parse import urlparse

import aiomysql

from core.config import CollectorConfig, DeviceConfig, MySQLConfig, VariableConfig
from core.exceptions import ConfigError
from core.logger import get_logger

logger = get_logger("config_db")


class DeviceSignature(NamedTuple):
    """用于运行期热更新检测的设备签名。"""

    config_version: int
    endpoint: str
    protocol_id: int
    rack: int
    slot: int
    scan_cycle: int

    def device_fields_changed(self, other: "DeviceSignature") -> bool:
        return self[1:] != other[1:]


@dataclass
class DatabaseConfig:
    """数据库连接配置"""

    host: str = "127.0.0.1"
    port: int = 3306
    user: str = "root"
    password: str = "password"
    database: str = "device_watch"
    charset: str = "utf8mb4"
    pool_size: int = 10
    max_overflow: int = 20
    pool_timeout: int = 30
    pool_recycle: int = 3600
    echo: bool = False

    @classmethod
    def from_mysql_config(cls, mysql: MySQLConfig, echo: bool = False) -> "DatabaseConfig":
        return cls(
            host=mysql.host,
            port=mysql.port,
            user=mysql.user,
            password=mysql.password,
            database=mysql.database,
            charset=mysql.charset,
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
        self._pool: Optional[aiomysql.Pool] = None
        self._pool_lock = asyncio.Lock()

        self._config_cache: Dict[str, Any] = {}
        self._cache_lock = threading.RLock()
        self._watch_task: Optional[asyncio.Task] = None
        self._config_changed_callbacks: List[Callable[[CollectorConfig], None]] = []
        # B044：最近一次 load_devices_and_variables 实际装载的设备签名（与装载数据同一事务读取），
        # 供热更新循环作为"已应用"基线，而不是把"启动结束时的源版本"当已应用。
        self.loaded_signatures: Optional[Dict[str, DeviceSignature]] = None

    async def _ensure_pool(self) -> aiomysql.Pool:
        pool = self._pool
        if pool is not None:
            return pool

        async with self._pool_lock:
            if self._pool is None:
                self._pool = await aiomysql.create_pool(
                    host=self.db_config.host,
                    port=int(self.db_config.port),
                    user=self.db_config.user,
                    password=self.db_config.password,
                    db=self.db_config.database,
                    charset=self.db_config.charset,
                    minsize=0,
                    maxsize=max(1, int(self.db_config.pool_size or 1) + int(self.db_config.max_overflow or 0)),
                    pool_recycle=int(self.db_config.pool_recycle or -1),
                    connect_timeout=float(self.db_config.pool_timeout or 30),
                    autocommit=True,
                )
            return self._pool

    async def close(self) -> None:
        """释放数据库连接池。"""
        pool = self._pool
        if pool is None:
            return
        self._pool = None
        pool.close()
        await pool.wait_closed()

    async def _fetchall_on_conn(
        self,
        conn: aiomysql.Connection,
        sql: str,
        params: Sequence[Any] = (),
    ) -> List[Dict[str, Any]]:
        async with conn.cursor(aiomysql.DictCursor) as cursor:
            await cursor.execute(sql, tuple(params))
            rows = await cursor.fetchall()
            return list(rows)

    async def _fetchone_on_conn(
        self,
        conn: aiomysql.Connection,
        sql: str,
        params: Sequence[Any] = (),
    ) -> Optional[Dict[str, Any]]:
        async with conn.cursor(aiomysql.DictCursor) as cursor:
            await cursor.execute(sql, tuple(params))
            row = await cursor.fetchone()
            return dict(row) if row is not None else None

    async def _execute_on_conn(
        self,
        conn: aiomysql.Connection,
        sql: str,
        params: Sequence[Any] = (),
    ) -> int:
        async with conn.cursor(aiomysql.DictCursor) as cursor:
            return await cursor.execute(sql, tuple(params))

    async def _fetchall(self, sql: str, params: Sequence[Any] = ()) -> List[Dict[str, Any]]:
        pool = await self._ensure_pool()
        async with pool.acquire() as conn:
            return await self._fetchall_on_conn(conn, sql, params)

    async def _fetchone(self, sql: str, params: Sequence[Any] = ()) -> Optional[Dict[str, Any]]:
        pool = await self._ensure_pool()
        async with pool.acquire() as conn:
            return await self._fetchone_on_conn(conn, sql, params)

    async def _execute(self, sql: str, params: Sequence[Any] = ()) -> int:
        pool = await self._ensure_pool()
        async with pool.acquire() as conn:
            return await self._execute_on_conn(conn, sql, params)

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
    def _normalize_slot(value: Optional[int]) -> int:
        """仅 NULL/None 默认 1；保留 0 等原值，供后续 unit_id 校验拒绝。"""
        if value is None:
            return 1
        return int(value)

    @staticmethod
    def _default_port_for_protocol(protocol: str) -> int:
        p = protocol.lower()
        if p == "ge_srtp":
            return 18245
        if p == "modbus_tcp":
            return 502
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
        pool = await self._ensure_pool()
        async with pool.acquire() as conn:
            await conn.begin()
            try:
                device_rows = await self._fetchall_on_conn(
                    conn,
                    # 查询所有已启用的设备及其协议信息
                    """
                    SELECT
                        d.id AS id,
                        d.device_code AS device_code,
                        d.endpoint AS endpoint,
                        d.rack AS rack,
                        d.slot AS slot,
                        d.scan_cycle AS scan_cycle,
                        d.protocol_id AS protocol_id,
                        d.type_id AS type_id,
                        d.variable_config_version AS variable_config_version,
                        cp.protocol AS protocol,
                        cp.port AS port,
                        cp.server AS server,
                        dt.type_code AS type_code
                    FROM device AS d
                    LEFT JOIN comm_protocol AS cp ON d.protocol_id = cp.id
                    LEFT JOIN device_type AS dt ON d.type_id = dt.id
                    WHERE d.enable = 1
                    ORDER BY d.device_code
                    """,
                )

                self.loaded_signatures = {
                    row["device_code"]: self._signature_from_row(row) for row in device_rows
                }
                if not device_rows:
                    await conn.commit()
                    logger.warning(
                        "No enabled devices found in MySQL table `device` (enable=1); collector will start in idle mode."
                    )
                    return [], []

                variable_rows = await self._fetchall_on_conn(
                    conn,
                    # 查询设备的变量配置信息
                    """
                    SELECT
                        v.id AS id,
                        v.device_id AS device_id,
                        v.var_seq AS var_seq,
                        v.var_type AS var_type,
                        v.var_address AS var_address,
                        v.scan_rate AS scan_rate,
                        v.is_storage AS is_storage,
                        v.td_column_name AS td_column_name,
                        g.group_code AS group_code,
                        g.config_version AS config_version
                    FROM device_variable AS v
                    LEFT JOIN device_point_group AS g ON v.point_group_id = g.id
                    WHERE v.enable = 1 AND v.deleted = 0
                    ORDER BY v.device_id, v.id
                    """,
                )
                await conn.commit()
            except Exception:
                await conn.rollback()
                raise

            devices: List[DeviceConfig] = []
            by_device_pk: Dict[int, DeviceConfig] = {}

            for row in device_rows:
                protocol = self._normalize_protocol(row["protocol"])
                default_port = int(row["port"] or self._default_port_for_protocol(protocol))
                ip, port = self._parse_endpoint(row["endpoint"], default_port)
                scan_rate = self._normalize_scan_rate(row["scan_cycle"], default_seconds=1)
                device_type = (row["type_code"] or "UNKNOWN").strip() if row["type_code"] else "UNKNOWN"

                device_cfg = DeviceConfig(
                    device_id=row["device_code"],
                    device_type=device_type,
                    protocol=protocol,
                    ip=ip,
                    endpoint=row["endpoint"] or "",
                    port=port,
                    rack=int(row["rack"] or 0),
                    slot=self._normalize_slot(row["slot"]),
                    scan_rate=scan_rate,
                    device_pk=int(row["id"]),
                    opc_server=row["server"],
                    config_version=int(row["variable_config_version"] or 1),
                )
                devices.append(device_cfg)
                by_device_pk[int(row["id"])] = device_cfg

            variables: List[VariableConfig] = []
            # C5：is_storage=1 但缺 td_column_name 的点位，writer 侧会静默跳过/丢值
            # （consumer 找不到列名 continue，无响亮日志）。在配置加载这个源头收集，
            # 循环后打一条聚合告警，让缺列配置在采集启动时就可见。
            missing_td_column: List[str] = []
            for row in variable_rows:
                device_cfg = by_device_pk.get(int(row["device_id"]))
                if device_cfg is None:
                    continue

                scan_rate = self._normalize_scan_rate(
                    row["scan_rate"],
                    default_seconds=device_cfg.scan_rate,
                )
                variable_id = str(row["id"])
                var_seq = int(row["var_seq"] or 0)
                if var_seq <= 0:
                    raise ConfigError(f"Variable {variable_id} missing canonical var_seq")

                variables.append(
                    VariableConfig(
                        variable_id=variable_id,
                        device_id=device_cfg.device_id,
                        address=row["var_address"],
                        data_type=(row["var_type"] or "INT").upper(),
                        scan_rate=scan_rate,
                        is_storage=bool(row["is_storage"]),
                        var_seq=var_seq,
                        group_code=str(row["group_code"] or "g001"),
                        group_config_version=int(row["config_version"] or 1),
                    )
                )
                if bool(row["is_storage"]) and not row["td_column_name"]:
                    missing_td_column.append(f"{device_cfg.device_id}/{variable_id}")

            if missing_td_column:
                logger.warning(
                    "%s storage variable(s) missing td_column_name; writer will silently "
                    "drop them (never written to TDengine). Fix device_variable.td_column_name. "
                    "e.g. %s",
                    len(missing_td_column),
                    missing_td_column[:5],
                )

            if not variables:
                logger.warning(
                    "No enabled variables found in MySQL table `device_variable`; collector will start without collection tasks."
                )
                return devices, []

            logger.info("Loaded %s devices and %s variables from MySQL.", len(devices), len(variables))
            return devices, variables

    async def load_config(self, base_config: Optional[CollectorConfig] = None) -> CollectorConfig:
        """在基础配置上注入 MySQL 中的设备和点位，以及 collector_setting 运行参数。"""
        config = base_config or CollectorConfig()
        devices, variables = await self.load_devices_and_variables()
        config.devices = devices
        config.variables = variables
        # app 端可配的运行参数：MySQL > settings.toml > 默认（重启 collector 生效）
        stream_maxlen = await self._load_stream_maxlen()
        if stream_maxlen is not None:
            config.redis.stream_maxlen = stream_maxlen

        with self._cache_lock:
            self._config_cache = {
                "devices_count": len(devices),
                "variables_count": len(variables),
                "last_updated": datetime.now(),
            }
        return config

    async def _load_stream_maxlen(self) -> Optional[int]:
        """读 collector_setting.stream_maxlen（id=1）。表不存在或读取失败返回 None，沿用 settings.toml/默认。"""
        try:
            rows = await self._fetchall(
                "SELECT stream_maxlen AS stream_maxlen FROM collector_setting WHERE id = 1 LIMIT 1"
            )
            if rows:
                return int(rows[0]["stream_maxlen"])
        except Exception as exc:
            logger.debug("Failed to read collector_setting.stream_maxlen: %s", exc)
        return None

    def add_config_changed_callback(self, callback: Callable[[CollectorConfig], None]) -> None:
        self._config_changed_callbacks.append(callback)

    def remove_config_changed_callback(self, callback: Callable[[CollectorConfig], None]) -> None:
        if callback in self._config_changed_callbacks:
            self._config_changed_callbacks.remove(callback)

    async def get_device_status(self) -> Dict[str, int]:
        """统计在线/离线设备。"""
        status_rows = await self._fetchall("SELECT status AS status FROM device")
        online = sum(1 for row in status_rows if row["status"] == 1)
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
            policy = await self._fetchone(
                # 查询日志保留策略配置
                """
                SELECT
                    debug_db_enabled AS debug_db_enabled,
                    debug_enabled_until AS debug_enabled_until
                FROM log_retention_policy
                WHERE id = %s
                LIMIT 1
                """,
                (1,),
            )
            if policy is None or not int(policy["debug_db_enabled"] or 0):
                return False
            if policy["debug_enabled_until"] is None:
                return True
            return policy["debug_enabled_until"] > datetime.now()
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
        """尽力写入 system_log。绝不让日志记录破坏采集。"""
        normalized_level = self._normalize_system_log_level(level)
        if normalized_level == "DEBUG" and not await self._is_debug_db_enabled():
            return False

        try:
            now = datetime.now()
            detail_text = json.dumps(detail_json, ensure_ascii=False) if detail_json else None
            await self._execute(
                # 插入系统日志记录
                """
                INSERT INTO system_log (
                    log_time, level, log_type, source, device_id, device_code, point_id,
                    host_name, service_name, content, detail_json, trace_id,
                    exception_stack, elapsed_ms, create_time
                )
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (
                    now,
                    normalized_level,
                    self._clip_text(log_type or "system", 16),
                    self._clip_text(source or "collector", 64),
                    device_id,
                    self._clip_text(device_code, 64),
                    point_id,
                    self._clip_text(host_name or socket.gethostname(), 64),
                    self._clip_text(service_name or "collector", 64),
                    self._clip_text(content, 500),
                    detail_text,
                    self._clip_text(trace_id, 64),
                    exception_stack,
                    elapsed_ms,
                    now,
                ),
            )
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
        pool = await self._ensure_pool()
        async with pool.acquire() as conn:
            await conn.begin()
            try:
                device_row = await self._fetchone_on_conn(
                    conn,
                    # 根据设备代码查询设备 ID
                    """
                    SELECT id AS id, device_code AS device_code
                    FROM device
                    WHERE device_code = %s
                    LIMIT 1
                    """,
                    (device_code,),
                )
                if device_row is None:
                    await conn.rollback()
                    logger.warning("Device not found when updating status: %s", device_code)
                    return

                now = datetime.now()
                await self._execute_on_conn(
                    conn,
                    "UPDATE device SET status = %s, update_time = %s WHERE id = %s",
                    (status, now, device_row["id"]),
                )
                # 注意：数据库字段名为 messsage（有拼写错误），此处保持一致
                await self._execute_on_conn(
                    conn,
                    # 插入 PLC 连接日志
                    """
                    INSERT INTO plc_connection_log (device_id, event_type, status, messsage, create_time)
                    VALUES (%s, %s, %s, %s, %s)
                    """,
                    (device_row["id"], event_type, status, message, now),
                )
                await conn.commit()
            except Exception:
                await conn.rollback()
                raise

            level = self._device_status_system_log_level(status, event_type, message)
            system_log_payload = {
                "level": level,
                "log_type": "collect",
                "source": "collector.device",
                "device_id": int(device_row["id"]),
                "device_code": device_row["device_code"],
                "content": f"Device {device_row['device_code']} {event_type}: {message or status}",
                "detail_json": {
                    "event_type": event_type,
                    "status": status,
                    "message": message,
                },
            }

        if system_log_payload is not None:
            await self.write_system_log(**system_log_payload)

    async def get_device_config_versions(self) -> Dict[str, DeviceSignature]:
        """返回所有启用设备的 {device_code: DeviceSignature}。"""
        rows = await self._fetchall(
            # 查询已启用的设备信息
            """
            SELECT
                device_code AS device_code,
                variable_config_version AS variable_config_version,
                endpoint AS endpoint,
                protocol_id AS protocol_id,
                rack AS rack,
                slot AS slot,
                scan_cycle AS scan_cycle
            FROM device
            WHERE enable = 1
            """
        )
        return {
            row["device_code"]: ConfigDBManager._signature_from_row(row)
            for row in rows
        }

    @staticmethod
    def _signature_from_row(row: Dict[str, Any]) -> DeviceSignature:
        return DeviceSignature(
            config_version=int(row["variable_config_version"] or 1),
            endpoint=str(row["endpoint"] or ""),
            protocol_id=int(row["protocol_id"] or 0),
            rack=int(row["rack"] or 0),
            slot=ConfigDBManager._normalize_slot(row["slot"]),
            scan_cycle=int(row["scan_cycle"] or 1000),
        )

    async def load_variables_for_device(
        self, device_pk: int, device_cfg: "DeviceConfig"
    ) -> List[VariableConfig]:
        """重新加载单台设备的变量配置。"""
        rows = await self._fetchall(
            # 查询设备的变量配置
            """
            SELECT
                v.id AS id,
                v.var_seq AS var_seq,
                v.var_type AS var_type,
                v.var_address AS var_address,
                v.scan_rate AS scan_rate,
                v.is_storage AS is_storage,
                g.group_code AS group_code
            FROM device_variable AS v
            LEFT JOIN device_point_group AS g ON v.point_group_id = g.id
            WHERE v.device_id = %s AND v.enable = 1 AND v.deleted = 0
            ORDER BY v.id
            """,
            (device_pk,),
        )
        variables = []
        for row in rows:
            scan_rate = self._normalize_scan_rate(row["scan_rate"], device_cfg.scan_rate)
            variables.append(VariableConfig(
                variable_id=str(row["id"]),
                device_id=device_cfg.device_id,
                address=row["var_address"],
                data_type=(row["var_type"] or "INT").upper(),
                scan_rate=scan_rate,
                is_storage=bool(row["is_storage"]),
                var_seq=int(row["var_seq"] or 0),
                group_code=str(row["group_code"] or "g001"),
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
