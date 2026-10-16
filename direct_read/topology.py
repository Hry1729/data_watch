"""解析设备与点位：MySQL 只读优先，不可用时才用本地快照或显式参数。"""

from __future__ import annotations

import json
import os
import re
from dataclasses import dataclass, replace
from pathlib import Path
from typing import Any
from urllib.parse import urlparse

from cli.errors import CliError
from cli.resolvers import Resolver, device_lookup_key

from direct_read.audit import scrub_text
from direct_read.errors import DirectReadError, TopologyUnavailable
from direct_read.quality import QUALITY_CONFIG_DECODE_ERROR, QUALITY_NOT_CONNECTED


# 与 collector/core/config_db.py 的 S7 别名对齐，并覆盖 driver.toml 的 aliases。
_S7_PROTOCOLS = frozenset(
    {
        "s7",
        "snap7",
        "siemens",
        "siemens_s7",
        "siemens_snap7",
        "siemens-snap7",
        "simens-snap7",
    }
)

DEVICE_SQL = """
SELECT
    d.id AS id,
    d.device_code AS device_code,
    d.device_name AS device_name,
    d.endpoint AS endpoint,
    d.rack AS rack,
    d.slot AS slot,
    cp.protocol AS protocol,
    cp.port AS port
FROM device AS d
LEFT JOIN comm_protocol AS cp ON d.protocol_id = cp.id
WHERE d.enable = 1
ORDER BY d.device_code
""".strip()

POINT_SQL = """
SELECT
    v.id AS id,
    v.var_seq AS var_seq,
    v.var_name AS var_name,
    v.var_type AS var_type,
    v.var_address AS var_address
FROM device_variable AS v
WHERE v.device_id = %s AND v.enable = 1 AND v.deleted = 0
ORDER BY v.var_seq
""".strip()

_SESSION_READ_ONLY_SQL = "SET SESSION TRANSACTION READ ONLY"
_SHOW_GRANTS_SQL = "SHOW GRANTS FOR CURRENT_USER()"
_GRANT_BODY = re.compile(r"^GRANT\s+(.*?)\s+ON\s+", re.IGNORECASE | re.DOTALL)
_WRITE_PRIVILEGE = re.compile(
    r"\b(?:INSERT|UPDATE|DELETE|CREATE|DROP|ALTER|INDEX|EXECUTE|TRIGGER|EVENT|"
    r"REFERENCES|SUPER|FILE|SHUTDOWN|RELOAD|LOCK\s+TABLES|GRANT\s+OPTION|"
    r"ALL\s+PRIVILEGES|ALL)\b",
    re.IGNORECASE,
)
_MODES = frozenset({"auto", "mysql", "snapshot", "explicit"})


@dataclass
class MysqlSettings:
    host: str
    port: int
    user: str
    database: str
    password: str

    def __repr__(self) -> str:
        return (
            f"MysqlSettings(host={self.host!r}, port={self.port}, "
            f"user={self.user!r}, database={self.database!r})"
        )


@dataclass(frozen=True)
class ResolvedTarget:
    device_code: str
    device_name: str
    device_id: int | None
    protocol: str
    host: str
    endpoint: str
    port: int
    rack: int
    slot: int
    point_name: str
    io_address: str
    data_type: str
    var_seq: int | None
    topology_source: str = ""

    def connection_config(self, timeout_ms: int) -> dict[str, Any]:
        """字段与 collector ``build_connection_config`` 的 S7 子集一致。"""
        return {
            "ip": self.host,
            "endpoint": self.endpoint,
            "port": self.port,
            "rack": self.rack,
            "slot": self.slot,
            "opc_server": None,
            "read_timeout_ms": timeout_ms,
        }


def is_s7_protocol(protocol: str | None) -> bool:
    """空协议按采集侧规则视为 S7。"""
    raw = (protocol or "").strip().lower()
    if not raw:
        return True
    compact = raw.replace("-", "_").replace(" ", "_")
    return raw in _S7_PROTOCOLS or compact in _S7_PROTOCOLS


def canonical_protocol(protocol: str | None) -> str:
    if is_s7_protocol(protocol):
        return "s7"
    return (protocol or "").strip().lower()


