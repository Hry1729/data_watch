from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timedelta
from functools import lru_cache
import hashlib
import importlib.util
from threading import RLock
from typing import Any

from app.core.config import Settings, get_settings
from app.core.logging import get_logger


logger = get_logger("backend.perspective")


@dataclass(frozen=True)
class PerspectiveProtectionConfig:
    enabled: bool
    ws_path: str
    max_clients: int
    session_idle_ttl_seconds: int
    session_restore_ttl_seconds: int
    max_sessions_per_owner: int
    max_sessions_global: int
    max_points_per_query: int
    max_range_hours: int
    max_records_per_query: int
    ping_interval_seconds: int
    timeout_seconds: int


@dataclass
class PerspectiveSession:
    owner_key: str
    session_id: str
    created_at: datetime
    updated_at: datetime
    last_query_at: datetime | None = None
    retain_until: datetime | None = None
    active_connection_id: str | None = None
    connection_ids: set[str] = field(default_factory=set)
    last_query_version: int = 0
    records: list[dict[str, Any]] = field(default_factory=list)

    @property
    def key(self) -> str:
        return f"{self.owner_key}:{self.session_id}"


class PerspectiveSessionManager:
    def __init__(self, config: PerspectiveProtectionConfig):
        self.config = config
        self._lock = RLock()
        self._sessions: dict[str, PerspectiveSession] = {}
        self._transports: dict[str, dict[str, Any]] = {}

    @staticmethod
    def _now() -> datetime:
        return datetime.now()

    def runtime_available(self) -> bool:
        return importlib.util.find_spec("perspective") is not None

    def build_owner_key(self, identity: str | None) -> str:
        source = (identity or "anonymous").strip() or "anonymous"
        digest = hashlib.sha256(source.encode("utf-8")).hexdigest()
        return digest[:24]

    def session_key(self, owner_key: str, session_id: str) -> str:
        return f"{owner_key}:{session_id}"

    def get_session(self, owner_key: str, session_id: str) -> PerspectiveSession | None:
        with self._lock:
            self._prune_expired_locked()
            return self._sessions.get(self.session_key(owner_key, session_id))

    def ensure_session(self, owner_key: str, session_id: str) -> PerspectiveSession:
        now = self._now()
        with self._lock:
            self._prune_expired_locked(now)
            key = self.session_key(owner_key, session_id)
            session = self._sessions.get(key)
            if session is not None:
                session.updated_at = now
                return session

            self._ensure_capacity_locked(owner_key, now)
            session = PerspectiveSession(
                owner_key=owner_key,
                session_id=session_id,
                created_at=now,
                updated_at=now,
            )
            self._sessions[key] = session
            return session

    def attach_connection(
        self,
        owner_key: str,
        session_id: str,
        connection_id: str,
    ) -> tuple[PerspectiveSession, bool, bool]:
        now = self._now()
        with self._lock:
            self._prune_expired_locked(now)
            key = self.session_key(owner_key, session_id)
            restored = key in self._sessions

            if not restored:
                self._ensure_capacity_locked(owner_key, now)
                self._sessions[key] = PerspectiveSession(
                    owner_key=owner_key,
                    session_id=session_id,
                    created_at=now,
                    updated_at=now,
                )

            session = self._sessions[key]
            conflict = bool(
                session.active_connection_id
                and session.active_connection_id != connection_id
                and session.connection_ids
            )
            if conflict:
                return session, restored, True

            session.active_connection_id = connection_id
            session.connection_ids.add(connection_id)
            session.updated_at = now
            session.retain_until = None
            return session, restored, False

    def bind_transport(
        self,
        owner_key: str,
        session_id: str,
        connection_id: str,
        transport: Any,
    ) -> None:
        with self._lock:
            key = self.session_key(owner_key, session_id)
            if key not in self._sessions:
                return
            self._transports.setdefault(key, {})[connection_id] = transport

    def detach_connection(self, owner_key: str, session_id: str, connection_id: str) -> None:
        now = self._now()
        with self._lock:
            key = self.session_key(owner_key, session_id)
            session = self._sessions.get(key)
            if session is None:
                return

            transport_map = self._transports.get(key)
            if transport_map is not None:
                transport_map.pop(connection_id, None)
                if not transport_map:
                    self._transports.pop(key, None)

            session.connection_ids.discard(connection_id)
            if session.active_connection_id == connection_id:
                session.active_connection_id = None
            session.updated_at = now

            if not session.connection_ids:
                session.retain_until = now + timedelta(seconds=self.config.session_restore_ttl_seconds)

            self._prune_expired_locked(now)

    def store_snapshot(
        self,
        owner_key: str,
        session_id: str,
        query_version: int,
        records: list[dict[str, Any]],
    ) -> bool:
        now = self._now()
        with self._lock:
            session = self._sessions.get(self.session_key(owner_key, session_id))
            if session is None:
                return False
            if query_version < session.last_query_version:
                return False

            session.last_query_version = query_version
            session.records = list(records)
            session.last_query_at = now
            session.updated_at = now
            return True

    async def publish_snapshot(self, owner_key: str, session_id: str) -> bool:
        with self._lock:
            key = self.session_key(owner_key, session_id)
            session = self._sessions.get(key)
            if session is None:
                return False
            snapshot = self.serialize_snapshot(session)
            transports = dict(self._transports.get(key, {}))

        failed_connection_ids: list[str] = []
        for connection_id, transport in transports.items():
            try:
                await transport.send_json(snapshot)
            except Exception:
                failed_connection_ids.append(connection_id)

        for connection_id in failed_connection_ids:
            self.detach_connection(owner_key, session_id, connection_id)

        return True

    def serialize_snapshot(self, session: PerspectiveSession) -> dict[str, Any]:
        return {
            "type": "snapshot",
            "sessionId": session.session_id,
            "queryVersion": session.last_query_version,
            "recordCount": len(session.records),
            "records": session.records,
            "timestamp": session.updated_at.strftime("%Y-%m-%d %H:%M:%S"),
        }

    def _prune_expired_locked(self, now: datetime | None = None) -> None:
        current = now or self._now()
        idle_deadline = current - timedelta(seconds=self.config.session_idle_ttl_seconds)

        expired_keys: list[str] = []
        for key, session in self._sessions.items():
            if not session.connection_ids and session.retain_until and session.retain_until <= current:
                expired_keys.append(key)
                continue
            if not session.connection_ids and session.updated_at <= idle_deadline:
                expired_keys.append(key)

        for key in expired_keys:
            self._sessions.pop(key, None)
            self._transports.pop(key, None)

    def _ensure_capacity_locked(self, owner_key: str, now: datetime) -> None:
        self._evict_idle_sessions_locked(
            lambda session: session.owner_key == owner_key,
            self.config.max_sessions_per_owner - 1,
            now,
        )
        owner_count = sum(1 for session in self._sessions.values() if session.owner_key == owner_key)
        if owner_count >= self.config.max_sessions_per_owner:
            raise RuntimeError("Perspective owner session limit reached")

        self._evict_idle_sessions_locked(
            lambda _session: True,
            self.config.max_sessions_global - 1,
            now,
        )
        if len(self._sessions) >= self.config.max_sessions_global:
            raise RuntimeError("Perspective global session limit reached")

    def _evict_idle_sessions_locked(
        self,
        predicate,
        limit_after_evict: int,
        now: datetime,
    ) -> None:
        matching_sessions = [session for session in self._sessions.values() if predicate(session)]
        if len(matching_sessions) <= limit_after_evict:
            return

        idle_sessions = sorted(
            (session for session in matching_sessions if not session.connection_ids),
            key=lambda session: session.updated_at,
        )
        while len(matching_sessions) > limit_after_evict and idle_sessions:
            victim = idle_sessions.pop(0)
            self._sessions.pop(victim.key, None)
            self._transports.pop(victim.key, None)
            matching_sessions = [session for session in self._sessions.values() if predicate(session)]

    def describe(self) -> dict[str, Any]:
        with self._lock:
            return {
                "enabled": self.config.enabled,
                "runtimeAvailable": self.runtime_available(),
                "wsPath": self.config.ws_path,
                "sessionCount": len(self._sessions),
                "maxSessionsGlobal": self.config.max_sessions_global,
                "maxSessionsPerOwner": self.config.max_sessions_per_owner,
            }


def build_perspective_protection_config(
    settings: Settings | None = None,
) -> PerspectiveProtectionConfig:
    resolved = settings or get_settings()
    return PerspectiveProtectionConfig(
        enabled=resolved.perspective_enabled,
        ws_path=resolved.perspective_ws_route,
        max_clients=resolved.perspective_max_clients,
        session_idle_ttl_seconds=resolved.perspective_session_idle_ttl_seconds,
        session_restore_ttl_seconds=resolved.perspective_session_restore_ttl_seconds,
        max_sessions_per_owner=resolved.perspective_max_sessions_per_owner,
        max_sessions_global=resolved.perspective_max_sessions_global,
        max_points_per_query=resolved.perspective_max_points_per_query,
        max_range_hours=resolved.perspective_max_range_hours,
        max_records_per_query=resolved.perspective_max_records_per_query,
        ping_interval_seconds=resolved.perspective_ping_interval_seconds,
        timeout_seconds=resolved.perspective_timeout_seconds,
    )


@lru_cache(maxsize=1)
def get_perspective_session_manager() -> PerspectiveSessionManager:
    manager = PerspectiveSessionManager(build_perspective_protection_config())
    logger.info("Perspective session manager ready: %s", manager.describe())
    return manager
