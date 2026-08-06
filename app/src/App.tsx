import { Component, lazy, Suspense, useEffect, useState } from "react";
import type { ErrorInfo, ReactNode } from "react";
import { BrowserRouter, Navigate, Route, Routes, useNavigate } from "react-router-dom";
import { AppLayout } from "@/components/AppLayout";
import {
  AuthGuard,
  AuthProvider,
  MenuAccessGuard,
  PublicOnlyRoute,
} from "@/contexts/AuthContext";
import { Button } from "@/components/ui/button";
import { TitleBar } from "@/components/TitleBar";

import { LoginPage } from "@/pages/LoginPage";

import { PlaceholderPage } from "@/pages/PlaceholderPage";
import { agentClient } from "@/lib/agent-client";
import { AGENT_PORT, BUILD_ROLE } from "@/config/build-role";
import { setBackendBaseUrl } from "@/lib/backend-client";
import { Toaster } from "@/components/ui/sonner";

import { legacyRedirects, placeholderPages } from "@/config/navigation";
import { PageRefreshProvider } from "@/contexts/PageRefreshContext";

/** 捕获懒加载页/布局渲染异常，避免整页白屏无提示 */
class RouteErrorBoundary extends Component<
  { children: ReactNode; onReturn: () => void },
  { error: Error | null }
> {
  state: { error: Error | null } = { error: null };

  static getDerivedStateFromError(error: Error) {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("路由渲染错误", error, info.componentStack);
  }

  render() {
    if (this.state.error) {
      return (
        <div className="flex h-full flex-col items-center justify-center gap-3 bg-background p-6">
          <p className="text-lg font-semibold text-foreground">页面渲染失败</p>
          <p className="max-w-lg text-center text-sm text-muted-foreground break-all">
            {this.state.error.message}
          </p>
          <Button
            onClick={() => {
              this.setState({ error: null });
              this.props.onReturn();
            }}
          >
            返回工作台
          </Button>
        </div>
      );
    }
    return this.props.children;
  }
}

