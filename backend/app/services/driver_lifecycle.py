"""外置驱动目录注册与卸载编排（Backend 侧）。

安装 ZIP 由本机 Agent 执行；Backend 负责：
- 安装成功后 upsert ``comm_protocol``；
- 卸载前绑定检查，调用 Agent DELETE，成功后删除目录行；
- MySQL ``driver_operation`` 持久化阶段，启动时 reconcile 补偿。
"""

from __future__ import annotations

import asyncio
import logging
import threading
import uuid
from datetime import datetime, timedelta, timezone
from typing import Any, Optional

import httpx
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import get_settings
from app.core.exceptions import ApiError

logger = logging.getLogger(__name__)

# Agent HTTP 调用超时（秒）；避免同步阻塞事件循环
_AGENT_HTTP_TIMEOUT = 30.0
_DRIVER_OPERATION_RETENTION_DAYS = 90
_DRIVER_OPERATION_CLEANUP_INTERVAL_SECONDS = 24 * 60 * 60
_DRIVER_OPERATION_CLEANUP_BATCHES = 10
_cleanup_task: asyncio.Task[None] | None = None


class AgentOutcomeUnknown(ApiError):
    """超时或连接中断：Agent 侧结果未知，不可记为 failed 后直接清理。"""

# 内置协议：不可通过本编排卸载（含 comm_protocol 历史别名）
_BUILTIN_PROTOCOLS = frozenset(
    {
        "s7",
        "snap7",
        "siemens",
        "siemens_s7",
        "siemens_snap7",
        "ge_srtp",
        "opc",
        "opcua",
        "opc_ua",
        "opcda_center",
        "opc-da-center",
        "opc_da_center",
        "opc-da-sidecar",
        "opc_da_sidecar",
        "opcda_sidecar",
        "modbus_tcp",
    }
)

# protocol(lower) → {operation_id, status, started_at, error}（进程内并发门闩）
_operations_lock = threading.Lock()
_operations: dict[str, dict[str, Any]] = {}


def _normalize_protocol(value: str) -> str:
    return str(value or "").strip().lower().replace("-", "_").replace(" ", "_")


def _agent_base_url() -> str:
    return get_settings().agent_base_url.rstrip("/")


def _agent_token() -> str:
    return str(get_settings().agent_token or "").strip()


def _utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _utc_naive() -> datetime:
    return datetime.now(timezone.utc).replace(tzinfo=None)


def is_uninstalling(protocol: str) -> bool:
    key = _normalize_protocol(protocol)
    with _operations_lock:
        op = _operations.get(key)
        return bool(op and op.get("status") == "uninstalling")


