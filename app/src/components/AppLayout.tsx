import { useEffect, useMemo, useRef, useState, type PointerEvent as ReactPointerEvent } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { createPortal } from "react-dom";
import {
  ArrowLeft,
  ChevronDown,
  ChevronRight,
  ChevronUp,
  LogOut,
  PanelLeftClose,
  PanelLeftOpen,
  RefreshCw,
  Settings,
  UserRound,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { ConnectionStatusBar } from "@/components/ConnectionStatusBar";
import { TopbarTools } from "@/components/TopbarTools";
import { ThemeToggle } from "@/components/ThemeToggle";
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import { getPageMeta } from "@/config/navigation";
import { filterNavigationGroups } from "@/lib/navigation-access";
import {
  readSidebarCollapsed,
  writeSidebarCollapsed,
} from "@/components/layout/sidebar-state";
import {
  DEFAULT_SETTINGS_TAB,
  normalizeSettingsTab,
  SETTINGS_TABS,
} from "@/components/layout/settings-tabs";

function navItemClass(isActive: boolean) {
  return [
    "flex h-9 items-center gap-2.5 rounded-md px-2.5 text-sm transition-colors",
    isActive
      ? "bg-brand-soft font-medium text-brand"
      : "text-sidebar-foreground/75 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
  ].join(" ");
}

export function AppLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const { auth, logout } = useAuth();
  const { refreshCurrentPage } = usePageRefresh();
  const canOpenSettings = auth?.allowedPaths?.includes("/settings") ?? false;
  const visibleGroups = useMemo(
    () => filterNavigationGroups(auth?.allowedPaths),
    [auth?.allowedPaths],
  );
  const [sidebarCollapsed, setSidebarCollapsed] = useState(() =>
    readSidebarCollapsed(window.localStorage),
  );
  const [openGroups, setOpenGroups] = useState<Set<string>>(
    () => new Set(visibleGroups.map((group) => group.label)),
  );
  const [collapsedFlyout, setCollapsedFlyout] = useState<{
    groupLabel: string;
    left: number;
    top: number;
  } | null>(null);
  const flyoutCloseTimerRef = useRef<number | null>(null);
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const userMenuRef = useRef<HTMLDivElement>(null);
  const pageMeta = getPageMeta(location.pathname);
  const isSettingsRoute = location.pathname === "/settings";
  const activeSettingsTab = normalizeSettingsTab(
    new URLSearchParams(location.search).get("tab"),
  );
  const settingsPath = `/settings?tab=${DEFAULT_SETTINGS_TAB}`;
  const collapsedFlyoutGroup = collapsedFlyout
    ? visibleGroups.find((group) => group.label === collapsedFlyout.groupLabel)
    : null;

  useEffect(() => {
    setOpenGroups((current) => {
      const next = new Set(current);
      for (const group of visibleGroups) next.add(group.label);
      return next;
    });
  }, [visibleGroups]);

  useEffect(() => {
    setUserMenuOpen(false);
    setCollapsedFlyout(null);
    if (flyoutCloseTimerRef.current !== null) {
      window.clearTimeout(flyoutCloseTimerRef.current);
      flyoutCloseTimerRef.current = null;
    }
  }, [location.pathname, sidebarCollapsed]);

  useEffect(() => {
    if (!userMenuOpen) return;

    function handlePointerDown(event: PointerEvent) {
      if (
        event.target instanceof Node &&
        !userMenuRef.current?.contains(event.target)
      ) {
        setUserMenuOpen(false);
      }
    }

    document.addEventListener("pointerdown", handlePointerDown);
    return () => document.removeEventListener("pointerdown", handlePointerDown);
  }, [userMenuOpen]);

  function updateSidebarCollapsed(collapsed: boolean) {
    setSidebarCollapsed(collapsed);
    writeSidebarCollapsed(window.localStorage, collapsed);
  }

  function cancelFlyoutClose() {
    if (flyoutCloseTimerRef.current === null) return;
    window.clearTimeout(flyoutCloseTimerRef.current);
    flyoutCloseTimerRef.current = null;
  }

  function showCollapsedGroupFlyout(
    groupLabel: string,
    itemCount: number,
    event: ReactPointerEvent<HTMLDivElement>,
  ) {
    if (!sidebarCollapsed) return;
    cancelFlyoutClose();
    const rect = event.currentTarget.getBoundingClientRect();
    const estimatedHeight = itemCount * 36 + 8;
    setCollapsedFlyout({
      groupLabel,
      left: rect.right + 8,
      top: Math.max(8, Math.min(rect.top, window.innerHeight - estimatedHeight - 8)),
    });
  }

  function scheduleFlyoutClose() {
    cancelFlyoutClose();
    flyoutCloseTimerRef.current = window.setTimeout(() => {
      setCollapsedFlyout(null);
      flyoutCloseTimerRef.current = null;
    }, 120);
  }
  function handleGroupClick(groupLabel: string) {
    if (sidebarCollapsed) {
      updateSidebarCollapsed(false);
      setOpenGroups((current) => new Set(current).add(groupLabel));
      return;
    }

    setOpenGroups((current) => {
      const next = new Set(current);
      if (next.has(groupLabel)) next.delete(groupLabel);
      else next.add(groupLabel);
      return next;
    });
  }

  const headerActions = (
    <div className="flex shrink-0 items-center gap-1.5">
      <div className="mr-2 hidden xl:block">
        <ConnectionStatusBar />
      </div>
      <Button
        variant="ghost"
        size="icon"
        className="h-8 w-8"
        onClick={refreshCurrentPage}
        aria-label="刷新当前页面"
        title="刷新当前页面"
      >
        <RefreshCw className="h-4 w-4" />
      </Button>
      <div
        className="flex h-8 w-8 items-center justify-center rounded-md bg-muted text-muted-foreground"
        title={auth?.user?.userName ?? "本地用户"}
        aria-label={auth?.user?.userName ?? "本地用户"}
      >
        <UserRound className="h-4 w-4" />
      </div>
      {canOpenSettings && (
        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8"
          onClick={() => navigate(settingsPath)}
          aria-label="设置"
          title="设置"
        >
          <Settings className="h-4 w-4" />
        </Button>
      )}
      <TopbarTools />
      <ThemeToggle />
    </div>
  );

  return (
    <div className="flex h-full min-w-0 overflow-hidden bg-sidebar">
      <aside
        className={[
          "flex shrink-0 flex-col bg-sidebar text-sidebar-foreground transition-[width] duration-200",
          sidebarCollapsed ? "w-16" : "w-60",
        ].join(" ")}
      >
        <div className="flex h-14 shrink-0 items-center justify-between border-b border-sidebar-border px-3">
          {!sidebarCollapsed && (
            <div className="min-w-0">
              <p className="truncate text-sm font-semibold text-sidebar-foreground">Data Watch</p>
              <p className="truncate text-xs text-muted-foreground">工业数据平台</p>
            </div>
          )}
          <Button
            variant="ghost"
            size="icon"
            className={[
              "h-8 w-8 shrink-0 text-sidebar-foreground",
              sidebarCollapsed ? "mx-auto" : "ml-auto",
            ].join(" ")}
            onClick={() => updateSidebarCollapsed(!sidebarCollapsed)}
            aria-label={sidebarCollapsed ? "展开导航栏" : "折叠导航栏"}
            title={sidebarCollapsed ? "展开导航栏" : "折叠导航栏"}
          >
            {sidebarCollapsed ? (
              <PanelLeftOpen className="h-4 w-4" />
            ) : (
              <PanelLeftClose className="h-4 w-4" />
            )}
          </Button>
        </div>

        <nav
          className="min-h-0 flex-1 overflow-y-auto px-2 py-3"
          aria-label={isSettingsRoute ? "设置导航" : "主导航"}
        >
          {isSettingsRoute ? (
            <div className="space-y-1">
              <button
                type="button"
                onClick={() => navigate(-1)}
                aria-label="返回上一页"
                title={sidebarCollapsed ? "返回" : undefined}
                className={[
                  "flex h-9 w-full items-center rounded-md text-sm transition-colors",
                  sidebarCollapsed
                    ? "justify-center px-0"
                    : "gap-2.5 px-2.5",
                  "text-sidebar-foreground/75 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
                ].join(" ")}
              >
                <ArrowLeft className="h-4 w-4 shrink-0" />
                {!sidebarCollapsed && <span className="min-w-0 truncate">返回</span>}
              </button>

              {!sidebarCollapsed && (
                <p className="px-2.5 pb-1 pt-2 text-xs font-medium text-muted-foreground">
                  设置
                </p>
              )}

              {SETTINGS_TABS.map((tab) => {
                const TabIcon = tab.icon;
                const isActive = tab.value === activeSettingsTab;
                return (
                  <button
                    key={tab.value}
                    type="button"
                    title={sidebarCollapsed ? tab.label : undefined}
                    onClick={() =>
                      navigate(`/settings?tab=${tab.value}`, { replace: true })
                    }
                    className={[
                      "flex h-9 w-full items-center rounded-md text-sm transition-colors",
                      sidebarCollapsed
                        ? "justify-center px-0"
                        : "gap-2.5 px-2.5",
                      isActive
                        ? "bg-brand-soft font-medium text-brand"
                        : "text-sidebar-foreground/75 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
                    ].join(" ")}
                  >
                    <TabIcon className="h-4 w-4 shrink-0" />
                    {!sidebarCollapsed && (
                      <span className="min-w-0 truncate">{tab.label}</span>
                    )}
                  </button>
                );
              })}
            </div>
          ) : (
            <div className="space-y-1">
              {visibleGroups.map((group) => {
                const GroupIcon = group.icon;
                const isOpen = openGroups.has(group.label);
                const hasActiveItem = group.items.some(
                  (item) => item.path === location.pathname,
                );

                return (
                  <div
                    key={group.label}
                    onPointerEnter={(event) =>
                      showCollapsedGroupFlyout(group.label, group.items.length, event)
                    }
                    onPointerLeave={scheduleFlyoutClose}
                  >
                    <button
                      type="button"
                      onClick={() => handleGroupClick(group.label)}
                      title={sidebarCollapsed ? group.label : undefined}
                      aria-expanded={!sidebarCollapsed && isOpen}
                      className={[
                        "flex h-9 w-full items-center rounded-md text-sm font-medium transition-colors",
                        sidebarCollapsed
                          ? "justify-center px-0"
                          : "gap-2.5 px-2.5",
                        hasActiveItem && sidebarCollapsed
                          ? "bg-brand-soft text-brand"
                          : "text-sidebar-foreground hover:bg-sidebar-accent",
                      ].join(" ")}
                    >
                      <GroupIcon className="h-4 w-4 shrink-0" />
                      {!sidebarCollapsed && (
                        <>
                          <span className="min-w-0 flex-1 truncate text-left">{group.label}</span>
                          {isOpen ? (
                            <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
                          ) : (
                            <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />
                          )}
                        </>
                      )}
                    </button>

                    {!sidebarCollapsed && isOpen && (
                      <div className="ml-4 mt-1 space-y-1 border-l border-sidebar-border pl-2">
                        {group.items.map((item) => {
                          const ItemIcon = item.icon;
                          return (
                            <NavLink
                              key={item.path}
                              to={item.path}
                              className={({ isActive }) => navItemClass(isActive)}
                            >
                              <ItemIcon className="h-4 w-4 shrink-0" />
                              <span className="min-w-0 truncate">{item.label}</span>
                            </NavLink>
                          );
                        })}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </nav>

        <div className="shrink-0 border-t border-sidebar-border p-2">
          <div ref={userMenuRef} className="relative">
            {userMenuOpen && (
              <div
                className={[
                  "absolute bottom-[calc(100%+8px)] z-30 rounded-md border border-border bg-popover p-1 text-popover-foreground shadow-lg",
                  sidebarCollapsed ? "left-0 w-52" : "left-0 right-0",
                ].join(" ")}
              >
                <div className="border-b border-border px-2.5 py-2">
                  <p className="text-sm font-medium">{auth?.user?.userName ?? "本地用户"}</p>
                  <p className="text-xs text-muted-foreground">
                    {auth?.user?.roles?.length ? auth.user.roles.join(" · ") : "Data Watch Console"}
                  </p>
                </div>
                {canOpenSettings && (
                  <button
                    type="button"
                    onClick={() => navigate(settingsPath)}
                    className="mt-1 flex h-9 w-full items-center gap-2 rounded-sm px-2.5 text-sm transition-colors hover:bg-accent"
                  >
                    <Settings className="h-4 w-4" />
                    设置
                  </button>
                )}
                <button
                  type="button"
                  onClick={logout}
                  className="flex h-9 w-full items-center gap-2 rounded-sm px-2.5 text-sm text-destructive transition-colors hover:bg-destructive-soft"
                >
                  <LogOut className="h-4 w-4" />
                  退出登录
                </button>
              </div>
            )}

            <button
              type="button"
              onClick={() => setUserMenuOpen((open) => !open)}
              title={sidebarCollapsed ? (auth?.user?.userName ?? "本地用户") : undefined}
              aria-expanded={userMenuOpen}
              className={[
                "flex h-11 w-full items-center rounded-md transition-colors hover:bg-sidebar-accent",
                sidebarCollapsed ? "justify-center px-0" : "gap-2.5 px-2",
              ].join(" ")}
            >
              <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-muted text-muted-foreground">
                <UserRound className="h-4 w-4" />
              </span>
              {!sidebarCollapsed && (
                <>
                  <span className="min-w-0 flex-1 text-left">
                    <span className="block truncate text-sm font-medium">{auth?.user?.userName ?? "本地用户"}</span>
                    <span className="block truncate text-xs text-muted-foreground">
                      {auth?.user?.roles?.length ? auth.user.roles.join(" · ") : "已登录"}
                    </span>
                  </span>
                  <ChevronUp className="h-3.5 w-3.5 text-muted-foreground" />
                </>
              )}
            </button>
          </div>
        </div>
      </aside>

      {sidebarCollapsed && collapsedFlyout && collapsedFlyoutGroup && createPortal(
        <div
          className="fixed z-50 w-52 overflow-y-auto rounded-md border border-border bg-popover p-1 text-popover-foreground shadow-lg"
          style={{
            left: collapsedFlyout.left,
            top: collapsedFlyout.top,
            maxHeight: "calc(100vh - 16px)",
          }}
          onPointerEnter={cancelFlyoutClose}
          onPointerLeave={scheduleFlyoutClose}
          aria-label={collapsedFlyoutGroup.label + "子菜单"}
        >
          {collapsedFlyoutGroup.items.map((item) => {
            const ItemIcon = item.icon;
            return (
              <NavLink
                key={item.path}
                to={item.path}
                onClick={() => setCollapsedFlyout(null)}
                className={({ isActive }) => [
                  "flex h-9 items-center gap-2.5 rounded-sm px-2.5 text-sm transition-colors",
                  isActive
                    ? "bg-brand-soft font-medium text-brand"
                    : "text-popover-foreground hover:bg-accent",
                ].join(" ")}
              >
                <ItemIcon className="h-4 w-4 shrink-0" />
                <span className="min-w-0 truncate">{item.label}</span>
              </NavLink>
            );
          })}
        </div>,
        document.body,
      )}

      <section className="m-2 ml-0 flex min-w-0 flex-1 flex-col overflow-hidden rounded-[12px] bg-background shadow-[var(--inset-shadow)]">
        <header className="flex h-14 shrink-0 items-center border-b border-border bg-transparent px-4">
          <div className="flex min-w-0 items-center gap-2 text-sm">
            {isSettingsRoute ? (
              <h1 className="truncate font-semibold text-foreground">设置</h1>
            ) : (
              <>
                {pageMeta?.groupLabel && (
                  <>
                    <span className="hidden truncate text-muted-foreground sm:inline">
                      {pageMeta.groupLabel}
                    </span>
                    <ChevronRight className="hidden h-3.5 w-3.5 shrink-0 text-muted-foreground sm:block" />
                  </>
                )}
                <h1 className="truncate font-semibold text-foreground">
                  {pageMeta?.title ?? "Data Watch"}
                </h1>
              </>
            )}
          </div>
          <div className="ml-auto">{headerActions}</div>
        </header>

        <main className="min-h-0 flex-1 overflow-auto p-4">
          <Outlet />
        </main>
      </section>
    </div>
  );
}
