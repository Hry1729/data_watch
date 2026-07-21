import { useCallback, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { AlertTriangle, BellRing, Bug, CircleAlert, CircleCheck, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Database, Download, FileClock, LoaderCircle, RefreshCw, Search, Trash2 } from "lucide-react";
import { Area, AreaChart, CartesianGrid, XAxis, YAxis } from "recharts";
import { PieCenter } from "@/components/charts/pie-center";
import { PieChart } from "@/components/charts/pie-chart";
import { PieSlice } from "@/components/charts/pie-slice";
import {
  Legend,
  LegendItem as LegendItemComponent,
  LegendLabel,
  LegendMarker,
} from "@/components/charts/legend";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
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
  info: { label: "INFO", color: "var(--chart-1)" },
  warn: { label: "WARN", color: "var(--warning)" },
  error: { label: "ERROR", color: "var(--destructive)" },
  critical: { label: "CRITICAL", color: "var(--chart-5)" },
} satisfies ChartConfig;

const TYPE_PIE_COLORS = [
  "var(--chart-1)",
  "var(--chart-2)",
  "var(--chart-3)",
  "var(--chart-4)",
  "var(--chart-5)",
];

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

function logLevelBadge(level: SystemLogItem["level"]) {
  if (level === "ERROR" || level === "CRITICAL") {
    return (
      <Badge variant="outline" className="border-destructive/30 bg-destructive/5 text-destructive">
        <CircleAlert className="size-3" />
        {level}
      </Badge>
    );
  }

  if (level === "WARN") {
    return (
      <Badge variant="outline" className="border-warning/30 bg-warning-soft text-warning">
        <BellRing className="size-3" />
        {level}
      </Badge>
    );
  }

  if (level === "DEBUG") {
    return (
      <Badge variant="outline" className="text-muted-foreground">
        <LoaderCircle className="size-3" />
        {level}
      </Badge>
    );
  }

  return (
    <Badge variant="outline" className="border-success/30 bg-success-soft text-success">
      <CircleCheck className="size-3" />
      {level}
    </Badge>
  );
}