class DriverLifecycleService:
    def __init__(self, session: AsyncSession):
        self.session = session

    async def register_protocol(self, payload: dict[str, Any]) -> dict[str, Any]:
        """安装成功后：若不存在则注册到 comm_protocol，并持久化 driver_id。"""
        protocol = str(payload.get("protocol") or payload.get("driverName") or "").strip()
        if not protocol:
            raise ApiError(400, "protocol 不能为空")
        if len(protocol) > 20:
            raise ApiError(400, "protocol 最大长度为 20 字符")
        driver_id = str(
            payload.get("driver_id") or payload.get("driverId") or payload.get("id") or ""
        ).strip()
        if not driver_id:
            # 兼容旧客户端：仅当 id 与 protocol 可能相同时回退
            driver_id = protocol
        description = str(
            payload.get("description")
            or payload.get("driverDesc")
            or payload.get("name")
            or protocol
        ).strip()
        version = str(payload.get("version") or payload.get("driver_version") or "").strip()
        sha256 = str(payload.get("sha256") or payload.get("driver_sha256") or "").strip()
        port = payload.get("port")
        try:
            port_val = int(port) if port is not None else 0
        except (TypeError, ValueError) as exc:
            raise ApiError(400, "port 必须为整数") from exc

        await self._ensure_installed_driver_table()
        await self._ensure_driver_operation_table()
        with _operations_lock:
            current = _operations.get(_normalize_protocol(protocol))
            if current and current.get("status") == "uninstalling":
                # B053：卸载仍在进行时注册会与其最终清理竞争，先拒绝。
                raise ApiError(409, "该驱动正在卸载中，请等待卸载完成后再注册")

        service_token = _agent_token()
        if not service_token:
            raise ApiError(500, "未配置 DATAWATCH_AGENT_SERVICE_TOKEN，无法核实驱动")
        agent_drivers = await self._list_agent_drivers(service_token)
        if agent_drivers is None:
            raise ApiError(503, "Agent 不可达，无法核实驱动安装状态")
        agent_info = next(
            (
                item
                for item in agent_drivers
                if isinstance(item, dict)
                and str(item.get("id") or "").strip() == driver_id
            ),
            None,
        )
        if agent_info is None:
            raise ApiError(409, f"Agent 未安装驱动 {driver_id}")
        verified_stopped = (agent_info.get("install_status") == "installed"
                            and agent_info.get("preflight_verified") is True
                            and agent_info.get("runtime_status") == "collector_stopped")
        if str(agent_info.get("status") or "") != "loaded" and not verified_stopped:
            raise ApiError(
                409,
                f"Agent 驱动 {driver_id} 未成功加载: "
                f"{agent_info.get('error') or agent_info.get('status') or 'unknown'}",
            )
        agent_read_write = str(
            agent_info.get("readWriteType") or agent_info.get("read_write") or ""
        ).strip()
        agent_capabilities = agent_info.get("capabilities")
        if (
            agent_read_write != "只读"
            or not isinstance(agent_capabilities, list)
            or "write" in agent_capabilities
        ):
            raise ApiError(
                409, f"Agent 驱动 {driver_id} 未通过只读能力校验"
            )
        agent_protocol = str(agent_info.get("protocol") or "").strip()
        if _normalize_protocol(agent_protocol) != _normalize_protocol(protocol):
            raise ApiError(409, "App 上报 protocol 与 Agent 已加载清单不一致")
        description = str(
            agent_info.get("description") or agent_info.get("name") or description
        ).strip()
        version = str(agent_info.get("version") or "").strip()
        sha256 = str(agent_info.get("sha256") or "").strip()
        if len(description) > 255:
            raise ApiError(400, "description 最大长度为 255 字符")
        if len(version) > 50:
            raise ApiError(400, "version 最大长度为 50 字符")

        existing = await self._fetch_protocol_row(protocol)
        op_type = "upgrade" if existing is not None else "install"
        # 注册入口调用时 Agent 侧已安装成功
        operation_id = await self._begin_operation(
            driver_id=driver_id,
            operation_type=op_type,
            target_version=version or None,
            agent_phase="success",
            db_phase="pending",
        )

        try:
            result = await self._apply_register_db(
                protocol=protocol,
                driver_id=driver_id,
                description=description,
                version=version,
                sha256=sha256,
                port_val=port_val,
                existing=existing,
            )
            await self._update_operation_phases(
                operation_id,
                agent_phase="success",
                db_phase="committed",
            )
            await self.session.commit()
            return result
        except Exception:
            await self.session.rollback()
            try:
                await self._update_operation_phases(
                    operation_id,
                    agent_phase="success",
                    db_phase="failed",
                )
                await self.session.commit()
            except Exception:
                logger.exception("更新 driver_operation 失败阶段失败: %s", operation_id)
            raise

    async def uninstall(
        self,
        *,
        protocol: str,
        agent_token: str | None = None,
        driver_id: str | None = None,
    ) -> dict[str, Any]:
        """卸载编排：绑定检查 → 标记 uninstalling → 用 driver_id 调 Agent → 删目录行。

        Agent 凭据取自 Settings.agent_token；agent_token 参数仅兼容旧调用。
        """
        protocol = str(protocol or "").strip()
        if not protocol:
            raise ApiError(400, "protocol 不能为空")
        token = str(agent_token or "").strip() or _agent_token()
        if not token:
            raise ApiError(
                500,
                "未配置 DATAWATCH_AGENT_SERVICE_TOKEN，无法调用 Agent 卸载驱动",
            )

        key = _normalize_protocol(protocol)

        await self._ensure_installed_driver_table()
        await self._ensure_driver_operation_table()
        resolved_driver_id = await self._resolve_uninstall_driver_id(
            protocol, str(driver_id or "").strip(), token
        )

        row = await self._fetch_protocol_row(protocol)
        with _operations_lock:
            current = _operations.get(key)
            if current and current.get("status") == "uninstalling":
                raise ApiError(409, "该驱动正在卸载中，请等待")

        operation_id = await self._begin_uninstall_operation(
            protocol=protocol,
            protocol_id=int(row["id"]) if row is not None else None,
            driver_id=resolved_driver_id,
        )

        with _operations_lock:
            _operations[key] = {
                "operation_id": operation_id,
                "status": "uninstalling",
                "protocol": protocol,
                "driver_id": resolved_driver_id,
                "started_at": _utc_now(),
                "error": None,
            }

        agent_result: Optional[dict[str, Any]] = None
        agent_ok = False
        try:
            # —— Agent 调用阶段 ——
            try:
                agent_result = await self._call_agent_uninstall(resolved_driver_id, token)
            except AgentOutcomeUnknown as exc:
                with _operations_lock:
                    op = _operations.get(key) or {}
                    op["status"] = "unknown"
                    op["error"] = exc.message
                    _operations[key] = op
                try:
                    await self._update_operation_phases(
                        operation_id,
                        agent_phase="unknown",
                        db_phase="pending",
                    )
                    await self.session.commit()
                except Exception:
                    logger.exception("更新卸载 unknown 阶段失败: %s", operation_id)
                raise
            except ApiError as exc:
                with _operations_lock:
                    op = _operations.get(key) or {}
                    op["status"] = "failed"
                    op["error"] = exc.message
                    _operations[key] = op
                    if _operations.get(key, {}).get("status") == "failed":
                        _operations.pop(key, None)
                try:
                    await self._release_uninstall_gate(protocol, operation_id)
                    await self._update_operation_phases(
                        operation_id,
                        agent_phase="failed",
                        db_phase="failed",
                    )
                    await self.session.commit()
                except Exception:
                    logger.exception("更新卸载失败阶段失败: %s", operation_id)
                raise
            except Exception as exc:
                with _operations_lock:
                    _operations.pop(key, None)
                try:
                    await self._update_operation_phases(
                        operation_id,
                        agent_phase="unknown",
                        db_phase="pending",
                    )
                    await self.session.commit()
                except Exception:
                    logger.exception("更新卸载 unknown 阶段失败: %s", operation_id)
                raise ApiError(503, f"调用 Agent 卸载失败: {exc}") from exc

            # Agent 已返回成功：此后取消/DB 失败均按 success 补偿，勿记 unknown
            agent_ok = True
            await self._update_operation_phases(
                operation_id,
                agent_phase="success",
                db_phase="pending",
            )
            await self.session.commit()

            deleted = False  # Local package removal never deletes the global protocol catalog.
            # B053：只删除本操作持有门禁的那一代安装记录；期间重新注册（新代际）不得被删。
            await self.session.execute(
                text(
                    "DELETE FROM installed_driver WHERE protocol = :protocol "
                    "AND lifecycle_operation_id = :operation_id"
                ),
                {"protocol": protocol, "operation_id": operation_id},
            )
            await self._update_operation_phases(
                operation_id,
                agent_phase="success",
                db_phase="committed",
            )
            await self.session.commit()

            return {
                "protocol": protocol,
                "driverId": resolved_driver_id,
                "operationId": operation_id,
                "catalogDeleted": deleted,
                "agent": agent_result,
                "status": "uninstalled",
            }
        except asyncio.CancelledError:
            # Agent 或 DB 任一阶段取消：清门闩；Agent 已成功则幂等补 success/pending
            with _operations_lock:
                _operations.pop(key, None)
            if not agent_ok:
                try:
                    await asyncio.shield(
                        self._mark_operation_unknown_best_effort(operation_id)
                    )
                except Exception:
                    logger.exception(
                        "取消时写入 unknown 失败 operation=%s", operation_id
                    )
            else:
                try:
                    await asyncio.shield(
                        self._mark_operation_success_pending_best_effort(operation_id)
                    )
                except Exception:
                    logger.exception(
                        "取消时补写 success/pending 失败 operation=%s", operation_id
                    )
            raise
        except (AgentOutcomeUnknown, ApiError):
            # 内层已更新 phase / 门闩，直接抛出
            raise
        except Exception:
            if agent_ok:
                await self.session.rollback()
                try:
                    await self._update_operation_phases(
                        operation_id,
                        agent_phase="success",
                        db_phase="failed",
                    )
                    await self.session.commit()
                except Exception:
                    logger.exception("更新卸载 db_phase=failed 失败: %s", operation_id)
            with _operations_lock:
                _operations.pop(key, None)
            raise
        finally:
            # 成功路径清 uninstalling 门闩；unknown/failed 由内层自行处理
            with _operations_lock:
                cur = _operations.get(key)
                if cur and cur.get("operation_id") == operation_id:
                    if cur.get("status") == "uninstalling":
                        _operations.pop(key, None)

    async def _resolve_uninstall_driver_id(
        self, protocol: str, explicit_driver_id: str, token: str
    ) -> str:
        """B019：以 protocol 的 canonical 安装记录确定 driverId，检查/门禁/卸载/清理同一条记录。

        显式 driverId 与安装记录不符 → 409；无安装映射时必须由 Agent 清单核对 protocol-ID 对。
        """
        meta = await self._fetch_installed_driver(protocol)
        canonical = str((meta or {}).get("driver_id") or "").strip()
        if canonical:
            if explicit_driver_id and explicit_driver_id != canonical:
                raise ApiError(
                    409,
                    f"driverId {explicit_driver_id} 与 protocol {protocol} 的安装记录"
                    f"（{canonical}）不一致",
                )
            return canonical
        if not explicit_driver_id:
            raise ApiError(
                400,
                "缺少 driver_id：请重新安装注册后再卸载，或显式传入 driverId",
            )
        agent_drivers = await self._list_agent_drivers(token)
        if agent_drivers is None:
            raise ApiError(503, "Agent 不可达，无法核对 driverId 与 protocol")
        agent_info = next(
            (
                item
                for item in agent_drivers
                if isinstance(item, dict)
                and str(item.get("id") or "").strip() == explicit_driver_id
            ),
            None,
        )
        if agent_info is None:
            raise ApiError(409, f"Agent 未安装驱动 {explicit_driver_id}")
        agent_protocol = str(agent_info.get("protocol") or "").strip()
        if _normalize_protocol(agent_protocol) != _normalize_protocol(protocol):
            raise ApiError(
                409,
                f"driverId {explicit_driver_id} 在 Agent 上对应 protocol "
                f"{agent_protocol or '(空)'}，与请求的 {protocol} 不一致",
            )
        return explicit_driver_id

    async def uninstall_status(self, protocol: str) -> dict[str, Any]:
        key = _normalize_protocol(protocol)
        with _operations_lock:
            op = _operations.get(key)
        if op is None:
            row = (
                await self.session.execute(
                    text(
                        """
                        SELECT d.lifecycle_status, d.driver_id,
                               o.operation_id, o.agent_phase, o.db_phase,
                               o.created_at, o.updated_at
                        FROM installed_driver d
                        LEFT JOIN driver_operation o
                          ON o.operation_id = d.lifecycle_operation_id
                        WHERE LOWER(d.protocol) = LOWER(:protocol)
                        LIMIT 1
                        """
                    ),
                    {"protocol": protocol},
                )
            ).mappings().first()
            if row is not None and str(row.get("lifecycle_status") or "") == "uninstalling":
                agent_phase = str(row.get("agent_phase") or "pending")
                op = {
                    "operation_id": row.get("operation_id"),
                    "status": "unknown" if agent_phase == "unknown" else "uninstalling",
                    "protocol": protocol,
                    "driver_id": row.get("driver_id"),
                    "started_at": row.get("created_at"),
                    "updated_at": row.get("updated_at"),
                    "agent_phase": agent_phase,
                    "db_phase": row.get("db_phase"),
                    "error": None,
                }
        return {"protocol": protocol, "operation": op}

    async def reconcile(self) -> dict[str, Any]:
        """启动补偿：

        - success + db pending/failed → 补数据库
        - failed + pending → 释放卸载门禁并终结为 failed/failed
        - pending/unknown + pending（卸载）→ 探活 Agent 后重试或补删
        """
        await self._ensure_driver_operation_table()
        await self._ensure_installed_driver_table()
        stats = {
            "retried": 0,
            "cleaned": 0,
            "retained": 0,
            "orphans": 0,
            "skipped": 0,
            "errors": 0,
        }

        try:
            stats["cleaned"] = await self._purge_terminal_operations()
            await self.session.commit()
        except Exception:
            await self.session.rollback()
            logger.exception("清理过期 driver_operation 终态记录失败")
            stats["errors"] += 1

        try:
            orphans = await self._find_installed_driver_orphans()
            stats["orphans"] = len(orphans)
            for orphan in orphans:
                logger.error(
                    "installed_driver 孤儿映射：protocol=%s driver_id=%s，"
                    "comm_protocol 不存在；保留原记录，需人工核对",
                    orphan.get("protocol"),
                    orphan.get("driver_id"),
                )
        except Exception:
            await self.session.rollback()
            logger.exception("检查 installed_driver 孤儿映射失败")
            stats["errors"] += 1

        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT operation_id, driver_id, operation_type, target_version,
                           agent_phase, db_phase
                    FROM driver_operation
                    WHERE NOT (
                        (agent_phase = 'success' AND db_phase = 'committed')
                        OR (agent_phase = 'failed' AND db_phase = 'failed')
                    )
                    ORDER BY created_at ASC
                    """
                )
            )
        ).mappings().all()

        for row in rows:
            op = dict(row)
            operation_id = str(op["operation_id"])
            agent_phase = str(op["agent_phase"] or "")
            db_phase = str(op["db_phase"] or "")
            op_type = str(op["operation_type"] or "")
            driver_id = str(op["driver_id"] or "").strip()

            try:
                if agent_phase == "failed" and db_phase == "pending":
                    if op_type == "uninstall":
                        await self._release_uninstall_gate_by_driver_id(
                            driver_id, operation_id
                        )
                    await self._update_operation_phases(
                        operation_id,
                        agent_phase="failed",
                        db_phase="failed",
                    )
                    await self.session.commit()
                    logger.warning(
                        "终结失败的 driver_operation，保留至过期清理：operation=%s "
                        "driver_id=%s agent_phase=failed db_phase=failed",
                        operation_id,
                        driver_id,
                    )
                    stats["retained"] += 1
                    continue

                if (
                    agent_phase in {"pending", "unknown"}
                    and db_phase == "pending"
                    and op_type == "uninstall"
                ):
                    outcome = await self._reconcile_uncertain_uninstall(op)
                    if outcome == "retried":
                        stats["retried"] += 1
                    elif outcome == "skipped":
                        stats["skipped"] += 1
                    else:
                        stats["errors"] += 1
                    continue

                if agent_phase == "success" and db_phase in {"pending", "failed"}:
                    if op_type in {"install", "upgrade"}:
                        ok = await self._reconcile_install(op)
                    elif op_type == "uninstall":
                        ok = await self._reconcile_uninstall(op)
                    else:
                        stats["skipped"] += 1
                        continue
                    if ok:
                        await self._update_operation_phases(
                            operation_id,
                            agent_phase="success",
                            db_phase="committed",
                        )
                        await self.session.commit()
                        stats["retried"] += 1
                    else:
                        stats["skipped"] += 1
                    continue

                stats["skipped"] += 1
            except Exception:
                await self.session.rollback()
                logger.exception("reconcile driver_operation 失败: %s", operation_id)
                stats["errors"] += 1

        return stats

    async def _reconcile_uncertain_uninstall(self, op: dict[str, Any]) -> str:
        """pending/unknown 卸载：探活 Agent 后决定重试 DELETE 或补删 MySQL。

        返回 retried | skipped | error。
        """
        operation_id = str(op.get("operation_id") or "")
        driver_id = str(op.get("driver_id") or "").strip()
        if not driver_id:
            return "skipped"

        token = _agent_token()
        if not token:
            logger.warning(
                "reconcile uninstall unknown: 缺少 DATAWATCH_AGENT_SERVICE_TOKEN，保留 operation=%s",
                operation_id,
            )
            return "skipped"

        presence = await self._probe_agent_driver(driver_id, token)
        if presence == "unreachable":
            logger.warning(
                "reconcile uninstall unknown: Agent 不可达，保留 operation=%s",
                operation_id,
            )
            return "skipped"

        if presence == "present":
            meta = await self._fetch_installed_driver_by_id(driver_id)
            if meta is not None and str(meta.get("lifecycle_operation_id") or "") != operation_id:
                # B053：Agent 上的驱动属于之后重新安装注册的新代际，旧操作不得重试 DELETE。
                logger.warning(
                    "reconcile uninstall: driver_id=%s 已被重新注册（新代际），终结过期 operation=%s",
                    driver_id,
                    operation_id,
                )
                await self._update_operation_phases(
                    operation_id,
                    agent_phase="failed",
                    db_phase="failed",
                )
                await self.session.commit()
                return "skipped"
            try:
                await self._call_agent_uninstall(driver_id, token)
            except AgentOutcomeUnknown:
                await self._update_operation_phases(
                    operation_id,
                    agent_phase="unknown",
                    db_phase="pending",
                )
                await self.session.commit()
                return "skipped"
            except ApiError as exc:
                logger.warning(
                    "reconcile 重试 DELETE 明确失败 operation=%s: %s",
                    operation_id,
                    exc.message,
                )
                await self._update_operation_phases(
                    operation_id,
                    agent_phase="failed",
                    db_phase="pending",
                )
                await self._release_uninstall_gate_by_driver_id(
                    driver_id, operation_id
                )
                await self.session.commit()
                return "skipped"

        # absent：Agent 已无此驱动；present 且 DELETE 成功：继续补删 MySQL
        await self._update_operation_phases(
            operation_id,
            agent_phase="success",
            db_phase="pending",
        )
        ok = await self._reconcile_uninstall(op)
        if not ok:
            await self.session.commit()
            return "skipped"
        await self._update_operation_phases(
            operation_id,
            agent_phase="success",
            db_phase="committed",
        )
        await self.session.commit()
        return "retried"

    async def _reconcile_install(self, op: dict[str, Any]) -> bool:
        """从 Agent 拉取驱动元数据后重试目录注册。"""
        driver_id = str(op.get("driver_id") or "").strip()
        if not driver_id:
            return False
        token = _agent_token()
        info = await self._fetch_agent_driver(driver_id, token)
        if info is None:
            logger.warning(
                "reconcile install: Agent 上找不到 driver_id=%s，保留 pending",
                driver_id,
            )
            return False
        protocol = str(info.get("protocol") or info.get("id") or driver_id).strip()
        description = str(
            info.get("description") or info.get("name") or protocol
        ).strip()
        version = str(
            info.get("version") or ""
        ).strip()

        if not version or info.get("status") not in {"loaded", "installed"}:
            return False
        if info.get("status") == "installed" and not info.get("preflight_verified"):
            return False

        sha256 = str(info.get("sha256") or "").strip()
        existing = await self._fetch_protocol_row(protocol)
        await self._apply_register_db(
            protocol=protocol,
            driver_id=driver_id,
            description=description,
            version=version,
            sha256=sha256,
            port_val=0,
            existing=existing,
        )
        return True

    async def _reconcile_uninstall(self, op: dict[str, Any]) -> bool:
        """Agent 已卸载成功时，仅清理安装映射，保留全局协议目录。"""
        driver_id = str(op.get("driver_id") or "").strip()
        if not driver_id:
            return False
        operation_id = str(op.get("operation_id") or "")
        meta = await self._fetch_installed_driver_by_id(driver_id)
        protocol = str((meta or {}).get("protocol") or "").strip()
        # B053：补偿只作用于当初持有门禁（lifecycle_operation_id 一致）的那一代记录；
        # 之后重新安装注册的新代际（门禁已重置）保持不动，过期操作直接终结。
        if protocol:
            await self.session.execute(
                text(
                    "DELETE FROM installed_driver WHERE protocol = :protocol "
                    "AND lifecycle_operation_id = :operation_id"
                ),
                {"protocol": protocol, "operation_id": operation_id},
            )
        else:
            await self.session.execute(
                text(
                    "DELETE FROM installed_driver WHERE driver_id = :driver_id "
                    "AND lifecycle_operation_id = :operation_id"
                ),
                {"driver_id": driver_id, "operation_id": operation_id},
            )
        return True

    async def _apply_register_db(
        self,
        *,
        protocol: str,
        driver_id: str,
        description: str,
        version: str,
        sha256: str,
        port_val: int,
        existing: Optional[dict[str, Any]],
    ) -> dict[str, Any]:
        """写入/更新 comm_protocol 与 installed_driver（不提交事务）。"""
        if existing is not None:
            await self.session.execute(
                text(
                    """
                    UPDATE comm_protocol
                    SET description = :description
                    WHERE id = :id
                    """
                ),
                {"description": description, "id": int(existing["id"])},
            )
            await self._upsert_installed_driver(
                protocol=protocol,
                driver_id=driver_id,
                version=version,
                sha256=sha256,
            )
            return {
                "id": int(existing["id"]),
                "protocol": existing["protocol"],
                "driverId": driver_id,
                "description": description,
                "created": False,
            }

        max_id_row = (
            await self.session.execute(
                text("SELECT COALESCE(MAX(id), 0) AS max_id FROM comm_protocol")
            )
        ).mappings().first()
        new_id = int(max_id_row["max_id"] if max_id_row else 0) + 1

        try:
            await self.session.execute(
                text(
                    """
                    INSERT INTO comm_protocol (id, protocol, port, description, create_time)
                    VALUES (:id, :protocol, :port, :description, NOW())
                    """
                ),
                {
                    "id": new_id,
                    "protocol": protocol,
                    "port": port_val,
                    "description": description,
                },
            )
            await self._upsert_installed_driver(
                protocol=protocol,
                driver_id=driver_id,
                version=version,
                sha256=sha256,
            )
        except Exception:
            await self.session.rollback()
            existing = await self._fetch_protocol_row(protocol)
            if existing is None:
                raise
            await self._ensure_installed_driver_table()
            await self._upsert_installed_driver(
                protocol=protocol,
                driver_id=driver_id,
                version=version,
                sha256=sha256,
            )
            return {
                "id": int(existing["id"]),
                "protocol": existing["protocol"],
                "driverId": driver_id,
                "description": existing.get("description") or description,
                "created": False,
            }

        return {
            "id": new_id,
            "protocol": protocol,
            "driverId": driver_id,
            "description": description,
            "created": True,
        }

    async def _fetch_protocol_row(self, protocol: str) -> Optional[dict[str, Any]]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT id, protocol, description, port
                    FROM comm_protocol
                    ORDER BY id ASC
                    """
                )
            )
        ).mappings().all()
        want = _normalize_protocol(protocol)
        for row in rows:
            if _normalize_protocol(str(row["protocol"])) == want:
                return dict(row)
        return None

    async def _bound_device_count(self, protocol_id: int) -> int:
        row = (
            await self.session.execute(
                text(
                    """
                    SELECT COUNT(id) AS cnt
                    FROM device
                    WHERE protocol_id = :protocol_id
                    """
                ),
                {"protocol_id": protocol_id},
            )
        ).mappings().first()
        return int(row["cnt"] if row else 0)

    async def _call_agent_uninstall(self, driver_id: str, agent_token: str) -> dict[str, Any]:
        """按 Manifest.id（安装目录名）删除；404 前先核实 Agent 列表。"""
        url = f"{_agent_base_url()}/admin/drivers/{driver_id}"
        headers = {
            "X-DataWatch-Service-Token": agent_token,
            "Accept": "application/json",
        }
        try:
            async with httpx.AsyncClient(timeout=_AGENT_HTTP_TIMEOUT) as client:
                resp = await client.delete(url, headers=headers)
                status = resp.status_code
                body = resp.text or ""
                if status == 404:
                    # 404 可能是路径用了 protocol；先核实列表，避免误删 MySQL
                    if await self._agent_still_has_driver(driver_id, agent_token, client):
                        raise ApiError(
                            502,
                            f"Agent 返回 404 但驱动 {driver_id} 仍在列表中（可能误用了 protocol）",
                        )
                    try:
                        payload = resp.json()
                        message = payload.get("detail") or payload.get("msg") or body
                    except Exception:
                        message = body or "Not Found"
                    return {"status": "absent", "detail": message}
                if status >= 400:
                    try:
                        payload = resp.json()
                        message = payload.get("detail") or payload.get("msg") or body
                    except Exception:
                        message = body or resp.reason_phrase or str(status)
                    raise ApiError(
                        status if status in {400, 409, 422, 503} else 502,
                        str(message),
                    )
                try:
                    data = resp.json() if body else {}
                except Exception:
                    data = {"raw": body}
                return {"httpStatus": status, "data": data}
        except ApiError:
            raise
        except httpx.TimeoutException as exc:
            raise AgentOutcomeUnknown(
                503, f"调用 Agent 卸载超时（{_AGENT_HTTP_TIMEOUT:.0f}s）"
            ) from exc
        except httpx.HTTPError as exc:
            raise AgentOutcomeUnknown(503, f"无法连接 Agent: {exc}") from exc

    async def _agent_still_has_driver(
        self,
        driver_id: str,
        agent_token: str,
        client: httpx.AsyncClient | None = None,
    ) -> bool:
        url = f"{_agent_base_url()}/admin/drivers"
        headers = {
            "X-DataWatch-Service-Token": agent_token,
            "Accept": "application/json",
        }

        async def _get(c: httpx.AsyncClient) -> Any:
            resp = await c.get(url, headers=headers)
            resp.raise_for_status()
            return resp.json()

        try:
            if client is not None:
                payload = await _get(client)
            else:
                async with httpx.AsyncClient(timeout=_AGENT_HTTP_TIMEOUT) as c:
                    payload = await _get(c)
        except Exception as exc:
            logger.warning("核实 Agent 驱动列表失败: %s", exc)
            # 列表不可达时不把 404 当成已卸载
            return True

        drivers = payload.get("drivers") if isinstance(payload, dict) else payload
        if not isinstance(drivers, list):
            return True
        want = str(driver_id).strip()
        for item in drivers:
            if not isinstance(item, dict):
                continue
            if str(item.get("id") or "").strip() == want:
                return True
        return False

    async def _list_agent_drivers(
        self,
        agent_token: str,
    ) -> Optional[list[Any]]:
        """拉取 Agent 驱动列表；不可达或响应异常时返回 None。"""
        url = f"{_agent_base_url()}/admin/drivers"
        headers = {
            "X-DataWatch-Service-Token": agent_token,
            "Accept": "application/json",
        }
        try:
            async with httpx.AsyncClient(timeout=_AGENT_HTTP_TIMEOUT) as client:
                resp = await client.get(url, headers=headers)
                resp.raise_for_status()
                payload = resp.json()
        except Exception as exc:
            logger.warning("拉取 Agent 驱动列表失败: %s", exc)
            return None
        drivers = payload.get("drivers") if isinstance(payload, dict) else payload
        if not isinstance(drivers, list):
            return None
        return drivers

    async def _probe_agent_driver(self, driver_id: str, agent_token: str) -> str:
        """探活：present | absent | unreachable。"""
        drivers = await self._list_agent_drivers(agent_token)
        if drivers is None:
            return "unreachable"
        want = str(driver_id).strip()
        for item in drivers:
            if not isinstance(item, dict):
                continue
            if str(item.get("id") or "").strip() == want:
                return "present"
        return "absent"

    async def _fetch_agent_driver(
        self,
        driver_id: str,
        agent_token: str,
    ) -> Optional[dict[str, Any]]:
        """从 Agent 列表按 id 取驱动元数据；失败或不存在返回 None。"""
        drivers = await self._list_agent_drivers(agent_token)
        if drivers is None:
            return None
        want = str(driver_id).strip()
        for item in drivers:
            if not isinstance(item, dict):
                continue
            if str(item.get("id") or "").strip() == want:
                return item
        return None

    async def _ensure_installed_driver_table(self) -> None:
        """表结构由 ``scripts/sql/db5_driver_lifecycle.sql`` 迁移创建；运行时不 DDL。"""
        return None

    async def _ensure_driver_operation_table(self) -> None:
        """表结构由 ``scripts/sql/db5_driver_lifecycle.sql`` 迁移创建；运行时不 DDL。"""
        return None

    async def _begin_operation(
        self,
        *,
        driver_id: str,
        operation_type: str,
        target_version: Optional[str],
        agent_phase: str,
        db_phase: str,
    ) -> str:
        operation_id = str(uuid.uuid4())
        now = _utc_naive()
        # ✅ 移除：不在请求路径清理历史，避免长事务
        # await self._purge_terminal_operations()
        await self.session.execute(
            text(
                """
                INSERT INTO driver_operation (
                    operation_id, driver_id, operation_type, target_version,
                    agent_phase, db_phase, created_at, updated_at
                ) VALUES (
                    :operation_id, :driver_id, :operation_type, :target_version,
                    :agent_phase, :db_phase, :created_at, :updated_at
                )
                """
            ),
            {
                "operation_id": operation_id,
                "driver_id": driver_id,
                "operation_type": operation_type,
                "target_version": target_version,
                "agent_phase": agent_phase,
                "db_phase": db_phase,
                "created_at": now,
                "updated_at": now,
            },
        )
        await self.session.commit()
        return operation_id

    async def _begin_uninstall_operation(
        self,
        *,
        protocol: str,
        protocol_id: int | None,
        driver_id: str,
    ) -> str:
        """Atomically fence new bindings and persist an uninstall operation."""
        operation_id = str(uuid.uuid4())
        now = _utc_naive()
        try:
            # ✅ 移除：不在请求路径清理历史，避免长事务
            # await self._purge_terminal_operations()
            if protocol_id is not None:
                await self.session.execute(
                    text("SELECT id FROM comm_protocol WHERE id = :id FOR UPDATE"),
                    {"id": protocol_id},
                )

            lifecycle = (
                await self.session.execute(
                    text(
                        """
                        SELECT lifecycle_status, lifecycle_operation_id
                        FROM installed_driver
                        WHERE protocol = :protocol
                        FOR UPDATE
                        """
                    ),
                    {"protocol": protocol},
                )
            ).mappings().first()
            if lifecycle is not None and str(lifecycle["lifecycle_status"]) != "active":
                raise ApiError(409, "该驱动已有未完成的卸载操作")

            if protocol_id is not None:
                bound = await self._bound_device_count(protocol_id)
                if bound > 0:
                    devices = (await self.session.execute(text("SELECT device_code FROM device WHERE protocol_id = :protocol_id ORDER BY device_code LIMIT 20"), {"protocol_id": protocol_id})).mappings().all()
                    names = ", ".join(str(item.get("device_code") or "") for item in devices)
                    raise ApiError(409, f"仍有 {bound} 台设备绑定该驱动，无法卸载：{names}")

            await self.session.execute(
                text(
                    """
                    INSERT INTO driver_operation (
                        operation_id, driver_id, operation_type, target_version,
                        agent_phase, db_phase, created_at, updated_at
                    ) VALUES (
                        :operation_id, :driver_id, 'uninstall', NULL,
                        'pending', 'pending', :created_at, :updated_at
                    )
                    """
                ),
                {
                    "operation_id": operation_id,
                    "driver_id": driver_id,
                    "created_at": now,
                    "updated_at": now,
                },
            )
            if lifecycle is not None:
                await self.session.execute(
                    text(
                        """
                        UPDATE installed_driver
                        SET lifecycle_status = 'uninstalling',
                            lifecycle_operation_id = :operation_id,
                            updated_at = :updated_at
                        WHERE protocol = :protocol
                          AND lifecycle_status = 'active'
                        """
                    ),
                    {
                        "protocol": protocol,
                        "operation_id": operation_id,
                        "updated_at": now,
                    },
                )
            await self.session.commit()
            return operation_id
        except Exception:
            await self.session.rollback()
            raise

    async def _release_uninstall_gate(self, protocol: str, operation_id: str) -> None:
        await self.session.execute(
            text(
                """
                UPDATE installed_driver
                SET lifecycle_status = 'active',
                    lifecycle_operation_id = NULL,
                    updated_at = :updated_at
                WHERE protocol = :protocol
                  AND lifecycle_operation_id = :operation_id
                """
            ),
            {
                "protocol": protocol,
                "operation_id": operation_id,
                "updated_at": _utc_naive(),
            },
        )

    async def _release_uninstall_gate_by_driver_id(
        self, driver_id: str, operation_id: str
    ) -> None:
        await self.session.execute(
            text(
                """
                UPDATE installed_driver
                SET lifecycle_status = 'active',
                    lifecycle_operation_id = NULL,
                    updated_at = :updated_at
                WHERE driver_id = :driver_id
                  AND lifecycle_operation_id = :operation_id
                """
            ),
            {
                "driver_id": driver_id,
                "operation_id": operation_id,
                "updated_at": _utc_naive(),
            },
        )

    async def _mark_operation_unknown_best_effort(self, operation_id: str) -> None:
        """取消/中断时尽力写入 unknown（供 shield 调用，失败不抛给调用方语义外）。"""
        try:
            await self._update_operation_phases(
                operation_id,
                agent_phase="unknown",
                db_phase="pending",
            )
            await self.session.commit()
        except Exception:
            logger.exception("best-effort 写入 unknown 失败: %s", operation_id)
            try:
                await self.session.rollback()
            except Exception:
                pass

    async def _mark_operation_success_pending_best_effort(self, operation_id: str) -> None:
        """Agent 已成功后取消：先回滚当前事务，再幂等写入 success/pending。"""
        try:
            await self.session.rollback()
        except Exception:
            pass
        try:
            await self._update_operation_phases(
                operation_id,
                agent_phase="success",
                db_phase="pending",
            )
            await self.session.commit()
        except Exception:
            logger.exception(
                "best-effort 写入 success/pending 失败: %s", operation_id
            )
            try:
                await self.session.rollback()
            except Exception:
                pass

    async def _update_operation_phases(
        self,
        operation_id: str,
        *,
        agent_phase: str,
        db_phase: str,
    ) -> None:
        await self.session.execute(
            text(
                """
                UPDATE driver_operation
                SET agent_phase = :agent_phase,
                    db_phase = :db_phase,
                    updated_at = :updated_at
                WHERE operation_id = :operation_id
                """
            ),
            {
                "operation_id": operation_id,
                "agent_phase": agent_phase,
                "db_phase": db_phase,
                "updated_at": _utc_naive(),
            },
        )

    async def _purge_terminal_operations(self, *, max_batches: int = 1) -> int:
        """按有限批次清理 90 天前的终态 operation。"""
        cutoff = _utc_naive() - timedelta(days=_DRIVER_OPERATION_RETENTION_DAYS)
        total_deleted = 0
        batch_size = 1000

        for _ in range(max(1, max_batches)):
            result = await self.session.execute(
                text(
                    """
                    DELETE FROM driver_operation
                    WHERE (
                            (agent_phase = 'success' AND db_phase = 'committed')
                            OR (agent_phase = 'failed' AND db_phase = 'failed')
                          )
                      AND updated_at < :cutoff
                    LIMIT :batch_size
                    """
                ),
                {"cutoff": cutoff, "batch_size": batch_size},
            )
            await self.session.commit()  # 每批提交一次

            rowcount = getattr(result, "rowcount", 0)
            if not isinstance(rowcount, int) or rowcount <= 0:
                break

            total_deleted += rowcount
            if rowcount < batch_size:
                break

        return total_deleted

    async def _find_installed_driver_orphans(self) -> list[dict[str, Any]]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT i.protocol, i.driver_id
                    FROM installed_driver i
                    LEFT JOIN comm_protocol c ON c.protocol = i.protocol
                    WHERE c.id IS NULL
                    ORDER BY i.protocol ASC
                    """
                )
            )
        ).mappings().all()
        return [dict(row) for row in rows]

    async def _upsert_installed_driver(
        self,
        *,
        protocol: str,
        driver_id: str,
        version: str,
        sha256: str,
    ) -> None:
        # B053：重新安装注册即新代际，重置旧卸载门禁（lifecycle_*），旧操作不得再修改本记录。
        await self.session.execute(
            text(
                """
                INSERT INTO installed_driver
                    (protocol, driver_id, driver_version, driver_sha256, install_source, updated_at)
                VALUES
                    (:protocol, :driver_id, :version, :sha256, 'zip', :updated_at)
                ON DUPLICATE KEY UPDATE
                    driver_id = VALUES(driver_id),
                    driver_version = VALUES(driver_version),
                    driver_sha256 = VALUES(driver_sha256),
                    install_source = 'zip',
                    lifecycle_status = 'active',
                    lifecycle_operation_id = NULL,
                    updated_at = VALUES(updated_at)
                """
            ),
            {
                "protocol": protocol,
                "driver_id": driver_id,
                "version": version or None,
                "sha256": sha256 or None,
                "updated_at": _utc_naive(),
            },
        )

    async def _fetch_installed_driver(self, protocol: str) -> Optional[dict[str, Any]]:
        row = (
            await self.session.execute(
                text(
                    """
                    SELECT protocol, driver_id, driver_version, driver_sha256
                    FROM installed_driver
                    WHERE protocol = :protocol
                    """
                ),
                {"protocol": protocol},
            )
        ).mappings().first()
        return dict(row) if row else None

    async def _fetch_installed_driver_by_id(self, driver_id: str) -> Optional[dict[str, Any]]:
        row = (
            await self.session.execute(
                text(
                    """
                    SELECT protocol, driver_id, driver_version, driver_sha256,
                           lifecycle_operation_id
                    FROM installed_driver
                    WHERE driver_id = :driver_id
                    LIMIT 1
                    """
                ),
                {"driver_id": driver_id},
            )
        ).mappings().first()
        return dict(row) if row else None

    async def _fetch_installed_driver_by_protocol(self, protocol: str) -> Optional[dict[str, Any]]:
        row = (
            await self.session.execute(
                text(
                    """
                    SELECT protocol, driver_id, driver_version, driver_sha256
                    FROM installed_driver
                    WHERE protocol = :protocol
                    LIMIT 1
                    """
                ),
                {"protocol": protocol},
            )
        ).mappings().first()
        return dict(row) if row else None


async def reconcile_driver_operations() -> dict[str, Any]:
    """Backend 启动时补偿未完成的驱动生命周期操作。"""
    from app.db.mysql import get_mysql_session_factory

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = DriverLifecycleService(session)
        try:
            stats = await service.reconcile()
            logger.info("driver_operation reconcile 完成: %s", stats)
            return stats
        except Exception:
            logger.exception("driver_operation reconcile 失败（不阻断启动）")
            return {
                "retried": 0,
                "cleaned": 0,
                "retained": 0,
                "orphans": 0,
                "skipped": 0,
                "errors": 1,
            }


async def purge_driver_operation_history() -> int:
    """后台维护入口：每轮最多清理固定批次数量。"""
    from app.db.mysql import get_mysql_session_factory

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = DriverLifecycleService(session)
        return await service._purge_terminal_operations(
            max_batches=_DRIVER_OPERATION_CLEANUP_BATCHES
        )


async def _run_driver_operation_cleanup_loop() -> None:
    while True:
        await asyncio.sleep(_DRIVER_OPERATION_CLEANUP_INTERVAL_SECONDS)
        try:
            deleted = await purge_driver_operation_history()
            if deleted:
                logger.info("后台清理 driver_operation 历史记录: %s", deleted)
        except asyncio.CancelledError:
            raise
        except Exception:
            logger.exception("后台清理 driver_operation 历史记录失败")


async def start_driver_operation_cleanup() -> None:
    global _cleanup_task
    if _cleanup_task is not None and not _cleanup_task.done():
        return
    _cleanup_task = asyncio.create_task(_run_driver_operation_cleanup_loop())


async def stop_driver_operation_cleanup() -> None:
    global _cleanup_task
    if _cleanup_task is None:
        return
    _cleanup_task.cancel()
    try:
        await _cleanup_task
    except asyncio.CancelledError:
        pass
    _cleanup_task = None
