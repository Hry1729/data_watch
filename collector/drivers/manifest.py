"""Collector adapter for the shared static driver contract."""
from data_watch_driver_manifest import *  # noqa: F403
from data_watch_driver_manifest import validate_package_compatibility as _validate
from core.version import __version__


def validate_package_compatibility(manifest, package_root, *, expected_abi=None, collector_version=None):
    return _validate(manifest, package_root, expected_abi=expected_abi, collector_version=collector_version or __version__)
