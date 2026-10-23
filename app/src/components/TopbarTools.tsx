import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { LucideIcon } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { Dialog as DialogPrimitive } from "radix-ui";
import {
  ArrowRight, Bell, CheckCheck, CornerDownLeft, FileText, History, LifeBuoy,
  Moon, RefreshCw, Search, Settings2, Sun,
} from "lucide-react";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Skeleton } from "@/components/ui/skeleton";
import { BUILD_ROLE } from "@/config/build-role";
import { useAuth } from "@/contexts/AuthContext";
import { useTheme } from "@/contexts/ThemeContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { backendClient } from "@/lib/backend-client";
import { agentClient } from "@/lib/agent-client";
import { filterNavigationGroups } from "@/lib/navigation-access";
import { recentNotifications, searchPages } from "@/lib/topbar-data";
import { cn } from "@/lib/utils";
import type { ModuleName } from "@/types";

type Severity = "critical" | "error" | "warn";
interface Notice { key: string; severity: Severity; source: string; content: string; time?: string; path: string }

const RECENT_KEY = "data-watch-recent-pages";
const SEEN_KEY = "data-watch-notice-seen";

function readRecent(): string[] {
  try { const v = JSON.parse(localStorage.getItem(RECENT_KEY) ?? "[]"); return Array.isArray(v) ? v.filter(x => typeof x === "string") : []; } catch { return []; }
}
function pushRecent(path: string) {
  try { localStorage.setItem(RECENT_KEY, JSON.stringify([path, ...readRecent().filter(p => p !== path)].slice(0, 6))); } catch { /* ignore */ }
}

function relativeTime(iso?: string): string {
  if (!iso) return "";
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return iso;
  const s = Math.max(0, Math.round((Date.now() - t) / 1000));
  if (s < 60) return "刚刚";
  if (s < 3600) return `${Math.floor(s / 60)} 分钟前`;
  if (s < 86400) return `${Math.floor(s / 3600)} 小时前`;
  return new Date(t).toLocaleString("zh-CN", { month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hour12: false });
}

const SEVERITY: Record<Severity, { label: string }> = { critical: { label: "严重" }, error: { label: "错误" }, warn: { label: "警告" } };

function Kbd({ children, className }: { children: React.ReactNode; className?: string }) {
  return <kbd className={cn("inline-flex h-5 min-w-5 items-center justify-center rounded-[5px] bg-muted px-1.5 font-sans text-[11px] font-medium text-muted-foreground shadow-[inset_0_-1px_0_rgb(0_0_0/0.08)]", className)}>{children}</kbd>;
}

interface CommandItem { id: string; label: string; hint: string; icon: LucideIcon; run: () => void; section: string }

