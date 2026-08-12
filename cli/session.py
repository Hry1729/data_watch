from __future__ import annotations

from types import TracebackType
from typing import Any, Protocol

from cli.client import BackendClient
from cli.config import RuntimeSettings
from cli.credentials import CredentialStore, StoredCredentials
from cli.errors import CliError, EXIT_API, EXIT_AUTH_REQUIRED, EXIT_PERMISSION_DENIED
from cli.permissions import CommandPolicy, get_command_policy, is_command_allowed


class SessionState(Protocol):
    credential_store: CredentialStore

    def runtime(self) -> RuntimeSettings: ...


class AuthorizedSession:
    def __init__(self, state: SessionState) -> None:
        self.state = state
        self.runtime: RuntimeSettings | None = None
        self.credentials: StoredCredentials | None = None
        self.client: BackendClient | None = None
        self._user_info: dict[str, Any] | None = None
        self._resolver: Any | None = None

    def __enter__(self) -> "AuthorizedSession":
        self.runtime = self.state.runtime()
        self.credentials = self.state.credential_store.load(
            self.runtime.profile,
            expected_base_url=self.runtime.base_url,
        )
        if self.credentials is None:
            raise CliError("auth_required", "请先执行 auth login", EXIT_AUTH_REQUIRED)
        self.client = BackendClient(
            self.runtime.base_url,
            token=self.credentials.token,
            timeout_seconds=self.runtime.timeout_seconds,
            refresh_token=self.credentials.refresh_token,
            token_refreshed=self._store_refreshed_credentials,
        )
        return self

    def __exit__(
        self,
        _exc_type: type[BaseException] | None,
        _exc_value: BaseException | None,
        _traceback: TracebackType | None,
    ) -> None:
        if self.client is not None:
            self.client.close()

    def request(
        self,
        method: str,
        path: str,
        *,
        params: Any = None,
        json_body: dict[str, Any] | None = None,
    ) -> Any:
        if self.client is None:
            raise CliError("internal_error", "认证会话未初始化", EXIT_API)
        normalized_method = method.upper()
        idempotency_key = getattr(self.state, "idempotency_key", None)
        if getattr(self.state, "dry_run", False) and normalized_method not in {"GET", "HEAD"}:
            return {
                "dryRun": True,
                "request": {
                    "method": normalized_method,
                    "path": path,
                    "params": params,
                    "jsonBody": _redact(json_body),
                    "idempotencyKey": idempotency_key,
                },
            }
        headers = (
            {"X-Idempotency-Key": idempotency_key}
            if idempotency_key and normalized_method not in {"GET", "HEAD"}
            else None
        )
        if (
            normalized_method == "GET"
            and getattr(self.state, "all_pages", False)
            and isinstance(params, dict)
            and (
                {"pageNo", "pageSize"}.issubset(params)
                or {"current", "size"}.issubset(params)
            )
        ):
            return self._request_all(path, params=params)
        return self.client.request(
            normalized_method,
            path,
            params=params,
            json_body=json_body,
            headers=headers,
        )

    def _request_all(self, path: str, *, params: dict[str, Any]) -> Any:
        if self.client is None:
            raise CliError("internal_error", "认证会话未初始化", EXIT_API)
        page_key, size_key = (
            ("pageNo", "pageSize")
            if "pageNo" in params
            else ("current", "size")
        )
        page = 1
        max_items = int(getattr(self.state, "max_items", 10000))
        collected: list[Any] = []
        first: dict[str, Any] | None = None
        item_key: str | None = None
        truncated = False
        while True:
            page_params = {**params, page_key: page}
            payload = self.client.request("GET", path, params=page_params)
            if not isinstance(payload, dict):
                return payload
            if first is None:
                first = payload
                item_key = next(
                    (
                        key
                        for key in ("list", "records", "items")
                        if isinstance(payload.get(key), list)
                    ),
                    None,
                )
                if item_key is None:
                    return payload
            items = payload.get(item_key)
            if not isinstance(items, list):
                raise CliError("api_error", "分页响应格式错误", EXIT_API)
            remaining = max_items - len(collected)
            collected.extend(items[:remaining])
            total = int(payload.get("total") or len(collected))
            if len(items) > remaining or len(collected) < total and len(collected) >= max_items:
                truncated = True
                break
            if not items or len(collected) >= total:
                break
            page += 1

        result = {**(first or {})}
        for key in ("list", "records", "items"):
            if isinstance(result.get(key), list):
                result[key] = collected
        result.update(
            {
                "total": int((first or {}).get("total") or len(collected)),
                "fetched": len(collected),
                "allPages": True,
                "truncated": truncated,
                page_key: 1,
                size_key: len(collected),
            }
        )
        return result

    def download_to(
        self,
        path: str,
        output: str,
        *,
        params: Any = None,
    ) -> dict[str, Any]:
        if self.client is None:
            raise CliError("internal_error", "认证会话未初始化", EXIT_API)
        return self.client.download_to(path, output, params=params)

    def download(self, path: str, *, params: Any = None) -> tuple[bytes, str]:
        if self.client is None:
            raise CliError("internal_error", "认证会话未初始化", EXIT_API)
        return self.client.download(path, params=params)

    def user_info(self) -> dict[str, Any]:
        if self._user_info is None:
            if self.client is None:
                raise CliError("internal_error", "认证会话未初始化", EXIT_API)
            self._user_info = self.client.get_user_info()
        return self._user_info

    def authorize(self, command: str) -> dict[str, Any]:
        policy = get_command_policy(command)
        if policy is None:
            raise CliError(
                "internal_error",
                f"命令缺少权限策略: {command}",
                EXIT_API,
            )
        user_info = self.user_info()
        if not is_command_allowed(policy, user_info):
            raise self._permission_error(policy)
        return user_info

    def has_any_permission(self, *permissions: str) -> bool:
        granted = {
            str(value)
            for value in self.user_info().get("permissions", [])
            if str(value).strip()
        }
        return bool(granted.intersection(permissions))

    @property
    def resolver(self):
        if self._resolver is None:
            from cli.resolvers import Resolver

            self._resolver = Resolver(self)
        return self._resolver

    def _store_refreshed_credentials(
        self,
        token: str,
        refresh_token: str | None,
    ) -> None:
        if self.runtime is None or self.credentials is None:
            return
        source = self.credentials.source
        self.credentials = StoredCredentials(
            token=token,
            refresh_token=refresh_token,
            user_name=self.credentials.user_name,
            base_url=self.runtime.base_url,
            source=source,
        )
        if source == "keyring":
            self.state.credential_store.save(
                self.runtime.profile,
                token=token,
                refresh_token=refresh_token,
                user_name=self.credentials.user_name,
                base_url=self.runtime.base_url,
            )

    @staticmethod
    def _permission_error(policy: CommandPolicy) -> CliError:
        return CliError(
            "permission_denied",
            f"当前账号不能执行 {policy.command}",
            EXIT_PERMISSION_DENIED,
            status=403,
            details={
                "requiredPermissionsAny": sorted(policy.permissions_any),
                "requiredRolesAny": sorted(policy.roles_any),
            },
        )

_SENSITIVE_FIELDS = {"password", "token", "refreshtoken", "secret", "authorization"}


def _redact(value: Any) -> Any:
    if isinstance(value, dict):
        return {
            str(key): "***" if str(key).lower() in _SENSITIVE_FIELDS else _redact(item)
            for key, item in value.items()
        }
    if isinstance(value, list):
        return [_redact(item) for item in value]
    return value
