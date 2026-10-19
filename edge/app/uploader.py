from __future__ import annotations

import asyncio
import base64
import hashlib
import json
import logging
import os
import time
from contextlib import AbstractContextManager
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from uuid import uuid4

import httpx
import msgspec
from cryptography.hazmat.primitives.serialization import load_pem_private_key

from ingest.app.security import canonical_json, signing_message

from .outbox import (
    DEAD_MAX_BYTES_DEFAULT,
    DEAD_RETENTION_DAYS_DEFAULT,
    OutboxCompletion,
    OutboxRecord,
    OutboxStore,
    SessionSwitchState,
)
from .storage_client import EdgeStorageClient, StorageUnavailable


logger = logging.getLogger(__name__)
UPLOAD_SUMMARY_INTERVAL_SECONDS = 10.0


class UploaderPaused(RuntimeError):
    pass


@dataclass(frozen=True, slots=True)
class UploaderSettings:
    center_url: str
    node_uuid: str
    private_key_path: Path
    lock_path: Path
    max_batch_items: int = 500
    max_batch_bytes: int = 1024 * 1024
    lease_ms: int = 30_000
    request_timeout_seconds: float = 10.0
    retry_base_ms: int = 500
    retry_max_ms: int = 30_000
    max_attempts: int = 10
    max_offline_retention_days: int = 7
    # dead 保留和离线 backlog 分开，默认 30 天 / 512MB。
    dead_retention_days: int = DEAD_RETENTION_DAYS_DEFAULT
    dead_max_bytes: int = DEAD_MAX_BYTES_DEFAULT
    maintenance_interval_seconds: float = 60.0
    sent_retention_ms: int = 24 * 60 * 60 * 1000


class RequestSigner:
    def __init__(self, private_key_pem: bytes) -> None:
        self._private_key = load_pem_private_key(private_key_pem, password=None)

    @classmethod
    def from_path(cls, path: Path) -> "RequestSigner":
        return cls(path.read_bytes())

    def headers(
        self,
        method: str,
        path: str,
        payload: dict[str, Any],
    ) -> dict[str, str]:
        timestamp = str(int(time.time()))
        nonce = str(uuid4())
        signature = self._private_key.sign(
            signing_message(
                method,
                path,
                timestamp,
                nonce,
                canonical_json(payload),
            )
        )
        return {
            "x-datawatch-timestamp": timestamp,
            "x-datawatch-nonce": nonce,
            "x-datawatch-signature": base64.b64encode(signature).decode("ascii"),
        }


def startup_phase_seconds(node_uuid: str, interval_ms: int) -> float:
    if interval_ms <= 0:
        raise ValueError("interval_ms must be positive")
    digest = hashlib.sha256(node_uuid.encode("utf-8")).digest()
    return (int.from_bytes(digest[:8], "big") % interval_ms) / 1000


