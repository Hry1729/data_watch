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

from .outbox import OutboxCompletion, OutboxRecord, OutboxStore, SessionSwitchState
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
    maintenance_interval_seconds: float = 60.0


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
        self._summary_started_at = time.monotonic()
        self._summary_batches = 0
        self._summary_records = 0
        self._summary_payload_bytes = 0
        self._summary_batch_ms: list[float] = []
        self._store.recover_inflight(self._lease_owner)

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
                max_items=self._settings.max_batch_items,
                max_bytes=self._settings.max_batch_bytes,
            )
            if not records:
                return
            should_continue = await self._upload_records(records)
            if not should_continue:
                return

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
        if response.status_code == 400:
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
        for record in records:
            key = (record.batch_id, record.upload_session_id, record.local_seq)
            result = result_map.get(key)
            if result is None:
                logger.error(
                    "Upload acknowledgement missing batch_id=%s local_seq=%d records=%d",
                    record.batch_id,
                    record.local_seq,
                    len(records),
                )
                await self._retry_records(records, "missing_ack", 200)
                return False
            status = str(result.get("status") or "")
            if status in {"ack", "duplicate", "stale_config_warning"}:
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
            elif status in {"seq_reject", "commit_unknown"}:
                logger.error(
                    "Upload sequence/commit uncertain status=%s local_seq=%d; "
                    "preserving and replaying unconfirmed backlog in a new session",
                    status,
                    record.local_seq,
                )
                quarantined = await self._retry_records(
                    records,
                    status,
                    200,
                    quarantine_after_max=True,
                )
                if quarantined:
                    logger.error(
                        "Quarantined %d unresolvable upload records after max attempts; "
                        "payloads remain in local dead state for replay/reconciliation",
                        len(records),
                    )
                await self._abandon_and_switch()
                return False
            elif status == "session_obsolete":
                logger.warning(
                    "Upload session obsolete session=%s local_seq=%d; reconciling",
                    record.upload_session_id,
                    record.local_seq,
                )
                current_session_id = await self._store_call(
                    self._store.current_session_id
                )
                if record.upload_session_id != current_session_id:
                    return False
                switched = await self._reconcile_obsolete_session()
                if not switched:
                    await self._retry_records(records, "session_obsolete", 200)
                return False
            else:
                logger.error(
                    "Upload acknowledgement unknown status=%s local_seq=%d",
                    status or "<empty>",
                    record.local_seq,
                )
                await self._retry_records(records, f"unknown_ack:{status}", 200)
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
            if quarantine_after_max and (
                record.attempt_count + 1 >= self._settings.max_attempts
            ):
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
                    OutboxCompletion(record, "dead", error)
                    for record in quarantined
                ],
                self._lease_owner,
            )
        return bool(quarantined)

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
            if os.name == "nt":
                import msvcrt

                self._file.seek(0)
                self._file.write(b"0")
                self._file.flush()
                self._file.seek(0)
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
