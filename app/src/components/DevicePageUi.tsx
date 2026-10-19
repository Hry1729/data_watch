/**
 * 设备监控页（驱动 / 设备 / 点位 / 实时 / 历史）共用的视觉片段，对齐概念稿 v5。
 * 只放样式与纯展示组件，不含业务逻辑。
 */
import type { ReactNode } from "react";
import type { LucideIcon } from "lucide-react";
import { Card } from "@/components/ui/card";
import { cn } from "@/lib/utils";

/** 表格外框：细边框 + 10px 圆角，内含表格与分页栏 */
export const TABLE_FRAME = "overflow-hidden rounded-[10px] border bg-card";
/** 表格整体字号 */
export const TABLE_TEXT = "text-[13px]";
/** 表头行：40px，浅灰底 */
export const HEAD_ROW = "h-10 border-b bg-muted/55 hover:bg-muted/55";
/** 表头单元格 */
export const TH = "h-10 px-3 text-left text-[13px] font-medium text-muted-foreground";
/** 普通单元格 */
export const TD = "px-3 py-0";
/** 选中行底色 */
export const ROW_SELECTED = "data-[state=selected]:bg-brand/[0.07]";
/** 卡片内表格下方分页栏 */
export const PAGER = "flex flex-wrap items-center gap-2 border-t px-3 py-2.5 text-xs text-muted-foreground";
/** 分页图标按钮 */
export const PAGER_BTN = "size-7 rounded-md";
/** 卡头控件统一高度/字号 */
export const CTRL = "!h-8 text-[13px] md:text-[13px]";

/** 类型 Tabs：32px 高、13px 字、选中项白底阴影（作用于内部 TabsTrigger） */
export const TAB_LIST = "w-max [&_[data-slot=tabs-trigger]]:px-2.5 [&_[data-slot=tabs-trigger]]:text-[13px] [&_[data-slot=tabs-trigger][data-active]]:shadow-[0_1px_2px_rgba(0,0,0,.08)]";
/** 卡头主按钮（品牌蓝） */
export const PRIMARY_BTN = "h-8 gap-1.5 bg-brand px-3 text-[13px] text-brand-foreground hover:bg-brand/90";
/** 卡头危险按钮（实心红） */
export const DANGER_BTN = "h-8 gap-1.5 bg-destructive px-3 text-[13px] text-white hover:bg-destructive/90 dark:bg-destructive dark:text-white disabled:opacity-45";
/** 卡头图标按钮（导入 / 导出） */
export const ICON_BTN = "size-8 px-0 [&_svg:not([class*='size-'])]:size-3.5";
/** 卡头描边按钮 */
export const OUTLINE_BTN = "h-8 gap-1.5 px-3 text-[13px]";
/** 行内操作图标按钮 */
export const ROW_ICON = "size-7 rounded-md text-muted-foreground hover:text-foreground";

/** 删除按钮内的已选数量徽标 */
export function SelectedCount({ count }: { count: number }) {
  return <span className="ml-0.5 inline-flex h-[18px] min-w-[18px] items-center justify-center rounded-full bg-white/25 px-1.5 text-[11px] tabular-nums text-white">{count}</span>;
}

export type Tone = "ok" | "warn" | "bad" | "brand" | "muted";

const TONE: Record<Tone, string> = {
  ok: "bg-success-soft text-success",
  warn: "bg-warning-soft text-warning",
  bad: "bg-destructive-soft text-destructive",
  brand: "bg-brand-soft text-brand",
  muted: "bg-muted text-muted-foreground",
};

/** 概念稿 .badge：20px 高、6px 圆角、12px 字，可带前置圆点 */
export function SoftBadge({ tone, dot = true, className, children }: { tone: Tone; dot?: boolean; className?: string; children: ReactNode }) {
  return (
    <span className={cn("inline-flex h-5 shrink-0 items-center gap-1.5 whitespace-nowrap rounded-md px-2 text-xs font-medium", TONE[tone], className)}>
      {dot && <span className="size-1.5 rounded-full bg-current" />}
      {children}
    </span>
  );
}

/** 胶囊状态（通讯状态等）：在线为描边胶囊，其余为软底胶囊 */
export function StatusPill({ tone, children }: { tone: Tone; children: ReactNode }) {
  return (
    <span className={cn(
      "inline-flex h-[22px] items-center gap-1.5 whitespace-nowrap rounded-full px-2 text-xs font-medium",
      tone === "ok" ? "border text-success" : cn("border border-transparent", TONE[tone]),
    )}
    >
      <span className="size-1.5 rounded-full bg-current" />
      {children}
    </span>
  );
}

