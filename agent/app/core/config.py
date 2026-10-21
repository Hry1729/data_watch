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


def resolve_runtime_dir(cfg: dict) -> Path:
    """运行目录：Agent 写 shutdown_flag、子进程读同一 flag 的**唯一**目录。

    `[agent].runtime_dir` 可能缺失（手改配置/旧版 config.toml）或为空串，两者都回退
    data_root()/runtime。factory（pm.runtime_dir，写侧）与 admin（DATAWATCH_RUNTIME_DIR
    /--runtime-dir，读侧）必须共用本函数：各自写一份默认值会让写入点与读取点分叉，
    子进程收不到优雅停机 flag，停机退化为超时强杀（D36.3 同类）。
    """
    value = (cfg.get("agent") or {}).get("runtime_dir")
    return Path(value) if value else data_root() / "runtime"


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


def initialize_config_permissions() -> None:
    """Repair existing secret file ACLs once, before accepting requests."""
    with _CONFIG_LOCK:
        for path in (CONFIG_PATH, CONFIG_PATH.with_name(f".{CONFIG_PATH.name}.tmp")):
            if path.exists() or path.is_symlink():
                _restrict_config_acl(path)


def _load_config_unlocked() -> dict:
    if CONFIG_PATH.is_symlink() or CONFIG_PATH.is_junction():
        raise ValueError(f"Refusing linked Agent configuration: {CONFIG_PATH}")
    if not CONFIG_PATH.exists():
        cfg = _default_config()
        _save_config_unlocked(cfg)
        return cfg
    cfg = toml.load(CONFIG_PATH)
    _ensure_jwt_secret_unlocked(cfg)
    _sync_redis_db_mirror(cfg)
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
    - **未配置**（键缺失，或值是空/纯空白占位符）：生成并落盘安全随机值，不论
      bootstrap 是否完成。这两种形态都是产品自己留下的，按篡改拒绝会让升级即砖机：
        * 键缺失：早于 2026-06-02 的配置模板从未写过该字段。
        * 空值：2026-06-02 ~ 2026-10-03 的 config.toml.example 写死
          `jwt_secret = ""` 并注明「首次启动时自动随机生成」，而那段自动生成
          2026-10-03（B15）才落地。该窗口内按文档流程装机的节点全是这个形态；
          现场 `C:\\ProgramData\\DataWatch` 即此——2026-10-08 实测服务 Paused、
          agent 反复以退出码 1 崩溃。
    - 键存在且**非空**但不合格：bootstrap 未完成时按首装生成；已完成则抛错——非空
      弱值只可能来自人为设置，是伪造管理员令牌的入口。
    """
    from app.core.security import validate_jwt_secret

    agent_section = cfg.get("agent")
    if not isinstance(agent_section, dict):
        raise ValueError(AGENT_SECTION_INVALID_DETAIL)

    if "jwt_secret" in agent_section:
        value = agent_section["jwt_secret"]
        try:
            validate_jwt_secret(value)
            return
        except ValueError:
            pass
        if isinstance(value, str) and not value.strip():
            reason = "为空（模板占位符，旧版配置迁移）"
        elif agent_section.get("bootstrap_done", False):
            raise ValueError(JWT_SECRET_INVALID_DETAIL)
        else:
            reason = "为空或过弱（bootstrap 未完成）"
    else:
        reason = "键缺失（旧版配置迁移）"

    agent_section["jwt_secret"] = secrets.token_hex(32)
    _save_config_unlocked(cfg)
    logger.warning(
        "config.toml 的 [agent].jwt_secret %s；已生成并落盘新的随机密钥，"
        "此前用旧密钥签发的令牌全部失效。",
        reason,
    )


def _save_config_unlocked(cfg: dict) -> None:
    CONFIG_PATH.parent.mkdir(parents=True, exist_ok=True)
    tmp_path = CONFIG_PATH.with_name(f".{CONFIG_PATH.name}.tmp")
    for path in (CONFIG_PATH, tmp_path):
        if path.exists() or path.is_symlink():
            _restrict_config_acl(path)
    try:
        with open(tmp_path, "w", encoding="utf-8", opener=_open_config_temp) as f:
            toml.dump(cfg, f)
            f.flush()
            os.fsync(f.fileno())
        os.replace(tmp_path, CONFIG_PATH)
    finally:
        tmp_path.unlink(missing_ok=True)


def _config_security_descriptor():
    import ntsecuritycon
    import win32api
    import win32con
    import win32security

    token = win32security.OpenProcessToken(win32api.GetCurrentProcess(), win32con.TOKEN_QUERY)
    try:
        current_sid = win32security.GetTokenInformation(token, win32security.TokenUser)[0]
    finally:
        token.Close()
    acl = win32security.ACL()
    for sid in (current_sid, win32security.CreateWellKnownSid(win32security.WinLocalSystemSid),
                win32security.CreateWellKnownSid(win32security.WinBuiltinAdministratorsSid)):
        acl.AddAccessAllowedAce(win32security.ACL_REVISION, ntsecuritycon.FILE_ALL_ACCESS, sid)
    descriptor = win32security.SECURITY_DESCRIPTOR()
    descriptor.SetSecurityDescriptorDacl(True, acl, False)
    descriptor.SetSecurityDescriptorControl(win32security.SE_DACL_PROTECTED, win32security.SE_DACL_PROTECTED)
    return descriptor


def _restrict_config_acl(path: Path) -> None:
    """JWT configuration is private to the service/admins and its actual developer owner."""
    if path.is_symlink() or path.is_junction():
        raise ValueError(f"Refusing linked Agent configuration: {path}")
    if os.name != "nt":
        path.chmod(0o600)
        return
    import win32security
    win32security.SetNamedSecurityInfo(
        str(path), win32security.SE_FILE_OBJECT,
        win32security.DACL_SECURITY_INFORMATION | win32security.PROTECTED_DACL_SECURITY_INFORMATION,
        None, None, _config_security_descriptor().GetSecurityDescriptorDacl(), None,
    )


def _open_config_temp(path, flags):
    if os.name != "nt":
        return os.open(path, flags, 0o600)
    import msvcrt
    import pywintypes
    import win32con
    import win32file
    import win32security
    attributes = pywintypes.SECURITY_ATTRIBUTES()
    attributes.SECURITY_DESCRIPTOR = _config_security_descriptor()
    handle = win32file.CreateFile(
        str(path), win32con.GENERIC_WRITE | win32con.WRITE_DAC, 0, attributes,
        win32con.CREATE_ALWAYS, win32con.FILE_ATTRIBUTE_NORMAL | win32file.FILE_FLAG_OPEN_REPARSE_POINT, None,
    )
    detached = False
    try:
        if win32file.GetFileInformationByHandle(handle)[0] & win32con.FILE_ATTRIBUTE_REPARSE_POINT:
            raise ValueError("Refusing linked Agent temporary configuration")
        win32security.SetSecurityInfo(
            handle, win32security.SE_FILE_OBJECT,
            win32security.DACL_SECURITY_INFORMATION | win32security.PROTECTED_DACL_SECURITY_INFORMATION,
            None, None, attributes.SECURITY_DESCRIPTOR.GetSecurityDescriptorDacl(), None,
        )
        raw_handle = handle.Detach()
        detached = True
        try:
            return msvcrt.open_osfhandle(raw_handle, os.O_WRONLY | os.O_BINARY)
        except Exception:
            win32file.CloseHandle(raw_handle)
            raise
    finally:
        if not detached:
            handle.Close()


REDIS_DB_INVALID_DETAIL = "Redis DB 必须为非负整数"


def set_redis_db(redis_section: dict, value) -> None:
    """把 Redis DB 写入服务实际读取的 [redis].db（int），并同步 database 展示镜像。

    [redis].db 是唯一权威值（Agent 启动 collector/writer/ingest 时据此下发）；
    database 仅为 GET /admin/config 回显用的字符串镜像。非法值抛 ValueError（端点转 400）。
    """
    if isinstance(value, bool):
        raise ValueError(REDIS_DB_INVALID_DETAIL)
    text = str(value).strip()
    if not text.isdigit():
        raise ValueError(REDIS_DB_INVALID_DETAIL)
    db = int(text)
    redis_section["db"] = db
    redis_section["database"] = str(db)


def _sync_redis_db_mirror(cfg: dict) -> None:
    """加载时让 [redis].database 镜像反映实际生效的 [redis].db（缺省 0）。

    旧版本把设置页的 DB 只写进 database、从未被服务读取；这里按实际生效值回显，
    不把旧镜像当成 db（否则重启后会静默切库）。db 非法时不动，由 _stream_contract 报错。
    """
    redis = cfg.get("redis")
    if not isinstance(redis, dict):
        return
    raw = redis.get("db", 0)
    if isinstance(raw, bool):
        return
    try:
        db = int(raw or 0)
    except (TypeError, ValueError):
        return
    if db >= 0:
        redis["database"] = str(db)


def apply_db_patch(cfg: dict, section: str, patch: dict) -> None:
    """对 db section（redis/mysql/tdengine）做 partial update。

    密码规则：patch 无 password 键 → 保留原 password_enc；password == "" → 抛
    ValueError（端点转 400）；password 非空 → DPAPI 加密写入 password_enc。
    """
    current = cfg.setdefault(section, {})

    for key in ("host", "port", "user", "database"):
        if key in patch and patch[key] is not None:
            if section == "redis" and key == "database":
                # 设置页的 Redis "DB" 走通用 database 字段，但 collector/writer/ingest
                # 读的是 [redis].db（经 _stream_contract → DATAWATCH_REDIS_DB /
                # INGEST_REDIS_URL 下发）。必须写到 db，否则保存后不生效。
                set_redis_db(current, patch[key])
                continue
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
        "redis": {"host": "127.0.0.1", "port": 6379, "db": 0, "database": "0", "password_enc": ""},
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