def parse_endpoint(endpoint: str, default_port: int) -> tuple[str, int]:
    """与 ``ConfigDBManager._parse_endpoint`` 相同：裸 IP，或 ``ip:port``。"""
    text = (endpoint or "").strip()
    if not text:
        return "127.0.0.1", default_port
    if "://" in text:
        parsed = urlparse(text)
        host = parsed.hostname or "127.0.0.1"
        return host, int(parsed.port or default_port)
    if text.count(":") == 1:
        host, port_text = text.rsplit(":", 1)
        try:
            return host.strip() or "127.0.0.1", int(port_text.strip())
        except ValueError:
            return text, default_port
    return text, default_port


def mysql_settings_from_env(environ: dict[str, str] | None = None) -> MysqlSettings | None:
    """没有主机、账号或库名时视为拓扑不可用。不默认 root。"""
    env = environ if environ is not None else os.environ
    host = str(env.get("DATAWATCH_MYSQL_HOST") or "").strip()
    user = str(env.get("DATAWATCH_MYSQL_USER") or "").strip()
    database = str(env.get("DATAWATCH_MYSQL_DATABASE") or "").strip()
    if not host or not user or not database:
        return None
    port_text = str(env.get("DATAWATCH_MYSQL_PORT") or "3306").strip() or "3306"
    try:
        port = int(port_text)
    except ValueError as exc:
        raise TopologyUnavailable("DATAWATCH_MYSQL_PORT 不是整数") from exc
    return MysqlSettings(
        host=host,
        port=port,
        user=user,
        database=database,
        password=str(env.get("DATAWATCH_MYSQL_PASSWORD") or ""),
    )


def ensure_read_only_account(grants: list[str]) -> None:
    """授权里出现写权限就拒绝。解析不了的授权同样拒绝，避免误用高权限账号。"""
    if not grants:
        raise DirectReadError(
            "config_error",
            "无法确认 MySQL 账号权限，已拒绝读取拓扑",
            quality=QUALITY_NOT_CONNECTED,
        )
    for grant in grants:
        text = str(grant or "").strip()
        match = _GRANT_BODY.search(text)
        if match is None:
            raise DirectReadError(
                "config_error",
                "无法解析 MySQL 授权，已拒绝读取拓扑",
                quality=QUALITY_NOT_CONNECTED,
            )
        if _WRITE_PRIVILEGE.search(match.group(1)):
            raise DirectReadError(
                "config_error",
                "MySQL 拓扑账号含写权限，直连只接受只读账号",
                quality=QUALITY_NOT_CONNECTED,
            )


def query_topology(cursor: Any) -> list[dict[str, Any]]:
    """只执行会话只读、查看授权和 SELECT。"""
    cursor.execute(_SESSION_READ_ONLY_SQL)
    cursor.execute(_SHOW_GRANTS_SQL)
    grants = [_first_value(row) for row in cursor.fetchall()]
    ensure_read_only_account(grants)
    cursor.execute(DEVICE_SQL)
    devices: list[dict[str, Any]] = []
    for row in cursor.fetchall():
        device = normalize_device_row(row)
        if device["deviceId"] is None or not device["deviceCode"]:
            continue
        cursor.execute(POINT_SQL, (device["deviceId"],))
        device["points"] = [normalize_point_row(item) for item in cursor.fetchall()]
        devices.append(device)
    return devices


def normalize_device_row(row: dict[str, Any]) -> dict[str, Any]:
    protocol = canonical_protocol(_cell(row, "protocol", "protocol"))
    declared_port = _cell(row, "port", "port")
    default_port = int(declared_port) if declared_port else 102
    endpoint = str(_cell(row, "endpoint", "endpoint") or "")
    host, port = parse_endpoint(endpoint, default_port)
    device_code = str(_cell(row, "device_code", "deviceCode") or "").strip()
    device_name = str(_cell(row, "device_name", "deviceName") or "").strip() or device_code
    rack = _cell(row, "rack", "rack")
    slot = _cell(row, "slot", "slot")
    device_id = _cell(row, "id", "deviceId")
    return {
        "deviceId": int(device_id) if device_id not in (None, "") else None,
        "deviceCode": device_code,
        "deviceName": device_name,
        "protocol": protocol,
        "endpoint": endpoint or host,
        "host": host,
        "port": port,
        "rack": 0 if rack is None else int(rack),
        "slot": 1 if slot is None else int(slot),
        "enable": _cell(row, "enable", "enable"),
        "points": [],
    }


