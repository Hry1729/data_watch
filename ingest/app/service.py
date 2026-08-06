from __future__ import annotations

from typing import Protocol

from data_watch_shared.stream_envelope import validate_envelope

from .redis_store import CommitUnknownError, RedisProcessResult
from .repository import IngestRepository, ValidationDecision
from .schemas import AckResult, SessionSwitch, UploadBatch, UploadItem


class IngestStore(Protocol):
    async def process_items(
        self,
        node_uuid: str,
        entries: list[tuple[UploadItem, str]],
        *,
        nonce: str,
    ) -> list[RedisProcessResult]: ...
    async def switch_session(
        self, node_uuid: str, old_session_id: str, new_session_id: str
    ) -> str: ...
    async def get_active_session(self, node_uuid: str) -> str | None: ...


class IngestService:
    def __init__(
        self,
        repository: IngestRepository,
        store: IngestStore,
        *,
        max_batch_items: int,
    ) -> None:
        self._repository = repository
        self._store = store
        self._max_batch_items = max_batch_items

    async def ingest(
        self, batch: UploadBatch, *, nonce: str | None = None
    ) -> list[AckResult]:
        if len(batch.items) > self._max_batch_items:
            raise ValueError("batch exceeds max_batch_items")

        items = sorted(batch.items, key=lambda value: value.local_seq)
        decisions = await self._decisions(batch.node_uuid, items)
        try:
            redis_results = await self._store.process_items(
                batch.node_uuid,
                [(item, decision.action) for item, decision in zip(items, decisions)],
                nonce=nonce or batch.request_id,
            )
        except CommitUnknownError as exc:
            await self._repository.touch_node(batch.node_uuid)
            return [self._ack(items[0], "commit_unknown", str(exc))]

        results: list[AckResult] = []
        for item, decision, redis_result in zip(items, decisions, redis_results):
            status = self._resolve_status(redis_result.status, decision)
            results.append(self._ack(item, status, decision.reason))
            if status in {
                "seq_reject",
                "commit_unknown",
                "session_obsolete",
            }:
                break

        await self._repository.touch_node(batch.node_uuid)
        return results

    async def switch_session(self, request: SessionSwitch) -> str:
        result = await self._store.switch_session(
            request.node_uuid,
            request.old_session_id,
            request.new_session_id,
        )
        await self._repository.touch_node(request.node_uuid)
        return result

    async def get_active_session(self, node_uuid: str) -> str | None:
        return await self._store.get_active_session(node_uuid)

    async def _decisions(
        self,
        node_uuid: str,
        items: list[UploadItem],
    ) -> list[ValidationDecision]:
        decisions: list[ValidationDecision | None] = [None] * len(items)
        valid_items = []
        valid_indexes = []
        for index, item in enumerate(items):
            decision = self._envelope_decision(item)
            if decision is not None:
                decisions[index] = decision
            else:
                valid_items.append(item)
                valid_indexes.append(index)

        repository_decisions = await self._repository.validate_items(
            node_uuid, valid_items
        )
        for index, decision in zip(valid_indexes, repository_decisions):
            decisions[index] = decision
        return [
            decision or ValidationDecision.reject("malformed_envelope")
            for decision in decisions
        ]

    @staticmethod
    def _envelope_decision(item: UploadItem) -> ValidationDecision | None:
        if item.envelope_error:
            return ValidationDecision.reject(
                f"malformed_envelope:{item.envelope_error}"
            )
        try:
            validate_envelope(
                device_code=item.device_code,
                device_type=item.device_type,
                group_code=item.group_code,
                scan_rate_ms=item.scan_rate,
                ts=item.ts,
                config_version=item.config_version,
                values=item.values,
                contract_version=item.contract_version,
                qualities=item.qualities,
            )
        except (TypeError, ValueError) as exc:
            return ValidationDecision.reject(f"malformed_envelope:{exc}")
        return None
    @staticmethod
    def _resolve_status(
        redis_status: str,
        decision: ValidationDecision,
    ) -> str:
        if redis_status == "ack" and decision.status_override:
            return decision.status_override
        if redis_status == "rejected_consumed":
            return decision.status_override or "rejected_consumed"
        return redis_status

    @staticmethod
    def _ack(item: UploadItem, status: str, reason: str | None = None) -> AckResult:
        return AckResult(
            batch_id=item.batch_id,
            upload_session_id=item.upload_session_id,
            local_seq=item.local_seq,
            status=status,
            reason=reason,
        )
