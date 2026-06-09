import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useAuth } from "@/contexts/AuthContext";
import { agentClient } from "@/lib/agent-client";
import { backendClient } from "@/lib/backend-client";
import type { AppError } from "@/types";

// Agent 地址为本机固定值，不可配置（spec §1.2）
const AGENT_ADDRESS = "http://127.0.0.1:7070";

export function LoginPage() {
  const navigate       = useNavigate();
  const { login }      = useAuth();

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [loading,  setLoading]  = useState(false);
  const [error,    setError]    = useState<string | null>(null);

  const canSubmit = username.trim() !== "" && password !== "";

  // ── 登录流程：agentLogin → backendLogin → AuthContext.login → navigate('/') ─

  async function handleLogin(e: React.FormEvent) {
    e.preventDefault();
    if (!canSubmit || loading) return;

    setLoading(true);
    setError(null);

    try {
      // Step 1：Agent 登录，获取 agentToken
      // 密码不经 console（agent-client 内部 sanitize 仅 debug 脱敏 body）
      const { token: agentToken } =
        await agentClient.agentLogin(username, password);

      // Step 2：Backend 登录，获取 backendToken / refreshToken
      const { token: backendToken, refreshToken: backendRefreshToken } =
        await backendClient.login(username, password);

      // Step 3：写入 AuthContext（仅内存，不持久化）
      login({ agentToken, backendToken, backendRefreshToken });

      // Step 4：跳转总览页
      navigate("/", { replace: true });

    } catch (err) {
      const e = err as AppError;
      if (e?.type === "unauthorized") {
        setError("用户名或密码错误");
      } else if (e?.type === "agent_unreachable") {
        navigate("/error", { replace: true });
      } else if (e?.type === "network_error") {
        setError("后端服务不可达，请检查网络");
      } else {
        setError("登录失败，请稍后重试");
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    // 全屏居中，无任何主导航（FR-015）
    <div
      className="min-h-full flex items-center justify-center px-4 bg-background"
    >
      <Card
        className="w-full max-w-sm"
        style={{
          background: "var(--card)",
          border: "1px solid var(--border)",
          borderRadius: 12,
        }}
      >
        <CardHeader className="pb-2 pt-6 px-6">
          <CardTitle className="text-xl font-bold text-foreground">
            Data Watch Console
          </CardTitle>
          <p className="mt-0.5 text-sm text-muted-foreground">
            本机采集与入库控制台
          </p>
        </CardHeader>

        <CardContent className="px-6 pb-6">
          <form onSubmit={handleLogin} className="space-y-4 mt-2">
            {/* 用户名 */}
            <div className="space-y-1.5">
              <Label htmlFor="login-username" className="text-sm text-muted-foreground">
                用户名
              </Label>
              <Input
                id="login-username"
                type="text"
                autoComplete="username"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                placeholder="admin"
                disabled={loading}
              />
            </div>

            {/* 密码 — 始终掩码，不提供明文切换（spec FR-007 / US2-AC4） */}
            <div className="space-y-1.5">
              <Label htmlFor="login-password" className="text-sm text-muted-foreground">
                密码
              </Label>
              <Input
                id="login-password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••"
                disabled={loading}
              />
            </div>

            {/* 错误提示：预留高度避免跳动；仅有错误时渲染文字 */}
            <p
              className="text-sm text-destructive min-h-[1.25rem]"
              role="alert"
            >
              {error ?? ""}
            </p>

            {/* 登录按钮：用户名或密码为空时 disabled */}
            <Button
              type="submit"
              className="w-full"
              disabled={!canSubmit || loading}
              style={
                canSubmit && !loading
                  ? { background: "var(--brand)", color: "var(--brand-foreground)" }
                  : undefined
              }
            >
              {loading ? "登录中…" : "登录"}
            </Button>
          </form>

          {/* Agent 地址提示 */}
          <p className="mt-5 text-center text-xs text-muted-foreground">
            Agent：{AGENT_ADDRESS}
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
