"use client";

import { useEffect, useState } from "react";
import { ApiCodeCard } from "./ApiCodeCard";
import { ArrowRightIcon, CheckIcon } from "../../shared/icons";
import type { CodeLanguage, CodeWindow } from "../../shared/types";

const LANGUAGES: { id: CodeLanguage; label: string }[] = [
  { id: "python", label: "Python" },
  { id: "javascript", label: "JavaScript" },
  { id: "curl", label: "cURL" },
  { id: "java", label: "Java" },
  { id: "c", label: "C" },
  { id: "cpp", label: "C++" },
];

const BULLETS = [
  "统一的响应结构,跨所有平台保持一致",
  "自动分页、速率限制、重试处理",
  "类型化 SDK,带本地缓存与故障转移",
];

const LINKS = [
  { text: "查看 API 文档", href: "#" },
  { text: "下载 SDK", href: "#" },
  { text: "请求沙盒密钥", href: "#" },
];

const FEATURES = [
  {
    title: "快速接入",
    desc: "一行代码获取数据,5 分钟集成到你的应用。",
  },
  {
    title: "全面文档",
    desc: "覆盖所有 1,000+ 接口,带 curl/Python/JS 示例。",
  },
  {
    title: "高可用架构",
    desc: "99.95% SLA,多区域部署,自动故障转移。",
  },
];

const CODE_WINDOWS: CodeWindow[] = [
  {
    id: "tiktok",
    filename: "tiktok_example.py",
    platform: "TikTok",
    samples: {
      python: `import tikhub

api = tikhub.TikHub(api_key="<YOUR_KEY>")
video = api.tiktok.video.get("https://www.tiktok.com/@user/video/123")
print(video.stats)`,
      javascript: `import { TikHub } from "tikhub";

const api = new TikHub({ apiKey: "<YOUR_KEY>" });
const video = await api.tiktok.video.get("https://www.tiktok.com/@user/video/123");
console.log(video.stats);`,
      curl: `curl -X GET "https://api.tikhub.io/tiktok/video?url=https://www.tiktok.com/@user/video/123" \\
  -H "Authorization: Bearer <YOUR_KEY>"`,
      java: `TikHub api = TikHub.builder().apiKey("<YOUR_KEY>").build();
Video v = api.tiktok().video().get("https://www.tiktok.com/@user/video/123");
System.out.println(v.stats());`,
      c: `// C SDK 示例 - 简化
tikhub_client_t* c = tikhub_new("<YOUR_KEY>");
tikhub_tiktok_video_t* v = tikhub_tiktok_video_get(c, "https://www.tiktok.com/@user/video/123");`,
      cpp: `auto client = tikhub::Client("<YOUR_KEY>");
auto video = client.tiktok().video().get("https://www.tiktok.com/@user/video/123");
std::cout << video.stats << std::endl;`,
    },
  },
  {
    id: "instagram",
    filename: "instagram_example.py",
    platform: "Instagram",
    samples: {
      python: `import tikhub

api = tikhub.TikHub(api_key="<YOUR_KEY>")
post = api.instagram.post.get("https://www.instagram.com/p/CODE/")
print(post.likes, post.comments)`,
      javascript: `import { TikHub } from "tikhub";
const api = new TikHub({ apiKey: "<YOUR_KEY>" });
const post = await api.instagram.post.get("https://www.instagram.com/p/CODE/");
console.log(post.likes, post.comments);`,
      curl: `curl "https://api.tikhub.io/instagram/post?url=https://www.instagram.com/p/CODE/" \\
  -H "Authorization: Bearer <YOUR_KEY>"`,
      java: `TikHub api = TikHub.builder().apiKey("<YOUR_KEY>").build();
Post p = api.instagram().post().get("https://www.instagram.com/p/CODE/");
System.out.println(p.likes() + " " + p.comments());`,
      c: `// C SDK Instagram 调用占位`,
      cpp: `auto client = tikhub::Client("<YOUR_KEY>");
auto post = client.instagram().post().get("https://www.instagram.com/p/CODE/");
std::cout << post.likes << " " << post.comments << std::endl;`,
    },
  },
  {
    id: "twitter",
    filename: "x_twitter_example.py",
    platform: "X / Twitter",
    samples: {
      python: `import tikhub

api = tikhub.TikHub(api_key="<YOUR_KEY>")
tweet = api.twitter.tweet.get("https://x.com/user/status/123")
print(tweet.metrics)`,
      javascript: `import { TikHub } from "tikhub";
const api = new TikHub({ apiKey: "<YOUR_KEY>" });
const tweet = await api.twitter.tweet.get("https://x.com/user/status/123");
console.log(tweet.metrics);`,
      curl: `curl "https://api.tikhub.io/twitter/tweet?url=https://x.com/user/status/123" \\
  -H "Authorization: Bearer <YOUR_KEY>"`,
      java: `TikHub api = TikHub.builder().apiKey("<YOUR_KEY>").build();
Tweet t = api.twitter().tweet().get("https://x.com/user/status/123");
System.out.println(t.metrics());`,
      c: `// C SDK Twitter 调用占位`,
      cpp: `auto client = tikhub::Client("<YOUR_KEY>");
auto tweet = client.twitter().tweet().get("https://x.com/user/status/123");
std::cout << tweet.metrics << std::endl;`,
    },
  },
];

