from __future__ import annotations

import base64
import json
import re
import urllib.error
import urllib.request
from typing import Any, Mapping

import pymysql
import redis

from app.core.security import dpapi_decrypt
from app.schemas.models import HealthCheckResponse

TIMEOUT_SECONDS = 3

# B11：限定目标地址/端口。只接受主机名、IPv4 字面量或方括号 IPv6 字面量，
# 从而排除 scheme（http://）、userinfo（user:pw@）、path/query/fragment（/@#?）。
# 用 \Z 而非 $：$ 允许尾随换行（"127.0.0.1\n" 会被接受），\Z 才是字符串真正结尾。
_HOST_PATTERN = re.compile(r"^[A-Za-z0-9._-]+\Z")
_IPV6_PATTERN = re.compile(r"^\[[0-9A-Fa-f:.]+\]\Z")


def _target_address(config: Mapping[str, Any]) -> tuple[str, int]:
    """校验并返回 (host, port)；不合格抛 ValueError（调用方统一转为检查失败）。"""
    host = str(config.get("host") or "")
    if not (_HOST_PATTERN.match(host) or _IPV6_PATTERN.match(host)):
        raise ValueError("invalid target host")
    try:
        port = int(config.get("port"))
    except (TypeError, ValueError) as exc:
        raise ValueError("invalid target port") from exc
    if not 1 <= port <= 65535:
        raise ValueError("invalid target port")
    return host, port


class _NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    """B11：TDengine 凭据不得随重定向离开原目标 origin（urllib 会转发 Authorization）。"""

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise urllib.error.HTTPError(
            req.full_url, code, "redirect not allowed", headers, fp
        )


class _NoProxyHandler(urllib.request.ProxyHandler):
    """B11-R3：健康检查不得把凭据交给 `HTTP_PROXY` 等环境变量指定的代理。

    `urllib` 默认按 `getproxies()` 决定是否经代理发送请求（含 `Authorization`）。
    这里显式声明空代理表：只影响本 opener，不改动进程的代理环境变量。
    """

    def __init__(self) -> None:
        super().__init__({})


_TDENGINE_OPENER = urllib.request.build_opener(_NoRedirectHandler, _NoProxyHandler())


def _decrypt_password(config: Mapping[str, Any]) -> str:
    if "password" in config and config.get("password") is not None:
        return str(config.get("password") or "")
    return dpapi_decrypt(str(config.get("password_enc") or ""))


def check_backend(base_url: str) -> HealthCheckResponse:
    """探测后端可达性：GET {base_url}/healthz 返回 200 即视为连通。"""
    url = (base_url or "").rstrip("/")
    if not url:
        return HealthCheckResponse(success=False, message="Backend base_url is empty")
    try:
        request = urllib.request.Request(url=f"{url}/healthz", method="GET")
        with urllib.request.urlopen(request, timeout=TIMEOUT_SECONDS) as response:
            if response.getcode() == 200:
                return HealthCheckResponse(success=True)
            return HealthCheckResponse(success=False, message="Backend health check failed")
    except Exception:
        return HealthCheckResponse(success=False, message="Backend health check failed")


def check_redis(config: Mapping[str, Any]) -> HealthCheckResponse:
    client = None
    try:
        host, port = _target_address(config)
        client = redis.Redis(
            host=host,
            port=port,
            password=_decrypt_password(config) or None,
            socket_connect_timeout=TIMEOUT_SECONDS,
            socket_timeout=TIMEOUT_SECONDS,
        )
        client.ping()
        return HealthCheckResponse(success=True)
    except Exception:
        return HealthCheckResponse(success=False, message="Redis health check failed")
    finally:
        if client is not None:
            try:
                client.close()
            except Exception:
                pass


def check_mysql(config: Mapping[str, Any]) -> HealthCheckResponse:
    connection = None
    try:
        host, port = _target_address(config)
        connection = pymysql.connect(
            host=host,
            port=port,
            user=config.get("user"),
            password=_decrypt_password(config),
            database=config.get("database"),
            connect_timeout=TIMEOUT_SECONDS,
            read_timeout=TIMEOUT_SECONDS,
            write_timeout=TIMEOUT_SECONDS,
            autocommit=True,
        )
        with connection.cursor() as cursor:
            cursor.execute("SELECT 1")
            cursor.fetchone()
        return HealthCheckResponse(success=True)
    except Exception:
        return HealthCheckResponse(success=False, message="MySQL health check failed")
    finally:
        if connection is not None:
            try:
                connection.close()
            except Exception:
                pass


def check_tdengine(config: Mapping[str, Any]) -> HealthCheckResponse:
    try:
        host, port = _target_address(config)
        user = str(config.get("user") or "")
        password = _decrypt_password(config)

        credentials = f"{user}:{password}".encode("utf-8")
        authorization = base64.b64encode(credentials).decode("ascii")

        request = urllib.request.Request(
            url=f"http://{host}:{port}/rest/sql",
            data=b"SHOW DATABASES",
            method="POST",
            headers={
                "Authorization": f"Basic {authorization}",
                "Content-Type": "text/plain; charset=utf-8",
            },
        )

        with _TDENGINE_OPENER.open(request, timeout=TIMEOUT_SECONDS) as response:
            if response.getcode() != 200:
                return HealthCheckResponse(success=False, message="TDengine health check failed")
            body = response.read()

        try:
            payload = json.loads(body.decode("utf-8"))
        except Exception:
            return HealthCheckResponse(success=False, message="TDengine health check failed")

        if payload.get("code") == 0 or payload.get("status") == "succ":
            return HealthCheckResponse(success=True)

        return HealthCheckResponse(success=False, message="TDengine health check failed")
    except Exception:
        return HealthCheckResponse(success=False, message="TDengine health check failed")