/** 文字前的小圆点（数据状态等） */
export function DotText({ tone, children }: { tone: Tone; children: ReactNode }) {
  const color = tone === "ok" ? "bg-success" : tone === "warn" ? "bg-warning" : tone === "bad" ? "bg-destructive" : tone === "brand" ? "bg-brand" : "bg-muted-foreground";
  return (
    <span className={cn("inline-flex items-center gap-1.5 text-[13px]", tone === "muted" && "text-muted-foreground")}>
      <span className={cn("size-1.5 rounded-full", color)} />
      {children}
    </span>
  );
}

/** 顶部 KPI 卡：图标 + 标签 / 大数字 / 可选一行说明 */
export function KpiTile({ icon: Icon, label, value, unit, sub }: { icon: LucideIcon; label: string; value: string; unit?: string; sub?: string }) {
  return (
    <Card className="gap-0 px-4 py-3.5">
      <div className="flex items-center gap-2 text-[13px] text-muted-foreground">
        <Icon className="size-[15px]" strokeWidth={1.8} />
        <span className="truncate">{label}</span>
      </div>
      <p className="mt-1.5 truncate text-[26px] font-semibold leading-[1.15] tracking-[-0.03em] tabular-nums text-foreground">
        {value}
        {unit && <span className="ml-0.5 text-sm font-medium tracking-normal text-muted-foreground">{unit}</span>}
      </p>
      {sub && <p className="mt-2 truncate text-xs text-muted-foreground">{sub}</p>}
    </Card>
  );
}

/** 已选数量圆角徽标（选择点位按钮内） */
export function CountChip({ count, className }: { count: number; className?: string }) {
  return <span className={cn("inline-grid h-[18px] min-w-[18px] place-items-center rounded-full bg-brand-soft px-1.5 text-[11px] font-semibold text-brand", className)}>{count}</span>;
}

export interface ChartTipRow { key: string; label: string; color: string; value: string }

/** 概念稿 .c-tip 风格的浮动 tooltip：时间标题 + 色块/名称/右对齐数值 */
export function ChartTipCard({ title, rows }: { title: string; rows: ChartTipRow[] }) {
  if (!rows.length) return null;
  return (
    <div className="min-w-[160px] rounded-[10px] border bg-popover px-2.5 py-2 text-xs shadow-[0_8px_24px_-8px_rgba(0,0,0,.18)]">
      <p className="mb-1 font-mono font-medium text-foreground">{title}</p>
      {rows.map((row) => (
        <div key={row.key} className="flex items-center gap-1.5 leading-[1.7] text-muted-foreground">
          <span className="size-2 shrink-0 rounded-[2px]" style={{ background: row.color }} />
          <span className="truncate">{row.label}</span>
          <span className="ml-auto pl-3 font-medium tabular-nums text-foreground">{row.value}</span>
        </div>
      ))}
    </div>
  );
}

/** 概念稿 .lg 图例胶囊：可点击切换显隐 */
export function LegendPill({ color, label, off, onClick }: { color: string; label: string; off?: boolean; onClick?: () => void }) {
  return (
    <button type="button" onClick={onClick} className={cn("flex h-[26px] items-center gap-1.5 rounded-full border px-2.5 text-xs text-muted-foreground transition-opacity", off && "opacity-45")}>
      <span className="size-2 rounded-full" style={{ background: off ? "var(--muted-foreground)" : color }} />
      {label}
    </button>
  );
}

/** 卡头分段切换（概念稿 .tabs），用于图表类型 */
export function SegTabs<T extends string>({ value, options, onChange }: { value: T; options: readonly (readonly [T, string])[]; onChange: (value: T) => void }) {
  return (
    <div className="inline-flex h-8 items-center rounded-lg bg-muted p-[3px]">
      {options.map(([key, label]) => (
        <button
          key={key}
          type="button"
          onClick={() => onChange(key)}
          className={cn(
            "h-full rounded-md px-2.5 text-[13px] font-medium text-muted-foreground transition-colors",
            value === key && "bg-background text-foreground shadow-[0_1px_2px_rgba(0,0,0,.08)] dark:bg-input",
          )}
        >
          {label}
        </button>
      ))}
    </div>
  );
}

/** 图表系列调色板（与概念稿一致） */
export const SERIES_COLORS = [
  "var(--brand)",
  "var(--success)",
  "var(--warning)",
  "color-mix(in oklab, var(--brand) 50%, var(--destructive))",
  "color-mix(in oklab, var(--brand) 35%, var(--success))",
  "var(--muted-foreground)",
];
