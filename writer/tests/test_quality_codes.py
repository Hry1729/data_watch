"""冻结平台质量码常量测试。"""

import importlib
import importlib.util
import sys
from pathlib import Path

import pytest

import quality_codes as writer_qc


COLLECTOR_QC_PATH = Path(__file__).resolve().parents[2] / "collector" / "core" / "quality_codes.py"
BACKEND_QC_PATH = Path(__file__).resolve().parents[2] / "backend" / "app" / "core" / "quality_codes.py"


def _load_module_from_path(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    return mod


@pytest.fixture(scope="module")
def collector_qc():
    collector_root = COLLECTOR_QC_PATH.parents[1]
    if str(collector_root) not in sys.path:
        sys.path.insert(0, str(collector_root))
    return _load_module_from_path("collector_quality_codes", COLLECTOR_QC_PATH)


@pytest.fixture(scope="module")
def backend_qc():
    backend_root = BACKEND_QC_PATH.parents[2]
    if str(backend_root) not in sys.path:
        sys.path.insert(0, str(backend_root))
    return _load_module_from_path("backend_quality_codes", BACKEND_QC_PATH)


FROZEN_VALUES = {
    "QUALITY_GOOD": 0,
    "QUALITY_UNCERTAIN": 2,
    "QUALITY_BAD": 5,
    "QUALITY_STALE": 6,
    "QUALITY_NOT_CONNECTED": 7,
    "QUALITY_CONFIG_DECODE_ERROR": 8,
}


class TestQualityCodesFrozen:
    @pytest.mark.parametrize("name,value", list(FROZEN_VALUES.items()))
    def test_writer_frozen_constants(self, name, value):
        assert getattr(writer_qc, name) == value

    @pytest.mark.parametrize("name,value", list(FROZEN_VALUES.items()))
    def test_collector_frozen_constants(self, collector_qc, name, value):
        assert getattr(collector_qc, name) == value

    @pytest.mark.parametrize("name,value", list(FROZEN_VALUES.items()))
    def test_backend_frozen_constants(self, backend_qc, name, value):
        assert getattr(backend_qc, name) == value

    def test_reserved_codes_frozenset(self, collector_qc, backend_qc):
        expected = frozenset({1, 3, 4})
        assert writer_qc.RESERVED_QUALITY_CODES == expected
        assert collector_qc.RESERVED_QUALITY_CODES == expected
        assert backend_qc.RESERVED_QUALITY_CODES == expected


class TestIsKnownQuality:
    @pytest.mark.parametrize("code", [0, 2, 5, 6, 7, 8])
    def test_known_business_codes(self, code):
        assert writer_qc.is_known_quality(code) is True

    @pytest.mark.parametrize("code", [1, 3, 4])
    def test_reserved_not_known_business(self, code):
        assert writer_qc.is_known_quality(code) is False
        assert code in writer_qc.RESERVED_QUALITY_CODES


class TestIsValidQuality:
    @pytest.mark.parametrize("code", [0, 1, 2, 3, 4, 5, 6, 7, 8, 99, 192, 255])
    def test_valid_codes(self, code, collector_qc, backend_qc):
        assert writer_qc.is_valid_quality(code) is True
        assert collector_qc.is_valid_quality(code) is True
        assert backend_qc.is_valid_quality(code) is True

    @pytest.mark.parametrize("code", [-1, 256, 0.9, 5.9, True, False, "0", None])
    def test_invalid_codes(self, code, collector_qc, backend_qc):
        assert writer_qc.is_valid_quality(code) is False
        assert collector_qc.is_valid_quality(code) is False
        assert backend_qc.is_valid_quality(code) is False