export function TopbarTools() {
  const { auth } = useAuth();
  const { theme, setTheme } = useTheme();
  const navigate = useNavigate();
  const [searchOpen, setSearchOpen] = useState(false);
  const [noticeOpen, setNoticeOpen] = useState(false);
  const [keyword, setKeyword] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);
  const [filter, setFilter] = useState<"all" | "error" | "warn">("all");
  const [seenAt, setSeenAt] = useState<string>(() => localStorage.getItem(SEEN_KEY) ?? "");
  const listRef = useRef<HTMLDivElement>(null);
  const isMac = typeof navigator !== "undefined" && /Mac|iPhone|iPad/.test(navigator.platform);
  const mod = isMac ? "⌘" : "Ctrl";

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setSearchOpen(open => { if (!open) { setKeyword(""); setActiveIndex(0); } return !open; });
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  const canReadLogs = BUILD_ROLE !== "edge" && Boolean(auth?.allowedPaths?.includes("/logs/system"));
  const canSettings = Boolean(auth?.allowedPaths?.includes("/settings"));
  const groups = useMemo(() => filterNavigationGroups(auth?.allowedPaths), [auth?.allowedPaths]);
  const iconOf = useMemo(() => {
    const map = new Map<string, LucideIcon>();
    for (const g of groups) for (const item of g.items) map.set(item.path, item.icon);
    map.set("/settings", Settings2); map.set("/help", LifeBuoy);
    return map;
  }, [groups]);

  function go(path: string) { pushRecent(path); setSearchOpen(false); setNoticeOpen(false); navigate(path); }

  const commands = useMemo<CommandItem[]>(() => {
    const q = keyword.trim().toLocaleLowerCase();
    const pages = searchPages(groups, keyword, canSettings);
    const out: CommandItem[] = [];
    if (!q) {
      const recent = readRecent().map(p => pages.find(x => x.path === p)).filter(Boolean).slice(0, 3);
      for (const page of recent) out.push({ id: `r:${page!.path}`, section: "最近访问", label: page!.label, hint: page!.group, icon: History, run: () => go(page!.path) });
    }
    for (const page of pages) out.push({ id: page.path, section: "页面", label: page.label, hint: page.group, icon: iconOf.get(page.path) ?? FileText, run: () => go(page.path) });
    const actions: CommandItem[] = [
      { id: "a:theme", section: "操作", label: theme === "dark" ? "切换到浅色主题" : "切换到深色主题", hint: "外观", icon: theme === "dark" ? Sun : Moon, run: () => { setTheme(theme === "dark" ? "light" : "dark"); setSearchOpen(false); } },
      { id: "a:reload", section: "操作", label: "刷新当前页面", hint: "应用", icon: RefreshCw, run: () => window.location.reload() },
    ];
    for (const a of actions) if (!q || `${a.label} ${a.hint}`.toLocaleLowerCase().includes(q)) out.push(a);
    return out;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [keyword, groups, canSettings, iconOf, theme]);

  useEffect(() => {
    listRef.current?.querySelector<HTMLElement>(`[data-index="${activeIndex}"]`)?.scrollIntoView({ block: "nearest" });
  }, [activeIndex]);

  const loadNotices = useCallback(async (token: string, signal: AbortSignal): Promise<Notice[]> => {
    if (canReadLogs) {
      const results = await Promise.all((["WARN", "ERROR", "CRITICAL"] as const).map(level =>
        backendClient.systemLogList(token, { level, pageNo: 1, pageSize: 20 }, signal)));
      return recentNotifications(results).map(item => ({
        key: `${item.kind}:${item.id}`,
        severity: item.level === "CRITICAL" ? "critical" : item.level === "ERROR" ? "error" : "warn",
        source: item.moduleName || item.serviceName || item.source,
        content: item.content, time: item.time, path: "/logs/system",
      }));
    }
    const modules: ModuleName[] = BUILD_ROLE === "edge" ? ["collector", "storage", "uploader"] : BUILD_ROLE === "distributed-center" ? ["ingest", "writer"] : ["collector", "writer"];
    const results = await Promise.all(modules.map(name => agentClient.getProcessMetrics(name, signal)));
    return results.flatMap((item, index) => item.status === "running" ? [] : [{
      key: modules[index], severity: item.status === "crashed" ? "error" : "warn", source: modules[index],
      content: item.status === "stopped" ? "进程已停止，请确认是否符合运行计划。" : item.status === "crashed" ? "进程异常退出，请检查模块日志。" : "暂时无法确认进程状态。",
      path: BUILD_ROLE === "edge" ? `/edge/${modules[index]}` : `/collection/${modules[index]}`,
    } satisfies Notice]);
  }, [canReadLogs]);
  const notices = useBackendQuery(loadNotices);
  const list = notices.data ?? [];
  const unread = list.filter(n => n.time && n.time > seenAt && n.severity !== "warn").length;
  const shown = list.filter(n => filter === "all" || (filter === "warn" ? n.severity === "warn" : n.severity !== "warn"));
  function markAllRead() {
    const latest = list.reduce((m, n) => (n.time && n.time > m ? n.time : m), seenAt);
    localStorage.setItem(SEEN_KEY, latest); setSeenAt(latest);
  }

  let lastSection = "";
  return <>
    <DialogPrimitive.Root open={searchOpen} onOpenChange={open => { setSearchOpen(open); if (open) { setKeyword(""); setActiveIndex(0); } }}>
      <DialogPrimitive.Trigger asChild>
        <button type="button" className="inline-flex size-8 items-center justify-center rounded-md text-sidebar-foreground/70 transition-colors outline-none hover:bg-sidebar-accent hover:text-sidebar-foreground focus-visible:ring-2 focus-visible:ring-brand/40" aria-label={`搜索（${mod}+K）`} title={`搜索（${mod}+K）`} aria-keyshortcuts="Control+K Meta+K"><Search className="size-4" /></button>
      </DialogPrimitive.Trigger>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-slate-900/20 backdrop-blur-[2px] data-[state=open]:animate-in data-[state=open]:fade-in-0 dark:bg-black/50" />
        <DialogPrimitive.Content aria-describedby={undefined} className="fixed top-[14vh] left-1/2 z-50 flex w-[min(640px,calc(100vw-48px))] -translate-x-1/2 flex-col overflow-hidden rounded-2xl bg-popover text-popover-foreground shadow-[0_24px_64px_-12px_rgb(15_23_42/0.28),0_0_0_0.5px_rgb(15_23_42/0.12)] outline-none data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-[0.98] dark:shadow-[0_24px_64px_-12px_rgb(0_0_0/0.7),0_0_0_0.5px_rgb(255_255_255/0.12)]">
          <DialogPrimitive.Title className="sr-only">搜索</DialogPrimitive.Title>
          <form className="flex h-14 items-center gap-3 border-b border-border/60 px-4" onSubmit={e => { e.preventDefault(); commands[Math.min(activeIndex, commands.length - 1)]?.run(); }}>
            <Search className="size-[18px] shrink-0 text-muted-foreground" />
            <input autoFocus value={keyword} placeholder="搜索页面或操作…" aria-label="搜索页面或操作"
              className="h-full flex-1 bg-transparent text-[15px] outline-none placeholder:text-muted-foreground/70"
              onChange={e => { setKeyword(e.target.value); setActiveIndex(0); }}
              onKeyDown={e => {
                if (!commands.length) return;
                if (e.key === "ArrowDown") { e.preventDefault(); setActiveIndex(i => (i + 1) % commands.length); }
                else if (e.key === "ArrowUp") { e.preventDefault(); setActiveIndex(i => (i - 1 + commands.length) % commands.length); }
              }} />
            <Kbd>Esc</Kbd>
          </form>
          <div ref={listRef} role="listbox" aria-label="搜索结果" className="max-h-[min(420px,56vh)] overflow-y-auto p-2">
            {commands.length ? commands.map((cmd, index) => {
              const header = cmd.section !== lastSection ? cmd.section : null; lastSection = cmd.section;
              const active = index === activeIndex; const Icon = cmd.icon;
              return <div key={cmd.id}>
                {header && <p className="px-2.5 pt-2 pb-1.5 text-xs font-medium text-muted-foreground">{header}</p>}
                <button type="button" role="option" aria-selected={active} data-index={index}
                  onMouseMove={() => setActiveIndex(index)} onClick={cmd.run}
                  className={cn("flex h-10 w-full items-center gap-3 rounded-lg px-2.5 text-left text-sm transition-colors", active ? "bg-accent text-accent-foreground" : "text-foreground")}>
                  <span className={cn("flex size-7 shrink-0 items-center justify-center rounded-md", active ? "bg-brand text-brand-foreground" : "bg-muted text-muted-foreground")}><Icon className="size-[15px]" /></span>
                  <span className="min-w-0 flex-1 truncate">{cmd.label}</span>
                  <span className="text-xs text-muted-foreground">{cmd.hint}</span>
                  <CornerDownLeft className={cn("size-3.5 text-muted-foreground", active ? "opacity-100" : "opacity-0")} />
                </button>
              </div>;
            }) : <div role="status" className="flex flex-col items-center gap-2 py-12 text-sm text-muted-foreground"><Search className="size-6 opacity-40" />没有找到「{keyword}」</div>}
          </div>
          <div className="flex h-10 items-center gap-4 border-t border-border/60 px-4 text-xs text-muted-foreground">
            <span className="flex items-center gap-1.5"><Kbd>↑</Kbd><Kbd>↓</Kbd>选择</span>
            <span className="flex items-center gap-1.5"><Kbd><CornerDownLeft className="size-3" /></Kbd>打开</span>
            <span className="ml-auto flex items-center gap-1.5"><Kbd>{mod}</Kbd><Kbd>K</Kbd>随时唤起</span>
          </div>
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>

    <Popover open={noticeOpen} onOpenChange={setNoticeOpen}>
      <PopoverTrigger asChild>
        <button type="button" className="relative inline-flex size-8 items-center justify-center rounded-md text-sidebar-foreground/70 transition-colors outline-none hover:bg-sidebar-accent hover:text-sidebar-foreground focus-visible:ring-2 focus-visible:ring-brand/40 data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-foreground" aria-label={unread ? `通知（${unread} 条未读）` : "通知"} title="通知">
          <Bell className="size-4" />
          {unread > 0 && <span className="absolute top-1 right-1 flex h-3.5 min-w-3.5 items-center justify-center rounded-full bg-destructive px-1 text-[9px] leading-none font-semibold text-white ring-2 ring-sidebar">{unread > 9 ? "9+" : unread}</span>}
        </button>
      </PopoverTrigger>
      <PopoverContent side="right" align="start" sideOffset={10} className="flex w-[360px] flex-col p-0">
        <div className="flex h-11 items-center gap-1 border-b border-border/60 px-3">
          {([["all", "全部"], ["error", "错误"], ["warn", "警告"]] as const).map(([v, l]) => {
            const count = v === "all" ? list.length : list.filter(n => (v === "warn") === (n.severity === "warn")).length;
            return <button key={v} type="button" onClick={() => setFilter(v)} className={cn("relative h-11 px-2 text-[13px] transition-colors", filter === v ? "font-medium text-foreground after:absolute after:inset-x-2 after:bottom-0 after:h-0.5 after:rounded-full after:bg-foreground" : "text-muted-foreground hover:text-foreground")}>{l}<span className="ml-1 text-xs tabular-nums text-muted-foreground">{count}</span></button>;
          })}
          <div className="ml-auto flex items-center">
            <button type="button" onClick={markAllRead} disabled={!unread} title="全部标为已读" aria-label="全部标为已读" className="inline-flex size-7 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-foreground disabled:opacity-35"><CheckCheck className="size-4" /></button>
            <button type="button" onClick={notices.refresh} disabled={notices.loading} title="刷新" aria-label="刷新通知" className="inline-flex size-7 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-foreground disabled:opacity-35"><RefreshCw className={cn("size-3.5", notices.loading && "animate-spin")} /></button>
          </div>
        </div>
        <div className="max-h-[400px] overflow-y-auto py-1" aria-live="polite" aria-busy={notices.loading}>
          {notices.loading && !list.length ? <div className="flex flex-col gap-2 p-3">{[0, 1, 2].map(i => <Skeleton key={i} className="h-10 rounded-md" />)}</div>
            : notices.error ? <p className="m-3 text-xs text-destructive">{notices.error}</p>
            : shown.length ? shown.map(item => {
              const isUnread = Boolean(item.time && item.time > seenAt);
              return <button key={item.key} type="button" onClick={() => go(item.path)} title={item.content} className="flex w-full items-start gap-2.5 px-4 py-2.5 text-left transition-colors hover:bg-accent/70">
                <span className={cn("mt-[7px] size-1.5 shrink-0 rounded-full", item.severity === "warn" ? "bg-warning" : "bg-destructive")} aria-label={SEVERITY[item.severity].label} />
                <span className="min-w-0 flex-1">
                  <span className={cn("block truncate text-[13px] leading-5", isUnread ? "font-medium text-foreground" : "text-foreground/80")}>{item.content}</span>
                  <span className="mt-0.5 block truncate text-xs text-muted-foreground">{item.source.replace(/^data-watch-/, "")}</span>
                </span>
                {item.time && <time dateTime={item.time} title={item.time} className="mt-0.5 shrink-0 text-xs tabular-nums text-muted-foreground">{relativeTime(item.time)}</time>}
              </button>;
            }) : <p role="status" className="py-10 text-center text-[13px] text-muted-foreground">暂无需要关注的提醒</p>}
        </div>
        {canReadLogs && <button type="button" onClick={() => go("/logs/system")} className="flex h-10 items-center justify-center gap-1 border-t border-border/60 text-xs text-muted-foreground transition-colors hover:text-foreground">查看全部日志<ArrowRight className="size-3.5" /></button>}
      </PopoverContent>
    </Popover>
  </>;
}
