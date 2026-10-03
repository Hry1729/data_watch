import logging
import os
import secrets
import sys
import threading
from pathlib import Path

import toml

from app.core.role import build_role

logger = logging.getLogger(__name__)


def data_root() -> Path:
    """可写数据根：环境变量 DATAWATCH_DATA_ROOT 优先；frozen 默认 C:\\ProgramData\\DataWatch；
    dev 回退到 agent 包目录（保持 config.toml 在 agent/config.toml）。

    frozen 下 __file__ 位于解包临时目录，不可作持久/可写配置根；Program Files 对服务账户
    只读，故 bootstrap 写入的 config.toml 必须落在 ProgramData。安装器/NSSM 注入
    DATAWATCH_DATA_ROOT 覆盖默认值。
    """
    env = os.environ.get("DATAWATCH_DATA_ROOT")
    if env:
        return Path(env)
    if getattr(sys, "frozen", False):
        names = {"centralized": "DataWatch", "edge": "DataWatch Edge", "distributed-center": "DataWatch Distributed Center"}
        return Path(r"C:\ProgramData") / names[build_role()]
    return Path(__file__).parent.parent.parent


def install_root() -> Path:
    """只读安装根：环境变量 DATAWATCH_INSTALL_ROOT 优先；frozen 取 exe 父目录；dev 取仓库根。

    安装器/NSSM 注入 DATAWATCH_INSTALL_ROOT，使布局与 frozen 推导解耦——agent.exe 放
    <root>\\agent\\ 子目录时 exe 父目录指向 agent\\ 而非根，故以环境变量为准；缺失时
    回退 frozen(exe 父目录)/dev(仓库根)。
    """
    env = os.environ.get("DATAWATCH_INSTALL_ROOT")
    if env:
        return Path(env).resolve()
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent
    return Path(__file__).resolve().parents[3]


CONFIG_PATH = data_root() / "config.toml"
_CONFIG_LOCK = threading.RLock()

DB_SECTIONS = ("redis", "mysql", "tdengine")

_MODULE_LAYOUT = {
    "centralized": {"collector": ("collector", "data-watch-collector.exe"), "writer": ("writer", "data-watch-writer.exe")},
    "edge": {
        "storage": ("edge-storage", "data-watch-edge-storage.exe"),
        "collector": ("edge-collector", "data-watch-edge-collector.exe"),
        "uploader": ("uploader", "data-watch-uploader.exe"),
    },
    "distributed-center": {"ingest": ("ingest", "data-watch-ingest.exe"), "writer": ("writer", "data-watch-writer.exe")},
}


def module_paths(name: str) -> tuple[Path, Path]:
    """(executable, working_dir)：二进制在只读安装根 <install_root>/<name>/<exe>；工作目录
    (含 config/settings.toml)在可写数据根 <data_root>/<name>。据此 config.toml 不存绝对路径，
    按目标机动态派生。
    """
    try:
        directory, executable = _MODULE_LAYOUT[build_role()][name]
    except KeyError as exc:
        raise ValueError(f"module {name!r} is not available for role {build_role()!r}") from exc
    return install_root() / directory / executable, data_root() / name
EMPTY_PASSWORD_DETAIL = "password 不允许为空串；如需保留原密码请省略 password 字段"


def load_config() -> dict:
    with _CONFIG_LOCK:
        return _load_config_unlocked()


def save_config(cfg: dict) -> None:
    with _CONFIG_LOCK:
        _save_config_unlocked(cfg)


def mutate_config(mutator) -> dict:
    """在同一个锁下加载、修改并保存配置。"""
    with _CONFIG_LOCK:
        cfg = _load_config_unlocked()
        mutator(cfg)
        _save_config_unlocked(cfg)
        return cfg


def _load_config_unlocked() -> dict:
    if not CONFIG_PATH.exists():
        cfg = _default_config()
        _save_config_unlocked(cfg)
        return cfg
    cfg = toml.load(CONFIG_PATH)
    _ensure_jwt_secret_unlocked(cfg)
    return cfg


JWT_SECRET_INVALID_DETAIL = (
    "config.toml 的 [agent].jwt_secret 为空或过弱（至少 32 字符）：空密钥会让任何本地进程"
    "伪造管理员令牌，因此拒绝启动认证服务。请改为安全随机值"
    "（例如 python -c \"import secrets;print(secrets.token_hex(32))\"）。"
    "注意：变更该值会使所有已签发的令牌失效，需重新登录。"
)
AGENT_SECTION_INVALID_DETAIL = "config.toml 缺少合法的 [agent] 段，拒绝按空密钥继续运行"


