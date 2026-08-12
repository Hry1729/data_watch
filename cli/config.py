from __future__ import annotations

import json
import os
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Mapping
from urllib.parse import urlsplit

from cli.errors import CliError, EXIT_USAGE


DEFAULT_PROFILE = "development"
DEFAULT_BASE_URL = "http://127.0.0.1:8000"
DEFAULT_TIMEOUT_SECONDS = 15.0
PROFILE_PATTERN = re.compile(r"^[A-Za-z0-9._-]+$")


@dataclass(frozen=True)
class RuntimeSettings:
    profile: str
    base_url: str
    timeout_seconds: float


class ConfigStore:
    def __init__(
        self,
        path: Path | None = None,
        *,
        environ: Mapping[str, str] | None = None,
    ) -> None:
        self.environ = environ if environ is not None else os.environ
        self.path = path or self._default_path()

    def load(self) -> dict:
        if not self.path.exists():
            return self._default_document()
        try:
            payload = json.loads(self.path.read_text(encoding="utf-8"))
        except (OSError, ValueError) as exc:
            raise CliError("config_error", "CLI 配置文件无法读取", EXIT_USAGE) from exc
        return self._validate_document(payload)

    def save(self, document: dict) -> None:
        validated = self._validate_document(document)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        temporary_path = self.path.with_suffix(self.path.suffix + ".tmp")
        content = json.dumps(validated, ensure_ascii=False, indent=2) + "\n"
        try:
            temporary_path.write_text(content, encoding="utf-8")
            temporary_path.replace(self.path)
        except OSError as exc:
            raise CliError("config_error", "CLI 配置文件无法保存", EXIT_USAGE) from exc

    def resolve(
        self,
        *,
        profile_override: str | None = None,
        api_url_override: str | None = None,
        timeout_override: float | None = None,
    ) -> RuntimeSettings:
        document = self.load()
        profile = (
            profile_override
            or self.environ.get("DATAWATCH_PROFILE")
            or str(document["currentProfile"])
        )
        profiles = document["profiles"]
        if profile not in profiles:
            raise CliError("config_error", f"Profile 不存在: {profile}", EXIT_USAGE)

        profile_data = profiles[profile]
        raw_url = (
            api_url_override
            or self.environ.get("DATAWATCH_API_URL")
            or profile_data["baseUrl"]
        )
        base_url = self.normalize_base_url(str(raw_url))
        timeout_seconds = self._resolve_timeout(profile_data, timeout_override)
        return RuntimeSettings(profile, base_url, timeout_seconds)

    def set_url(self, url: str, *, profile: str | None = None) -> dict:
        document = self.load()
        profile_name = profile or str(document["currentProfile"])
        if profile_name not in document["profiles"]:
            raise CliError("config_error", f"Profile 不存在: {profile_name}", EXIT_USAGE)
        document["profiles"][profile_name]["baseUrl"] = self.normalize_base_url(url)
        self.save(document)
        return self.public_document(document)

    def create_profile(self, name: str, url: str, *, timeout_seconds: float = 15.0) -> dict:
        self.validate_profile_name(name)
        document = self.load()
        if name in document["profiles"]:
            raise CliError("config_error", f"Profile 已存在: {name}", EXIT_USAGE)
        document["profiles"][name] = {
            "baseUrl": self.normalize_base_url(url),
            "output": "json",
            "timeoutSeconds": self.validate_timeout(timeout_seconds),
        }
        self.save(document)
        return self.public_document(document)

    def update_profile(
        self,
        name: str,
        *,
        url: str | None = None,
        timeout_seconds: float | None = None,
    ) -> dict:
        document = self.load()
        if name not in document["profiles"]:
            raise CliError("config_error", f"Profile 不存在: {name}", EXIT_USAGE)
        if url is None and timeout_seconds is None:
            raise CliError("config_error", "请至少提供 --url 或 --timeout", EXIT_USAGE)
        profile = document["profiles"][name]
        if url is not None:
            profile["baseUrl"] = self.normalize_base_url(url)
        if timeout_seconds is not None:
            profile["timeoutSeconds"] = self.validate_timeout(timeout_seconds)
        self.save(document)
        return self.public_document(document)

    def delete_profile(self, name: str) -> dict:
        document = self.load()
        profiles = document["profiles"]
        if name not in profiles:
            raise CliError("config_error", f"Profile 不存在: {name}", EXIT_USAGE)
        if len(profiles) == 1:
            raise CliError("config_error", "不能删除唯一的 Profile", EXIT_USAGE)
        del profiles[name]
        if document["currentProfile"] == name:
            document["currentProfile"] = sorted(profiles)[0]
        self.save(document)
        return self.public_document(document)

    def use_profile(self, name: str) -> dict:
        document = self.load()
        if name not in document["profiles"]:
            raise CliError("config_error", f"Profile 不存在: {name}", EXIT_USAGE)
        document["currentProfile"] = name
        self.save(document)
        return self.public_document(document)

    def public_document(self, document: dict | None = None) -> dict:
        validated = self._validate_document(document or self.load())
        return {
            "configPath": str(self.path),
            "currentProfile": validated["currentProfile"],
            "profiles": validated["profiles"],
        }

    def _default_path(self) -> Path:
        explicit = self.environ.get("DATAWATCH_CONFIG_FILE")
        if explicit:
            return Path(explicit).expanduser()
        app_data = self.environ.get("APPDATA")
        if app_data:
            return Path(app_data) / "DataWatch" / "cli-config.json"
        return Path.home() / ".data-watch" / "cli-config.json"

    def _resolve_timeout(self, profile_data: dict, override: float | None) -> float:
        if override is not None:
            return self.validate_timeout(override)
        environment_value = self.environ.get("DATAWATCH_TIMEOUT")
        if environment_value:
            try:
                return self.validate_timeout(float(environment_value))
            except ValueError as exc:
                raise CliError("config_error", "DATAWATCH_TIMEOUT 必须是数字", EXIT_USAGE) from exc
        return self.validate_timeout(profile_data.get("timeoutSeconds", DEFAULT_TIMEOUT_SECONDS))

    def _validate_document(self, payload: object) -> dict:
        if not isinstance(payload, dict):
            raise CliError("config_error", "CLI 配置根节点必须是对象", EXIT_USAGE)
        current_profile = str(payload.get("currentProfile") or DEFAULT_PROFILE)
        raw_profiles = payload.get("profiles")
        if not isinstance(raw_profiles, dict) or not raw_profiles:
            raise CliError("config_error", "CLI 配置缺少 profiles", EXIT_USAGE)

        profiles: dict[str, dict] = {}
        for raw_name, raw_profile in raw_profiles.items():
            name = str(raw_name)
            self.validate_profile_name(name)
            if not isinstance(raw_profile, dict):
                raise CliError("config_error", f"Profile 格式错误: {name}", EXIT_USAGE)
            profiles[name] = {
                "baseUrl": self.normalize_base_url(str(raw_profile.get("baseUrl") or "")),
                "output": "json",
                "timeoutSeconds": self.validate_timeout(
                    raw_profile.get("timeoutSeconds", DEFAULT_TIMEOUT_SECONDS)
                ),
            }
        if current_profile not in profiles:
            raise CliError("config_error", f"当前 Profile 不存在: {current_profile}", EXIT_USAGE)
        return {"currentProfile": current_profile, "profiles": profiles}

    @staticmethod
    def normalize_base_url(url: str) -> str:
        normalized = url.strip().rstrip("/")
        parsed = urlsplit(normalized)
        if parsed.scheme not in {"http", "https"} or not parsed.netloc:
            raise CliError("config_error", "Backend URL 必须是有效的 HTTP(S) 地址", EXIT_USAGE)
        return normalized

    @staticmethod
    def validate_profile_name(name: str) -> None:
        if not PROFILE_PATTERN.fullmatch(name):
            raise CliError(
                "config_error",
                "Profile 名称只能包含字母、数字、点、下划线和连字符",
                EXIT_USAGE,
            )

    @staticmethod
    def validate_timeout(value: object) -> float:
        try:
            timeout = float(value)
        except (TypeError, ValueError) as exc:
            raise CliError("config_error", "timeout 必须是数字", EXIT_USAGE) from exc
        if timeout <= 0 or timeout > 300:
            raise CliError("config_error", "timeout 必须在 0 到 300 秒之间", EXIT_USAGE)
        return timeout

    @staticmethod
    def _default_document() -> dict:
        return {
            "currentProfile": DEFAULT_PROFILE,
            "profiles": {
                DEFAULT_PROFILE: {
                    "baseUrl": DEFAULT_BASE_URL,
                    "output": "json",
                    "timeoutSeconds": DEFAULT_TIMEOUT_SECONDS,
                }
            },
        }

def is_insecure_transport(base_url: str) -> bool:
    return urlsplit(base_url).scheme.lower() == "http"
