import os
import sys

import pytest

# 让 `import app.*` 在任意 cwd 下可用（agent/ 加入 sys.path）
_AGENT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if _AGENT_DIR not in sys.path:
    sys.path.insert(0, _AGENT_DIR)

from app.services.process_manager import ModuleSpec, ProcessManager  # noqa: E402

_FIXTURES = os.path.join(os.path.dirname(__file__), "fixtures")
DUMMY_CHILD = os.path.join(_FIXTURES, "dummy_child.py")
FAKE_PROXY = os.path.join(_FIXTURES, "fake_proxy.py")


@pytest.fixture
def runtime_dir(tmp_path):
    d = tmp_path / "runtime"
    for sub in ("pids", "opcda_proxy", "logs", "state"):
        (d / sub).mkdir(parents=True, exist_ok=True)
    return d


@pytest.fixture
def make_pm(runtime_dir, tmp_path):
    """构造一个跟踪 dummy_child 的 ProcessManager。

    child_args 省略时默认监听 shutdown_flag（优雅退出路径）；
    传入 ["--ignore-flag"] 测强杀；传入 ["--exit-after","0.3","--exit-code","1"] 测 crashed。
    """
    def _make(name: str = "collector", child_args=None):
        flag = str(runtime_dir / f"{name}.shutdown_flag")
        if child_args is None:
            child_args = ["--flag", flag]
        spec = ModuleSpec(
            name=name,
            executable=sys.executable,
            args=[DUMMY_CHILD, *child_args],
            working_dir=str(tmp_path),
        )
        pm = ProcessManager(runtime_dir=runtime_dir, modules={name: spec})
        return pm, name

    return _make
