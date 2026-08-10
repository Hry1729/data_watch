from __future__ import annotations

from email.message import Message
from email.utils import collapse_rfc2231_value
from typing import Any

import httpx

from cli.errors import (
    CliError,
    EXIT_API,
    EXIT_AUTH_REQUIRED,
    EXIT_NETWORK,
    EXIT_PERMISSION_DENIED,
    EXIT_TIMEOUT,
)


class BackendClient:
    def __init__(
        self,
        base_url: str,
        *,
        token: str | None = None,
        timeout_seconds: float = 15.0,
        transport: httpx.BaseTransport | None = None,
    ) -> None:
        self.base_url = base_url.rstrip("/")
        self.token = token
        self._client = httpx.Client(
            base_url=self.base_url,
            timeout=timeout_seconds,
            transport=transport,
        )

    def __enter__(self) -> "BackendClient":
        return self

    def __exit__(self, *_args: object) -> None:
        self.close()

    def close(self) -> None:
        self._client.close()

    def login(self, user_name: str, password: str) -> dict[str, Any]:
        data = self.request(
            "POST",
            "/api/auth/login",
            json_body={"userName": user_name, "password": password},
            authenticated=False,
        )
        if not isinstance(data, dict) or not str(data.get("token") or "").strip():
            raise CliError("api_error", "登录响应缺少访问令牌", EXIT_API)
        return data

    def logout(self) -> Any:
        return self.request("POST", "/api/auth/logout")

    def get_user_info(self) -> dict[str, Any]:
        data = self.request("GET", "/api/user/info")
        if not isinstance(data, dict):
            raise CliError("api_error", "用户信息响应格式错误", EXIT_API)
        return data

    def health(self) -> dict[str, Any]:
        data = self.request("GET", "/healthz", authenticated=False)
        if not isinstance(data, dict):
            raise CliError("api_error", "健康检查响应格式错误", EXIT_API)
        return data

    def request(
        self,
        method: str,
        path: str,
        *,
        params: Any = None,
        json_body: dict[str, Any] | None = None,
        authenticated: bool = True,
    ) -> Any:
        response = self._send(
            method,
            path,
            params=params,
            json_body=json_body,
            authenticated=authenticated,
        )
        payload = self._json_payload(response)
        code = self._coerce_code(payload.get("code"), response.status_code)
        self._raise_for_error(response, payload, code)
        return payload.get("data")

    def download(
        self,
        path: str,
        *,
        params: Any = None,
    ) -> tuple[bytes, str]:
        response = self._send("GET", path, params=params, authenticated=True)
        if response.is_error:
            try:
                payload = response.json()
            except ValueError as exc:
                raise CliError(
                    "api_error",
                    "Backend 下载失败",
                    EXIT_API,
                    status=response.status_code,
                ) from exc
            if not isinstance(payload, dict):
                raise CliError(
                    "api_error",
                    "Backend 下载错误格式无效",
                    EXIT_API,
                    status=response.status_code,
                )
            code = self._coerce_code(payload.get("code"), response.status_code)
            self._raise_for_error(response, payload, code)
        return response.content, self._response_filename(response)

    def _send(
        self,
        method: str,
        path: str,
        *,
        params: Any = None,
        json_body: dict[str, Any] | None = None,
        authenticated: bool,
    ) -> httpx.Response:
        headers: dict[str, str] = {"Accept": "application/json"}
        if authenticated:
            if not self.token:
                raise CliError("auth_required", "请先登录", EXIT_AUTH_REQUIRED)
            headers["Authorization"] = self._authorization_value(self.token)
        try:
            return self._client.request(
                method,
                path,
                params=params,
                json=json_body,
                headers=headers,
            )
        except httpx.TimeoutException as exc:
            raise CliError("timeout", "Backend 请求超时", EXIT_TIMEOUT) from exc
        except httpx.RequestError as exc:
            raise CliError("network_error", "无法连接到 Backend 服务", EXIT_NETWORK) from exc

    @staticmethod
    def _json_payload(response: httpx.Response) -> dict[str, Any]:
        try:
            payload = response.json()
        except ValueError as exc:
            raise CliError(
                "api_error",
                "Backend 响应不是合法 JSON",
                EXIT_API,
                status=response.status_code,
            ) from exc
        if not isinstance(payload, dict):
            raise CliError(
                "api_error",
                "Backend 响应格式错误",
                EXIT_API,
                status=response.status_code,
            )
        return payload

    @staticmethod
    def _raise_for_error(
        response: httpx.Response,
        payload: dict[str, Any],
        code: int,
    ) -> None:
        message = str(payload.get("msg") or payload.get("detail") or "请求失败")
        if response.status_code == 401 or code == 401:
            raise CliError("unauthorized", message, EXIT_AUTH_REQUIRED, status=401)
        if response.status_code == 403 or code == 403:
            raise CliError("permission_denied", message, EXIT_PERMISSION_DENIED, status=403)
        if response.is_error or code != 200:
            raise CliError("api_error", message, EXIT_API, status=code)

    @staticmethod
    def _response_filename(response: httpx.Response) -> str:
        header = response.headers.get("Content-Disposition")
        if not header:
            return "download.bin"
        message = Message()
        message["Content-Disposition"] = header
        filename = message.get_filename()
        if isinstance(filename, tuple):
            filename = collapse_rfc2231_value(filename)
        normalized = str(filename or "download.bin").replace("\\", "/").split("/")[-1]
        return normalized or "download.bin"

    @staticmethod
    def _authorization_value(token: str) -> str:
        normalized = token.strip()
        if normalized.lower().startswith("bearer "):
            return normalized
        return f"Bearer {normalized}"

    @staticmethod
    def _coerce_code(value: Any, fallback: int) -> int:
        if value is None:
            return fallback
        try:
            return int(value)
        except (TypeError, ValueError):
            return fallback
