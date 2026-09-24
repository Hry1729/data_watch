"use client";

import { useEffect, useState } from "react";
import { ArrowRightIcon } from "../../shared/icons";

const ROTATING_BADGES = [
  "监控 YouTube 频道分析",
  "分析 Instagram 互动趋势",
  "大规模提取 TikTok 用户数据",
  "通过 API",
];

function RotatingBadge() {
  const [idx, setIdx] = useState(0);
  useEffect(() => {
    const t = setInterval(
      () => setIdx((i) => (i + 1) % ROTATING_BADGES.length),
      2500,
    );
    return () => clearInterval(t);
  }, []);
  return (
    <div className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-3 py-1.5">
      <span className="relative w-2 h-2 rounded-full bg-emerald-500" />
      <span key={idx} className="text-xs font-medium animate-text-reveal">
        {ROTATING_BADGES[idx]}
      </span>
    </div>
  );
}

export function Hero() {
  return (
    <section className="relative z-10 bg-hero-purple-soft">
      <div className="max-w-6xl mx-auto px-6 py-20 md:py-28 text-center">
        <RotatingBadge />

        <h1 className="mt-6 text-4xl md:text-5xl lg:text-6xl font-bold leading-[1.1] tracking-tight">
          社交数据基础设施{" "}
          <span className="relative inline-block">
            <span
              className="absolute -inset-1.5 border-2 border-dashed border-foreground/30 rounded-lg pointer-events-none"
              aria-hidden
            />
            为
          </span>{" "}
          <span className="text-gradient-accent">AI 时代而生</span>
        </h1>

        <p className="mt-6 text-lg md:text-xl text-muted-foreground">
          为 AI 应用、代理和研究提供生产级社交媒体数据
        </p>

        <div className="mt-10 flex flex-wrap items-center justify-center gap-3">
          <a
            href="#"
            className="inline-flex items-center gap-2 rounded-lg bg-inverse px-4 py-2.5 text-sm font-medium transition-transform hover:scale-105"
          >
            快速开始
            <ArrowRightIcon className="h-4 w-4" />
          </a>
          <a
            href="#"
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-4 py-2.5 text-sm font-medium transition-colors hover:bg-muted"
          >
            数据 API
            <ArrowRightIcon className="h-4 w-4" />
          </a>
          <a
            href="#"
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-4 py-2.5 text-sm font-medium transition-colors hover:bg-muted"
          >
            数据集
            <ArrowRightIcon className="h-4 w-4" />
          </a>
          <a
            href="#"
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-4 py-2.5 text-sm font-medium transition-colors hover:bg-muted"
          >
            入驻市场
            <span className="ml-1 rounded bg-accent px-1.5 py-0.5 text-[10px] text-white">
              新
            </span>
            <ArrowRightIcon className="h-4 w-4" />
          </a>
        </div>

        <div className="mt-10 flex flex-wrap items-center justify-center gap-x-3 gap-y-2 text-xs text-muted-foreground">
          <span>16 个平台</span>
          <span aria-hidden>·</span>
          <span>1,000+ 接口</span>
          <span aria-hidden>·</span>
          <span>10亿+ 数据集记录</span>
          <span aria-hidden>·</span>
          <span>低至 $0.001/次请求</span>
        </div>
      </div>
    </section>
  );
}
