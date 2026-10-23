import { useCallback, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { AlertTriangle, ArrowLeftRight, BellRing, CalendarDays, CircleAlert, CircleCheck, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Database, Download, FileClock, LoaderCircle, RefreshCw, Search, Trash2 } from "lucide-react";
import { Area, Bar, CartesianGrid, ComposedChart, Line, XAxis, YAxis } from "recharts";
import { PieCenter } from "@/components/charts/pie-center";
import { PieChart } from "@/components/charts/pie-chart";
import { PieSlice } from "@/components/charts/pie-slice";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { useAuth } from "@/contexts/AuthContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { backendClient } from "@/lib/backend-client";
import type { LogCleanupPayload, SystemLogItem, SystemLogQuery } from "@/types/log";

const PAGE_SIZE = 10;

const levelOptions = ["DEBUG", "INFO", "WARN", "ERROR", "CRITICAL"] as const;
const typeOptions = [
  ["system", "系统"], ["auth", "认证"], ["operation", "操作"], ["collect", "采集"],
  ["storage", "存储"], ["frontend", "前端"], ["security", "安全"], ["alarm", "告警"],
] as const;
const typeName = Object.fromEntries(typeOptions);

const trendConfig = {
  info: { label: "INFO", color: "var(--brand)" },
  warn: { label: "WARN", color: "var(--warning)" },
  error: { label: "ERROR", color: "var(--destructive)" },
  critical: { label: "CRITICAL", color: "var(--destructive)" },
} satisfies ChartConfig;
type TrendKey = keyof typeof trendConfig;

// 概念稿类型占比配色：品牌蓝三档 + 成功 / 警告 / 灰
const TYPE_PIE_COLORS = [
  "var(--brand)",
  "color-mix(in oklab, var(--brand) 62%, var(--card))",
  "color-mix(in oklab, var(--brand) 36%, var(--card))",
  "var(--success)",
  "var(--warning)",
  "var(--muted-foreground)",
  "color-mix(in oklab, var(--success) 50%, var(--card))",
  "color-mix(in oklab, var(--muted-foreground) 45%, var(--card))",
];

// 概念稿尺寸：控件 32px、正文 13px
const btnCls = "h-8 gap-1.5 bg-background px-3 text-[13px] [&_svg:not([class*='size-'])]:size-3.5";
const priCls = "h-8 gap-1.5 bg-brand px-3 text-[13px] text-brand-foreground hover:bg-brand/90 [&_svg:not([class*='size-'])]:size-3.5";
const fieldCls = "h-8 bg-background text-[13px] md:text-[13px]";

function apiTime(value?: string) {
  if (!value) return undefined;
  const normalized = value.replace("T", " ");
  return normalized.length === 16 ? `${normalized}:00` : normalized;
}

function formatTime(value?: string) {
  if (!value) return "--";
  return value.replace("T", " ").slice(0, 19);
}

function formatTableTime(value?: string) {
  if (!value) return "--";
  const normalized = value.includes("T") ? value : value.replace(" ", "T");
  const date = new Date(normalized);
  if (Number.isNaN(date.getTime())) return formatTime(value);
  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  }).format(date).replace(/\//g, "-");
}

function formatCompactNumber(value: number) {
  return new Intl.NumberFormat("zh-CN", {
    notation: "compact",
    maximumFractionDigits: 1,
  }).format(value);
}

function splitTableTime(value?: string): [string, string] {
  const text = formatTableTime(value);
  const [date, time] = text.split(" ");
  return time ? [time, date] : [text, ""];
}

const levelBadgeCls = "inline-flex h-[22px] items-center gap-1 rounded-md border px-2 font-mono text-[11.5px] font-semibold tracking-[0.02em] [&_svg]:size-3";

function logLevelBadge(level: SystemLogItem["level"]) {
  if (level === "CRITICAL") {
    return <span className={`${levelBadgeCls} border-transparent bg-destructive text-white`}><CircleAlert />{level}</span>;
  }
  if (level === "ERROR") {
    return <span className={`${levelBadgeCls} border-destructive/25 bg-destructive-soft text-destructive`}><CircleAlert />{level}</span>;
  }
  if (level === "WARN") {
    return <span className={`${levelBadgeCls} border-warning/25 bg-warning-soft text-warning`}><BellRing />{level}</span>;
  }
  if (level === "DEBUG") {
    return <span className={`${levelBadgeCls} border-border bg-transparent text-muted-foreground`}><LoaderCircle />{level}</span>;
  }
  return <span className={`${levelBadgeCls} border-success/25 bg-success-soft text-success`}><CircleCheck />{level}</span>;
}