function LeftIntro({
  active,
  onChange,
}: {
  active: CodeLanguage;
  onChange: (l: CodeLanguage) => void;
}) {
  return (
    <div>
      <p className="text-sm text-muted-foreground">实时 API 预览</p>
      <h2 className="mt-2 text-3xl md:text-4xl lg:text-5xl font-bold tracking-tight">
        查看 API 运行效果
      </h2>
      <p className="mt-4 text-muted-foreground">
        访问涵盖娱乐、科技、新闻、政治、教育、时尚、美食等领域的视频、音频和文本内容——结构化数据,可直接用于 AI 训练、市场研究、内容分析和竞争情报。
      </p>
      <p className="mt-8 text-sm font-medium text-muted-foreground">
        选择语言
      </p>
      <div className="mt-3 flex flex-wrap gap-2">
        {LANGUAGES.map((l) => (
          <button
            key={l.id}
            type="button"
            onClick={() => onChange(l.id)}
            className={
              "rounded-md px-3 py-1.5 text-xs font-medium transition-colors " +
              (active === l.id
                ? "bg-inverse text-surface"
                : "bg-surface text-muted-foreground border border-border hover:bg-muted")
            }
          >
            {l.label}
          </button>
        ))}
      </div>
      <ul className="mt-8 space-y-3 text-sm">
        {BULLETS.map((b) => (
          <li key={b} className="flex items-start gap-2">
            <CheckIcon className="mt-0.5 h-4 w-4 shrink-0 text-accent" />
            <span>{b}</span>
          </li>
        ))}
      </ul>
      <div className="mt-8 flex flex-col gap-2">
        {LINKS.map((l) => (
          <a
            key={l.text}
            href={l.href}
            className="inline-flex items-center gap-1 text-sm font-medium text-accent hover:underline w-fit"
          >
            {l.text}
            <ArrowRightIcon className="w-4 h-4" />
          </a>
        ))}
      </div>
    </div>
  );
}

function CodeCardSwap({ active }: { active: CodeLanguage }) {
  const [topIdx, setTopIdx] = useState(0);
  useEffect(() => {
    const t = setInterval(
      () => setTopIdx((i) => (i + 1) % CODE_WINDOWS.length),
      4500,
    );
    return () => clearInterval(t);
  }, []);
  return (
    <div className="relative h-[480px] md:h-[520px]">
      {CODE_WINDOWS.map((w, i) => {
        const offset =
          (i - topIdx + CODE_WINDOWS.length) % CODE_WINDOWS.length;
        return (
          <ApiCodeCard
            key={w.id}
            window={w}
            language={active}
            offset={offset}
            isTop={offset === 0}
          />
        );
      })}
      <div className="absolute bottom-4 left-1/2 -translate-x-1/2 flex gap-1.5 z-30">
        {CODE_WINDOWS.map((_, i) => (
          <button
            key={i}
            type="button"
            onClick={() => setTopIdx(i)}
            aria-label={`切换到第 ${i + 1} 个示例`}
            className={
              "h-1.5 rounded-full transition-all " +
              (i === topIdx
                ? "w-6 bg-foreground"
                : "w-1.5 bg-foreground/30 hover:bg-foreground/50")
            }
          />
        ))}
      </div>
    </div>
  );
}

function DeveloperFeatures() {
  return (
    <div className="mt-16 grid grid-cols-1 md:grid-cols-3 gap-4">
      {FEATURES.map((f) => (
        <div
          key={f.title}
          className="rounded-2xl border border-border bg-card p-6"
        >
          <h3 className="font-semibold">{f.title}</h3>
          <p className="mt-2 text-sm text-muted-foreground">{f.desc}</p>
        </div>
      ))}
    </div>
  );
}

export function ApiPreview() {
  const [active, setActive] = useState<CodeLanguage>("python");
  return (
    <section className="scroll-mt-20">
      <div className="max-w-7xl mx-auto px-6 py-20 md:py-28">
        <div className="grid lg:grid-cols-2 gap-12 items-start">
          <LeftIntro active={active} onChange={setActive} />
          <CodeCardSwap active={active} />
        </div>
        <DeveloperFeatures />
      </div>
    </section>
  );
}
