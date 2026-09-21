"""外置驱动安装/升级/卸载事务（staging → 激活 → 重启 → 回滚）。"""

from __future__ import annotations

import asyncio
import hashlib
import socket
import json
import logging
import os
import shutil
import sys
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Awaitable, Callable, Optional

from app.services.driver_zip import (
    BUILTIN_PROTOCOLS,
    DriverZipError,
    StagingPathConflictError,
    ValidatedDriverPackage,
    is_valid_driver_id,
    validate_and_extract,
)

logger = logging.getLogger(__name__)

# collector 重启后等待进程存活的超时（秒）
_HEALTH_WAIT_SECONDS = 30.0
_HEALTH_POLL_INTERVAL = 0.5
_COLLECTOR_STATUS_URL = os.environ.get(
    "DATAWATCH_COLLECTOR_STATUS_URL", "http://127.0.0.1:6868"
).rstrip("/")
_TERMINAL_AUDIT_LIMIT = 100

# 安装事务阶段：未完成时启动需恢复；终态不再处理
# activated → verifying → verified → committed；仅 verified 允许提交
_PHASE_STAGING = "staging"
_PHASE_BACKUP = "backup"
_PHASE_ACTIVATING = "activating"
_PHASE_ACTIVATED = "activated"
_PHASE_VERIFYING = "verifying"
_PHASE_VERIFIED = "verified"
_PHASE_COMMITTED = "committed"
_PHASE_ROLLED_BACK = "rolled_back"
# 磁盘已回滚、等待 collector 确认旧版 loaded / 目标 absent
_PHASE_ROLLBACK_VERIFYING = "rollback_verifying"
# 兼容旧日志：过早写入的 finalizing 视为未验证，一律回滚
_PHASE_FINALIZING_LEGACY = "finalizing"
_ROLLBACK_PHASES = frozenset(
    {
        _PHASE_STAGING,
        _PHASE_BACKUP,
        _PHASE_ACTIVATING,
        _PHASE_ACTIVATED,
        _PHASE_VERIFYING,
        _PHASE_ROLLBACK_VERIFYING,
        _PHASE_FINALIZING_LEGACY,
    }
)
_COMMIT_PHASES = frozenset({_PHASE_VERIFIED})
_TERMINAL_PHASES = frozenset({_PHASE_COMMITTED, _PHASE_ROLLED_BACK})

# 卸载事务阶段：collector_verified 前崩溃恢复目录；之后完成 trash 清理
_PHASE_UNINSTALL_PREPARED = "uninstall_prepared"
_PHASE_MOVED_TO_TRASH = "moved_to_trash"
_PHASE_COLLECTOR_VERIFIED = "collector_verified"
_UNINSTALL_ROLLBACK_PHASES = frozenset(
    {
        _PHASE_UNINSTALL_PREPARED,
        _PHASE_MOVED_TO_TRASH,
        _PHASE_ROLLBACK_VERIFYING,
    }
)
_UNINSTALL_COMMIT_PHASES = frozenset({_PHASE_COLLECTOR_VERIFIED})


class DriverInstallError(Exception):
    """安装事务失败（含业务冲突）。"""

    def __init__(self, message: str, *, status_code: int = 400):
        super().__init__(message)
        self.status_code = status_code


class InconsistentStateError(DriverInstallError):
    """state.json 与激活目录不一致。"""

    def __init__(self, message: str):
        super().__init__(message, status_code=409)


@dataclass
class InstallResult:
    """安装/升级结果。"""

    driver: dict[str, Any]
    created: bool  # True → HTTP 201；False → 幂等 200
    rolled_back: bool = False


RestartCollectorFn = Callable[[], Awaitable[None]]
CollectorAliveFn = Callable[[], Awaitable[bool]]


