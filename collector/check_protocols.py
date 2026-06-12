"""
启动前检查脚本

检查数据库中设备的协议类型，返回需要启动的服务列表。
"""

import os
import sys
import warnings

# 添加项目路径
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import argparse
import asyncio
import json
from typing import Dict, List

from core.config_loader import load_collector_config_from_env
from core.exceptions import ConfigError

warnings.filterwarnings(
    "ignore",
    category=RuntimeWarning,
    message=r"The global interpreter lock .*",
)

CENTER_PROTOCOLS = {
    "opcda_center",
    "opc_da_center",
    "opc da center",
    "opcda-center",
    "opc-da-center",
    "opcda center",
}

SIDECAR_PROTOCOLS = {
    "opcda_sidecar",
    "opc_da_sidecar",
    "opc da sidecar",
    "opcda-sidecar",
    "opc-da-sidecar",
    "opcda sidecar",
    "http_push",
    "http push",
    "http-push",
    "push",
}


def normalize_protocol(value: str) -> str:
    """Normalize protocol aliases used by DB rows and old seed scripts."""
    return (value or "").strip().lower()


def load_toml_file(path: str) -> dict:
    """Load TOML without requiring the third-party toml package on Python 3.11+."""
    try:
        import tomllib

        with open(path, "rb") as f:
            return tomllib.load(f)
    except ModuleNotFoundError:
        import toml

        return toml.load(path)


async def check_protocols(mysql_config: dict) -> Dict[str, List[str]]:
    """
    检查数据库中的协议类型

    Returns:
        {
            "center_collection": ["172.27.18.221", "172.27.18.222"],
            "sidecar_push": [],
            "has_center": True,
            "has_sidecar": False
        }
    """
    result = {
        "center_collection": [],
        "sidecar_push": [],
        "has_center": False,
        "has_sidecar": False,
        "port_mapping": {}  # endpoint -> port
    }

    try:
        from sqlalchemy import text
        from sqlalchemy.engine import URL
        from sqlalchemy.ext.asyncio import create_async_engine, async_sessionmaker
    except Exception as e:
        print(f"[WARN] Failed to import DB dependencies for protocol check: {e}", file=sys.stderr)
        return result

    # Build the DSN through SQLAlchemy so special chars in the password are safe.
    dsn = URL.create(
        "mysql+aiomysql",
        username=mysql_config["user"],
        password=mysql_config["password"],
        host=mysql_config["host"],
        port=int(mysql_config["port"]),
        database=mysql_config["database"],
        query={"charset": "utf8mb4"},
    )

    engine = create_async_engine(dsn, pool_pre_ping=True)
    async_session = async_sessionmaker(engine, expire_on_commit=False)

    try:
        async with async_session() as session:
            rows = await session.execute(
                text("""
                    SELECT d.device_code, d.endpoint, cp.protocol
                    FROM device d
                    JOIN comm_protocol cp ON d.protocol_id = cp.id
                    WHERE d.enable = 1
                """)
            )

            # center / sidecar 各自独立去重：同一 endpoint 若两类协议都配置，
            # 用单集合会让先处理的一类把它加入 seen，另一类被静默漏掉（A方-10）。
            center_seen = set()
            sidecar_seen = set()
            port_counter = 9090

            for row in rows:
                device_code, endpoint, protocol = row
                protocol_lower = normalize_protocol(protocol)

                if protocol_lower in CENTER_PROTOCOLS:
                    result["has_center"] = True
                    if endpoint and endpoint not in center_seen:
                        center_seen.add(endpoint)
                        result["center_collection"].append(endpoint)
                        result["port_mapping"][endpoint] = port_counter
                        port_counter += 1

                elif protocol_lower in SIDECAR_PROTOCOLS:
                    result["has_sidecar"] = True
                    if endpoint and endpoint not in sidecar_seen:
                        sidecar_seen.add(endpoint)
                        result["sidecar_push"].append(endpoint)

    except Exception as e:
        print(f"[WARN] Failed to check protocols: {e}", file=sys.stderr)
        # 返回空结果，让用户手动启动
        return result
    finally:
        await engine.dispose()

    return result


