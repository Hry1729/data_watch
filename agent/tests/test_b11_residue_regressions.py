"""B11 残留修复（R1 明文 `password` · R3 `HTTP_PROXY` · host 正则 `$`→`\\Z`）——复现与回归。

独立文件（不改动 tests/conftest.py 与既有 B11 用例文件；接收端与哨兵值复用
`tests.test_b11_health_target_credentials` 的隔离夹具）。全部接收端都是**本机回环临时端口**，
凭据一律 `TESTONLY_*` 哨兵；不连接现场 Redis/MySQL/TDengine 或任何真实库，
不读写 `C:\\ProgramData\\DataWatch Edge`。

三条残留均由 **W1 独立验收 agent** 发现（见 `docs/整改记录/W1-验收-agent.md` 遗留问题 2/3/4）：
- R1：绑定检查只看 `password_enc`，明文 `password` 形态（需手改配置）下改目标仍会外发口令；
- R2：`_HOST_PATTERN` 用 `$`，尾随换行的 host 被 `_target_address` 接受；
- R3：`_TDENGINE_OPENER` 在 import 时快照 `getproxies()`，进程启动前设 `HTTP_PROXY`
      会把 TDengine 存储口令发给代理（实测 `proxy_requests=1, direct_requests=0`）。
"""

import importlib.util
import secrets
import toml
import urllib.request
from pathlib import Path

import pytest
from fastapi import HTTPException

from app.core import config
from app.core.security import dpapi_encrypt
from app.routers.admin import health_check
from app.schemas.models import HealthCheckConfig, HealthCheckRequest
from app.services import health_checker
from app.services.health_checker import _target_address, check_tdengine
from tests.test_b11_health_target_credentials import (
    HttpRecorder,
    RedisReceiver,
    _basic_credentials,
)

STORED_SECRET = "TESTONLY_stored_pw_sentinel"
TEMP_SECRET = "TESTONLY_temp_pw_sentinel"
STRONG_JWT_SECRET = secrets.token_hex(32)
HEALTH_CHECKER_PATH = Path(health_checker.__file__)


@pytest.fixture(autouse=True)
def _isolated_data_root(tmp_path, monkeypatch):
    """安全守卫：全部夹具落在临时目录（绝不读写真实安装目录）。"""
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    monkeypatch.delenv("DATAWATCH_INSTALL_ROOT", raising=False)
    monkeypatch.setattr(config, "CONFIG_PATH", tmp_path / "config.toml")
    assert config.data_root() == tmp_path

    yield

    resolved = Path(config.CONFIG_PATH).resolve()
    assert tmp_path.resolve() in resolved.parents, f"配置路径越界: {resolved}"


def _write_config(tmp_path, monkeypatch, target: str, section: dict, path=None):
    path = path or tmp_path / "config.toml"
    path.write_text(
        toml.dumps(
            {
                "agent": {
                    "host": "127.0.0.1",
                    "port": 7070,
                    "jwt_secret": STRONG_JWT_SECRET,
                    "jwt_expire_hours": 8,
                    "bootstrap_done": True,
                },
                target: section,
            }
        ),
        encoding="utf-8",
    )
    monkeypatch.setattr(config, "CONFIG_PATH", path)
    return path


def _plaintext_section(host: str, port: int, secret: str = STORED_SECRET) -> dict:
    """R1 触发形态：**明文 `password`**（非产品写入形态），不含 `password_enc`。"""
    return {
        "host": host,
        "port": port,
        "user": "root",
        "database": "device_data",
        "password": secret,
    }


def _request(target: str, **overrides) -> HealthCheckRequest:
    return HealthCheckRequest(
        target=target,
        config=HealthCheckConfig(**overrides) if overrides else None,
    )


async def _run(request: HealthCheckRequest):
    try:
        return None, await health_check(request, _user="admin")
    except HTTPException as exc:
        return exc.status_code, None


# ── R1：明文 password 不得跟随新目标 ─────────────────────────────────────────


