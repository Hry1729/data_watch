import { useEffect, useMemo, useRef, useState } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { toast } from "sonner";
import {
  Bell,
  ChevronDown,
  CircleHelp,
  LogOut,
  Power,
  RefreshCw,
  Search,
  Settings,
  UserRound,
} from "lucide-react";
import appIconUrl from "../../src-tauri/icons/128x128.png";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { ConnectionStatusBar } from "@/components/ConnectionStatusBar";
import { OverlayScrollArea } from "@/components/layout/overlay-scroll-area";
import { SegmentedPills } from "@/components/layout/segmented-pills";
import { ThemeToggle } from "@/components/ThemeToggle";
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import { filterNavigationGroups } from "@/lib/navigation-access";
import { cn } from "@/lib/utils";
import {
  DEFAULT_SETTINGS_TAB,
  normalizeSettingsTab,
  SETTINGS_TABS,
} from "@/components/layout/settings-tabs";

const HELP_PATH = "/help";

/** 统一胶囊：白底、圆角、软阴影；顶栏里每个单元 32px（h-8）+ 6px 内边距 = 44px 高，
    一级轨道里换成 36px 单元（见 itemClass），胶囊是 48px = w-12 */
const CAPSULE = "flex shrink-0 items-center gap-1 rounded-3xl bg-white p-1.5 shadow-card dark:bg-sidebar";

/** 顶栏胶囊外的柔光外壳：内容还停在页面顶部时只保留胶囊自身的卡片阴影（与内容区卡片一致）；
    往下滚动、菜单栏开始悬浮在内容之上后，再淡入一圈柔光——box-shadow 从胶囊轮廓向四周
    连续发散、越外越淡（没有硬边），22px 模糊 + 2px 扩散，最外圈几乎归零 */
function glowClass(floating: boolean) {
  return cn(
    "rounded-3xl transition-shadow duration-300",
    floating &&
      "shadow-[0_0_22px_2px_color-mix(in_oklab,var(--foreground)_12%,transparent)]",
  );
}

/** 品牌位占位宽度：按 app 名称展开时的胶囊宽度固定，这样 logo/名称折叠时
    右侧的二级菜单行不会被推着走（改名称文案或字号时同步改这里） */
const BRAND_SLOT = "w-[152px]";

/** 内容区四周留白统一为 10px（与壳层 p-2.5 / gap-2.5 一致）：
    四个方向的内侧留白都由壳层接管——页面自带的 px-4/md:px-6/pb-4/pb-6 一律归零，
    左右留白来自 main 自身的盒边（左到轨道、右到窗口各 10px），
    上下留白由滚动视口自己给出：pt-[66px] = 顶栏 h-14(56) + 间距 10（内容能滚到顶栏下面）、pb-2.5 = 10 */
const CONTENT_GUTTER =
  "pt-[66px] pb-2.5 [&>*]:px-0 [&>*]:pb-0 md:[&>*]:px-0 md:[&>*]:pb-0";

