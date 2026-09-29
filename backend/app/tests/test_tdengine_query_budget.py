from __future__ import annotations

import asyncio
import threading

import pytest

from app.db import tdengine


@pytest.mark.asyncio
async def test_query_timeout_keeps_slot_until_worker_finishes(monkeypatch):
    monkeypatch.setattr(tdengine, "_query_semaphore", asyncio.Semaphore(1))
    monkeypatch.setattr(tdengine, "_QUEUE_WAIT_SECONDS", 0.03)
    started = threading.Event()
    release = threading.Event()
    finished = threading.Event()
    client = tdengine.TDengineClient("localhost", 6041, "root", "", "device_data")

    def query(sql, _database):
        if sql == "first":
            started.set()
            try:
                release.wait(1)
            finally:
                finished.set()
        return [(sql,)]

    monkeypatch.setattr(client, "_fetch_all_sync", query)
    first = asyncio.create_task(client.fetch_all("first", timeout=0.03))
    try:
        assert await asyncio.to_thread(started.wait, 1)
        with pytest.raises(TimeoutError):
            await first
        with pytest.raises(TimeoutError, match="queue"):
            await client.fetch_all("second")
    finally:
        release.set()

    assert await asyncio.to_thread(finished.wait, 1)
    await asyncio.sleep(0.01)
    assert await client.fetch_all("third") == [("third",)]
