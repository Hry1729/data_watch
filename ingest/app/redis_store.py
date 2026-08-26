from __future__ import annotations

import asyncio
from dataclasses import dataclass
from typing import Callable

import msgspec
import redis.asyncio as redis
from redis.exceptions import RedisError

from data_watch_shared.stream_envelope import build_latest_updates, stream_key

from .config import IngestSettings
from .schemas import UploadItem


CONTROL_INVALIDATION_CHANNEL = "ingest:control:invalidate"
STREAM_TRIM_EVERY_ITEMS = 5_000


INGEST_BATCH_LUA = r"""
local nonce_reserved = redis.call(
    "SET", KEYS[#KEYS - 1], "1", "NX", "EX", tonumber(ARGV[5])
)
if nonce_reserved == false then
    return cjson.encode({"nonce_replay"})
end

local active_session = redis.call("GET", KEYS[2])
if active_session == false then
    redis.call("SET", KEYS[2], ARGV[1])
elseif active_session ~= ARGV[1] then
    return cjson.encode({"session_obsolete"})
end

local hw = tonumber(redis.call("GET", KEYS[1]) or "0")
local stream_maxlen = tonumber(ARGV[2])
local items = cjson.decode(ARGV[3])
local trim_every_items = tonumber(ARGV[4])
local results = {}
local changes = {}
local touched_streams = {}
local pending_latest = {}
local pending_latest_keys = {}

local function is_newer(incoming, current)
    local current_epoch = tonumber(current.e or 0)
    return incoming.t > current.t or (
        incoming.t == current.t and incoming.e > current_epoch
    )
end

local function stage_latest(latest_key, updates)
    local staged = pending_latest[latest_key]
    if staged == nil then
        staged = {}
        pending_latest[latest_key] = staged
        table.insert(pending_latest_keys, latest_key)
    end
    for var_id, incoming in pairs(updates) do
        local previous = staged[var_id]
        if previous == nil or is_newer(incoming, previous) then
            staged[var_id] = incoming
        end
    end
end

for index, item in ipairs(items) do
    local local_seq = tonumber(item.local_seq)
    if local_seq <= hw then
        table.insert(results, "duplicate")
    elseif local_seq > hw + 1 then
        table.insert(results, "seq_reject")
        break
    elseif item.action == "consume_reject" then
        redis.call("SET", KEYS[1], local_seq)
        hw = local_seq
        table.insert(results, "rejected_consumed")
    else
        local stream_key_index = 2 * index + 2
        local latest_key_index = stream_key_index + 1
        local envelope_data = item.envelope
        local xadd_args = {
            KEYS[stream_key_index], "*",
            "device_code", envelope_data.device_code,
            "device_type", envelope_data.device_type,
            "group_code", envelope_data.group_code,
            "scan_rate", tostring(envelope_data.scan_rate),
            "ts", tostring(envelope_data.ts),
            "config_version", tostring(envelope_data.config_version),
            "values", envelope_data.values_json
        }
        if envelope_data.contract_version == 2 then
            table.insert(xadd_args, "contract_version")
            table.insert(xadd_args, "2")
            table.insert(xadd_args, "qualities")
            table.insert(xadd_args, envelope_data.qualities_json)
        end

        local reply = redis.pcall("XADD", unpack(xadd_args))
        if type(reply) == "table" and reply.err then
            table.insert(results, "commit_unknown")
            break
        end

        stage_latest(KEYS[latest_key_index], item.latest_updates)

        redis.call("SET", KEYS[1], local_seq)
        hw = local_seq
        local touched_stream = KEYS[stream_key_index]
        touched_streams[touched_stream] = (touched_streams[touched_stream] or 0) + 1
        table.insert(changes, item.change)
        table.insert(results, "ack")
    end
end

local latest_chunk_size = 128
for _, latest_key in ipairs(pending_latest_keys) do
    local updates = pending_latest[latest_key]
    local var_ids = {}
    for var_id, _ in pairs(updates) do
        table.insert(var_ids, var_id)
    end
    table.sort(var_ids)

    local first = 1
    while first <= #var_ids do
        local last = math.min(first + latest_chunk_size - 1, #var_ids)
        local get_args = {latest_key}
        for index = first, last do
            table.insert(get_args, var_ids[index])
        end
        local current_values = redis.call("HMGET", unpack(get_args))
        local set_args = {latest_key}
        for index = first, last do
            local var_id = var_ids[index]
            local incoming = updates[var_id]
            local current_json = current_values[index - first + 1]
            local should_write = true
            if current_json then
                should_write = is_newer(incoming, cjson.decode(current_json))
            end
            if should_write then
                table.insert(set_args, var_id)
                table.insert(set_args, cjson.encode(incoming))
            end
        end
        if #set_args > 1 then
            redis.call("HSET", unpack(set_args))
        end
        first = last + 1
    end
end

if #changes > 0 then
    for touched_stream, item_count in pairs(touched_streams) do
        local trim_count = redis.call(
            "HINCRBY", KEYS[#KEYS], touched_stream, item_count
        )
        if trim_count >= trim_every_items then
            redis.call("XTRIM", touched_stream, "MAXLEN", "~", stream_maxlen)
            local consumed = math.floor(trim_count / trim_every_items) * trim_every_items
            redis.call("HINCRBY", KEYS[#KEYS], touched_stream, -consumed)
        end
    end
    redis.call("PUBLISH", KEYS[3], cjson.encode(changes))
end

return cjson.encode(results)
"""

