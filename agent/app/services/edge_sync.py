from __future__ import annotations

import asyncio
import base64
import hashlib
import json
import logging
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Awaitable, Callable
from uuid import uuid4

from cryptography.hazmat.primitives.asymmetric.ed25519 import (
    Ed25519PrivateKey,
    Ed25519PublicKey,
)
from cryptography.hazmat.primitives.serialization import (
    Encoding,
    NoEncryption,
    PrivateFormat,
    PublicFormat,
    load_pem_private_key,
)

from app.services.edge_config_store import EdgeConfigStore
from app.services.edge_local_store import EdgeLocalStore


logger = logging.getLogger(__name__)
Transport = Callable[[str, str, bytes | None, dict[str, str]], tuple[int, Any]]
# B016：手动轮换与后台 pending 恢复共享同一把锁，轮换操作串行化。
identity_rotation_lock = asyncio.Lock()


class CenterRequestError(RuntimeError):
    def __init__(self, status_code: int, message: str, code: int | None = None) -> None:
        self.status_code = status_code
        self.code = code
        super().__init__(message)


@dataclass(frozen=True, slots=True)
class EdgeIdentity:
    node_uuid: str
    private_key_pem: str
    public_key_pem: str


def generate_identity() -> EdgeIdentity:
    private_key = Ed25519PrivateKey.generate()
    return EdgeIdentity(
        node_uuid=str(uuid4()),
        private_key_pem=private_key.private_bytes(
            Encoding.PEM,
            PrivateFormat.PKCS8,
            NoEncryption(),
        ).decode("ascii"),
        public_key_pem=private_key.public_key().public_bytes(
            Encoding.PEM,
            PublicFormat.SubjectPublicKeyInfo,
        ).decode("ascii"),
    )


