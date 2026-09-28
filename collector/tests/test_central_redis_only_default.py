"""中心式采集架构防复发测试。

本轮架构决定（用户明确指定）：
  中心式：PLC/OPC/外部源 → 中心 Collector（采集/解析/分组）→ RedisWriter **直接批量写 Redis**
          （latest / window / Stream）→ Writer 消费 → TDengine。
  分布式：Edge Collector → 本地 SQLite(Outbox) → Uploader → 中心 Ingest → Redis → Writer → TDengine。

本文件把该决定钉成可执行断言，防止中心默认再被改回 Durable 持久回放：
  1. collector/main.py 导入并默认构造 RedisWriter（storage_path = redis_only）；
  2. 中心默认分支不得构造 DurableRedisWriter / 采样 outbox，也不得启动回放；
  3. writer_factory 边界必须保留（edge 入口靠它注入 EdgeOutboxWriter）；
  4. 真实构造和采集由 test_central_runtime_redis_only.py 验证。
"""

from __future__ import annotations

import ast
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
MAIN_PY = REPO_ROOT / "collector" / "main.py"
EDGE_MAIN_PY = REPO_ROOT / "edge" / "collector_main.py"


def _main_source() -> str:
    return MAIN_PY.read_text(encoding="utf-8")


def _main_tree() -> ast.Module:
    return ast.parse(_main_source())


def _imported_names(tree: ast.Module) -> set[str]:
    names: set[str] = set()
    for node in ast.walk(tree):
        if isinstance(node, ast.ImportFrom):
            for alias in node.names:
                names.add(alias.asname or alias.name)
        elif isinstance(node, ast.Import):
            for alias in node.names:
                names.add(alias.name)
    return names


def _default_writer_branch(tree: ast.Module) -> ast.If:
    """取出 CollectorApp.__init__ 中给 self.redis_writer 赋值的默认分支。"""
    for node in ast.walk(tree):
        if isinstance(node, ast.If) and isinstance(node.test, ast.Compare):
            left = node.test.left
            if (isinstance(left, ast.Name) and left.id == "writer_factory"
                    and any(isinstance(op, ast.Is) for op in node.test.ops)
                    and any(
                        isinstance(item, ast.Assign)
                        and any(
                            isinstance(target, ast.Attribute) and target.attr == "redis_writer"
                            for target in item.targets
                        )
                        for item in node.body
                    )):
                return node
    raise AssertionError("未找到中心默认存储赋值分支")


def _called_names(node: ast.AST) -> list[str]:
    called = []
    for child in ast.walk(node):
        if isinstance(child, ast.Call):
            func = child.func
            if isinstance(func, ast.Name):
                called.append(func.id)
            elif isinstance(func, ast.Attribute):
                called.append(func.attr)
    return called


def test_central_imports_redis_writer_not_durable():
    names = _imported_names(_main_tree())
    assert "RedisWriter" in names, "中心入口必须导入 RedisWriter"
    assert "DurableRedisWriter" not in names, "中心入口不得再导入 DurableRedisWriter"
    assert "from storage.redis_writer import RedisWriter" in _main_source()


def test_central_default_branch_constructs_redis_writer():
    branch = _default_writer_branch(_main_tree())
    called = _called_names(branch)
    assert "RedisWriter" in called, "中心默认分支必须构造 RedisWriter"
    assert "DurableRedisWriter" not in called, "中心默认分支不得构造 DurableRedisWriter"


def test_central_default_has_no_sampling_outbox():
    called = _called_names(_default_writer_branch(_main_tree()))
    # 允许只读识别遗留数据库，但中心默认分支不得构造采样 Outbox 或执行回放。
    for banned in ("DurableRedisWriter", "CollectorRetryOutbox", "start_replay", "pause_replay", "delivery_timeline"):
        assert banned not in called, f"中心默认分支不得调用 {banned}"


def test_writer_factory_boundary_preserved():
    source = _main_source()
    assert "writer_factory is None" in source
    assert "writer_factory(config, self.performance_metrics)" in source
    # 分布式入口仍然通过该边界注入自己的存储适配器
    edge_source = EDGE_MAIN_PY.read_text(encoding="utf-8")
    assert "EdgeOutboxWriter" in edge_source
    assert "EdgeStorageClient" in edge_source
    assert "writer_factory=_writer_factory" in edge_source


def test_storage_path_semantics():
    from storage.durable_redis_writer import DurableRedisWriter
    from storage.redis_writer import RedisWriter

    assert RedisWriter.storage_path == "redis_only"
    assert DurableRedisWriter.storage_path == "sqlite_outbox_redis_aof"


@pytest.mark.parametrize("name", ["write_window_stream", "write_latest", "ping", "close"])
def test_redis_writer_keeps_central_contract(name):
    """中心直写仍需提供既定写入契约（window / latest / 生命周期）。"""
    from storage.redis_writer import RedisWriter

    assert callable(getattr(RedisWriter, name))
