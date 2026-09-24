import asyncio

import pytest

from benchmark_writer_packing import benchmark, build_rows


def test_fixture_is_reproducible_and_includes_good_and_bad_quality():
    rows = build_rows(2, 11, 2)
    assert rows == build_rows(2, 11, 2)
    assert len(rows) == 4
    assert rows[1]["ts"] - rows[0]["ts"] == 100
    assert set(rows[0]["qualities"].values()) == {0, 192}


@pytest.mark.parametrize("dimensions", [(0, 1, 1), (1, -1, 1), (1, 1, 0)])
def test_fixture_rejects_invalid_dimensions(dimensions):
    with pytest.raises(ValueError):
        build_rows(*dimensions)


def test_offline_packing_checks_complete_results_without_database():
    result = asyncio.run(benchmark(2, 10, 2))
    assert result["passed"]
    assert result["logical_frames"] == result["unique_complete_frame_results"] == 4
    assert result["expected_quality_rows"] == 40
    assert result["max_sql_bytes"] <= result["sql_byte_budget"]
