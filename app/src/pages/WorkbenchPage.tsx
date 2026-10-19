import { useCallback, useMemo, useState, type CSSProperties, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import {
  Bell,
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
  ChartTooltip,
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
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
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

const SOFT_BRAND = "color-mix(in oklab, var(--brand) 45%, white)";

type TrendKey = "collectCount" | "onlineDeviceCount" | "alarmCount";

const trendConfig = {
  collectCount: { label: "采集量", color: "var(--brand)" },
  onlineDeviceCount: { label: "在线设备数", color: SOFT_BRAND },
  alarmCount: { label: "告警数", color: "var(--warning)" },
} satisfies ChartConfig;

const TREND_KEYS: TrendKey[] = ["collectCount", "onlineDeviceCount", "alarmCount"];

const RANGE_OPTIONS: { value: DashboardTrendRange; label: string }[] = [
  { value: "today", label: "今日" },
  { value: "last24h", label: "近24小时" },
  { value: "last7d", label: "近7天" },
];

/* 卡片统一节奏：头部 20px 内边距，标题 14/600，描述 13px */
const CARD_CLASS = "gap-0 py-0";
const HEADER_CLASS = "gap-1.5 px-5 pt-5";
const TITLE_CLASS = "text-sm font-semibold leading-none";
const DESC_CLASS = "text-[13px] leading-normal";

function formatNumber(value: number | undefined) {
  return (value ?? 0).toLocaleString("zh-CN");
}

function formatCompactNumber(value: number) {
  return new Intl.NumberFormat("zh-CN", {
    notation: "compact",
    maximumFractionDigits: 2,
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
      <Badge variant="outline" className="rounded-md border-transparent bg-destructive-soft text-xs text-destructive">
        <CircleAlert className="size-3" />
        {level}
      </Badge>
    );
  }

  if (level === "WARN") {
    return (
      <Badge variant="outline" className="rounded-md border-transparent bg-warning-soft text-xs text-warning">
        <BellRing className="size-3" />
        {level}
      </Badge>
    );
  }

  if (level === "DEBUG") {
    return (
      <Badge variant="outline" className="rounded-md border-transparent bg-muted text-xs text-muted-foreground">
        <LoaderCircle className="size-3" />
        {level}
      </Badge>
    );
  }

  return (
    <Badge variant="outline" className="rounded-md border-transparent bg-success-soft text-xs text-success">
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

/* 分段刻度仪表：24 根刻度，按在线率点亮 */
function OnlineRateGauge({ value }: { value: number }) {
  const ratio = Math.max(0, Math.min(100, value)) / 100;
  const ticks = 24;
  return (
    <svg viewBox="0 0 96 56" width={96} height={56} aria-hidden>
      {Array.from({ length: ticks }, (_, i) => {
        const angle = Math.PI + (Math.PI * i) / (ticks - 1);
        const on = i / (ticks - 1) <= ratio && ratio > 0;
        return (
          <line
            key={i}
            x1={48 + 30 * Math.cos(angle)}
            y1={50 + 30 * Math.sin(angle)}
            x2={48 + 42 * Math.cos(angle)}
            y2={50 + 42 * Math.sin(angle)}
            stroke={on ? "var(--brand)" : "var(--muted)"}
            strokeWidth={3}
            strokeLinecap="round"
          />
        );
      })}
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
  badge,
}: {
  title: string;
  value: string;
  unit?: string;
  detail: string;
  icon: typeof Server;
  loading: boolean;
  viz?: ReactNode;
  badge?: ReactNode;
}) {
  return (
    <Card size="sm" className="relative min-h-[124px] gap-0 p-4">
      <CardContent className="flex flex-col gap-0.5 px-0 group-data-[size=sm]/card:px-0">
        <div className="flex items-center gap-2 text-[13px] text-muted-foreground">
          <Icon className="size-[15px] shrink-0" />
          <span className="truncate">{title}</span>
          {badge && <span className="ml-auto">{badge}</span>}
        </div>
        {loading ? (
          <Skeleton className="mt-2 h-8 w-24" />
        ) : (
          <p className="mt-2 text-[28px] font-semibold leading-[1.15] tracking-[-0.03em] tabular-nums">
            {value}
            {unit && <small className="ml-0.5 text-sm font-medium text-muted-foreground">{unit}</small>}
          </p>
        )}
        <p className="truncate text-xs text-muted-foreground">{detail}</p>
      </CardContent>
      {viz && <div className="pointer-events-none absolute bottom-3.5 right-3.5">{viz}</div>}
    </Card>
  );
}

/* 图例胶囊（可点击切换显示） */
function LegendChip({
  color,
  label,
  off,
  onClick,
  marker,
}: {
  color: string;
  label: string;
  off?: boolean;
  onClick?: () => void;
  marker?: CSSProperties;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={onClick ? !off : undefined}
      className={[
        "flex h-[26px] items-center gap-1.5 rounded-full border px-2.5 text-xs text-muted-foreground transition-opacity",
        onClick ? "cursor-pointer hover:bg-muted/60" : "cursor-default",
        off ? "opacity-45" : "",
      ].join(" ")}
    >
      <i className="size-2 rounded-full" style={off ? { background: "var(--muted-foreground)" } : { background: color, ...marker }} />
      {label}
    </button>
  );
}

type TrendRow = { time: string } & Record<TrendKey, number>;

function TrendTooltip({ active, payload }: { active?: boolean; payload?: { payload?: TrendRow }[] }) {
  const row = payload?.[0]?.payload;
  if (!active || !row) return null;
  const items: { label: string; color: string; value: string }[] = [
    { label: "采集量", color: "var(--brand)", value: formatCompactNumber(row.collectCount) },
    { label: "在线设备", color: SOFT_BRAND, value: formatNumber(row.onlineDeviceCount) },
    { label: "告警", color: "var(--warning)", value: formatNumber(row.alarmCount) },
  ];
  return (
    <div className="min-w-[150px] rounded-[10px] border bg-popover px-2.5 py-2 text-xs shadow-[0_8px_24px_-8px_rgba(0,0,0,0.18)]">
      <p className="mb-1 font-medium">{row.time}</p>
      {items.map((item) => (
        <div key={item.label} className="flex items-center gap-1.5 leading-[1.7] text-muted-foreground">
          <i className="size-2 rounded-[2px]" style={{ background: item.color }} />
          {item.label}
          <em className="ml-auto pl-3 font-medium not-italic tabular-nums text-foreground">{item.value}</em>
        </div>
      ))}
    </div>
  );
}

/* 同心环：在线 / 风险 / 离线 */
function HealthRings({
  data,
  total,
  hoveredIndex,
}: {
  data: { label: string; value: number; color: string }[];
  total: number;
  hoveredIndex: number | null;
}) {
  return (
    <svg viewBox="0 0 168 168" width={150} height={150} className="shrink-0" aria-label="设备通讯概览">
      {data.map((item, index) => {
        const r = 72 - index * 16;
        const c = 2 * Math.PI * r;
        const ratio = total > 0 ? Math.max(item.value > 0 ? 0.02 : 0, item.value / total) : 0;
        const active = hoveredIndex === null || hoveredIndex === index;
        return (
          <g key={item.label}>
            <circle cx={84} cy={84} r={r} fill="none" stroke="var(--muted)" strokeWidth={10} />
            {ratio > 0 && (
              <circle
                cx={84}
                cy={84}
                r={r}
                fill="none"
                stroke={item.color}
                strokeWidth={hoveredIndex === index ? 13 : 10}
                strokeLinecap="round"
                strokeDasharray={`${ratio * c} ${c}`}
                transform="rotate(-90 84 84)"
                style={{ opacity: active ? 1 : 0.25, transition: "opacity .15s, stroke-width .15s, stroke-dasharray .8s ease-out" }}
              />
            )}
          </g>
        );
      })}
      <text x={84} y={84} textAnchor="middle" fontSize={22} fontWeight={600} fill="var(--foreground)" className="tabular-nums">
        {formatNumber(total)}
      </text>
      <text x={84} y={102} textAnchor="middle" fontSize={11} fill="var(--muted-foreground)">
        设备
      </text>
    </svg>
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
  healthy: { label: "正常", color: "var(--success)", row: "", badge: "bg-success-soft text-success" },
  warning: { label: "需关注", color: "var(--warning)", row: "bg-warning-soft/70", badge: "bg-warning-soft text-warning" },
  critical: { label: "异常", color: "var(--destructive)", row: "bg-destructive-soft/70", badge: "bg-destructive-soft text-destructive" },
  unknown: { label: "未知", color: "var(--muted-foreground)", row: "", badge: "bg-muted text-muted-foreground" },
  inactive: { label: "未启用", color: "color-mix(in oklab, var(--muted-foreground) 50%, transparent)", row: "", badge: "bg-muted text-muted-foreground/80" },
} as const;

/* "3ms" → 3 + ms；"2/3 正常" → 2/3 + 正常；其他原样 */
function splitDetail(detail: string): { value: string; unit: string } | null {
  const match = detail.trim().match(/^([\d.,/]+)\s*(.*)$/);
  return match ? { value: match[1], unit: match[2] } : null;
}

function PipelineTimeline({ stages }: { stages: PipelineStageHealth[] }) {
  return (
    <TooltipProvider delayDuration={200}>
      <ol className="-mx-2 flex flex-1 flex-col">
        {stages.map((stage, index) => {
          const meta = PIPELINE_STAGE_META[stage.key];
          const styles = PIPELINE_STATUS_META[stage.status];
          const parts = splitDetail(stage.detail);
          const muted = stage.status === "inactive" || stage.status === "unknown";
          return (
            <li
              key={stage.key}
              style={{ "--ic": styles.color } as CSSProperties}
              className={[
                "relative grid min-h-[34px] flex-1 grid-cols-[16px_minmax(0,1fr)_auto_52px] items-center gap-x-3 rounded-lg px-2",
                styles.row,
              ].join(" ")}
            >
              <span
                aria-hidden
                className={[
                  "absolute left-[15.5px] w-px bg-border",
                  index === 0 ? "top-1/2" : "top-0",
                  index === stages.length - 1 ? "bottom-1/2" : "bottom-0",
                ].join(" ")}
              />
              <span
                className={[
                  "relative z-10 grid size-4 place-items-center rounded-full",
                  stage.status === "warning" ? "bg-warning-soft" : stage.status === "critical" ? "bg-destructive-soft" : "bg-card",
                ].join(" ")}
                style={{ boxShadow: "0 0 0 1.5px color-mix(in oklab, var(--ic) 45%, var(--border))" }}
              >
                <i className="size-1.5 rounded-full" style={{ background: "var(--ic)" }} />
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
              <span
                className={[
                  "max-w-[140px] truncate text-right text-[13px] tabular-nums",
                  muted ? "text-muted-foreground" : "font-semibold",
                ].join(" ")}
              >
                {parts ? (
                  <>
                    {parts.value}
                    {parts.unit && <small className="ml-1 text-xs font-normal text-muted-foreground">{parts.unit}</small>}
                  </>
                ) : (
                  <span className={muted ? "" : "font-medium"}>{stage.detail}</span>
                )}
              </span>
              <Badge className={`h-5 justify-self-end rounded-md border-transparent px-2 text-xs font-medium ${styles.badge}`}>
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
  const [hiddenSeries, setHiddenSeries] = useState<Record<TrendKey, boolean>>({
    collectCount: false,
    onlineDeviceCount: false,
    alarmCount: false,
  });
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

  const trendData = useMemo<TrendRow[]>(() => {
    const series = new Map(dashboard.trend?.series?.map((item) => [item.metricKey, item.data]) ?? []);
    return (dashboard.trend?.timeAxis ?? []).map((time, index) => ({
      time,
      collectCount: series.get("collectCount")?.[index] ?? 0,
      alarmCount: series.get("alarmCount")?.[index] ?? 0,
      onlineDeviceCount: series.get("onlineDeviceCount")?.[index] ?? 0,
    }));
  }, [dashboard.trend]);

  /* X 轴只标 7 个刻度（首尾 + 均分） */
  const trendTicks = useMemo(() => {
    const n = trendData.length;
    if (n <= 7) return trendData.map((row) => row.time);
    const step = (n - 1) / 6;
    return Array.from({ length: 7 }, (_, i) => trendData[Math.round(i * step)].time);
  }, [trendData]);

  const healthData = useMemo(
    () => [
      { label: "在线", value: dashboard.health?.overview?.onlineDevices ?? 0, color: "var(--brand)" },
      { label: "风险", value: dashboard.health?.overview?.riskDevices ?? 0, color: "var(--warning)" },
      { label: "离线", value: dashboard.health?.overview?.offlineDevices ?? 0, color: "var(--muted-foreground)" },
    ],
    [dashboard.health],
  );
  const healthTotal = healthData.reduce((sum, item) => sum + item.value, 0);

  const distributionData = useMemo(
    () =>
      (dashboard.distribution?.groups ?? []).slice(0, 6).map((group) => ({
        name: group.typeName,
        online: group.onlineCount,
        offline: Math.max(0, group.deviceCount - group.onlineCount),
      })),
    [dashboard.distribution],
  );
  const distributionMax = Math.max(1, ...distributionData.map((row) => row.online + row.offline));

  return (
    <div className="mx-auto w-full max-w-[1680px] space-y-4">
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
          detail="当前已配置采集点位"
          icon={Database}
          loading={dashboard.loading}
        />
        <StatCard
          title="近24小时系统告警"
          value={formatNumber(dashboard.overview?.systemAlertTotal)}
          detail={
            alertDiff > 0
              ? `较上一周期增加 ${alertDiff} 条`
              : alertDiff < 0
                ? `较上一周期减少 ${Math.abs(alertDiff)} 条`
                : "与上一周期持平"
          }
          icon={Bell}
          loading={dashboard.loading}
          badge={
            alertDiff !== 0 && !dashboard.loading ? (
              <span
                className={[
                  "inline-flex items-center rounded-full px-1.5 py-px text-xs font-medium tabular-nums",
                  alertDiff > 0 ? "bg-warning-soft text-warning" : "bg-success-soft text-success",
                ].join(" ")}
              >
                {alertDiff > 0 ? `+${alertDiff}` : `−${Math.abs(alertDiff)}`}
              </span>
            ) : undefined
          }
        />
      </div>

      <Card data-dashboard-section="trend" className={`@container/card ${CARD_CLASS}`}>
        <CardHeader className={HEADER_CLASS}>
          <CardTitle className={TITLE_CLASS}>采集量 / 在线设备 / 告警趋势</CardTitle>
          <CardDescription className={DESC_CLASS}>采集量、在线设备数与告警数量变化</CardDescription>
          <CardAction className="flex items-center gap-4">
            <div className="hidden items-center gap-1.5 @[900px]/card:flex">
              {TREND_KEYS.map((key) => (
                <LegendChip
                  key={key}
                  color={trendConfig[key].color}
                  label={trendConfig[key].label}
                  off={hiddenSeries[key]}
                  onClick={() => setHiddenSeries((prev) => ({ ...prev, [key]: !prev[key] }))}
                />
              ))}
            </div>
            <Tabs
              value={dashboard.trendRange}
              onValueChange={(value) => value && dashboard.setTrendRange(value as DashboardTrendRange)}
              className="hidden @[767px]/card:flex"
            >
              <TabsList className="h-8">
                {RANGE_OPTIONS.map((option) => (
                  <TabsTrigger
                    key={option.value}
                    value={option.value}
                    className="px-2.5 text-[13px] text-muted-foreground data-[state=active]:bg-background data-[state=active]:text-foreground data-[state=active]:shadow-[0_1px_2px_rgba(0,0,0,0.08)]"
                  >
                    {option.label}
                  </TabsTrigger>
                ))}
              </TabsList>
            </Tabs>
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
        <CardContent className="px-5 pb-5 pt-4">
          {trendData.length ? (
            <ChartContainer config={trendConfig} className="h-[264px] w-full aspect-auto">
              <ComposedChart data={trendData} margin={{ left: 24, right: 24, top: 10, bottom: 0 }} accessibilityLayer barGap={0}>
                <defs>
                  <linearGradient id="wbFillCollect" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="var(--brand)" stopOpacity={0.32} />
                    <stop offset="75%" stopColor="var(--brand)" stopOpacity={0.03} />
                    <stop offset="100%" stopColor="var(--brand)" stopOpacity={0} />
                  </linearGradient>
                  <pattern id="wbAlarmHatch" width="5" height="5" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
                    <line x1="0" y1="0" x2="0" y2="5" stroke="var(--warning)" strokeWidth="2.2" />
                  </pattern>
                </defs>
                <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 4" />
                <XAxis
                  dataKey="time"
                  ticks={trendTicks}
                  interval={0}
                  tickLine={false}
                  axisLine={false}
                  tickMargin={10}
                  tick={{ fontSize: 11, fill: "var(--muted-foreground)" }}
                />
                <YAxis yAxisId="collect" hide domain={[0, (max: number) => Math.max(1, max * 1.5)]} />
                <YAxis yAxisId="online" hide domain={[0, (max: number) => Math.max(1, max * 1.12)]} />
                <YAxis yAxisId="alarm" hide allowDecimals={false} domain={[0, (max: number) => Math.max(4, max * 4)]} />
                <ChartTooltip
                  cursor={{ stroke: "var(--muted-foreground)", strokeOpacity: 0.5, strokeDasharray: "3 3" }}
                  content={<TrendTooltip />}
                />
                <Bar
                  yAxisId="alarm"
                  dataKey="alarmCount"
                  hide={hiddenSeries.alarmCount}
                  fill="url(#wbAlarmHatch)"
                  stroke="var(--warning)"
                  strokeWidth={1}
                  radius={[3, 3, 0, 0]}
                  barSize={10}
                  isAnimationActive={false}
                />
                <Area
                  yAxisId="collect"
                  dataKey="collectCount"
                  hide={hiddenSeries.collectCount}
                  type="monotone"
                  fill="url(#wbFillCollect)"
                  stroke="var(--brand)"
                  strokeWidth={2.2}
                  activeDot={{ r: 5, fill: "var(--brand)", stroke: "var(--card)", strokeWidth: 2.5 }}
                />
                <Line
                  yAxisId="online"
                  dataKey="onlineDeviceCount"
                  hide={hiddenSeries.onlineDeviceCount}
                  type="monotone"
                  stroke={SOFT_BRAND}
                  strokeWidth={1.8}
                  strokeDasharray="5 5"
                  dot={false}
                  activeDot={false}
                />
              </ComposedChart>
            </ChartContainer>
          ) : (
            <PanelFallback loading={dashboard.panelLoading.trend} error={dashboard.errors.trend} onRetry={dashboard.refresh} />
          )}
        </CardContent>
      </Card>

      <div data-dashboard-row="overview-charts" className="grid items-stretch gap-4 lg:grid-cols-[1.15fr_1fr_1.15fr]">
        <Card data-dashboard-section="distribution" className={CARD_CLASS}>
          <CardHeader className={HEADER_CLASS}>
            <CardTitle className={TITLE_CLASS}>设备类型分布</CardTitle>
            <CardDescription className={DESC_CLASS}>各类型设备在线与离线数量</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-1 flex-col px-5 pb-5 pt-5">
            {distributionData.length ? (
              <>
                {distributionData.map((row) => {
                  const total = row.online + row.offline;
                  return (
                    <div
                      key={row.name}
                      className="mb-3 grid grid-cols-[72px_minmax(0,1fr)_64px] items-center gap-2.5 text-[13px]"
                    >
                      <span className="truncate" title={row.name}>{row.name}</span>
                      <div className="h-3.5">
                        <div
                          className="flex h-full overflow-hidden rounded-full bg-muted"
                          style={{ width: `${(total / distributionMax) * 100}%` }}
                        >
                          {row.online > 0 && (
                            <div className="h-full rounded-l-full bg-brand" style={{ flex: row.online }} />
                          )}
                          {row.offline > 0 && (
                            <div
                              className="h-full"
                              style={{
                                flex: row.offline,
                                backgroundImage:
                                  "repeating-linear-gradient(-45deg, color-mix(in oklab, var(--muted-foreground) 45%, transparent) 0 2px, transparent 2px 5px)",
                              }}
                            />
                          )}
                        </div>
                      </div>
                      <span className="text-right text-xs tabular-nums text-muted-foreground">
                        {row.online} / {total}
                      </span>
                    </div>
                  );
                })}
                <div className="mt-1 flex items-center gap-1.5">
                  <LegendChip color="var(--brand)" label="在线" />
                  <LegendChip color="var(--muted-foreground)" label="离线" marker={{ opacity: 0.5 }} />
                </div>
              </>
            ) : (
              <PanelFallback loading={dashboard.panelLoading.distribution} error={dashboard.errors.distribution} onRetry={dashboard.refresh} />
            )}
          </CardContent>
        </Card>

        <Card data-dashboard-section="health" className={`@container/card ${CARD_CLASS}`}>
          <CardHeader className={HEADER_CLASS}>
            <CardTitle className={TITLE_CLASS}>设备通讯概览</CardTitle>
            <CardDescription className={DESC_CLASS}>近24小时通讯健康状态</CardDescription>
            <CardAction>
              <Button variant="outline" onClick={() => navigate("/device/devices")}>
                查看设备
              </Button>
            </CardAction>
          </CardHeader>
          <CardContent className="flex flex-1 flex-col px-5 pb-5 pt-4">
            {dashboard.health ? (
              <div className="flex items-center gap-5">
                <HealthRings data={healthData} total={healthTotal} hoveredIndex={hoveredIndex} />
                <div className="flex min-w-0 flex-1 flex-col gap-2.5">
                  {healthData.map((item, index) => (
                    <div
                      key={item.label}
                      onMouseEnter={() => setHoveredIndex(index)}
                      onMouseLeave={() => setHoveredIndex(null)}
                      className="flex cursor-default items-center gap-2 whitespace-nowrap rounded-lg px-2 py-1.5 text-[13px] transition-colors hover:bg-muted"
                    >
                      <i className="size-2 shrink-0 rounded-full" style={{ background: item.color }} />
                      {item.label}
                      <em className="ml-auto font-medium not-italic tabular-nums">{formatNumber(item.value)}</em>
                      <span className="w-11 text-right text-xs tabular-nums text-muted-foreground">
                        {healthTotal ? `${((item.value / healthTotal) * 100).toFixed(1)}%` : "—"}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            ) : (
              <PanelFallback loading={dashboard.panelLoading.health} error={dashboard.errors.health} onRetry={dashboard.refresh} />
            )}
          </CardContent>
        </Card>
        <Card data-dashboard-section="pipeline" className={`@container/card ${CARD_CLASS}`}>
          <CardHeader className={HEADER_CLASS}>
            <CardTitle className={TITLE_CLASS}>数据链路健康</CardTitle>
            <CardDescription className={DESC_CLASS}>采集到入库的全链路状态</CardDescription>
            {pipeline.data && (
              <CardAction className="pt-0.5 text-xs tabular-nums text-muted-foreground">
                更新 {formatTime(pipeline.data.generatedAt)}
              </CardAction>
            )}
          </CardHeader>
          <CardContent className="flex min-h-[220px] flex-1 flex-col px-5 pb-4 pt-3">
            {pipeline.loading && !pipeline.data ? (
              <Skeleton className="h-52 w-full" />
            ) : pipeline.error ? (
              <PanelFallback loading={false} error={pipeline.error} onRetry={pipeline.refresh} />
            ) : pipeline.data ? (
              <PipelineTimeline stages={pipeline.data.stages} />
            ) : (
              <PanelFallback loading={false} error={null} onRetry={pipeline.refresh} />
            )}
          </CardContent>
        </Card>
      </div>

      <Card data-dashboard-section="logs" className={`@container/card ${CARD_CLASS}`}>
        <CardHeader className={HEADER_CLASS}>
          <CardTitle className={TITLE_CLASS}>最近系统日志</CardTitle>
          <CardDescription className={DESC_CLASS}>系统运行、采集、存储与操作日志</CardDescription>
          <CardAction>
            <Button variant="outline" onClick={() => navigate("/logs/system")}>
              更多日志
              <ExternalLink className="size-4" />
            </Button>
          </CardAction>
        </CardHeader>
        <CardContent className="space-y-3 px-5 pb-5 pt-4">
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