async def test_plaintext_password_is_not_reused_for_changed_target(tmp_path, monkeypatch):
    """R1 回归：存储段是明文 `password` 时，改 host/port 必须拒绝且一个字节都不外发。"""
    stored = RedisReceiver()
    new_target = RedisReceiver()
    try:
        _write_config(
            tmp_path, monkeypatch, "redis", _plaintext_section("127.0.0.1", stored.port)
        )
        status, _response = await _run(
            _request("redis", host="localhost", port=new_target.port)
        )
        assert len(new_target.payload()) == 0, "明文存储凭据不得跟随新目标"
        assert len(stored.payload()) == 0
        assert status == 400
    finally:
        stored.close()
        new_target.close()


@pytest.mark.parametrize(
    "overrides",
    [
        {"host": "localhost"},
        {"port": 60001},
        {"user": "other"},
        {"database": "other_db"},
    ],
)
async def test_plaintext_password_binding_covers_every_target_identifier(
    tmp_path, monkeypatch, overrides
):
    stored = RedisReceiver()
    try:
        _write_config(
            tmp_path, monkeypatch, "redis", _plaintext_section("127.0.0.1", stored.port)
        )
        status, _response = await _run(_request("redis", **overrides))
        assert len(stored.payload()) == 0, "目标变更时不得沿用明文存储凭据"
        assert status == 400
    finally:
        stored.close()


async def test_plaintext_password_explicit_none_still_denied(tmp_path, monkeypatch):
    """显式 `password=None` 不能把「未显式提供口令」判断绕过去。"""
    stored = RedisReceiver()
    try:
        _write_config(
            tmp_path, monkeypatch, "redis", _plaintext_section("127.0.0.1", stored.port)
        )
        status, _response = await _run(
            _request("redis", host="127.0.0.1", port=stored.port + 1, password=None)
        )
        assert len(stored.payload()) == 0
        assert status == 400
    finally:
        stored.close()


async def test_plaintext_stored_credential_with_temporary_one_succeeds(
    tmp_path, monkeypatch
):
    """显式临时凭据的新目标仍可用，且用的确是临时口令（存储明文一次都不出现）。"""
    stored = HttpRecorder()
    new_target = HttpRecorder()
    try:
        _write_config(
            tmp_path,
            monkeypatch,
            "tdengine",
            _plaintext_section("127.0.0.1", stored.port),
        )
        status, response = await _run(
            _request("tdengine", host="127.0.0.1", port=new_target.port, password=TEMP_SECRET)
        )

        assert status is None
        assert response is not None and response.success is True
        assert new_target.requests, "目标变更后检查本应照常执行"
        assert _basic_credentials(new_target.authorization_headers()[0]).endswith(
            f":{TEMP_SECRET}"
        )
        assert all(
            STORED_SECRET not in _basic_credentials(h)
            for h in stored.authorization_headers() + new_target.authorization_headers()
        )
        assert stored.requests == []
    finally:
        stored.close()
        new_target.close()


async def test_plaintext_stored_credential_unchanged_target_still_succeeds(
    tmp_path, monkeypatch
):
    """反向守卫：目标未变时，合法已配置目标（含明文 password 形态）的一键检查仍可用。"""
    origin = HttpRecorder()
    try:
        _write_config(
            tmp_path, monkeypatch, "tdengine", _plaintext_section("127.0.0.1", origin.port)
        )
        status, response = await _run(_request("tdengine"))

        assert status is None
        assert response is not None and response.success is True
        assert _basic_credentials(origin.authorization_headers()[0]).endswith(
            f":{STORED_SECRET}"
        )
    finally:
        origin.close()


