from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, Field


# ── Bootstrap ─────────────────────────────────────────────────────────────────

class BootstrapStatusResponse(BaseModel):
    in_bootstrap: bool


class RuntimeConfigResponse(BaseModel):
    """app 启动/登录前读取的运行期配置（无敏感字段）。"""
    backend_base_url: str


# 引导页四块配置：backend(仅 base_url) + mysql/redis/tdengine(连接 + 密码明文，
# Agent 收到后 DPAPI 加密存 password_enc)。各段可选，便于分块提交。
class BootstrapBackendIn(BaseModel):
    base_url: str = Field(min_length=1)


class BootstrapDbIn(BaseModel):
    host: str
    port: int = Field(ge=1, le=65535)
    user: str | None = None
    password: str | None = None
    database: str | None = None


class BootstrapConfigRequest(BaseModel):
    backend: BootstrapBackendIn | None = None
    mysql: BootstrapDbIn | None = None
    redis: BootstrapDbIn | None = None
    tdengine: BootstrapDbIn | None = None


# 引导期连通性测试：内联传入待测配置，无需先保存。backend 用 base_url；
# mysql/redis/tdengine 用 host/port/user/password/database。
class TestConnectionRequest(BaseModel, extra="forbid"):
    target: Literal["backend", "mysql", "redis", "tdengine"]
    base_url: str | None = None
    host: str | None = None
    port: int | None = None
    user: str | None = None
    password: str | None = None
    database: str | None = None


# ── Health ────────────────────────────────────────────────────────────────────

class HealthCheckConfig(BaseModel):
    host: str | None = None
    port: int | None = None
    user: str | None = None
    password: str | None = None
    database: str | None = None


class HealthCheckRequest(BaseModel):
    target: Literal["redis", "mysql", "tdengine"]
    config: HealthCheckConfig | None = None


class HealthCheckResponse(BaseModel):
    success: bool
    message: str | None = None


# ── Auth ──────────────────────────────────────────────────────────────────────

class LoginRequest(BaseModel):
    username: str
    password: str


class LoginResponse(BaseModel):
    token: str


# ── Viewer ────────────────────────────────────────────────────────────────────

class ViewerStatusResponse(BaseModel):
    agent: str = "running"


class ServiceHealthItem(BaseModel):
    status: Literal["connected", "checking", "disconnected", "unknown"]
    latencyMs: int | None = None
    message: str | None = None
    checkedAt: str | None = None


class ViewerHealthResponse(BaseModel):
    redis: ServiceHealthItem
    mysql: ServiceHealthItem
    tdengine: ServiceHealthItem


class ProxyLogItem(BaseModel):
    endpoint: str
    port: int
    log_path: str


class ModuleStatusResponse(BaseModel):
    name: Literal["collector", "writer"]
    status: Literal["running", "stopped", "crashed", "unknown"]
    pid: int | None
    uptime_seconds: int
    memory_mb: float
    cpu_percent: float
    started_at: str | None
    restart_count: int
    # 可观测、非阻断：只暴露日志路径供现场排查，不读日志内容/不做错误摘要解析。
    log_path: str | None = None
    proxy_logs: list[ProxyLogItem] = Field(default_factory=list)


class ProcessStatusResponse(BaseModel):
    status: str


# ── Config GET（无密码）──────────────────────────────────────────────────────

class DbConfigOut(BaseModel):
    host: str
    port: int
    user: str | None = None
    database: str | None = None


class ProcessConfigOut(BaseModel):
    executable: str
    working_dir: str


class ConnectionConfigOut(BaseModel):
    redis: DbConfigOut
    mysql: DbConfigOut
    tdengine: DbConfigOut
    collector: ProcessConfigOut
    writer: ProcessConfigOut


# ── Config PUT（Partial，password 可选）──────────────────────────────────────

class DbConfigIn(BaseModel):
    host: str | None = None
    port: int | None = None
    user: str | None = None
    password: str | None = None
    database: str | None = None
    # 注：空串 password 的拒绝改在 PUT /admin/config 端点返回 400（A008 C5），
    # 不在此用 validator 拒绝（否则会被 FastAPI 转成 422）。


class ProcessConfigIn(BaseModel):
    executable: str | None = None
    working_dir: str | None = None


class ConnectionConfigIn(BaseModel):
    redis: DbConfigIn | None = None
    mysql: DbConfigIn | None = None
    tdengine: DbConfigIn | None = None
    collector: ProcessConfigIn | None = None
    writer: ProcessConfigIn | None = None


# ── 日志流 ──────────────────────────────────────────────────────────────────

class LogLine(BaseModel):
    timestamp: str
    level: Literal["DEBUG", "INFO", "WARN", "ERROR"]
    source: Literal["collector", "writer"]
    message: str
