import type { Platform } from "../../shared/types";
import { PLATFORMS } from "../../shared/icons";

function SectionHeader() {
  return (
    <div className="max-w-6xl mx-auto px-6 pt-20 md:pt-28 pb-10 text-center">
      <p className="text-sm text-muted-foreground">社媒数据,触手可及</p>
      <h2 className="mt-2 text-3xl md:text-4xl lg:text-5xl font-bold tracking-tight">
        16 个平台 · 1 个 API
      </h2>
      <p className="mt-3 text-muted-foreground">
        一套 API 密钥、一份文档、一致的响应结构。覆盖所有主流社交平台。
      </p>
    </div>
  );
}

function PlatformCard({ platform }: { platform: Platform }) {
  const Icon = platform.icon;
  return (
    <a
      href="#"
      className="group flex-shrink-0 w-[220px] md:w-[250px] lg:w-[280px] px-2 md:px-3 transition-transform hover:-translate-y-1"
    >
      <div className="bg-surface/50 backdrop-blur-sm rounded-xl border border-accent/15 shadow-[0_0_12px_rgba(99,102,241,0.08)] p-5 h-full transition-all group-hover:shadow-[0_0_20px_rgba(99,102,241,0.18)] group-hover:bg-surface/70 group-hover:border-accent/30">
        <div className="flex items-start gap-3">
          <div className="w-10 h-10 rounded-lg bg-card border border-border/40 flex items-center justify-center shrink-0 text-foreground transition-transform group-hover:scale-110">
            <Icon className="w-5 h-5" />
          </div>
          <div className="min-w-0 flex-1">
            <h3 className="text-sm font-semibold truncate">{platform.name}</h3>
            <p className="text-[11px] text-muted-foreground/80 mb-1">
              {platform.subtitle}
            </p>
            <p className="text-xs text-muted-foreground line-clamp-2">
              {platform.description}
            </p>
          </div>
        </div>
      </div>
    </a>
  );
}

function MarqueeRow({ direction }: { direction: "left" | "right" }) {
  // 把 PLATFORMS 数组重复两次(无缝循环)
  const items = [...PLATFORMS, ...PLATFORMS];
  const animationClass =
    direction === "left" ? "animate-scroll-left" : "animate-scroll-right";
  return (
    <div className={`flex hover-pause ${animationClass}`}>
      {items.map((p, i) => (
        <PlatformCard key={`${p.id}-${i}`} platform={p} />
      ))}
    </div>
  );
}

export function PlatformMarquee() {
  return (
    <>
      <SectionHeader />
      <div className="relative overflow-hidden w-[100dvw] max-w-[100vw] -ml-[50vw] left-1/2 space-y-3 mask-fade-edges">
        <MarqueeRow direction="left" />
        <MarqueeRow direction="right" />
      </div>
    </>
  );
}
