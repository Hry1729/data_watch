import Image from "next/image";
import { LinkedinIcon, XTwitterIcon } from "../../shared/icons";

const COLUMNS: { title: string; items: string[] }[] = [
  {
    title: "产品",
    items: [
      "社交媒体 API",
      "数据集",
      "解决方案",
      "演示应用",
      "直播间 API",
    ],
  },
  {
    title: "平台 API",
    items: [
      "TikTok API",
      "Instagram API",
      "YouTube API",
      "X (Twitter) API",
      "Douyin API",
    ],
  },
  {
    title: "开发者",
    items: [
      "快速入门",
      "文档",
      "API 参考",
      "Python SDK",
      "MCP 集成",
      "教程",
      "更新日志",
      "术语表",
      "博客",
      "集成",
    ],
  },
  {
    title: "公司",
    items: [
      "关于我们",
      "定价",
      "企业版",
      "合作伙伴计划",
      "招聘",
      "联系",
    ],
  },
  {
    title: "法律",
    items: ["隐私政策", "服务条款"],
  },
];

function LinkColumn({ title, items }: { title: string; items: string[] }) {
  return (
    <div>
      <h4 className="text-sm font-semibold text-white">{title}</h4>
      <ul className="mt-4 space-y-2">
        {items.map((item) => (
          <li key={item}>
            <a href="#" className="text-sm text-white/60 hover:text-white transition-colors">
              {item}
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}

function BrandColumn() {
  return (
    <div className="col-span-2 md:col-span-3 lg:col-span-1">
      <a href="/" className="inline-flex items-center gap-2">
        <Image
          src="/sites/tikhub-io-5f62aaaa/shared/logo.jpeg"
          alt="TikHub"
          width={32}
          height={32}
          className="rounded"
        />
        <span className="text-lg font-semibold text-white">TikHub</span>
      </a>
      <p className="mt-4 text-sm text-white/60 max-w-xs">
        社交媒体数据基础设施:覆盖 TikTok、Instagram、抖音等 16 个平台的 1,000+ 实时 API、可直接用于训练的数据集与 MCP 工具。
      </p>
      <div className="mt-6 flex items-center gap-3">
        <a
          href="#"
          aria-label="X (Twitter)"
          className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-white/10 text-white/60 hover:text-white hover:border-white/30 transition-colors"
        >
          <XTwitterIcon className="h-4 w-4" />
        </a>
        <a
          href="#"
          aria-label="LinkedIn"
          className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-white/10 text-white/60 hover:text-white hover:border-white/30 transition-colors"
        >
          <LinkedinIcon className="h-4 w-4" />
        </a>
        <a
          href="#"
          aria-label="Discord"
          className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-white/10 text-white/60 hover:text-white hover:border-white/30 transition-colors"
        >
          <svg
            viewBox="0 0 24 24"
            fill="currentColor"
            className="h-4 w-4"
          >
            <path d="M20.32 4.37a19.7 19.7 0 0 0-4.89-1.52.07.07 0 0 0-.07.03 13.7 13.7 0 0 0-.6 1.24 18.2 18.2 0 0 0-5.5 0 12.6 12.6 0 0 0-.62-1.24.07.07 0 0 0-.07-.03A19.7 19.7 0 0 0 3.7 4.37a.06.06 0 0 0-.03.02C.5 9.05-.32 13.6.1 18.1a.08.08 0 0 0 .03.05 19.9 19.9 0 0 0 5.99 3.03.07.07 0 0 0 .08-.03 14.2 14.2 0 0 0 1.23-2 .07.07 0 0 0-.04-.1 13.1 13.1 0 0 1-1.87-.9.07.07 0 0 1 0-.12c.13-.1.25-.2.37-.3a.07.07 0 0 1 .07-.01c3.92 1.79 8.18 1.79 12.06 0a.07.07 0 0 1 .07.01l.37.3a.07.07 0 0 1 0 .12c-.6.35-1.23.65-1.88.9a.07.07 0 0 0-.04.1c.37.7.78 1.36 1.23 2a.07.07 0 0 0 .08.03 19.84 19.84 0 0 0 6-3.03.08.08 0 0 0 .03-.05c.5-5.18-.84-9.7-3.55-13.71a.06.06 0 0 0-.03-.02ZM8.02 15.33c-1.18 0-2.16-1.08-2.16-2.41 0-1.33.95-2.42 2.16-2.42 1.22 0 2.18 1.1 2.16 2.42 0 1.33-.95 2.41-2.16 2.41Zm7.97 0c-1.18 0-2.16-1.08-2.16-2.41 0-1.33.95-2.42 2.16-2.42 1.22 0 2.18 1.1 2.16 2.42 0 1.33-.94 2.41-2.16 2.41Z" />
          </svg>
        </a>
      </div>
    </div>
  );
}

export function Footer() {
  return (
    <footer className="bg-inverse text-inverse-foreground">
      <div className="max-w-7xl mx-auto px-6 py-16">
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-8 mb-12">
          <BrandColumn />
          {COLUMNS.map((col) => (
            <LinkColumn key={col.title} title={col.title} items={col.items} />
          ))}
        </div>
        <div className="border-t border-white/10 pt-8">
          <p className="text-xs text-white/60 max-w-5xl">
            TikHub 是一家 API 开发基础设施服务提供商。我们提供实现数据访问的工具与服务,但我们并不拥有、转售或再分发底层数据本身。最终用户须自行负责确定其在其自有系统中如何使用、存储和处理所获取的数据。
          </p>
          <div className="mt-6 flex flex-wrap items-center justify-between gap-4">
            <p className="text-sm text-white/60">© 2026 TikHub. 保留所有权利。</p>
            <div className="flex items-center gap-4 text-sm text-white/60">
              <a
                href="mailto:team@tikhub.io"
                className="hover:text-white transition-colors"
              >
                team@tikhub.io
              </a>
              <span aria-hidden className="text-white/30">·</span>
              <a href="#" className="hover:text-white transition-colors">
                Discord
              </a>
            </div>
          </div>
        </div>
      </div>
    </footer>
  );
}
