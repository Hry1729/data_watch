"""检测 opcda_center 端点 + 粘性端口映射 + MySQL 一致性校验（FR-P01~P05、FR-P13）。

设计要点：
- 检测用 pymysql + DPAPI 解密（与 health_checker.py 一致），不用 sqlalchemy/aiomysql。
- 粘性端口：已存在 endpoint 沿用旧端口；新 endpoint 取 max(现有∪历史)+1；stale 端点
  保留在 mapping、端口不回收（FR-P03/P04）。
- 一致性：Agent MySQL 与 collector settings.toml 的 host/port/database 须一致（FR-P13）。
"""

from __future__ import annotations

import json
import tomllib
from pathlib import Path
from typing import Any, List, Mapping

import pymysql

from app.core.security import dpapi_decrypt

# 须与 collector/check_protocols.py 的 CENTER_PROTOCOLS 同步（FR-P02）。
# agent 为独立部署单元（独立 venv），无法 import collector 包，故复制一份。
CENTER_PROTOCOLS = {
    "opcda_center",
    "opc_da_center",
    "opc da center",
    "opcda-center",
    "opc-da-center",
    "opcda center",
}

BASE_PORT = 9090

_DETECT_SQL = """
    SELECT d.endpoint, cp.protocol
    FROM device d
    JOIN comm_protocol cp ON d.protocol_id = cp.id
    WHERE d.enable = 1
"""

# 与 health_checker.py 保持一致的连接超时（秒）。
CONNECT_TIMEOUT_SECONDS = 3


class MysqlConsistencyError(Exception):
    """Agent 与 collector 的 MySQL 配置不一致（FR-P13），拒绝启动 collector。"""


def _to_text(value: Any) -> Any:
    """pymysql 在某些编码/字段配置下返回 bytes，统一 decode 成 str。"""
    if isinstance(value, bytes):
        return value.decode("utf-8", "replace")
    return value


def normalize_protocol(value: Any) -> str:
    return (_to_text(value) or "").strip().lower()


def _normalize_endpoint(endpoint: Any) -> str:
    ep = (_to_text(endpoint) or "").strip()
    if ep == "" or ep in ("127.0.0.1", "localhost"):
        return "localhost"
    return ep


def _coerce_port(value: Any) -> int | None:
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _decrypt_password(config: Mapping[str, Any]) -> str:
    if "password" in config and config.get("password") is not None:
        return str(config.get("password") or "")
    return dpapi_decrypt(str(config.get("password_enc") or ""))


def detect_center_endpoints(mysql_config: Mapping[str, Any]) -> List[str]:
    """查询启用设备，返回去重有序的 opcda_center 端点列表（FR-P01/P02）。"""
    connection = pymysql.connect(
        host=mysql_config.get("host"),
        port=int(mysql_config.get("port")),
        user=mysql_config.get("user"),
        password=_decrypt_password(mysql_config),
        database=mysql_config.get("database"),
        connect_timeout=CONNECT_TIMEOUT_SECONDS,
        read_timeout=CONNECT_TIMEOUT_SECONDS,
        write_timeout=CONNECT_TIMEOUT_SECONDS,
        autocommit=True,
    )
    try:
        with connection.cursor() as cursor:
            cursor.execute(_DETECT_SQL)
            rows = cursor.fetchall()
    finally:
        connection.close()

    endpoints: List[str] = []
    seen = set()
    for endpoint, protocol in rows:
        if normalize_protocol(protocol) not in CENTER_PROTOCOLS:
            continue
        ep = _normalize_endpoint(endpoint)
        if ep not in seen:
            seen.add(ep)
            endpoints.append(ep)
    return endpoints


def assign_sticky_ports(
    endpoints: List[str],
    existing_mapping: Mapping[str, int],
    base_port: int = BASE_PORT,
) -> dict[str, int]:
    """粘性分配：已存在沿用旧端口；新端点取 max+1；stale 保留、端口不回收（FR-P03/P04）。"""
    mapping: dict[str, int] = dict(existing_mapping)
    for endpoint in endpoints:
        ep = _normalize_endpoint(endpoint)
        if ep not in mapping:
            # 取 max(现有∪历史)+1，且不低于 base_port（历史端口可能低于 9090）。
            mapping[ep] = max([base_port - 1, *mapping.values()]) + 1
    return mapping


def load_port_mapping(path: str | Path) -> dict[str, int]:
    p = Path(path)
    if not p.exists():
        return {}
    with open(p, "r", encoding="utf-8") as f:
        data = json.load(f)
    # 已存在但格式损坏须 fail-fast：静默清空会导致端口从 9090 重排、违反粘性（FR-P03/P04）。
    if not isinstance(data, dict):
        raise ValueError(f"port_mapping 文件格式错误（应为对象）: {p}")
    for endpoint, port in data.items():
        if isinstance(port, bool) or not isinstance(port, int):
            raise ValueError(f"port_mapping 端口必须为整数: {endpoint}={port!r} @ {p}")
    return data


def save_port_mapping(path: str | Path, mapping: Mapping[str, int]) -> None:
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    with open(p, "w", encoding="utf-8") as f:
        json.dump(dict(mapping), f, indent=2, ensure_ascii=False)


def load_collector_mysql(settings_path: str | Path) -> dict[str, Any]:
    """读 collector settings.toml 的 [mysql]，取 host/port/database（FR-P13 数据源）。"""
    with open(Path(settings_path), "rb") as f:
        data = tomllib.load(f)
    mysql = data.get("mysql", {})
    return {
        "host": mysql.get("host"),
        "port": mysql.get("port"),
        "database": mysql.get("database"),
    }


def verify_mysql_consistency(
    agent_mysql: Mapping[str, Any],
    collector_mysql: Mapping[str, Any],
) -> None:
    """host/port/database 任一不同则抛 MysqlConsistencyError（FR-P13）。"""
    diffs = []
    a_host = str(agent_mysql.get("host") or "").strip()
    c_host = str(collector_mysql.get("host") or "").strip()
    if a_host != c_host:
        diffs.append(f"host: agent={a_host!r} collector={c_host!r}")

    a_port = _coerce_port(agent_mysql.get("port"))
    c_port = _coerce_port(collector_mysql.get("port"))
    if a_port is None or c_port is None or a_port != c_port:
        diffs.append(
            f"port: agent={agent_mysql.get('port')!r} collector={collector_mysql.get('port')!r}"
        )

    a_db = str(agent_mysql.get("database") or "").strip()
    c_db = str(collector_mysql.get("database") or "").strip()
    if a_db != c_db:
        diffs.append(f"database: agent={a_db!r} collector={c_db!r}")

    if diffs:
        raise MysqlConsistencyError(
            "Agent 与 collector 的 MySQL 配置不一致: " + "; ".join(diffs)
        )
