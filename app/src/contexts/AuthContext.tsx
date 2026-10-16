import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from "react";
import type { ReactNode } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { setAgentAuthToken } from "@/lib/agent-auth-token";
import { clearBackendSession, setBackendSession } from "@/lib/backend-session";
import { canAccessPath, pickFallbackPath } from "@/lib/navigation-access";
import type { AuthStore } from "@/types";

// ─── Context 类型 ────────────────────────────────────────────────────────────

interface AuthContextValue {
  /** null = 未登录 */
  auth: AuthStore | null;
  /** 派生值：auth !== null */
  isAuthenticated: boolean;
  /** 写入内存 token，不持久化 */
  login(store: AuthStore): void;
  /** 清空 token，跳转 /login；幂等，可重复调用 */
  logout(): void;
  /** 完成一次性密码更换后解除强制改密状态。 */
  clearMustChangePassword(): void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

// ─── 提供者 Provider ────────────────────────────────────────────────────────────

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthStore | null>(null);
  const navigate = useNavigate();
  const navigateRef = useRef(navigate);
  navigateRef.current = navigate;
  useEffect(() => () => clearBackendSession(), []);
  /**
   * 防止并发 401 触发多次跳转（spec plan §5.3）。
   * login() 时重置为 false，使新一轮 logout 可以正常工作。
   */
  const isLoggingOutRef = useRef(false);

  function login(store: AuthStore) {
    isLoggingOutRef.current = false; // 重置，以便下次 logout 可触发
    setAgentAuthToken(store.agentToken);
    setBackendSession({ token: store.backendToken, refreshToken: store.backendRefreshToken }, (tokens) => {
      setAuth((current) => current ? {
        ...current,
        backendToken: tokens.token,
        backendRefreshToken: tokens.refreshToken,
        mustChangePassword: tokens.mustChangePassword ?? current.mustChangePassword,
      } : current);
    });
    setAuth(store);
  }

  const logout = useCallback(() => {
    // 幂等：已在 logout 中则直接返回
    if (isLoggingOutRef.current) return;
    isLoggingOutRef.current = true;
    setAgentAuthToken(null);
    clearBackendSession();
    setAuth(null);
    navigateRef.current("/login", { replace: true });
  }, []);

  function clearMustChangePassword() {
    setAuth((current) => current ? { ...current, mustChangePassword: false } : current);
  }

  return (
    <AuthContext.Provider
      value={{ auth, isAuthenticated: auth !== null, login, logout, clearMustChangePassword }}
    >
      {children}
    </AuthContext.Provider>
  );
}

// ─── 钩子 Hook ─────────────────────────────────────────────────────────────────

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth 必须在 AuthProvider 内使用");
  }
  return context;
}

// ─── 路由守卫 ────────────────────────────────────────────────────────────────

/**
 * AuthGuard — 包裹需要登录的路由。
 * 未登录时跳转 /login；已登录时渲染 children。
 */
export function AuthGuard({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}

/**
 * MenuAccessGuard — 按角色菜单过滤路由。
 * 无权限 path 时跳转到第一个可访问页（桌面专有页始终可进）。
 */
export function MenuAccessGuard({ children }: { children: ReactNode }) {
  const { auth } = useAuth();
  const location = useLocation();
  if (auth?.mustChangePassword && location.pathname !== "/force-password-change") {
    return <Navigate to="/force-password-change" state={{ from: location.pathname }} replace />;
  }
  if (!canAccessPath(location.pathname, auth?.allowedPaths, auth?.user?.roles)) {
    return <Navigate to={pickFallbackPath(auth?.allowedPaths)} replace />;
  }
  return <>{children}</>;
}

/**
 * PublicOnlyRoute — 包裹 /login 路由。
 * 已登录时跳转有权限首页；未登录时渲染 children。
 */
export function PublicOnlyRoute({ children }: { children: ReactNode }) {
  const { auth, isAuthenticated } = useAuth();
  if (isAuthenticated) {
    return <Navigate to={auth?.mustChangePassword ? "/force-password-change" : pickFallbackPath(auth?.allowedPaths)} replace />;
  }
  return <>{children}</>;
}
