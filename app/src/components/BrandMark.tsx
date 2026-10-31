import { useId } from "react";
import { cn } from "@/lib/utils";

/**
 * Data Watch 品牌标志「Pleat W」：四道斜笔组成 W，顶端落在同一段圆弧上。
 * 两道上行笔为冰青渐变，两道下行笔跟随 currentColor（默认 --brand-ink）。
 */
const UP_STROKES = [
  "M25.5 8.28A76 76 0 0 1 38.5 8.28L21 56H17Z",
  "M50.5 10.29A76 76 0 0 1 63.5 14.84L47 56H43Z",
];
const DOWN_STROKES = [
  "M0.5 14.84A76 76 0 0 1 13.5 10.29L21 56H17Z",
  "M25.5 8.28A76 76 0 0 1 38.5 8.28L47 56H43Z",
];

export function BrandMark({ className, title }: { className?: string; title?: string }) {
  const gradientId = `dw-mark-${useId().replace(/:/g, "")}`;
  return (
    <svg
      viewBox="0 0 64 64"
      fill="none"
      role={title ? "img" : undefined}
      aria-label={title}
      aria-hidden={title ? undefined : true}
      className={cn("shrink-0 text-brand-ink", className)}
    >
      <defs>
        <linearGradient id={gradientId} x1="0" y1="8" x2="0" y2="56" gradientUnits="userSpaceOnUse">
          <stop stopColor="var(--brand-mark-from)" />
          <stop offset="1" stopColor="var(--brand-mark-to)" />
        </linearGradient>
      </defs>
      {UP_STROKES.map((d) => <path key={d} d={d} fill={`url(#${gradientId})`} />)}
      {DOWN_STROKES.map((d) => <path key={`down-${d}`} d={d} fill="currentColor" />)}
    </svg>
  );
}

/** 软件名字标：Data 用 800，Watch 用 500（Plus Jakarta Sans，字距 -0.02em）。 */
export function BrandWordmark({ className }: { className?: string }) {
  return (
    <span className={cn("font-brand whitespace-nowrap leading-none tracking-[-0.02em] text-brand-ink", className)}>
      <span className="font-extrabold">Data</span>
      <span className="font-medium">Watch</span>
    </span>
  );
}

/** 标志 + 字标横排组合，标志高度为字号的 1.18 倍，间距 0.3em。 */
export function BrandLockup({ className }: { className?: string }) {
  return (
    <span className={cn("inline-flex items-center gap-[0.3em] leading-none", className)}>
      <BrandMark className="size-[1.18em]" />
      <BrandWordmark />
    </span>
  );
}
