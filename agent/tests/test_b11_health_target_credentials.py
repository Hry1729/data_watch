"""B11：Agent 健康检查把已有凭据用于可覆写的新目标 —— 复现与回归。

独立文件（不改动 tests/conftest.py）。全部接收端都是 **本机回环临时端口上的隔离
接收端**，凭据一律使用测试哨兵值，不连接现场 Redis/MySQL/TDengine 或任何真实库。

断言刻意避免回显捕获到的字节：只断言长度/包含关系，防止把哨兵口令打进测试输出。
"""

import base64
import json
import secrets
import socket
import threading
import toml
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import pytest
from fastapi import HTTPException

from app.core import config
from app.core.security import dpapi_encrypt
from app.routers.admin import health_check
from app.schemas.models import HealthCheckConfig, HealthCheckRequest
from app.services.health_checker import check_mysql, check_redis, check_tdengine

STORED_SECRET = "TESTONLY_stored_pw_sentinel"
TEMP_SECRET = "TESTONLY_temp_pw_sentinel"
STRONG_JWT_SECRET = secrets.token_hex(32)


@pytest.fixture(autouse=True)
def _isolated_data_root(tmp_path, monkeypatch):
    """安全守卫：B11 全部夹具必须落在临时目录（绝不读写真实安装目录）。

    本机存在真实安装 `C:\\ProgramData\\DataWatch Edge`；这里显式把
    DATAWATCH_DATA_ROOT 与 CONFIG_PATH 钉到 pytest tmp_path，并在用例结束后断言
    配置路径没有越界。
    """
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    monkeypatch.delenv("DATAWATCH_INSTALL_ROOT", raising=False)
    monkeypatch.setattr(config, "CONFIG_PATH", tmp_path / "config.toml")
    assert config.data_root() == tmp_path

    yield

    resolved = Path(config.CONFIG_PATH).resolve()
    assert tmp_path.resolve() in resolved.parents, f"配置路径越界: {resolved}"


# ── 隔离接收端 1：记录 TCP 收到的字节（用于「新目标一个字节都收不到」断言） ────


def _pop_resp_command(buffer: bytes):
    """从 buffer 取出一个完整的 RESP 数组命令；不完整则返回 (None, buffer)。"""
    if not buffer.startswith(b"*"):
        return None, buffer
    end = buffer.find(b"\r\n")
    if end < 0:
        return None, buffer
    try:
        count = int(buffer[1:end])
    except ValueError:
        return None, buffer
    pos = end + 2
    for _ in range(count):
        if buffer[pos : pos + 1] != b"$":
            return None, buffer
        eol = buffer.find(b"\r\n", pos)
        if eol < 0:
            return None, buffer
        try:
            length = int(buffer[pos + 1 : eol])
        except ValueError:
            return None, buffer
        start = eol + 2
        if len(buffer) < start + length + 2:
            return None, buffer
        pos = start + length + 2
    # 只求尽快结束握手、避免测试等超时；本接收端不承担「真实 Redis」语义，
    # 相关用例只断言「收到了多少字节」。
    return b"+PONG\r\n", buffer[pos:]


class RedisReceiver:
    """隔离接收端：记录客户端发送的全部字节。"""

    def __init__(self) -> None:
        self.received: list[bytes] = []
        self.connections = 0
        self._stop = threading.Event()
        self._sock = socket.socket()
        self._sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._sock.bind(("127.0.0.1", 0))
        self._sock.listen(8)
        self._sock.settimeout(0.2)
        self.port = self._sock.getsockname()[1]
        threading.Thread(target=self._accept_loop, daemon=True).start()

    def _accept_loop(self) -> None:
        while not self._stop.is_set():
            try:
                conn, _addr = self._sock.accept()
            except (socket.timeout, OSError):
                continue
            self.connections += 1
            threading.Thread(target=self._serve, args=(conn,), daemon=True).start()

    def _serve(self, conn: socket.socket) -> None:
        conn.settimeout(0.3)
        buffer = b""
        try:
            while not self._stop.is_set():
                try:
                    chunk = conn.recv(4096)
                except (socket.timeout, OSError):
                    break
                if not chunk:
                    break
                self.received.append(chunk)
                buffer += chunk
                while True:
                    reply, buffer = _pop_resp_command(buffer)
                    if reply is None:
                        break
                    try:
                        conn.sendall(reply)
                    except OSError:
                        return
        finally:
            try:
                conn.close()
            except OSError:
                pass

    def payload(self) -> bytes:
        return b"".join(self.received)

    def close(self) -> None:
        self._stop.set()
        try:
            self._sock.close()
        except OSError:
            pass


# ── 隔离接收端 2：记录 HTTP 请求头，可返回 302 到另一个 origin ────────────────


def _basic_credentials(header: str | None) -> str:
    """把 `Basic base64(user:pass)` 还原为 `user:pass`。"""
    if not header or not header.startswith("Basic "):
        return ""
    return base64.b64decode(header.removeprefix("Basic ")).decode("utf-8")


