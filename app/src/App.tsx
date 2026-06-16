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
import { TitleBar } from "@/components/TitleBar";
import { BootstrapPage } from "@/pages/BootstrapPage";
import { LoginPage } from "@/pages/LoginPage";
import { OverviewPage } from "@/pages/OverviewPage";
import { CollectorPage } from "@/pages/CollectorPage";
import { WriterPage } from "@/pages/WriterPage";
import { SettingsPage } from "@/pages/SettingsPage";
import { agentClient } from "@/lib/agent-client";
import { setBackendBaseUrl } from "@/lib/backend-client";
import { Toaster } from "@/components/ui/sonner";

// ─── 启动检查（Bootstrap 状态 + Agent 连通性）────────────────────────────────
//
// 渲染流程：
//   挂载 → loading 屏 → getBootstrapStatus()
//     ├── in_bootstrap: true  → navigate('/bootstrap')
//     ├── in_bootstrap: false → setReady(true)，由 AuthGuard 跳 /login
//     └── agent_unreachable   → navigate('/error')

function AppStartup({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const [deciding, setDeciding] = useState(true);

  useEffect(() => {
    agentClient
      .getBootstrapStatus()
      .then(async ({ in_bootstrap }) => {
        // 运行期后端地址：从 Agent 读取并注入 backend-client（替代打包写死的 VITE_BACKEND_URL）。
        // 拉取失败不阻断启动，登录时会再暴露问题。
        try {
          const { backend_base_url } = await agentClient.getRuntimeConfig();
          setBackendBaseUrl(backend_base_url);
        } catch {
          // ignore：后端地址不可得时由登录流程报错
        }
        if (in_bootstrap) navigate("/bootstrap", { replace: true });
        // else: 由 AuthGuard 跳 /login
      })
      .catch(() => {
        navigate("/error", { replace: true });
      })
      .finally(() => setDeciding(false));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // 始终渲染 children(<Routes>)，确保上面的 navigate 触发时路由已挂载。
  // 旧实现在 loading 阶段隐藏 Routes 再 navigate，打包后的 webview 里该命令式跳转会丢失，
  // 导致停在默认路由被 AuthGuard 跳到 /login（表现为新装机直接进登录页而非引导页）。
  // 改为始终挂载 Routes、加载期间盖一层全屏遮罩。
  return (
    <>
      {children}
      {deciding && (
        // 遮罩从标题栏下方(top-8)开始，保证加载期间仍可拖动/最小化/关闭窗口
        <div className="fixed left-0 right-0 bottom-0 top-8 z-40 flex items-center justify-center bg-background">
          <p className="text-sm text-muted-foreground/70">正在连接 Agent…</p>
        </div>
      )}
    </>
  );
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
      className="h-full flex flex-col items-center justify-center gap-6 bg-background"
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
    // 全局壳：顶部自定义标题栏(所有页面通用) + 下方内容区
    <div className="flex flex-col h-screen overflow-hidden bg-background">
      <TitleBar />
      <div className="relative flex-1 min-h-0">
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
              <Route index             element={<OverviewPage />} />
              <Route path="/collector" element={<CollectorPage />} />
              <Route path="/writer"    element={<WriterPage />} />
              <Route path="/settings"  element={<SettingsPage />} />
            </Route>

            {/* 未匹配路由跳登录页 */}
            <Route path="*" element={<Navigate to="/login" replace />} />
          </Routes>
        </AppStartup>
      </AuthProvider>
    </BrowserRouter>
      </div>
      <Toaster richColors position="top-right" />
    </div>
  );
}