class DriverInstaller:
    """同一时刻只允许一个驱动变更操作。"""

    def __init__(
        self,
        drivers_root: Path,
        *,
        stop_collector: RestartCollectorFn | None = None,
        collector_pid: Callable[[], int | None] | None = None,
        catalog_path: Path | None = None,
        health_wait_seconds: float = _HEALTH_WAIT_SECONDS,
        health_poll_interval: float = _HEALTH_POLL_INTERVAL,
    ):
        self.drivers_root = Path(drivers_root)
        self._collector_pid = collector_pid
        self._previous_boot_id: str | None = None
        self._stop_collector = stop_collector
        self._catalog_path_override = catalog_path
        self._operation_was_running = True
        self.staging_root = self.drivers_root / ".staging"
        self.backup_root = self.drivers_root / ".backup"
        self.trash_root = self.drivers_root / ".trash"
        self.state_path = self.drivers_root / "state.json"
        # 持久化事务日志（进程崩溃后启动恢复用）
        self.operation_log_path = self.drivers_root / "driver_operations.jsonl"
        self._lock = asyncio.Lock()
        self._health_wait_seconds = health_wait_seconds
        self._health_poll_interval = health_poll_interval
        # 启动恢复未通过 collector 验证时置位；阻止新的安装/卸载
        self._recovery_blocked = False
        self._recovery_block_reasons: list[dict[str, Any]] = []

    def ensure_layout(self) -> None:
        self.drivers_root.mkdir(parents=True, exist_ok=True)
        self.staging_root.mkdir(parents=True, exist_ok=True)
        self.backup_root.mkdir(parents=True, exist_ok=True)
        self.trash_root.mkdir(parents=True, exist_ok=True)
        if not self.state_path.exists():
            self._write_state({"drivers": {}, "operation": None})

    def apply_disk_recovery(self) -> list[dict[str, Any]]:
        """仅恢复磁盘与 state，不写终态；返回待 collector 验证的计划。

        每项含：op_id/driver_id/version/type/action/expect/phase/previous_record。
        expect=present|absent，供 /drivers 探针校验运行代际。
        """
        self.ensure_layout()
        plans: list[dict[str, Any]] = []
        for op in self._latest_incomplete_operations():
            phase = str(op.get("phase") or "")
            op_id = str(op.get("op_id") or "")
            driver_id = str(op.get("driver_id") or "")
            op_type = str(op.get("type") or "install")
            try:
                plan: Optional[dict[str, Any]] = None
                if op_type == "uninstall":
                    if phase in _UNINSTALL_ROLLBACK_PHASES:
                        plan = self._apply_uninstall_rollback_disk(op)
                    elif phase in _UNINSTALL_COMMIT_PHASES:
                        plan = self._apply_uninstall_commit_disk(op)
                    else:
                        logger.warning("忽略未知卸载 phase 的残留操作: %s", op)
                        continue
                elif phase in _ROLLBACK_PHASES:
                    plan = self._apply_install_rollback_disk(op)
                elif phase in _COMMIT_PHASES:
                    plan = self._apply_install_commit_disk(op)
                else:
                    logger.warning("忽略未知 phase 的残留操作: %s", op)
                    continue
                if plan is not None:
                    plans.append(plan)
                    logger.info(
                        "磁盘已恢复待验证 op_id=%s type=%s driver=%s phase=%s → %s expect=%s",
                        op_id,
                        op_type,
                        driver_id,
                        phase,
                        plan.get("action"),
                        plan.get("expect"),
                    )
            except Exception as exc:
                logger.exception(
                    "磁盘恢复失败 op_id=%s type=%s driver=%s phase=%s",
                    op_id,
                    op_type,
                    driver_id,
                    phase,
                )
                # 明确失败项，避免后续误判「全部成功」而解除门禁
                plans.append(
                    {
                        "op_id": op_id,
                        "driver_id": driver_id,
                        "version": str(op.get("version") or ""),
                        "type": op_type,
                        "phase": phase,
                        "action": "disk_failed",
                        "expect": None,
                        "disk_failed": True,
                        "previous_record": op.get("previous_record"),
                        "error": str(exc),
                    }
                )
        return plans

    async def recover_incomplete_operations(
        self,
        *,
        restart_collector: RestartCollectorFn,
        collector_alive: CollectorAliveFn,
    ) -> list[dict[str, Any]]:
        """磁盘恢复 → 统一重启 collector → /drivers 验证 → 成功才写终态。

        与安装/卸载共用 _lock，防止并发 recover 交叉改目录。
        验证失败时保留未完成事务，不得记 committed/rolled_back。
        """
        async with self._lock:
            return await self._recover_incomplete_operations_unlocked(
                restart_collector=restart_collector,
                collector_alive=collector_alive,
            )

    async def _recover_incomplete_operations_unlocked(
        self,
        *,
        restart_collector: RestartCollectorFn,
        collector_alive: CollectorAliveFn,
    ) -> list[dict[str, Any]]:
        operations = self._latest_incomplete_operations()
        if operations:
            await self._stop_before_switch(collector_alive)
        plans = self.apply_disk_recovery()
        restore_running = any(op.get("was_running", True) for op in operations)
        if not plans:
            self._update_recovery_block_from_results([])
            return []

        disk_failed = [p for p in plans if p.get("disk_failed")]
        verify_plans = [p for p in plans if not p.get("disk_failed")]
        results: list[dict[str, Any]] = [
            {**p, "verified": False, "error": p.get("error") or "磁盘恢复失败"}
            for p in disk_failed
        ]

        if verify_plans:
            try:
                if restore_running:
                    await restart_collector()
                    alive = await self._wait_alive(collector_alive)
                    if not alive:
                        logger.error("恢复后 collector 未在超时内恢复，保留未完成事务")
                        results.extend(
                            {
                                **plan,
                                "verified": False,
                                "error": "collector 未恢复运行",
                            }
                            for plan in verify_plans
                        )
                        self._update_recovery_block_from_results(results)
                        return results
            except Exception as exc:
                logger.exception("恢复后重启 collector 失败，保留未完成事务")
                results.extend(
                    {**plan, "verified": False, "error": str(exc)}
                    for plan in verify_plans
                )
                self._update_recovery_block_from_results(results)
                return results

            for plan in verify_plans:
                try:
                    self._operation_was_running = bool(plan.get("was_running", True))
                    ok = await self._verify_recover_plan(plan)
                    if not ok:
                        logger.error(
                            "collector 代际验证失败，保留事务 op_id=%s driver=%s expect=%s",
                            plan.get("op_id"),
                            plan.get("driver_id"),
                            plan.get("expect"),
                        )
                        results.append(
                            {
                                **plan,
                                "verified": False,
                                "error": "collector /drivers 与期望不一致",
                            }
                        )
                        continue
                    self._finalize_recover_plan(plan)
                    results.append({**plan, "verified": True, "error": None})
                    logger.info(
                        "已验证并终结驱动事务 op_id=%s type=%s driver=%s → %s",
                        plan.get("op_id"),
                        plan.get("type"),
                        plan.get("driver_id"),
                        plan.get("action"),
                    )
                except Exception as exc:
                    logger.exception(
                        "终结恢复失败 op_id=%s driver=%s",
                        plan.get("op_id"),
                        plan.get("driver_id"),
                    )
                    results.append({**plan, "verified": False, "error": str(exc)})

        self._update_recovery_block_from_results(results)
        return results

    @property
    def recovery_blocked(self) -> bool:
        return self._recovery_blocked

    def recovery_status(self) -> dict[str, Any]:
        return {
            "blocked": self._recovery_blocked,
            "reasons": list(self._recovery_block_reasons),
            "incomplete": self._latest_incomplete_operations(),
        }

    def mark_recovery_blocked(self, reason: str) -> None:
        """启动恢复异常时由 lifespan 调用。"""
        self._recovery_blocked = True
        self._recovery_block_reasons = [{"error": reason, "verified": False}]

    def _update_recovery_block_from_results(self, results: list[dict[str, Any]]) -> None:
        """门禁以「结果失败 ∪ 残留 incomplete」为准，避免部分失败被误解除。"""
        failed = [r for r in results if not r.get("verified")]
        leftover = self._latest_incomplete_operations()
        if failed or leftover:
            self._recovery_blocked = True
            reasons: list[dict[str, Any]] = list(failed)
            seen = {str(r.get("op_id") or "") for r in reasons}
            for op in leftover:
                op_id = str(op.get("op_id") or "")
                if op_id and op_id in seen:
                    continue
                reasons.append(
                    {
                        **op,
                        "verified": False,
                        "error": op.get("error") or "残留未完成事务",
                    }
                )
                if op_id:
                    seen.add(op_id)
            self._recovery_block_reasons = reasons
            return
        self._recovery_blocked = False
        self._recovery_block_reasons = []

    def _assert_mutation_allowed(self, driver_id: str = "") -> None:
        """恢复失败或同驱动未完成事务时，拒绝新的安装/卸载。"""
        from app.core.role import managed_module_names
        if "collector" not in managed_module_names():
            raise DriverInstallError("此节点不运行 Collector，请在目标采集节点管理驱动", status_code=409)
        if self._recovery_blocked:
            raise DriverInstallError(
                "驱动恢复未完成（driver_recovery_blocked），请先 POST /admin/drivers/recovery/retry",
                status_code=503,
            )
        want = str(driver_id or "").strip()
        for op in self._latest_incomplete_operations():
            op_driver = str(op.get("driver_id") or "").strip()
            if want and op_driver == want:
                raise DriverInstallError(
                    f"驱动 {want} 仍有未完成事务 phase={op.get('phase')}，拒绝新变更",
                    status_code=503,
                )
            if op_driver:
                # All packages share one Collector; incomplete work blocks other mutations too.
                raise DriverInstallError(
                    f"存在未完成驱动事务 driver={op_driver} phase={op.get('phase')}，拒绝新安装",
                    status_code=503,
                )

    def _node_identity(self) -> dict[str, str]:
        from app.core.role import build_role
        host = socket.gethostname()
        key = host + "|" + str(self.drivers_root.parent.resolve()).casefold()
        return {"id": hashlib.sha256(key.encode("utf-8")).hexdigest()[:24], "host": host, "deployment_role": build_role()}

    def _catalog_path(self) -> Path:
        if self._catalog_path_override is not None:
            return self._catalog_path_override
        from app.core.config import install_root, module_paths
        executable, _ = module_paths("collector")
        if getattr(sys, "frozen", False) or executable.is_file():
            return executable.parent / "driver-catalog/catalog.json"
        return install_root() / "packages/driver-catalog/catalog.json"

    def _bundled_inventory(self) -> list[dict[str, Any]]:
        from app.core.role import managed_module_names
        from data_watch_driver_catalog import read_catalog
        if "collector" not in managed_module_names():
            return []
        try:
            return [{**item, "source": "official", "protected": False} for item in read_catalog(self._catalog_path())["drivers"]]
        except (OSError, ValueError) as exc:
            logger.warning("无法读取离线驱动目录: %s", exc)
            return []

    async def _stop_before_switch(self, collector_alive: CollectorAliveFn) -> None:
        if not await collector_alive():
            return
        if self._stop_collector is None:
            raise DriverInstallError("未配置 Collector 停止操作，不能切换驱动目录", status_code=409)
        try:
            old = await asyncio.to_thread(self._fetch_json, f"{_COLLECTOR_STATUS_URL}/drivers")
            if self._valid_instance(old):
                self._previous_boot_id = old["instance"]["boot_id"]
        except Exception:
            pass
        await self._stop_collector()
        if await collector_alive():
            raise DriverInstallError("Collector 尚未退出，已取消驱动目录切换", status_code=409)

    def _assert_trusted_package(self, validated: ValidatedDriverPackage) -> None:
        if validated.signature_verified or validated.publisher == "datawatch":
            return  # Static validation already pins official packages to the release catalog.
        from data_watch_driver_catalog import read_catalog
        try:
            catalog = read_catalog(self._catalog_path())
        except (OSError, ValueError) as exc:
            raise DriverInstallError("缺少可信发布目录，不能安装第三方驱动", status_code=403) from exc
        approved = any(item.get("id") == validated.driver_id
            and item.get("publisher") == validated.publisher and bool(validated.publisher)
            and item.get("version") == validated.version
            and item.get("tree_sha256") == validated.tree_sha256
            for item in catalog.get("approved_packages", []))
        if not approved:
            raise DriverInstallError("此驱动未经过内部来源审核；请由发布管理员审核并加入可信目录", status_code=403)

    async def _preflight_package(self, package: Path) -> dict[str, Any]:
        from app.core.config import install_root, module_paths
        executable, _ = module_paths("collector")
        if executable.is_file():
            command = [str(executable)]
        else:
            root = install_root()
            python = root / "collector/.venv-pack/Scripts/python.exe"
            if not python.is_file():
                raise DriverInstallError("找不到目标 Collector 运行时，无法预检", status_code=503)
            command = [str(python), str(root / "collector/driver_preflight.py")]
        env = dict(os.environ, PYTHONIOENCODING="utf-8", DATAWATCH_DATA_ROOT=str(self.drivers_root.parent), DATAWATCH_DRIVER_CATALOG_ROOT=str(self._catalog_path().parent))
        approval_args = []
        from data_watch_driver_manifest import load_driver_toml
        from data_watch_driver_signatures import verify_package_signature
        from data_watch_driver_catalog import read_catalog, tree_digest
        if (package / "signature.json").is_file() and verify_package_signature(package, load_driver_toml(package / "driver.toml"), read_catalog(self._catalog_path())):
            approval_args = ["--approved-digest", tree_digest(package)]
        process = await asyncio.create_subprocess_exec(*command, *approval_args, "--driver-preflight", str(package), "--active-drivers", str(self.drivers_root), stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE, env=env)
        try:
            stdout, stderr = await asyncio.wait_for(process.communicate(), timeout=45)
        except (TimeoutError, asyncio.CancelledError):
            process.kill()
            await process.wait()
            raise
        try:
            result = json.loads(stdout.decode("utf-8").strip().splitlines()[-1])
        except (ValueError, IndexError):
            raise DriverInstallError("Collector 预检未返回有效结果: " + stderr.decode("utf-8", errors="replace")[-2000:], status_code=422)
        if process.returncode or result.get("status") != "verified":
            raise DriverInstallError("驱动预检失败: " + str(result.get("error") or "unknown"), status_code=422)
        return result

    async def initialize_preinstalled(self, *, restart_collector: RestartCollectorFn, collector_alive: CollectorAliveFn) -> list[str]:
        """First installation only. A completed catalog never restores user removals."""
        from data_watch_driver_catalog import read_catalog, file_digest
        async with self._lock:
            self.ensure_layout()
            state = self._read_state()
            if state.get("preinstall_completed"):
                return []
            catalog_path = self._catalog_path()
            catalog = read_catalog(catalog_path)
            was_running = state.get("preinstall_was_running", await collector_alive())
            state["preinstall_was_running"] = was_running
            self._write_state(state)
            await self._stop_before_switch(collector_alive)
            installed = []
            async def stopped():
                return False
            for item in catalog["drivers"]:
                current = self._read_state()
                if item["id"] in current.get("removed", []) or item["id"] in current.get("drivers", {}) or (self.drivers_root / item["id"]).exists():
                    continue
                archive = catalog_path.parent / item["archive"]
                if file_digest(archive) != item["sha256"]:
                    raise DriverInstallError(f"离线包校验失败: {item['id']}", status_code=422)
                await self._install_locked(archive.read_bytes(), restart_collector=restart_collector, collector_alive=stopped, operator="preinstall")
                installed.append(item["id"])
            if was_running:
                await restart_collector()
                if not await self._wait_alive(collector_alive):
                    raise DriverInstallError("预装后 Collector 未恢复运行", status_code=503)
                for driver_id, record in self._read_state().get("drivers", {}).items():
                    if not await self._wait_for_driver_loaded(driver_id=driver_id, version=record.get("version", "")):
                        raise DriverInstallError(f"预装后驱动未加载: {driver_id}", status_code=503)
            state = self._read_state()
            state["preinstall_completed"] = True
            state.pop("preinstall_was_running", None)
            self._write_state(state)
            return installed

    def restoration_archive(self, driver_id: str) -> bytes:
        from data_watch_driver_catalog import read_catalog, file_digest
        path = self._catalog_path()
        item = next((item for item in read_catalog(path)["drivers"] if item["id"] == driver_id), None)
        if item is None:
            raise DriverInstallError("离线目录没有该驱动", status_code=404)
        archive = path.parent / item["archive"]
        if file_digest(archive) != item["sha256"]:
            raise DriverInstallError("离线恢复包校验失败", status_code=422)
        return archive.read_bytes()

    async def list_drivers(self, *, collector_status: str = "unknown", collector_pid: int | None = None) -> list[dict[str, Any]]:
        """Static installation inventory and runtime observations are independent."""
        self.ensure_layout()
        state_map = {
            str(raw["id"]).strip(): dict(raw)
            for raw in self._read_state().get("drivers", {}).values()
            if isinstance(raw, dict) and raw.get("id")
        }
        removed_map = self._read_state().get("removed_drivers", {})
        disk_ids = {
            child.name for child in self.drivers_root.iterdir()
            if child.is_dir() and not child.name.startswith(".")
            and (child / "driver.toml").is_file()
        }
        bundled = {item["id"]: item for item in await asyncio.to_thread(self._bundled_inventory)}
        stopped = collector_status in {"stopped", "crashed"}
        collector_map = None if stopped else await self._probe_collector_drivers()
        if collector_map:
            for runtime in collector_map.values():
                identity = runtime.get("instance", {})
                if collector_pid is not None and identity.get("pid") != collector_pid:
                    collector_map = None
                    break
        runtime_ids = set(collector_map or {})
        items = []
        for driver_id in sorted(set(bundled) | set(state_map) | set(removed_map) | disk_ids | runtime_ids):
            runtime = dict((collector_map or {}).get(driver_id) or {})
            base = {**bundled.get(driver_id, {}), **removed_map.get(driver_id, {}), **state_map.get(driver_id, {}), "id": driver_id}
            base["node"] = self._node_identity()
            base["protected"] = False
            if driver_id in disk_ids:
                installation = "installed"
                try:
                    from data_watch_driver_manifest import load_driver_toml
                    from data_watch_driver_catalog import tree_digest
                    manifest = load_driver_toml(self.drivers_root / driver_id / "driver.toml")
                    base.update(protocol=manifest.protocol, version=manifest.version, aliases=list(manifest.aliases), capabilities=list(manifest.capabilities), readWriteType=manifest.read_write, publisher=manifest.publisher)
                    expected = state_map.get(driver_id, {}).get("tree_sha256")
                    if expected and await asyncio.to_thread(tree_digest, self.drivers_root / driver_id) != expected:
                        installation = "invalid"
                        base["error"] = "激活包完整性校验失败"
                    else:
                        base.pop("error", None)
                except (OSError, ValueError) as exc:
                    installation = "invalid"
                    base["error"] = str(exc)
            elif driver_id in state_map:
                installation = "missing"
                base["error"] = "驱动目录或 driver.toml 缺失"
            else:
                installation = "not_installed"
                base.pop("error", None)
            base["source"] = "official" if base.get("publisher") == "datawatch" else "installed" if driver_id in state_map else "untracked"
            base["install_status"] = installation
            if stopped:
                runtime_status = "collector_stopped"
            elif collector_map is None:
                runtime_status = "unknown"
            elif driver_id in runtime_ids:
                runtime_status = str(runtime.get("status") or "loaded")
            else:
                runtime_status = "not_loaded"
            base["runtime_status"] = runtime_status
            if runtime.get("instance"):
                base["instance"] = runtime["instance"]
            if installation in {"missing", "invalid", "unknown", "not_installed"}:
                base["status"] = installation
                if installation == "missing" and runtime_status == "loaded":
                    base["error"] += "（当前 collector 进程仍加载）"
            elif runtime_status == "loaded":
                base["status"] = "loaded"
                base["error"] = None
            elif runtime_status in {"load_failed", "not_loaded"}:
                base["status"] = "load_failed"
                base["error"] = runtime.get("error") or "collector 未加载该驱动"
            else:
                base["status"] = "installed"
                base["error"] = None if stopped else "Collector 状态不可核验：探针不可达或实例身份不匹配"
            if driver_id not in bundled and runtime.get("version"):
                base["version"] = runtime["version"]
            items.append(base)
        return items

    def _valid_instance(self, payload: Any, *, require_new: bool = False) -> bool:
        from app.core.role import build_role
        if not isinstance(payload, dict):
            return False
        identity = payload.get("instance")
        if not isinstance(identity, dict):
            return False
        valid = (identity.get("role") == "collector"
            and identity.get("deployment_role") == build_role()
            and bool(identity.get("boot_id")) and bool(identity.get("build_id"))
            and isinstance(identity.get("data_root"), str)
            and Path(identity["data_root"]).resolve() == self.drivers_root.parent.resolve())
        if self._collector_pid is not None:
            pid = self._collector_pid()
            valid = valid and pid is not None and identity.get("pid") == pid
        if require_new and self._previous_boot_id:
            valid = valid and identity.get("boot_id") != self._previous_boot_id
        return bool(valid)

    async def _probe_collector_drivers(self) -> Optional[dict[str, dict[str, Any]]]:
        """异步拉取 collector /drivers；不可达返回 None。"""
        url = f"{_COLLECTOR_STATUS_URL.rstrip('/')}/drivers"
        try:
            payload = await asyncio.to_thread(self._fetch_json, url)
        except Exception as exc:
            logger.debug("列表合并 collector /drivers 失败: %s", exc)
            return None
        if not self._valid_instance(payload):
            return None
        identity = payload["instance"]
        drivers = payload.get("drivers")
        if not isinstance(drivers, list):
            return None
        result: dict[str, dict[str, Any]] = {}
        for item in drivers:
            if not isinstance(item, dict):
                continue
            driver_id = str(item.get("id") or "").strip()
            if driver_id:
                result[driver_id] = {**item, "instance": identity}
        return result

    def get_driver(self, driver_id: str) -> Optional[dict[str, Any]]:
        self.ensure_layout()
        return self._read_state().get("drivers", {}).get(driver_id)

    async def preview(self, zip_bytes: bytes) -> dict[str, Any]:
        """Validate the proposed package without stopping collection or activating files."""
        from data_watch_driver_catalog import tree_digest
        async with self._lock:
            self._assert_mutation_allowed()
            self.ensure_layout()
            staging = self.staging_root / uuid.uuid4().hex
            try:
                validated = validate_and_extract(zip_bytes, staging, catalog_path=self._catalog_path())
                self._assert_trusted_package(validated)
                package = staging / validated.package_root_name
                if not package.is_dir():
                    package = staging
                result = await self._preflight_package(package)
                if result.get("tree_sha256") != validated.tree_sha256 or tree_digest(package) != validated.tree_sha256:
                    raise DriverInstallError("预检期间驱动内容发生变化", status_code=409)
                return {"id": validated.driver_id, "name": validated.name, "version": validated.version,
                        "sha256": validated.sha256, "preflight_verified": True}
            except StagingPathConflictError:
                staging = None
                raise
            finally:
                if staging is not None:
                    shutil.rmtree(staging, ignore_errors=True)

    async def install(
        self,
        zip_bytes: bytes,
        *,
        restart_collector: RestartCollectorFn,
        collector_alive: CollectorAliveFn,
        operator: str = "",
    ) -> InstallResult:
        async with self._lock:
            self._assert_mutation_allowed("")
            return await self._install_locked(
                zip_bytes,
                restart_collector=restart_collector,
                collector_alive=collector_alive,
                operator=operator,
            )

    async def _install_locked(
        self,
        zip_bytes: bytes,
        *,
        restart_collector: RestartCollectorFn,
        collector_alive: CollectorAliveFn,
        operator: str,
    ) -> InstallResult:
        self.ensure_layout()
        operation_id = uuid.uuid4().hex
        staging_dir = self.staging_root / operation_id

        try:
            validated = validate_and_extract(zip_bytes, staging_dir, catalog_path=self._catalog_path())
        except StagingPathConflictError:
            # 目录非本次操作创建，绝不能清理攻击者或另一进程预置的路径。
            raise
        except DriverZipError:
            shutil.rmtree(staging_dir, ignore_errors=True)
            raise
        except Exception as exc:
            logger.exception("驱动包校验失败")
            shutil.rmtree(staging_dir, ignore_errors=True)
            raise DriverInstallError("驱动包校验失败", status_code=400) from exc

        package_src = staging_dir / validated.package_root_name
        if not package_src.is_dir():
            # 根级 driver.toml 布局：staging 本身即包内容
            package_src = staging_dir

        existing = self.get_driver(validated.driver_id)
        # Only a matching on-disk tree is idempotent; missing/damaged packages can be repaired.
        from data_watch_driver_catalog import tree_digest
        active_check = self.drivers_root / validated.driver_id
        if (existing and existing.get("version") == validated.version
                and existing.get("sha256") == validated.sha256
                and active_check.is_dir() and tree_digest(active_check) == validated.tree_sha256):
            shutil.rmtree(staging_dir, ignore_errors=True)
            return InstallResult(driver=existing, created=False)

        # 同版本不同内容 → 冲突
        if (
            existing
            and existing.get("version") == validated.version
            and existing.get("sha256") != validated.sha256
        ):
            shutil.rmtree(staging_dir, ignore_errors=True)
            raise DriverInstallError(
                f"驱动 {validated.driver_id} 版本 {validated.version} 已存在但内容不同",
                status_code=409,
            )

        active_dir = self.drivers_root / validated.driver_id
        backup_dir: Optional[Path] = None
        previous_record = dict(existing) if existing else None

        try:
            self._assert_trusted_package(validated)
            preflight = await self._preflight_package(package_src)
            if preflight.get("tree_sha256") != validated.tree_sha256 or tree_digest(package_src) != validated.tree_sha256:
                raise DriverInstallError("预检期间驱动内容发生变化", status_code=409)
        except BaseException:
            self._cleanup_staging(operation_id)
            raise
        self._operation_was_running = await collector_alive()

        # 进入 staging：记录事务日志（崩溃后据此回滚）
        self._persist_operation(
            operation_id,
            _PHASE_STAGING,
            validated.driver_id,
            validated.version,
            previous_record=previous_record,
            operator=operator,
        )

        try:
            await self._stop_before_switch(collector_alive)
        except Exception:
            self._persist_operation(operation_id, _PHASE_ROLLED_BACK, validated.driver_id, validated.version, previous_record=previous_record, operator=operator)
            self._cleanup_staging(operation_id)
            raise

        try:
            # 备份现有激活目录
            if active_dir.exists():
                backup_dir = (
                    self.backup_root
                    / validated.driver_id
                    / (existing.get("version") if existing else "unknown")
                )
                self._persist_operation(
                    operation_id,
                    _PHASE_BACKUP,
                    validated.driver_id,
                    validated.version,
                    previous_record=previous_record,
                    operator=operator,
                )
                if backup_dir.exists():
                    shutil.rmtree(backup_dir, ignore_errors=True)
                backup_dir.parent.mkdir(parents=True, exist_ok=True)
                shutil.move(str(active_dir), str(backup_dir))

            # 原子激活：staging 包根 → drivers/<id>
            self._persist_operation(
                operation_id,
                _PHASE_ACTIVATING,
                validated.driver_id,
                validated.version,
                previous_record=previous_record,
                operator=operator,
            )
            if package_src.resolve() == staging_dir.resolve():
                # 根级布局：将 staging 改名为临时再移入 active
                tmp_named = staging_dir.parent / f".{operation_id}_{validated.driver_id}"
                if tmp_named.exists():
                    shutil.rmtree(tmp_named, ignore_errors=True)
                shutil.move(str(staging_dir), str(tmp_named))
                shutil.move(str(tmp_named), str(active_dir))
            else:
                shutil.move(str(package_src), str(active_dir))
                shutil.rmtree(staging_dir, ignore_errors=True)

            record = self._build_record(validated, status="installed", operator=operator)
            self._upsert_driver_record(record)
            # 目录与 state 已就位，但尚未健康验证：崩溃只能回滚，不能提交
            self._persist_operation(
                operation_id,
                _PHASE_ACTIVATED,
                validated.driver_id,
                validated.version,
                previous_record=previous_record,
                operator=operator,
            )
            self._persist_operation(
                operation_id,
                _PHASE_VERIFYING,
                validated.driver_id,
                validated.version,
                previous_record=previous_record,
                operator=operator,
            )

            if self._operation_was_running:
                # 冷加载：重启 collector → 进程存活 → 注册表真实 loaded
                try:
                    await restart_collector()
                    alive = await self._wait_alive(collector_alive)
                    if not alive:
                        raise DriverInstallError(
                            "collector 重启后未在超时内恢复运行",
                            status_code=503,
                        )
                    loaded = await self._wait_for_driver_loaded(
                        driver_id=validated.driver_id,
                        version=validated.version,
                    )
                    if not loaded:
                        raise DriverInstallError(
                            f"collector 未成功加载驱动 {validated.driver_id}@{validated.version}",
                            status_code=503,
                        )
                except DriverInstallError:
                    raise
                except Exception as exc:
                    raise DriverInstallError(
                        f"collector 重启失败: {exc}",
                        status_code=503,
                    ) from exc

            # 探针通过后才允许启动恢复提交；此前崩溃必须回滚
            self._persist_operation(
                operation_id,
                _PHASE_VERIFIED,
                validated.driver_id,
                validated.version,
                previous_record=previous_record,
                operator=operator,
            )
            record = self._build_record(validated, status="loaded" if self._operation_was_running else "installed", operator=operator)
            self._upsert_driver_record(record)
            self._prune_driver_backups(
                validated.driver_id,
                keep_version=str((previous_record or {}).get("version") or ""),
            )
            self._persist_operation(
                operation_id,
                _PHASE_COMMITTED,
                validated.driver_id,
                validated.version,
                previous_record=previous_record,
                operator=operator,
            )
            # 成功后仅保留一份上一版本备份（已在 backup_dir）；清理 staging 残留
            self._cleanup_staging(operation_id)
            state = self._read_state()
            state["removed"] = [key for key in state.get("removed", []) if key != validated.driver_id]
            state.get("removed_drivers", {}).pop(validated.driver_id, None)
            self._write_state(state)
            return InstallResult(driver=record, created=previous_record is None)

        except Exception as exc:
            logger.exception("驱动安装失败，执行回滚: %s", validated.driver_id)
            try:
                await self._stop_before_switch(collector_alive)
            except Exception as stop_error:
                self.mark_recovery_blocked(str(stop_error))
                raise
            self._rollback_activate(
                driver_id=validated.driver_id,
                active_dir=active_dir,
                backup_dir=backup_dir,
                previous_record=previous_record,
                error=str(exc),
            )
            await self._verify_then_mark_rolled_back(
                operation_id=operation_id,
                driver_id=validated.driver_id,
                version=str(
                    (previous_record or {}).get("version") or validated.version or ""
                ),
                previous_record=previous_record,
                operator=operator,
                op_type="install",
                expect_present=previous_record is not None,
                restart_collector=restart_collector,
                collector_alive=collector_alive,
            )
            self._cleanup_staging(operation_id)
            if isinstance(exc, (DriverInstallError, DriverZipError)):
                raise
            raise DriverInstallError(str(exc), status_code=503) from exc

    async def uninstall(
        self,
        driver_id: str,
        *,
        restart_collector: RestartCollectorFn,
        collector_alive: CollectorAliveFn,
        operator: str = "",
        allow_builtin_guard: bool = True,
    ) -> dict[str, Any]:
        async with self._lock:
            self._assert_mutation_allowed(driver_id)
            return await self._uninstall_locked(
                driver_id,
                restart_collector=restart_collector,
                collector_alive=collector_alive,
                operator=operator,
                allow_builtin_guard=allow_builtin_guard,
            )

    async def _uninstall_locked(
        self,
        driver_id: str,
        *,
        restart_collector: RestartCollectorFn,
        collector_alive: CollectorAliveFn,
        operator: str,
        allow_builtin_guard: bool,
    ) -> dict[str, Any]:
        self.ensure_layout()
        if not is_valid_driver_id(driver_id):
            raise DriverInstallError(f"非法驱动 id: {driver_id!r}", status_code=400)

        record = self.get_driver(driver_id)
        root_resolved = self.drivers_root.resolve()
        active_dir = (self.drivers_root / driver_id).resolve()
        if active_dir.parent != root_resolved:
            raise DriverInstallError("驱动目录必须是 drivers 根目录的直接子目录", status_code=400)
        if active_dir.exists() and (
            not active_dir.is_dir() or not (active_dir / "driver.toml").is_file()
        ):
            raise DriverInstallError(
                f"驱动目录缺少合法 driver.toml: {driver_id}", status_code=409
            )
        if record is None and not active_dir.exists():
            raise DriverInstallError(f"驱动未安装: {driver_id}", status_code=404)

        self._operation_was_running = await collector_alive()

        operation_id = uuid.uuid4().hex
        trash_dir = self.trash_root / operation_id / driver_id
        saved_record = dict(record) if record else None
        version = str((saved_record or {}).get("version") or "")

        self._persist_operation(
            operation_id,
            _PHASE_UNINSTALL_PREPARED,
            driver_id,
            version,
            previous_record=saved_record,
            operator=operator,
            op_type="uninstall",
        )

        try:
            await self._stop_before_switch(collector_alive)
        except Exception:
            self._persist_operation(operation_id, _PHASE_ROLLED_BACK, driver_id, version, previous_record=saved_record, operator=operator, op_type="uninstall")
            raise

        try:
            if active_dir.exists():
                trash_dir.parent.mkdir(parents=True, exist_ok=True)
                if trash_dir.exists():
                    shutil.rmtree(trash_dir, ignore_errors=True)
                shutil.move(str(active_dir), str(trash_dir))

            # 目录已入 trash：崩溃需从 trash 还原，不能完成卸载
            self._persist_operation(
                operation_id,
                _PHASE_MOVED_TO_TRASH,
                driver_id,
                version,
                previous_record=saved_record,
                operator=operator,
                op_type="uninstall",
            )

            # 先从 state 移除，collector 确认后再删 trash
            state = self._read_state()
            state.get("drivers", {}).pop(driver_id, None)
            self._write_state(state)

            if self._operation_was_running:
                try:
                    await restart_collector()
                    alive = await self._wait_alive(collector_alive)
                    if not alive:
                        raise DriverInstallError(
                            "卸载后 collector 未在超时内恢复",
                            status_code=503,
                        )
                    unloaded = await self._wait_for_driver_unloaded(driver_id)
                    if not unloaded:
                        raise DriverInstallError(
                            f"collector 重启后仍加载驱动 {driver_id}，卸载未生效",
                            status_code=503,
                        )
                except DriverInstallError:
                    raise
                except Exception as exc:
                    raise DriverInstallError(
                        f"collector 重启失败: {exc}",
                        status_code=503,
                    ) from exc

            # registry 已确认无此驱动：崩溃后应完成 trash 清理，不可还原
            self._persist_operation(
                operation_id,
                _PHASE_COLLECTOR_VERIFIED,
                driver_id,
                version,
                previous_record=saved_record,
                operator=operator,
                op_type="uninstall",
            )
            state = self._read_state()
            state["removed"] = sorted(set(state.get("removed", [])) | {driver_id})
            state.setdefault("removed_drivers", {})[driver_id] = saved_record or {"id": driver_id}
            self._write_state(state)
            shutil.rmtree(trash_dir.parent, ignore_errors=True)
            self._persist_operation(
                operation_id,
                _PHASE_COMMITTED,
                driver_id,
                version,
                previous_record=saved_record,
                operator=operator,
                op_type="uninstall",
            )
            return {
                "id": driver_id,
                "status": "uninstalled",
                "operator": operator,
                "updated_at": _utc_now(),
            }
        except Exception as exc:
            logger.exception("驱动卸载失败，执行还原: %s", driver_id)
            try:
                await self._stop_before_switch(collector_alive)
            except Exception as stop_error:
                self.mark_recovery_blocked(str(stop_error))
                raise
            # 还原目录与 state
            if trash_dir.exists() and not active_dir.exists():
                shutil.move(str(trash_dir), str(active_dir))
            if saved_record is not None:
                self._upsert_driver_record(saved_record)
            await self._verify_then_mark_rolled_back(
                operation_id=operation_id,
                driver_id=driver_id,
                version=version,
                previous_record=saved_record,
                operator=operator,
                op_type="uninstall",
                expect_present=True,
                restart_collector=restart_collector,
                collector_alive=collector_alive,
            )
            shutil.rmtree(self.trash_root / operation_id, ignore_errors=True)
            if isinstance(exc, DriverInstallError):
                raise
            raise DriverInstallError(str(exc), status_code=503) from exc

    async def _verify_then_mark_rolled_back(
        self,
        *,
        operation_id: str,
        driver_id: str,
        version: str,
        previous_record: Optional[dict[str, Any]],
        operator: str,
        op_type: str,
        expect_present: bool,
        restart_collector: RestartCollectorFn,
        collector_alive: CollectorAliveFn,
    ) -> None:
        """磁盘回滚后必须经 /drivers 探针，通过才写 rolled_back。"""
        self._persist_operation(
            operation_id,
            _PHASE_ROLLBACK_VERIFYING,
            driver_id,
            version,
            previous_record=previous_record,
            operator=operator,
            op_type=op_type,
        )
        try:
            if not self._operation_was_running:
                if expect_present:
                    await self._preflight_package(self.drivers_root / driver_id)
                elif (self.drivers_root / driver_id).exists():
                    raise DriverInstallError("回滚后激活目录仍存在", status_code=503)
            else:
                await restart_collector()
                alive = await self._wait_alive(collector_alive)
                if not alive:
                    raise DriverInstallError(
                        "回滚后 collector 未在超时内恢复",
                        status_code=503,
                    )
                if expect_present:
                    ok = await self._wait_for_driver_loaded(
                        driver_id=driver_id,
                        version=version,
                    )
                    if not ok:
                        raise DriverInstallError(
                            f"回滚后 collector 未加载驱动 {driver_id}@{version}",
                            status_code=503,
                        )
                else:
                    ok = await self._wait_for_driver_unloaded(driver_id)
                    if not ok:
                        raise DriverInstallError(
                            f"全新安装回滚后 collector 仍加载驱动 {driver_id}",
                            status_code=503,
                        )
        except DriverInstallError:
            # 保留 rollback_verifying，置位恢复门禁
            self._recovery_blocked = True
            self._recovery_block_reasons = [
                {
                    "op_id": operation_id,
                    "driver_id": driver_id,
                    "phase": _PHASE_ROLLBACK_VERIFYING,
                    "type": op_type,
                    "verified": False,
                    "error": "回滚后 collector 探针失败",
                }
            ]
            raise
        except Exception as probe_exc:
            self._recovery_blocked = True
            self._recovery_block_reasons = [
                {
                    "op_id": operation_id,
                    "driver_id": driver_id,
                    "phase": _PHASE_ROLLBACK_VERIFYING,
                    "type": op_type,
                    "verified": False,
                    "error": str(probe_exc),
                }
            ]
            raise DriverInstallError(
                f"回滚后 collector 验证失败: {probe_exc}",
                status_code=503,
            ) from probe_exc

        self._persist_operation(
            operation_id,
            _PHASE_ROLLED_BACK,
            driver_id,
            version,
            previous_record=previous_record,
            operator=operator,
            op_type=op_type,
        )

    def _rollback_activate(
        self,
        *,
        driver_id: str,
        active_dir: Path,
        backup_dir: Optional[Path],
        previous_record: Optional[dict[str, Any]],
        error: str,
    ) -> None:
        # A failed backup move must never delete the still-active previous package.
        if previous_record is not None and active_dir.exists() and (backup_dir is None or not backup_dir.exists()):
            from data_watch_driver_manifest import load_driver_toml
            manifest = load_driver_toml(active_dir / "driver.toml")
            from data_watch_driver_catalog import tree_digest
            if manifest.version == previous_record.get("version") and (not previous_record.get("tree_sha256") or tree_digest(active_dir) == previous_record["tree_sha256"]):
                self._upsert_driver_record(previous_record)
                return
        # 移走失败的新目录
        failed = self.trash_root / f"failed_{uuid.uuid4().hex}"
        if active_dir.exists():
            failed.parent.mkdir(parents=True, exist_ok=True)
            shutil.move(str(active_dir), str(failed))

        if backup_dir is not None and backup_dir.exists():
            shutil.move(str(backup_dir), str(active_dir))
            if previous_record is not None:
                restored = dict(previous_record)
                restored["status"] = "rollback"
                restored["error"] = error
                restored["updated_at"] = _utc_now()
                self._upsert_driver_record(restored)
            return

        # 无备份：标记 load_failed 并清除目录残留记录
        state = self._read_state()
        drivers = state.setdefault("drivers", {})
        if driver_id in drivers:
            drivers[driver_id]["status"] = "load_failed"
            drivers[driver_id]["error"] = error
            drivers[driver_id]["updated_at"] = _utc_now()
            self._write_state(state)
        else:
            # 全新安装失败：不保留伪成功记录
            drivers.pop(driver_id, None)
            self._write_state(state)

    def _persist_operation(
        self,
        op_id: str,
        phase: str,
        driver_id: str,
        version: str,
        *,
        previous_record: Optional[dict[str, Any]] = None,
        operator: str = "",
        op_type: str = "install",
    ) -> None:
        """追加写入 driver_operations.jsonl，并同步 state.operation。"""
        entry: dict[str, Any] = {
            "op_id": op_id,
            "phase": phase,
            "driver_id": driver_id,
            "version": version,
            "ts": _utc_now(),
            "type": op_type,
            "was_running": self._operation_was_running,
        }
        if previous_record is not None:
            entry["previous_record"] = previous_record
        if operator:
            entry["operator"] = operator

        self.drivers_root.mkdir(parents=True, exist_ok=True)
        with self.operation_log_path.open("a", encoding="utf-8") as fp:
            fp.write(json.dumps(entry, ensure_ascii=False) + "\n")
            fp.flush()
            os.fsync(fp.fileno())

        if phase in _TERMINAL_PHASES:
            self._set_operation(None)
            try:
                self._compact_operation_log()
            except Exception:
                logger.exception("压缩驱动事务日志失败，保留原日志继续运行")
        else:
            self._set_operation(
                {
                    "id": op_id,
                    "type": op_type,
                    "phase": phase,
                    "driver_id": driver_id,
                    "version": version,
                    "operator": operator,
                    "started_at": entry["ts"],
                    "previous_record": previous_record,
                    "was_running": self._operation_was_running,
                }
            )

    def _compact_operation_log(self) -> None:
        """保留全部未完成操作的最新状态和有限条终态审计。

        ✅ 改进：流式读取，避免超大文件一次性加载到内存
        """
        if not self.operation_log_path.exists():
            return

        incomplete: dict[str, tuple[int, dict[str, Any]]] = {}
        terminal: dict[str, tuple[int, dict[str, Any]]] = {}

        with open(self.operation_log_path, "r", encoding="utf-8") as f:
            for index, line in enumerate(f):
                try:
                    raw = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if not isinstance(raw, dict) or not raw.get("op_id"):
                    continue
                op_id = str(raw["op_id"])
                item = (index, raw)
                if str(raw.get("phase") or "") in _TERMINAL_PHASES:
                    incomplete.pop(op_id, None)
                    terminal.pop(op_id, None)
                    terminal[op_id] = item
                    while len(terminal) > _TERMINAL_AUDIT_LIMIT:
                        terminal.pop(next(iter(terminal)))
                else:
                    terminal.pop(op_id, None)
                    incomplete[op_id] = item

        retained = sorted(
            [*incomplete.values(), *terminal.values()], key=lambda item: item[0]
        )

        tmp = self.operation_log_path.with_suffix(".jsonl.tmp")
        try:
            with tmp.open("w", encoding="utf-8") as fp:
                for _index, entry in retained:
                    fp.write(json.dumps(entry, ensure_ascii=False) + "\n")
                fp.flush()
                os.fsync(fp.fileno())
            os.replace(tmp, self.operation_log_path)
        finally:
            if tmp.exists():
                tmp.unlink()

    def _prune_driver_backups(self, driver_id: str, *, keep_version: str) -> None:
        """提交后只保留当前版本的上一可用版本；提交前绝不调用。"""
        root = self.backup_root / driver_id
        if not root.is_dir():
            return
        for child in root.iterdir():
            if keep_version and child.name == keep_version:
                continue
            try:
                if child.is_dir() and not child.is_symlink():
                    shutil.rmtree(child)
                else:
                    child.unlink()
            except OSError:
                logger.exception("清理旧驱动备份失败: %s", child)

    def _latest_incomplete_operations(self) -> list[dict[str, Any]]:
        """按 op_id 取 jsonl 最后一条；若非终态则视为未完成。"""
        by_id: dict[str, dict[str, Any]] = {}
        if self.operation_log_path.exists():
            try:
                text = self.operation_log_path.read_text(encoding="utf-8")
            except OSError:
                text = ""
            for line in text.splitlines():
                line = line.strip()
                if not line:
                    continue
                try:
                    raw = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if not isinstance(raw, dict):
                    continue
                op_id = raw.get("op_id")
                if op_id:
                    by_id[str(op_id)] = raw

        # 兼容：仅有 state.operation、尚无 jsonl 终态时也纳入恢复
        state_op = self._read_state().get("operation")
        if isinstance(state_op, dict) and state_op.get("id"):
            op_id = str(state_op["id"])
            op_type = str(state_op.get("type") or "install")
            if op_id not in by_id and op_type in {"install", "uninstall"}:
                default_phase = (
                    _PHASE_ACTIVATING
                    if op_type == "install"
                    else _PHASE_UNINSTALL_PREPARED
                )
                by_id[op_id] = {
                    "op_id": op_id,
                    "phase": state_op.get("phase") or default_phase,
                    "driver_id": state_op.get("driver_id") or "",
                    "version": state_op.get("version") or "",
                    "previous_record": state_op.get("previous_record"),
                    "was_running": state_op.get("was_running", True),
                    "type": op_type,
                }

        incomplete: list[dict[str, Any]] = []
        for entry in by_id.values():
            phase = str(entry.get("phase") or "")
            op_type = str(entry.get("type") or "install")
            if phase not in _TERMINAL_PHASES and op_type in {"install", "uninstall"}:
                incomplete.append(entry)
        return incomplete

    def _backup_dir_for(self, driver_id: str, previous_record: Optional[dict[str, Any]]) -> Path:
        version = "unknown"
        if previous_record and previous_record.get("version"):
            version = str(previous_record["version"])
        return self.backup_root / driver_id / version

    def _plan_base(self, op: dict[str, Any], *, action: str, expect: str) -> dict[str, Any]:
        previous_record = op.get("previous_record")
        if previous_record is not None and not isinstance(previous_record, dict):
            previous_record = None
        version = str(op.get("version") or "")
        if expect == "present" and previous_record and previous_record.get("version"):
            # 回滚期望旧版本；提交期望新版本（见调用方覆盖）
            if action == "rollback":
                version = str(previous_record.get("version") or version)
        return {
            "op_id": str(op.get("op_id") or ""),
            "driver_id": str(op.get("driver_id") or ""),
            "version": version,
            "type": str(op.get("type") or "install"),
            "phase": str(op.get("phase") or ""),
            "action": action,
            "expect": expect,
            "previous_record": previous_record,
            "was_running": op.get("was_running", True),
        }

    def _apply_install_rollback_disk(self, op: dict[str, Any]) -> dict[str, Any]:
        """未验证安装：恢复 backup / 清半成品；不写终态。"""
        op_id = str(op.get("op_id") or "")
        driver_id = str(op.get("driver_id") or "")
        previous_record = op.get("previous_record")
        if previous_record is not None and not isinstance(previous_record, dict):
            previous_record = None

        active_dir = self.drivers_root / driver_id
        backup_dir = self._backup_dir_for(driver_id, previous_record)

        self._cleanup_staging(op_id)
        for tmp in self.staging_root.glob(f".{op_id}_*"):
            shutil.rmtree(tmp, ignore_errors=True)

        error = "启动恢复：未完成安装已回滚（待 collector 验证）"
        if backup_dir.exists():
            if active_dir.exists():
                failed = self.trash_root / f"recover_failed_{uuid.uuid4().hex}"
                failed.parent.mkdir(parents=True, exist_ok=True)
                shutil.move(str(active_dir), str(failed))
            shutil.move(str(backup_dir), str(active_dir))
            if previous_record is not None:
                restored = dict(previous_record)
                restored["status"] = "rollback"
                restored["error"] = error
                restored["updated_at"] = _utc_now()
                self._upsert_driver_record(restored)
            return self._plan_base(op, action="rollback", expect="present")
        if previous_record is not None:
            restored = dict(previous_record)
            restored["status"] = "rollback"
            restored["error"] = error
            restored["updated_at"] = _utc_now()
            self._upsert_driver_record(restored)
            return self._plan_base(op, action="rollback", expect="present")

        # 全新安装中途失败：清掉半成品，期望 registry 无此驱动
        phase = str(op.get("phase") or "")
        if active_dir.exists() and phase in _ROLLBACK_PHASES:
            failed = self.trash_root / f"recover_failed_{uuid.uuid4().hex}"
            failed.parent.mkdir(parents=True, exist_ok=True)
            shutil.move(str(active_dir), str(failed))
        state = self._read_state()
        state.get("drivers", {}).pop(driver_id, None)
        self._write_state(state)
        plan = self._plan_base(op, action="rollback", expect="absent")
        return plan

    def _apply_install_commit_disk(self, op: dict[str, Any]) -> dict[str, Any]:
        """已 verified：确认激活目录与 state；保留 backup 直至 collector 验证通过。"""
        op_id = str(op.get("op_id") or "")
        driver_id = str(op.get("driver_id") or "")
        version = str(op.get("version") or "")
        previous_record = op.get("previous_record")
        if previous_record is not None and not isinstance(previous_record, dict):
            previous_record = None

        active_dir = self.drivers_root / driver_id
        if not active_dir.is_dir():
            logger.warning(
                "verified 恢复时激活目录缺失，改为磁盘回滚 op_id=%s driver=%s",
                op_id,
                driver_id,
            )
            return self._apply_install_rollback_disk(op)

        state = self._read_state()
        drivers = state.setdefault("drivers", {})
        rec = drivers.get(driver_id)
        if isinstance(rec, dict):
            rec["version"] = version or rec.get("version")
            if rec.get("status") not in ("loaded", "installed"):
                rec["status"] = "installed"
            rec["error"] = None
            rec["updated_at"] = _utc_now()
            self._write_state(state)
        else:
            drivers[driver_id] = {
                "id": driver_id,
                "version": version,
                "status": "installed",
                "source": "installed",
                "error": None,
                "updated_at": _utc_now(),
            }
            self._write_state(state)

        plan = self._plan_base(op, action="commit", expect="present")
        plan["version"] = version
        plan["previous_record"] = previous_record
        return plan

    def _apply_uninstall_rollback_disk(self, op: dict[str, Any]) -> dict[str, Any]:
        """collector 未确认前：从 trash 还原目录与 state；不写终态。"""
        op_id = str(op.get("op_id") or "")
        driver_id = str(op.get("driver_id") or "")
        previous_record = op.get("previous_record")
        if previous_record is not None and not isinstance(previous_record, dict):
            previous_record = None

        active_dir = self.drivers_root / driver_id
        trash_dir = self.trash_root / op_id / driver_id

        if trash_dir.exists() and not active_dir.exists():
            active_dir.parent.mkdir(parents=True, exist_ok=True)
            shutil.move(str(trash_dir), str(active_dir))
        elif trash_dir.exists() and active_dir.exists():
            shutil.rmtree(trash_dir, ignore_errors=True)

        if previous_record is not None:
            restored = dict(previous_record)
            restored["updated_at"] = _utc_now()
            self._upsert_driver_record(restored)

        # trash 残留目录可清；事务终态仍待 collector 验证
        shutil.rmtree(self.trash_root / op_id, ignore_errors=True)
        return self._plan_base(op, action="rollback", expect="present")

    def _apply_uninstall_commit_disk(self, op: dict[str, Any]) -> dict[str, Any]:
        """collector_verified：确保 state 无驱动；trash 留到验证通过后再删。"""
        driver_id = str(op.get("driver_id") or "")
        state = self._read_state()
        state.get("drivers", {}).pop(driver_id, None)
        self._write_state(state)
        return self._plan_base(op, action="commit", expect="absent")

    def _finalize_recover_plan(self, plan: dict[str, Any]) -> None:
        """collector 验证通过后写终态并做最终清理。"""
        op_id = str(plan.get("op_id") or "")
        driver_id = str(plan.get("driver_id") or "")
        version = str(plan.get("version") or "")
        previous_record = plan.get("previous_record")
        if previous_record is not None and not isinstance(previous_record, dict):
            previous_record = None
        op_type = str(plan.get("type") or "install")
        action = str(plan.get("action") or "")

        if action == "commit" and op_type == "install":
            self._cleanup_staging(op_id)
            for tmp in self.staging_root.glob(f".{op_id}_*"):
                shutil.rmtree(tmp, ignore_errors=True)
            self._prune_driver_backups(
                driver_id,
                keep_version=str((previous_record or {}).get("version") or ""),
            )
            self._persist_operation(
                op_id,
                _PHASE_COMMITTED,
                driver_id,
                version,
                previous_record=previous_record,
                op_type="install",
            )
            return

        if action == "commit" and op_type == "uninstall":
            state = self._read_state()
            state["removed"] = sorted(set(state.get("removed", [])) | {driver_id})
            state.setdefault("removed_drivers", {})[driver_id] = previous_record or {"id": driver_id}
            self._write_state(state)
            shutil.rmtree(self.trash_root / op_id, ignore_errors=True)
            self._persist_operation(
                op_id,
                _PHASE_COMMITTED,
                driver_id,
                version,
                previous_record=previous_record,
                op_type="uninstall",
            )
            return

        # rollback
        self._persist_operation(
            op_id,
            _PHASE_ROLLED_BACK,
            driver_id,
            version,
            previous_record=previous_record,
            op_type=op_type if op_type in {"install", "uninstall"} else "install",
        )

    async def _verify_recover_plan(self, plan: dict[str, Any]) -> bool:
        expect = str(plan.get("expect") or "")
        driver_id = str(plan.get("driver_id") or "")
        version = str(plan.get("version") or "")
        if not plan.get("was_running", True):
            if expect == "absent":
                return not (self.drivers_root / driver_id).exists()
            if expect == "present":
                await self._preflight_package(self.drivers_root / driver_id)
                return True
            return False
        if expect == "absent":
            return await self._wait_for_driver_unloaded(driver_id)
        if expect == "present":
            return await self._wait_for_driver_loaded(
                driver_id=driver_id,
                version=version,
            )
        return False

    async def _wait_alive(self, collector_alive: CollectorAliveFn) -> bool:
        deadline = asyncio.get_running_loop().time() + self._health_wait_seconds
        while True:
            try:
                if await collector_alive():
                    return True
            except Exception:
                pass
            if asyncio.get_running_loop().time() >= deadline:
                return False
            await asyncio.sleep(self._health_poll_interval)

    async def _wait_for_driver_loaded(
        self,
        *,
        driver_id: str,
        version: str,
        status_url: Optional[str] = None,
    ) -> bool:
        """轮询 collector GET /drivers，确认目标驱动已注册（真实 loaded）。"""
        base = (status_url or _COLLECTOR_STATUS_URL).rstrip("/")
        url = f"{base}/drivers"
        deadline = asyncio.get_running_loop().time() + self._health_wait_seconds
        while True:
            try:
                payload = await asyncio.to_thread(self._fetch_json, url)
                drivers = payload.get("drivers") if self._valid_instance(payload, require_new=True) else None
                if isinstance(drivers, list):
                    for item in drivers:
                        if not isinstance(item, dict):
                            continue
                        if str(item.get("id") or "") != driver_id:
                            continue
                        if str(item.get("status") or "loaded") != "loaded":
                            continue
                        from data_watch_driver_catalog import tree_digest
                        active = self.drivers_root / driver_id
                        if not active.is_dir() or item.get("tree_sha256") != tree_digest(active):
                            continue
                        if not version or str(item.get("version") or "") == version:
                            return True
            except Exception as exc:
                logger.debug("等待 collector /drivers 失败: %s", exc)
            if asyncio.get_running_loop().time() >= deadline:
                return False
            await asyncio.sleep(self._health_poll_interval)

    async def _wait_for_driver_unloaded(
        self,
        driver_id: str,
        *,
        status_url: Optional[str] = None,
    ) -> bool:
        """轮询 collector GET /drivers，确认目标驱动已从 registry 消失。"""
        base = (status_url or _COLLECTOR_STATUS_URL).rstrip("/")
        url = f"{base}/drivers"
        deadline = asyncio.get_running_loop().time() + self._health_wait_seconds
        while True:
            try:
                payload = await asyncio.to_thread(self._fetch_json, url)
                drivers = payload.get("drivers") if self._valid_instance(payload, require_new=True) else None
                if isinstance(drivers, list):
                    still_there = any(
                        isinstance(item, dict)
                        and str(item.get("id") or "") == driver_id
                        for item in drivers
                    )
                    if not still_there:
                        return True
            except Exception as exc:
                logger.debug("等待 collector 卸载探针失败: %s", exc)
            if asyncio.get_running_loop().time() >= deadline:
                return False
            await asyncio.sleep(self._health_poll_interval)

    @staticmethod
    def _fetch_json(url: str) -> dict[str, Any]:
        import urllib.error
        import urllib.request

        req = urllib.request.Request(url, method="GET")
        with urllib.request.urlopen(req, timeout=2.0) as resp:
            body = resp.read().decode("utf-8")
        data = json.loads(body) if body else {}
        if not isinstance(data, dict):
            raise ValueError("collector /drivers 响应不是对象")
        return data

    def _build_record(
        self,
        validated: ValidatedDriverPackage,
        *,
        status: str,
        operator: str,
    ) -> dict[str, Any]:
        return {
            "node": self._node_identity(),
            "id": validated.driver_id,
            "protocol": validated.protocol,
            "name": validated.name,
            "description": validated.description,
            "version": validated.version,
            "sha256": validated.sha256,
            "entry": validated.entry,
            "capabilities": list(validated.capabilities),
            "readWriteType": validated.read_write,
            "pythonAbi": validated.python_abi,
            "source": "official" if validated.publisher == "datawatch" else "installed",
            "publisher": validated.publisher, "aliases": list(validated.aliases),
            "tree_sha256": validated.tree_sha256, "preflight_verified": True, "approved_source": True, "signature_verified": validated.signature_verified,
            "status": status,
            "install_status": "installed",
            "runtime_status": "loaded" if status == "loaded" else "collector_stopped",
            "error": None,
            "operator": operator,
            "updated_at": _utc_now(),
        }

    def _upsert_driver_record(self, record: dict[str, Any]) -> None:
        state = self._read_state()
        state.setdefault("drivers", {})[record["id"]] = record
        self._write_state(state)

    def _set_operation(self, operation: Optional[dict[str, Any]]) -> None:
        state = self._read_state()
        state["operation"] = operation
        self._write_state(state)

    def _cleanup_staging(self, operation_id: str) -> None:
        path = self.staging_root / operation_id
        if path.exists():
            shutil.rmtree(path, ignore_errors=True)

    def _read_state(self) -> dict[str, Any]:
        if not self.state_path.exists():
            return {"drivers": {}, "operation": None}
        try:
            with self.state_path.open("r", encoding="utf-8") as fp:
                data = json.load(fp)
            if not isinstance(data, dict) or not isinstance(data.get("drivers", {}), dict):
                raise InconsistentStateError("驱动安装记录格式损坏，已停止自动恢复")
            data.setdefault("drivers", {})
            data.setdefault("operation", None)
            return data
        except (OSError, json.JSONDecodeError) as exc:
            raise InconsistentStateError("无法读取驱动安装记录，已停止自动预装") from exc

    def _write_state(self, state: dict[str, Any]) -> None:
        self.drivers_root.mkdir(parents=True, exist_ok=True)
        tmp = self.state_path.with_name(f".{self.state_path.name}.tmp")
        payload = json.dumps(state, ensure_ascii=False, indent=2)
        with open(tmp, "w", encoding="utf-8") as fp:
            fp.write(payload)
            fp.flush()
            os.fsync(fp.fileno())
        os.replace(tmp, self.state_path)


def _utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
