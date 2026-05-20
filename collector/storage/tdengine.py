"""TDengine连接管理"""

import asyncio
import re
from dataclasses import dataclass
from typing import Any, Dict, List, Optional
from urllib.parse import quote_plus

from core.config import TDengineConfig
from core.exceptions import StorageError
from core.logger import get_logger

logger = get_logger("tdengine")


@dataclass(frozen=True)
class VariableStorageTarget:
    """单个变量在 TDengine 中的落库目标。"""

    variable_id: str
    device_pk: int
    device_code: str
    device_type: str
    var_seq: int
    database_name: str
    stable_name: str
    table_name: str


class TDengineConnection:
    """
    TDengine 连接管理器

    统一通过 WebSocket 连接（taos-ws-py / taosws），经 taosAdapter 访问数据库，
    与 TDengine 官方对 Python 场景的推荐一致；不再使用原生 libtaos 客户端连接。
    """

    def __init__(self, config: TDengineConfig):
        self.config = config
        self._conn = None
        self._connected = False
        self._lock = asyncio.Lock()
        self._execute_lock = asyncio.Lock()  # 保护并发执行
        self._reconnect_attempts = 0
        self._max_reconnect_attempts = 5
        self._reconnect_delay = 5  # 秒
        self._identifier_pattern = re.compile(r"[^A-Za-z0-9_]")
        self._variable_targets: Dict[str, VariableStorageTarget] = {}

    async def connect(self) -> None:
        """建立 TDengine WebSocket 连接"""
        async with self._lock:
            if self._connected:
                return

            try:
                await self._connect_websocket()
                self._connected = True
                self._reconnect_attempts = 0
                scheme = "wss" if self.config.use_ssl else "ws"
                logger.info(
                    "TDengine 已通过 %s 连接 %s:%s (multi-db mode)",
                    scheme.upper(),
                    self.config.host,
                    self.config.port,
                )
            except Exception as e:
                self._connected = False
                raise StorageError(f"TDengine connect failed: {e}") from e

    async def _connect_websocket(self) -> None:
        """通过 WebSocket（ws / wss）连接"""
        from taosws import connect

        loop = asyncio.get_event_loop()
        dsn = self._build_dsn(database=None)
        self._conn = await asyncio.wait_for(
            loop.run_in_executor(None, lambda: connect(dsn)),
            timeout=self.config.connect_timeout_seconds,
        )

    def _build_dsn(self, database: Optional[str]) -> str:
        scheme = "wss" if self.config.use_ssl else "ws"
        query_params = []
        if self.config.timezone:
            query_params.append(f"timezone={self.config.timezone}")
        query_params.append("conn_retries=1")
        query_params.append("retry_backoff_ms=100")
        query_params.append("retry_backoff_max_ms=500")

        # URL encode 账号密码，避免特殊字符导致 DSN 解析失败
        user_encoded = quote_plus(self.config.user)
        password_encoded = quote_plus(self.config.password)

        db_path = f"/{database}" if database else ""
        return (
            f"{scheme}://{user_encoded}:{password_encoded}"
            f"@{self.config.host}:{self.config.port}{db_path}"
            f"?{'&'.join(query_params)}"
        )

    def normalize_identifier(self, raw_name: str, prefix: str) -> str:
        """将外部名称规范为 TDengine 标识符，避免非法字符导致建表失败。"""
        cleaned = self._identifier_pattern.sub("_", str(raw_name).strip())
        if not cleaned:
            cleaned = f"{prefix}_unknown"
        if cleaned[0].isdigit():
            cleaned = f"{prefix}_{cleaned}"
        return cleaned

    def get_database_name(self, device_type: str) -> str:
        """根据设备类型生成数据库名。"""
        raw = (device_type or "").strip().lower()
        db_name = self.normalize_identifier(raw, prefix="db")
        return db_name.lower()

    def get_stable_name(self, device_code: str) -> str:
        """根据设备编码生成超级表名。"""
        return self.normalize_identifier(str(device_code).lower(), prefix="dev")

    def get_subtable_name(self, device_code: str, var_seq: int) -> str:
        """根据设备编码和点位序号生成子表名。"""
        return self.normalize_identifier(f"{device_code}_{var_seq}".lower(), prefix="var")

    def get_storage_target(self, variable_id: str) -> VariableStorageTarget:
        """根据变量主键获取 TDengine 落库目标。"""
        try:
            return self._variable_targets[str(variable_id)]
        except KeyError as exc:
            raise StorageError(f"TDengine target not initialized for variable_id={variable_id}") from exc

    async def init_storage_topology(self, devices: List, variables: List) -> None:
        """
        初始化"设备类型数据库 -> 设备超级表 -> 变量子表"。
        幂等操作：已存在的数据库、超级表、子表不会重复创建。
        """
        device_map = {getattr(device, "device_id", ""): device for device in devices}
        created_dbs = set()
        created_stables = set()
        created_tables = set()
        targets: Dict[str, VariableStorageTarget] = {}
        failures: List[str] = []
        device_table_stats: Dict[str, Dict[str, Any]] = {}

        for variable in variables:
            variable_id = str(getattr(variable, "variable_id", "")).strip()
            device_code = getattr(variable, "device_id", "")
            device = device_map.get(device_code)
            if not variable_id or device is None:
                message = f"Skip TDengine topology init for variable_id={variable_id} device={device_code}"
                logger.warning(message)
                failures.append(message)
                continue

            var_seq = int(getattr(variable, "var_seq", 0) or 0)
            if var_seq <= 0:
                message = f"Skip TDengine topology init for variable_id={variable_id} because var_seq is invalid"
                logger.warning(message)
                failures.append(message)
                continue
            db_name = self.get_database_name(getattr(device, "device_type", "UNKNOWN"))
            stable_name = self.get_stable_name(device_code)
            table_name = self.get_subtable_name(device_code, var_seq)
            device_pk = self._to_bigint_tag(getattr(device, "device_pk", None), f"device {device_code}")
            stats = device_table_stats.setdefault(
                device_code,
                {
                    "db_name": db_name,
                    "stable_name": stable_name,
                    "variables": 0,
                    "created_stables": 0,
                    "existing_stables": 0,
                    "created_tables": 0,
                    "existing_tables": 0,
                },
            )
            stats["variables"] += 1

            target = VariableStorageTarget(
                variable_id=variable_id,
                device_pk=device_pk,
                device_code=device_code,
                device_type=getattr(device, "device_type", "UNKNOWN"),
                var_seq=var_seq,
                database_name=db_name,
                stable_name=stable_name,
                table_name=table_name,
            )

            try:
                # 1. 创建数据库（幂等）
                if db_name not in created_dbs:
                    await self.execute(
                        f"CREATE DATABASE IF NOT EXISTS `{db_name}` "
                        f"KEEP {self.config.db_keep} "
                        f"DURATION {self.config.db_duration} "
                        f"BUFFER {self.config.db_buffer} "
                        f"PAGES {self.config.db_pages}"
                    )
                    created_dbs.add(db_name)

                # 2. 检查并创建超级表（幂等）
                stable_key = (db_name, stable_name)
                if stable_key not in created_stables:
                    stable_exists = await self._stable_exists(db_name, stable_name)
                    if not stable_exists:
                        await self.execute(
                            f"CREATE STABLE IF NOT EXISTS `{db_name}`.`{stable_name}` "
                            f"(`ts` TIMESTAMP, `value` DOUBLE, `quality` TINYINT) "
                            f"TAGS (`var_seq` INT)"
                        )
                        logger.info("创建超级表: %s.%s", db_name, stable_name)
                        stats["created_stables"] += 1
                    else:
                        stats["existing_stables"] += 1
                    created_stables.add(stable_key)

                # 3. 检查并创建子表（幂等）
                table_key = (db_name, table_name)
                if table_key not in created_tables:
                    table_exists = await self._table_exists(db_name, table_name)
                    if not table_exists:
                        await self.execute(
                            f"CREATE TABLE IF NOT EXISTS `{db_name}`.`{table_name}` "
                            f"USING `{db_name}`.`{stable_name}` TAGS ({var_seq})"
                        )
                        logger.info("创建子表: %s.%s", db_name, table_name)
                        stats["created_tables"] += 1
                    else:
                        stats["existing_tables"] += 1
                    created_tables.add(table_key)

                targets[variable_id] = target
                logger.debug(
                    "TDengine 变量子表已就绪: db=%s stable=%s table=%s variable_id=%s",
                    db_name,
                    stable_name,
                    table_name,
                    variable_id,
                )
            except Exception as e:
                message = f"创建子表 {table_name} 失败: {e}"
                logger.warning(message)
                failures.append(message)

        self._variable_targets = targets
        for device_code, stats in device_table_stats.items():
            logger.info(
                "TDengine 表初始化完成: device=%s, db=%s, stable=%s, variables=%s, "
                "created_stables=%s, created_tables=%s, existing_tables=%s",
                device_code,
                stats["db_name"],
                stats["stable_name"],
                stats["variables"],
                stats["created_stables"],
                stats["created_tables"],
                stats["existing_tables"],
            )
        if failures:
            raise StorageError(
                f"TDengine topology initialization failed for {len(failures)} variable(s): "
                f"{'; '.join(failures[:5])}"
            )

    async def _stable_has_correct_schema(self, db_name: str, stable_name: str) -> bool:
        """
        检查超级表是否为当前模型（ts/value/quality + var_seq TAG）

        TDengine DESCRIBE 返回字段：
        - field: 列名 (ts, value, var_seq)
        - type: 类型 (TIMESTAMP, DOUBLE, INT)
        - length: 长度
        - note: 备注，TAG 列此处为 "TAG"
        """
        try:
            rows = await self._fetch_all(f"DESCRIBE `{db_name}`.`{stable_name}`")
            if not rows:
                return False

            # 查找数据列与 TAG 列，检查是否有 quality 与 var_seq
            column_names = set()
            tag_names = set()
            for row in rows:
                # TDengine DESCRIBE 字段：field, type, length, note
                col_name = str(row.get("field", "")).lower()
                note = str(row.get("note", "")).upper()
                if note != "TAG":
                    column_names.add(col_name)
                # TAG 列的标识：note 字段为 "TAG"
                if note == "TAG":
                    tag_names.add(col_name)

            return {"ts", "value", "quality"}.issubset(column_names) and "var_seq" in tag_names
        except Exception as e:
            logger.warning("检查超级表 schema 失败: %s.%s, error: %s", db_name, stable_name, e)
            return False

    async def _stable_exists(self, db_name: str, stable_name: str) -> bool:
        """
        检查超级表是否存在且模型兼容

        兼容性检查：
        1. 超级表存在
        2. 数据列包含 quality，TAG 列包含 var_seq（当前模型）

        如果存在同名但 schema 不兼容的超级表，抛出异常提示手动迁移。
        """
        try:
            await self.execute(f"USE `{db_name}`")
            rows = await self._fetch_all(f"SHOW STABLES LIKE '{stable_name}'")
            if not rows:
                return False

            # 检查 schema 是否兼容
            if not await self._stable_has_correct_schema(db_name, stable_name):
                raise StorageError(
                    f"超级表 `{db_name}`.`{stable_name}` 已存在但结构不兼容。"
                    f"当前模型需要数据列 'quality' 和 TAG 'var_seq'。请手动迁移或删除后重建。"
                )
            return True
        except StorageError:
            raise
        except Exception as e:
            logger.debug("检查超级表存在性失败: %s.%s, error: %s", db_name, stable_name, e)
            return False

    async def _table_exists(self, db_name: str, table_name: str) -> bool:
        """检查子表是否存在"""
        try:
            await self.execute(f"USE `{db_name}`")
            rows = await self._fetch_all(f"SHOW TABLES LIKE '{table_name}'")
            return bool(rows)
        except Exception:
            return False

    async def _fetch_all(self, sql: str) -> List[Dict[str, Any]]:
        """执行查询并返回所有结果"""
        if not self._conn:
            raise StorageError("Not connected to TDengine")

        loop = asyncio.get_event_loop()
        try:
            result = await asyncio.wait_for(
                loop.run_in_executor(None, lambda: self._conn.query(sql)),
                timeout=getattr(self.config, "timeout", self.config.connect_timeout_seconds),
            )
            logger.debug("_fetch_all SQL=%s, result_type=%s", sql, type(result).__name__)
            if not result:
                logger.debug("_fetch_all: result is empty")
                return []

            # 尝试获取 fields
            fields = None
            try:
                if hasattr(result, "fields"):
                    fields_attr = result.fields
                    # fields 可能是方法或属性
                    fields = fields_attr() if callable(fields_attr) else fields_attr
            except Exception as fe:
                logger.debug("_fetch_all: fields() call failed: %s", fe)
                fields = None

            if fields:
                # fields 是 TaosField 列表，每个有 name 方法（不是属性）
                field_names = []
                for field in fields:
                    try:
                        name_attr = getattr(field, "name", None)
                        # name 可能是方法或属性，需要判断并调用
                        if callable(name_attr):
                            name = name_attr()
                        elif name_attr:
                            name = name_attr
                        else:
                            name = str(field)
                        field_names.append(name)
                    except Exception:
                        field_names.append(str(field))
                rows = []
                for row in result:
                    # row 是元组，按字段顺序排列
                    rows.append(dict(zip(field_names, row)))
                return rows

            # 回退：尝试 fetch_all_into_dict
            if hasattr(result, "fetch_all_into_dict"):
                try:
                    rows = result.fetch_all_into_dict()
                    return list(rows or [])
                except Exception:
                    pass

            if hasattr(result, "__iter__"):
                return [{"row": row} for row in result]

            return []
        except Exception as e:
            logger.warning("_fetch_all 查询失败: %s, SQL: %s", e, sql)
            return []

    def _to_bigint_tag(self, raw_value, label: str) -> int:
        """将主数据主键转换为 BIGINT tag。"""
        try:
            return int(raw_value)
        except (TypeError, ValueError) as exc:
            raise StorageError(f"Invalid BIGINT tag value for {label}: {raw_value}") from exc

    async def execute(self, sql: str) -> int:
        """
        执行SQL语句

        自动重连机制：
        1. 检查连接状态，断开时尝试重连
        2. 使用异步锁保护并发执行，避免多任务复用同一连接时的竞态
        3. 执行失败时标记连接失效，下次执行会触发重连
        """
        async with self._execute_lock:
            # 确保连接有效，断开时尝试重连
            await self.ensure_connected()

            if not self._conn:
                raise StorageError("Not connected to TDengine")

            loop = asyncio.get_event_loop()
            try:
                affected = await asyncio.wait_for(
                    loop.run_in_executor(None, lambda: self._conn.execute(sql)),
                    timeout=self.config.connect_timeout_seconds,
                )
                return affected if affected else 0
            except Exception as e:
                # 连接可能已断开，标记状态
                self._connected = False
                logger.error("TDengine execute failed: %s, error: %s", sql, e)
                raise StorageError(f"SQL execute failed: {e}") from e

    async def close(self) -> None:
        """关闭连接"""
        if self._conn:
            try:
                loop = asyncio.get_event_loop()
                await loop.run_in_executor(None, self._conn.close)
            except Exception as e:
                logger.warning("TDengine close error: %s", e)
            finally:
                self._connected = False
                self._conn = None
                logger.info("TDengine disconnected")

    async def ensure_connected(self):
        """
        确保连接有效

        检查策略：
        1. 未连接时直接连接
        2. 已连接但曾经有重连记录时，ping 检查有效性
        3. ping 失败时尝试重连
        """
        if not self._connected or not self._conn:
            await self.connect()
            return

        # 有过重连记录或距离上次成功超过一定时间时，主动 ping 检查
        if self._reconnect_attempts > 0:
            try:
                ok = await self.ping()
                if not ok:
                    logger.warning("TDengine ping failed, attempting reconnect...")
                    await self.reconnect()
            except Exception as e:
                logger.warning("TDengine ping error: %s, attempting reconnect...", e)
                await self.reconnect()

    async def ping(self) -> bool:
        """检查连接是否有效"""
        if not self._conn:
            return False

        try:
            loop = asyncio.get_event_loop()
            await loop.run_in_executor(None, self._conn.query, "SELECT 1")
            return True
        except Exception:
            return False

    async def reconnect(self):
        """重新连接"""
        if self._reconnect_attempts >= self._max_reconnect_attempts:
            logger.error("Max reconnection attempts (%s) reached", self._max_reconnect_attempts)
            return False

        self._reconnect_attempts += 1
        wait_time = self._reconnect_delay * (2 ** (self._reconnect_attempts - 1))

        logger.info("Reconnection attempt %s, waiting %ss...", self._reconnect_attempts, wait_time)
        await asyncio.sleep(wait_time)

        try:
            await self.close()
            await self.connect()
            return True
        except Exception as e:
            logger.error("Reconnection failed: %s", e)
            return False

    @property
    def is_connected(self) -> bool:
        return self._connected

    @property
    def connection(self) -> Any:
        """返回底层连接对象"""
        return self._conn
