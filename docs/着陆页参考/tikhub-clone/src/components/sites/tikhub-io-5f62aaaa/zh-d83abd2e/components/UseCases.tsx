"use client";

import { useState } from "react";
import {
  ArrowRightIcon,
  BotIcon,
  DatabaseIcon,
  FileTextIcon,
  LayersIcon,
  LightbulbIcon,
  WorkflowIcon,
} from "../../shared/icons";
import type { UseCase } from "../../shared/types";

const USE_CASES: UseCase[] = [
  {
    id: "agent",
    label: "AI 智能体",
    icon: BotIcon,
    title: "AI 智能体",
    description: "构建能读取实时社交数据并据此行动的自主智能体。MCP 协议让 Claude、Cursor、Cherry Studio 等 5 分钟内接入。",
    example: { label: "示例", text: "Claude + TikHub MCP → 智能体根据实时趋势起草内容策略" },
    link: { text: "探索此使用场景", href: "#" },
  },
  {
    id: "rag",
    label: "RAG 与检索",
    icon: DatabaseIcon,
    title: "RAG 与检索",
    description: "用新鲜、结构化的社交内容驱动 RAG,让你的 LLM 在回答中引用实时来源,而不是 2021 年的训练数据。",
    example: { label: "示例", text: "检索今日热门 → 嵌入 + 重排 → 在回答中以引用形式给出实时来源链接。" },
    link: { text: "探索此使用场景", href: "#" },
  },
  {
    id: "training",
    label: "训练数据",
    icon: FileTextIcon,
    title: "训练数据",
    description: "10亿+ 记录,涵盖 16 个平台,字段文档化,使用条款清晰。直接下载 CSV/Parquet 用于微调。",
    example: { label: "示例", text: "下载 1M 条 Reddit 评论 + 元数据 → 清洗 → 微调 LLaMA 风格分类器,识别金融情绪。" },
    link: { text: "探索此使用场景", href: "#" },
  },
  {
    id: "saas",
    label: "SaaS 产品",
    icon: LayersIcon,
    title: "SaaS 产品",
    description: "营销、舆情、内容运营、社群管理 — 4 行代码集成社交数据,让你的产品立刻具备市场差异化。",
    example: { label: "示例", text: "营销 SaaS → 客户输入品牌名 → 自动拉取跨平台提及、情感、热门话题、竞品对比。" },
    link: { text: "探索此使用场景", href: "#" },
  },
  {
    id: "content",
    label: "内容智能",
    icon: LightbulbIcon,
    title: "内容智能",
    description: "追踪你所在品类的爆款,自动生成选题建议,跨平台同步分发,把内容生产从一周缩短到一天。",
    example: { label: "示例", text: "监控 3 个对标账号 → 识别增长最快的 10 个视频 → 提取共同特征 → 给出 5 个选题方向。" },
    link: { text: "探索此使用场景", href: "#" },
  },
  {
    id: "automation",
    label: "自动化",
    icon: WorkflowIcon,
    title: "自动化",
    description: "用工作流串联社交数据与下游动作。当某个事件触发(帖子爆款、负面评论、用户增长),自动执行下一步。",
    example: { label: "示例", text: "YouTube 视频突破 100K 播放 → 触发 → 抓取评论 + 情感分析 → 写入 Notion + 推送 Slack。" },
    link: { text: "探索此使用场景", href: "#" },
  },
];

export function UseCases() {
  const [active, setActive] = useState(USE_CASES[0].id);
  const tab = USE_CASES.find((u) => u.id === active) ?? USE_CASES[0];
  const Icon = tab.icon;
  return (
    <section className="scroll-mt-20">
      <div className="max-w-7xl mx-auto px-6 py-20 md:py-28">
        <div className="text-center mb-12">
          <p className="text-sm text-muted-foreground">用 16 个平台,只需 1 个 API 密钥</p>
          <h2 className="mt-2 text-3xl md:text-4xl lg:text-5xl font-bold tracking-tight">你能构建什么</h2>
        </div>
        <div className="grid lg:grid-cols-[280px_1fr] gap-8">
          <div className="flex lg:flex-col gap-2 overflow-x-auto lg:overflow-visible pb-2 lg:pb-0">
            {USE_CASES.map((u) => {
              const TabIcon = u.icon;
              const isActive = u.id === active;
              return (
                <button key={u.id} type="button" onClick={() => setActive(u.id)} className={
                  "flex items-center gap-3 rounded-xl px-4 py-3 text-left transition-colors whitespace-nowrap shrink-0 " +
                  (isActive ? "bg-card border border-border shadow-sm" : "hover:bg-muted/50 text-muted-foreground")
                }>
                  <span className={"flex h-8 w-8 items-center justify-center rounded-lg shrink-0 " + (isActive ? "bg-accent/15 text-accent" : "text-muted-foreground")}>
                    <TabIcon className="h-4 w-4" />
                  </span>
                  <span className="text-sm font-medium">{u.label}</span>
                </button>
              );
            })}
          </div>
          <div>
            <div className="flex items-center gap-3 mb-4">
              <div className="h-12 w-12 rounded-xl bg-accent/15 text-accent flex items-center justify-center">
                <Icon className="h-6 w-6" />
              </div>
              <h3 className="text-2xl md:text-3xl font-semibold tracking-tight">{tab.title}</h3>
            </div>
            <p className="text-muted-foreground">{tab.description}</p>
            <div className="mt-6 rounded-xl bg-muted-soft border border-border p-5">
              <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">{tab.example.label}</p>
              <p className="mt-2 text-sm">{tab.example.text}</p>
            </div>
            <a href={tab.link.href} className="mt-6 inline-flex items-center gap-1 text-sm font-medium text-accent hover:underline">
              {tab.link.text} <ArrowRightIcon className="w-4 h-4" />
            </a>
          </div>
        </div>
      </div>
    </section>
  );
}
