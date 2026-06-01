import {
  createContext,
  useContext,
  useRef,
  useState,
} from "react";
import type { ReactNode } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { setAgentAuthToken } from "@/lib/agent-client";
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
}

const AuthContext = createContext<AuthContextValue>(null!);

// ─── Provider ────────────────────────────────────────────────────────────────

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthStore | null>(null);
  const navigate = useNavigate();
  /**
   * 防止并发 401 触发多次跳转（spec plan §5.3）。
   * login() 时重置为 false，使新一轮 logout 可以正常工作。
   */
  const isLoggingOutRef = useRef(false);

  function login(store: AuthStore) {
    isLoggingOutRef.current = false; // 重置，以便下次 logout 可触发
    setAgentAuthToken(store.agentToken);
    setAuth(store);
  }

  function logout() {
    // 幂等：已在 logout 中则直接返回
    if (isLoggingOutRef.current) return;
    isLoggingOutRef.current = true;
    setAgentAuthToken(null);
    setAuth(null);
    navigate("/login", { replace: true });
  }

  return (
    <AuthContext.Provider
      value={{ auth, isAuthenticated: auth !== null, login, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
}

// ─── Hook ─────────────────────────────────────────────────────────────────────

export function useAuth() {
  return useContext(AuthContext);
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
 * PublicOnlyRoute — 包裹 /login 路由。
 * 已登录时跳转 /（避免重复进入登录页）；未登录时渲染 children。
 */
export function PublicOnlyRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  if (isAuthenticated) {
    return <Navigate to="/" replace />;
  }
  return <>{children}</>;
}
