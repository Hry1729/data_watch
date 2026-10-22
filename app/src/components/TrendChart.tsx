import { useId, useMemo, useState, type ReactNode } from "react";
import {
  Area,
  AreaChart,
  CartesianGrid,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { Card, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { cn } from "@/lib/utils";
import type { RatePoint, TrendChartState } from "@/types";

const RANGE_OPTIONS = [
  { label: "1 分钟", minutes: 1 },
  { label: "5 分钟", minutes: 5 },
  { label: "10 分钟", minutes: 10 },
] as const;

type RangeMinutes = (typeof RANGE_OPTIONS)[number]["minutes"];

/** 每个区间的 X 轴刻度间隔（秒） */
const TICK_INTERVAL: Record<RangeMinutes, number> = { 1: 15, 5: 60, 10: 120 };

const PRIMARY_COLOR = "var(--brand)";
const SECONDARY_COLOR = "var(--success)";

export interface TrendChartSeries {
  label: string;
  unit: string;
  data: RatePoint[];
  color?: string;
}

export interface TrendChartProps {
  title: string;
  /** 单位标签，如 "批次/min" 或 "rows/min" */
  unit: string;
  /** 四态：empty | loading | error | ready */
  state: TrendChartState;
  /** ready 态时的数据点序列（含时间标签） */
  data?: RatePoint[];
  /** 当前值（速率卡同源）；null 时显示 — */
  current?: number | null;
  /** 主序列图例名；默认“速率” */
  seriesLabel?: string;
  /** 可选的第二序列（双轴趋势，控制台使用） */
  secondary?: TrendChartSeries;
  /** 填充父容器高度；仅用于监控页的自适应布局。 */
  fillHeight?: boolean;
  /** 标题下方的一行说明 */
  description?: string;
  /** 在图表上方展示当前区间的峰值 / 均值 / 谷值 */
  showStats?: boolean;
  /** 非 fillHeight 时的图表高度（px） */
  height?: number;
}

interface TooltipPayloadItem {
  value?: number;
  dataKey?: string;
  name?: string;
  color?: string;
}

function parseTimeSeconds(value: string): number | null {
  const parts = value.split(":").map((part) => Number.parseInt(part, 10));
  if (parts.length < 2 || parts.some((part) => Number.isNaN(part))) return null;
  const [hh, mm, ss = 0] = parts;
  return hh * 3600 + mm * 60 + ss;
}

function filterByRange(data: RatePoint[], minutes: RangeMinutes): RatePoint[] {
  if (minutes === 10 || data.length < 2) return data;

  const lastSeconds = parseTimeSeconds(data[data.length - 1]?.t ?? "");
  if (lastSeconds == null) return data;

  const rangeSeconds = minutes * 60;
  const filtered = data.filter((point) => {
    const seconds = parseTimeSeconds(point.t);
    if (seconds == null) return false;
    let delta = lastSeconds - seconds;
    if (delta < 0) delta += 24 * 3600;
    return delta <= rangeSeconds;
  });

  return filtered.length > 0 ? filtered : data.slice(-1);
}

function formatValue(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "—";
  return Math.round(value).toLocaleString("en-US");
}

function formatAxis(value: number): string {
  if (value >= 1e6) return `${(value / 1e6).toFixed(value >= 1e7 ? 1 : 2).replace(/\.?0+$/, "")}M`;
  if (value >= 1e4) return `${(value / 1e3).toFixed(value >= 1e5 ? 0 : 1).replace(/\.0$/, "")}k`;
  return value.toLocaleString("en-US", { maximumFractionDigits: 2 });
}

/** 单序列：数据上下留白后取整刻度（概念稿 live line 同款） */
function niceScale(values: number[]) {
  const a = Math.min(...values);
  const b = Math.max(...values);
  const span = Math.max(b - a, b * 0.05, 1);
  const lo0 = Math.max(0, a - span * 0.8);
  const hi0 = b + span * 0.45;
  const raw = (hi0 - lo0) / 4;
  const mag = Math.pow(10, Math.floor(Math.log10(raw || 1)));
  // 速率为整数：刻度步长不小于 1，避免出现 0 / 0 / 1 这样的重复刻度
  const minStep = values.every(Number.isInteger) ? 1 : 0;
  let step = Math.max(minStep, [1, 2, 2.5, 5, 10].map((m) => m * mag).find((x) => x >= raw) ?? 10 * mag);
  let lo = Math.floor(lo0 / step) * step;
  let hi = Math.ceil(hi0 / step) * step;
  if (hi <= lo) hi = lo + step;
  if ((hi - lo) / step < 3 && step / 2 >= minStep) {
    step /= 2;
    lo = Math.floor(lo0 / step) * step;
    hi = Math.ceil(hi0 / step) * step;
  }
  lo = Math.max(0, lo);
  const ticks: number[] = [];
  for (let v = lo; v <= hi + 1e-9; v += step) ticks.push(Number(v.toFixed(6)));
  return { lo, hi, ticks };
}

/** 双轴：每侧 4 段等分刻度，左右网格线对齐。
 *  步长从更密的“好看”数列里取最小可行值，让峰值落在图高约 65%~88%，不再出现只占一半的情况。 */
const FIT_STEPS = [1, 1.2, 1.5, 2, 2.5, 3, 4, 5, 6, 8];
function fitScale(values: number[]) {
  const a = Math.min(...values);
  const b = Math.max(...values);
  const integer = values.every(Number.isInteger);
  const span = Math.max(b - a, b * 0.02, 1);
  const mag0 = Math.pow(10, Math.floor(Math.log10(span / 4)) - 1);
  for (let e = 0; e < 4; e += 1) {
    const mag = mag0 * Math.pow(10, e);
    for (const k of FIT_STEPS) {
      const step = k * mag;
      if (integer && step < 1) continue;
      const lo = Math.max(0, Math.floor(a / step) * step);
      const hi = lo + 4 * step;
      // 峰值上方至少留 12% 余量
      if ((b - lo) / (hi - lo) <= 0.88) {
        const ticks = Array.from({ length: 5 }, (_, i) => Number((lo + i * step).toFixed(6)));
        return { lo, hi, ticks };
      }
    }
  }
  const step = Math.max(integer ? 1 : 0, span / 3);
  const lo = 0;
  return { lo, hi: lo + 4 * step, ticks: Array.from({ length: 5 }, (_, i) => Number((lo + i * step).toFixed(6))) };
}

function buildTimeTicks(points: { t: string }[], range: RangeMinutes): string[] {
  const interval = TICK_INTERVAL[range];
  const ticks: string[] = [];
  const tailGuard = Math.max(2, Math.round(points.length * 0.03));
  let prevBucket: number | null = null;
  points.forEach((point, index) => {
    const seconds = parseTimeSeconds(point.t);
    if (seconds == null) return;
    const bucket = Math.floor(seconds / interval);
    if (prevBucket != null && bucket !== prevBucket && index > 0 && index < points.length - tailGuard) {
      ticks.push(point.t);
    }
    prevBucket = bucket;
  });
  return ticks;
}

function ChartTooltipCard({
  active,
  payload,
  label,
  unit,
  secondaryUnit,
}: {
  active?: boolean;
  payload?: readonly TooltipPayloadItem[];
  label?: string;
  unit: string;
  secondaryUnit?: string;
}) {
  if (!active || !payload || payload.length === 0) return null;
  return (
    <div className="min-w-[150px] rounded-[10px] border-border bg-popover border px-2.5 py-2 text-xs shadow-[0_8px_24px_-8px_rgba(0,0,0,0.18)]">
      <div className="mb-1 font-medium text-foreground">{label}</div>
      {payload.map((item) => (
        <div key={item.dataKey} className="flex items-center gap-1.5 leading-[1.7] text-muted-foreground">
          <span className="size-2 rounded-[2px]" style={{ background: item.color ?? PRIMARY_COLOR }} />
          {item.name ?? "速率"}
          <em className="ml-auto pl-3 font-medium not-italic tabular-nums text-foreground">
            {formatValue(item.value)} {item.dataKey === "v2" ? secondaryUnit ?? unit : unit}
          </em>
        </div>
      ))}
    </div>
  );
}

function RangeTabs({
  value,
  onChange,
}: {
  value: RangeMinutes;
  onChange: (value: RangeMinutes) => void;
}) {
  return (
    <ToggleGroup
      type="single"
      value={String(value)}
      onValueChange={(next) => {
        if (next) onChange(Number(next) as RangeMinutes);
      }}
      spacing={1}
      className="h-8 shrink-0 rounded-lg bg-muted p-[3px]"
    >
      {RANGE_OPTIONS.map((option) => (
        <ToggleGroupItem
          key={option.minutes}
          value={String(option.minutes)}
          className="h-[26px] rounded-md px-[9px] text-xs font-medium text-muted-foreground hover:bg-transparent data-[state=on]:bg-background data-[state=on]:text-foreground data-[state=on]:shadow-[0_1px_2px_rgba(0,0,0,0.08)] dark:data-[state=on]:bg-input"
        >
          {option.label}
        </ToggleGroupItem>
      ))}
    </ToggleGroup>
  );
}

function LegendValue({
  color,
  label,
  value,
  unit,
}: {
  color: string;
  label: string;
  value: string;
  unit: string;
}) {
  return (
    <span className="inline-flex items-baseline gap-1.5 whitespace-nowrap text-xs text-muted-foreground">
      <span className="size-2 self-center rounded-full" style={{ background: color }} />
      {label}
      <b className="ml-0.5 text-[15px] font-semibold tracking-[-0.01em] text-foreground tabular-nums">{value}</b>
      <small className="-ml-0.5 text-[11px]">{unit}</small>
    </span>
  );
}

/** 末端值胶囊（右侧留白区内，跟随最新点的 y） */
function EndValuePill({
  viewBox,
  text,
  color,
}: {
  viewBox?: { x?: number; y?: number; width?: number };
  text: string;
  color: string;
}) {
  if (!viewBox || viewBox.x == null || viewBox.y == null || viewBox.width == null) return null;
  const width = text.length * 7 + 18;
  return (
    <g transform={`translate(${viewBox.x + viewBox.width + 10},${viewBox.y})`}>
      <rect x={0} y={-11} width={width} height={22} rx={11} fill={color} />
      <text x={9} y={4} fontSize={11.5} fontWeight={600} fill="var(--brand-foreground)">
        {text}
      </text>
    </g>
  );
}

function renderEndDot(color: string, lastIndex: number) {
  return function EndDot(props: { cx?: number; cy?: number; index?: number }) {
    const { cx, cy, index } = props;
    if (index !== lastIndex || cx == null || cy == null) return <g key={`dot-${index}`} />;
    return (
      <g key={`dot-${index}`}>
        <circle
          cx={cx}
          cy={cy}
          r={4}
          fill={color}
          className="animate-ping"
          style={{ transformBox: "fill-box", transformOrigin: "center", animationDuration: "1.9s", opacity: 0.55 }}
        />
        <circle cx={cx} cy={cy} r={4} fill={color} stroke="var(--card)" strokeWidth={2} />
      </g>
    );
  };
}

export function TrendChart({
  title,
  unit,
  state,
  data = [],
  current,
  seriesLabel = "速率",
  secondary,
  fillHeight = false,
  description,
  showStats = false,
  height,
}: TrendChartProps) {
  const reactId = useId().replace(/:/g, "");
  const gradientId = `trend-grad-${reactId}`;
  const secondaryGradientId = `trend-grad-2-${reactId}`;
  const [rangeMinutes, setRangeMinutes] = useState<RangeMinutes>(10);
  const primaryColor = PRIMARY_COLOR;
  const secondaryColor = secondary?.color ?? SECONDARY_COLOR;

  const visibleData = useMemo(
    () => filterByRange(data, rangeMinutes),
    [data, rangeMinutes]
  );
  const secondaryMap = useMemo(
    () => new Map((secondary?.data ?? []).map((point) => [point.t, point.v])),
    [secondary?.data]
  );
  const chartData = useMemo(
    () =>
      visibleData.map((point) => ({
        t: point.t,
        v: point.v,
        v2: secondaryMap.get(point.t) ?? null,
      })),
    [visibleData, secondaryMap]
  );
  const displayCurrent =
    visibleData.length > 0 ? visibleData[visibleData.length - 1].v : current;
  const displaySecondaryCurrent =
    secondary && secondary.data.length > 0
      ? secondary.data[secondary.data.length - 1].v
      : null;
  const hasSeries = state === "ready" && visibleData.length >= 2;

  const primaryValues = useMemo(() => visibleData.map((p) => p.v).filter(Number.isFinite), [visibleData]);
  const secondaryValues = useMemo(
    () => chartData.map((p) => p.v2).filter((v): v is number => v != null && Number.isFinite(v)),
    [chartData]
  );
  const primaryScale = useMemo(
    () => (primaryValues.length ? (secondary ? fitScale(primaryValues) : niceScale(primaryValues)) : null),
    [primaryValues, secondary]
  );
  const secondaryScale = useMemo(
    () => (secondary && secondaryValues.length ? fitScale(secondaryValues) : null),
    [secondary, secondaryValues]
  );
  const timeTicks = useMemo(() => buildTimeTicks(chartData, rangeMinutes), [chartData, rangeMinutes]);
  const stats = useMemo(() => {
    if (!primaryValues.length) return null;
    return {
      max: Math.max(...primaryValues),
      avg: primaryValues.reduce((sum, v) => sum + v, 0) / primaryValues.length,
      min: Math.min(...primaryValues),
    };
  }, [primaryValues]);

  const chartHeight = height ?? (secondary ? 260 : 236);
  const lastIndex = chartData.length - 1;
  const formatTick = (t: string) => (rangeMinutes === 1 ? String(t).slice(0, 8) : String(t).slice(0, 5));

  let header: ReactNode;
  if (secondary) {
    header = (
      <div className="flex items-center gap-3 px-5 pt-5">
        <div className="flex min-w-0 flex-wrap items-center gap-x-[22px] gap-y-1.5">
          <CardTitle className="mr-0.5 text-sm font-semibold leading-none">{title}</CardTitle>
          <LegendValue
            color={primaryColor}
            label={seriesLabel}
            value={state === "ready" ? formatValue(displayCurrent) : "—"}
            unit={unit}
          />
          <LegendValue
            color={secondaryColor}
            label={secondary.label}
            value={formatValue(displaySecondaryCurrent)}
            unit={secondary.unit}
          />
        </div>
        <div className="ml-auto">
          <RangeTabs value={rangeMinutes} onChange={setRangeMinutes} />
        </div>
      </div>
    );
  } else {
    header = (
      <div className="flex items-center gap-3 px-5 pt-5">
        <div className="min-w-0">
          <CardTitle className="text-sm font-semibold leading-none">{title}</CardTitle>
          {description && <p className="mt-1.5 truncate text-[13px] text-muted-foreground">{description}</p>}
        </div>
        <div className="ml-auto flex shrink-0 items-center gap-3.5">
          <span className="inline-flex items-baseline gap-[7px] whitespace-nowrap text-xs text-muted-foreground">
            当前
            <b className="text-[15px] font-semibold tracking-[-0.01em] text-foreground tabular-nums">
              {state === "ready" ? formatValue(displayCurrent) : "—"}
            </b>
            {unit}
          </span>
          <RangeTabs value={rangeMinutes} onChange={setRangeMinutes} />
        </div>
      </div>
    );
  }

  return (
    <Card className={cn("w-full gap-0 py-0", fillHeight ? "h-full" : "")}>
      {header}

      {showStats && !secondary && (
        <div className="flex gap-6 px-5 pt-3">
          {([
            ["峰值", stats?.max],
            ["均值", stats?.avg],
            ["谷值", stats?.min],
          ] as const).map(([label, value]) => (
            <div key={label} className="text-xs text-muted-foreground">
              {label}
              <b className="block text-[17px] font-semibold tracking-[-0.02em] text-foreground tabular-nums">
                {state === "ready" ? formatValue(value) : "—"}
                <small className="ml-[3px] text-[11px] font-medium tracking-normal text-muted-foreground">{unit}</small>
              </b>
            </div>
          ))}
        </div>
      )}

      <div className={cn("px-5 pb-5 pt-2", secondary && "pt-4", fillHeight && "flex min-h-0 flex-1 flex-col")}>
        <div
          className={cn("relative", fillHeight && "min-h-0 flex-1")}
          style={fillHeight ? { minHeight: 140 } : { height: chartHeight }}
        >
          {hasSeries && primaryScale ? (
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart
                data={chartData}
                margin={secondary ? { top: 8, right: 0, bottom: 0, left: 0 } : { top: 16, right: 78, bottom: 0, left: 6 }}
              >
                <defs>
                  <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
                    {secondary ? (
                      <>
                        <stop offset="0" stopColor={primaryColor} stopOpacity={0.22} />
                        <stop offset="1" stopColor={primaryColor} stopOpacity={0} />
                      </>
                    ) : (
                      <>
                        <stop offset="0" stopColor={primaryColor} stopOpacity={0.3} />
                        <stop offset="0.8" stopColor={primaryColor} stopOpacity={0.03} />
                        <stop offset="1" stopColor={primaryColor} stopOpacity={0} />
                      </>
                    )}
                  </linearGradient>
                  <linearGradient id={secondaryGradientId} x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0" stopColor={secondaryColor} stopOpacity={0.22} />
                    <stop offset="1" stopColor={secondaryColor} stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 4" />
                <XAxis
                  dataKey="t"
                  ticks={timeTicks}
                  interval={0}
                  tickLine={false}
                  axisLine={false}
                  tickMargin={8}
                  height={24}
                  tick={{ fontSize: 10.5, fill: "var(--muted-foreground)" }}
                  tickFormatter={formatTick}
                />
                {secondary ? (
                  <>
                    <YAxis
                      yAxisId="left"
                      domain={[primaryScale.lo, primaryScale.hi]}
                      ticks={primaryScale.ticks}
                      interval={0}
                      allowDataOverflow
                      axisLine={false}
                      tickLine={false}
                      width={46}
                      tickMargin={8}
                      tick={{ fontSize: 10.5, fill: primaryColor, fillOpacity: 0.9 }}
                      tickFormatter={formatAxis}
                    />
                    <YAxis
                      yAxisId="right"
                      orientation="right"
                      domain={secondaryScale ? [secondaryScale.lo, secondaryScale.hi] : [0, "auto"]}
                      ticks={secondaryScale?.ticks}
                      interval={0}
                      allowDataOverflow
                      axisLine={false}
                      tickLine={false}
                      width={46}
                      tickMargin={8}
                      tick={{ fontSize: 10.5, fill: secondaryColor, fillOpacity: 0.9 }}
                      tickFormatter={formatAxis}
                    />
                  </>
                ) : (
                  <YAxis
                    yAxisId="left"
                    domain={[primaryScale.lo, primaryScale.hi]}
                    ticks={primaryScale.ticks}
                    interval={0}
                    allowDataOverflow
                    axisLine={false}
                    tickLine={false}
                    mirror
                    width={1}
                    tick={(props: { x?: number | string; y?: number | string; payload?: { value?: unknown } }) => {
                      const value = Number(props.payload?.value);
                      if (!Number.isFinite(value) || value === primaryScale.lo) return <g />;
                      return (
                        <text x={Number(props.x ?? 0) + 2} y={Number(props.y ?? 0) - 5} fontSize={10.5} fill="var(--muted-foreground)">
                          {formatAxis(value)}
                        </text>
                      );
                    }}
                  />
                )}
                <Tooltip
                  cursor={{ stroke: "var(--muted-foreground)", strokeOpacity: 0.55, strokeDasharray: "3 3" }}
                  content={(props) => (
                    <ChartTooltipCard
                      active={props.active}
                      payload={props.payload as unknown as readonly TooltipPayloadItem[] | undefined}
                      label={props.label as string | undefined}
                      unit={unit}
                      secondaryUnit={secondary?.unit}
                    />
                  )}
                />
                {!secondary && displayCurrent != null && (
                  <ReferenceLine
                    yAxisId="left"
                    y={visibleData[visibleData.length - 1].v}
                    stroke={primaryColor}
                    strokeOpacity={0.45}
                    strokeDasharray="2 4"
                    ifOverflow="extendDomain"
                    label={(props: { viewBox?: { x?: number; y?: number; width?: number } }) => (
                      <EndValuePill viewBox={props.viewBox} text={formatValue(visibleData[visibleData.length - 1].v)} color={primaryColor} />
                    )}
                  />
                )}
                <Area
                  yAxisId="left"
                  dataKey="v"
                  name={seriesLabel}
                  type="monotone"
                  stroke={primaryColor}
                  strokeWidth={2}
                  strokeLinejoin="round"
                  fill={`url(#${gradientId})`}
                  dot={renderEndDot(primaryColor, lastIndex)}
                  activeDot={{ r: 4.5, fill: primaryColor, stroke: "var(--card)", strokeWidth: 2.5 }}
                  isAnimationActive={false}
                />
                {secondary && (
                  <Area
                    yAxisId="right"
                    dataKey="v2"
                    name={secondary.label}
                    type="monotone"
                    stroke={secondaryColor}
                    strokeWidth={2}
                    strokeLinejoin="round"
                    fill={`url(#${secondaryGradientId})`}
                    dot={renderEndDot(secondaryColor, lastIndex)}
                    activeDot={{ r: 4.5, fill: secondaryColor, stroke: "var(--card)", strokeWidth: 2.5 }}
                    connectNulls
                    isAnimationActive={false}
                  />
                )}
              </AreaChart>
            </ResponsiveContainer>
          ) : (
            <div className="flex h-full items-center justify-center overflow-hidden rounded-lg">
              {state === "empty" && (
                <div
                  className="absolute inset-0 rounded-lg"
                  style={{ border: "1.5px dashed var(--border)", background: "var(--background)" }}
                />
              )}
              {state === "loading" && <Skeleton className="absolute inset-0 rounded-lg" />}
              {state === "error" && (
                <div
                  className="absolute inset-0 flex items-center justify-center rounded-lg"
                  style={{ background: "var(--destructive-soft)", border: "1px solid var(--destructive)" }}
                >
                  <span className="text-sm text-destructive">数据加载失败</span>
                </div>
              )}
              {(state === "empty" || state === "loading") && (
                <span className="relative text-sm text-muted-foreground/70">数据接入中</span>
              )}
              {state === "ready" && (
                <span className="relative text-sm text-muted-foreground/70">数据采集中</span>
              )}
            </div>
          )}
        </div>
      </div>
    </Card>
  );
}
