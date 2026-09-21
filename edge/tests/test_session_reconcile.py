import sqlite3
from pathlib import Path
from uuid import uuid4

import pytest

from edge.app.outbox import OutboxStore, SessionSwitchState


def test_prepare_session_switch_uses_expected_center_session(tmp_path: Path):
    store = OutboxStore(tmp_path / "edge.db")
    local_session = store.current_session_id()

    # 场景：本地是 local_session，但中心当前活跃的是 center_session
    center_session = str(uuid4())
    state = store.prepare_session_switch(expected_old_session_id=center_session)

    # 验证生成的切换状态使用的是中心会话，能够使中心的 SESSION_SWITCH_LUA CAS 成功
    assert state.old_session_id == center_session
    assert state.new_session_id != local_session
    assert state.new_session_id != center_session


def test_prepare_session_switch_updates_stale_pending_switch(tmp_path: Path):
    store = OutboxStore(tmp_path / "edge.db")
    local_session = store.current_session_id()

    # 先以本地会话进入 pending 状态
    state1 = store.prepare_session_switch()
    assert state1.old_session_id == local_session

    # 之后查询中心发现中心会话为 center_session，再次调用应修正 pending 记录
    center_session = str(uuid4())
    state2 = store.prepare_session_switch(expected_old_session_id=center_session)
    assert state2.old_session_id == center_session
    assert state2.new_session_id == state1.new_session_id