def canonical_json(payload: dict[str, Any]) -> bytes:
    return json.dumps(
        payload,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def signing_message(
    method: str,
    path: str,
    timestamp: str,
    nonce: str,
    payload: dict[str, Any],
) -> bytes:
    digest = hashlib.sha256(canonical_json(payload)).hexdigest()
    return f"{method.upper()}\n{path}\n{timestamp}\n{nonce}\n{digest}".encode("utf-8")


class CenterClient:
    def __init__(
        self,
        center_url: str,
        identity: EdgeIdentity,
        *,
        transport: Transport | None = None,
        timeout_seconds: float = 10.0,
    ) -> None:
        self.center_url = center_url.rstrip("/")
        self.identity = identity
        key = load_pem_private_key(identity.private_key_pem.encode("ascii"), password=None)
        if not isinstance(key, Ed25519PrivateKey):
            raise ValueError("edge private key must use Ed25519")
        self._private_key = key
        self._transport = transport or self._urlopen
        self._timeout_seconds = timeout_seconds

    async def register(self, registration_code: str, node_name: str) -> dict[str, Any]:
        return await self._request(
            "POST",
            "/api/distributed/edge/register",
            {
                "registrationCode": registration_code,
                "nodeUuid": self.identity.node_uuid,
                "nodeName": node_name,
                "publicKeyPem": self.identity.public_key_pem,
            },
            signed=False,
        )

    async def heartbeat(self, payload: dict[str, Any]) -> dict[str, Any]:
        path = f"/api/distributed/edge/{self.identity.node_uuid}/heartbeat"
        return await self._request("POST", path, payload, signed=True)

    async def rotate_key(self, new_public_key_pem: str) -> dict[str, Any]:
        path = f"/api/distributed/edge/{self.identity.node_uuid}/rotate-key"
        return await self._request(
            "POST",
            path,
            {"newPublicKeyPem": new_public_key_pem},
            signed=True,
        )

    async def config(self, revision: int) -> dict[str, Any]:
        path = f"/api/distributed/edge/{self.identity.node_uuid}/config"
        query_path = f"{path}?{urllib.parse.urlencode({'revision': revision})}"
        return await self._request("GET", query_path, {}, signed=True, signing_path=path)

    async def _request(
        self,
        method: str,
        path: str,
        payload: dict[str, Any],
        *,
        signed: bool,
        signing_path: str | None = None,
    ) -> dict[str, Any]:
        body = canonical_json(payload) if method != "GET" else None
        headers = {"Content-Type": "application/json", "Accept": "application/json"}
        if signed:
            headers.update(self._signature_headers(method, signing_path or path, payload))
        status, response = await asyncio.to_thread(
            self._transport,
            method,
            f"{self.center_url}{path}",
            body,
            headers,
        )
        if not isinstance(response, dict):
            raise CenterRequestError(status, "中心响应不是 JSON 对象")
        code = response.get("code")
        if status < 200 or status >= 300 or (code is not None and int(code) != 200):
            raise CenterRequestError(
                status,
                str(response.get("msg") or response.get("detail") or "中心请求失败"),
                int(code) if code is not None else None,
            )
        data = response.get("data", response)
        if not isinstance(data, dict):
            raise CenterRequestError(status, "中心响应 data 不是对象")
        return data

    def _signature_headers(
        self,
        method: str,
        path: str,
        payload: dict[str, Any],
    ) -> dict[str, str]:
        timestamp = str(int(time.time()))
        nonce = str(uuid4())
        signature = self._private_key.sign(
            signing_message(method, path, timestamp, nonce, payload)
        )
        return {
            "x-datawatch-timestamp": timestamp,
            "x-datawatch-nonce": nonce,
            "x-datawatch-signature": base64.b64encode(signature).decode("ascii"),
        }

    def _urlopen(
        self,
        method: str,
        url: str,
        body: bytes | None,
        headers: dict[str, str],
    ) -> tuple[int, Any]:
        request = urllib.request.Request(url, data=body, headers=headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=self._timeout_seconds) as response:
                return int(response.status), _decode_response(response.read())
        except urllib.error.HTTPError as exc:
            return int(exc.code), _decode_response(exc.read())


async def recover_pending_rotation(
    center_url: str,
    node_uuid: str,
    pending_private_key_pem: str,
    *,
    transport: Transport | None = None,
) -> dict[str, Any] | None:
    """B016：旧 key 被中心拒绝（轮换已提交但响应丢失且 grace 已过）时，用 pending 新 key
    签名重放同一轮换。中心已提交 → 新 key 即 current，返回 already_rotated；pending key
    同样签名无效（401）→ 中心未提交，返回 None。不生成第三把 key。
    """
    pending = EdgeIdentity(
        node_uuid=node_uuid,
        private_key_pem=pending_private_key_pem,
        public_key_pem=identity_public_key(pending_private_key_pem),
    )
    try:
        return await CenterClient(center_url, pending, transport=transport).rotate_key(
            pending.public_key_pem
        )
    except CenterRequestError as exc:
        if exc.status_code == 401:
            return None
        raise


# 审查 R2：pending key 已提升为 current、但 Uploader 尚未以新 key 重启时的持久化标记。
IDENTITY_PROPAGATION_PENDING_KEY = "identity_propagation_pending"


class EdgeSyncService:
    def __init__(
        self,
        database_path: str | Path,
        *,
        load_config: Callable[[], dict],
        mutate_config: Callable[[Callable[[dict], None]], dict],
        decrypt: Callable[[str], str],
        process_status: Callable[[str], str],
        transport: Transport | None = None,
        interval_seconds: float = 10.0,
        config_store=None,
        on_config_applied: Callable[[], Awaitable[None]] | None = None,
        on_identity_rotated: Callable[[], Awaitable[None]] | None = None,
    ) -> None:
        self._config_store = config_store or EdgeConfigStore(database_path)
        self._local_store = EdgeLocalStore(database_path)
        self._load_config = load_config
        self._mutate_config = mutate_config
        self._decrypt = decrypt
        self._process_status = process_status
        self._transport = transport
        self._interval_seconds = interval_seconds
        self._stopped = asyncio.Event()
        # B015：配置快照变更后需联动 OPC DA Sidecar；失败保留待办，下一轮重试。
        self._on_config_applied = on_config_applied
        self._config_reconcile_pending = False
        self._on_identity_rotated = on_identity_rotated

    def stop(self) -> None:
        self._stopped.set()

    async def run_forever(self) -> None:
        last_error: str | None = None
        while not self._stopped.is_set():
            try:
                await self.run_once()
                last_error = None
            except Exception as exc:
                # 同一条错误只报一次：中心未注册/拒绝连接属于运维条件，可能持续数天，
                # 每 10 秒一条曾把 agent-service.err.log 撑到 22MB。
                if str(exc) != last_error:
                    logger.warning("Edge center sync failed: %s", exc)
                    last_error = str(exc)
                try:
                    self._record_sync(status=None, error=str(exc))
                except Exception:
                    # 记录同步状态失败不得杀死同步循环。_record_sync 走 config 保存路径，
                    # 冻结包里缺 win32timezone 这类问题会让它抛错；此前异常逃逸出循环，
                    # 协程静默死亡 = 节点永久停止同步，且只在关机时才以
                    # "Application shutdown failed" 现形。
                    logger.exception("Failed to record edge sync state")
            try:
                await asyncio.wait_for(
                    self._stopped.wait(),
                    timeout=self._interval_seconds,
                )
            except asyncio.TimeoutError:
                pass

    async def run_once(self) -> dict[str, Any]:
        cfg = self._load_config()
        edge = cfg.get("edge") or {}
        identity = _identity_from_config(edge, self._decrypt)
        if not edge.get("registered") or identity is None:
            return {"status": "not_registered"}

        client = CenterClient(
            str(edge.get("center_url") or ""),
            identity,
            transport=self._transport,
        )
        outbox = self._local_store.outbox_status()
        # D9：Storage 重启/迁移/繁忙时 status/revision 会失败；此时仍发降级心跳
        # （storageStatus=unavailable，appliedRevision 取 config.toml 记录），不让节点在中心"离线"。
        storage_error = ""
        try:
            storage = (
                await asyncio.to_thread(self._config_store.status)
                if hasattr(self._config_store, "status")
                else {}
            )
            applied = await asyncio.to_thread(self._config_store.revision)
        except Exception as exc:
            logger.warning("Edge Storage status unavailable, sending degraded heartbeat: %s", exc)
            storage_error = str(exc) or type(exc).__name__
            storage = {}
            applied = int(edge.get("applied_revision") or 0)
        heartbeat_payload = (
            {
                "appliedRevision": applied,
                "outboxBacklog": outbox["backlog_count"],
                "outboxDatabaseBytes": outbox["database_bytes"],
                "outboxDiskUsedPercent": outbox["disk_used_percent"],
                "outboxWatermark": outbox["watermark"],
                "outboxDroppedRecords": outbox["dropped_records"],
                "collectorStatus": self._process_status("collector"),
                "storageStatus": "unavailable" if storage_error else self._process_status("storage"),
                "uploaderStatus": self._process_status("uploader"),
                "storageQueueDepth": int(storage.get("writer_queue_depth") or 0),
                "storageCommitP99Ms": float(storage.get("commit_p99_ms") or 0),
                "storageErrorsTotal": int(storage.get("errors_total") or 0),
                "edgeTimeMs": int(time.time() * 1000),
                "lastError": str(edge.get("last_sync_error") or ""),
            }
        )
        try:
            heartbeat = await client.heartbeat(heartbeat_payload)
        except CenterRequestError as exc:
            # B016：旧 key 已失效但持有 pending 新 key 时，尝试有界恢复后用新身份重试一次。
            if exc.status_code != 401 or not await self._recover_pending_identity(edge):
                raise
            edge = self._load_config().get("edge") or {}
            identity = _identity_from_config(edge, self._decrypt)
            if identity is None:
                raise
            client = CenterClient(
                str(edge.get("center_url") or ""),
                identity,
                transport=self._transport,
            )
            heartbeat = await client.heartbeat(heartbeat_payload)
        desired = int(heartbeat.get("desiredRevision") or 0)
        identity_error = await self._propagate_rotated_identity()
        if storage_error:
            # Storage 不可用时不做排程刷新/配置下发，下一轮 Storage 恢复后再收敛。
            error = f"{storage_error}; {identity_error}" if identity_error else storage_error
            self._record_sync(status="approved", error=error, applied_revision=applied)
            return {"status": "approved", "appliedRevision": applied, "desiredRevision": desired}
        center_time_ms = int(heartbeat.get("centerTimeMs") or int(time.time() * 1000))
        await asyncio.to_thread(self._config_store.refresh_schedule, center_time_ms)
        if desired != applied:
            snapshot = await client.config(applied)
            if snapshot.get("changed"):
                applied = await asyncio.to_thread(self._config_store.apply, snapshot)
                self._config_reconcile_pending = True
        sync_error = identity_error
        if self._config_reconcile_pending and self._on_config_applied is not None:
            try:
                await self._on_config_applied()
                self._config_reconcile_pending = False
            except Exception as exc:
                # 配置已落库但运行依赖（OPC DA 代理/映射）未就绪：不得报告完全收敛。
                logger.warning("Edge config applied but sidecar reconcile failed: %s", exc)
                reconcile_error = f"sidecar reconcile failed: {exc}"
                sync_error = f"{sync_error}; {reconcile_error}" if sync_error else reconcile_error
        self._record_sync(status="approved", error=sync_error, applied_revision=applied)
        return {"status": "approved", "appliedRevision": applied, "desiredRevision": desired}

    async def _recover_pending_identity(self, edge: dict[str, Any]) -> bool:
        """B016：用 pending key 验证中心是否已提交轮换，确认后原子提升为 current。"""
        pending_enc = str(edge.get("pending_private_key_enc") or "")
        pending_pem = self._decrypt(pending_enc) if pending_enc else ""
        if not pending_pem:
            return False
        async with identity_rotation_lock:
            latest = self._load_config().get("edge") or {}
            if str(latest.get("pending_private_key_enc") or "") != pending_enc:
                # 并发的手动轮换已处理该 pending，按最新配置重试即可。
                return True
            result = await recover_pending_rotation(
                str(edge.get("center_url") or ""),
                str(edge.get("node_uuid") or ""),
                pending_pem,
                transport=self._transport,
            )
            if result is None:
                return False

            def promote(cfg: dict) -> None:
                target = cfg.setdefault("edge", {})
                if str(target.get("pending_private_key_enc") or "") != pending_enc:
                    return
                target["private_key_enc"] = target.pop("pending_private_key_enc")
                target["key_version"] = int(result.get("keyVersion") or 1)
                # 审查 R2：提升与“Uploader 待更新”标记在同一次原子写入，回调失败后
                # 后续心跳（已用新 key 成功，不会再走恢复分支）仍能凭此标记重试。
                if self._on_identity_rotated is not None:
                    target[IDENTITY_PROPAGATION_PENDING_KEY] = True

            self._mutate_config(promote)
        logger.warning("Recovered edge identity with pending key after lost rotation response")
        # Uploader 更新由 run_once 在心跳成功后统一执行（含后续轮次的重试）。
        return True

    async def _propagate_rotated_identity(self) -> str:
        """把已提升的新身份下发给依赖模块（重启 Uploader）；失败保留标记，下一轮重试。

        返回错误描述（成功或无待办时为空串）。
        """
        if self._on_identity_rotated is None:
            return ""
        edge = self._load_config().get("edge") or {}
        if not edge.get(IDENTITY_PROPAGATION_PENDING_KEY):
            return ""
        try:
            await self._on_identity_rotated()
        except Exception as exc:
            logger.exception("Failed to apply rotated edge identity to dependent modules")
            return f"uploader identity update failed: {exc}"

        def clear(cfg: dict) -> None:
            cfg.setdefault("edge", {}).pop(IDENTITY_PROPAGATION_PENDING_KEY, None)

        self._mutate_config(clear)
        return ""

    def _record_sync(
        self,
        *,
        status: str | None,
        error: str,
        applied_revision: int | None = None,
    ) -> None:
        now = int(time.time() * 1000)

        def apply(cfg: dict) -> None:
            edge = cfg.setdefault("edge", {})
            if status is not None:
                edge["registration_status"] = status
            edge["last_sync_error"] = error[:1000]
            edge["last_sync_at"] = now
            if applied_revision is not None:
                edge["applied_revision"] = applied_revision

        self._mutate_config(apply)


def identity_public_key(private_key_pem: str) -> str:
    key = load_pem_private_key(private_key_pem.encode("ascii"), password=None)
    if not isinstance(key, Ed25519PrivateKey):
        raise ValueError("edge private key must use Ed25519")
    return key.public_key().public_bytes(
        Encoding.PEM,
        PublicFormat.SubjectPublicKeyInfo,
    ).decode("ascii")


def _identity_from_config(edge: dict[str, Any], decrypt: Callable[[str], str]) -> EdgeIdentity | None:
    node_uuid = str(edge.get("node_uuid") or "").strip()
    private_key_pem = decrypt(str(edge.get("private_key_enc") or ""))
    if not node_uuid or not private_key_pem:
        return None
    return EdgeIdentity(node_uuid, private_key_pem, identity_public_key(private_key_pem))


def _decode_response(raw: bytes) -> Any:
    if not raw:
        return {}
    try:
        return json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return {"msg": raw.decode("utf-8", errors="replace")}
