"""The Docker backend must stay free-threaded after loading the application."""

import sys
import sysconfig

import pytest


@pytest.mark.skipif(
    sysconfig.get_config_var("Py_GIL_DISABLED") != 1,
    reason="requires a free-threaded Python build",
)
def test_backend_import_does_not_enable_gil():
    assert not sys._is_gil_enabled(), "GIL was enabled before app import"
    import main  # noqa: F401

    assert not sys._is_gil_enabled(), "a backend dependency enabled the GIL"