def normalize_point_row(row: dict[str, Any]) -> dict[str, Any]:
    var_seq = _cell(row, "var_seq", "varSeq")
    data_type = str(_cell(row, "var_type", "dataType") or "INT").strip().upper() or "INT"
    return {
        "pointId": _cell(row, "id", "pointId"),
        "pointName": str(_cell(row, "var_name", "pointName") or "").strip(),
        "ioAddress": str(_cell(row, "var_address", "ioAddress") or "").strip(),
        "dataType": data_type,
        "varSeq": int(var_seq) if var_seq not in (None, "") else None,
        "description": _cell(row, "description", "description"),
        "enable": _cell(row, "enable", "enable"),
        "deleted": _cell(row, "deleted", "deleted"),
    }


def select_target(
    devices: list[dict[str, Any]],
    device_query: str,
    point_query: str,
) -> ResolvedTarget:
    device = _select_device(_active_devices(devices), device_query)
    point = _select_point(device, point_query)
    return _target_from(device, point)


def build_sources(
    *,
    mode: str,
    snapshot_path: str | None,
    endpoint: str | None,
    address: str | None,
    protocol: str | None,
    rack: int | None,
    slot: int | None,
    data_type: str | None,
    port: int | None,
) -> list[Any]:
    mode_name = (mode or "auto").strip().lower()
    if mode_name not in _MODES:
        raise DirectReadError(
            "usage_error",
            "topology 只能是 auto、mysql、snapshot 或 explicit",
            quality=QUALITY_NOT_CONNECTED,
        )
    mysql = MysqlTopology()
    snapshot = SnapshotTopology(snapshot_path)
    explicit = ExplicitTopology(
        endpoint=endpoint,
        address=address,
        protocol=protocol,
        rack=rack,
        slot=slot,
        data_type=data_type,
        port=port,
        required=mode_name == "explicit",
    )
    if mode_name == "mysql":
        return [mysql]
    if mode_name == "snapshot":
        return [snapshot]
    if mode_name == "explicit":
        return [explicit]
    return [mysql, snapshot, explicit]


def resolve_chain(sources: list[Any], device: str, point: str) -> ResolvedTarget:
    attempts: list[dict[str, str]] = []
    for source in sources:
        try:
            resolved = source.resolve(device, point)
        except TopologyUnavailable as exc:
            attempts.append(
                {"source": str(source.name), "reason": scrub_text(str(exc))}
            )
            continue
        except DirectReadError as exc:
            exc.details.setdefault("topologySource", str(source.name))
            raise
        return replace(resolved, topology_source=str(source.name))
    raise DirectReadError(
        "topology_unavailable",
        "拓扑不可用：MySQL 无法读取，且没有可用的本地快照或显式地址",
        quality=QUALITY_NOT_CONNECTED,
        details={"attempts": attempts},
    )


class MysqlTopology:
    name = "mysql"

    def resolve(self, device: str, point: str) -> ResolvedTarget:
        settings = mysql_settings_from_env()
        if settings is None:
            raise TopologyUnavailable(
                "未配置 DATAWATCH_MYSQL_HOST、DATAWATCH_MYSQL_USER 或 DATAWATCH_MYSQL_DATABASE"
            )
        try:
            import pymysql
            from pymysql.cursors import DictCursor
        except ImportError as exc:
            raise TopologyUnavailable("未安装 PyMySQL，无法读取 MySQL 拓扑") from exc
        try:
            connection = pymysql.connect(
                host=settings.host,
                port=settings.port,
                user=settings.user,
                password=settings.password,
                database=settings.database,
                charset="utf8mb4",
                cursorclass=DictCursor,
                connect_timeout=3,
                read_timeout=5,
                write_timeout=5,
                autocommit=True,
            )
        except Exception as exc:
            raise TopologyUnavailable(scrub_text(str(exc))) from exc
        try:
            with connection.cursor() as cursor:
                devices = query_topology(cursor)
        except DirectReadError:
            raise
        except Exception as exc:
            raise TopologyUnavailable(scrub_text(str(exc))) from exc
        finally:
            connection.close()
        return select_target(devices, device, point)