function StatCard({
  title,
  value,
  detail,
  icon: Icon,
  loading,
}: {
  title: string;
  value: string;
  detail: string;
  icon: typeof Database;
  loading: boolean;
}) {
  return (
    <Card size="sm" className="border border-border bg-card shadow-xs">
      <CardContent className="flex min-h-24 items-center justify-between gap-3 py-2">
        <div className="min-w-0">
          <p className="text-xs text-muted-foreground">{title}</p>
          {loading ? <Skeleton className="mt-2 h-7 w-24" /> : <p className="mt-1 text-2xl font-semibold tabular-nums">{value}</p>}
          <p className="mt-1 truncate text-xs text-muted-foreground">{detail}</p>
        </div>
        <div className="flex size-10 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
          <Icon className="size-6" />
        </div>
      </CardContent>
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

  const loadLogs = useCallback((activeToken: string) => backendClient.systemLogList(activeToken, query), [query]);
  const logs = useBackendQuery(loadLogs, { enabled: mode === "system" });
  const loadStats = useCallback((activeToken: string) => backendClient.logStats(activeToken), []);
  const stats = useBackendQuery(loadStats, { intervalMs: 30_000 });
  const loadFiles = useCallback((activeToken: string) => backendClient.tdengineLogFiles(activeToken), []);
  const files = useBackendQuery(loadFiles, { enabled: mode === "db" });
  const loadTail = useCallback((activeToken: string) => backendClient.tdengineLogTail(activeToken, selectedFile, tailLines), [selectedFile, tailLines]);
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
  const statCards = [
    { title: "今日日志", value: (summary?.todayTotal ?? 0).toLocaleString(), detail: `较昨日 ${(summary?.todayTotal ?? 0) - (summary?.yesterdayTotal ?? 0) >= 0 ? "+" : ""}${(summary?.todayTotal ?? 0) - (summary?.yesterdayTotal ?? 0)} 条`, icon: FileClock },
    { title: "今日异常", value: String((summary?.todayWarnTotal ?? 0) + (summary?.todayErrorTotal ?? 0) + (summary?.todayCriticalTotal ?? 0)), detail: `WARN ${summary?.todayWarnTotal ?? 0} / ERROR ${summary?.todayErrorTotal ?? 0}`, icon: AlertTriangle },
    { title: summary?.debugEnabled ? "DEBUG 记录中" : "DEBUG 已关闭", value: summary?.debugEnabled ? String(summary.debugRemainingSeconds) : "-", detail: summary?.debugEnabled ? "剩余秒数" : "点击下方开关设置", icon: Bug },
    { title: "日志容量", value: `${Math.round(summary?.totalSizeMb ?? 0)} MB`, detail: `上限 ${summary?.maxTotalMb ?? 0} MB`, icon: Database },
    { title: "最近清理", value: (summary?.lastCleanupDeletedTotal ?? 0).toLocaleString(), detail: summary?.lastCleanupTime ?? "暂无记录", icon: Trash2 },
  ];

  return (
    <div className="mx-auto w-full max-w-[1680px] space-y-4 p-4 md:p-6">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {statCards.map((item) => <StatCard key={item.title} {...item} loading={stats.loading} />)}
      </div>

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1.55fr)_minmax(320px,1fr)]">
        <Card className="@container/card border border-border">
          <CardHeader>
            <CardTitle>日志级别趋势</CardTitle>
            <CardDescription>最近 24 小时各级别日志数量变化</CardDescription>
            <CardAction>
              <Button size="icon" variant="ghost" title="刷新统计" onClick={stats.refresh}>
                <RefreshCw className="size-4" />
              </Button>
            </CardAction>
          </CardHeader>
          <CardContent className="px-2 pt-2 sm:px-6">
            {trendData.length ? (
              <ChartContainer config={trendConfig} className="aspect-auto h-[280px] w-full">
                <AreaChart data={trendData} margin={{ left: 0, right: 0, top: 8 }} accessibilityLayer>
                  <defs>
                    <linearGradient id="fillLogInfo" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="var(--color-info)" stopOpacity={0.85} />
                      <stop offset="95%" stopColor="var(--color-info)" stopOpacity={0.08} />
                    </linearGradient>
                    <linearGradient id="fillLogWarn" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="var(--color-warn)" stopOpacity={0.7} />
                      <stop offset="95%" stopColor="var(--color-warn)" stopOpacity={0.06} />
                    </linearGradient>
                    <linearGradient id="fillLogError" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="var(--color-error)" stopOpacity={0.7} />
                      <stop offset="95%" stopColor="var(--color-error)" stopOpacity={0.06} />
                    </linearGradient>
                    <linearGradient id="fillLogCritical" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="var(--color-critical)" stopOpacity={0.65} />
                      <stop offset="95%" stopColor="var(--color-critical)" stopOpacity={0.06} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid vertical={false} />
                  <XAxis dataKey="time" tickLine={false} axisLine={false} tickMargin={8} minTickGap={32} />
                  <YAxis
                    domain={[0, "auto"]}
                    tickLine={false}
                    axisLine={false}
                    width={48}
                    tickFormatter={(value) => formatCompactNumber(Number(value))}
                  />
                  <ChartTooltip cursor={false} content={<ChartTooltipContent indicator="dot" />} />
                  <Area dataKey="info" type="monotone" fill="url(#fillLogInfo)" stroke="var(--color-info)" strokeWidth={2} />
                  <Area dataKey="warn" type="monotone" fill="url(#fillLogWarn)" stroke="var(--color-warn)" strokeWidth={2} />
                  <Area dataKey="error" type="monotone" fill="url(#fillLogError)" stroke="var(--color-error)" strokeWidth={2} />
                  <Area dataKey="critical" type="monotone" fill="url(#fillLogCritical)" stroke="var(--color-critical)" strokeWidth={2} />
                  <ChartLegend content={<ChartLegendContent />} />
                </AreaChart>
              </ChartContainer>
            ) : (
              <div className="flex h-[280px] items-center justify-center text-sm text-muted-foreground">
                {stats.loading ? "加载中…" : "暂无统计数据"}
              </div>
            )}
          </CardContent>
        </Card>

        <Card className="@container/card border border-border">
          <CardHeader>
            <CardTitle>日志类型占比</CardTitle>
            <CardDescription>各类日志条数分布</CardDescription>
          </CardHeader>
          <CardContent className="flex min-h-[280px] items-center justify-center">
            {typePieData.length ? (
              <div className="flex w-full max-w-lg flex-col items-center justify-center gap-5">
                <PieChart
                  data={typePieData}
                  hoveredIndex={hoveredIndex}
                  innerRadius={45}
                  onHoverChange={setHoveredIndex}
                  size={180}
                >
                  {typePieData.map((_, index) => <PieSlice index={index} key={index} />)}
                  <PieCenter defaultLabel="类型" />
                </PieChart>
                <Legend
                  hoveredIndex={hoveredIndex}
                  items={typePieData}
                  onHoverChange={setHoveredIndex}
                  className="w-full max-w-sm flex-row flex-wrap justify-center gap-x-4 gap-y-1"
                >
                  <LegendItemComponent className="flex items-center gap-3">
                    <LegendMarker />
                    <LegendLabel />
                  </LegendItemComponent>
                </Legend>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">{stats.loading ? "加载中…" : "暂无统计数据"}</p>
            )}
          </CardContent>
        </Card>
      </div>

      {/* 查询工具栏 */}
      {mode === "system" && (
        <div className="flex flex-wrap items-center gap-2">
          <Input
            type="datetime-local"
            aria-label="开始时间"
            title="开始时间"
            value={draft.startTime ?? ""}
            onChange={(event) => setDraft((value) => ({ ...value, startTime: event.target.value }))}
            className="h-7 w-40"
          />
          <Input
            type="datetime-local"
            aria-label="结束时间"
            title="结束时间"
            value={draft.endTime ?? ""}
            onChange={(event) => setDraft((value) => ({ ...value, endTime: event.target.value }))}
            className="h-7 w-40"
          />
          <Select
            value={draft.type || "all"}
            onValueChange={(value) => setDraft((current) => ({ ...current, type: value === "all" ? "" : value as SystemLogQuery["type"] }))}
          >
            <SelectTrigger className="!h-7 w-24" aria-label="日志类型">
              <SelectValue placeholder="类型" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">全部类型</SelectItem>
              {typeOptions.map(([value, label]) => (
                <SelectItem key={value} value={value}>{label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Input
            placeholder="关键字"
            value={draft.keyword ?? ""}
            onChange={(event) => setDraft((value) => ({ ...value, keyword: event.target.value }))}
            onKeyDown={(event) => { if (event.key === "Enter") applySearch(); }}
            className="h-7 w-32"
          />
          <div className="ml-auto flex flex-wrap items-center justify-end gap-2">
            <Button variant="outline" onClick={() => setAdvancedOpen(true)}>
              高级查询{advancedCount > 0 && <Badge className="ml-1">{advancedCount}</Badge>}
            </Button>
            <Button variant="outline" onClick={() => setCleanupOpen(true)}><Trash2 className="size-3.5" />清理日志</Button>
            <Button variant="outline" onClick={exportRows}><Download className="size-3.5" />导出日志</Button>
            <Button variant="outline" onClick={resetSearch}>重置</Button>
            <Button onClick={applySearch}><Search className="size-3.5" />查询</Button>
          </div>
        </div>
      )}

      <Card className="border border-border">
        <CardHeader>
          <CardTitle>
            {mode === "system" ? "历史日志查询" : "数据库日志（TDengine 服务日志）"}
          </CardTitle>
          <CardAction>
            <div className="flex items-center gap-2">
              {mode === "system" && (
                <Button
                  variant={summary?.debugEnabled ? "secondary" : "outline"}
                  onClick={() => { setDebugEnabled(!summary?.debugEnabled); setDebugOpen(true); }}
                >
                  {summary?.debugEnabled ? "DEBUG 开启中" : "DEBUG 开关"}
                </Button>
              )}
              <Button
                variant="outline"
                onClick={() => setMode((value) => (value === "system" ? "db" : "system"))}
              >
                {mode === "system" ? "切换到数据库日志" : "切换到系统日志"}
              </Button>
            </div>
          </CardAction>
        </CardHeader>
        <CardContent>
          {mode === "system" ? (
            <>
              <div className="overflow-hidden rounded-lg border">
                <TooltipProvider delayDuration={200}>
                  <Table className="w-full table-fixed">
                    <TableHeader className="bg-muted/50">
                      <TableRow className="h-10 hover:bg-transparent">
                        <TableHead className="w-[168px] text-center">时间</TableHead>
                        <TableHead className="w-[96px] text-center">级别</TableHead>
                        <TableHead className="w-[88px] text-center">类型</TableHead>
                        <TableHead className="w-[11%] text-center">来源</TableHead>
                        <TableHead className="w-[11%] text-center">设备</TableHead>
                        <TableHead className="text-center">内容</TableHead>
                        <TableHead className="w-[72px] text-center">操作</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {logs.loading && Array.from({ length: PAGE_SIZE }, (_, index) => (
                        <TableRow key={index} className="h-12">
                          <TableCell colSpan={7}><Skeleton className="h-7 w-full" /></TableCell>
                        </TableRow>
                      ))}
                      {!logs.loading && (logs.data?.list ?? []).map((row) => (
                        <TableRow key={`${row.kind}:${row.id}`} className="h-12">
                          <TableCell className="whitespace-nowrap text-center text-xs tabular-nums text-muted-foreground">
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <span className="block cursor-help">{formatTableTime(row.time)}</span>
                              </TooltipTrigger>
                              <TooltipContent side="top">{formatTime(row.time)}</TooltipContent>
                            </Tooltip>
                          </TableCell>
                          <TableCell className="max-w-0 overflow-hidden text-center">{logLevelBadge(row.level)}</TableCell>
                          <TableCell className="max-w-0 overflow-hidden text-center">
                            <Badge variant="outline" className="max-w-full truncate font-normal text-muted-foreground">
                              {typeName[row.type] ?? row.type}
                            </Badge>
                          </TableCell>
                          <TableCell className="max-w-0 overflow-hidden text-center">
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <span className="block cursor-help truncate font-medium">{row.source || "-"}</span>
                              </TooltipTrigger>
                              <TooltipContent side="top" className="max-w-sm whitespace-pre-wrap break-words text-left leading-relaxed">
                                {row.source || "-"}
                                {row.serviceName ? `\n服务：${row.serviceName}` : ""}
                              </TooltipContent>
                            </Tooltip>
                          </TableCell>
                          <TableCell className="max-w-0 overflow-hidden text-center">
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <span className="block cursor-help truncate">{row.deviceCode || row.operatorName || "-"}</span>
                              </TooltipTrigger>
                              <TooltipContent side="top" className="max-w-sm whitespace-pre-wrap break-words text-left leading-relaxed">
                                设备：{row.deviceCode || "-"}
                                {"\n"}操作人：{row.operatorName || "-"}
                              </TooltipContent>
                            </Tooltip>
                          </TableCell>
                          <TableCell className="max-w-0 overflow-hidden px-3 text-center">
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <span className="block w-full cursor-help truncate">{row.content || "-"}</span>
                              </TooltipTrigger>
                              <TooltipContent side="top" className="max-w-sm whitespace-pre-wrap break-words text-left leading-relaxed">
                                {row.content || "-"}
                              </TooltipContent>
                            </Tooltip>
                          </TableCell>
                          <TableCell className="text-center">
                            <Button variant="ghost" size="sm" onClick={() => setDetail(row)}>详情</Button>
                          </TableCell>
                        </TableRow>
                      ))}
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
              <div className="mt-3 flex flex-col gap-3 px-1 text-sm sm:flex-row sm:items-center sm:justify-between">
                <span className="text-muted-foreground">共 {logs.data?.total ?? 0} 条 · 每页 {PAGE_SIZE} 条</span>
                <div className="flex flex-wrap items-center gap-3 sm:justify-end">
                  <span className="min-w-24 text-center font-medium tabular-nums">第 {currentPage} / {totalPages} 页</span>
                  <div className="flex items-center gap-2">
                    <Button variant="outline" size="icon" className="size-8" disabled={currentPage <= 1} onClick={() => changePage(1)} aria-label="第一页">
                      <ChevronsLeft className="size-4" />
                    </Button>
                    <Button variant="outline" size="icon" className="size-8" disabled={currentPage <= 1} onClick={() => changePage(currentPage - 1)} aria-label="上一页">
                      <ChevronLeft className="size-4" />
                    </Button>
                    <Button variant="outline" size="icon" className="size-8" disabled={currentPage >= totalPages} onClick={() => changePage(currentPage + 1)} aria-label="下一页">
                      <ChevronRight className="size-4" />
                    </Button>
                    <Button variant="outline" size="icon" className="size-8" disabled={currentPage >= totalPages} onClick={() => changePage(totalPages)} aria-label="最后一页">
                      <ChevronsRight className="size-4" />
                    </Button>
                  </div>
                </div>
              </div>
            </>
          ) : (
            <div className="space-y-3">
              <div className="flex flex-wrap gap-2">
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
                <Button variant="outline" onClick={tail.refresh}><RefreshCw className="size-4" />刷新</Button>
              </div>
              <pre className="max-h-[560px] min-h-96 overflow-auto rounded-md bg-zinc-950 p-4 font-mono text-xs leading-6 text-zinc-200">
                {tail.loading ? "正在读取日志..." : (tail.error ?? tailText) || "请选择日志文件"}
              </pre>
            </div>
          )}
        </CardContent>
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
