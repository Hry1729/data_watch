"use client";

import { useState } from "react";
import {
  ArrowRightIcon,
  FileTextIcon,
  PlayIcon,
  SearchIcon,
  ShieldIcon,
} from "../../shared/icons";
import type { MCPTab } from "../../shared/types";

const MCP_TABS: MCPTab[] = [
  {
    id: "tiktok",
    label: "TikTok",
    count: 204,
    serverKey: "tikhub-tiktok",
    url: "https://mcp.tikhub.io/tiktok/mcp",
    configJson:
      '{\n  "mcpServers": {\n    "tikhub-tiktok": {\n      "command": "npx",\n      "args": [\n        "mcp-remote",\n        "https://mcp.tikhub.io/tiktok/mcp",\n        "--header",\n        "Authorization: Bearer YOUR_API_KEY"\n      ]\n    }\n  }\n}',
    rightTitle: "Claude Desktop",
    rightDescription:
      "让 Claude 直接访问16个平台的社交数据工具。用自然语言提问,获取结构化数据——个人资料、视频、评论、趋势等。",
    rightStats: [
      { label: "MCP 工具", value: "990+" },
      { label: "平台 MCP", value: "16" },
      { label: "即可开始", value: "3 lines" },
    ],
  },
  {
    id: "instagram",
    label: "Instagram",
    count: 82,
    serverKey: "tikhub-instagram",
    url: "https://mcp.tikhub.io/instagram/mcp",
    configJson:
      '{\n  "mcpServers": {\n    "tikhub-instagram": {\n      "command": "npx",\n      "args": [\n        "mcp-remote",\n        "https://mcp.tikhub.io/instagram/mcp",\n        "--header",\n        "Authorization: Bearer YOUR_API_KEY"\n      ]\n    }\n  }\n}',
    rightTitle: "Claude Desktop",
    rightDescription:
      "把 Instagram 实时数据接入 Claude Desktop。82 个工具覆盖帖子、故事、Reels、用户与话题趋势。",
    rightStats: [
      { label: "MCP 工具", value: "82" },
      { label: "平台 MCP", value: "16" },
      { label: "即可开始", value: "3 lines" },
    ],
  },
  {
    id: "twitter",
    label: "Twitter",
    count: 13,
    serverKey: "tikhub-twitter",
    url: "https://mcp.tikhub.io/twitter/mcp",
    configJson:
      '{\n  "mcpServers": {\n    "tikhub-twitter": {\n      "command": "npx",\n      "args": [\n        "mcp-remote",\n        "https://mcp.tikhub.io/twitter/mcp",\n        "--header",\n        "Authorization: Bearer YOUR_API_KEY"\n      ]\n    }\n  }\n}',
    rightTitle: "Claude Desktop",
    rightDescription:
      "让 Claude 访问 X / Twitter 实时数据。13 个工具覆盖推文、用户、趋势、列表与搜索。",
    rightStats: [
      { label: "MCP 工具", value: "13" },
      { label: "平台 MCP", value: "16" },
      { label: "即可开始", value: "3 lines" },
    ],
  },
];

const NATIVE_CLIENTS = [
  "Cursor",
  "Cline",
  "Cherry Studio",
  "LangChain / LangGraph",
];

const YOUTUBE_ID = "IsstyDED2nc";

const CAPABILITIES = [
  {
    title: "公开视频数据分析",
    desc: "视频元数据、播放量与互动统计——以 JSON 导出。",
  },
  {
    title: "评论互动分析",
    desc: "聚合公开评论与回复,用于情感、关键词与受众研究——以 CSV 或 JSON 导出。",
  },
  {
    title: "公开账号数据分析",
    desc: "账号指标与完整内容历史,用于基准对比与竞品分析——以 CSV 或 JSON 导出。",
  },
  {
    title: "深度趋势报告",
    desc: "一键生成交互式 HTML 报告,内置 AI 视频内容分析——互动率、评论词云与高表现评论,支持 ZIP (含 JSON/CSV 原始数据) 导出。",
  },
];