function NavigableRouteErrorBoundary({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  return (
    <RouteErrorBoundary onReturn={() => navigate("/dashboard/workbench", { replace: true })}>
      {children}
    </RouteErrorBoundary>
  );
}

const CollectorPage = __DATAWATCH_ROLE__ === "centralized"
  ? lazy(() => import("@/pages/CollectorPage").then((module) => ({
      default: module.CollectorPage,
    })))
  : null;
const IngestPage = __DATAWATCH_ROLE__ === "distributed-center"
  ? lazy(() => import("@/pages/IngestPage").then((module) => ({
      default: module.IngestPage,
    })))
  : null;
const WriterPage = __DATAWATCH_ROLE__ !== "edge"
  ? lazy(() => import("@/pages/WriterPage").then((module) => ({
      default: module.WriterPage,
    })))
  : null;
const EdgeConsolePage = __DATAWATCH_ROLE__ === "edge"
  ? lazy(() => import("@/pages/EdgeConsolePage"))
  : null;
const EdgeOperationsPage = __DATAWATCH_ROLE__ === "edge"
  ? lazy(() => import("@/pages/EdgeOperationsPage"))
  : null;
const EdgeCollectorPage = __DATAWATCH_ROLE__ === "edge"
  ? lazy(() => import("@/pages/EdgeModulePages").then((module) => ({ default: module.EdgeCollectorPage })))
  : null;
const EdgeUploaderPage = __DATAWATCH_ROLE__ === "edge"
  ? lazy(() => import("@/pages/EdgeModulePages").then((module) => ({ default: module.EdgeUploaderPage })))
  : null;
const OverviewPage = __DATAWATCH_ROLE__ !== "edge"
  ? lazy(() => import("@/pages/OverviewPage").then((module) => ({ default: module.OverviewPage })))
  : null;
const SettingsPage = __DATAWATCH_ROLE__ === "edge"
  ? lazy(() => import("@/pages/EdgeSettingsPage"))
  : lazy(() => import("@/pages/SettingsPage").then((module) => ({ default: module.SettingsPage })));
const WorkbenchPage = __DATAWATCH_ROLE__ !== "edge" ? lazy(() => import("@/pages/WorkbenchPage")) : null;
const DeviceCommunicationPage = __DATAWATCH_ROLE__ !== "edge" ? lazy(() => import("@/pages/DeviceCommunicationPage")) : null;
const DriverManagementPage = __DATAWATCH_ROLE__ !== "edge" ? lazy(() => import("@/pages/DriverManagementPage")) : null;
const DeviceManagementPage = __DATAWATCH_ROLE__ !== "edge" ? lazy(() => import("@/pages/DeviceManagementPage")) : null;
const PointManagementPage = __DATAWATCH_ROLE__ !== "edge" ? lazy(() => import("@/pages/PointManagementPage")) : null;
const EdgePointMonitorPage = __DATAWATCH_ROLE__ === "edge" ? lazy(() => import("@/pages/EdgePointMonitorPage")) : null;
const RealtimeMonitorPage = lazy(() => import("@/pages/RealtimeMonitorPage"));
const HistoryDataPage = lazy(() => import("@/pages/HistoryDataPage"));
const SystemLogPage = __DATAWATCH_ROLE__ !== "edge" ? lazy(() => import("@/pages/SystemLogPage")) : null;
const UserManagementPage = __DATAWATCH_ROLE__ !== "edge"
  ? lazy(() => import("@/pages/UserManagementPage"))
  : null;
const RoleManagementPage = __DATAWATCH_ROLE__ !== "edge"
  ? lazy(() => import("@/pages/RoleManagementPage"))
  : null;
const MenuManagementPage = __DATAWATCH_ROLE__ !== "edge"
  ? lazy(() => import("@/pages/MenuManagementPage"))
  : null;

const DistributedNodesPage = __DATAWATCH_ROLE__ === "distributed-center"
  ? lazy(() => import("@/pages/DistributedNodesPage"))
  : null;
const BootstrapRoutePage = __DATAWATCH_ROLE__ === "edge"
  ? lazy(() => import("@/pages/EdgeBootstrapPage"))
  : lazy(() => import("@/pages/BootstrapPage").then((module) => ({
      default: module.BootstrapPage,
    })));

const STARTUP_TIMEOUT_MS = 6_000;

// ─── 启动检查（Bootstrap 状态 + Agent 连通性）────────────────────────────────
//
// 渲染流程：
//   挂载 → loading 屏 → getBootstrapStatus()
//     ├── in_bootstrap: true  → 跳转到 navigate('/bootstrap')
//     ├── in_bootstrap: false → setReady(true)，由 AuthGuard 跳 /login
//     └── agent_unreachable   → 跳转到 navigate('/error')

function withStartupTimeout<T>(request: Promise<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    const timeoutId = window.setTimeout(
      () => reject(new Error("Agent request timed out")),
      STARTUP_TIMEOUT_MS,
    );
    request.then(resolve, reject).finally(() => window.clearTimeout(timeoutId));
  });
}
function AppStartup({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const [deciding, setDeciding] = useState(true);

  useEffect(() => {
    withStartupTimeout(agentClient.getBootstrapStatus())
      .then(async ({ in_bootstrap }) => {
        // 运行期后端地址：从 Agent 读取并注入 backend-client（替代打包写死的 VITE_BACKEND_URL）。
        // 拉取失败不阻断启动，登录时会再暴露问题。
        try {
          const { backend_base_url } = await withStartupTimeout(agentClient.getRuntimeConfig());
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
    withStartupTimeout(agentClient.getBootstrapStatus())
      .then(async ({ in_bootstrap }) => {
        const { backend_base_url } = await withStartupTimeout(agentClient.getRuntimeConfig());
        setBackendBaseUrl(backend_base_url);
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
        <p className="mt-1 font-mono text-sm text-muted-foreground">127.0.0.1:{AGENT_PORT}</p>
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
          <NavigableRouteErrorBoundary>
          <Suspense fallback={<div className="flex h-full items-center justify-center text-sm text-muted-foreground">正在加载页面…</div>}>
          <Routes>
            {/* 公开路由：无 AppLayout，无 AuthGuard */}
            <Route path="/bootstrap" element={<BootstrapRoutePage />} />
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
                  <MenuAccessGuard>
                    <PageRefreshProvider>
                      <AppLayout />
                    </PageRefreshProvider>
                  </MenuAccessGuard>
                </AuthGuard>
              }
            >
              {EdgeConsolePage && (
                <Route path="/edge/console" element={<EdgeConsolePage />} />
              )}
              {EdgeOperationsPage && (
                <Route path="/edge/operations" element={<EdgeOperationsPage />} />
              )}
              {EdgeCollectorPage && (
                <Route path="/edge/collector" element={<EdgeCollectorPage />} />
              )}
              {EdgeUploaderPage && (
                <Route path="/edge/uploader" element={<EdgeUploaderPage />} />
              )}
              {WorkbenchPage && (
                <Route path="/dashboard/workbench" element={<WorkbenchPage />} />
              )}
              {OverviewPage && (
                <Route path="/dashboard/console" element={<OverviewPage />} />
              )}
              {CollectorPage && (
                <Route path="/collection/collector" element={<CollectorPage />} />
              )}
              {IngestPage && (
                <Route path="/collection/ingest" element={<IngestPage />} />
              )}
              {WriterPage && (
                <Route path="/collection/writer" element={<WriterPage />} />
              )}
              {DeviceCommunicationPage && <Route path="/device/communication" element={<DeviceCommunicationPage />} />}
              {DriverManagementPage && <Route path="/device/drivers" element={<DriverManagementPage />} />}
              {DeviceManagementPage && <Route path="/device/devices" element={<DeviceManagementPage />} />}
              {EdgePointMonitorPage && <Route path="/device/points" element={<EdgePointMonitorPage />} />}
              {PointManagementPage && <Route path="/device/points" element={<PointManagementPage />} />}
              <Route path="/device/realtime" element={<RealtimeMonitorPage />} />
              <Route path="/device/history" element={<HistoryDataPage />} />
              {SystemLogPage && <Route path="/logs/system" element={<SystemLogPage />} />}
              {UserManagementPage && RoleManagementPage && MenuManagementPage && (
                <>
                  <Route path="/system/users" element={<UserManagementPage />} />
                  <Route path="/system/roles" element={<RoleManagementPage />} />
                  <Route path="/system/menus" element={<MenuManagementPage />} />
                </>
              )}
              {SettingsPage && <Route path="/settings" element={<SettingsPage />} />}
              {DistributedNodesPage && <Route path="/distributed/nodes" element={<DistributedNodesPage />} />}

              {placeholderPages.map(({ path, title }) => (
                <Route
                  key={path}
                  path={path}
                  element={<PlaceholderPage title={title} />}
                />
              ))}

              {legacyRedirects.map(({ from, to }) =>
                from === "/" ? (
                  <Route
                    key={from}
                    index
                    element={<Navigate to={to} replace />}
                  />
                ) : (
                  <Route
                    key={from}
                    path={from}
                    element={<Navigate to={to} replace />}
                  />
                ),
              )}
            </Route>

            {/* 未匹配路由：回到工作台入口（未登录时由 AuthGuard 再跳登录） */}
            <Route path="*" element={<Navigate to={BUILD_ROLE === "edge" ? "/edge/operations" : "/dashboard/workbench"} replace />} />
          </Routes>
          </Suspense>
          </NavigableRouteErrorBoundary>
        </AppStartup>
      </AuthProvider>
    </BrowserRouter>
      </div>
      <Toaster richColors position="top-right" />
    </div>
  );
}
