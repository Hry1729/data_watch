import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { Settings } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ConnectionStatusBar } from "@/components/ConnectionStatusBar";
import { ThemeToggle } from "@/components/ThemeToggle";

const NAV_ITEMS = [
  { to: "/",          label: "总览",      end: true  },
  { to: "/collector", label: "Collector", end: false },
  { to: "/writer",    label: "Writer",    end: false },
] as const;

export function AppLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const isSettings = location.pathname === "/settings";

  return (
    <div className="flex flex-col h-full bg-background">
      {/* 顶部应用导航栏（/settings 时完全隐藏，FR-014） */}
      {!isSettings && (
        <header className="h-[72px] flex-shrink-0 border-b border-border flex items-center px-6 gap-4">
          {/* 分段导航 Tab */}
          <nav className="flex items-center gap-1">
            {NAV_ITEMS.map(({ to, label, end }) => (
              <NavLink
                key={to}
                to={to}
                end={end}
                className={({ isActive }) =>
                  [
                    "px-4 py-1.5 rounded-md text-sm font-medium transition-colors border",
                    isActive
                      ? "border-brand text-brand bg-card"
                      : "border-transparent text-muted-foreground hover:text-foreground hover:bg-accent/50",
                  ].join(" ")
                }
              >
                {label}
              </NavLink>
            ))}
          </nav>

          {/* 右侧：连接状态点 + 设置按钮 */}
          <div className="ml-auto flex items-center gap-3">
            <ConnectionStatusBar />
            <ThemeToggle />
            <Button
              variant="ghost"
              size="icon"
              className="h-7 w-7"
              onClick={() => navigate("/settings")}
              aria-label="设置"
            >
              <Settings className="h-4 w-4" />
            </Button>
          </div>
        </header>
      )}

      {/* 页面内容 */}
      <main className="flex-1 overflow-auto">
        <Outlet />
      </main>
    </div>
  );
}
