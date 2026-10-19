import { useRef, type ReactNode } from "react";

/** 点位数值卡（实时监控 / 历史数据共用）：名称 + 色点、数值、右侧 Δ/附注、地址 · 类型 · 单位 */
export type PointTileValue = string | number | boolean | null | undefined;

function tileNumber(value: PointTileValue): number | null {
  if (typeof value === "boolean") return value ? 1 : 0;
  if (typeof value === "number") return Number.isFinite(value) ? value : null;
  if (value == null || !value.trim() || value === "--") return null;
  const number = Number(value);
  return Number.isFinite(number) ? number : null;
}

export function formatTileValue(value: PointTileValue) {
  if (value == null) return "—";
  if (typeof value === "boolean") return value ? "ON" : "OFF";
  if (typeof value === "number") return Number.isInteger(value) ? String(value) : value.toFixed(2);
  return value || "--";
}

export interface PointValueTileProps {
  name: string;
  color: string;
  ioAddress?: string;
  dataType?: string;
  unit?: string;
  value: PointTileValue;
  /** 质量异常时数值标红 */
  bad?: boolean;
  /** 传入时显示 Δ 并在跳变时闪烁（实时监控）；不传则显示 aside */
  previous?: number | null;
  /** 右侧附注（历史数据：最新值时间） */
  aside?: ReactNode;
}

export function PointValueTile({ name, color, ioAddress, dataType, unit, value, bad = false, previous, aside }: PointValueTileProps) {
  const isBool = (dataType ?? "").toUpperCase() === "BOOL";
  const trend = previous !== undefined;
  const current = tileNumber(value);
  const delta = trend && current != null && previous != null ? current - previous : null;
  const threshold = isBool ? 0.5 : Math.max(0.3, Math.abs(current ?? 0) * 0.005);
  const flash = delta != null && Math.abs(delta) >= threshold;
  const flashKey = useRef(0);
  const lastFlash = useRef<PointTileValue>(null);
  if (flash && lastFlash.current !== value) { flashKey.current += 1; lastFlash.current = value; }
  return (
    <div className="relative min-w-0 overflow-hidden rounded-[10px] bg-muted/55 px-3 py-2.5">
      {flashKey.current > 0 && <span key={flashKey.current} className="pointer-events-none absolute inset-0 rounded-[inherit] bg-brand/15 opacity-0 animate-in duration-800 ease-out" />}
      <div className="flex items-center gap-1.5 text-xs font-medium">
        <span className="min-w-0 flex-1 truncate" title={name}>{name}</span>
        <span className="size-2 shrink-0 rounded-full" style={{ background: color }} />
      </div>
      <div className="mt-1 flex items-baseline gap-1 leading-[1.2]">
        {isBool && typeof value === "boolean" ? (
          <span className={`inline-flex h-6 items-center gap-1.5 rounded-full px-2.5 text-[13px] font-semibold ${value ? "bg-success-soft text-success" : "bg-muted text-muted-foreground"}`}><span className="size-1.5 rounded-full bg-current" />{formatTileValue(value)}</span>
        ) : (
          <span key={flashKey.current} className={`truncate font-mono text-[22px] font-semibold tracking-[-0.02em] tabular-nums ${bad ? "text-destructive" : value == null ? "text-muted-foreground" : "text-foreground"}`}>{formatTileValue(value)}</span>
        )}
        {trend ? (
          <span className={`ml-auto shrink-0 text-[11px] font-medium ${isBool ? "text-muted-foreground" : delta != null && delta > 0.05 ? "text-success" : delta != null && delta < -0.05 ? "text-warning" : "text-muted-foreground"}`}>
            {isBool ? (delta ? "刚刚切换" : "") : delta == null || Math.abs(delta) < 0.05 ? "—" : `${delta > 0 ? "▲" : "▼"} ${Math.abs(delta).toFixed(2)}`}
          </span>
        ) : (
          <span className="ml-auto shrink-0 text-[11px] font-medium tabular-nums text-muted-foreground">{aside}</span>
        )}
      </div>
      <p className="mt-0.5 truncate font-mono text-[10.5px] text-muted-foreground">{[ioAddress, dataType || "--", unit].filter(Boolean).join(" · ")}</p>
    </div>
  );
}

/** 数值卡行：始终单行，约 6 张等宽，多了横向滚动（细滚动条） */
export function PointValueTileRow({ children, empty }: { children?: ReactNode; empty: ReactNode }) {
  const hasTiles = Array.isArray(children) ? children.length > 0 : Boolean(children);
  if (!hasTiles) return <div className="flex h-[74px] flex-col items-center justify-center gap-1 text-[13px] text-muted-foreground">{empty}</div>;
  return (
    <div className="overflow-x-auto overflow-y-hidden pb-1 [scrollbar-width:thin]">
      <div className="grid grid-flow-col auto-cols-[minmax(180px,calc((100%-3.125rem)/6))] gap-2.5">{children}</div>
    </div>
  );
}