@pytest.mark.parametrize(
    "section",
    [
        {"host": "127.0.0.1", "port": 1, "user": "root", "database": "device_data"},
        {
            "host": "127.0.0.1",
            "port": 1,
            "user": "root",
            "database": "device_data",
            "password_enc": "",
        },
        {
            "host": "127.0.0.1",
            "port": 1,
            "user": "root",
            "database": "device_data",
            "password": "",
        },
    ],
)
async def test_no_stored_credential_is_not_blocked(tmp_path, monkeypatch, section):
    """反向守卫：无任何**可用的**存储凭据时不得退化成「一律需人工授权」。"""
    new_target = HttpRecorder()
    try:
        _write_config(tmp_path, monkeypatch, "tdengine", dict(section))
        status, response = await _run(
            _request("tdengine", host="127.0.0.1", port=new_target.port)
        )
        assert status is None
        assert response is not None and response.success is True
    finally:
        new_target.close()


async def test_plaintext_password_denial_records_contain_no_secret(
    tmp_path, monkeypatch, caplog
):
    stored = RedisReceiver()
    try:
        _write_config(
            tmp_path, monkeypatch, "redis", _plaintext_section("127.0.0.1", stored.port)
        )
        with caplog.at_level("DEBUG"):
            try:
                await health_check(
                    _request("redis", host="127.0.0.1", port=stored.port + 1),
                    _user="admin",
                )
                raise AssertionError("变更目标且无显式凭据时必须 400")
            except HTTPException as exc:
                assert exc.status_code == 400
                assert STORED_SECRET not in str(exc.detail)
                assert TEMP_SECRET not in str(exc.detail)
        assert STORED_SECRET not in caplog.text
        assert TEMP_SECRET not in caplog.text
        assert "password_enc" not in caplog.text
        assert "TESTONLY" not in caplog.text
    finally:
        stored.close()


def test_stored_credential_predicate_forms():
    """纯函数级：两种形态（密文/明文）都算存储凭据；空值与 None 不算。"""
    from app.routers.admin import _has_stored_credential

    assert _has_stored_credential({"password_enc": "cipher"}) is True
    assert _has_stored_credential({"password": STORED_SECRET}) is True
    assert _has_stored_credential({"password_enc": "cipher", "password": STORED_SECRET}) is True
    assert _has_stored_credential({"password_enc": ""}) is False
    assert _has_stored_credential({"password": ""}) is False
    assert _has_stored_credential({"password": None}) is False
    assert _has_stored_credential({}) is False


# ── R3：健康检查不得走代理环境变量 ───────────────────────────────────────────


def _load_fresh_health_checker():
    """按文件路径重新装载一份 health_checker（模拟「进程启动时」的 opener 构建）。"""
    name = f"b11_fresh_health_checker_{secrets.token_hex(4)}"
    spec = importlib.util.spec_from_file_location(name, HEALTH_CHECKER_PATH)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _set_proxy_env(monkeypatch, port: int) -> str:
    proxy_url = f"http://127.0.0.1:{port}"
    monkeypatch.setenv("HTTP_PROXY", proxy_url)
    monkeypatch.setenv("http_proxy", proxy_url)
    for key in ("HTTPS_PROXY", "https_proxy", "NO_PROXY", "no_proxy"):
        monkeypatch.delenv(key, raising=False)
    return proxy_url


def test_tdengine_check_ignores_proxy_environment_at_import_time(monkeypatch):
    """R3 回归：`HTTP_PROXY` 在 opener 构建前就已设置时，凭据仍只发往直连目标。"""
    proxy = HttpRecorder()
    direct = HttpRecorder()
    try:
        proxy_url = _set_proxy_env(monkeypatch, proxy.port)
        fresh = _load_fresh_health_checker()  # opener 在代理变量已生效时构建

        result = fresh.check_tdengine(
            {"host": "127.0.0.1", "port": direct.port, "user": "root", "password": STORED_SECRET}
        )

        assert result.success is True
        assert proxy.requests == [], "健康检查不得把请求（含 Authorization）发给代理"
        assert direct.requests, "直连目标必须真的收到请求（防空转通过）"
        assert _basic_credentials(direct.authorization_headers()[0]).endswith(
            f":{STORED_SECRET}"
        )
        # 只影响健康检查自身的 opener：进程级代理设置/环境变量不被改动
        assert urllib.request.getproxies().get("http") == proxy_url
    finally:
        proxy.close()
        direct.close()


