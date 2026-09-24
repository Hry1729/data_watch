# 页面拓扑 — https://tikhub.io/zh

**编写日期:** 2026-08-30
**视口:** 1440 × 完整高度
**总高度:** 7774px

---

## 全局 section 列表(绝对 Y 坐标)

| # | Y 范围 | 高度 | section 类型 | 关键 class | 复杂度 |
|---|---|---|---|---|---|
| 0 | 0 - 894 | 894 | `<header>` 导航 + Hero | `relative z-10` | 中 |
| 1 | 894 - 1140 | 246 | 平台统计条 | `relative z-0` | 低 |
| 2 | 1140 - 2172 | 1032 | API 预览 | `relative z-0 scroll-mt-20` | **高** |
| 3 | 2172 - 4172 | 2000 | MCP 集成 | `scroll-mt-20` | **高** |
| 4 | 4173 - 5144 | 971 | 数据集 | `scroll-mt-20` | 中 |
| 5 | 5144 - 5972 | 828 | 市场与合作伙伴 | `scroll-mt-20` | 中 |
| 6 | 5971 - 6800 | 829 | 你能构建什么 | `scroll-mt-20` | **高** |
| 7 | 6800 - 7162 | 362 | CTA 收尾 | `<section>` 无 class | 低 |
| 8 | 7162 - 7774 | 612 | Footer | `<footer class="bg-inverse text-white">` | 中 |

---

## 详细区块结构

### 0. Header + Hero (Y 0-894)

#### 0.1 Header 子结构
- Logo (`<a href="/">` 跳首页)
- 主 nav (产品 / 解决方案 / 开发者 / 定价 / 企业版)
- 右侧工具: 搜索 / 暗黑模式切换 / 语言选择 / 登录 / 预约通话

#### 0.2 Hero 子结构
- 顶部: 旋转徽章 (`rounded-full border`)
- 标题 h1: "社交数据基础设施 为 AI 时代而生" (含渐变字 + 角标)
- 副标题 p: "为 AI 应用、代理和研究提供生产级社交媒体数据"
- 4 个 CTA 按钮
- 4 个统计徽章
- 背景: 淡紫渐变

---

### 1. 平台统计条 (Y 894-1140, 246px)

```
<div class="border-y">
  <div class="max-w-7xl mx-auto px-6 py-12 grid lg:grid-cols-5 gap-6 items-center">
    <div class="lg:col-span-1 text-sm text-muted-foreground">标题</div>
    <div class="lg:col-span-3 grid grid-cols-4 gap-6">
      4 个统计(竖直分隔线)
    </div>
    <div class="lg:col-span-1 flex gap-2">GDPR/CCPA 徽章</div>
  </div>
</div>
```

---

### 2. API 预览 (Y 1140-2172, 1032px)

#### 2.1 顶部 50% (Y 1140-1656, 516px)
- 左列 50%: 标题 + 描述 + 6 语言 tabs + 3 bullet + 3 链接
- 右列 50%: card-swap 3 卡片堆叠

#### 2.2 card-swap 详情
- 卡片 1 (top=1367, h=466): tiktok_example
- 卡片 2 (top=1396, h=502): instagram_example  
- 卡片 3 (top=1431, h=543): x_twitter_example
- 全部 `!bg-[#1E1E2E]`
- 每张有 macOS 圆点 + 文件名标签

#### 2.3 底部 50% (Y 1656-2172, 516px)
- 全宽,文本居中
- "开发者喜爱的 API" 标题
- 3 列小卡(快速接入 / 全面文档 / 高可用架构)

---

### 3. MCP 集成 (Y 2172-4172, 2000px)

#### 3.1 子区块 A: 4-tab 演示 (Y 2172-2640, 468px)
- 左列: 配置代码块 (`claude_desktop_config.json`)
- 右列: 平台名 + 描述 + 3 统计

#### 3.2 子区块 B: 16 个 MCP 服务器 (Y 2640-3120, 480px)
- 左列: 文字 + 端点模式 + 原生客户端徽章
- 右列: YouTube 视频缩略图

#### 3.3 子区块 C: 4 能力卡片 (Y 3120-3536, 416px)
- 2x2 网格
- 公开视频数据分析 / 评论互动分析 / 公开账号数据分析 / 深度趋势报告

#### 3.4 子区块 D: Chrome 扩展 (Y 3536-3904, 368px)
- 左列: 文字
- 右列: 5 平台徽章 + 2 特性 + 2 按钮

#### 3.5 子区块 E: TDIYX 徽章 (Y 3904-4172, 268px)
- 5 圆形字母徽章横排
- "990+ 工具覆盖16个平台"
- "查看完整设置指南 →" 链接

---

### 4. 数据集 (Y 4173-5144, 971px)

```
<section class="scroll-mt-20">
  <div class="max-w-7xl mx-auto px-6">
    <div class="text-center mb-12">
      <h2>10亿+ 记录</h2>
      <p>从 14 个平台收集 2,000 万+ 个数据集</p>
    </div>
    <div class="grid lg:grid-cols-2 gap-12">
      <div class="relative h-96"> 3 张堆叠数据集图 </div>
      <div>
        <h3>浏览我们的数据集</h3>
        <p>描述</p>
        <table>3 列 2 行定价</table>
        <div>2 个链接</div>
      </div>
    </div>
  </div>
</section>
```

