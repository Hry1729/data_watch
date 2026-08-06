from __future__ import annotations

import pytest

from scripts.benchmark_ingest_capacity import node_phase_offset


def test_node_phase_offsets_are_uniform_within_one_period() -> None:
    offsets = [node_phase_offset(index, 4, 0.1) for index in range(4)]

    assert offsets == pytest.approx([0.0, 0.025, 0.05, 0.075])