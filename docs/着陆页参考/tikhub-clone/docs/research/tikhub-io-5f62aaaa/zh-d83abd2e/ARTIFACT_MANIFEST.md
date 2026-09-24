# 资源清单 — https://tikhub.io/zh

**编写日期:** 2026-08-30
**目标:** 列出所有要下载的真实资源

---

## 1. 图片资源(5 个 `<img>`)

### 1.1 Logo

| 文件 | 用途 | 出现位置 | 真实 URL |
|---|---|---|---|
| `logo.jpeg` | TikHub 品牌标识 | Header (顶部) + Footer (页脚) | `https://tikhub.io/logo.jpeg` |

**处理:**
- 下载到 `public/sites/tikhub-io-5f62aaaa/shared/logo.jpeg`
- 共享(Header 和 Footer 都引用)
- 替代品:Next.js `<Image>` 组件 + `width={32} height={32}`

### 1.2 数据集示例图

| 文件 | 用途 | 出现位置 | 真实 URL |
|---|---|---|---|
| `tiktok-video-dataset-export.webp` | 数据集 #1 - YouTube 视频数据集红色 | 数据集区左列最底 | `https://tikhub.io/datasets/tiktok-video-dataset-export.webp` |
| `social-profile-dataset-fields.webp` | 数据集 #2 - Instagram Post 紫橙渐变 | 数据集区左列中间 | `https://tikhub.io/datasets/social-profile-dataset-fields.webp` |
| `social-engagement-dataset.webp` | 数据集 #3 - TikTok Product dataset 黑色 | 数据集区左列最上 | `https://tikhub.io/datasets/social-engagement-dataset.webp` |

**处理:**
- 下载到 `public/sites/tikhub-io-5f62aaaa/zh-d83abd2e/images/`
- 在数据集区块以层叠形式展示(轻微旋转,逐层偏移)
- 替代品:Next.js `<Image>` + `width={...} height={...}`

---

## 2. 内联 SVG 图标(14 个平台)

### 2.1 平台图标来源

- **来源:** DOM 内联 `<svg viewBox="0 0 24 24" class="w-5 h-5" fill="currentColor">` 在每个平台卡片中
- **提取方式:** 浏览器 DevTools snapshot 提取 outerHTML
- **存放:** `src/components/sites/tikhub-io-5f62aaaa/shared/icons.tsx` 作为 React 组件

### 2.2 14 个唯一平台 + SVG 标识符

| # | 平台名 | 标识符 (PascalCase) | 内联 SVG 来源 |
|---|---|---|---|
| 1 | TikTok | `Tiktok` | DOM 提取 |
| 2 | Douyin | `Douyin` | DOM 提取 |
| 3 | Instagram | `Instagram` | DOM 提取 |
| 4 | YouTube | `Youtube` | DOM 提取 |
| 5 | X / Twitter | `XTwitter` | DOM 提取 |
| 6 | Rednote (小红书) | `Rednote` | DOM 提取 |
| 7 | Bilibili | `Bilibili` | DOM 提取 |
| 8 | Weibo | `Weibo` | DOM 提取 |
| 9 | Kuaishou | `Kuaishou` | DOM 提取 |
| 10 | WeChat | `Wechat` | DOM 提取 |
| 11 | Lemon8 | `Lemon8` | DOM 提取 |
| 12 | Zhihu | `Zhihu` | DOM 提取 |
| 13 | Reddit | `Reddit` | DOM 提取 |
| 14 | LinkedIn | `Linkedin` | DOM 提取 |

### 2.3 平台数据结构(给 marquee builder)

```ts
export type Platform = {
  id: string;
  name: string;       // 卡片主标题
  subtitle: string;   // "视频、用户、热门" 之类
  description: string;// 1-2 句描述
  icon: React.ComponentType<{ className?: string }>;
};

export const PLATFORMS: Platform[] = [
  {
    id: 'tiktok',
    name: 'TikTok API',
    subtitle: '视频、用户、热门',
    description: '通过我们开发者友好的 API 轻松访问 TikTok 视频、用户信息和热门内容。',
    icon: Tiktok,
  },
  // ... 13 个其余
];
```

### 2.4 平台数据完整列表(从快照)