class HttpRecorder:
    def __init__(self, status: int = 200, location: str | None = None) -> None:
        self.requests: list[dict] = []
        self._body = json.dumps({"code": 0, "status": "succ"}).encode()
        outer = self

        class Handler(BaseHTTPRequestHandler):
            protocol_version = "HTTP/1.1"

            def _record(self) -> None:
                length = int(self.headers.get("Content-Length") or 0)
                if length:
                    self.rfile.read(length)
                outer.requests.append(
                    {
                        "path": self.path,
                        "host": self.headers.get("Host"),
                        "authorization": self.headers.get("Authorization"),
                    }
                )

            def do_POST(self) -> None:  # noqa: N802 - http.server 约定
                self._record()
                if location:
                    self.send_response(302)
                    self.send_header("Location", location)
                    self.send_header("Content-Length", "0")
                else:
                    self.send_response(status)
                    self.send_header("Content-Length", str(len(outer._body)))
                self.end_headers()
                if not location:
                    self.wfile.write(outer._body)

            # 302 会把 POST 改写成 GET：必须照样记录，否则重定向用例会假通过。
            do_GET = do_POST

            def log_message(self, *_args) -> None:  # 静默，避免测试输出噪声
                pass

        self._server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self._server.daemon_threads = True
        self.port = self._server.server_address[1]
        threading.Thread(target=self._server.serve_forever, daemon=True).start()

    def authorization_headers(self) -> list[str]:
        return [r["authorization"] for r in self.requests if r["authorization"]]

    def close(self) -> None:
        self._server.shutdown()
        self._server.server_close()


# ── 夹具：把真实 CONFIG_PATH 指向临时配置（含哨兵存储凭据） ───────────────────


def _write_config(tmp_path, monkeypatch, target: str, host: str, port: int, secret: str = STORED_SECRET):
    section = {"host": host, "port": port, "password_enc": dpapi_encrypt(secret) if secret else ""}
    if target != "redis":
        section["user"] = "root"
        section["database"] = "device_data" if target == "tdengine" else "device_watch"
    path = tmp_path / "config.toml"
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


def _request(target: str, **overrides) -> HealthCheckRequest:
    return HealthCheckRequest(
        target=target,
        config=HealthCheckConfig(**overrides) if overrides else None,
    )


async def _run(request: HealthCheckRequest):
    """以真实路由函数为入口，返回 (http_status_or_None, response_or_None)。"""
    try:
        return None, await health_check(request, _user="admin")
    except HTTPException as exc:
        return exc.status_code, None


# ── 验收 2：改变 host/port 拿不到原存储凭据 ───────────────────────────────────


async def test_new_target_receiver_captures_nothing_when_host_and_port_change(
    tmp_path, monkeypatch
):
    """复现点：host/port 被改写时，新目标隔离接收端不得收到任何存储凭据。"""
    stored = RedisReceiver()
    new_target = RedisReceiver()
    try:
        _write_config(tmp_path, monkeypatch, "redis", "127.0.0.1", stored.port)
        status, _response = await _run(
            _request("redis", host="localhost", port=new_target.port)
        )
        captured = new_target.payload()
        assert len(captured) == 0, f"目标变更后新接收端仍收到 {len(captured)} 字节"
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
async def test_credential_binding_covers_every_target_identifier(
    tmp_path, monkeypatch, overrides
):
    stored = RedisReceiver()
    try:
        _write_config(tmp_path, monkeypatch, "redis", "127.0.0.1", stored.port)
        status, _response = await _run(_request("redis", **overrides))
        assert len(stored.payload()) == 0, "目标变更时不得沿用存储凭据"
        assert status == 400
    finally:
        stored.close()


@pytest.mark.parametrize(
    "host",
    [
        "LOCALHOST",
        "127.0.0.1.",
        "2130706433",  # 十进制形式的 127.0.0.1
        "0177.0.0.1",  # 八进制形式
        "[::1]",
        "127.0.0.1:1@evil",
        "127.0.0.1:1",
    ],
)
async def test_host_textual_variants_are_treated_as_new_targets(
    tmp_path, monkeypatch, host
):
    """反证：任何 host 文本差异都算「新目标」，不得沿用存储凭据。"""
    stored = RedisReceiver()
    new_target = RedisReceiver()
    try:
        _write_config(tmp_path, monkeypatch, "redis", "127.0.0.1", stored.port)
        status, _response = await _run(_request("redis", host=host, port=new_target.port))
        assert len(new_target.payload()) == 0, f"host={host} 时新接收端收到字节"
        assert len(stored.payload()) == 0
        assert status == 400
    finally:
        stored.close()
        new_target.close()


# ── 验收 3：合法已配置目标的一键检查仍正常 ────────────────────────────────────


async def test_unchanged_stored_target_check_still_succeeds(tmp_path, monkeypatch):
    """未覆盖任何目标字段时，存盘目标的一键检查必须照常成功。"""
    origin = HttpRecorder()
    try:
        _write_config(tmp_path, monkeypatch, "tdengine", "127.0.0.1", origin.port)
        status, response = await _run(_request("tdengine"))

        assert status is None
        assert response is not None and response.success is True
        assert _basic_credentials(origin.authorization_headers()[0]).endswith(
            f":{STORED_SECRET}"
        )
    finally:
        origin.close()


