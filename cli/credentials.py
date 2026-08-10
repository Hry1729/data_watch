from __future__ import annotations

import json
import os
from dataclasses import dataclass
from typing import Any, Mapping

from cli.errors import CliError, EXIT_API


KEYRING_SERVICE = "DataWatchCLI"


@dataclass(frozen=True)
class StoredCredentials:
    token: str
    refresh_token: str | None = None
    user_name: str | None = None
    source: str = "keyring"


class CredentialStore:
    def __init__(
        self,
        *,
        environ: Mapping[str, str] | None = None,
        keyring_backend: Any | None = None,
    ) -> None:
        self.environ = environ if environ is not None else os.environ
        self._keyring_backend = keyring_backend

    def load(self, profile: str) -> StoredCredentials | None:
        environment_token = str(self.environ.get("DATAWATCH_TOKEN") or "").strip()
        if environment_token:
            return StoredCredentials(
                token=environment_token,
                refresh_token=self.environ.get("DATAWATCH_REFRESH_TOKEN"),
                source="environment",
            )

        backend = self._backend()
        try:
            raw_value = backend.get_password(KEYRING_SERVICE, profile)
        except Exception as exc:
            raise CliError("credential_error", "无法读取系统凭据存储", EXIT_API) from exc
        if not raw_value:
            return None
        try:
            payload = json.loads(raw_value)
        except ValueError as exc:
            raise CliError("credential_error", "系统凭据内容格式错误", EXIT_API) from exc
        token = str(payload.get("token") or "").strip()
        if not token:
            return None
        return StoredCredentials(
            token=token,
            refresh_token=str(payload.get("refreshToken") or "").strip() or None,
            user_name=str(payload.get("userName") or "").strip() or None,
        )

    def save(
        self,
        profile: str,
        *,
        token: str,
        refresh_token: str | None,
        user_name: str | None,
    ) -> None:
        normalized_token = token.strip()
        if not normalized_token:
            raise CliError("credential_error", "访问令牌不能为空", EXIT_API)
        payload = json.dumps(
            {
                "token": normalized_token,
                "refreshToken": refresh_token,
                "userName": user_name,
            },
            ensure_ascii=False,
        )
        try:
            self._backend().set_password(KEYRING_SERVICE, profile, payload)
        except Exception as exc:
            raise CliError("credential_error", "无法保存到系统凭据存储", EXIT_API) from exc

    def clear(self, profile: str) -> bool:
        backend = self._backend()
        try:
            existing = backend.get_password(KEYRING_SERVICE, profile)
            if existing is None:
                return False
            backend.delete_password(KEYRING_SERVICE, profile)
            return True
        except Exception as exc:
            raise CliError("credential_error", "无法清理系统凭据存储", EXIT_API) from exc

    def _backend(self) -> Any:
        if self._keyring_backend is not None:
            return self._keyring_backend
        try:
            import keyring
        except ImportError as exc:
            raise CliError(
                "credential_error",
                "缺少 keyring 依赖，无法安全保存登录凭据",
                EXIT_API,
            ) from exc
        self._keyring_backend = keyring
        return self._keyring_backend