function StatCard({
  title,
  value,
  unit,
  detail,
  icon: Icon,
  loading,
  tone,
}: {
  title: string;
  value: string;
  unit?: string;
  detail: string;
  icon?: typeof Database;
  loading: boolean;
  tone?: "warning";
}) {
  return (
    <Card className="min-h-[108px] gap-0 p-4">
      <div className={`flex items-center gap-2 text-[13px] ${tone === "warning" ? "text-warning" : "text-muted-foreground"}`}>
        {Icon ? <Icon className="size-[15px] shrink-0" /> : <span className="relative flex size-2 shrink-0"><span className="absolute inset-0 animate-ping rounded-full bg-warning opacity-60" /><span className="relative size-2 rounded-full bg-warning" /></span>}
        <span className="truncate">{title}</span>
      </div>
      {loading ? <Skeleton className="mt-2.5 h-7 w-24" /> : (
        <p className="mt-2.5 truncate text-[26px] leading-[1.15] font-semibold tracking-[-0.03em] tabular-nums">
          {value}
          {unit && <small className="ml-[3px] text-[13px] font-medium tracking-normal text-muted-foreground">{unit}</small>}
        </p>
      )}
      <p className="mt-0.5 truncate text-xs text-muted-foreground tabular-nums">{detail}</p>
    </Card>
  );
}