const TDIYX = [
  { letter: "T", color: "bg-pink-500" },
  { letter: "D", color: "bg-blue-500" },
  { letter: "I", color: "bg-purple-500" },
  { letter: "Y", color: "bg-red-500" },
  { letter: "X", color: "bg-black" },
];

function renderConfigWithLineNumbers(config: string) {
  const lines = config.split("\n");
  return lines.map((line, idx) => (
    <div key={idx} className="flex gap-4">
      <span className="text-muted-foreground/50 w-6 shrink-0 text-right select-none">
        {idx + 1}
      </span>
      <span className="whitespace-pre">{line || " "}</span>
    </div>
  ));
}

function SubA_DemoTabs() {
  const [activeId, setActiveId] = useState(MCP_TABS[0].id);
  const active = MCP_TABS.find((t) => t.id === activeId) ?? MCP_TABS[0];

  return (
    <div>
      <p className="text-sm text-accent">MCP 集成与 CHROME 扩展</p>
      <div className="mt-2 flex flex-wrap items-start justify-between gap-4">
        <h2 className="text-3xl md:text-4xl lg:text-5xl font-bold tracking-tight max-w-3xl">
          MCP 与 Chrome 扩展:将任何 AI 代理连接到社交媒体数据
        </h2>
        <a
          href="#"
          className="inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-4 py-2.5 text-sm font-medium transition-colors hover:bg-muted shrink-0"
        >
          浏览 MCP 文档
        </a>
      </div>

      <div className="mt-10 flex flex-wrap items-center gap-2">
        {MCP_TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            onClick={() => setActiveId(tab.id)}
            className={
              "inline-flex items-center gap-2 rounded-full px-4 py-1.5 text-sm font-medium transition-colors " +
              (activeId === tab.id
                ? "bg-inverse text-surface"
                : "bg-surface text-muted-foreground border border-border hover:bg-muted")
            }
          >
            {tab.label}
            <span
              className={
                "text-xs " +
                (activeId === tab.id
                  ? "text-surface/70"
                  : "text-muted-foreground/70")
              }
            >
              {tab.count}
            </span>
          </button>
        ))}
        <a
          href="#"
          className="inline-flex items-center gap-1 rounded-full border border-dashed border-accent/40 px-4 py-1.5 text-sm font-medium text-accent hover:bg-accent/5"
        >
          全部 16 个平台
          <ArrowRightIcon className="w-3.5 h-3.5" />
        </a>
      </div>

      <div className="mt-6 grid lg:grid-cols-2 gap-12 items-start">
        <div className="rounded-2xl bg-muted-soft border border-border overflow-hidden">
          <div className="px-4 py-2.5 border-b border-border bg-muted/40 flex items-center gap-2">
            <FileTextIcon className="w-4 h-4 text-muted-foreground" />
            <span className="text-xs font-mono text-muted-foreground truncate">
              ~/Library/Application Support/Claude/claude_desktop_config.json
            </span>
            <button
              type="button"
              aria-label="复制"
              className="ml-auto text-muted-foreground hover:text-foreground"
            >
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
                className="w-4 h-4"
              >
                <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
              </svg>
            </button>
          </div>
          <pre className="p-5 font-mono text-xs leading-relaxed overflow-x-auto bg-muted-soft text-foreground/90">
            <code>{renderConfigWithLineNumbers(active.configJson)}</code>
          </pre>
        </div>

        <div>
          <h3 className="text-2xl font-semibold tracking-tight">
            {active.rightTitle}
          </h3>
          <p className="mt-4 text-muted-foreground">{active.rightDescription}</p>
          <div className="mt-8 grid grid-cols-3 gap-6">
            {active.rightStats.map((s) => (
              <div key={s.label}>
                <div className="text-xl font-bold tracking-tight">{s.value}</div>
                <div className="mt-1 text-xs text-muted-foreground">
                  {s.label}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function SubB_McpServers() {
  return (
    <div className="grid lg:grid-cols-2 gap-12 items-center">
      <div>
        <h2 className="text-2xl md:text-3xl font-bold tracking-tight">
          16个专用社交媒体数据 MCP 服务器
        </h2>
        <p className="mt-4 text-muted-foreground">
          每个社交平台都有自己的模型上下文协议服务器和专业 AI 代理工具。只选择您需要的平台——从 TikTok 和抖音到 Reddit、LinkedIn 和微信。
        </p>
        <p className="mt-6 text-sm text-muted-foreground">
          每个 MCP 端点遵循以下模式:
        </p>
        <code className="mt-2 inline-block rounded-md bg-muted-soft border border-border px-3 py-1.5 text-xs font-mono text-accent">
          mcp.tikhub.io/{"{platform}"}/mcp
        </code>
        <p className="mt-8 text-sm font-medium text-muted-foreground">
          原生 MCP 客户端
        </p>
        <div className="mt-3 flex flex-wrap gap-2">
          {NATIVE_CLIENTS.map((c) => (
            <span
              key={c}
              className="rounded-full bg-surface border border-border px-3 py-1 text-xs font-medium"
            >
              {c}
            </span>
          ))}
        </div>
      </div>
      <div>
        <a
          href={"https://www.youtube.com/watch?v=" + YOUTUBE_ID}
          target="_blank"
          rel="noopener noreferrer"
          className="group relative block aspect-video rounded-2xl overflow-hidden bg-inverse border border-border"
        >
          <img
            src={
              "https://img.youtube.com/vi/" + YOUTUBE_ID + "/maxresdefault.jpg"
            }
            alt="TikHub MCP 演示视频"
            className="w-full h-full object-cover transition-transform group-hover:scale-105"
          />
          <div className="absolute inset-0 bg-black/30 flex items-center justify-center">
            <div className="w-20 h-20 rounded-full bg-white/90 flex items-center justify-center transition-transform group-hover:scale-110">
              <PlayIcon className="w-10 h-10 text-black ml-1" />
            </div>
          </div>
          <div className="absolute bottom-4 right-4 px-3 py-1.5 bg-red-600 text-white text-xs font-semibold rounded">
            在 YouTube 上观看 ↗
          </div>
        </a>
        <p className="mt-4 text-sm text-muted-foreground">
          观看 Claude 如何使用 TikHub MCP 获取社交数据并从自然语言生成报告。
        </p>
      </div>
    </div>
  );
}

function SubC_Capabilities() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
      {CAPABILITIES.map((c) => (
        <div
          key={c.title}
          className="rounded-2xl border border-border bg-card p-6"
        >
          <h3 className="font-semibold">{c.title}</h3>
          <p className="mt-2 text-sm text-muted-foreground">{c.desc}</p>
        </div>
      ))}
    </div>
  );
}

function ChromeExtensionMockup() {
  return (
    <div className="relative aspect-[4/3] rounded-2xl border border-border bg-muted-soft overflow-hidden">
      <div className="flex items-center gap-1.5 px-4 py-3 border-b border-border bg-card">
        <span className="h-3 w-3 rounded-full bg-[#f38ba8]" />
        <span className="h-3 w-3 rounded-full bg-[#f9e2af]" />
        <span className="h-3 w-3 rounded-full bg-[#a6e3a1]" />
        <div className="ml-4 flex-1 rounded-md bg-muted px-3 py-1 text-[10px] text-muted-foreground font-mono truncate">
          https://www.tiktok.com/@user/video/123
        </div>
      </div>
      <div className="grid grid-cols-3 gap-3 p-5 h-[calc(100%-3rem)]">
        <div className="col-span-2 rounded-lg bg-card border border-border p-4 space-y-3">
          <div className="flex items-center gap-2">
            <div className="h-8 w-8 rounded-full bg-muted" />
            <div className="space-y-1">
              <div className="h-2 w-20 rounded bg-muted-foreground/30" />
              <div className="h-2 w-12 rounded bg-muted-foreground/20" />
            </div>
          </div>
          <div className="aspect-video rounded bg-muted" />
          <div className="space-y-1.5">
            <div className="h-2 w-full rounded bg-muted-foreground/20" />
            <div className="h-2 w-3/4 rounded bg-muted-foreground/20" />
          </div>
        </div>
        <div className="rounded-lg bg-card border border-accent/30 p-3 space-y-3">
          <div className="flex items-center gap-1.5">
            <div className="h-4 w-4 rounded bg-accent" />
            <div className="h-2 w-16 rounded bg-accent/40" />
          </div>
          {[
            { l: "播放量", v: "1.2M" },
            { l: "点赞", v: "84.3K" },
            { l: "评论", v: "3,210" },
            { l: "分享", v: "12.4K" },
          ].map((row) => (
            <div
              key={row.l}
              className="flex items-center justify-between text-[10px]"
            >
              <span className="text-muted-foreground">{row.l}</span>
              <span className="font-mono font-semibold">{row.v}</span>
            </div>
          ))}
          <div className="border-t border-border pt-2 space-y-1.5">
            <div className="h-2 w-full rounded bg-muted-foreground/20" />
            <div className="h-2 w-2/3 rounded bg-muted-foreground/20" />
          </div>
        </div>
      </div>
    </div>
  );
}

function SubD_ChromeExtension() {
  const platforms = ["TikTok", "Douyin", "Instagram", "X", "Bilibili"];
  return (
    <div className="grid lg:grid-cols-2 gap-12 items-center">
      <ChromeExtensionMockup />
      <div>
        <p className="text-sm text-accent">浏览器扩展</p>
        <h2 className="mt-2 text-3xl font-bold tracking-tight">
          Chrome 扩展:在浏览器侧边栏中进行社媒数据分析
        </h2>
        <p className="mt-4 text-muted-foreground">
          自带密钥 (BYOK) 的 TikHub 客户端。无需离开页面,即可采集评论、分析主页并为 TikTok、抖音、Instagram、X 与哔哩哔哩生成分析报告。
        </p>
        <div className="mt-6 flex flex-wrap gap-2">
          {platforms.map((p) => (
            <span
              key={p}
              className="inline-flex items-center gap-1.5 rounded-full bg-muted px-3 py-1 text-xs font-medium"
            >
              <SearchIcon className="w-3 h-3" />
              {p}
            </span>
          ))}
        </div>
        <div className="mt-4 flex flex-wrap gap-2">
          <span className="inline-flex items-center gap-1.5 rounded-full border border-accent/30 bg-accent/10 px-3 py-1 text-xs text-accent">
            <ShieldIcon className="w-3 h-3" />
            需要一个免费的 TikHub API 密钥
          </span>
          <span className="inline-flex items-center gap-1.5 rounded-full border border-accent/30 bg-accent/10 px-3 py-1 text-xs text-accent">
            <ShieldIcon className="w-3 h-3" />
            无追踪
          </span>
        </div>
        <div className="mt-6 flex flex-wrap gap-3">
          <a
            href="#"
            className="inline-flex items-center gap-2 rounded-lg bg-inverse text-surface px-4 py-2.5 text-sm font-medium"
          >
            添加到 Chrome
          </a>
          <a
            href="#"
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-4 py-2.5 text-sm font-medium transition-colors hover:bg-muted"
          >
            了解更多
            <ArrowRightIcon className="w-4 h-4" />
          </a>
        </div>
      </div>
    </div>
  );
}

function SubE_TdiyxBadge() {
  return (
    <div className="flex flex-wrap items-center gap-4">
      {TDIYX.map((b) => (
        <div
          key={b.letter}
          className={
            "h-12 w-12 rounded-full " +
            b.color +
            " text-white flex items-center justify-center text-xl font-bold"
          }
        >
          {b.letter}
        </div>
      ))}
      <p className="text-sm font-medium ml-2">
        990+ 工具覆盖16个平台——一个 API 密钥即可使用
      </p>
      <a
        href="#"
        className="ml-auto inline-flex items-center gap-1 text-sm font-medium text-accent hover:underline"
      >
        查看完整设置指南
        <ArrowRightIcon className="w-4 h-4" />
      </a>
    </div>
  );
}

export function McpIntegration() {
  return (
    <section className="scroll-mt-20">
      <div className="max-w-7xl mx-auto px-6 py-20 md:py-28 space-y-20">
        <SubA_DemoTabs />
        <div className="border-t border-border" />
        <SubB_McpServers />
        <div className="border-t border-border" />
        <SubC_Capabilities />
        <div className="border-t border-border" />
        <SubD_ChromeExtension />
        <SubE_TdiyxBadge />
      </div>
    </section>
  );
}
