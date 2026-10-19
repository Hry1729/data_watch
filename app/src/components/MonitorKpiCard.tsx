import type { ComponentType, ReactNode } from "react";
import { Card } from "@/components/ui/card";
import { cn } from "@/lib/utils";

/** 监控类页面顶部 KPI 卡：图标+标签 / 大数字+单位 / 一行说明（概念稿 kpi2） */
export function MonitorKpiCard({
  label,
  icon: Icon,
  value,
  unit,
  detail,
  tone = "normal",
  size = "default",
  denseDetail = false,
  className,
}: {
  label: string;
  icon?: ComponentType<{ className?: string }>;
  value: ReactNode;
  unit?: string;
  detail?: ReactNode;
  tone?: "normal" | "warning" | "destructive" | "success";
  /** lg：四卡布局使用的大号数字（28px） */
  size?: "default" | "lg";
  /** 说明行使用更紧凑的字号（控制台 峰值·均值·谷值） */
  denseDetail?: boolean;
  className?: string;
}) {
  const lg = size === "lg";
  return (
    <Card className={cn("min-h-[104px] gap-0 px-4 py-3.5", lg && "min-h-[112px] py-4", className)}>
      <div className="flex min-w-0 items-center gap-2 text-[13px] leading-5 text-muted-foreground">
        {Icon && <Icon className="size-[15px] shrink-0" />}
        <span className="truncate">{label}</span>
      </div>
      <p
        className={cn(
          "mt-2 truncate whitespace-nowrap text-2xl font-semibold leading-[1.15] tracking-[-0.03em] tabular-nums",
          lg && "text-[28px]",
          tone === "warning" && "text-warning",
          tone === "destructive" && "text-destructive",
          tone === "success" && "text-success",
        )}
      >
        {value}
        {unit && <small className={cn("ml-0.5 font-medium tracking-normal text-muted-foreground", lg ? "text-sm" : "text-xs")}>{unit}</small>}
      </p>
      {detail != null && (
        <p className={cn("mt-0.5 truncate text-xs leading-5 text-muted-foreground", denseDetail && "text-[11.5px] tracking-[-0.01em] tabular-nums")} title={typeof detail === "string" ? detail : undefined}>
          {detail}
        </p>
      )}
    </Card>
  );
}

/** 千分位数字；非数字显示 — */
export function formatCount(value: number | null | undefined, digits = 0): string {
  if (value == null || !Number.isFinite(value)) return "—";
  return value.toLocaleString("en-US", { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

/** 大数缩写：37.8k / 2.15M */
export function formatShort(value: number): string {
  if (value >= 1e6) return `${(value / 1e6).toFixed(value >= 1e7 ? 1 : 2).replace(/\.?0+$/, "")}M`;
  if (value >= 1e4) return `${(value / 1e3).toFixed(value >= 1e5 ? 0 : 1).replace(/\.0$/, "")}k`;
  return formatCount(value);
}

/** 表格内状态：脉冲圆点 + 文字；off 为斜纹空心点（概念稿 pcl-st） */
export function StatusDot({
  tone,
  label,
}: {
  tone: "success" | "warning" | "off";
  label: string;
}) {
  const color = tone === "success" ? "var(--success)" : tone === "warning" ? "var(--warning)" : "var(--muted-foreground)";
  return (
    <span className="inline-flex items-center gap-1.5 text-xs" style={{ color }}>
      {tone === "off" ? (
        <span
          className="size-[7px] rounded-full"
          style={{
            background: "repeating-linear-gradient(-45deg, var(--muted-foreground) 0 1.5px, transparent 1.5px 3px)",
            boxShadow: "inset 0 0 0 1px var(--muted-foreground)",
          }}
        />
      ) : (
        <span className="relative size-[7px]">
          <span className="absolute inset-0 animate-ping rounded-full bg-current opacity-50 [animation-duration:2.4s]" />
          <span className="absolute inset-0 rounded-full bg-current" />
        </span>
      )}
      {label}
    </span>
  );
}

/** 监控页表格：粘性表头、紧凑行高（概念稿 pcl-scroll .tbl） */
export const MONITOR_TABLE_SCROLL_CLASS =
  "min-h-0 flex-1 overflow-y-auto border-t border-border [&_[data-slot=table-container]]:overflow-visible";
export const MONITOR_TH_CLASS =
  "sticky top-0 z-[1] h-9 bg-card px-3 text-xs font-medium text-muted-foreground shadow-[inset_0_-1px_0_var(--border)] first:pl-5 last:pr-5";
export const MONITOR_TD_CLASS = "h-[38px] px-3 py-0 text-xs first:pl-5 last:pr-5";