def main():
    """主入口"""
    parser = argparse.ArgumentParser(description="Check device protocols before startup")
    parser.add_argument("--output", "-o", choices=["json", "batch"], default="json",
                        help="Output format: json or batch script")
    parser.add_argument("--config", "-c", default=None,
                        help="Path to config file (overrides COLLECTOR_CONFIG env)")
    args = parser.parse_args()

    # 加载配置 - 与 main.py 保持一致的逻辑
    mysql_config = None

    if args.config:
        # 命令行参数优先
        config_path = args.config
        if not os.path.exists(config_path):
            print(f"[ERROR] Config file not found: {config_path}", file=sys.stderr)
            return 1
        try:
            from core.config_loader import load_collector_config
            config = load_collector_config(config_path)
            mysql_config = {
                "host": config.mysql.host,
                "port": config.mysql.port,
                "user": config.mysql.user,
                "password": config.mysql.password,
                "database": config.mysql.database,
            }
        except ConfigError as e:
            print(f"[ERROR] Failed to load config: {e}", file=sys.stderr)
            return 1
    else:
        # 使用环境变量 COLLECTOR_CONFIG（与 main.py 一致）
        env_config_path = os.getenv("COLLECTOR_CONFIG")
        if env_config_path and not os.path.exists(env_config_path):
            # COLLECTOR_CONFIG 设置了但文件不存在，报错而非回退
            print(f"[ERROR] COLLECTOR_CONFIG file not found: {env_config_path}", file=sys.stderr)
            return 1

        try:
            config = load_collector_config_from_env()
            mysql_config = {
                "host": config.mysql.host,
                "port": config.mysql.port,
                "user": config.mysql.user,
                "password": config.mysql.password,
                "database": config.mysql.database,
            }
        except ConfigError as e:
            print(f"[ERROR] Failed to load config: {e}", file=sys.stderr)
            if env_config_path:
                # 如果设置了 COLLECTOR_CONFIG，报错而非回退默认配置
                return 1
            # 否则回退到默认配置（用于开发测试）
            mysql_config = {
                "host": "127.0.0.1",
                "port": 3306,
                "user": "root",
                "password": "",
                "database": "device_watch",
            }
            print("[WARN] Using default MySQL config", file=sys.stderr)

    # 检查协议
    result = asyncio.run(check_protocols(mysql_config))

    # 保存端口映射到文件（供 DeviceManager 复用）
    if result["port_mapping"]:
        mapping_file = os.path.join(os.path.dirname(__file__), "port_mapping.json")
        try:
            with open(mapping_file, "w", encoding="utf-8") as f:
                json.dump(result["port_mapping"], f, indent=2)
            print(f"[INFO] Saved port mapping to {mapping_file}", file=sys.stderr)
        except Exception as e:
            print(f"[WARN] Failed to save port mapping: {e}", file=sys.stderr)

    if args.output == "json":
        print(json.dumps(result, indent=2))
    else:
        # 输出批处理脚本片段
        print(f'SET "OPCDA_CENTER_COUNT={len(result["center_collection"])}"')
        print(f'SET "OPCDA_SIDECAR_COUNT={len(result["sidecar_push"])}"')

        if result["has_center"]:
            print('SET "NEED_CENTER=1"')
            for i, endpoint in enumerate(result["center_collection"]):
                port = result["port_mapping"][endpoint]
                print(f'SET "VM_ENDPOINT_{i}={endpoint}"')
                print(f'SET "VM_PORT_{i}={port}"')
        else:
            print('SET "NEED_CENTER=0"')

        if result["has_sidecar"]:
            print('SET "NEED_SIDECAR=1"')
        else:
            print('SET "NEED_SIDECAR=0"')

    return 0


if __name__ == "__main__":
    sys.exit(main())