SESSION_SWITCH_LUA = r"""
local current = redis.call("GET", KEYS[1])
if current == false then
    redis.call("SET", KEYS[1], ARGV[2])
    redis.call("SETNX", KEYS[2], "0")
    return "switched"
elseif current == ARGV[2] then
    redis.call("SETNX", KEYS[2], "0")
    return "already_switched"
elseif current == ARGV[1] then
    redis.call("SET", KEYS[1], ARGV[2])
    redis.call("SETNX", KEYS[2], "0")
    redis.call("EXPIRE", KEYS[3], tonumber(ARGV[3]))
    return "switched"
else
    return "session_conflict"
end
"""


class CommitUnknownError(RuntimeError):
    pass


class NonceReplayError(RuntimeError):
    pass


@dataclass(frozen=True, slots=True)
class RedisProcessResult:
    status: str


class IngestRedisStore:
    def __init__(self, client: redis.Redis, settings: IngestSettings) -> None:
        self._client = client
        self._settings = settings

    @classmethod
    def create(cls, settings: IngestSettings) -> "IngestRedisStore":
        client = redis.from_url(settings.redis_url, decode_responses=True)
        return cls(client, settings)

    async def close(self) -> None:
        await self._client.aclose()

    async def listen_control_invalidations(
        self, callback: Callable[[dict], None]
    ) -> None:
        while True:
            pubsub = self._client.pubsub()
            try:
                await pubsub.subscribe(CONTROL_INVALIDATION_CHANNEL)
                while True:
                    message = await pubsub.get_message(
                        ignore_subscribe_messages=True,
                        timeout=1.0,
                    )
                    if message is None:
                        await asyncio.sleep(0)
                        continue
                    try:
                        payload = msgspec.json.decode(message["data"])
                    except (msgspec.DecodeError, TypeError, KeyError):
                        continue
                    if isinstance(payload, dict):
                        callback(payload)
            except asyncio.CancelledError:
                raise
            except RedisError:
                await asyncio.sleep(1.0)
            finally:
                try:
                    await pubsub.aclose()
                except RedisError:
                    pass
    async def reserve_nonce(self, node_uuid: str, nonce: str, ttl_seconds: int) -> bool:
        result = await self._client.set(
            f"ingest:nonce:{node_uuid}:{nonce}",
            "1",
            nx=True,
            ex=ttl_seconds,
        )
        return bool(result)

    async def process_item(
        self,
        node_uuid: str,
        item: UploadItem,
        action: str,
        *,
        nonce: str | None = None,
    ) -> RedisProcessResult:
        return (
            await self.process_items(
                node_uuid,
                [(item, action)],
                nonce=nonce or item.batch_id,
            )
        )[0]

    async def process_items(
        self,
        node_uuid: str,
        entries: list[tuple[UploadItem, str]],
        *,
        nonce: str,
    ) -> list[RedisProcessResult]:
        if not entries:
            return []
        session_ids = {item.upload_session_id for item, _ in entries}
        if len(session_ids) != 1:
            raise ValueError("one Redis batch may contain only one upload_session_id")

        first_item = entries[0][0]
        keys = [
            f"hw:{node_uuid}:{first_item.upload_session_id}",
            f"active_session:{node_uuid}",
            "rt:change",
        ]
        payload = []
        for item, action in entries:
            envelope = {
                "device_code": item.device_code,
                "device_type": item.device_type,
                "group_code": item.group_code,
                "scan_rate": item.scan_rate,
                "ts": item.ts,
                "config_version": item.config_version,
                "values_json": msgspec.json.encode(item.values).decode("utf-8"),
                "contract_version": item.contract_version,
            }
            if item.contract_version == 2:
                envelope["qualities_json"] = msgspec.json.encode(
                    item.qualities or {}
                ).decode("utf-8")

            latest_updates = build_latest_updates(
                item.values,
                ts=item.ts,
                assignment_epoch=item.assignment_epoch,
                qualities=item.qualities,
            )
            keys.extend(
                (
                    stream_key(
                        item.device_code,
                        item.device_type,
                        item.group_code,
                        self._settings.stream_shard_count,
                    ),
                    f"rt:latest:{item.device_code}:{item.group_code}",
                )
            )
            payload.append(
                {
                    "local_seq": item.local_seq,
                    "action": action,
                    "envelope": envelope,
                    "latest_updates": latest_updates,
                    "change": {
                        "device_code": item.device_code,
                        "group_code": item.group_code,
                    },
                }
            )
        keys.extend(
            (
                f"ingest:nonce:{node_uuid}:{nonce}",
                "ingest:stream_trim_item_counts",
            )
        )

        try:
            result = await self._client.eval(
                INGEST_BATCH_LUA,
                len(keys),
                *keys,
                first_item.upload_session_id,
                str(self._settings.stream_maxlen),
                msgspec.json.encode(payload).decode("utf-8"),
                str(STREAM_TRIM_EVERY_ITEMS),
                str(self._settings.nonce_ttl_seconds),
            )
        except RedisError as exc:
            raise CommitUnknownError(str(exc)) from exc
        statuses = msgspec.json.decode(_decode_result(result))
        if statuses == ["nonce_replay"]:
            raise NonceReplayError("nonce already used")
        return [RedisProcessResult(status=str(status)) for status in statuses]
    async def switch_session(
        self,
        node_uuid: str,
        old_session_id: str,
        new_session_id: str,
    ) -> str:
        keys = (
            f"active_session:{node_uuid}",
            f"hw:{node_uuid}:{new_session_id}",
            f"hw:{node_uuid}:{old_session_id}",
        )
        try:
            result = await self._client.eval(
                SESSION_SWITCH_LUA,
                3,
                *keys,
                old_session_id,
                new_session_id,
                str(self._settings.max_offline_retention_days * 24 * 60 * 60),
            )
        except RedisError as exc:
            raise CommitUnknownError(str(exc)) from exc
        return _decode_result(result)

    async def get_active_session(self, node_uuid: str) -> str | None:
        value = await self._client.get(f"active_session:{node_uuid}")
        return str(value) if value else None


def _decode_result(value) -> str:
    if isinstance(value, bytes):
        return value.decode("utf-8")
    return str(value)
