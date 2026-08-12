from __future__ import annotations

from email.message import Message
from email.utils import collapse_rfc2231_value
from pathlib import Path
import time
from typing import Any, Callable

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
        refresh_token: str | None = None,
        token_refreshed: Callable[[str, str | None], None] | None = None,
        max_retries: int = 2,
        retry_backoff_seconds: float = 0.2,
    ) -> None:
        self.base_url = base_url.rstrip("/")
        self.token = token
        self.refresh_token = refresh_token
        self.token_refreshed = token_refreshed
        self.max_retries = max(0, int(max_retries))
        self.retry_backoff_seconds = max(0.0, float(retry_backoff_seconds))
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

    def refresh(self) -> dict[str, Any]:
        if not self.refresh_token:
            raise CliError("auth_required", "没有可用的刷新令牌，请重新登录", EXIT_AUTH_REQUIRED)
        data = self.request(
            "POST",
            "/api/auth/refresh",
            json_body={"refreshToken": self.refresh_token},
            authenticated=False,
        )
        if not isinstance(data, dict) or not str(data.get("token") or "").strip():
            raise CliError("api_error", "刷新令牌响应格式错误", EXIT_API)
        self.token = str(data["token"])
        self.refresh_token = str(data.get("refreshToken") or "").strip() or self.refresh_token
        if self.token_refreshed is not None:
            self.token_refreshed(self.token, self.refresh_token)
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

    def compatibility(self) -> dict[str, Any]:
        data = self.request("GET", "/api/cli/compatibility", authenticated=False)
        if not isinstance(data, dict):
            raise CliError("api_error", "兼容性响应格式错误", EXIT_API)
        return data

    def request(
        self,
        method: str,
        path: str,
        *,
        params: Any = None,
        json_body: dict[str, Any] | None = None,
        authenticated: bool = True,
        headers: dict[str, str] | None = None,
    ) -> Any:
        response = self._send(
            method,
            path,
            params=params,
            json_body=json_body,
            authenticated=authenticated,
            extra_headers=headers,
        )
        payload = self._json_payload(response)
        code = self._coerce_code(payload.get("code"), response.status_code)
        if authenticated and (response.status_code == 401 or code == 401) and self.refresh_token:
            self.refresh()
            response = self._send(
                method,
                path,
                params=params,
                json_body=json_body,
                authenticated=True,
                extra_headers=headers,
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
        authenticated: bool = True,
    ) -> tuple[bytes, str]:
        response = self._send(
            "GET",
            path,
            params=params,
            authenticated=authenticated,
        )
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

    def download_to(
        self,
        path: str,
        output: str | Path,
        *,
        params: Any = None,
        authenticated: bool = True,
        chunk_size: int = 1024 * 1024,
    ) -> dict[str, Any]:
        refreshed = False
        while True:
            headers: dict[str, str] = {"Accept": "application/octet-stream"}
            if authenticated:
                if not self.token:
                    raise CliError("auth_required", "请先登录", EXIT_AUTH_REQUIRED)
                headers["Authorization"] = self._authorization_value(self.token)
            try:
                with self._client.stream("GET", path, params=params, headers=headers) as response:
                    if (
                        authenticated
                        and response.status_code == 401
                        and self.refresh_token
                        and not refreshed
                    ):
                        response.read()
                        self.refresh()
                        refreshed = True
                        continue
                    if response.is_error:
                        response.read()
                        payload = self._json_payload(response)
                        code = self._coerce_code(payload.get("code"), response.status_code)
                        self._raise_for_error(response, payload, code)
                    filename = self._response_filename(response)
                    target = Path(output).expanduser()
                    if target.exists() and target.is_dir():
                        target = target / filename
                    target.parent.mkdir(parents=True, exist_ok=True)
                    temporary = target.with_suffix(target.suffix + ".tmp")
                    written = 0
                    try:
                        with temporary.open("wb") as handle:
                            for chunk in response.iter_bytes(chunk_size=chunk_size):
                                handle.write(chunk)
                                written += len(chunk)
                        temporary.replace(target)
                    except OSError as exc:
                        temporary.unlink(missing_ok=True)
                        raise CliError(
                            "file_error",
                            f"无法写入文件: {target}",
                            EXIT_API,
                        ) from exc
                    return {
                        "output": str(target.resolve()),
                        "fileName": filename,
                        "bytes": written,
                    }
            except httpx.TimeoutException as exc:
                raise CliError("timeout", "Backend 下载超时", EXIT_TIMEOUT) from exc
            except httpx.RequestError as exc:
                raise CliError("network_error", "Backend 下载连接失败", EXIT_NETWORK) from exc

    def _send(
        self,
        method: str,
        path: str,
        *,
        params: Any = None,
        json_body: dict[str, Any] | None = None,
        authenticated: bool,
        extra_headers: dict[str, str] | None = None,
    ) -> httpx.Response:
        headers: dict[str, str] = {"Accept": "application/json", **(extra_headers or {})}
        if authenticated:
            if not self.token:
                raise CliError("auth_required", "请先登录", EXIT_AUTH_REQUIRED)
            headers["Authorization"] = self._authorization_value(self.token)

        retryable = method.upper() in {"GET", "HEAD"}
        attempts = self.max_retries + 1 if retryable else 1
        for attempt in range(attempts):
            try:
                response = self._client.request(
                    method,
                    path,
                    params=params,
                    json=json_body,
                    headers=headers,
                )
            except httpx.TimeoutException as exc:
                if attempt + 1 < attempts:
                    time.sleep(self.retry_backoff_seconds * (2**attempt))
                    continue
                raise CliError("timeout", "Backend 请求超时", EXIT_TIMEOUT) from exc
            except httpx.RequestError as exc:
                if attempt + 1 < attempts:
                    time.sleep(self.retry_backoff_seconds * (2**attempt))
                    continue
                raise CliError("network_error", "无法连接到 Backend 服务", EXIT_NETWORK) from exc
            if response.status_code in {429, 502, 503, 504} and attempt + 1 < attempts:
                retry_after = response.headers.get("Retry-After")
                try:
                    delay = min(float(retry_after), 5.0) if retry_after else 0.0
                except ValueError:
                    delay = 0.0
                time.sleep(max(delay, self.retry_backoff_seconds * (2**attempt)))
                continue
            return response
        raise CliError("network_error", "无法连接到 Backend 服务", EXIT_NETWORK)

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
