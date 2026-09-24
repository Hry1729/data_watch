import { ShieldIcon } from "../../shared/icons";

const STATS = [
  { value: "30,000+", label: "开发者与数据团队" },
  { value: "3亿+", label: "每日请求处理量" },
  { value: "16", label: "覆盖社媒平台" },
  { value: "10亿+", label: "数据集记录交付量" },
];

function StatItem({ value, label }: { value: string; label: string }) {
  return (
    <div className="border-l border-border pl-4 first:border-l-0">
      <div className="text-2xl md:text-3xl font-bold tracking-tight">{value}</div>
      <div className="text-xs text-muted-foreground mt-1">{label}</div>
    </div>
  );
}

export function StatsStrip() {
  return (
    <div className="border-y">
      <div className="max-w-7xl mx-auto px-6 py-12 grid lg:grid-cols-5 gap-6 items-center">
        <div className="lg:col-span-1">
          <p className="text-sm text-muted-foreground">
            深受全球数据团队与 AI 开发者信赖
          </p>
        </div>
        <div className="lg:col-span-3 grid grid-cols-2 md:grid-cols-4 gap-6">
          {STATS.map((stat) => (
            <StatItem key={stat.label} value={stat.value} label={stat.label} />
          ))}
        </div>
        <div className="lg:col-span-1 flex items-center gap-2">
          <div className="inline-flex items-center gap-2 rounded-full bg-accent/10 px-3 py-1.5 text-accent">
            <ShieldIcon className="w-5 h-5" />
            <span className="text-sm font-medium">GDPR</span>
          </div>
          <div className="inline-flex items-center gap-2 rounded-full bg-accent/10 px-3 py-1.5 text-accent">
            <ShieldIcon className="w-5 h-5" />
            <span className="text-sm font-medium">CCPA</span>
          </div>
        </div>
      </div>
    </div>
  );
}