class SnapshotTopology:
    name = "snapshot"

    def __init__(self, snapshot_path: str | None) -> None:
        self.snapshot_path = snapshot_path

    def resolve(self, device: str, point: str) -> ResolvedTarget:
        path = _snapshot_file(self.snapshot_path)
        if path is None:
            raise TopologyUnavailable("未配置本地拓扑快照")
        if not path.is_file():
            raise TopologyUnavailable(f"本地拓扑快照不存在: {path}")
        try:
            payload = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, ValueError) as exc:
            raise DirectReadError(
                "config_error",
                "本地拓扑快照无法解析",
                quality=QUALITY_CONFIG_DECODE_ERROR,
            ) from exc
        if not isinstance(payload, dict) or not isinstance(payload.get("devices"), list):
            raise DirectReadError(
                "config_error",
                "本地拓扑快照缺少 devices 列表",
                quality=QUALITY_CONFIG_DECODE_ERROR,
            )
        devices = [_snapshot_device(item) for item in payload["devices"] if isinstance(item, dict)]
        return select_target(devices, device, point)


class ExplicitTopology:
    name = "explicit"

    def __init__(
        self,
        *,
        endpoint: str | None,
        address: str | None,
        protocol: str | None,
        rack: int | None,
        slot: int | None,
        data_type: str | None,
        port: int | None,
        required: bool,
    ) -> None:
        self.endpoint = endpoint
        self.address = address
        self.protocol = protocol
        self.rack = rack
        self.slot = slot
        self.data_type = data_type
        self.port = port
        self.required = required

    def resolve(self, device: str, point: str) -> ResolvedTarget:
        endpoint = str(self.endpoint or "").strip()
        address = str(self.address or "").strip()
        if not endpoint or not address:
            if self.required:
                raise DirectReadError(
                    "usage_error",
                    "显式拓扑需要 --endpoint 和 --address",
                    quality=QUALITY_NOT_CONNECTED,
                )
            raise TopologyUnavailable("未提供显式 endpoint 和 address")
        default_port = int(self.port) if self.port else 102
        host, parsed_port = parse_endpoint(endpoint, default_port)
        device_code = device.strip()
        return ResolvedTarget(
            device_code=device_code,
            device_name=device_code,
            device_id=None,
            protocol=canonical_protocol(self.protocol),
            host=host,
            endpoint=endpoint,
            port=parsed_port,
            rack=0 if self.rack is None else int(self.rack),
            slot=1 if self.slot is None else int(self.slot),
            point_name=point.strip(),
            io_address=address,
            data_type=(self.data_type or "INT").strip().upper() or "INT",
            var_seq=None,
        )


def _snapshot_file(snapshot_path: str | None) -> Path | None:
    if snapshot_path:
        return Path(snapshot_path)
    root = os.environ.get("DATAWATCH_DATA_ROOT")
    if not root:
        return None
    return Path(root) / "direct_read" / "topology.json"


def _snapshot_device(item: dict[str, Any]) -> dict[str, Any]:
    device = normalize_device_row(
        {
            "id": item.get("deviceId", item.get("id")),
            "device_code": item.get("deviceCode", item.get("device_code")),
            "device_name": item.get("deviceName", item.get("device_name")),
            "endpoint": item.get("endpoint"),
            "rack": item.get("rack"),
            "slot": item.get("slot"),
            "protocol": item.get("protocol"),
            "port": item.get("port"),
            "enable": item.get("enable", 1),
        }
    )
    points = []
    raw_points = item.get("points") or []
    if isinstance(raw_points, list):
        for point in raw_points:
            if isinstance(point, dict):
                points.append(
                    normalize_point_row(
                        {
                            "id": point.get("pointId", point.get("id")),
                            "var_seq": point.get("varSeq", point.get("var_seq")),
                            "var_name": point.get("pointName", point.get("var_name")),
                            "var_type": point.get("dataType", point.get("var_type")),
                            "var_address": point.get("ioAddress", point.get("var_address")),
                            "description": point.get("description"),
                            "enable": point.get("enable", 1),
                            "deleted": point.get("deleted", 0),
                        }
                    )
                )
    device["points"] = points
    return device


def _active_devices(devices: list[dict[str, Any]]) -> list[dict[str, Any]]:
    active = []
    for device in devices:
        if _disabled(device.get("enable"), default_enabled=True):
            continue
        active.append(device)
    return active


