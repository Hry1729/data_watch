import { useCallback, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  BellRing,
  ArrowDown,
  ArrowLeft,
  ArrowRight,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  CircleAlert,
  CircleCheck,
  Cpu,
  Database,
  CloudUpload,
  GitMerge,
  Layers3,
  RadioTower,
  Save,
  ExternalLink,
  LoaderCircle,
  RefreshCw,
  Server,
} from "lucide-react";
import {
  Area,
  ComposedChart,
  Bar,
  BarChart,
  CartesianGrid,
  Line,
  XAxis,
  YAxis,
} from "recharts";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { useDashboardData, type DashboardTrendRange } from "@/hooks/useDashboardData";
import { usePipelineHealth, type PipelineStageHealth, type PipelineStageKey } from "@/hooks/usePipelineHealth";
import { backendClient } from "@/lib/backend-client";
import type { SystemLogItem } from "@/types/log";

const trendConfig = {
  collectCount: { label: "采集量", color: "var(--chart-4)" },
  alarmCount: { label: "告警数", color: "var(--chart-2)" },
  onlineDeviceCount: { label: "在线设备数", color: "var(--chart-3)" },
} satisfies ChartConfig;

const distributionConfig = {
  online: { label: "在线", color: "var(--chart-3)" },
  offline: { label: "离线", color: "var(--chart-1)" },
} satisfies ChartConfig;

const RANGE_OPTIONS: { value: DashboardTrendRange; label: string }[] = [
  { value: "today", label: "今日" },
  { value: "last24h", label: "近24小时" },
  { value: "last7d", label: "近7天" },
];

function formatNumber(value: number | undefined) {
  return (value ?? 0).toLocaleString("zh-CN");
}

function formatCompactNumber(value: number) {
  return new Intl.NumberFormat("zh-CN", {
    notation: "compact",
    maximumFractionDigits: 1,
  }).format(value);
}

