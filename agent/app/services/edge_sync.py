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
from typing import Any, Callable
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

    def stop(self) -> None:
        self._stopped.set()

    async def run_forever(self) -> None:
        while not self._stopped.is_set():
            try:
                await self.run_once()
            except Exception as exc:
                logger.warning("Edge center sync failed: %s", exc)
                self._record_sync(status=None, error=str(exc))
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
        storage = (
            await asyncio.to_thread(self._config_store.status)
            if hasattr(self._config_store, "status")
            else {}
        )
        applied = await asyncio.to_thread(self._config_store.revision)
        heartbeat = await client.heartbeat(
            {
                "appliedRevision": applied,
                "outboxBacklog": outbox["backlog_count"],
                "outboxDatabaseBytes": outbox["database_bytes"],
                "outboxDiskUsedPercent": outbox["disk_used_percent"],
                "outboxWatermark": outbox["watermark"],
                "outboxDroppedRecords": outbox["dropped_records"],
                "collectorStatus": self._process_status("collector"),
                "storageStatus": self._process_status("storage"),
                "uploaderStatus": self._process_status("uploader"),
                "storageQueueDepth": int(storage.get("writer_queue_depth") or 0),
                "storageCommitP99Ms": float(storage.get("commit_p99_ms") or 0),
                "storageErrorsTotal": int(storage.get("errors_total") or 0),
                "edgeTimeMs": int(time.time() * 1000),
                "lastError": str(edge.get("last_sync_error") or ""),
            }
        )
        center_time_ms = int(heartbeat.get("centerTimeMs") or int(time.time() * 1000))
        await asyncio.to_thread(self._config_store.refresh_schedule, center_time_ms)
        desired = int(heartbeat.get("desiredRevision") or 0)
        if desired != applied:
            snapshot = await client.config(applied)
            if snapshot.get("changed"):
                applied = await asyncio.to_thread(self._config_store.apply, snapshot)
        self._record_sync(status="approved", error="", applied_revision=applied)
        return {"status": "approved", "appliedRevision": applied, "desiredRevision": desired}

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