| 平台 | 标题 | 副标题 | 描述 |
|---|---|---|---|
| TikTok | TikTok API | 视频、用户、热门 | 通过我们开发者友好的 API 轻松访问 TikTok 视频、用户信息和热门内容。 |
| Douyin | 抖音 API | 视频、直播、商品 | 访问抖音平台的海量短视频、直播数据和电商信息。 |
| Instagram | Instagram API | 帖子、故事、Reels | 提取 Instagram 公开帖子、故事、Reels 视频和用户互动数据。 |
| YouTube | YouTube API | 视频、频道、播放列表 | 获取 YouTube 视频元数据、频道统计、评论和趋势分析。 |
| X / Twitter | X (Twitter) API | 推文、趋势、用户 | 实时访问 X (Twitter) 推文、话题趋势、用户资料和互动数据。 |
| Rednote | 小红书 API | 笔记、用户、商品 | 抓取小红书笔记内容、用户画像、热门话题和电商数据。 |
| Bilibili | 哔哩哔哩 API | 视频、弹幕、UP主 | 访问 B 站视频信息、弹幕数据、UP 主统计和热门排行。 |
| Weibo | 微博 API | 微博、话题、用户 | 抓取微博公开内容、热门话题、用户关系和事件传播路径。 |
| Kuaishou | 快手 API | 视频、直播、用户 | 访问快手短视频、直播数据和电商信息。 |
| WeChat | 微信公众号 API | 文章、阅读、互动 | 抓取微信公众号文章、阅读量、点赞和用户互动数据。 |
| Lemon8 | Lemon8 API | 帖子、用户、趋势 | 访问 Lemon8 平台的生活方式内容和趋势数据。 |
| Zhihu | 知乎 API | 问答、用户、话题 | 抓取知乎问题、答案、用户专业度和话题热度数据。 |
| Reddit | Reddit API | 帖子、评论、子版 | 访问 Reddit 各 subreddit 的帖子、评论、投票和趋势数据。 |
| LinkedIn | LinkedIn API | 个人资料、公司、职位 | 抓取 LinkedIn 公开资料、公司信息、职位发布和行业数据。 |

---

## 3. 字体(网络字体)

### 3.1 Geist Sans + Geist Mono (默认)

- 来自 `next/font/google`
- 已在 `src/app/layout.tsx` 加载
- **不需要额外处理**

### 3.2 Noto Sans SC (中文)

- 来自 `next/font/google`
- **必须添加到 layout.tsx**

```tsx
import { Noto_Sans_SC } from "next/font/google";

const notoSansSC = Noto_Sans_SC({
  weight: ["400", "500", "600", "700"],
  subsets: ["latin"],
  variable: "--font-noto-sans-sc",
  display: "swap",
});

// 在 <html> className 中:
className={`${geistSans.variable} ${geistMono.variable} ${notoSansSC.variable} ...`}
```

---

## 4. YouTube 视频(占位处理)

### 4.1 视频 ID

- `IsstyDED2nc` (从嵌入 URL `youtube-nocookie.com/embed/IsstyDED2nc` 提取)

### 4.2 实现策略(用户已确认)

- **不嵌入 iframe**(避免第三方跟踪)
- 使用占位缩略图 + "在 YouTube 观看" 按钮
- 缩略图 URL: `https://img.youtube.com/vi/IsstyDED2nc/maxresdefault.jpg`
  - 备用: `https://img.youtube.com/vi/IsstyDED2nc/hqdefault.jpg`
- 跳转到: `https://www.youtube.com/watch?v=IsstyDED2nc` `target="_blank"`
- 按钮样式: 黑底白字 + YouTube logo + "在 YouTube 上观看"

### 4.3 占位 UI 设计

```tsx
<a 
  href="https://www.youtube.com/watch?v=IsstyDED2nc"
  target="_blank"
  rel="noopener noreferrer"
  className="relative group block aspect-video rounded-2xl overflow-hidden"
>
  <img 
    src="https://img.youtube.com/vi/IsstyDED2nc/maxresdefault.jpg"
    alt="TikHub MCP 演示视频"
    className="w-full h-full object-cover transition-transform group-hover:scale-105"
  />
  <div className="absolute inset-0 bg-black/30 flex items-center justify-center">
    <div className="w-20 h-20 rounded-full bg-white/90 flex items-center justify-center">
      <PlayIcon className="w-10 h-10 text-black ml-1" />
    </div>
  </div>
  <div className="absolute bottom-4 right-4 px-3 py-1.5 bg-red-600 text-white text-xs font-semibold rounded">
    在 YouTube 上观看 ↗
  </div>
</a>
```

