from uuid import uuid4
import pytest
from edge.app.outbox import OutboxStore


def test_foreign_session_cannot_be_taken_over(tmp_path):
    store = OutboxStore(tmp_path / "edge.db")
    original = store.current_session_id()
    with pytest.raises(RuntimeError, match="foreign"):
        store.prepare_session_switch(expected_old_session_id=str(uuid4()))
    assert store.current_session_id() == original
    assert store.pending_session_switch() is None
    store.close()


def test_pending_switch_is_idempotent_and_cannot_be_retargeted(tmp_path):
    store = OutboxStore(tmp_path / "edge.db")
    state = store.prepare_session_switch()
    assert store.prepare_session_switch() == state
    with pytest.raises(RuntimeError, match="foreign"):
        store.prepare_session_switch(expected_old_session_id=str(uuid4()))
    assert store.pending_session_switch() == state
    store.close()
