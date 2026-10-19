import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import {
  BellRing,
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

function OnlineRateGauge({ value }: { value: number }) {
  const clamped = Math.max(0, Math.min(100, value));
  const length = Math.PI * 40;
  return (
    <svg viewBox="0 0 96 56" className="h-14 w-24" aria-hidden>
      <path
        d="M 8 52 A 40 40 0 0 1 88 52"
        fill="none"
        stroke="var(--muted)"
        strokeLinecap="round"
        strokeWidth="8"
      />
      <path
        d="M 8 52 A 40 40 0 0 1 88 52"
        fill="none"
        stroke="var(--brand)"
        strokeLinecap="round"
        strokeWidth="8"
        strokeDasharray={`${(clamped / 100) * length} ${length}`}
      />
    </svg>
  );
}

function StatCard({
  title,
  value,
  unit,
  detail,
  icon: Icon,
  loading,
  viz,
}: {
  title: string;
  value: string;
  unit?: string;
  detail: string;
  icon: typeof Server;
  loading: boolean;
  viz?: ReactNode;
}) {
  return (
    <Card size="sm" className="relative min-h-[132px]">
      <CardContent className="flex flex-col gap-1 px-4 py-1">
        <div className="flex items-center gap-2 text-[13px] text-muted-foreground">
          <Icon className="size-4" />
          <span className="truncate">{title}</span>
        </div>
        {loading ? (
          <Skeleton className="mt-2 h-8 w-24" />
        ) : (
          <p className="mt-1 text-[28px] font-semibold leading-tight tracking-tight tabular-nums">
            {value}
            {unit && <span className="ml-1 text-sm font-medium text-muted-foreground">{unit}</span>}
          </p>
        )}
        <p className="truncate text-xs text-muted-foreground">{detail}</p>
      </CardContent>
      {viz && <div className="pointer-events-none absolute bottom-3 right-4">{viz}</div>}
    </Card>
  );
}


const PIPELINE_STAGE_META: Record<PipelineStageKey, { label: string; icon: typeof Server }> = {
  collector: { label: "采集", icon: RadioTower },
  uploader: { label: "上行", icon: CloudUpload },
  ingest: { label: "汇聚", icon: GitMerge },
  redis: { label: "Redis", icon: Layers3 },
  writer: { label: "入库", icon: Save },
  tdengine: { label: "存储", icon: Database },
};

const PIPELINE_STATUS_META = {
  healthy: { label: "正常", dot: "bg-success", badge: "border-success/30 bg-success-soft text-success" },
  warning: { label: "告警", dot: "bg-warning", badge: "border-warning/30 bg-warning-soft text-warning" },
  critical: { label: "异常", dot: "bg-destructive", badge: "border-destructive/30 bg-destructive/5 text-destructive" },
  unknown: { label: "未知", dot: "bg-muted-foreground/60", badge: "text-muted-foreground" },
  inactive: { label: "未启用", dot: "bg-muted-foreground/35", badge: "text-muted-foreground/70" },
} as const;

function PipelineTimeline({ stages }: { stages: PipelineStageHealth[] }) {
  return (
    <TooltipProvider delayDuration={200}>
      <ol className="flex flex-col">
        {stages.map((stage, index) => {
          const meta = PIPELINE_STAGE_META[stage.key];
          const styles = PIPELINE_STATUS_META[stage.status];
          return (
            <li
              key={stage.key}
              className={[
                "relative grid min-h-[36px] grid-cols-[16px_minmax(0,1fr)_auto_auto] items-center gap-x-3 rounded-lg px-2",
                stage.status === "warning" ? "bg-warning-soft/50" : "",
              ].join(" ")}
            >
              <span className="relative flex h-full items-center justify-center">
                {index < stages.length - 1 && (
                  <span className="absolute left-1/2 top-1/2 h-full w-px -translate-x-1/2 bg-border" />
                )}
                <span className={`relative z-10 size-2 rounded-full ${styles.dot}`} />
              </span>
              <Tooltip>
                <TooltipTrigger asChild>
                  <span className="min-w-0 cursor-help truncate text-[13px] font-medium">
                    {meta.label}
                  </span>
                </TooltipTrigger>
                <TooltipContent className="max-w-56">
                  <p className="font-medium">{meta.label} · {stage.detail}</p>
                  <p className="mt-1 text-xs text-muted-foreground">{stage.description}</p>
                </TooltipContent>
              </Tooltip>
              <span className="max-w-[120px] truncate text-right text-xs tabular-nums text-muted-foreground">
                {stage.detail}
              </span>
              <Badge variant="outline" className={`w-14 justify-center font-normal ${styles.badge}`}>
                {styles.label}
              </Badge>
            </li>
          );
        })}
      </ol>
    </TooltipProvider>
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
  const alertDiff = dashboard.overview?.systemAlertComparePrev?.countDiff ?? 0;

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
    <div className="mx-auto w-full max-w-[1680px] space-y-4">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          title="设备总数"
          value={formatNumber(dashboard.overview?.deviceTotal)}
          unit="台"
          detail={`${formatNumber(dashboard.overview?.onlineDeviceTotal)} 台在线 · ${formatNumber(dashboard.overview?.offlineDeviceTotal)} 台离线`}
          icon={Server}
          loading={dashboard.loading}
        />
        <StatCard
          title="在线率"
          value={onlineRate.toFixed(1)}
          unit="%"
          detail={`${formatNumber(dashboard.overview?.riskDeviceTotal)} 台设备需要关注`}
          icon={Cpu}
          loading={dashboard.loading}
          viz={<OnlineRateGauge value={onlineRate} />}
        />
        <StatCard
          title="采集点位"
          value={formatNumber(dashboard.overview?.pointTotal)}
          unit="个"
          detail="当前已配置采集点位"
          icon={Database}
          loading={dashboard.loading}
        />
        <StatCard
          title="近24小时系统告警"
          value={formatNumber(dashboard.overview?.systemAlertTotal)}
          unit="条"
          detail={`较上一周期 ${alertDiff > 0 ? "↑" : alertDiff < 0 ? "↓" : ""}${Math.abs(alertDiff)} 条`}
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
        <Card data-dashboard-section="distribution">
          <CardHeader>
            <CardTitle>设备类型分布</CardTitle>
            <CardDescription>各类型设备在线与离线数量</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-1 flex-col justify-center gap-3">
            {distributionData.length ? (
              <>
                {distributionData.map((row) => {
                  const total = row.online + row.offline;
                  return (
                    <div
                      key={row.name}
                      className="grid grid-cols-[72px_minmax(0,1fr)_64px] items-center gap-2.5 text-xs"
                    >
                      <span className="truncate text-muted-foreground" title={row.name}>{row.name}</span>
                      <div className="flex h-3.5 overflow-hidden rounded-full bg-muted">
                        {row.online > 0 && (
                          <div className="h-full bg-brand" style={{ flex: row.online }} />
                        )}
                        {row.offline > 0 && (
                          <div
                            className="h-full"
                            style={{
                              flex: row.offline,
                              backgroundImage:
                                "repeating-linear-gradient(135deg, var(--muted-foreground) 0, var(--muted-foreground) 1px, transparent 1px, transparent 4px)",
                            }}
                          />
                        )}
                      </div>
                      <span className="text-right tabular-nums">
                        {row.online} / {total}
                      </span>
                    </div>
                  );
                })}
                <div className="flex items-center gap-4 pt-1 text-xs text-muted-foreground">
                  <span className="flex items-center gap-1.5">
                    <span className="size-2.5 rounded-[2px] bg-brand" />
                    在线
                  </span>
                  <span className="flex items-center gap-1.5">
                    <span
                      className="size-2.5 rounded-[2px]"
                      style={{
                        backgroundImage:
                          "repeating-linear-gradient(135deg, var(--muted-foreground) 0, var(--muted-foreground) 1px, transparent 1px, transparent 3px)",
                      }}
                    />
                    离线
                  </span>
                </div>
              </>
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
          <CardContent className="flex min-h-[220px] flex-1 items-center justify-center">
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
          <CardContent className="flex min-h-[220px] flex-1 flex-col justify-center gap-2">
            {pipeline.loading && !pipeline.data ? (
              <Skeleton className="h-52 w-full" />
            ) : pipeline.error ? (
              <PanelFallback loading={false} error={pipeline.error} onRetry={pipeline.refresh} />
            ) : pipeline.data ? (
              <>
                <PipelineTimeline stages={pipeline.data.stages} />
                <div className="flex items-center justify-between border-t pt-2 text-[10px] text-muted-foreground">
                  <span>六级链路状态聚合</span>
                  <span>更新 {formatTime(pipeline.data.generatedAt)}</span>
                </div>
              </>
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