#### 4.1 数据集图片
1. `tiktok-video-dataset-export.webp` (YouTube 视频数据集红色)
2. `social-profile-dataset-fields.webp` (Instagram Post 紫橙)
3. `social-engagement-dataset.webp` (TikTok Product 黑色)

#### 4.2 定价表
| 记录数 | 起价 | 每千条 |
|---|---|---|
| 100K-500K | $150 | $2.00 |
| 20M+ | $8,000 | $0.40 |

---

### 5. 市场与合作伙伴 (Y 5144-5972, 828px)

```
<section class="scroll-mt-20">
  <div class="max-w-7xl mx-auto px-6">
    <div class="bg-inverse text-white rounded-3xl p-12">
      <div class="grid lg:grid-cols-2 gap-12">
        <div>
          <span class="text-accent">市场与合作伙伴</span>
          <span class="bg-accent/20 text-accent">2026 年 8 月上线</span>
          <h2>...大标题...</h2>
          <p>描述</p>
          <div class="grid grid-cols-3 gap-4">3 列小卡</div>
          <div class="flex gap-3">2 按钮</div>
          <p>底部说明("在线前" 错别字)</p>
        </div>
        <div>
          <h3>仅收取 10% 佣金</h3>
          <p>对比约 20% 的行业标准佣金。</p>
          <div>2 行对比</div>
        </div>
      </div>
    </div>
  </div>
</section>
```

#### 5.1 3 列小卡
- AI 智能体
- API
- 网络数据采集方案

#### 5.2 佣金对比
- TikHub 10% (紫进度条,50% 宽)
- 行业标准 约 20% (灰进度条,100% 宽)

---

### 6. 你能构建什么 (Y 5971-6800, 829px)

```
<section class="scroll-mt-20">
  <div class="max-w-7xl mx-auto px-6">
    <div class="text-center mb-12">
      <h2>你能构建什么</h2>
      <p>用 16 个平台,只需 1 个 API 密钥</p>
    </div>
    <div class="grid lg:grid-cols-[280px_1fr] gap-8">
      <div class="flex flex-col gap-2"> 6 tab 按钮 </div>
      <div>
        <div class="grid md:grid-cols-[auto_1fr] gap-4 mb-4">
          <div class="w-12 h-12 bg-accent/10 rounded-lg flex items-center justify-center">
            <TabIcon />
          </div>
          <h3>{tab.title}</h3>
        </div>
        <p>{tab.description}</p>
        <div class="bg-muted rounded-lg p-4 mt-4">
          <p class="text-xs text-muted-foreground">示例</p>
          <p>{tab.example}</p>
        </div>
        <a>探索此使用场景 →</a>
      </div>
    </div>
  </div>
</section>
```

#### 6.1 6 个 tab(全部都要独立内容)
1. AI 智能体
2. RAG 与检索
3. 训练数据
4. SaaS 产品
5. 内容智能
6. 自动化与工作流

---

### 7. CTA 收尾 (Y 6800-7162, 362px)

```
<section>
  <div class="max-w-3xl mx-auto px-6 text-center">
    <h2>社媒数据不会自己变成结构化。</h2>
    <p>注册即送 $0.05 免费额度——足以测试任意端点。无需信用卡。</p>
    <div class="flex gap-3 justify-center mt-8">
      <a class="bg-inverse text-white">免费开始</a>
      <a class="bg-surface border">预约通话</a>
    </div>
  </div>
</section>
```

---

### 8. Footer (Y 7162-7774, 612px)

```
<footer class="bg-inverse text-white">
  <div class="max-w-7xl mx-auto px-6 py-16">
    <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-8 mb-12">
      <div>Logo + 描述 + 社交图标</div>
      <div>产品 5 链接</div>
      <div>平台 API 5 链接</div>
      <div>开发者 10 链接</div>
      <div>公司 7 链接</div>
      <div>法律 2 链接</div>
    </div>
    <div class="border-t border-white/10 pt-8">
      <p>免责声明</p>
      <div class="flex justify-between">
        <p>© 2026 TikHub. 保留所有权利。</p>
        <p>team@tikhub.io · Discord</p>
      </div>
    </div>
  </div>
</footer>
```

#### 8.1 浮动 Scroll-to-top 按钮
- 固定右下角
- 仅滚动后出现

---

## 复杂区块子组件清单(builder 必须实现)

### API 预览 (3 子组件)
1. `APIPreviewIntro` — 左列文字+tabs
2. `CodeCardSwap` — 右列 3 卡片堆叠
3. `DeveloperFeatures` — 底部 3 列小卡

### MCP 集成 (5 子组件)
1. `MCPTabDemo` — 4 tab + 配置代码
2. `MCPVideoSection` — 16 MCP + YouTube 占位
3. `MCPCapabilitiesGrid` — 4 能力卡片
4. `MCPChromeExtension` — Chrome 扩展
5. `MCPTdiyxBadge` — 5 字母徽章

### 你能构建什么 (1 组件 + 6 数据对象)
1. `UseCases` — 主组件
2. `useCases: UseCase[]` — 6 个数据对象(tab+title+desc+example+icon)

---

## Section 复杂度评级

| 等级 | 区块 | 估计代码量 |
|---|---|---|
| 低 | Header / Hero / 统计 / CTA | < 200 行 |
| 中 | Footer / 数据集 / 市场 / 平台 marquee | 200-400 行 |
| 高 | API 预览 / MCP 集成 / Use Cases | 400-800 行 |

**总估计:** 3500-5000 行组件代码 + 200-400 行样式/token
