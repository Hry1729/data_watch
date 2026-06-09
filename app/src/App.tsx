import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { BrowserRouter, Navigate, Route, Routes, useNavigate } from "react-router-dom";
import { AppLayout } from "@/components/AppLayout";
import {
  AuthGuard,
  AuthProvider,
  PublicOnlyRoute,
} from "@/contexts/AuthContext";
import { Button } from "@/components/ui/button";
import { BootstrapPage } from "@/pages/BootstrapPage";
import { LoginPage } from "@/pages/LoginPage";
import { OverviewPage } from "@/pages/OverviewPage";
import { CollectorPage } from "@/pages/CollectorPage";
import { WriterPage } from "@/pages/WriterPage";
import { SettingsPage } from "@/pages/SettingsPage";
import { agentClient } from "@/lib/agent-client";
import { Toaster } from "@/components/ui/sonner";
import { TitleBar } from "@/components/TitleBar";

// ─── 启动检查（Bootstrap 状态 + Agent 连通性）────────────────────────────────
//
// 渲染流程：
//   挂载 → loading 屏 → getBootstrapStatus()
//     ├── in_bootstrap: true  → navigate('/bootstrap')
//     ├── in_bootstrap: false → setReady(true)，由 AuthGuard 跳 /login
//     └── agent_unreachable   → navigate('/error')

function AppStartup({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const [ready, setReady] = useState(false);

  useEffect(() => {
    agentClient
      .getBootstrapStatus()
      .then(({ in_bootstrap }) => {
        if (in_bootstrap) navigate("/bootstrap", { replace: true });
        // else: Routes 渲染后 AuthGuard 负责跳 /login
      })
      .catch(() => {
        navigate("/error", { replace: true });
      })
      .finally(() => setReady(true));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  if (!ready) {
    return (
      <div
        className="min-h-full flex items-center justify-center bg-background"
      >
        <p className="text-sm text-muted-foreground/70">正在连接 Agent…</p>
      </div>
    );
  }

  return <>{children}</>;
}

// ─── Agent 不可达错误页 ───────────────────────────────────────────────────────

function AgentErrorPage() {
  const navigate = useNavigate();
  const [retrying, setRetrying] = useState(false);

  function handleRetry() {
    setRetrying(true);
    agentClient
      .getBootstrapStatus()
      .then(({ in_bootstrap }) => {
        if (in_bootstrap) navigate("/bootstrap", { replace: true });
        else navigate("/login", { replace: true });
      })
      .catch(() => {
        // 仍不可达：停留错误页，按钮恢复可点
        setRetrying(false);
      })
      .finally(() => setRetrying(false));
  }

  return (
    <div
      className="min-h-full flex flex-col items-center justify-center gap-6 bg-background"
    >
      <div className="text-center">
        <p className="text-lg font-semibold text-foreground">无法连接到 Agent</p>
        <p className="mt-1 font-mono text-sm text-muted-foreground">127.0.0.1:7070</p>
        <p className="mt-2 text-xs text-muted-foreground/70">
          请确认 Data Watch Agent 服务已启动
        </p>
      </div>
      <Button
        onClick={handleRetry}
        disabled={retrying}
        className="bg-brand text-brand-foreground"
      >
        {retrying ? "重试中…" : "重试"}
      </Button>
    </div>
  );
}

// ─── 根组件 ───────────────────────────────────────────────────────────────────

export default function App() {
  return (
    <>
      <div className="flex h-screen flex-col overflow-hidden bg-background">
        <TitleBar />
        <div className="min-h-0 flex-1 overflow-y-auto">
          <BrowserRouter>
            {/* AuthProvider 必须在 BrowserRouter 内，因为内部使用 useNavigate */}
            <AuthProvider>
              {/* AppStartup：启动检查，期间显示 loading 屏 */}
              <AppStartup>
                <Routes>
                  {/* 公开路由：无 AppLayout，无 AuthGuard */}
                  <Route path="/bootstrap" element={<BootstrapPage />} />
                  <Route
                    path="/login"
                    element={
                      <PublicOnlyRoute>
                        <LoginPage />
                      </PublicOnlyRoute>
                    }
                  />
                  <Route path="/error" element={<AgentErrorPage />} />

                  {/* 受保护路由：未登录时 AuthGuard 跳 /login */}
                  <Route
                    element={
                      <AuthGuard>
                        <AppLayout />
                      </AuthGuard>
                    }
                  >
                    <Route index element={<OverviewPage />} />
                    <Route path="/collector" element={<CollectorPage />} />
                    <Route path="/writer" element={<WriterPage />} />
                    <Route path="/settings" element={<SettingsPage />} />
                  </Route>

                  {/* 未匹配路由跳登录页 */}
                  <Route path="*" element={<Navigate to="/login" replace />} />
                </Routes>
              </AppStartup>
            </AuthProvider>
          </BrowserRouter>
        </div>
      </div>
      <Toaster richColors position="top-right" />
    </>
  );
}