---

## 5. 资源下载脚本

### 5.1 文件路径

`scripts/download-assets-tikhub-io-5f62aaaa-zh-d83abd2e.mjs`

### 5.2 待下载 URL 列表

```js
const ASSETS = [
  {
    name: 'logo.jpeg',
    url: 'https://tikhub.io/logo.jpeg',
    dest: 'public/sites/tikhub-io-5f62aaaa/shared/logo.jpeg',
  },
  {
    name: 'tiktok-video-dataset-export.webp',
    url: 'https://tikhub.io/datasets/tiktok-video-dataset-export.webp',
    dest: 'public/sites/tikhub-io-5f62aaaa/zh-d83abd2e/images/tiktok-video-dataset-export.webp',
  },
  {
    name: 'social-profile-dataset-fields.webp',
    url: 'https://tikhub.io/datasets/social-profile-dataset-fields.webp',
    dest: 'public/sites/tikhub-io-5f62aaaa/zh-d83abd2e/images/social-profile-dataset-fields.webp',
  },
  {
    name: 'social-engagement-dataset.webp',
    url: 'https://tikhub.io/datasets/social-engagement-dataset.webp',
    dest: 'public/sites/tikhub-io-5f62aaaa/zh-d83abd2e/images/social-engagement-dataset.webp',
  },
];
```

### 5.3 脚本骨架

```js
import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(__dirname, '..');

async function download(url, dest) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`Failed ${res.status} for ${url}`);
  const buffer = Buffer.from(await res.arrayBuffer());
  const fullPath = join(ROOT, dest);
  await mkdir(dirname(fullPath), { recursive: true });
  await writeFile(fullPath, buffer);
  console.log(`✓ ${dest}`);
}

for (const asset of ASSETS) {
  try { await download(asset.url, asset.dest); }
  catch (e) { console.error(`✗ ${asset.name}: ${e.message}`); }
}
```

---

## 6. 不需要下载 / 替换

| 项目 | 处理 |
|---|---|
| Lucide 图标 | 用 `lucide-react`,已在 `package.json` |
| shadcn 组件 | 用 `components/ui/*`,按需 `npx shadcn add` |
| 紫色背景渐变 | CSS 渐变,无资源 |
| code 语法高亮颜色 | 9 个颜色 token,无资源 |
| 文字内容 | 直接写在组件里(从快照提取的真实文案) |

---

## 7. 资源总大小估计

| 类型 | 数量 | 平均大小 | 总计 |
|---|---|---|---|
| Logo JPEG | 1 | ~10 KB | 10 KB |
| Dataset WebP | 3 | ~80 KB | 240 KB |
| 平台 SVG (内联) | 14 | ~1 KB | 14 KB (代码) |
| YouTube 缩略图 | 1 | ~30 KB (运行时拉取) | — |
| **总计** | **19** | — | **~265 KB** |

---

## 8. 资源目录结构(下载后)

```
public/
  sites/
    tikhub-io-5f62aaaa/
      shared/
        logo.jpeg
      zh-d83abd2e/
        images/
          tiktok-video-dataset-export.webp
          social-profile-dataset-fields.webp
          social-engagement-dataset.webp

src/
  components/
    sites/
      tikhub-io-5f62aaaa/
        shared/
          icons.tsx          (14 平台 SVG 组件)
          types.ts           (Platform, UseCase 等类型)
        zh-d83abd2e/
          components/
            header.tsx
            hero.tsx
            platform-marquee.tsx
            stats-bar.tsx
            api-preview.tsx
            mcp-integration.tsx
            datasets.tsx
            marketplace.tsx
            use-cases.tsx
            cta-footer.tsx
            footer.tsx
            scroll-to-top.tsx
          page-data.ts       (硬编码内容)
```