/** 一级轨道里的图标单元：36px 圆圈，与 48px 胶囊之间留 6px 间隙（与顶栏一致） */
function itemClass(isActive: boolean) {
  return [
    "flex h-9 w-9 shrink-0 items-center justify-center rounded-3xl transition-colors",
    isActive
      ? "bg-brand text-brand-foreground"
      : "text-sidebar-foreground/80 hover:bg-muted hover:text-sidebar-foreground",
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
  const [quitDialogOpen, setQuitDialogOpen] = useState(false);
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const [brandPinned, setBrandPinned] = useState(false);
  const [headerFloating, setHeaderFloating] = useState(false);
  const userMenuRef = useRef<HTMLDivElement>(null);
  const isSettingsRoute = location.pathname === "/settings";
  const activeSettingsTab = normalizeSettingsTab(
    new URLSearchParams(location.search).get("tab"),
  );
  const settingsPath = `/settings?tab=${DEFAULT_SETTINGS_TAB}`;
  const activeGroup = isSettingsRoute
    ? null
    : visibleGroups.find((group) =>
        group.items.some((item) => item.path === location.pathname),
      ) ?? null;

  const pills = isSettingsRoute
    ? SETTINGS_TABS.map((tab) => ({ value: tab.value, label: tab.label }))
    : (activeGroup?.items ?? []).map((item) => ({
        value: item.path,
        label: item.label,
      }));
  const activePill = isSettingsRoute ? activeSettingsTab : location.pathname;

  useEffect(() => {
    setUserMenuOpen(false);
  }, [location.pathname]);

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

  /** 内容滚过顶部 10px 留白、开始钻到菜单栏下面之后，胶囊阴影放大成柔光 */
  function handleViewportScroll(scrollTop: number) {
    setHeaderFloating(scrollTop > 10);
  }

  function handlePillChange(value: string) {
    if (isSettingsRoute) {
      navigate(`/settings?tab=${value}`, { replace: true });
      return;
    }
    navigate(value);
  }

  async function quitApp() {
    try {
      const { getCurrentWindow } = await import("@tauri-apps/api/window");
      await getCurrentWindow().close();
    } catch {
      toast.error("当前环境不支持关闭窗口");
    }
  }

  return (
    <div className="relative flex h-full min-w-0 flex-col gap-2.5 overflow-hidden bg-background p-2.5">
      {/* 顶栏：每个部分一个独立胶囊，胶囊外各自围一圈柔光（阴影向四周发散、越外越淡）；
          gap-3 就是胶囊之间的空隙，品牌胶囊停在 x=10、账号胶囊右缘停在 1254，
          z-20 保证这些胶囊浮在穿过它们下方的内容之上 */}
      <header className="relative z-20 flex h-14 shrink-0 items-center gap-3">
        {/* 品牌：默认只有 app logo（无底色），悬停或点击固定、app 名称展开后才露出蓝色圆底。
            外层宽度按展开态固定，折叠时只收窄胶囊本身，不动右边的菜单行 */}
        <div className={cn("flex shrink-0 items-center", BRAND_SLOT)}>
          <div className={glowClass(headerFloating)}>
            <button
              type="button"
              onClick={() => setBrandPinned((pinned) => !pinned)}
              aria-expanded={brandPinned}
              aria-label="Data Watch 工业数据平台"
              className={cn(CAPSULE, "group cursor-pointer")}
            >
              <span
                className={cn(
                  "flex h-8 w-8 shrink-0 items-center justify-center rounded-full transition-colors",
                  brandPinned ? "bg-brand" : "sm:group-hover:bg-brand",
                )}
              >
                <img
                  src={appIconUrl}
                  alt=""
                  className="h-6 w-6 shrink-0 rounded-full object-cover"
                />
              </span>
              <span
                className={cn(
                  "h-8 min-w-0 flex-col justify-center pl-3 pr-2.5 text-center",
                  brandPinned ? "flex" : "hidden sm:group-hover:flex",
                )}
              >
                <span className="block truncate text-sm font-semibold leading-none text-sidebar-foreground">
                  Data Watch
                </span>
                <span className="mt-1 block truncate text-xs text-muted-foreground">
                  工业数据平台
                </span>
              </span>
            </button>
          </div>
        </div>

        {/* 二级菜单 */}
        <div className="flex min-w-0 flex-1 justify-center px-1">
          <div className={cn(glowClass(headerFloating), "flex min-w-0")}>
            <SegmentedPills
              value={activePill}
              options={pills}
              onChange={handlePillChange}
              ariaLabel={
                isSettingsRoute ? "设置导航" : `${activeGroup?.label ?? "二级"}子菜单`
              }
            />
          </div>
        </div>

        {/* 数据库状态 */}
        <div className={cn(glowClass(headerFloating), "shrink-0")}>
          <div className={CAPSULE}>
            <ConnectionStatusBar />
          </div>
        </div>

        {/* 刷新 + 搜索 + 通知 */}
        <div className={cn(glowClass(headerFloating), "shrink-0")}>
          <div className={CAPSULE}>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 rounded-3xl text-sidebar-foreground hover:bg-muted"
              onClick={refreshCurrentPage}
              aria-label="刷新当前页面"
              title="刷新当前页面"
            >
              <RefreshCw className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 rounded-3xl text-sidebar-foreground hover:bg-muted"
              disabled
              aria-label="搜索（待实现）"
              title="搜索（待实现）"
            >
              <Search className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 rounded-3xl text-sidebar-foreground hover:bg-muted"
              disabled
              aria-label="通知（待实现）"
              title="通知（待实现）"
            >
              <Bell className="h-4 w-4" />
            </Button>
          </div>
        </div>

        {/* 账号 */}
        <div className={cn(glowClass(headerFloating), "shrink-0")}>
          <div ref={userMenuRef} className={cn(CAPSULE, "relative")}>
            {userMenuOpen && (
              <div className="absolute right-0 top-[calc(100%+8px)] z-30 w-56 rounded-xl border border-border bg-popover p-1 text-popover-foreground shadow-lg">
                <div className="border-b border-border px-2.5 py-2">
                  <p className="text-sm font-medium">
                    {auth?.user?.userName ?? "本地用户"}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {auth?.user?.roles?.length
                      ? auth.user.roles.join(" · ")
                      : "Data Watch Console"}
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
              aria-expanded={userMenuOpen}
              aria-label="账号菜单"
              title={auth?.user?.userName ?? "本地用户"}
              className="flex h-8 items-center gap-2 rounded-3xl pl-0.5 pr-1.5 transition-colors hover:bg-muted"
            >
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-muted text-muted-foreground">
                <UserRound className="h-4 w-4" />
              </span>
              <span className="hidden min-w-0 max-w-32 text-left lg:block">
                <span className="block truncate text-sm font-medium leading-none">
                  {auth?.user?.userName ?? "本地用户"}
                </span>
                <span className="mt-0.5 block truncate text-xs text-muted-foreground">
                  {auth?.user?.roles?.length ? auth.user.roles.join(" · ") : "已登录"}
                </span>
              </span>
              <ChevronDown className="hidden h-3.5 w-3.5 shrink-0 text-muted-foreground lg:block" />
            </button>
          </div>
        </div>
      </header>

      <div className="flex min-h-0 min-w-0 flex-1 gap-2.5">
        {/* 一级图标轨道：一竖条胶囊（主题 / 主导航 / 帮助+退出），单元 36px、胶囊 48px */}
        <aside className="relative flex w-12 shrink-0 flex-col">
          {/* 主题与帮助/退出钉在轨道两端；一级菜单不在流内，钉在整个工作区高度的正中（见下方 nav） */}
          <div className={cn(CAPSULE, "absolute inset-x-0 top-0 flex-col")}>
            <ThemeToggle />
          </div>

          <div className={cn(CAPSULE, "absolute inset-x-0 bottom-0 flex-col")}>
            <button
              type="button"
              onClick={() => navigate(HELP_PATH)}
              title="帮助"
              aria-current={location.pathname === HELP_PATH ? "page" : undefined}
              className={itemClass(location.pathname === HELP_PATH)}
            >
              <CircleHelp className="h-4 w-4 shrink-0" />
            </button>

            <button
              type="button"
              onClick={() => setQuitDialogOpen(true)}
              title="退出软件"
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-3xl text-destructive transition-colors hover:bg-destructive-soft"
            >
              <Power className="h-4 w-4 shrink-0" />
            </button>
          </div>
        </aside>

        {/* 主内容区：卡片直接罗列在灰色底上，四周留白由 CONTENT_GUTTER 统一给到 10px。
            -mt-[66px]（h-14 的 56 + 间距 10）把滚动视口顶到顶栏上沿，卡片便能滚到顶栏下面；
            -mb-2.5 把视口下沿顶到窗口底边，滚到底时最后一张卡片（连阴影）落在 10px 留白之上 */}
        <main className="min-h-0 min-w-0 flex-1 -mb-2.5 -mt-[66px]">
          <OverlayScrollArea
            className="h-full"
            viewportClassName={CONTENT_GUTTER}
            thumbClassName="-right-2"
            onViewportScroll={handleViewportScroll}
          >
            <Outlet />
          </OverlayScrollArea>
        </main>
      </div>

      {/* 一级菜单：钉在整个工作区（含顶栏）高度的正中，左缘与图标轨道对齐 */}
      <nav
        className={cn(CAPSULE, "absolute left-2.5 top-1/2 w-12 -translate-y-1/2 flex-col")}
        aria-label="主导航"
      >
        {visibleGroups.map((group) => {
          const GroupIcon = group.icon;
          const isActive = activeGroup?.label === group.label;
          const firstItem = group.items[0];
          return (
            <button
              key={group.label}
              type="button"
              onClick={() => firstItem && navigate(firstItem.path)}
              title={group.label}
              aria-current={isActive ? "page" : undefined}
              className={itemClass(isActive)}
            >
              <GroupIcon className="h-4 w-4 shrink-0" />
            </button>
          );
        })}

        {canOpenSettings && (
          <button
            type="button"
            onClick={() => navigate(settingsPath)}
            title="设置"
            aria-current={isSettingsRoute ? "page" : undefined}
            className={itemClass(isSettingsRoute)}
          >
            <Settings className="h-4 w-4 shrink-0" />
          </button>
        )}
      </nav>

      <AlertDialog open={quitDialogOpen} onOpenChange={setQuitDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>退出软件</AlertDialogTitle>
            <AlertDialogDescription>
              确认退出 Data Watch？未保存的操作会丢失。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={() => void quitApp()}>
              退出
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
