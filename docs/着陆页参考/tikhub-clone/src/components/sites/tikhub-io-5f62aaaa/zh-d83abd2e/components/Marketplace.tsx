import { ArrowRightIcon } from "../../shared/icons";

type MarketplaceCard = {
  title: string;
  desc: string;
};

const CARDS: MarketplaceCard[] = [
  {
    title: "AI 智能体",
    desc: "为 AI 应用提供开箱即用的工具与数据。",
  },
  {
    title: "API",
    desc: "把你的服务通过我们分发到所有集成方。",
  },
  {
    title: "网络数据采集",
    desc: "共享数据集与爬取能力,加速研究。",
  },
];

function LeftContent() {
  return (
    <div>
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-medium text-accent">市场与合作伙伴</span>
        <span className="inline-flex items-center rounded-full bg-accent/20 px-2.5 py-0.5 text-xs font-medium text-accent">
          2026 年 8 月上线
        </span>
      </div>
      <h2 className="mt-4 text-3xl md:text-4xl lg:text-5xl font-bold tracking-tight">
        与 AI 时代一起 加速前进
      </h2>
      <p className="mt-4 text-white/70">
        把你的产品带给 30,000+ 开发者、3亿+ 每日请求,以及 16 个全球主流社交平台。
      </p>
      <div className="mt-8 grid grid-cols-1 sm:grid-cols-3 gap-4">
        {CARDS.map((c) => (
          <div
            key={c.title}
            className="rounded-xl bg-white/5 p-4 border border-white/10"
          >
            <h3 className="font-semibold">{c.title}</h3>
            <p className="mt-1 text-sm text-white/60">{c.desc}</p>
          </div>
        ))}
      </div>
      <div className="mt-8 flex flex-wrap gap-3">
        <a
          href="#"
          className="inline-flex items-center gap-2 rounded-lg bg-white px-4 py-2.5 text-sm font-medium text-inverse transition-colors hover:bg-white/90"
        >
          提交您的产品 <ArrowRightIcon className="w-4 h-4" />
        </a>
        <a
          href="#"
          className="inline-flex items-center gap-2 rounded-lg border border-white/20 bg-white/5 px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-white/10"
        >
          合作伙伴计划 <ArrowRightIcon className="w-4 h-4" />
        </a>
      </div>
      <p className="mt-6 text-sm text-white/60">
        现已开放早期提交审核——联系我们,在上线前锁定您的位置。
      </p>
    </div>
  );
}

function RightCommissionCard() {
  return (
    <div className="rounded-2xl bg-white/5 p-8 border border-white/10">
      <h3 className="text-2xl md:text-3xl font-bold">仅收取 10% 佣金</h3>
      <p className="mt-2 text-white/70">对比约 20% 的行业标准佣金。</p>
      <div className="mt-8 space-y-4">
        <div>
          <div className="flex items-baseline justify-between text-sm">
            <span>TikHub</span>
            <span className="font-semibold">10%</span>
          </div>
          <div className="mt-1 h-2 rounded-full bg-white/10 overflow-hidden">
            <div className="h-full w-1/2 rounded-full bg-accent" />
          </div>
        </div>
        <div>
          <div className="flex items-baseline justify-between text-sm">
            <span>行业标准</span>
            <span className="font-semibold text-white/70">约 20%</span>
          </div>
          <div className="mt-1 h-2 rounded-full bg-white/10 overflow-hidden">
            <div className="h-full w-full rounded-full bg-white/40" />
          </div>
        </div>
      </div>
      <p className="mt-8 text-sm text-white/60">
        透明的佣金结构,每月 1 号结算。无隐藏费用,无强制促销。
      </p>
    </div>
  );
}

export function Marketplace() {
  return (
    <section className="scroll-mt-20">
      <div className="max-w-7xl mx-auto px-6 py-20 md:py-28">
        <div className="bg-inverse text-inverse-foreground rounded-3xl p-8 md:p-12">
          <div className="grid lg:grid-cols-2 gap-12">
            <LeftContent />
            <RightCommissionCard />
          </div>
        </div>
      </div>
    </section>
  );
}
