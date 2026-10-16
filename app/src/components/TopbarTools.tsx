import { useCallback, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Bell, Search } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Field, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { BUILD_ROLE } from "@/config/build-role";
import { useAuth } from "@/contexts/AuthContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { backendClient } from "@/lib/backend-client";
import { agentClient } from "@/lib/agent-client";
import { filterNavigationGroups } from "@/lib/navigation-access";
import { recentNotifications, searchPages } from "@/lib/topbar-data";
import type { ModuleName } from "@/types";

interface Notice { key: string; title: string; content: string; time?: string; path: string }

export function TopbarTools() {
  const { auth } = useAuth();
  const navigate = useNavigate();
  const [searchOpen, setSearchOpen] = useState(false);
  const [noticeOpen, setNoticeOpen] = useState(false);
  const [keyword, setKeyword] = useState("");
  const canReadLogs = BUILD_ROLE !== "edge" && Boolean(auth?.allowedPaths?.includes("/logs/system"));
  const pages = useMemo(() => searchPages(filterNavigationGroups(auth?.allowedPaths), keyword, Boolean(auth?.allowedPaths?.includes("/settings"))), [auth?.allowedPaths, keyword]);
  const loadNotices = useCallback(async (token: string, signal: AbortSignal): Promise<Notice[]> => {
    if (canReadLogs) {
      const results = await Promise.all((["WARN", "ERROR", "CRITICAL"] as const).map(level =>
        backendClient.systemLogList(token, { level, pageNo: 1, pageSize: 20 }, signal)));
      return recentNotifications(results).map(item => ({ key: `${item.kind}:${item.id}`, title: item.level, content: item.content, time: item.time, path: "/logs/system" }));
    }
    const modules: ModuleName[] = BUILD_ROLE === "edge" ? ["collector", "storage", "uploader"] : BUILD_ROLE === "distributed-center" ? ["ingest", "writer"] : ["collector", "writer"];
    const results = await Promise.all(modules.map(name => agentClient.getProcessMetrics(name, signal)));
    return results.flatMap((item, index) => item.status === "running" ? [] : [{
      key: modules[index], title: modules[index],
      content: item.status === "stopped" ? "进程已停止，请确认是否符合运行计划。" : item.status === "crashed" ? "进程异常退出，请检查模块日志。" : "暂时无法确认进程状态。",
      path: BUILD_ROLE === "edge" ? `/edge/${modules[index]}` : `/collection/${modules[index]}`,
    }]);
  }, [canReadLogs]);
  const notices = useBackendQuery(loadNotices, { enabled: noticeOpen });
  function openPage(path: string) { setSearchOpen(false); setNoticeOpen(false); navigate(path); }

  return <>
    <Dialog open={searchOpen} onOpenChange={open => { setSearchOpen(open); if (open) setKeyword(""); }}>
      <DialogTrigger asChild><Button variant="ghost" size="icon" className="size-8 rounded-3xl" aria-label="搜索页面" title="搜索页面"><Search /></Button></DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader><DialogTitle>搜索页面</DialogTitle><DialogDescription>搜索当前账号可访问的页面，按 Enter 打开首个结果。</DialogDescription></DialogHeader>
        <form onSubmit={event => { event.preventDefault(); if (pages[0]) openPage(pages[0].path); }}>
          <Field><FieldLabel htmlFor="page-search">页面名称</FieldLabel><Input id="page-search" autoFocus value={keyword} onChange={event => setKeyword(event.target.value)} placeholder="例如：实时监控、设置" /></Field>
        </form>
        <nav aria-label="页面搜索结果" className="flex max-h-80 flex-col gap-1 overflow-y-auto">
          {pages.length ? pages.map(page => <Button key={page.path} type="button" variant="ghost" className="justify-between" onClick={() => openPage(page.path)}><span>{page.label}</span><span className="text-muted-foreground">{page.group}</span></Button>) : <p role="status">没有匹配的可访问页面。</p>}
        </nav>
      </DialogContent>
    </Dialog>
    <Dialog open={noticeOpen} onOpenChange={setNoticeOpen}>
      <DialogTrigger asChild><Button variant="ghost" size="icon" className="size-8 rounded-3xl" aria-label="通知" title="通知"><Bell /></Button></DialogTrigger>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader><DialogTitle>通知</DialogTitle><DialogDescription>{canReadLogs ? "最近 20 条警告、错误与严重异常日志。" : "本机模块的当前运行提醒。"} 打开时更新。</DialogDescription></DialogHeader>
        <div className="flex justify-end"><Button variant="outline" size="sm" disabled={notices.loading} onClick={notices.refresh}>刷新通知</Button></div>
        <div className="flex max-h-96 flex-col gap-3 overflow-y-auto" aria-live="polite" aria-busy={notices.loading}>
          {notices.loading ? <Skeleton className="h-28" /> : notices.error ? <Alert variant="destructive"><AlertDescription>{notices.error}，请重试。</AlertDescription></Alert> : notices.data?.length ? notices.data.map(item => <article key={item.key} className="flex flex-col gap-2 rounded-md border p-3">
            <div className="flex items-center justify-between gap-2"><Badge variant="outline">{item.title}</Badge>{item.time && <time className="text-xs text-muted-foreground">{item.time}</time>}</div>
            <p className="break-all">{item.content}</p>
            <Button variant="outline" size="sm" className="self-start" onClick={() => openPage(item.path)}>查看详情</Button>
          </article>) : <p role="status">当前没有需要关注的提醒。</p>}
        </div>
      </DialogContent>
    </Dialog>
  </>;
}
