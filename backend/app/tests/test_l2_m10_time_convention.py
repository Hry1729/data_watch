"""L2 / M10 回归：时间约定统一。

L2：注册码过期时间用 ``datetime.now()``（进程本地时区），其余分布式逻辑用固定 UTC+8；
    非容器 / Windows 部署下会差 8 小时，``expiresAt`` 也不带时区。
M10：导出任务 ``created_at`` 统一为带时区的 UTC；旧 tasks.json 里不带时区的 ISO 字符串
    与 ``datetime.now(timezone.utc)`` 相减会抛 TypeError，清理协程随之退出。
"""

from __future__ import annotations

import json
from datetime import datetime, timedelta, timezone

import pytest

from app.services import distributed_nodes as distributed_module
from app.services.distributed_nodes import DistributedNodeService
from app.services.task_manager import ExportTaskManager

_UTC8 = timezone(timedelta(hours=8))


class _FrozenDatetime(datetime):
    """模拟进程本地时区为 UTC（与中心约定 UTC+8 相差 8 小时）。"""

    @classmethod
    def now(cls, tz=None):
        instant = datetime(2026, 8, 1, 4, 0, 0, tzinfo=timezone.utc)
        if tz is None:
            return instant.replace(tzinfo=None)  # 进程本地时间 = UTC
        return instant.astimezone(tz)


class _CaptureSession:
    def __init__(self, row=None):
        self.params = []
        self._row = row
        self.commits = 0

    async def execute(self, _statement, params=None):
        self.params.append(params or {})
        session = self

        class _Result:
            def mappings(self):
                return self

            def first(self):
                return session._row

        return _Result()

    async def commit(self):
        self.commits += 1

    async def rollback(self):
        pass


@pytest.mark.asyncio
async def test_registration_code_expiry_uses_center_timezone(monkeypatch) -> None:
    monkeypatch.setattr(distributed_module, "datetime", _FrozenDatetime)
    session = _CaptureSession()

    result = await DistributedNodeService(session).create_registration_code(
        node_name="edge-01", actor="admin", expires_minutes=30
    )

    stored = session.params[0]["expires_at"]
    # 存库沿用中心约定：UTC+8 的 naive 时间（04:00Z → 12:00 +08:00，再加 30 分钟）
    assert stored == datetime(2026, 8, 1, 12, 30, 0)
    # 返回值带 +08:00，客户端不必猜时区
    assert result["expiresAt"] == "2026-08-01T12:30:00+08:00"


@pytest.mark.asyncio
async def test_registration_code_expiry_check_uses_center_timezone(monkeypatch) -> None:
    monkeypatch.setattr(distributed_module, "datetime", _FrozenDatetime)
    # 中心时间 12:00(+08:00)，注册码 11:50(+08:00) 已过期。
    # 修复前与进程本地时间 04:00 比较，误判为仍有效。
    session = _CaptureSession(
        row={
            "node_name": "edge-01",
            "expires_at": datetime(2026, 8, 1, 11, 50, 0),
            "used_at": None,
            "used_by_node_uuid": None,
        }
    )
    from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
    from cryptography.hazmat.primitives.serialization import Encoding, PublicFormat

    public_key = Ed25519PrivateKey.generate().public_key().public_bytes(
        Encoding.PEM, PublicFormat.SubjectPublicKeyInfo
    ).decode()
    from app.core.exceptions import ApiError

    with pytest.raises(ApiError, match="已过期"):
        await DistributedNodeService(session).register_node(
            {
                "registrationCode": "code",
                "nodeUuid": "3911959d-3e0d-45a4-880a-d3c7df3a2d65",
                "publicKeyPem": public_key,
            }
        )


def test_legacy_naive_task_timestamps_do_not_break_cleanup(tmp_path) -> None:
    tasks_file = tmp_path / "tasks.json"
    old = (datetime.now(_UTC8) - timedelta(days=3)).replace(tzinfo=None)
    tasks_file.write_text(
        json.dumps(
            {
                "legacy": {
                    "task_id": "legacy",
                    "status": "completed",
                    "created_at": old.isoformat(),  # 旧版本写入的不带时区时间
                    "progress": 100,
                    "file_path": None,
                    "error": None,
                    "metadata": {},
                },
                "fresh": {
                    "task_id": "fresh",
                    "status": "completed",
                    "created_at": datetime.now(timezone.utc).isoformat(),
                    "progress": 100,
                    "file_path": None,
                    "error": None,
                    "metadata": {},
                },
            }
        ),
        encoding="utf-8",
    )
    ExportTaskManager._instance = None
    try:
        manager = ExportTaskManager(tasks_file=str(tasks_file))
        legacy = manager.get_task("legacy")
        assert legacy is not None
        assert legacy.created_at.tzinfo is not None
        # 不带时区的旧值按项目约定视为 UTC+8
        assert legacy.created_at == old.replace(tzinfo=_UTC8)

        manager.perform_cleanup()  # 修复前：TypeError（naive 与 aware 相减）

        assert manager.get_task("legacy") is None
        assert manager.get_task("fresh") is not None
    finally:
        ExportTaskManager._instance = None