def test_health_checker_opener_declares_empty_proxy_table():
    """纯结构级：模块 opener 用空代理表，且不含任何可代理的 ProxyHandler。

    （`ProxyHandler({})` 不注册 `http_open`，因此 `build_opener` 连它本身都不会放进
    `opener.handlers`；这正是「空代理表 = 不代理」的直接证据。）
    """
    handler = health_checker._NoProxyHandler()
    assert isinstance(handler, urllib.request.ProxyHandler)
    assert handler.proxies == {}
    assert not hasattr(handler, "http_open"), "空代理表不得注册任何代理处理器"

    opener = health_checker._TDENGINE_OPENER
    assert not any(isinstance(h, urllib.request.ProxyHandler) for h in opener.handlers)
    assert any(isinstance(h, health_checker._NoRedirectHandler) for h in opener.handlers)


def test_module_import_does_not_mutate_global_proxy_view(monkeypatch):
    """反向检查：修复不得全局改写进程代理设置（`getproxies()` 仍看得见环境变量）。"""
    proxy = HttpRecorder()
    try:
        proxy_url = _set_proxy_env(monkeypatch, proxy.port)
        _load_fresh_health_checker()
        assert urllib.request.getproxies().get("http") == proxy_url
    finally:
        proxy.close()


# ── R2：host 正则收紧（`$` → `\\Z`），尾随换行必须被拒 ───────────────────────


@pytest.mark.parametrize(
    "host",
    ["LOCALHOST", "127.0.0.1.", "0x7f000001", "176.0.0.1", "db.example.com", "[::1]", "localhost"],
)
def test_legitimate_hosts_are_still_accepted(host):
    """反向守卫：收紧正则不得误拒合法主机名 / IPv4 字面量 / `[IPv6]`。"""
    assert _target_address({"host": host, "port": 6041}) == (host, 6041)


@pytest.mark.parametrize(
    "host",
    [
        "127.0.0.1\n",
        "127.0.0.1\r\n",
        "[::1]\n",
        "db.example.com\n",
        "127.0.0.1 ",
        "127.0.0.1\t",
        "127.0.0.1\x00",
        "127.0.0.1:1",
        "127.0.0.1:1@evil",
        "127.0.0.1#",
        "127.0.0.1/path",
        "127.0.0.1?x=1",
        "http://127.0.0.1",
        "::1",
        "",
        "\n",
    ],
)
def test_illegal_hosts_are_rejected(host):
    with pytest.raises(ValueError):
        _target_address({"host": host, "port": 6041})


async def test_trailing_newline_host_is_a_rejected_target(tmp_path, monkeypatch):
    """R2 回归（端到端）：`"127.0.0.1\\n"` 既不得被视为「同一目标」而被接受。"""
    stored = RedisReceiver()
    try:
        _write_config(
            tmp_path,
            monkeypatch,
            "redis",
            {"host": "127.0.0.1", "port": stored.port, "password_enc": dpapi_encrypt(STORED_SECRET)},
        )
        status, _response = await _run(_request("redis", host="127.0.0.1\n"))
        assert status == 400, "尾随换行 host 与存储目标文本不同，必须先判为变更并拒绝"
        assert stored.payload() == b""
    finally:
        stored.close()


def test_trailing_newline_host_never_connects():
    """R2 回归（checker 级）：`_target_address` 直接拒绝，且不发起任何连接。

    端口合法且直连目标就在本机回环：若地址校验失效，请求会真的落到 `direct`
    （`check_tdengine` 是同步调用，返回时连接尝试已经结束）。
    """
    direct = HttpRecorder()
    try:
        result = check_tdengine(
            {"host": "127.0.0.1\n", "port": direct.port, "user": "root", "password": STORED_SECRET}
        )
        assert result.success is False
        assert direct.requests == []
    finally:
        direct.close()
