import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  ChevronDown,
  ChevronUp,
  CircleAlert,
  CircleCheck,
  Eye,
  EyeOff,
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
import { ThemeToggle } from "@/components/ThemeToggle";
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
  const [showPassword, setShowPassword] = useState(false);

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
    <div className="relative flex h-full items-center justify-center overflow-auto bg-background px-5 py-8">
      <div
        aria-hidden
        className="pointer-events-none absolute left-1/2 top-[-30%] h-[700px] w-[1100px] -translate-x-1/2"
        style={{ background: "radial-gradient(closest-side, color-mix(in oklab, var(--brand) 16%, transparent), transparent)" }}
      />
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0"
        style={{
          backgroundImage: "radial-gradient(color-mix(in oklab, var(--brand) 22%, transparent) 1px, transparent 1.2px)",
          backgroundSize: "22px 22px",
          maskImage: "radial-gradient(ellipse 70% 60% at 50% 42%, #000 20%, transparent 75%)",
          WebkitMaskImage: "radial-gradient(ellipse 70% 60% at 50% 42%, #000 20%, transparent 75%)",
        }}
      />
      <div className="absolute right-4 top-4 z-10">
        <ThemeToggle />
      </div>

      <Card className="relative z-10 w-full max-w-[430px] gap-0 rounded-2xl py-0">
        <CardHeader className="gap-5 px-7 pt-7">
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <span className="flex size-10 items-center justify-center rounded-xl bg-brand text-brand-foreground shadow-[0_6px_16px_-6px_var(--brand)]">
                <RadioTower className="size-5" />
              </span>
              <div>
                <p className="text-sm font-semibold leading-none">Data Watch</p>
                <p className="mt-[5px] text-xs text-muted-foreground">工业数据平台</p>
              </div>
            </div>
            <Badge variant="outline" className="h-5 rounded-md bg-transparent px-2 text-xs font-medium">
              {BUILD_ROLE === "edge" ? "边缘节点" : "控制台"}
            </Badge>
          </div>
          <div>
            <CardTitle className="text-2xl font-semibold leading-tight tracking-[-0.02em]">欢迎回来</CardTitle>
            <CardDescription className="mt-1.5 text-sm">
              登录后继续管理设备、采集链路与系统运行状态。
            </CardDescription>
          </div>
        </CardHeader>

        <CardContent className="px-7 pb-6 pt-6">
          <form onSubmit={handleLogin}>
            <FieldGroup className="gap-4">
              <Field className="gap-2">
                <FieldLabel htmlFor="login-username" className="text-sm">用户名</FieldLabel>
                <Input
                  id="login-username"
                  type="text"
                  autoComplete="username"
                  value={username}
                  onChange={(event) => setUsername(event.target.value)}
                  placeholder="请输入用户名"
                  disabled={loading}
                  className="h-10 px-3 text-sm md:text-sm"
                  autoFocus
                />
              </Field>
              <Field className="gap-2">
                <FieldLabel htmlFor="login-password" className="text-sm">密码</FieldLabel>
                <div className="relative">
                  <Input
                    id="login-password"
                    type={showPassword ? "text" : "password"}
                    autoComplete="current-password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    placeholder="请输入密码"
                    disabled={loading}
                    className="h-10 px-3 pr-10 text-sm md:text-sm"
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="absolute right-1 top-1 size-8 text-muted-foreground hover:text-foreground"
                    onClick={() => setShowPassword((value) => !value)}
                    aria-label={showPassword ? "隐藏密码" : "显示密码"}
                    title={showPassword ? "隐藏密码" : "显示密码"}
                  >
                    {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                  </Button>
                </div>
              </Field>
              <div className="-my-1.5 flex min-h-5 items-center">
                {error ? (
                  <FieldError className="flex items-center gap-1.5 text-[13px]">
                    <CircleAlert className="size-3.5" />
                    {error}
                  </FieldError>
                ) : null}
              </div>
              <Button type="submit" className="h-10 w-full bg-brand text-sm text-brand-foreground hover:bg-brand/90" disabled={!canSubmit || loading}>
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

        <CardFooter className="flex-col items-stretch gap-1.5 border-t bg-muted/35 px-5 pb-3 pt-2.5">
          <Button
            type="button"
            variant="ghost"
            className="h-9 justify-between px-2 font-normal"
            onClick={() => setConnectionDetailsOpen((open) => !open)}
            aria-expanded={connectionDetailsOpen}
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
            {connectionDetailsOpen ? <ChevronUp className="size-3.5 text-muted-foreground" /> : <ChevronDown className="size-3.5 text-muted-foreground" />}
          </Button>
          {connectionDetailsOpen ? (
            <Alert className={connectionState === "unavailable" ? "border-destructive/30 bg-destructive/5 px-2.5 py-2" : "border-0 bg-card px-2.5 py-2 shadow-card"}>
              <AlertDescription className="flex items-center justify-between gap-3">
                <span className="flex min-w-0 items-center gap-2 text-xs">
                  <span
                    className={[
                      "size-1.5 shrink-0 rounded-full",
                      connectionState === "ready" ? "bg-success" : connectionState === "unavailable" ? "bg-destructive" : "bg-muted-foreground",
                    ].join(" ")}
                  />
                  <span className="truncate font-mono">Agent：{AGENT_HTTP_ORIGIN}</span>
                </span>
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

      <p className="pointer-events-none absolute inset-x-0 bottom-4 z-10 text-center text-xs text-muted-foreground">
        Data Watch · 集装箱码头设备数据采集与监控
      </p>
    </div>
  );
}
