import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  ChevronDown,
  ChevronUp,
  CircleAlert,
  CircleCheck,
  LoaderCircle,
  RadioTower,
} from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Field,
  FieldError,
  FieldGroup,
  FieldLabel,
} from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/contexts/AuthContext";
import { agentClient } from "@/lib/agent-client";
import { backendClient } from "@/lib/backend-client";
import { buildAllowedPaths, pickFallbackPath } from "@/lib/navigation-access";
import { AGENT_HTTP_ORIGIN, BUILD_ROLE } from "@/config/build-role";
import type { AppError } from "@/types";

type ConnectionState = "checking" | "ready" | "unavailable";

function canUseEdgeOffline(error: unknown): boolean {
  const appError = error as AppError;
  return appError?.type === "network_error"
    || (appError?.type === "api_error" && (appError.status ?? 0) >= 500);
}

export function LoginPage() {
  const navigate = useNavigate();
  const { login } = useAuth();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [connectionState, setConnectionState] = useState<ConnectionState>("checking");
  const [connectionDetailsOpen, setConnectionDetailsOpen] = useState(false);
  const [connectionCheckVersion, setConnectionCheckVersion] = useState(0);

  const canSubmit = username.trim() !== "" && password !== "";

  useEffect(() => {
    let active = true;
    setConnectionState("checking");
    agentClient.getBootstrapStatus()
      .then(() => {
        if (active) setConnectionState("ready");
      })
      .catch(() => {
        if (active) setConnectionState("unavailable");
      });
    return () => {
      active = false;
    };
  }, [connectionCheckVersion]);

  async function handleLogin(event: React.FormEvent) {
    event.preventDefault();
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
      const appError = err as AppError;
      if (appError?.type === "unauthorized") {
        setError("用户名或密码错误");
      } else if (appError?.type === "agent_unreachable") {
        navigate("/error", { replace: true });
      } else if (appError?.type === "network_error") {
        setError("后端服务不可达，请检查网络");
      } else {
        setError("登录失败，请稍后重试");
      }
    } finally {
      setLoading(false);
    }
  }

  const connectionLabel = connectionState === "ready"
    ? "本机服务已就绪"
    : connectionState === "unavailable"
      ? "本机服务暂不可达"
      : "正在检查本机服务";

  return (
    <div className="flex h-full items-center justify-center overflow-auto bg-muted/20 px-5 py-8">
      <Card className="w-full max-w-[430px] border-border shadow-sm">
        <CardHeader className="space-y-5 px-7 pt-7">
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <span className="flex size-10 items-center justify-center rounded-xl bg-primary text-primary-foreground">
                <RadioTower className="size-5" />
              </span>
              <div>
                <p className="text-sm font-semibold leading-none">Data Watch</p>
                <p className="mt-1 text-xs text-muted-foreground">工业数据平台</p>
              </div>
            </div>
            <Badge variant="outline">{BUILD_ROLE === "edge" ? "边缘节点" : "控制台"}</Badge>
          </div>
          <div>
            <CardTitle className="text-2xl tracking-tight">欢迎回来</CardTitle>
            <CardDescription className="mt-1.5">
              登录后继续管理设备、采集链路与系统运行状态。
            </CardDescription>
          </div>
        </CardHeader>

        <CardContent className="px-7">
          <form onSubmit={handleLogin}>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="login-username">用户名</FieldLabel>
                <Input
                  id="login-username"
                  type="text"
                  autoComplete="username"
                  value={username}
                  onChange={(event) => setUsername(event.target.value)}
                  placeholder="请输入用户名"
                  disabled={loading}
                  autoFocus
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="login-password">密码</FieldLabel>
                <Input
                  id="login-password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  placeholder="请输入密码"
                  disabled={loading}
                />
              </Field>
              <div className="min-h-5">
                <FieldError>{error}</FieldError>
              </div>
              <Button type="submit" className="w-full" disabled={!canSubmit || loading}>
                {loading ? (
                  <>
                    <LoaderCircle className="size-4 animate-spin" />
                    登录中…
                  </>
                ) : "登录"}
              </Button>
            </FieldGroup>
          </form>
        </CardContent>

        <CardFooter className="flex-col items-stretch gap-2 border-t bg-muted/20 px-7 py-4">
          <Button
            type="button"
            variant="ghost"
            className="h-9 justify-between px-2"
            onClick={() => setConnectionDetailsOpen((open) => !open)}
          >
            <span className="flex items-center gap-2 text-xs">
              {connectionState === "checking" ? (
                <LoaderCircle className="size-3.5 animate-spin text-muted-foreground" />
              ) : connectionState === "ready" ? (
                <CircleCheck className="size-3.5 text-success" />
              ) : (
                <CircleAlert className="size-3.5 text-destructive" />
              )}
              {connectionLabel}
            </span>
            {connectionDetailsOpen ? <ChevronUp className="size-3.5" /> : <ChevronDown className="size-3.5" />}
          </Button>
          {connectionDetailsOpen ? (
            <Alert className={connectionState === "unavailable" ? "border-destructive/30 bg-destructive/5" : ""}>
              <AlertDescription className="flex items-center justify-between gap-3">
                <span className="min-w-0 truncate text-xs">Agent：{AGENT_HTTP_ORIGIN}</span>
                {connectionState === "unavailable" ? (
                  <Button type="button" variant="outline" size="sm" onClick={() => setConnectionCheckVersion((version) => version + 1)}>
                    重试
                  </Button>
                ) : null}
              </AlertDescription>
            </Alert>
          ) : null}
        </CardFooter>
      </Card>
    </div>
  );
}
