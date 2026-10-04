"""B1 验收：用**独立子进程调用真实入口**验证退出码与 stdout 单一包络。

清单明确要求："用独立子进程调用真实入口，覆盖成功、参数错、认证、权限、网络、超时、服务异常和
中断；同时断言退出码、stdout 单一包络和 stderr。**不能仅用 CliRunner 替代生产入口验证**。"

本文件覆盖其中可确定性复现的两类（其余类别依赖真实网络/服务/中断，属目标机或后续补充）：
- **认证缺失**：`doctor` 的后端与兼容性检查可达（本地 stub 提供 `/healthz` 与
  `/api/cli/compatibility`，二者均免鉴权），但**没有凭据** ⇒ `checks.authentication.ok == False`
  ⇒ 修复后退出码必须是 **3**（`EXIT_AUTH_REQUIRED`），而修复前是 0（显式失败退出码丢失）。
- **参数错**：未知选项 ⇒ 退出码 **2**（`EXIT_USAGE`），并同样只输出一个 JSON 包络。

顺带断言：**stdout 是单一 JSON 包络**（可整体 `json.loads`）、**stderr 不输出异常回溯**。
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]


class _StubHandler(BaseHTTPRequestHandler):
    def do_GET(self) -> None:  # noqa: N802 (http.server 的命名约定)
        if self.path.startswith("/healthz"):
            body = {"ok": True, "data": {"status": "ok"}}
        elif self.path.startswith("/api/cli/compatibility"):
            body = {"ok": True, "data": {"compatible": True}}
        else:
            self.send_response(404)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(b'{"ok": false, "error": {"message": "not found"}}')
            return
        payload = json.dumps(body).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def log_message(self, *args) -> None:  # 静默，避免污染测试输出
        return None


class _Stub:
    def __enter__(self) -> str:
        self._server = ThreadingHTTPServer(("127.0.0.1", 0), _StubHandler)
        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)
        self._thread.start()
        host, port = self._server.server_address[:2]
        return f"http://{host}:{port}"

    def __exit__(self, exc_type, exc, tb) -> bool:
        self._server.shutdown()
        self._server.server_close()
        return False


def _run_cli(base_url: str, tmp_path: Path, *arguments: str) -> subprocess.CompletedProcess[str]:
    """用**子进程**执行真实入口（`python -m cli`），并把配置/凭据隔离到临时目录。"""
    environment = dict(os.environ)
    environment.update(
        {
            "DATAWATCH_API_URL": base_url,
            "DATAWATCH_CONFIG_FILE": str(tmp_path / "cli-config.json"),
            "APPDATA": str(tmp_path),
            "PYTHONPATH": os.pathsep.join(
                [str(REPO_ROOT), environment.get("PYTHONPATH", "")]
            ).strip(os.pathsep),
            "PYTHONUTF8": "1",
        }
    )
    environment.pop("DATAWATCH_TOKEN", None)  # 明确"未登录"
    # 必须显式指定 UTF-8：CLI 自身把标准流重配为 UTF-8（`_configure_standard_streams`），
    # 输出含中文；而 `text=True` 在父进程按**区域默认编码**（中文 Windows 上是 GBK）解码，
    # 会在读取线程里抛 UnicodeDecodeError 并破坏捕获结果——测试因此不能依赖环境区域设置。
    return subprocess.run(
        [sys.executable, "-m", "cli", *arguments],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=120,
        cwd=str(REPO_ROOT),
        env=environment,
    )


def _single_json_envelope(stdout: str) -> dict:
    payload = json.loads(stdout)  # 整体可解析 ⇒ 单一包络
    assert isinstance(payload, dict), "包络必须是 JSON 对象"
    return payload


def test_doctor_authentication_failure_exits_three(tmp_path: Path) -> None:
    """核心：`doctor` 用 payload 表达失败 ⇒ 必须转换为退出码 3（修复前为 0）。"""
    with _Stub() as base_url:
        completed = _run_cli(base_url, tmp_path, "doctor")

    payload = _single_json_envelope(completed.stdout)
    # 成功包络把业务数据嵌在 `data` 之下（`_execute` → `{"ok": true, "command": ..., "data": ...}`）。
    checks = ((payload.get("data") or {}).get("checks")) or {}
    assert (checks.get("authentication") or {}).get("ok") is False, payload
    assert completed.returncode == 3, f"期望 EXIT_AUTH_REQUIRED=3，实际 {completed.returncode}"
    assert "Traceback" not in completed.stderr, completed.stderr


def test_usage_error_exits_two_with_single_envelope(tmp_path: Path) -> None:
    """参数错走**错误包络**：`_write_json(error=True)` 把它写到 **stderr**（设计如此）。"""
    with _Stub() as base_url:
        completed = _run_cli(base_url, tmp_path, "doctor", "--definitely-not-an-option")

    payload = _single_json_envelope(completed.stderr)
    assert payload.get("ok") is False, payload
    assert completed.returncode == 2, f"期望 EXIT_USAGE=2，实际 {completed.returncode}"
    assert completed.stdout.strip() == "", "参数错时不应在 stdout 输出包络"
    assert "Traceback" not in completed.stderr, completed.stderr


def test_stdout_is_a_single_envelope_on_success_path(tmp_path: Path) -> None:
    """成功路径也必须只输出一个 JSON 包络（`doctor` 在未登录时不算成功，这里只验包络形态）。"""
    with _Stub() as base_url:
        completed = _run_cli(base_url, tmp_path, "version")

    payload = _single_json_envelope(completed.stdout)
    assert completed.returncode == 0, completed.stderr
    assert payload.get("ok") is True, payload



