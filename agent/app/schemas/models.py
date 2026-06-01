from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, Field


# ── Bootstrap ─────────────────────────────────────────────────────────────────

class BootstrapStatusResponse(BaseModel):
    in_bootstrap: bool


class BootstrapConfigRequest(BaseModel):
    host: str
    port: int = Field(ge=1, le=65535)
    user: str
    password: str = Field(min_length=1)
    database: str


class TestConnectionRequest(BaseModel, extra="forbid"):
    target: Literal["mysql"]


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


class ModuleStatusResponse(BaseModel):
    name: Literal["collector", "writer"]
    status: Literal["running", "stopped", "crashed", "unknown"]
    pid: int | None
    uptime_seconds: int
    memory_mb: float
    cpu_percent: float
    started_at: str | None
    restart_count: int


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