def _select_device(devices: list[dict[str, Any]], device_query: str) -> dict[str, Any]:
    identifier = device_query.strip()
    if identifier.isdigit():
        device_id = _positive_int(identifier, "deviceId")
        matches = [item for item in devices if int(item.get("deviceId") or 0) == device_id]
        return _rethrow_cli(lambda: Resolver._unique(matches, "设备", identifier))
    return _rethrow_cli(
        lambda: Resolver._ranked_unique(
            devices,
            "设备",
            identifier,
            fields=("deviceCode", "deviceName"),
            match_key=device_lookup_key(identifier),
        )
    )


def _select_point(device: dict[str, Any], point_query: str) -> dict[str, Any]:
    identifier = point_query.strip()
    items = []
    for point in device.get("points") or []:
        if _disabled(point.get("enable"), default_enabled=True):
            continue
        if _truthy_flag(point.get("deleted")):
            continue
        items.append(
            {
                "deviceName": device.get("deviceName"),
                "deviceCode": device.get("deviceCode"),
                "pointName": point.get("pointName"),
                "ioAddress": point.get("ioAddress"),
                "description": point.get("description"),
                "varSeq": point.get("varSeq"),
                "dataType": point.get("dataType"),
                "_point": point,
            }
        )
    if identifier.isdigit():
        var_seq = _positive_int(identifier, "varSeq")
        matches = [item for item in items if int(item.get("varSeq") or 0) == var_seq]
        chosen = _rethrow_cli(lambda: Resolver._unique(matches, "点位", identifier))
    else:
        chosen = _rethrow_cli(
            lambda: Resolver._ranked_unique(
                items,
                "点位",
                identifier,
                fields=("pointName", "ioAddress"),
            )
        )
    return dict(chosen["_point"])


def _target_from(device: dict[str, Any], point: dict[str, Any]) -> ResolvedTarget:
    var_seq = point.get("varSeq")
    return ResolvedTarget(
        device_code=str(device.get("deviceCode") or ""),
        device_name=str(device.get("deviceName") or device.get("deviceCode") or ""),
        device_id=device.get("deviceId"),
        protocol=canonical_protocol(device.get("protocol")),
        host=str(device.get("host") or ""),
        endpoint=str(device.get("endpoint") or device.get("host") or ""),
        port=int(device.get("port") or 102),
        rack=int(device.get("rack") or 0),
        slot=1 if device.get("slot") is None else int(device["slot"]),
        point_name=str(point.get("pointName") or ""),
        io_address=str(point.get("ioAddress") or ""),
        data_type=str(point.get("dataType") or "INT"),
        var_seq=int(var_seq) if var_seq not in (None, "") else None,
    )


def _positive_int(value: str, label: str) -> int:
    try:
        parsed = int(value)
    except ValueError as exc:
        raise DirectReadError(
            "usage_error",
            f"{label} 必须为正整数",
            quality=QUALITY_NOT_CONNECTED,
        ) from exc
    if parsed <= 0:
        raise DirectReadError(
            "usage_error",
            f"{label} 必须为正整数",
            quality=QUALITY_NOT_CONNECTED,
        )
    return parsed


def _rethrow_cli(operation):
    try:
        return operation()
    except CliError as exc:
        raise DirectReadError(
            exc.error_type,
            exc.message,
            status=exc.status,
            details=dict(exc.details or {}),
            quality=QUALITY_NOT_CONNECTED,
        ) from exc


def _cell(row: Any, snake: str, camel: str) -> Any:
    if isinstance(row, dict):
        if snake in row:
            return row[snake]
        return row.get(camel)
    return None


def _first_value(row: Any) -> str:
    if isinstance(row, dict):
        if not row:
            return ""
        return str(next(iter(row.values())))
    if isinstance(row, (list, tuple)) and row:
        return str(row[0])
    return str(row or "")


def _disabled(value: Any, *, default_enabled: bool) -> bool:
    if value is None:
        return not default_enabled
    if isinstance(value, str):
        return value.strip() in {"0", "false", "False"}
    return value in {0, False}


def _truthy_flag(value: Any) -> bool:
    if isinstance(value, str):
        return value.strip() in {"1", "true", "True"}
    return value in {1, True}
