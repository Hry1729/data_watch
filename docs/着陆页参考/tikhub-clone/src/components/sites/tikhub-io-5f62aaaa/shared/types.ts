// 共享类型 — tikhub.io 克隆专用
// 为 /zh 路由服务

import type { ComponentType, SVGProps } from "react";

/** 平台图标组件的统一接口 */
export type IconComponent = ComponentType<SVGProps<SVGSVGElement>>;

/** 平台数据(marquee / 平台选择区 用) */
export type Platform = {
  id: string;
  name: string;
  subtitle: string;
  description: string;
  icon: IconComponent;
};

/** API 预览代码示例(按语言) */
export type CodeLanguage = "python" | "javascript" | "curl" | "java" | "c" | "cpp";

export type CodeSample = {
  language: CodeLanguage;
  filename: string;
  code: string;
};

/** card-swap 堆叠的代码窗口 */
export type CodeWindow = {
  id: string;
  filename: string;
  platform: string;
  samples: Record<CodeLanguage, string>;
};

/** MCP 集成 demo 标签 */
export type MCPTab = {
  id: string;
  label: string;
  count: number;
  serverKey: string;
  url: string;
  configJson: string;
  rightTitle: string;
  rightDescription: string;
  rightStats: { label: string; value: string }[];
};

/** 6 个 use case tab */
export type UseCase = {
  id: string;
  label: string;
  icon: IconComponent;
  title: string;
  description: string;
  example: { label: string; text: string };
  link: { text: string; href: string };
};

/** 数据集定价表行 */
export type DatasetTier = {
  records: string;
  startsAt: string;
  perThousand: string;
};

/** 区块标题 */
export type SectionHeader = {
  eyebrow?: string;
  title: string;
  subtitle?: string;
};