class Uploader:
    def __init__(
        self,
        store: OutboxStore | EdgeStorageClient,
        settings: UploaderSettings,
        *,
        client: httpx.AsyncClient | None = None,
        signer: RequestSigner | None = None,
        sleep=asyncio.sleep,
    ) -> None:
        self._store = store
        self._settings = settings
        self._client = client or httpx.AsyncClient(
            base_url=settings.center_url.rstrip("/"),
            timeout=settings.request_timeout_seconds,
            limits=httpx.Limits(max_connections=2, max_keepalive_connections=1),
        )
        self._owns_client = client is None
        self._signer = signer or RequestSigner.from_path(settings.private_key_path)
        self._sleep = sleep
        self._lease_owner = str(uuid4())
        self._stopped = asyncio.Event()
        self._paused_records: list[OutboxRecord] | None = None
        self._next_maintenance_at = 0.0
        self._startup_phase_applied = False
        self._session_conflict = False
        self._summary_started_at = time.monotonic()
        self._summary_batches = 0
        self._summary_records = 0
        self._summary_payload_bytes = 0
        self._summary_batch_ms: list[float] = []
        self._reject_count_missing_warned = False

    async def recover_inflight(self) -> int:
        """恢复上一实例崩溃遗留的 inflight 租约。

        B25：该方法会改写共享 outbox 状态（经 Edge Storage），因此**只能**在
        持有单实例锁之后调用（见 `edge/uploader_main.py`）。构造器不做任何存储写入。
        """
        return await self._store_call(self._store.recover_inflight, self._lease_owner)

    async def close(self) -> None:
        if self._owns_client:
            await self._client.aclose()

    def stop(self) -> None:
        self._stopped.set()

    async def _store_call(self, method, *args, **kwargs):
        return await asyncio.to_thread(method, *args, **kwargs)

    async def run_forever(self) -> None:
        while not self._stopped.is_set():
            try:
                await self._maintain_if_due()
                interval_ms = await self._store_call(self._store.upload_interval_ms)
            except StorageUnavailable as exc:
                logger.warning("Edge Storage unavailable: %s", exc)
                await self._sleep(1.0)
                continue
            if interval_ms is None:
                await self._sleep(1.0)
                continue
            if not self._startup_phase_applied:
                await self._sleep(
                    startup_phase_seconds(self._settings.node_uuid, interval_ms)
                )
                self._startup_phase_applied = True
                if self._stopped.is_set():
                    break
            started = time.monotonic()
            try:
                await self.run_cycle()
            except httpx.HTTPError as exc:
                logger.warning("Session verification unavailable: %s", exc)
                await self._sleep(5.0)
            except UploaderPaused as exc:
                logger.warning("Uploader paused: %s; retrying in 5 seconds", exc)
                await self._sleep(5.0)
            except StorageUnavailable as exc:
                logger.warning("Edge Storage unavailable during upload cycle: %s", exc)
                await self._sleep(1.0)
            elapsed = time.monotonic() - started
            await self._sleep(max(0.0, interval_ms / 1000 - elapsed))

    async def _maintain_if_due(self) -> None:
        now = time.monotonic()
        if now < self._next_maintenance_at:
            return
        maintenance = await self._store_call(
            self._store.maintenance,
            self._settings.max_offline_retention_days,
            self._settings.sent_retention_ms,
            self._settings.dead_retention_days,
            self._settings.dead_max_bytes,
            node_uuid=self._settings.node_uuid,
        )
        if maintenance.get("degraded"):
            logger.error(
                "Edge outbox degraded reasons=%s backlog=%s; upload will retry without dropping data",
                ",".join(maintenance.get("degraded_reasons") or []),
                maintenance.get("backlog_count", "unknown"),
            )
        self._next_maintenance_at = now + self._settings.maintenance_interval_seconds

    async def run_cycle(self) -> None:
        pending_switch = await self._store_call(self._store.pending_session_switch)
        if self._session_conflict:
            current = await self._store_call(self._store.current_session_id)
            path = f"/ingest/session/{self._settings.node_uuid}"
            response = await self._client.get(path, headers=self._signer.headers("GET", path, {}))
            if response.status_code != 200:
                raise UploaderPaused("session conflict requires center verification")
            active = response.json().get("active_session_id")
            allowed = {None, current}
            if pending_switch is not None:
                allowed.add(pending_switch.old_session_id)
            if active not in allowed:
                raise UploaderPaused("another instance owns the node; no takeover attempted")
            self._session_conflict = False
        if pending_switch is not None:
            await self._complete_session_switch(pending_switch)
        if self._paused_records is not None:
            records = self._paused_records
            self._paused_records = None
            if not await self._upload_records(records):
                return
        target_seq = await self._store_call(self._store.snapshot_target_seq)
        while not self._stopped.is_set():
            records = await self._store_call(
                self._store.claim_batch,
                target_seq=target_seq,
                lease_owner=self._lease_owner,
                lease_ms=self._settings.lease_ms,
                max_items=min(self._settings.max_batch_items, getattr(self, "_contract_batch_limit", self._settings.max_batch_items)),
                max_bytes=self._settings.max_batch_bytes,
            )
            if not records:
                return
            should_continue = await self._upload_records(records)
            if not should_continue:
                return
            if records[-1].local_seq >= target_seq:
                self._contract_batch_limit = self._settings.max_batch_items

    async def _upload_records(self, records: list[OutboxRecord]) -> bool:
        batch_started = time.perf_counter()
        payload_bytes = sum(len(record.payload) for record in records)
        payload = self._upload_payload(records)
        try:
            response = await self._client.post(
                "/ingest",
                json=payload,
                headers=self._signer.headers("POST", "/ingest", payload),
            )
        except httpx.HTTPError as exc:
            logger.warning(
                "Upload request failed center=%s records=%d payload_bytes=%d error=%s",
                self._settings.center_url,
                len(records),
                payload_bytes,
                exc,
            )
            await self._retry_records(records, str(exc), None)
            return False

        if response.status_code in (401, 403):
            logger.error(
                "Upload authentication rejected status=%d records=%d response=%s",
                response.status_code,
                len(records),
                self._response_detail(response),
            )
            self._paused_records = records
            raise UploaderPaused("node authentication rejected")
        if response.status_code == 429 or response.status_code >= 500:
            logger.warning(
                "Upload deferred status=%d records=%d response=%s",
                response.status_code,
                len(records),
                self._response_detail(response),
            )
            await self._retry_records(records, response.text, response.status_code)
            return False
        if response.status_code == 413:
            # Edge 18：请求体超过中心/反代上限。批次减半重试，不消耗隔离预算。
            if len(records) > 1:
                self._contract_batch_limit = max(1, len(records) // 2)
                logger.warning(
                    "Upload payload too large status=413 records=%d payload_bytes=%d; "
                    "retrying with batch limit %d",
                    len(records),
                    payload_bytes,
                    self._contract_batch_limit,
                )
            else:
                logger.error(
                    "Upload payload too large status=413 for a single record batch_id=%s "
                    "local_seq=%d payload_bytes=%d; raise the center request body limit "
                    "(INGEST max_body_bytes / reverse proxy client_max_body_size). "
                    "Record kept for retry.",
                    records[0].batch_id,
                    records[0].local_seq,
                    payload_bytes,
                )
            await self._retry_records(records, "http_413_payload_too_large", 413)
            return False
        if response.status_code == 400 and self._error_scope(response) != "item":
            # Edge 6：批级 400（node_uuid 非法、批次超过中心上限、代理/旧中心未标明
            # scope 等）与具体记录无关，不得消耗逐条的隔离预算，也不判 dead。
            logger.error(
                "Upload rejected at batch level status=400 records=%d response=%s; "
                "check center/edge configuration, records kept for retry",
                len(records),
                self._response_detail(response),
            )
            if len(records) > 1:
                self._contract_batch_limit = max(1, len(records) // 2)
            await self._retry_records(records, "http_400_batch_rejected", 400)
            return False
        if response.status_code == 400:
            if len(records) > 1:
                self._contract_batch_limit = 1
                await self._retry_records(records, "http_400_batch_isolation", 400)
                return False
            logger.error(
                "Upload contract rejected status=400 records=%d response=%s; retrying batch without discarding backlog",
                len(records),
                self._response_detail(response),
            )
            quarantined = await self._retry_records(
                records,
                "http_400_contract_rejected",
                400,
                quarantine_after_max=True,
            )
            if quarantined:
                self._contract_batch_limit = self._settings.max_batch_items
                await self._abandon_and_switch()
            return False
        if response.status_code != 200:
            logger.warning(
                "Upload returned unexpected status=%d records=%d response=%s",
                response.status_code,
                len(records),
                self._response_detail(response),
            )
            await self._retry_records(records, response.text, response.status_code)
            return False

        try:
            results = response.json()["results"]
            result_map = {
                (
                    str(result["batch_id"]),
                    str(result["upload_session_id"]),
                    int(result["local_seq"]),
                ): result
                for result in results
            }
        except (KeyError, TypeError, ValueError, json.JSONDecodeError) as exc:
            logger.error(
                "Upload acknowledgement malformed records=%d error=%s response=%s",
                len(records),
                exc,
                self._response_detail(response),
            )
            await self._retry_records(records, f"malformed_ack:{exc}", 200)
            return False

        completions: list[OutboxCompletion] = []
        for index, record in enumerate(records):
            key = (record.batch_id, record.upload_session_id, record.local_seq)
            result = result_map.get(key)
            if result is None:
                logger.error(
                    "Upload acknowledgement missing batch_id=%s local_seq=%d records=%d",
                    record.batch_id,
                    record.local_seq,
                    len(records),
                )
                await self._persist_confirmed(completions)
                await self._retry_records(records[index:], "missing_ack", 200)
                return False
            status = str(result.get("status") or "")
            if status in {"ack", "duplicate", "stale_config_warning"}:
                if result.get("durability") != "redis-aof":
                    await self._persist_confirmed(completions)
                    await self._retry_records(records[index:], "center has not confirmed durable acceptance; upgrade center first", 200)
                    return False
                completions.append(OutboxCompletion(record, "sent"))
            elif status == "rejected_consumed":
                logger.error(
                    "Upload record permanently rejected batch_id=%s local_seq=%d reason=%s",
                    record.batch_id,
                    record.local_seq,
                    result.get("reason") or "rejected_consumed",
                )
                completions.append(
                    OutboxCompletion(
                        record,
                        "dead",
                        str(result.get("reason") or "rejected_consumed"),
                    )
                )
            elif status == "commit_unknown":
                # 中心持久化结果不确定（Redis 暂时故障）。同一 session 内退避重试：
                # 已提交的帧会被中心按 hw 判为 duplicate，不会重复写入；不消耗隔离预算，
                # 不切换 session。
                logger.error(
                    "Upload commit uncertain status=commit_unknown local_seq=%d reason=%s; "
                    "retrying in the same session with backoff",
                    record.local_seq,
                    result.get("reason") or "",
                )
                await self._persist_confirmed(completions)
                await self._retry_records(records[index:], "commit_unknown", 200)
                return False
            elif status == "seq_reject":
                logger.error(
                    "Upload sequence/commit uncertain status=%s local_seq=%d; "
                    "preserving and replaying unconfirmed backlog in a new session",
                    status,
                    record.local_seq,
                )
                # B5：先持久提交已明确确认的项，再做不确定项的迁移/隔离。
                await self._persist_confirmed(completions)
                unconfirmed = records[index:]
                quarantined = await self._retry_records(
                    unconfirmed[:1],
                    status,
                    200,
                    quarantine_after_max=True,
                )
                await self._retry_records(unconfirmed[1:], "batch_sequence_wait", 200)
                if quarantined:
                    logger.error(
                        "Quarantined %d unresolvable upload records after max attempts; "
                        "payloads remain in local dead state for replay/reconciliation",
                        1,
                    )
                await self._abandon_and_switch()
                return False
            elif status == "session_obsolete":
                logger.warning(
                    "Upload session obsolete session=%s local_seq=%d; reconciling",
                    record.upload_session_id,
                    record.local_seq,
                )
                await self._persist_confirmed(completions)
                current_session_id = await self._store_call(
                    self._store.current_session_id
                )
                if record.upload_session_id != current_session_id:
                    return False
                switched = await self._reconcile_obsolete_session()
                if not switched:
                    await self._retry_records(records[index:], "session_obsolete", 200)
                return False
            else:
                logger.error(
                    "Upload acknowledgement unknown status=%s local_seq=%d",
                    status or "<empty>",
                    record.local_seq,
                )
                await self._persist_confirmed(completions)
                await self._retry_records(records[index:], f"unknown_ack:{status}", 200)
                return False
        await self._store_call(
            self._store.complete_batch,
            completions,
            self._lease_owner,
        )
        self._record_success(
            records=len(records),
            payload_bytes=payload_bytes,
            batch_ms=(time.perf_counter() - batch_started) * 1000,
        )
        return True

    def _record_success(self, *, records: int, payload_bytes: int, batch_ms: float) -> None:
        self._summary_batches += 1
        self._summary_records += records
        self._summary_payload_bytes += payload_bytes
        self._summary_batch_ms.append(batch_ms)
        now = time.monotonic()
        elapsed = now - self._summary_started_at
        if elapsed < UPLOAD_SUMMARY_INTERVAL_SECONDS:
            return
        samples = sorted(self._summary_batch_ms)
        p99_index = max(0, min(len(samples) - 1, int(len(samples) * 0.99)))
        logger.info(
            "Upload summary batches=%d records=%d payload_mb=%.2f rate_records_s=%.1f batch_p99_ms=%.1f",
            self._summary_batches,
            self._summary_records,
            self._summary_payload_bytes / (1024 * 1024),
            self._summary_records / max(elapsed, 0.001),
            samples[p99_index],
        )
        self._summary_started_at = now
        self._summary_batches = 0
        self._summary_records = 0
        self._summary_payload_bytes = 0
        self._summary_batch_ms.clear()

    @staticmethod
    def _error_scope(response: httpx.Response) -> str | None:
        """中心 400 响应体里的 ``scope``（``batch``/``item``）；缺失或非 JSON 时为 None。"""
        try:
            body = response.json()
        except ValueError:
            return None
        return body.get("scope") if isinstance(body, dict) else None

    @staticmethod
    def _response_detail(response: httpx.Response) -> str:
        return response.text[:500].replace("\r", " ").replace("\n", " ")

    def _upload_payload(self, records: list[OutboxRecord]) -> dict[str, Any]:
        items = []
        for record in records:
            envelope = msgspec.json.decode(record.payload)
            if not isinstance(envelope, dict):
                raise ValueError("outbox payload must be an object")
            row = dict(envelope)
            row.update(
                {
                    "batch_id": record.batch_id,
                    "upload_session_id": record.upload_session_id,
                    "local_seq": record.local_seq,
                    "assignment_epoch": record.assignment_epoch,
                }
            )
            items.append(row)
        return {
            "node_uuid": self._settings.node_uuid,
            # Keep the application idempotency key stable while this exact
            # outbox batch is retried. The signature nonce remains per
            # transport attempt and is intentionally independent.
            "request_id": self._stable_request_id(records),
            "items": items,
        }

    def _stable_request_id(self, records: list[OutboxRecord]) -> str:
        material = "|".join(
            f"{record.batch_id}:{record.upload_session_id}:{record.local_seq}"
            for record in records
        )
        digest = hashlib.sha256(
            f"{self._settings.node_uuid}|{material}".encode("utf-8")
        ).hexdigest()
        return f"edge-{digest}"

    async def _persist_confirmed(
        self,
        completions: list[OutboxCompletion],
    ) -> None:
        """B5：先持久提交已明确确认（sent/dead）的项。

        只有落库之后才允许处理未确认/未知项，否则后续的 session 异常
        （``seq_reject``/``commit_unknown``/ACK 缺失/未知状态）会把本批已 ACK
        的帧一起重排进新会话并重传。未知结果一律保留原帧身份，
        由 ``_retry_records``/``_abandon_and_switch`` 按至少一次语义重放。
        """
        if not completions:
            return
        await self._store_call(
            self._store.complete_batch,
            completions,
            self._lease_owner,
        )

    async def _retry_records(
        self,
        records: list[OutboxRecord],
        error: str,
        http_status: int | None,
        *,
        quarantine_after_max: bool = False,
    ) -> bool:
        retries = []
        quarantined = []
        for record in records:
            if quarantine_after_max and self._quarantine_budget_exhausted(record):
                quarantined.append(record)
            else:
                retries.append(
                    (
                        record,
                        min(
                            self._settings.retry_base_ms * (2 ** record.attempt_count),
                            self._settings.retry_max_ms,
                        ),
                    )
                )
        if retries:
            await self._store_call(
                self._store.mark_retry_batch,
                retries,
                self._lease_owner,
                error=error,
                http_status=http_status,
            )
        if quarantined:
            await self._store_call(
                self._store.complete_batch,
                [
                    # B6：把「致命那次裁决」的协议状态码一起写入，避免 dead 行残留
                    # 上一次传输失败的 http_status（取证会指向错误原因）。
                    OutboxCompletion(record, "dead", error, http_status=http_status)
                    for record in quarantined
                ],
                self._lease_owner,
            )
        return bool(quarantined)

    def _quarantine_budget_exhausted(self, record: OutboxRecord) -> bool:
        """B6：坏数据隔离预算**只看** ``reject_count``（协议级裁决计数）。

        - ``reject_count`` 由 Edge Storage 的 ``mark_retry_batch`` 权威累加，且
          **只**由中心给出的协议级裁决（记录级 400 契约拒绝 / ``seq_reject``）消耗；
          ``commit_unknown``/批级 400/413/429/5xx/断网/ACK 缺失等传输类与中心侧问题只累加
          ``attempt_count``（退避用）。因此长时间断网的历史网络次数不会再让一次
          序列异常把好数据判死。
        - ``reject_count is None`` 表示对端 Edge Storage **没有回传**该字段（旧版
          存储进程，混合升级窗口）。此时**显式**降级为旧的 ``attempt_count`` 语义并
          告警一次 —— 不得静默当成 0：那会让真正非法的载荷永远不被隔离
          （见 docs/整改记录/B6.md「兼容策略」）。
        """
        reject_count = record.reject_count
        if reject_count is None:
            if not self._reject_count_missing_warned:
                self._reject_count_missing_warned = True
                logger.warning(
                    "Edge Storage did not report reject_count for claimed records; "
                    "falling back to attempt_count for quarantine decisions. "
                    "Upgrade edge-storage and the uploader together (legacy peer detected)."
                )
            reject_count = record.attempt_count
        return reject_count + 1 >= self._settings.max_attempts

    async def _abandon_and_switch(self) -> None:
        state = await self._store_call(self._store.prepare_session_switch)
        await self._complete_session_switch(state)

    async def _complete_session_switch(self, state: SessionSwitchState) -> None:
        payload = {
            "node_uuid": self._settings.node_uuid,
            "old_session_id": state.old_session_id,
            "new_session_id": state.new_session_id,
        }
        try:
            response = await self._client.post(
                "/ingest/session_switch",
                json=payload,
                headers=self._signer.headers(
                    "POST",
                    "/ingest/session_switch",
                    payload,
                ),
            )
        except httpx.HTTPError:
            return
        if response.status_code in (401, 403):
            raise UploaderPaused("node authentication rejected during session switch")
        if response.status_code != 200:
            return
        status = str(response.json().get("status") or "")
        if status in {"switched", "already_switched"}:
            await self._store_call(self._store.complete_session_switch, state)
        elif status == "session_conflict":
            self._session_conflict = True
            # 记录冲突错误并不盲目重发，避免永久阻塞上传循环
            logger.error(
                "Active session conflict for node=%s: local old=%s, new=%s. "
                "Will reconcile with center state.",
                self._settings.node_uuid,
                state.old_session_id,
                state.new_session_id,
            )
            raise UploaderPaused("active session conflict")

    async def _reconcile_obsolete_session(self) -> bool:
        current = await self._store_call(self._store.current_session_id)
        path = f"/ingest/session/{self._settings.node_uuid}"
        response = await self._client.get(
            path,
            headers=self._signer.headers("GET", path, {}),
        )
        if response.status_code in (401, 403):
            raise UploaderPaused("node authentication rejected during reconciliation")
        if response.status_code != 200:
            return False
        active = response.json().get("active_session_id")
        if active == current:
            return False
        pending = await self._store_call(self._store.pending_session_switch)
        if pending is not None and active in {pending.old_session_id, pending.new_session_id, None}:
            await self._complete_session_switch(pending)
            return (await self._store_call(self._store.pending_session_switch)) is None
        if active is not None:
            self._session_conflict = True
            raise UploaderPaused("another session owns this node; verify the other instance and re-register this node")
        state = await self._store_call(self._store.prepare_session_switch)
        await self._complete_session_switch(state)
        return (await self._store_call(self._store.pending_session_switch)) is None



class SingleInstanceLock(AbstractContextManager):
    def __init__(self, path: Path) -> None:
        self._path = path
        self._file = None

    def __enter__(self):
        self._path.parent.mkdir(parents=True, exist_ok=True)
        self._file = self._path.open("a+b")
        try:
            # 与 collector 的 ``_acquire_owner`` 同一做法：文件已有内容时**不再**写入。
            # 文件以 "a+b" 打开，write 总是落在末尾，而无条件写一字节发生在加锁
            # （可能失败）之前，会让锁文件每次争锁失败都 +1 字节。
            self._file.seek(0, 2)
            if self._file.tell() == 0:
                self._file.write(b"0")
                self._file.flush()
            self._file.seek(0)
            if os.name == "nt":
                import msvcrt

                msvcrt.locking(self._file.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl

                fcntl.flock(self._file.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError as exc:
            self._file.close()
            self._file = None
            raise RuntimeError("another uploader instance is running") from exc
        return self

    def __exit__(self, exc_type, exc, tb) -> bool:
        if self._file is None:
            return False
        try:
            if os.name == "nt":
                import msvcrt

                self._file.seek(0)
                msvcrt.locking(self._file.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                import fcntl

                fcntl.flock(self._file.fileno(), fcntl.LOCK_UN)
        finally:
            self._file.close()
            self._file = None
        return False
