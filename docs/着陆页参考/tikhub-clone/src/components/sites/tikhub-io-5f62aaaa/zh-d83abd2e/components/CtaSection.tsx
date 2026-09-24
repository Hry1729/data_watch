import { ArrowRightIcon } from "../../shared/icons";

export function CtaSection() {
  return (
    <section>
      <div className="max-w-3xl mx-auto px-6 py-20 md:py-28 text-center">
        <h2 className="text-3xl md:text-4xl font-bold tracking-tight">
          社媒数据不会自己变成结构化。
        </h2>
        <p className="text-muted-foreground mt-4">
          注册即送 $0.05 免费额度——足以测试任意端点。无需信用卡。
        </p>
        <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
          <a
            href="#"
            className="inline-flex items-center gap-2 rounded-lg bg-inverse px-4 py-2.5 text-sm font-medium transition-transform hover:scale-105"
          >
            免费开始
            <ArrowRightIcon className="h-4 w-4" />
          </a>
          <a
            href="https://calendar.app.google/AQLMWVdfAksrWeHh9"
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-4 py-2.5 text-sm font-medium transition-colors hover:bg-muted"
          >
            预约通话
            <ArrowRightIcon className="h-4 w-4" />
          </a>
        </div>
      </div>
    </section>
  );
}