def _ensure_jwt_secret_unlocked(cfg: dict) -> None:
    """既有配置的 jwt_secret 强制校验（B15）。

    - 合格：原样保留，不轮换、不改写文件（合法存量配置保持稳定）。
    - 不合格且 bootstrap 未完成（明确首装场景）：原子生成并落盘安全随机值，并显式告警。
    - 不合格且 bootstrap 已完成：抛错，不得启动认证服务。
    """
    from app.core.security import validate_jwt_secret

    agent_section = cfg.get("agent")
    if not isinstance(agent_section, dict):
        raise ValueError(AGENT_SECTION_INVALID_DETAIL)
    try:
        validate_jwt_secret(agent_section.get("jwt_secret"))
        return
    except ValueError:
        pass

    if agent_section.get("bootstrap_done", False):
        raise ValueError(JWT_SECRET_INVALID_DETAIL)

    agent_section["jwt_secret"] = secrets.token_hex(32)
    _save_config_unlocked(cfg)
    logger.warning(
        "config.toml 已存在但 [agent].jwt_secret 为空或过弱；首装场景（bootstrap 未完成）"
        "已生成并落盘新的随机密钥，此前用旧密钥签发的令牌全部失效。"
    )


def _save_config_unlocked(cfg: dict) -> None:
    CONFIG_PATH.parent.mkdir(parents=True, exist_ok=True)
    tmp_path = CONFIG_PATH.with_name(f".{CONFIG_PATH.name}.tmp")
    with open(tmp_path, "w", encoding="utf-8") as f:
        toml.dump(cfg, f)
        f.flush()
        os.fsync(f.fileno())
    os.replace(tmp_path, CONFIG_PATH)


def apply_db_patch(cfg: dict, section: str, patch: dict) -> None:
    """对 db section（redis/mysql/tdengine）做 partial update。

    密码规则：patch 无 password 键 → 保留原 password_enc；password == "" → 抛
    ValueError（端点转 400）；password 非空 → DPAPI 加密写入 password_enc。
    """
    current = cfg.setdefault(section, {})

    for key in ("host", "port", "user", "database"):
        if key in patch and patch[key] is not None:
            current[key] = patch[key]

    if "password" not in patch:
        return

    password = patch["password"]
    if password == "":
        raise ValueError(EMPTY_PASSWORD_DETAIL)

    if password is not None:
        from app.core.security import dpapi_encrypt

        current["password_enc"] = dpapi_encrypt(password)


def _default_config() -> dict:
    return {
        "agent": {
            "host": "127.0.0.1",
            "port": int(os.environ.get("DATAWATCH_AGENT_PORT") or {"centralized": 7070, "edge": 7071, "distributed-center": 7072}[build_role()]),
            "jwt_secret": secrets.token_hex(32),
            "jwt_expire_hours": 8,
            "bootstrap_done": False,
            "log_level": "INFO",
            "runtime_dir": str(data_root() / "runtime"),
        },
        "redis": {"host": "127.0.0.1", "port": 6379, "password_enc": ""},
        "mysql": {
            "host": "127.0.0.1",
            "port": 3306,
            "user": "root",
            "database": "device_watch",
            "password_enc": "",
        },
        "tdengine": {
            "host": "127.0.0.1",
            "port": 6041,
            "user": "root",
            "database": "device_data",
            "password_enc": "",
        },
        # 后端地址运行期可配（引导页录入），不再打包进 app 前端；app 启动时经
        # GET /bootstrap/runtime-config 读取。base_url 无密码，明文存储。
        "backend": {"base_url": "http://127.0.0.1:8000"},
        "edge": {
            "center_url": "",
            "node_uuid": "",
            "node_name": "",
            "private_key_enc": "",
            "registered": False,
            "registration_status": "not_registered",
            "applied_revision": 0,
            "last_sync_error": "",
            "max_offline_retention_days": 7,
            "outbox_maintenance_seconds": 60,
        },

        "collector": {"executable": "", "args": [], "working_dir": ""},
        "writer": {"executable": "", "args": [], "working_dir": ""},
        "uploader": {"executable": "", "args": [], "working_dir": ""},
        "storage": {
            "executable": "",
            "args": [],
            "working_dir": "",
            "host": "127.0.0.1",
            "port": 7073,
            "queue_capacity": 2048,
        },
        "ingest": {
            "executable": "",
            "args": [],
            "working_dir": "",
            "max_offline_retention_days": 7,
        },
    }