async def test_changed_target_with_explicit_temporary_credential_succeeds(
    tmp_path, monkeypatch
):
    """显式临时凭据 + 新目标：检查照常可用，且新目标拿到的是临时凭据。"""
    stored = HttpRecorder()
    new_target = HttpRecorder()
    try:
        _write_config(tmp_path, monkeypatch, "tdengine", "127.0.0.1", stored.port)
        status, response = await _run(
            _request(
                "tdengine",
                host="127.0.0.1",
                port=new_target.port,
                password=TEMP_SECRET,
            )
        )

        assert status is None
        assert response is not None and response.success is True
        assert new_target.requests, "目标变更后检查本应照常执行"
        assert _basic_credentials(new_target.authorization_headers()[0]).endswith(
            f":{TEMP_SECRET}"
        )
        assert all(
            TEMP_SECRET not in _basic_credentials(h)
            for h in stored.authorization_headers()
        )
        assert stored.requests == []
    finally:
        stored.close()
        new_target.close()


async def test_changed_target_without_stored_credential_is_not_blocked(
    tmp_path, monkeypatch
):
    """存储凭据为空串（无秘密可泄漏）时不得把检查变成「必须人工授权」。"""
    new_target = HttpRecorder()
    try:
        _write_config(
            tmp_path, monkeypatch, "tdengine", "127.0.0.1", 1, secret=""
        )
        status, response = await _run(
            _request("tdengine", host="127.0.0.1", port=new_target.port)
        )

        assert status is None
        assert response is not None and response.success is True
    finally:
        new_target.close()


# ── 验收 5（补充）：TDengine 跨源重定向不得携带凭据 ───────────────────────────


async def test_tdengine_redirect_does_not_forward_credential_to_other_origin(
    tmp_path, monkeypatch
):
    sink = HttpRecorder()
    origin = HttpRecorder(location=f"http://127.0.0.1:{sink.port}/rest/sql")
    try:
        _write_config(tmp_path, monkeypatch, "tdengine", "127.0.0.1", origin.port)
        _status, response = await _run(_request("tdengine"))

        # 初始请求必须真的带上了凭据，否则本用例空转
        assert origin.authorization_headers(), "初始请求必须携带凭据"
        assert sink.authorization_headers() == []
        assert sink.requests == []
        assert response is not None and response.success is False
    finally:
        origin.close()
        sink.close()


# ── 验收：限定协议/地址/端口 ──────────────────────────────────────────────────


def test_host_smuggling_another_port_is_rejected_before_connecting():
    """host 中夹带 `#`/端口不得把凭据发往未授权端口。"""
    sink = HttpRecorder()
    try:
        result = check_tdengine(
            {
                "host": f"127.0.0.1:{sink.port}#",
                "port": 6041,
                "user": "root",
                "password": STORED_SECRET,
            }
        )
        assert result.success is False
        assert sink.requests == []
    finally:
        sink.close()


def test_address_and_port_are_limited():
    from app.services.health_checker import _target_address

    assert _target_address({"host": "127.0.0.1", "port": 6041}) == ("127.0.0.1", 6041)
    assert _target_address({"host": "db.example.com", "port": 1}) == ("db.example.com", 1)
    assert _target_address({"host": "[::1]", "port": 65535}) == ("[::1]", 65535)

    for bad in (
        {"host": "http://127.0.0.1", "port": 6041},
        {"host": "127.0.0.1/rest/sql", "port": 6041},
        {"host": "root:pw@127.0.0.1", "port": 6041},
        {"host": "127.0.0.1#", "port": 6041},
        {"host": "", "port": 6041},
        {"host": None, "port": 6041},
        {"host": "127.0.0.1", "port": 0},
        {"host": "127.0.0.1", "port": 65536},
        {"host": "127.0.0.1", "port": "not-a-port"},
    ):
        with pytest.raises(ValueError):
            _target_address(bad)


def test_checkers_report_failure_instead_of_leaking_on_bad_target():
    for checker in (check_redis, check_mysql, check_tdengine):
        result = checker(
            {"host": "root:pw@127.0.0.1", "port": 1, "user": "u", "password": "p"}
        )
        assert result.success is False
        assert result.message is not None
        assert "pw" not in result.message


# ── 验收 4：审计/错误记录不含秘密 ─────────────────────────────────────────────


async def test_denial_records_contain_no_secret(tmp_path, monkeypatch, caplog):
    stored = RedisReceiver()
    try:
        _write_config(tmp_path, monkeypatch, "redis", "127.0.0.1", stored.port)
        with caplog.at_level("DEBUG"):
            status, _response = await _run(
                _request("redis", host="127.0.0.1", port=stored.port + 1)
            )
        assert status == 400
        assert STORED_SECRET not in caplog.text
        assert TEMP_SECRET not in caplog.text
        assert "password_enc" not in caplog.text
    finally:
        stored.close()
