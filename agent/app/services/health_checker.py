from __future__ import annotations

import base64
import json
import urllib.request
from typing import Any, Mapping

import pymysql
import redis

from app.core.security import dpapi_decrypt
from app.schemas.models import HealthCheckResponse

TIMEOUT_SECONDS = 3


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
        client = redis.Redis(
            host=config.get("host"),
            port=int(config.get("port")),
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
        connection = pymysql.connect(
            host=config.get("host"),
            port=int(config.get("port")),
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
        host = config.get("host")
        port = int(config.get("port"))
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

        with urllib.request.urlopen(request, timeout=TIMEOUT_SECONDS) as response:
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
