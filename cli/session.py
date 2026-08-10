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
        self.credentials = self.state.credential_store.load(self.runtime.profile)
        if self.credentials is None:
            raise CliError("auth_required", "请先执行 auth login", EXIT_AUTH_REQUIRED)
        self.client = BackendClient(
            self.runtime.base_url,
            token=self.credentials.token,
            timeout_seconds=self.runtime.timeout_seconds,
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
        return self.client.request(
            method,
            path,
            params=params,
            json_body=json_body,
        )

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
