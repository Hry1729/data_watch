"""B18 回归：登录/登出限流与日志的来源 IP 解析。

修复前的两个问题：
1. 路由把 ``x_forwarded_for`` / ``x_real_ip`` 声明为**裸参数**，在 FastAPI/TurboAPI 中会从
   **query string** 绑定 —— 客户端可用 ``?x_forwarded_for=...`` 自设来源 IP，而真正的
   请求头被忽略。
2. 解析实现直接取 ``X-Forwarded-For`` 的**最左项**；nginx 的 ``$proxy_add_x_forwarded_for``
   会追加对端，客户端自带的伪造项留在最左，因此最左项不可信。

修复后：来源 IP 以连接对端为准；``AUTH_TRUSTED_PROXIES`` 为空时完全忽略转发头；
仅在可信代理链上才从**右往左**跳过可信跳取第一个不可信地址。
"""

from __future__ import annotations

import inspect
from types import SimpleNamespace

import pytest

from app.api.routes import auth as auth_routes


class _Request:
    """最小请求替身：解析器只使用 .client.host 与 .headers.get。"""

    def __init__(self, peer: str | None, headers: dict[str, str] | None = None) -> None:
        self.client = SimpleNamespace(host=peer) if peer is not None else None
        self.headers = headers or {}


def _use_proxies(monkeypatch, value: str) -> None:
    monkeypatch.setattr(
        auth_routes,
        "get_settings",
        lambda: SimpleNamespace(auth_trusted_proxies=value),
    )


def test_query_parameters_cannot_impersonate_forwarding_headers() -> None:
    """路由签名里不得再存在同名参数（否则会被当作 query 参数绑定）。"""
    for route in (auth_routes.login, auth_routes.logout):
        parameters = inspect.signature(route).parameters
        assert "x_forwarded_for" not in parameters
        assert "x_real_ip" not in parameters
    assert "request" in inspect.signature(auth_routes.login).parameters


def test_forwarding_headers_ignored_when_no_trusted_proxy(monkeypatch) -> None:
    """默认（未配置可信代理）来源即连接对端，转发头无法伪造。"""
    _use_proxies(monkeypatch, "")
    request = _Request(
        "203.0.113.7",
        {"X-Forwarded-For": "1.2.3.4", "X-Real-IP": "5.6.7.8"},
    )
    assert auth_routes._resolve_client_ip(request) == "203.0.113.7"


def test_untrusted_peer_cannot_spoof_via_headers(monkeypatch) -> None:
    """对端不在可信网段时，即使配置了可信代理也不采信它带来的转发头。"""
    _use_proxies(monkeypatch, "10.0.0.0/8")
    request = _Request("203.0.113.7", {"X-Forwarded-For": "1.2.3.4"})
    assert auth_routes._resolve_client_ip(request) == "203.0.113.7"


def test_trusted_proxy_uses_rightmost_untrusted_hop(monkeypatch) -> None:
    """可信代理链上从右往左取第一个不可信地址，而非最左项。"""
    _use_proxies(monkeypatch, "10.0.0.0/8")
    # 客户端伪造了最左的 1.2.3.4；真实链路是 client(198.51.100.9) -> proxy(10.0.0.5)
    request = _Request(
        "10.0.0.5",
        {"X-Forwarded-For": "1.2.3.4, 198.51.100.9, 10.0.0.5"},
    )
    assert auth_routes._resolve_client_ip(request) == "198.51.100.9"


def test_multiple_trusted_hops_are_skipped(monkeypatch) -> None:
    _use_proxies(monkeypatch, "10.0.0.0/8, 192.168.0.0/16")
    request = _Request(
        "10.0.0.5",
        {"X-Forwarded-For": "198.51.100.9, 192.168.1.2, 10.0.0.5"},
    )
    assert auth_routes._resolve_client_ip(request) == "198.51.100.9"


def test_falls_back_to_real_ip_then_peer(monkeypatch) -> None:
    _use_proxies(monkeypatch, "10.0.0.0/8")
    # XFF 全为可信跳 ⇒ 看 X-Real-IP
    request = _Request("10.0.0.5", {"X-Forwarded-For": "10.0.0.4", "X-Real-IP": "198.51.100.9"})
    assert auth_routes._resolve_client_ip(request) == "198.51.100.9"
    # 两个头都不可用 ⇒ 回落到连接对端
    request = _Request("10.0.0.5", {"X-Forwarded-For": "10.0.0.4"})
    assert auth_routes._resolve_client_ip(request) == "10.0.0.5"


def test_missing_peer_is_reported_as_unknown(monkeypatch) -> None:
    _use_proxies(monkeypatch, "")
    assert auth_routes._resolve_client_ip(_Request(None)) == "0.0.0.0"


def test_invalid_trusted_proxy_config_does_not_widen_trust(monkeypatch) -> None:
    """配置写错时按"不信任"处理，不得静默放宽。"""
    _use_proxies(monkeypatch, "not-a-network, 10.0.0.0/8")
    request = _Request("203.0.113.7", {"X-Forwarded-For": "1.2.3.4"})
    assert auth_routes._resolve_client_ip(request) == "203.0.113.7"


def test_ipv6_trusted_proxy(monkeypatch) -> None:
    _use_proxies(monkeypatch, "::1/128")
    request = _Request("::1", {"X-Forwarded-For": "2001:db8::9, ::1"})
    assert auth_routes._resolve_client_ip(request) == "2001:db8::9"


def test_garbage_forwarded_header_is_ignored(monkeypatch) -> None:
    _use_proxies(monkeypatch, "10.0.0.0/8")
    request = _Request("10.0.0.5", {"X-Forwarded-For": "not-an-ip"})
    assert auth_routes._resolve_client_ip(request) == "10.0.0.5"


@pytest.mark.parametrize("header", ["X-Forwarded-For", "X-Real-IP"])
def test_single_header_alone_cannot_set_source(monkeypatch, header: str) -> None:
    _use_proxies(monkeypatch, "")
    request = _Request("203.0.113.7", {header: "1.2.3.4"})
    assert auth_routes._resolve_client_ip(request) == "203.0.113.7"
