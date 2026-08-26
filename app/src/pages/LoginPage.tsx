import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useAuth } from "@/contexts/AuthContext";
import { agentClient } from "@/lib/agent-client";
import { backendClient } from "@/lib/backend-client";
import { buildAllowedPaths, pickFallbackPath } from "@/lib/navigation-access";
import { AGENT_HTTP_ORIGIN, BUILD_ROLE } from "@/config/build-role";
import type { AppError } from "@/types";

function canUseEdgeOffline(error: unknown): boolean {
  const appError = error as AppError;
  return appError?.type === "network_error"
    || (appError?.type === "api_error" && (appError.status ?? 0) >= 500);
}

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
      const { token: agentToken, roles: agentRoles } =
        await agentClient.agentLogin(username, password);

      let backendToken = "";
      let backendRefreshToken = "";
      let mustChangePassword = false;
      let user = {
        userId: 0,
        userName: username,
        roles: agentRoles,
        buttons: [] as string[],
      };
      let allowedPaths = buildAllowedPaths([], agentRoles);

      try {
        const centerAuth = await backendClient.login(username, password);
        backendToken = centerAuth.token;
        backendRefreshToken = centerAuth.refreshToken;
        mustChangePassword = Boolean(centerAuth.mustChangePassword);
        const userInfo = await backendClient.userInfo(backendToken);
        user = userInfo;
        if (typeof userInfo.mustChangePassword === "boolean") {
          mustChangePassword = userInfo.mustChangePassword;
        }
        if (!mustChangePassword) {
          try {
            const menus = await backendClient.currentUserMenus(backendToken);
            allowedPaths = buildAllowedPaths(menus, userInfo.roles);
          } catch (centerProfileError) {
            if ((centerProfileError as AppError)?.type === "unauthorized") throw centerProfileError;
          }
        }
      } catch (centerError) {
        if (BUILD_ROLE !== "edge" || !canUseEdgeOffline(centerError)) throw centerError;
      }

      login({
        agentToken,
        backendToken,
        backendRefreshToken,
        user,
        allowedPaths,
        mustChangePassword,
      });
      navigate(pickFallbackPath(allowedPaths), { replace: true });

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
      className="h-full flex items-center justify-center px-4 bg-background overflow-auto"
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
            {BUILD_ROLE === "edge" ? "边缘节点本机运维" : "本机采集与入库控制台"}
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
            Agent：{AGENT_HTTP_ORIGIN}
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
