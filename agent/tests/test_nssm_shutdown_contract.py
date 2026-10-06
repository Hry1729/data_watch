from pathlib import Path
import re

import pytest


@pytest.mark.parametrize("installer", ["DataWatch.iss", "DataWatchRole.iss"])
def test_service_stop_budget_covers_agent_shutdown(installer):
    root = Path(__file__).resolve().parents[2]
    source = (root / "installer" / installer).read_text(encoding="utf-8-sig")
    factory = (root / "agent/app/factory.py").read_text(encoding="utf-8")
    agent_seconds = float(re.search(r"_SHUTDOWN_TIMEOUT_SECONDS = ([\d.]+)", factory)[1])
    console_ms = int(re.search(r"AppStopMethodConsole (\d+)", source)[1])
    assert console_ms >= (agent_seconds + 5) * 1000
    assert re.search(r"AppStopMethodWindow \d+", source)
    assert re.search(r"AppStopMethodThreads \d+", source)
