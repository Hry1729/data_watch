import { useId, useMemo, useState } from "react";
import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import type { RatePoint, TrendChartState } from "@/types";
import { useChartColors } from "@/hooks/useChartColors";

const RANGE_OPTIONS = [
  { label: "1 分钟", minutes: 1 },
  { label: "5 分钟", minutes: 5 },
  { label: "10 分钟", minutes: 10 },
] as const;

type RangeMinutes = (typeof RANGE_OPTIONS)[number]["minutes"];

export interface TrendChartProps {
  title: string;
  /** 单位标签，如 "批次/min" 或 "rows/min" */
  unit: string;
  /** 四态：empty | loading | error | ready */
  state: TrendChartState;
  /** ready 态时的数据点序列（含时间标签） */
  data?: RatePoint[];
  /** 当前值（速率卡同源）；null 时显示 -- */
  current?: number | null;
  /** 填充父容器高度；仅用于监控页的自适应布局。 */
  fillHeight?: boolean;
}

interface TooltipPayloadItem {
  value?: number;
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

function ChartTooltipCard({
  active,
  payload,
  label,
  unit,
}: {
  active?: boolean;
  payload?: readonly TooltipPayloadItem[];
  label?: string;
  unit: string;
}) {
  const c = useChartColors();
  if (!active || !payload || payload.length === 0) return null;
  return (
    <div
      style={{
        background: c.tooltipBg,
        border: `1px solid ${c.tooltipBorder}`,
        borderRadius: 8,
        boxShadow: "0 4px 12px rgba(0,0,0,0.08)",
        padding: "8px 12px",
      }}
    >
      <div className="text-xs font-semibold mb-1" style={{ color: c.text }}>{label}</div>
      <div className="flex items-center gap-2 text-xs">
        <span style={{ width: 8, height: 8, background: c.line, borderRadius: 2 }} />
        <span style={{ color: c.muted }}>速率</span>
        <span className="ml-2 font-semibold" style={{ color: c.text }}>
          {payload[0].value} {unit}
        </span>
      </div>
    </div>
  );
}

export function TrendChart({
  title,
  unit,
  state,
  data = [],
  current,
  fillHeight = false,
}: TrendChartProps) {
  const gradientId = `trend-grad-${useId().replace(/:/g, "")}`;
  const [rangeMinutes, setRangeMinutes] = useState<RangeMinutes>(10);
  const c = useChartColors();
  const visibleData = useMemo(
    () => filterByRange(data, rangeMinutes),
    [data, rangeMinutes]
  );
  const displayCurrent =
    visibleData.length > 0 ? visibleData[visibleData.length - 1].v : current;
  const hasSeries = state === "ready" && visibleData.length >= 2;

  return (
    <Card
      className={[
        "w-full",
        fillHeight ? "h-full" : "",
      ].join(" ")}
    >
      <CardHeader>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <CardTitle>{title}</CardTitle>
          <div className="flex flex-wrap items-center justify-end gap-3">
            <div className="text-right">
              {state === "ready" && displayCurrent != null ? (
                <span className="text-sm font-semibold text-foreground">
                  {displayCurrent} <span className="font-normal text-muted-foreground">{unit}</span>
                </span>
              ) : (
                <span className="text-sm font-semibold text-muted-foreground/70">-- {unit}</span>
              )}
            </div>
            <div
              className="flex overflow-hidden rounded-lg"
              style={{ border: "1px solid var(--border)", background: "var(--card)" }}
            >
              {RANGE_OPTIONS.map((option, index) => {
                const active = option.minutes === rangeMinutes;
                return (
                  <button
                    key={option.minutes}
                    type="button"
                    onClick={() => setRangeMinutes(option.minutes)}
                    className="h-8 px-3 text-xs transition-colors"
                    style={{
                      borderLeft: index === 0 ? "none" : "1px solid var(--border)",
                      background: active ? "var(--accent)" : "var(--card)",
                      color: active ? "var(--foreground)" : "var(--muted-foreground)",
                      fontWeight: active ? 600 : 500,
                    }}
                  >
                    {option.label}
                  </button>
                );
              })}
            </div>
          </div>
        </div>
      </CardHeader>

      <CardContent
        className={[
          "px-5 pb-1 pt-1",
          fillHeight ? "flex min-h-0 flex-1 flex-col" : "",
        ].join(" ")}
      >
        {/* 图表区域 */}
        <div
          className={["relative", fillHeight ? "min-h-0 flex-1" : ""].join(" ")}
          style={fillHeight ? undefined : { height: 180 }}
        >
          {hasSeries ? (
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={visibleData} margin={{ top: 8, right: 8, bottom: 0, left: 8 }}>
                <defs>
                  <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor={c.line} stopOpacity={0.3} />
                    <stop offset="95%" stopColor={c.line} stopOpacity={0.02} />
                  </linearGradient>
                </defs>
                <CartesianGrid vertical={false} stroke={c.grid} />
                <XAxis
                  dataKey="t"
                  tickLine={false}
                  axisLine={false}
                  tickMargin={8}
                  minTickGap={32}
                  tick={{ fontSize: 11, fill: c.axis }}
                  tickFormatter={(t) => String(t).slice(0, 5)}
                />
                <YAxis hide domain={[0, "auto"]} />
                <Tooltip
                  cursor={{ stroke: c.cursor, strokeDasharray: "3 3" }}
                  content={(props) => (
                    <ChartTooltipCard
                      active={props.active}
                      payload={props.payload as unknown as readonly TooltipPayloadItem[] | undefined}
                      label={props.label as string | undefined}
                      unit={unit}
                    />
                  )}
                />
                <Area
                  dataKey="v"
                  type="monotone"
                  stroke={c.line}
                  strokeWidth={2}
                  fill={`url(#${gradientId})`}
                  dot={false}
                  activeDot={{ r: 4, fill: c.line }}
                  isAnimationActive={false}
                />
              </AreaChart>
            </ResponsiveContainer>
          ) : (
            <div className="flex h-full items-center justify-center rounded-lg overflow-hidden">
              {state === "empty" && (
                <div
                  className="absolute inset-0 rounded-lg"
                  style={{ border: "1.5px dashed var(--border)", background: "var(--background)" }}
                />
              )}
              {state === "loading" && <Skeleton className="absolute inset-0 rounded-lg" />}
              {state === "error" && (
                <div
                  className="absolute inset-0 rounded-lg flex items-center justify-center"
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
      </CardContent>
    </Card>
  );
}