export function SystemLogPage() {
  const { auth } = useAuth();
  const token = auth?.backendToken;
  const [mode, setMode] = useState<"system" | "db">("system");
  const [draft, setDraft] = useState<SystemLogQuery>({ pageNo: 1, pageSize: PAGE_SIZE });
  const [query, setQuery] = useState<SystemLogQuery>({ pageNo: 1, pageSize: PAGE_SIZE });
  const [detail, setDetail] = useState<SystemLogItem | null>(null);
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const [cleanupOpen, setCleanupOpen] = useState(false);
  const [debugOpen, setDebugOpen] = useState(false);
  const [cleanup, setCleanup] = useState<LogCleanupPayload>({ mode: "policy", force: false });
  const [debugEnabled, setDebugEnabled] = useState(true);
  const [debugDuration, setDebugDuration] = useState(60);
  const [debugReason, setDebugReason] = useState("");
  const [selectedFile, setSelectedFile] = useState("");
  const [tailLines, setTailLines] = useState(300);
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);
  const [hiddenSeries, setHiddenSeries] = useState<Set<TrendKey>>(new Set());

  const loadLogs = useCallback((activeToken: string, signal: AbortSignal) => backendClient.systemLogList(activeToken, query, signal), [query]);
  const logs = useBackendQuery(loadLogs, { enabled: mode === "system" });
  const loadStats = useCallback((activeToken: string, signal: AbortSignal) => backendClient.logStats(activeToken, signal), []);
  const stats = useBackendQuery(loadStats, { intervalMs: 30_000 });
  const loadFiles = useCallback((activeToken: string, signal: AbortSignal) => backendClient.tdengineLogFiles(activeToken, signal), []);
  const files = useBackendQuery(loadFiles, { enabled: mode === "db" });
  const loadTail = useCallback((activeToken: string, signal: AbortSignal) => backendClient.tdengineLogTail(activeToken, selectedFile, tailLines, signal), [selectedFile, tailLines]);
  const tail = useBackendQuery(loadTail, { enabled: mode === "db" && Boolean(selectedFile) });

  useEffect(() => {
    if (!selectedFile && files.data?.[0]) setSelectedFile(files.data[0].name);
  }, [files.data, selectedFile]);

  const advancedCount = useMemo(
    () => [draft.kind, draft.level, draft.source, draft.serviceName, draft.deviceCode, draft.operatorName, draft.requestId, draft.traceId, draft.resultStatus].filter(Boolean).length,
    [draft],
  );
  const totalPages = Math.max(1, Math.ceil((logs.data?.total ?? 0) / (query.pageSize ?? PAGE_SIZE)));
  const currentPage = query.pageNo ?? 1;
  const tailText = Array.isArray(tail.data?.lines) ? tail.data.lines.join("\n") : tail.data?.lines ?? "";

  const typePieData = useMemo(
    () =>
      (stats.data?.typeRatio ?? []).map((item, index) => ({
        label: typeName[item.type] ?? item.type,
        value: item.count,
        color: TYPE_PIE_COLORS[index % TYPE_PIE_COLORS.length],
      })),
    [stats.data?.typeRatio],
  );

  const trendData = stats.data?.levelTrend ?? [];
  const typeTotal = typePieData.reduce((sum, item) => sum + item.value, 0);

  function toggleSeries(key: TrendKey) {
    setHiddenSeries((current) => { const next = new Set(current); if (next.has(key)) next.delete(key); else next.add(key); return next; });
  }

  function applySearch() {
    setQuery({ ...draft, startTime: apiTime(draft.startTime), endTime: apiTime(draft.endTime), pageNo: 1, pageSize: PAGE_SIZE });
    setAdvancedOpen(false);
  }

  function resetSearch() {
    const empty = { pageNo: 1, pageSize: PAGE_SIZE } satisfies SystemLogQuery;
    setDraft(empty);
    setQuery(empty);
  }

  function changePage(pageNo: number) {
    setQuery((value) => ({ ...value, pageNo }));
  }

  function exportRows() {
    const rows = logs.data?.list ?? [];
    if (!rows.length) return toast.info("当前没有可导出的日志");
    const csv = ["时间,级别,类型,来源,服务,设备,操作人,内容", ...rows.map((row) => [row.time, row.level, typeName[row.type] ?? row.type, row.source, row.serviceName, row.deviceCode ?? "", row.operatorName ?? "", row.content].map((cell) => `"${String(cell).replace(/"/g, '""')}"`).join(","))].join("\r\n");
    const link = document.createElement("a");
    link.href = URL.createObjectURL(new Blob([`\ufeff${csv}`], { type: "text/csv;charset=utf-8" }));
    link.download = `system-logs-${Date.now()}.csv`;
    link.click();
    URL.revokeObjectURL(link.href);
  }

  async function submitDebug() {
    if (!token) return;
    if (debugEnabled && !debugReason.trim()) return toast.error("请填写开启 DEBUG 的原因");
    try {
      await backendClient.toggleLogDebug(token, { enabled: debugEnabled, durationMinutes: debugEnabled ? debugDuration : undefined, reason: debugEnabled ? debugReason.trim() : undefined });
      toast.success(debugEnabled ? "DEBUG 已开启" : "DEBUG 已关闭");
      setDebugOpen(false);
      stats.refresh();
    } catch { toast.error("DEBUG 设置失败"); }
  }

  async function submitCleanup() {
    if (!token) return;
    try {
      const result = await backendClient.cleanupLogs(token, { ...cleanup, beforeTime: apiTime(cleanup.beforeTime), startTime: apiTime(cleanup.startTime), endTime: apiTime(cleanup.endTime) });
      toast.success(`清理完成，共删除 ${result.deletedOperationLogs + result.deletedSystemLogs} 条日志`);
      setCleanupOpen(false);
      logs.refresh();
      stats.refresh();
    } catch { toast.error("日志清理失败"); }
  }

  const summary = stats.data;
  const usedPercent = summary?.maxTotalMb ? Math.round(((summary.totalSizeMb ?? 0) / summary.maxTotalMb) * 100) : null;
  const statCards = [
    { title: "今日日志", value: (summary?.todayTotal ?? 0).toLocaleString(), detail: `较昨日 ${(summary?.todayTotal ?? 0) - (summary?.yesterdayTotal ?? 0) >= 0 ? "+" : ""}${(summary?.todayTotal ?? 0) - (summary?.yesterdayTotal ?? 0)} 条`, icon: FileClock },
    { title: "今日异常", value: ((summary?.todayWarnTotal ?? 0) + (summary?.todayErrorTotal ?? 0) + (summary?.todayCriticalTotal ?? 0)).toLocaleString(), detail: `WARN ${summary?.todayWarnTotal ?? 0} / ERROR ${summary?.todayErrorTotal ?? 0}`, icon: AlertTriangle },
    summary?.debugEnabled
      ? { title: "DEBUG 记录中", value: summary.debugRemainingSeconds.toLocaleString(), unit: "秒", detail: "剩余秒数", tone: "warning" as const }
      : { title: "DEBUG 已关闭", value: "—", detail: "点击下方开关设置", icon: LoaderCircle },
    { title: "日志容量", value: Math.round(summary?.totalSizeMb ?? 0).toLocaleString(), unit: "MB", detail: `上限 ${(summary?.maxTotalMb ?? 0).toLocaleString()} MB${usedPercent === null ? "" : ` · 已用 ${usedPercent}%`}`, icon: Database },
    { title: "最近清理", value: (summary?.lastCleanupDeletedTotal ?? 0).toLocaleString(), detail: summary?.lastCleanupTime ?? "暂无记录", icon: Trash2 },
  ];

  return (
    <div className="flex min-h-full w-full flex-col gap-4">
      <div className="grid shrink-0 grid-cols-5 gap-4">
        {statCards.map((item) => <StatCard key={item.title} {...item} loading={stats.loading} />)}
      </div>

      <div className="grid shrink-0 grid-cols-[minmax(0,1.55fr)_minmax(320px,1fr)] gap-4">
        <Card className="gap-0 py-0">
          <div className="flex items-start gap-3 px-5 pt-5">
            <div className="min-w-0">
              <p className="text-sm leading-none font-semibold">日志级别趋势</p>
              <p className="mt-1.5 text-[13px] text-muted-foreground">最近 24 小时各级别日志数量变化</p>
            </div>
            <Button size="icon" variant="ghost" className="ml-auto size-7 text-muted-foreground" title="刷新统计" onClick={stats.refresh}>
              <RefreshCw className="size-3.5" />
            </Button>
          </div>
          <div className="px-5 pt-4 pb-5">
            <div className="mb-2.5 flex items-center gap-3">
              <div className="flex flex-wrap gap-1.5">
                {(Object.keys(trendConfig) as TrendKey[]).map((key) => (
                  <button key={key} type="button" onClick={() => toggleSeries(key)} className={`inline-flex h-7 items-center gap-1.5 rounded-full border px-2.5 text-xs transition-opacity hover:bg-muted ${hiddenSeries.has(key) ? "opacity-45" : ""}`}>
                    <i className={`size-2 rounded-full ${key === "critical" ? "bg-[repeating-linear-gradient(45deg,var(--destructive)_0_2px,transparent_2px_4px)] shadow-[inset_0_0_0_1px_var(--destructive)]" : ""}`} style={key === "critical" ? undefined : { background: trendConfig[key].color }} />
                    {trendConfig[key].label}
                  </button>
                ))}
              </div>
              <span className="ml-auto text-[11.5px] text-muted-foreground">左轴 INFO · 右轴 WARN / ERROR（条/小时）</span>
            </div>
            {trendData.length ? (
              <ChartContainer config={trendConfig} className="aspect-auto h-[250px] w-full">
                <ComposedChart data={trendData} margin={{ left: 0, right: 0, top: 8, bottom: 0 }} accessibilityLayer barCategoryGap="38%">
                  <defs>
                    <linearGradient id="fillLogInfo" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor="var(--color-info)" stopOpacity={0.3} />
                      <stop offset="100%" stopColor="var(--color-info)" stopOpacity={0} />
                    </linearGradient>
                    <linearGradient id="fillLogWarn" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor="var(--color-warn)" stopOpacity={0.22} />
                      <stop offset="100%" stopColor="var(--color-warn)" stopOpacity={0} />
                    </linearGradient>
                    <pattern id="fillLogCritical" width="4" height="4" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
                      <line x1="0" y1="0" x2="0" y2="4" stroke="var(--color-critical)" strokeWidth={2} />
                    </pattern>
                  </defs>
                  <CartesianGrid vertical={false} strokeDasharray="3 4" />
                  <XAxis dataKey="time" tickLine={false} axisLine={false} tickMargin={8} interval={3} fontSize={10.5} tickFormatter={(value, index) => (index === trendData.length - 1 ? "现在" : String(value))} />
                  <YAxis yAxisId="info" domain={[0, "auto"]} tickLine={false} axisLine={false} width={40} fontSize={10.5} tickCount={4} tickFormatter={(value) => formatCompactNumber(Number(value))} />
                  <YAxis yAxisId="critical" orientation="right" hide width={0} domain={[0, 6]} />
                  <YAxis yAxisId="alert" orientation="right" domain={[0, "auto"]} tickLine={false} axisLine={false} width={28} fontSize={10.5} tickCount={4} />
                  <ChartTooltip
                    cursor={{ stroke: "var(--muted-foreground)", strokeOpacity: 0.5, strokeDasharray: "3 3" }}
                    content={<ChartTooltipContent indicator="dot" labelFormatter={(label) => { const index = trendData.findIndex((item) => item.time === label); const next = trendData[index + 1]?.time ?? "现在"; return `${label} – ${next}`; }} />}
                  />
                  {!hiddenSeries.has("info") && <Area yAxisId="info" dataKey="info" type="monotone" fill="url(#fillLogInfo)" stroke="var(--color-info)" strokeWidth={2} activeDot={{ r: 4.5, strokeWidth: 2.5, stroke: "var(--card)" }} />}
                  {!hiddenSeries.has("warn") && <Area yAxisId="alert" dataKey="warn" type="monotone" fill="url(#fillLogWarn)" stroke="var(--color-warn)" strokeWidth={2} />}
                  {!hiddenSeries.has("error") && <Line yAxisId="alert" dataKey="error" type="monotone" stroke="var(--color-error)" strokeWidth={2} strokeDasharray="4 4" dot={false} />}
                  {!hiddenSeries.has("critical") && <Bar yAxisId="critical" dataKey="critical" fill="url(#fillLogCritical)" stroke="var(--color-critical)" strokeWidth={1} radius={2} maxBarSize={8} />}
                </ComposedChart>
              </ChartContainer>
            ) : (
              <div className="flex h-[250px] items-center justify-center text-[13px] text-muted-foreground">
                {stats.loading ? "加载中…" : "暂无统计数据"}
              </div>
            )}
          </div>
        </Card>

        <Card className="gap-0 py-0">
          <div className="px-5 pt-5">
            <p className="text-sm leading-none font-semibold">日志类型占比</p>
            <p className="mt-1.5 text-[13px] text-muted-foreground">各类日志条数分布</p>
          </div>
          <div className="flex min-h-[280px] flex-1 items-center gap-5 px-5 pt-4 pb-5">
            {typePieData.length ? (
              <>
                <PieChart
                  data={typePieData}
                  hoveredIndex={hoveredIndex}
                  innerRadius={58}
                  padAngle={0.03}
                  startAngle={0}
                  endAngle={Math.PI * 2}
                  onHoverChange={setHoveredIndex}
                  size={180}
                  className="shrink-0"
                >
                  {typePieData.map((_, index) => <PieSlice index={index} key={index} />)}
                  <PieCenter defaultLabel="类型" formatOptions={{ notation: "compact", maximumFractionDigits: 1 }} />
                </PieChart>
                <div className="grid max-w-[300px] flex-1">
                  {typePieData.map((item, index) => (
                    <div
                      key={item.label}
                      className={`flex items-center gap-2 rounded-lg px-2 py-[5px] text-[13px] whitespace-nowrap transition-opacity hover:bg-muted ${hoveredIndex !== null && hoveredIndex !== index ? "opacity-50" : ""}`}
                      onMouseEnter={() => setHoveredIndex(index)}
                      onMouseLeave={() => setHoveredIndex(null)}
                    >
                      <i className="size-2 shrink-0 rounded-full" style={{ background: item.color }} />
                      {item.label}
                      <em className="ml-auto text-xs font-medium not-italic tabular-nums">{item.value.toLocaleString()}</em>
                      <span className="w-[34px] text-right text-[11.5px] text-muted-foreground tabular-nums">{typeTotal ? Math.round((item.value / typeTotal) * 100) : 0}%</span>
                    </div>
                  ))}
                </div>
              </>
            ) : (
              <p className="w-full text-center text-[13px] text-muted-foreground">{stats.loading ? "加载中…" : "暂无统计数据"}</p>
            )}
          </div>
        </Card>
      </div>

      <Card className="flex min-h-0 flex-1 flex-col gap-0 py-0">
        <div className="flex shrink-0 flex-wrap items-center gap-x-3 gap-y-3.5 px-5 pt-5">
          <div className="min-w-0">
            <p className="text-sm leading-none font-semibold">{mode === "system" ? "历史日志查询" : "数据库日志（TDengine 服务日志）"}</p>
            <p className="mt-1.5 text-[13px] text-muted-foreground">{mode === "system" ? `共 ${(logs.data?.total ?? 0).toLocaleString()} 条` : "只读查看服务端日志文件末尾"}</p>
          </div>
          <div className="ml-auto flex items-center gap-2">
            {mode === "system" && (
              <div className="relative">
                <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
                <Input placeholder="搜索关键字，回车查询" value={draft.keyword ?? ""} onChange={(event) => setDraft((value) => ({ ...value, keyword: event.target.value }))} onKeyDown={(event) => { if (event.key === "Enter") applySearch(); }} className={`${fieldCls} w-56 pl-8`} />
              </div>
            )}
            {mode === "system" && (
              <Button
                variant="outline"
                className={summary?.debugEnabled ? `${btnCls} border-warning/40 text-warning hover:text-warning` : btnCls}
                onClick={() => { setDebugEnabled(!summary?.debugEnabled); setDebugOpen(true); }}
              >
                {summary?.debugEnabled && <span className="relative flex size-2"><span className="absolute inset-0 animate-ping rounded-full bg-warning opacity-60" /><span className="relative size-2 rounded-full bg-warning" /></span>}
                {summary?.debugEnabled ? "DEBUG 开启中" : "DEBUG 开关"}
              </Button>
            )}
            <Button variant="outline" className={btnCls} onClick={() => setMode((value) => (value === "system" ? "db" : "system"))}>
              <ArrowLeftRight />
              {mode === "system" ? "切换到数据库日志" : "切换到系统日志"}
            </Button>
          </div>
          {mode === "system" && (
            <div className="flex basis-full flex-wrap items-center gap-2">
              <div className="relative">
                <CalendarDays className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
                <Input type="datetime-local" aria-label="开始时间" title="开始时间" value={draft.startTime ?? ""} onChange={(event) => setDraft((value) => ({ ...value, startTime: event.target.value }))} className={`${fieldCls} w-[184px] pl-8 tabular-nums`} />
              </div>
              <div className="relative">
                <CalendarDays className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
                <Input type="datetime-local" aria-label="结束时间" title="结束时间" value={draft.endTime ?? ""} onChange={(event) => setDraft((value) => ({ ...value, endTime: event.target.value }))} className={`${fieldCls} w-[184px] pl-8 tabular-nums`} />
              </div>
              <Select
                value={draft.type || "all"}
                onValueChange={(value) => setDraft((current) => ({ ...current, type: value === "all" ? "" : value as SystemLogQuery["type"] }))}
              >
                <SelectTrigger className="!h-8 w-[104px] bg-background text-[13px]" aria-label="日志类型">
                  <SelectValue placeholder="类型" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">全部类型</SelectItem>
                  {typeOptions.map(([value, label]) => (
                    <SelectItem key={value} value={value}>{label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <div className="ml-auto flex flex-wrap items-center justify-end gap-2">
                <Button variant="outline" className={btnCls} onClick={() => setAdvancedOpen(true)}>
                  高级查询{advancedCount > 0 && <span className="inline-grid h-[18px] min-w-[18px] place-items-center rounded-full bg-brand px-[5px] text-[11px] font-semibold text-brand-foreground">{advancedCount}</span>}
                </Button>
                <Button variant="outline" className={btnCls} onClick={() => setCleanupOpen(true)}><Trash2 />清理日志</Button>
                <Button variant="outline" className={btnCls} onClick={exportRows}><Download />导出日志</Button>
                <Button variant="outline" className={btnCls} onClick={resetSearch}>重置</Button>
                <Button className={priCls} onClick={applySearch}><Search />查询</Button>
              </div>
            </div>
          )}
        </div>
        <div className="flex min-h-0 flex-1 flex-col px-5 pt-4 pb-5">
          {mode === "system" ? (
            <>
              <div className="flex-1 overflow-hidden rounded-[10px] border">
                <TooltipProvider delayDuration={200}>
                  <Table className="w-full table-fixed text-[13px]">
                    <colgroup><col className="w-[150px]" /><col className="w-[112px]" /><col className="w-[80px]" /><col className="w-[14%]" /><col className="w-[11%]" /><col /><col className="w-[76px]" /></colgroup>
                    <TableHeader className="bg-[color-mix(in_oklab,var(--muted)_55%,var(--card))]">
                      <TableRow className="hover:bg-transparent">
                        {["时间", "级别", "类型", "来源", "设备", "内容"].map((label) => <TableHead key={label} className="h-10 px-3 text-[13px] font-medium text-muted-foreground">{label}</TableHead>)}
                        <TableHead className="h-10 px-3 text-center text-[13px] font-medium text-muted-foreground">操作</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {logs.loading && Array.from({ length: PAGE_SIZE }, (_, index) => (
                        <TableRow key={index}>
                          <TableCell colSpan={7} className="h-[52px] px-3"><Skeleton className="h-7 w-full" /></TableCell>
                        </TableRow>
                      ))}
                      {!logs.loading && (logs.data?.list ?? []).map((row) => {
                        const [clock, date] = splitTableTime(row.time);
                        const bar = row.level === "ERROR" || row.level === "CRITICAL" ? "shadow-[inset_3px_0_0_var(--destructive)]" : row.level === "WARN" ? "shadow-[inset_3px_0_0_var(--warning)]" : "";
                        return (
                        <TableRow key={`${row.kind}:${row.id}`}>
                          <TableCell className={`h-[52px] px-3 py-1 text-xs leading-[1.3] text-muted-foreground tabular-nums ${bar}`}>
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <span className="block cursor-help"><b className="block text-[12.5px] font-medium text-foreground">{clock}</b>{date}</span>
                              </TooltipTrigger>
                              <TooltipContent side="top">{formatTime(row.time)}</TooltipContent>
                            </Tooltip>
                          </TableCell>
                          <TableCell className="px-3 py-1">{logLevelBadge(row.level)}</TableCell>
                          <TableCell className="max-w-0 overflow-hidden px-3 py-1">
                            <Badge variant="outline" className="h-5 max-w-full truncate rounded-md bg-transparent px-2 text-xs font-normal text-muted-foreground">
                              {typeName[row.type] ?? row.type}
                            </Badge>
                          </TableCell>
                          <TableCell className="max-w-0 overflow-hidden px-3 py-1">
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <span className="block cursor-help">
                                  <span className="block truncate font-medium">{row.source || "-"}</span>
                                  {row.serviceName && <span className="block truncate text-[11.5px] leading-[1.3] text-muted-foreground">{row.serviceName}</span>}
                                </span>
                              </TooltipTrigger>
                              <TooltipContent side="top" className="max-w-sm whitespace-pre-wrap break-words text-left leading-relaxed">
                                {row.source || "-"}
                                {row.serviceName ? `\n服务：${row.serviceName}` : ""}
                              </TooltipContent>
                            </Tooltip>
                          </TableCell>
                          <TableCell className="max-w-0 overflow-hidden px-3 py-1">
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <span className={`block cursor-help truncate ${row.deviceCode ? "font-mono" : "text-muted-foreground"}`}>{row.deviceCode || row.operatorName || "-"}</span>
                              </TooltipTrigger>
                              <TooltipContent side="top" className="max-w-sm whitespace-pre-wrap break-words text-left leading-relaxed">
                                设备：{row.deviceCode || "-"}
                                {"\n"}操作人：{row.operatorName || "-"}
                              </TooltipContent>
                            </Tooltip>
                          </TableCell>
                          <TableCell className="max-w-0 overflow-hidden px-3 py-1">
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <span className="block w-full cursor-help truncate">{row.content || "-"}</span>
                              </TooltipTrigger>
                              <TooltipContent side="top" className="max-w-sm whitespace-pre-wrap break-words text-left leading-relaxed">
                                {row.content || "-"}
                              </TooltipContent>
                            </Tooltip>
                          </TableCell>
                          <TableCell className="px-3 py-1 text-center">
                            <Button variant="ghost" size="sm" className="h-7 px-2 text-[13px]" onClick={() => setDetail(row)}>详情</Button>
                          </TableCell>
                        </TableRow>
                        );
                      })}
                      {!logs.loading && !logs.data?.list?.length && (
                        <TableRow>
                          <TableCell colSpan={7} className="h-32 text-center text-muted-foreground">
                            {logs.error ?? "暂无日志"}
                          </TableCell>
                        </TableRow>
                      )}
                    </TableBody>
                  </Table>
                </TooltipProvider>
              </div>
              <div className="flex shrink-0 items-center gap-3 px-1 pt-3 text-[13px] text-muted-foreground">
                <span>共 {(logs.data?.total ?? 0).toLocaleString()} 条 · 每页 {PAGE_SIZE} 条</span>
                <div className="ml-auto flex items-center gap-2">
                  <span className="min-w-[110px] text-center font-medium text-foreground tabular-nums">第 {currentPage} / {totalPages} 页</span>
                  <Button variant="outline" size="icon" className="size-8 bg-background" disabled={currentPage <= 1} onClick={() => changePage(1)} aria-label="第一页">
                    <ChevronsLeft className="size-4" />
                  </Button>
                  <Button variant="outline" size="icon" className="size-8 bg-background" disabled={currentPage <= 1} onClick={() => changePage(currentPage - 1)} aria-label="上一页">
                    <ChevronLeft className="size-4" />
                  </Button>
                  <Button variant="outline" size="icon" className="size-8 bg-background" disabled={currentPage >= totalPages} onClick={() => changePage(currentPage + 1)} aria-label="下一页">
                    <ChevronRight className="size-4" />
                  </Button>
                  <Button variant="outline" size="icon" className="size-8 bg-background" disabled={currentPage >= totalPages} onClick={() => changePage(totalPages)} aria-label="最后一页">
                    <ChevronsRight className="size-4" />
                  </Button>
                </div>
              </div>
            </>
          ) : (
            <div className="flex min-h-0 flex-1 flex-col gap-3">
              <div className="flex shrink-0 flex-wrap gap-2">
                <Select value={selectedFile} onValueChange={setSelectedFile}>
                  <SelectTrigger className="w-72"><SelectValue placeholder="选择日志文件" /></SelectTrigger>
                  <SelectContent>
                    {(files.data ?? []).map((file) => (
                      <SelectItem key={file.name} value={file.name}>
                        {file.name}{file.sizeMb ? ` · ${file.sizeMb.toFixed(1)} MB` : ""}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Select value={String(tailLines)} onValueChange={(value) => setTailLines(Number(value))}>
                  <SelectTrigger className="w-32"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {[100, 300, 500, 1000].map((value) => (
                      <SelectItem key={value} value={String(value)}>末尾 {value} 行</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Button variant="outline" className={btnCls} onClick={tail.refresh}><RefreshCw />刷新</Button>
              </div>
              <pre className="h-0 min-h-96 flex-1 overflow-auto rounded-md bg-zinc-950 p-4 font-mono text-xs leading-6 text-zinc-200">
                {tail.loading ? "正在读取日志..." : (tail.error ?? tailText) || "请选择日志文件"}
              </pre>
            </div>
          )}
        </div>
      </Card>

      <Dialog open={Boolean(detail)} onOpenChange={(open) => { if (!open) setDetail(null); }}>
        <DialogContent className="flex max-h-[85vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-4xl">
          <DialogHeader className="shrink-0 px-4 pt-4 pr-12">
            <DialogTitle>日志详情</DialogTitle>
          </DialogHeader>
          {detail && (
            <div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-4 pb-4 text-sm">
              <div className="grid gap-4 rounded-md border p-4 sm:grid-cols-2">
                <div className="grid grid-cols-[7rem_1fr] gap-x-4 gap-y-2.5">
                  <span className="text-muted-foreground">时间</span><span className="break-all">{formatTime(detail.time)}</span>
                  <span className="text-muted-foreground">级别</span><span>{logLevelBadge(detail.level)}</span>
                  <span className="text-muted-foreground">类型</span><span>{typeName[detail.type] ?? detail.type}</span>
                  <span className="text-muted-foreground">来源</span><span className="break-all">{detail.source || "-"}</span>
                </div>
                <div className="grid grid-cols-[7rem_1fr] gap-x-4 gap-y-2.5">
                  <span className="text-muted-foreground">服务</span><span className="break-all">{detail.serviceName || "-"}</span>
                  <span className="text-muted-foreground">设备</span><span className="break-all">{detail.deviceCode || "-"}</span>
                  <span className="text-muted-foreground">操作人</span><span className="break-all">{detail.operatorName || "-"}</span>
                  <span className="text-muted-foreground">Request ID</span><span className="break-all font-mono text-xs">{detail.requestId || "-"}</span>
                  <span className="text-muted-foreground">Trace ID</span><span className="break-all font-mono text-xs">{detail.traceId || "-"}</span>
                </div>
              </div>
              <section>
                <h4 className="mb-2 font-medium">日志内容</h4>
                <pre className="max-h-48 overflow-auto whitespace-pre-wrap rounded-md bg-muted p-3 text-xs">{detail.content}</pre>
              </section>
              {detail.detail && (
                <section>
                  <h4 className="mb-2 font-medium">结构化详情</h4>
                  <pre className="max-h-64 overflow-auto rounded-md bg-muted p-3 text-xs">{JSON.stringify(detail.detail, null, 2)}</pre>
                </section>
              )}
              {detail.exceptionStack && (
                <section>
                  <h4 className="mb-2 font-medium">异常堆栈</h4>
                  <pre className="max-h-72 overflow-auto whitespace-pre-wrap rounded-md bg-zinc-950 p-3 text-xs text-zinc-200">{detail.exceptionStack}</pre>
                </section>
              )}
              <Button
                variant="outline"
                onClick={() => {
                  void navigator.clipboard.writeText(`${detail.time} | ${detail.level} | ${detail.content}`);
                  toast.success("日志已复制");
                }}
              >
                复制日志
              </Button>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={advancedOpen} onOpenChange={setAdvancedOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader><DialogTitle>高级查询</DialogTitle></DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            {([["来源", "source"], ["服务", "serviceName"], ["设备编号", "deviceCode"], ["操作人", "operatorName"], ["Request ID", "requestId"], ["Trace ID", "traceId"]] as const).map(([label, key]) => (
              <div key={key} className="space-y-2">
                <Label>{label}</Label>
                <Input
                  value={String(draft[key] ?? "")}
                  onChange={(event) => setDraft((value) => ({ ...value, [key]: event.target.value }))}
                />
              </div>
            ))}
            <div className="space-y-2">
              <Label>级别</Label>
              <Select
                value={draft.level || "all"}
                onValueChange={(value) => setDraft((current) => ({ ...current, level: value === "all" ? "" : value as SystemLogQuery["level"] }))}
              >
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">全部级别</SelectItem>
                  {levelOptions.map((level) => <SelectItem key={level} value={level}>{level}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>日志种类</Label>
              <Select
                value={draft.kind || "all"}
                onValueChange={(value) => setDraft((current) => ({ ...current, kind: value as SystemLogQuery["kind"] }))}
              >
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">全部</SelectItem>
                  <SelectItem value="system">系统运行</SelectItem>
                  <SelectItem value="operation">用户操作</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={resetSearch}>重置</Button>
            <Button onClick={applySearch}>查询</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={cleanupOpen} onOpenChange={setCleanupOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>清理日志</DialogTitle></DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>清理模式</Label>
              <Select
                value={cleanup.mode}
                onValueChange={(value) => setCleanup((current) => ({ ...current, mode: value as LogCleanupPayload["mode"] }))}
              >
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="policy">按策略清理</SelectItem>
                  <SelectItem value="beforeTime">清理指定时间之前</SelectItem>
                  <SelectItem value="range">清理时间范围内</SelectItem>
                </SelectContent>
              </Select>
            </div>
            {cleanup.mode === "beforeTime" && (
              <div className="space-y-2">
                <Label>截止时间</Label>
                <Input
                  type="datetime-local"
                  value={cleanup.beforeTime ?? ""}
                  onChange={(event) => setCleanup((current) => ({ ...current, beforeTime: event.target.value }))}
                />
              </div>
            )}
            {cleanup.mode === "range" && (
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label>开始时间</Label>
                  <Input
                    type="datetime-local"
                    value={cleanup.startTime ?? ""}
                    onChange={(event) => setCleanup((current) => ({ ...current, startTime: event.target.value }))}
                  />
                </div>
                <div className="space-y-2">
                  <Label>结束时间</Label>
                  <Input
                    type="datetime-local"
                    value={cleanup.endTime ?? ""}
                    onChange={(event) => setCleanup((current) => ({ ...current, endTime: event.target.value }))}
                  />
                </div>
              </div>
            )}
            <div className="flex items-center justify-between rounded-md border p-3">
              <div>
                <p className="text-sm font-medium">强制清理</p>
                <p className="text-xs text-muted-foreground">允许清理最近 24 小时日志</p>
              </div>
              <Switch checked={cleanup.force} onCheckedChange={(force) => setCleanup((current) => ({ ...current, force }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCleanupOpen(false)}>取消</Button>
            <Button variant="destructive" onClick={submitCleanup}>确认清理</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={debugOpen} onOpenChange={setDebugOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>DEBUG 日志开关</DialogTitle></DialogHeader>
          <div className="space-y-4">
            <div className="flex items-center justify-between rounded-md border p-3">
              <div>
                <p className="text-sm font-medium">开启 DEBUG</p>
                <p className="text-xs text-muted-foreground">排障完成后请及时关闭</p>
              </div>
              <Switch checked={debugEnabled} onCheckedChange={setDebugEnabled} />
            </div>
            {debugEnabled && (
              <>
                <div className="space-y-2">
                  <Label>开启时长</Label>
                  <Select value={String(debugDuration)} onValueChange={(value) => setDebugDuration(Number(value))}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {([[30, "30 分钟"], [60, "1 小时"], [240, "4 小时"], [480, "8 小时"]] as const).map(([value, label]) => (
                        <SelectItem key={value} value={String(value)}>{label}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>开启原因</Label>
                  <Textarea rows={3} value={debugReason} onChange={(event) => setDebugReason(event.target.value)} placeholder="请填写开启原因" />
                </div>
              </>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDebugOpen(false)}>取消</Button>
            <Button onClick={submitDebug}>{debugEnabled ? "开启 DEBUG" : "关闭 DEBUG"}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

export default SystemLogPage;