function formatTime(value: string) {
  if (!value) return "--";
  const normalized = value.includes("T") ? value : value.replace(" ", "T");
  const date = new Date(normalized);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat("zh-CN", {
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).format(date);
}

const SYSTEM_LOG_TYPE_NAMES: Record<SystemLogItem["type"], string> = {
  system: "系统",
  auth: "认证",
  operation: "操作",
  collect: "采集",
  storage: "存储",
  frontend: "前端",
  security: "安全",
  alarm: "告警",
};

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
function PanelFallback({ loading, error, onRetry }: { loading: boolean; error: string | null; onRetry: () => void }) {
  if (loading) {
    return (
      <div className="space-y-3 py-8">
        <Skeleton className="h-4 w-2/3" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }
  if (error) {
    return (
      <div className="flex min-h-48 flex-col items-center justify-center gap-3 text-center">
        <p className="text-sm text-muted-foreground">{error}</p>
        <Button variant="outline" size="sm" onClick={onRetry}>
          <RefreshCw className="size-4" />
          重试
        </Button>
      </div>
    );
  }
  return <div className="flex min-h-48 items-center justify-center text-sm text-muted-foreground">暂无数据</div>;
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
  icon: typeof Server;
  loading: boolean;
}) {
  return (
    <Card size="sm" className="bg-card">
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


const PIPELINE_STAGE_META: Record<PipelineStageKey, { label: string; icon: typeof Server }> = {
  collector: { label: "Collector", icon: RadioTower },
  uploader: { label: "Uploader", icon: CloudUpload },
  ingest: { label: "Ingest", icon: GitMerge },
  redis: { label: "Redis", icon: Layers3 },
  writer: { label: "Writer", icon: Save },
  tdengine: { label: "TDengine", icon: Database },
};

const PIPELINE_GRID_POSITIONS = [
  "col-start-1 row-start-1",
  "col-start-3 row-start-1",
  "col-start-5 row-start-1",
  "col-start-5 row-start-3",
  "col-start-3 row-start-3",
  "col-start-1 row-start-3",
] as const;

const PIPELINE_STATUS_CLASSES = {
  healthy: { node: "border-emerald-200 bg-emerald-50/40", icon: "text-emerald-600", dot: "bg-emerald-500" },
  warning: { node: "border-amber-200 bg-amber-50/50", icon: "text-amber-600", dot: "animate-pulse bg-amber-400" },
  critical: { node: "border-red-200 bg-red-50/50", icon: "text-red-600", dot: "bg-red-500" },
  unknown: { node: "border-border bg-muted/30", icon: "text-muted-foreground", dot: "bg-muted-foreground/60" },
  inactive: { node: "border-border bg-muted/20", icon: "text-muted-foreground/60", dot: "bg-muted-foreground/35" },
} as const;

function PipelineStageNode({ stage }: { stage: PipelineStageHealth }) {
  const meta = PIPELINE_STAGE_META[stage.key];
  const styles = PIPELINE_STATUS_CLASSES[stage.status];
  const Icon = meta.icon;
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <div
          tabIndex={0}
          role="status"
          aria-label={`${meta.label}：${stage.detail}`}
          className={`relative z-10 flex h-20 min-w-0 flex-col items-center justify-center rounded-md border px-1.5 py-2 outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring ${styles.node}`}
        >
          <span className={`absolute right-1.5 top-1.5 size-2 rounded-full ${styles.dot}`} />
          <Icon className={`mb-1 size-4 shrink-0 ${styles.icon}`} />
          <span className="max-w-full truncate text-[10px] font-semibold">{meta.label}</span>
          <span className="mt-0.5 max-w-full truncate text-[9px] text-muted-foreground">{stage.detail}</span>
        </div>
      </TooltipTrigger>
      <TooltipContent className="max-w-56">
        <p className="font-medium">{meta.label} · {stage.detail}</p>
        <p className="mt-1 text-xs text-muted-foreground">{stage.description}</p>
      </TooltipContent>
    </Tooltip>
  );
}

export function WorkbenchPage() {
  const navigate = useNavigate();
  const dashboard = useDashboardData();
  const pipeline = usePipelineHealth();
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);
  const [logPage, setLogPage] = useState(1);
  const [logPageSize, setLogPageSize] = useState(10);
  const loadRecentLogs = useCallback(
    (token: string, signal: AbortSignal) => backendClient.systemLogList(token, { pageNo: logPage, pageSize: logPageSize }, signal),
    [logPage, logPageSize],
  );
  const recentLogs = useBackendQuery(loadRecentLogs, { intervalMs: 30_000 });
  const logTotalPages = Math.max(1, Math.ceil((recentLogs.data?.total ?? 0) / logPageSize));

  const onlineRate = dashboard.overview?.deviceTotal
    ? (dashboard.overview.onlineDeviceTotal / dashboard.overview.deviceTotal) * 100
    : 0;

  const trendData = useMemo(() => {
    const series = new Map(dashboard.trend?.series?.map((item) => [item.metricKey, item.data]) ?? []);
    return (dashboard.trend?.timeAxis ?? []).map((time, index) => ({
      time,
      collectCount: series.get("collectCount")?.[index] ?? 0,
      alarmCount: series.get("alarmCount")?.[index] ?? 0,
      onlineDeviceCount: series.get("onlineDeviceCount")?.[index] ?? 0,
    }));
  }, [dashboard.trend]);

  const healthData = useMemo(
    () => [
      { label: "在线", value: dashboard.health?.overview?.onlineDevices ?? 0, color: "var(--chart-2)" },
      { label: "风险", value: dashboard.health?.overview?.riskDevices ?? 0, color: "var(--chart-4)" },
      { label: "离线", value: dashboard.health?.overview?.offlineDevices ?? 0, color: "var(--chart-5)" },
    ],
    [dashboard.health],
  );
  const healthLegendItems = healthData;

  const distributionData = useMemo(
    () =>
      (dashboard.distribution?.groups ?? []).slice(0, 6).map((group) => ({
        name: group.typeName,
        online: group.onlineCount,
        offline: Math.max(0, group.deviceCount - group.onlineCount),
      })),
    [dashboard.distribution],
  );

  return (
    <div className="mx-auto w-full max-w-[1680px] space-y-4 px-4 pb-4 md:px-6 md:pb-6">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          title="设备总数"
          value={formatNumber(dashboard.overview?.deviceTotal)}
          detail={`${formatNumber(dashboard.overview?.onlineDeviceTotal)} 台在线 · ${formatNumber(dashboard.overview?.offlineDeviceTotal)} 台离线`}
          icon={Server}
          loading={dashboard.loading}
        />
        <StatCard
          title="在线率"
          value={`${onlineRate.toFixed(1)}%`}
          detail={`${formatNumber(dashboard.overview?.riskDeviceTotal)} 台设备需要关注`}
          icon={Cpu}
          loading={dashboard.loading}
        />
        <StatCard
          title="采集点位"
          value={formatNumber(dashboard.overview?.pointTotal)}
          detail="当前已配置采集点位"
          icon={Database}
          loading={dashboard.loading}
        />
        <StatCard
          title="近24小时系统告警"
          value={formatNumber(dashboard.overview?.systemAlertTotal)}
          detail={`较上一周期 ${dashboard.overview?.systemAlertComparePrev?.countDiff ?? 0} 条`}
          icon={BellRing}
          loading={dashboard.loading}
        />
      </div>

      <Card data-dashboard-section="trend" className="@container/card">
        <CardHeader>
          <CardTitle>采集量 / 在线设备 / 告警趋势</CardTitle>
          <CardDescription>采集量、在线设备数与告警数量变化</CardDescription>
          <CardAction>
            <ToggleGroup
              type="single"
              value={dashboard.trendRange}
              onValueChange={(value: DashboardTrendRange) => value && dashboard.setTrendRange(value)}
              variant="outline"
              className="hidden *:data-[slot=toggle-group-item]:px-4! @[767px]/card:flex"
            >
              {RANGE_OPTIONS.map((option) => (
                <ToggleGroupItem key={option.value} value={option.value}>{option.label}</ToggleGroupItem>
              ))}
            </ToggleGroup>
            <Select value={dashboard.trendRange} onValueChange={(value: DashboardTrendRange) => dashboard.setTrendRange(value)}>
              <SelectTrigger className="w-[132px] @[767px]/card:hidden" size="sm" aria-label="选择趋势时间范围">
                <SelectValue />
              </SelectTrigger>
              <SelectContent align="end">
                {RANGE_OPTIONS.map((option) => (
                  <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </CardAction>
        </CardHeader>
        <CardContent className="px-2 pt-2 sm:px-6">
          {trendData.length ? (
            <ChartContainer config={trendConfig} className="h-[280px] w-full aspect-auto">
              <ComposedChart data={trendData} margin={{ left: 0, right: 0, top: 8 }} accessibilityLayer barGap={0}>
                <defs>
                  <linearGradient id="fillCollectCount" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="var(--color-collectCount)" stopOpacity={0.85} />
                    <stop offset="95%" stopColor="var(--color-collectCount)" stopOpacity={0.08} />
                  </linearGradient>
                </defs>
                <CartesianGrid vertical={false} />
                <XAxis dataKey="time" tickLine={false} axisLine={false} tickMargin={8} minTickGap={32} />
                <YAxis
                  yAxisId="collect"
                  domain={[0, "auto"]}
                  tickLine={false}
                  axisLine={false}
                  width={48}
                  tickFormatter={(value) => formatCompactNumber(Number(value))}
                />
                <YAxis yAxisId="count" domain={[0, "auto"]} orientation="right" allowDecimals={false} tickLine={false} axisLine={false} width={40} />
                <ChartTooltip cursor={false} content={<ChartTooltipContent indicator="dot" />} />
                <Area
                  yAxisId="collect"
                  dataKey="collectCount"
                  type="monotone"
                  fill="url(#fillCollectCount)"
                  stroke="var(--color-collectCount)"
                  strokeWidth={2}
                />
                <Bar
                  yAxisId="count"
                  dataKey="onlineDeviceCount"
                  fill="var(--color-onlineDeviceCount)"
                  radius={[4, 4, 0, 0]}
                  maxBarSize={30}
                />
                <Line
                  yAxisId="count"
                  dataKey="alarmCount"
                  type="monotone"
                  stroke="var(--color-alarmCount)"
                  strokeWidth={2.5}
                  dot={false}
                />
                <ChartLegend content={<ChartLegendContent />} />
              </ComposedChart>
            </ChartContainer>
          ) : (
            <PanelFallback loading={dashboard.panelLoading.trend} error={dashboard.errors.trend} onRetry={dashboard.refresh} />
          )}
        </CardContent>
      </Card>

      <div data-dashboard-row="overview-charts" className="grid gap-4 lg:grid-cols-3">
        <Card data-dashboard-section="distribution" className="@container/card">
          <CardHeader>
            <CardTitle>设备类型分布</CardTitle>
            <CardDescription>各类型设备在线与离线数量</CardDescription>
          </CardHeader>
          <CardContent>
            {distributionData.length ? (
              <ChartContainer config={distributionConfig} className="h-[220px] w-full aspect-auto">
                <BarChart data={distributionData} margin={{ left: 0, right: 8, top: 8 }} accessibilityLayer>
                  <CartesianGrid vertical={false} />
                  <XAxis
                    dataKey="name"
                    tickLine={false}
                    axisLine={false}
                    tickMargin={10}
                    interval={0}
                    tickFormatter={(value) => String(value).length > 6 ? `${String(value).slice(0, 6)}…` : String(value)}
                  />
                  <YAxis allowDecimals={false} tickLine={false} axisLine={false} width={32} />
                  <ChartTooltip content={<ChartTooltipContent indicator="dot" />} />
                  <ChartLegend content={<ChartLegendContent />} />
                  <Bar dataKey="online" stackId="devices" fill="var(--color-online)" radius={[0, 0, 3, 3]} />
                  <Bar dataKey="offline" stackId="devices" fill="var(--color-offline)" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ChartContainer>
            ) : (
              <PanelFallback loading={dashboard.panelLoading.distribution} error={dashboard.errors.distribution} onRetry={dashboard.refresh} />
            )}
          </CardContent>
        </Card>

        <Card data-dashboard-section="health" className="@container/card">
          <CardHeader>
            <CardTitle>设备通讯概览</CardTitle>
            <CardDescription>近24小时通讯健康状态</CardDescription>
            <CardAction>
              <Button variant="outline" size="sm" onClick={() => navigate("/device/devices")}>
                查看设备
                <ExternalLink className="size-4" />
              </Button>
            </CardAction>
          </CardHeader>
          <CardContent className="flex min-h-[220px] items-center justify-center">
            {dashboard.health ? (
              <div className="flex w-full max-w-lg flex-col items-center justify-center gap-5">
                <PieChart
                  data={healthData}
                  hoveredIndex={hoveredIndex}
                  innerRadius={45}
                  onHoverChange={setHoveredIndex}
                  size={150}
                >
                  {healthData.map((_, index) => <PieSlice index={index} key={index} />)}
                  <PieCenter defaultLabel="设备" />
                </PieChart>
                <Legend
                  hoveredIndex={hoveredIndex}
                  items={healthLegendItems}
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
              <PanelFallback loading={dashboard.panelLoading.health} error={dashboard.errors.health} onRetry={dashboard.refresh} />
            )}
          </CardContent>
        </Card>
        <Card data-dashboard-section="pipeline" className="@container/card">
          <CardHeader>
            <CardTitle>数据链路健康</CardTitle>
            <CardDescription>采集到入库的全链路状态</CardDescription>
          </CardHeader>
          <CardContent className="flex min-h-[220px] flex-col justify-center gap-4">
            {pipeline.loading && !pipeline.data ? (
              <Skeleton className="h-20 w-full" />
            ) : pipeline.error ? (
              <PanelFallback loading={false} error={pipeline.error} onRetry={pipeline.refresh} />
            ) : pipeline.data ? (
              <TooltipProvider delayDuration={200}>
                <div className="space-y-4">
                  <div className="grid grid-cols-[minmax(0,1fr)_18px_minmax(0,1fr)_18px_minmax(0,1fr)] grid-rows-[80px_18px_80px] items-center gap-x-1">
                    {pipeline.data.stages.map((stage, index) => (
                      <div key={stage.key} className={"min-w-0 " + (PIPELINE_GRID_POSITIONS[index] ?? "")}>
                        <PipelineStageNode stage={stage} />
                      </div>
                    ))}
                    <div aria-hidden className="col-start-2 row-start-1 flex items-center">
                      <span className="h-px flex-1 bg-border" />
                      <ArrowRight className="-ml-px size-3 shrink-0 text-muted-foreground/70" />
                    </div>
                    <div aria-hidden className="col-start-4 row-start-1 flex items-center">
                      <span className="h-px flex-1 bg-border" />
                      <ArrowRight className="-ml-px size-3 shrink-0 text-muted-foreground/70" />
                    </div>
                    <div aria-hidden className="col-start-5 row-start-2 flex h-full flex-col items-center">
                      <span className="w-px flex-1 bg-border" />
                      <ArrowDown className="-mt-px size-3 shrink-0 text-muted-foreground/70" />
                    </div>
                    <div aria-hidden className="col-start-4 row-start-3 flex items-center">
                      <ArrowLeft className="-mr-px size-3 shrink-0 text-muted-foreground/70" />
                      <span className="h-px flex-1 bg-border" />
                    </div>
                    <div aria-hidden className="col-start-2 row-start-3 flex items-center">
                      <ArrowLeft className="-mr-px size-3 shrink-0 text-muted-foreground/70" />
                      <span className="h-px flex-1 bg-border" />
                    </div>
                  </div>
                  <div className="flex items-center justify-between border-t pt-3 text-[10px] text-muted-foreground">
                    <span>六级链路状态聚合</span>
                    <span>更新 {formatTime(pipeline.data.generatedAt)}</span>
                  </div>
                </div>
              </TooltipProvider>
            ) : (
              <PanelFallback loading={false} error={null} onRetry={pipeline.refresh} />
            )}
          </CardContent>
        </Card>
      </div>

      <Card data-dashboard-section="logs" className="@container/card">
        <CardHeader>
          <CardTitle>最近系统日志</CardTitle>
          <CardDescription>系统运行、采集、存储与操作日志</CardDescription>
          <CardAction>
            <Button variant="outline" onClick={() => navigate("/logs/system")}>
              更多日志
              <ExternalLink className="size-4" />
            </Button>
          </CardAction>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="overflow-hidden rounded-lg border">
            <TooltipProvider delayDuration={200}>
              <Table className="w-full table-fixed">
                <TableHeader className="bg-muted/50">
                  <TableRow className="h-10 hover:bg-transparent">
                    <TableHead className="w-[18%] text-center">时间</TableHead>
                    <TableHead className="w-[11%] text-center">级别</TableHead>
                    <TableHead className="w-[11%] text-center">类型</TableHead>
                    <TableHead className="w-[16%] text-center">来源</TableHead>
                    <TableHead className="w-[44%] text-center">内容</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {recentLogs.loading ? (
                    Array.from({ length: 5 }, (_, index) => (
                      <TableRow key={index} className="h-12">
                        <TableCell colSpan={5}><Skeleton className="h-7 w-full" /></TableCell>
                      </TableRow>
                    ))
                  ) : recentLogs.data?.list?.length ? (
                    recentLogs.data.list.map((row) => (
                      <TableRow key={`${row.kind}:${row.id}`} className="h-12">
                        <TableCell className="max-w-0 overflow-hidden whitespace-nowrap text-center text-xs tabular-nums text-muted-foreground">
                          <span className="block truncate">{formatTime(row.time)}</span>
                        </TableCell>
                        <TableCell className="max-w-0 overflow-hidden text-center">{logLevelBadge(row.level)}</TableCell>
                        <TableCell className="max-w-0 overflow-hidden text-center">
                          <Badge variant="outline" className="max-w-full truncate font-normal text-muted-foreground">
                            {SYSTEM_LOG_TYPE_NAMES[row.type] ?? row.type}
                          </Badge>
                        </TableCell>
                        <TableCell className="max-w-0 overflow-hidden text-center font-medium">
                          <span className="block truncate">{row.source || "-"}</span>
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
                      </TableRow>
                    ))
                  ) : (
                    <TableRow>
                      <TableCell colSpan={5} className="h-28 text-center text-muted-foreground">
                        {recentLogs.error ?? "暂无系统日志"}
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TooltipProvider>
          </div>

          <div className="flex flex-col gap-3 px-1 text-sm sm:flex-row sm:items-center sm:justify-between">
            <span className="text-muted-foreground">共 {recentLogs.data?.total ?? 0} 条日志</span>
            <div className="flex flex-wrap items-center gap-3 sm:justify-end">
              <div className="flex items-center gap-2">
                <span className="text-muted-foreground">每页行数</span>
                <Select
                  value={String(logPageSize)}
                  onValueChange={(value) => {
                    setLogPageSize(Number(value));
                    setLogPage(1);
                  }}
                >
                  <SelectTrigger className="w-20" size="sm" aria-label="每页行数">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent align="end">
                    {[5, 10, 20].map((value) => <SelectItem key={value} value={String(value)}>{value}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <span className="min-w-24 text-center font-medium tabular-nums">第 {logPage} / {logTotalPages} 页</span>
              <div className="flex items-center gap-2">
                <Button variant="outline" size="icon" className="size-8" disabled={logPage <= 1} onClick={() => setLogPage(1)} aria-label="第一页">
                  <ChevronsLeft className="size-4" />
                </Button>
                <Button variant="outline" size="icon" className="size-8" disabled={logPage <= 1} onClick={() => setLogPage((page) => Math.max(1, page - 1))} aria-label="上一页">
                  <ChevronLeft className="size-4" />
                </Button>
                <Button variant="outline" size="icon" className="size-8" disabled={logPage >= logTotalPages} onClick={() => setLogPage((page) => Math.min(logTotalPages, page + 1))} aria-label="下一页">
                  <ChevronRight className="size-4" />
                </Button>
                <Button variant="outline" size="icon" className="size-8" disabled={logPage >= logTotalPages} onClick={() => setLogPage(logTotalPages)} aria-label="最后一页">
                  <ChevronsRight className="size-4" />
                </Button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

export default WorkbenchPage;
