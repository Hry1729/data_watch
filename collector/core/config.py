"""配置管理模块"""

from dataclasses import dataclass, field
from typing import List, Optional
from urllib.parse import quote_plus


@dataclass
class MySQLConfig:
    """后台配置数据库连接信息"""
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

    def to_sqlalchemy_url(self, async_mode: bool = True) -> str:
        """构造 SQLAlchemy DSN（密码安全转义）。"""
        driver = "mysql+aiomysql" if async_mode else "mysql+pymysql"
        user_enc = quote_plus(self.user)
        password_enc = quote_plus(self.password)
        return (
            f"{driver}://{user_enc}:{password_enc}"
            f"@{self.host}:{self.port}/{self.database}?charset={self.charset}"
        )


@dataclass
class InterfaceIngestConfig:
    """外部接口采集协议配置（HTTP Push）"""
    enabled: bool = False
    host: str = "127.0.0.1"  # 默认只监听本地，生产环境需配置 API Key
    port: int = 8090
    path: str = "/ingest"
    api_key: Optional[str] = None
    max_batch_size: int = 1000
    max_body_bytes: int = 1_048_576  # 请求体字节上限，默认 1MB


@dataclass
class DeviceConfig:
    """单个设备配置"""
    device_id: str
    device_type: str  # QC, RTG, RMG, AGV, etc.
    protocol: str  # s7, ge_srtp, opcda_center, opcda_sidecar
    ip: str
    endpoint: str = ""
    port: int = 102
    rack: int = 0
    slot: int = 1
    scan_rate: float = 1.0  # seconds
    device_pk: Optional[int] = None  # MySQL device.id
    opc_server: Optional[str] = None  # OPC DA ProgID，从 comm_protocol.server 获取
    config_version: int = 1  # device.variable_config_version，用于 Redis Stream entry


@dataclass
class VariableConfig:
    """变量配置"""
    variable_id: str
    device_id: str
    address: str  # e.g., "DB398.DBD0", "%MD1304", "%R100"
    data_type: str = "INT"  # INT, FLOAT, BOOL, etc.
    scan_rate: float = 1.0  # seconds
    is_storage: bool = True  # whether to store in TDengine
    var_seq: int = 0  # canonical TDengine point sequence
    group_code: str = "g001"  # 所属点位分组编码，来自 device_point_group.group_code


@dataclass
class RedisConfig:
    """Redis 连接与 Stream 配置"""
    host: str = "127.0.0.1"
    port: int = 6379
    db: int = 0
    password: str = ""
    stream_shard_count: int = 4
    stream_maxlen: int = 500000


@dataclass
class CollectorConfig:
    """采集模块总配置"""
    mysql: MySQLConfig = field(default_factory=MySQLConfig)
    interface_ingest: InterfaceIngestConfig = field(default_factory=InterfaceIngestConfig)
    redis: RedisConfig = field(default_factory=RedisConfig)

    # Devices
    devices: List[DeviceConfig] = field(default_factory=list)
    variables: List[VariableConfig] = field(default_factory=list)

    # 当 MySQL 不可用时，是否允许回退到临时假数据拓扑（仅联调验证）
    enable_fake_bootstrap: bool = False
    # 运行态标记：当前是否正在使用假数据模式
    fake_mode_active: bool = False

    # Logging
    log_level: str = "INFO"
    log_file: Optional[str] = None  # 日志文件路径，如 "logs/collector.log"，为空则只输出控制台
    log_max_bytes: int = 10 * 1024 * 1024  # 单个日志文件最大 10MB
    log_backup_count: int = 5  # 保留 5 个历史日志文件
