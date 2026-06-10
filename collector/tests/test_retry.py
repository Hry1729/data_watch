"""Retry helper tests."""

from unittest.mock import MagicMock

import pytest

import core.retry as retry
from core.retry import with_retry


@pytest.mark.asyncio
async def test_with_retry_uses_self_logger_warning(monkeypatch):
    async def _no_sleep(_delay):
        return None

    monkeypatch.setattr(retry.asyncio, "sleep", _no_sleep)

    class Service:
        def __init__(self):
            self.logger = MagicMock()
            self.calls = 0

        @with_retry(max_retries=1, base_delay=0)
        async def run(self):
            self.calls += 1
            if self.calls == 1:
                raise TimeoutError("temporary")
            return "ok"

    service = Service()

    assert await service.run() == "ok"
    service.logger.warning.assert_called_once()
