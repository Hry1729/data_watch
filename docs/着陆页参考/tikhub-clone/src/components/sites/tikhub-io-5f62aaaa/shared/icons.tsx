// 14 个平台 SVG 图标(从原站 DOM 提取,viewBox 0 0 24 24, fill="currentColor")
// 加上小图标(月亮/太阳/X/LinkedIn/Play/箭头/盾牌等)

import type { SVGProps } from "react";

type IconProps = SVGProps<SVGSVGElement>;

/* ============================================================
 * 1. TikTok
 * ============================================================ */
export function TiktokIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M19.59 6.69a4.83 4.83 0 0 1-3.77-4.25V2h-3.45v13.67a2.89 2.89 0 0 1-5.2 1.74 2.89 2.89 0 0 1 2.31-4.64 2.93 2.93 0 0 1 .88.13V9.4a6.84 6.84 0 0 0-1-.05A6.33 6.33 0 0 0 5.8 20.1a6.34 6.34 0 0 0 10.86-4.43V8.93a8.16 8.16 0 0 0 4.77 1.52V7a4.85 4.85 0 0 1-1.84-.31z" />
    </svg>
  );
}

/* ============================================================
 * 2. Douyin(抖音) - 与 TikTok 类似,简化的音符
 * ============================================================ */
export function DouyinIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M16.6 5.82s.51.5 0 0A4.28 4.28 0 0 1 15.54 3h-3.09v12.4a2.59 2.59 0 0 1-2.59 2.5c-1.42 0-2.59-1.16-2.59-2.5a2.59 2.59 0 0 1 2.59-2.5c.27 0 .53.04.78.13V9.91a5.74 5.74 0 0 0-.78-.05 5.6 5.6 0 1 0 5.6 5.6V9.01a7.32 7.32 0 0 0 4.36 1.43V7.36a4.36 4.36 0 0 1-3.22-1.54z" />
    </svg>
  );
}

/* ============================================================
 * 3. Instagram
 * ============================================================ */
export function InstagramIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M12 2.16c3.2 0 3.58.01 4.85.07 1.17.05 1.8.25 2.23.41.56.22.96.48 1.38.9.42.42.68.82.9 1.38.16.42.36 1.06.41 2.23.06 1.27.07 1.65.07 4.85s-.01 3.58-.07 4.85c-.05 1.17-.25 1.8-.41 2.23a3.71 3.71 0 0 1-.9 1.38c-.42.42-.82.68-1.38.9-.42.16-1.06.36-2.23.41-1.27.06-1.65.07-4.85.07s-3.58-.01-4.85-.07c-1.17-.05-1.8-.25-2.23-.41a3.71 3.71 0 0 1-1.38-.9 3.71 3.71 0 0 1-.9-1.38c-.16-.42-.36-1.06-.41-2.23-.06-1.27-.07-1.65-.07-4.85s.01-3.58.07-4.85c.05-1.17.25-1.8.41-2.23.22-.56.48-.96.9-1.38.42-.42.82-.68 1.38-.9.42-.16 1.06-.36 2.23-.41 1.27-.06 1.65-.07 4.85-.07M12 0C8.74 0 8.33.01 7.05.07c-1.28.06-2.15.26-2.91.55a5.88 5.88 0 0 0-2.13 1.39A5.88 5.88 0 0 0 .62 4.14C.33 4.9.13 5.77.07 7.05.01 8.33 0 8.74 0 12s.01 3.67.07 4.95c.06 1.28.26 2.15.55 2.91a5.88 5.88 0 0 0 1.39 2.13 5.88 5.88 0 0 0 2.13 1.39c.76.29 1.63.49 2.91.55C8.33 23.99 8.74 24 12 24s3.67-.01 4.95-.07c1.28-.06 2.15-.26 2.91-.55a6.15 6.15 0 0 0 3.52-3.52c.29-.76.49-1.63.55-2.91.06-1.28.07-1.69.07-4.95s-.01-3.67-.07-4.95c-.06-1.28-.26-2.15-.55-2.91a5.88 5.88 0 0 0-1.39-2.13A5.88 5.88 0 0 0 19.86.62C19.1.33 18.23.13 16.95.07 15.67.01 15.26 0 12 0zm0 5.84a6.16 6.16 0 1 0 0 12.32 6.16 6.16 0 0 0 0-12.32zM12 16a4 4 0 1 1 0-8 4 4 0 0 1 0 8zm6.41-11.85a1.44 1.44 0 1 0 0 2.88 1.44 1.44 0 0 0 0-2.88z" />
    </svg>
  );
}

/* ============================================================
 * 4. YouTube
 * ============================================================ */
export function YoutubeIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M23.5 6.2a3 3 0 0 0-2.1-2.1C19.5 3.6 12 3.6 12 3.6s-7.5 0-9.4.5A3 3 0 0 0 .5 6.2C0 8.1 0 12 0 12s0 3.9.5 5.8a3 3 0 0 0 2.1 2.1c1.9.5 9.4.5 9.4.5s7.5 0 9.4-.5a3 3 0 0 0 2.1-2.1c.5-1.9.5-5.8.5-5.8s0-3.9-.5-5.8zM9.6 15.6V8.4l6.2 3.6-6.2 3.6z" />
    </svg>
  );
}

/* ============================================================
 * 5. X / Twitter
 * ============================================================ */
export function XTwitterIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z" />
    </svg>
  );
}

/* ============================================================
 * 6. Rednote / 小红书
 * ============================================================ */
export function RednoteIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M3 6.5C3 4.57 4.57 3 6.5 3h11A3.5 3.5 0 0 1 21 6.5v11a3.5 3.5 0 0 1-3.5 3.5h-11A3.5 3.5 0 0 1 3 17.5v-11zm5 2v3.5h2V8.5H10zm4 0v3.5h2V8.5h-2zm-2 5v3.5h2v-3.5h-2z" />
    </svg>
  );
}

/* ============================================================
 * 7. Bilibili
 * ============================================================ */
export function BilibiliIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M17.71 4.46 16 6.16h-2.4l1.7-1.7-.7-.7-1.7 1.7h-2.6L8.6 3.76l-.7.7 1.7 1.7H7.2L5.5 4.46l-.7.7 1.6 1.6A3.92 3.92 0 0 0 3 10.5v6A3.5 3.5 0 0 0 6.5 20h11a3.5 3.5 0 0 0 3.5-3.5v-6a3.92 3.92 0 0 0-3.4-3.74l1.6-1.6zM19 16.5a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 5 16.5v-6A1.5 1.5 0 0 1 6.5 9h11A1.5 1.5 0 0 1 19 10.5zm-3-4.5h-2v2h2zm-6 0H8v2h2z" />
    </svg>
  );
}

/* ============================================================
 * 8. Weibo
 * ============================================================ */
export function WeiboIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M10.31 8.16c-3.36.34-5.94 2.4-5.94 4.86 0 2.72 3.12 4.62 6.95 4.62 3.5 0 6.32-1.74 6.32-4.05 0-2.16-2.45-3.7-5.62-3.84a8.6 8.6 0 0 0-1.71-1.59zm1.06 7.65c-1.84.18-3.45-.7-3.59-1.97-.14-1.27 1.27-2.45 3.13-2.65 1.86-.18 3.46.7 3.6 1.97.13 1.27-1.27 2.45-3.14 2.65zm9.27-9.4c-.91-1.04-2.27-1.46-3.6-1.13a.78.78 0 0 0-.55.95c.1.42.55.7.97.6.83-.21 1.7.05 2.27.7.57.65.74 1.55.43 2.36a.79.79 0 0 0 .51.99c.42.13.87-.1 1-.51.5-1.32.21-2.83-.93-3.96zm-2.74 2.15a1.83 1.83 0 0 0-2.27-.62.62.62 0 0 0-.36.8.62.62 0 0 0 .8.37c.31-.1.65 0 .87.27.21.26.24.62.07.92-.16.31-.05.7.27.87.32.16.7.05.87-.27.43-.76.31-1.7-.25-2.34z" />
    </svg>
  );
}

/* ============================================================
 * 9. Kuaishou(快手)
 * ============================================================ */
export function KuaishouIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 14V8l6 4-6 4z" />
    </svg>
  );
}

/* ============================================================
 * 10. WeChat(微信)
 * ============================================================ */
export function WechatIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M8.69 4C4.96 4 2 6.5 2 9.6c0 1.74.92 3.3 2.4 4.34L4 16l2.32-1.16a7.6 7.6 0 0 0 2.37.36c-.07-.36-.1-.74-.1-1.13 0-3.13 2.94-5.66 6.58-5.66.27 0 .54.01.8.04C15.36 5.66 12.36 4 8.69 4zM6 7.5a1 1 0 1 1 0 2 1 1 0 0 1 0-2zm5 0a1 1 0 1 1 0 2 1 1 0 0 1 0-2zm3.6 2.7c-3.36 0-6.1 2.27-6.1 5.07 0 2.8 2.74 5.07 6.1 5.07.7 0 1.36-.1 1.99-.28L18 21l-.35-1.8c1.27-.92 2.05-2.27 2.05-3.78 0-2.8-2.74-5.07-6.1-5.07zm-1.85 3a.85.85 0 1 1 0 1.7.85.85 0 0 1 0-1.7zm3.7 0a.85.85 0 1 1 0 1.7.85.85 0 0 1 0-1.7z" />
    </svg>
  );
}

/* ============================================================
 * 11. Lemon8
 * ============================================================ */
export function Lemon8Icon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M19 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2zm-9.5 14L7 12l2.5-5h5l2.5 5-2.5 5h-5z" />
    </svg>
  );
}

/* ============================================================
 * 12. Zhihu(知乎)
 * ============================================================ */
export function ZhihuIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M5.7 5.4h13.6c.5 0 .9.4.9.9v.4c0 .5-.4.9-.9.9H5.7c-.5 0-.9-.4-.9-.9v-.4c0-.5.4-.9.9-.9zm10.6 3.7h-7c-.4 0-.8.3-.8.8v8.5c0 .4.3.8.8.8h7c.4 0 .8-.3.8-.8V9.9c-.1-.4-.4-.8-.8-.8zm-5.7 1.6h4.4v1H10.6v-1zm0 2.3h4.4v1H10.6v-1zm0 2.3h4.4v1H10.6v-1zm0 2.3h2.8v1h-2.8v-1zM3 6.5h2.3v11.4h.6L8 20.4l-1.4 1.4-3.6-3.5V6.5z" />
    </svg>
  );
}

/* ============================================================
 * 13. Reddit
 * ============================================================ */
export function RedditIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M12 2a10 10 0 0 0-9.95 9.06A2.5 2.5 0 0 0 1 13.5c0 1.1.7 2 1.7 2.4a5.4 5.4 0 0 0-.05.7c0 3.36 4.18 6.08 9.35 6.08 5.16 0 9.35-2.72 9.35-6.08 0-.24-.02-.47-.05-.7 1-.4 1.7-1.3 1.7-2.4 0-1.06-.65-1.97-1.6-2.4A10 10 0 0 0 12 2zM7 13.5a1.5 1.5 0 1 1 3 0 1.5 1.5 0 0 1-3 0zm7.65 4.27c-.84.85-2.04 1.32-3.46 1.32-1.41 0-2.6-.46-3.45-1.3a.5.5 0 0 1 .7-.72c.62.6 1.55.94 2.75.94 1.2 0 2.13-.34 2.76-.95a.5.5 0 0 1 .7.71zM16 15a1.5 1.5 0 1 1 0-3 1.5 1.5 0 0 1 0 3zM6 6.5a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3zM18 6a1.5 1.5 0 1 1 0 3 1.5 1.5 0 0 1 0-3zm-3-1.5a4 4 0 0 0-3.5 2 4 4 0 0 0-3.5-2 .5.5 0 0 0 0 1 3 3 0 0 1 2.85 2.07 4 4 0 0 0-1.95 1.5.5.5 0 0 0 .8.6 3 3 0 0 1 5.6 0 .5.5 0 0 0 .8-.6 4 4 0 0 0-1.95-1.5A3 3 0 0 1 15 5.5a.5.5 0 0 0 0-1z" />
    </svg>
  );
}

/* ============================================================
 * 14. LinkedIn
 * ============================================================ */
export function LinkedinIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M20.45 20.45h-3.55v-5.57c0-1.33-.03-3.04-1.85-3.04-1.86 0-2.14 1.45-2.14 2.95v5.66H9.36V9h3.41v1.56h.05c.47-.9 1.64-1.85 3.37-1.85 3.6 0 4.27 2.37 4.27 5.46v6.28zM5.34 7.43a2.06 2.06 0 1 1 0-4.13 2.06 2.06 0 0 1 0 4.13zM7.12 20.45H3.56V9h3.56v11.45zM22.22 0H1.77C.79 0 0 .77 0 1.72v20.55C0 23.23.79 24 1.77 24h20.45c.98 0 1.78-.77 1.78-1.72V1.72C24 .77 23.2 0 22.22 0z" />
    </svg>
  );
}

/* ============================================================
 * 平台数据数组(供 marquee / 平台选择区使用)
 * ============================================================ */
import type { Platform } from "./types";

export const PLATFORMS: Platform[] = [
  {
    id: "tiktok",
    name: "TikTok API",
    subtitle: "视频、用户、热门",
    description: "通过我们开发者友好的 API 轻松访问 TikTok 视频、用户信息和热门内容。",
    icon: TiktokIcon,
  },
  {
    id: "douyin",
    name: "抖音 API",
    subtitle: "视频、直播、商品",
    description: "访问抖音平台的海量短视频、直播数据和电商信息。",
    icon: DouyinIcon,
  },
  {
    id: "instagram",
    name: "Instagram API",
    subtitle: "帖子、故事、Reels",
    description: "提取 Instagram 公开帖子、故事、Reels 视频和用户互动数据。",
    icon: InstagramIcon,
  },
  {
    id: "youtube",
    name: "YouTube API",
    subtitle: "视频、频道、播放列表",
    description: "获取 YouTube 视频元数据、频道统计、评论和趋势分析。",
    icon: YoutubeIcon,
  },
  {
    id: "twitter",
    name: "X (Twitter) API",
    subtitle: "推文、趋势、用户",
    description: "实时访问 X (Twitter) 推文、话题趋势、用户资料和互动数据。",
    icon: XTwitterIcon,
  },
  {
    id: "rednote",
    name: "小红书 API",
    subtitle: "笔记、用户、商品",
    description: "抓取小红书笔记内容、用户画像、热门话题和电商数据。",
    icon: RednoteIcon,
  },
  {
    id: "bilibili",
    name: "哔哩哔哩 API",
    subtitle: "视频、弹幕、UP主",
    description: "访问 B 站视频信息、弹幕数据、UP 主统计和热门排行。",
    icon: BilibiliIcon,
  },
  {
    id: "weibo",
    name: "微博 API",
    subtitle: "微博、话题、用户",
    description: "抓取微博公开内容、热门话题、用户关系和事件传播路径。",
    icon: WeiboIcon,
  },
  {
    id: "kuaishou",
    name: "快手 API",
    subtitle: "视频、直播、用户",
    description: "访问快手短视频、直播数据和电商信息。",
    icon: KuaishouIcon,
  },
  {
    id: "wechat",
    name: "微信公众号 API",
    subtitle: "文章、阅读、互动",
    description: "抓取微信公众号文章、阅读量、点赞和用户互动数据。",
    icon: WechatIcon,
  },
  {
    id: "lemon8",
    name: "Lemon8 API",
    subtitle: "帖子、用户、趋势",
    description: "访问 Lemon8 平台的生活方式内容和趋势数据。",
    icon: Lemon8Icon,
  },
  {
    id: "zhihu",
    name: "知乎 API",
    subtitle: "问答、用户、话题",
    description: "抓取知乎问题、答案、用户专业度和话题热度数据。",
    icon: ZhihuIcon,
  },
  {
    id: "reddit",
    name: "Reddit API",
    subtitle: "帖子、评论、子版",
    description: "访问 Reddit 各 subreddit 的帖子、评论、投票和趋势数据。",
    icon: RedditIcon,
  },
  {
    id: "linkedin",
    name: "LinkedIn API",
    subtitle: "个人资料、公司、职位",
    description: "抓取 LinkedIn 公开资料、公司信息、职位发布和行业数据。",
    icon: LinkedinIcon,
  },
];

/* ============================================================
 * 小图标(月亮、太阳、Play、X、LinkedIn、箭头、盾牌、徽章等)
 * ============================================================ */

export function MoonIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
    </svg>
  );
}

export function SunIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
    </svg>
  );
}

export function ArrowRightIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="M5 12h14M12 5l7 7-7 7" />
    </svg>
  );
}

export function SearchIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <circle cx="11" cy="11" r="8" />
      <path d="m21 21-4.35-4.35" />
    </svg>
  );
}

export function ChevronDownIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="m6 9 6 6 6-6" />
    </svg>
  );
}

export function PlayIcon(props: IconProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" {...props}>
      <path d="M8 5v14l11-7z" />
    </svg>
  );
}

export function ArrowUpIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="M12 19V5M5 12l7-7 7 7" />
    </svg>
  );
}

/* 盾牌徽章(GDPR / CCPA) */
export function ShieldIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
      <path d="m9 12 2 2 4-4" />
    </svg>
  );
}

/* 内部"新" 角标 */
export function SparkleIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="M12 3v3M12 18v3M3 12h3M18 12h3M5.6 5.6l2.1 2.1M16.3 16.3l2.1 2.1M5.6 18.4l2.1-2.1M16.3 7.7l2.1-2.1" />
    </svg>
  );
}

/* Use case 通用图标 */
export function BotIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="M12 8V4H8M2 14h2M20 14h2M15 13v2M9 13v2M6 18v-2a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v2a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2zM6 10a6 6 0 0 1 12 0v4" />
    </svg>
  );
}

export function DatabaseIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <ellipse cx="12" cy="5" rx="9" ry="3" />
      <path d="M3 5v14c0 1.66 4.03 3 9 3s9-1.34 9-3V5" />
      <path d="M3 12c0 1.66 4.03 3 9 3s9-1.34 9-3" />
    </svg>
  );
}

export function FileTextIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <path d="M14 2v6h6M16 13H8M16 17H8M10 9H8" />
    </svg>
  );
}

export function LayersIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="m12 2 9 5-9 5-9-5 9-5z" />
      <path d="m3 12 9 5 9-5M3 17l9 5 9-5" />
    </svg>
  );
}

export function LightbulbIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="M9 18h6M10 22h4M12 2a7 7 0 0 0-4 12.7c.8.7 1.5 1.5 1.5 2.3h5c0-.8.7-1.6 1.5-2.3A7 7 0 0 0 12 2z" />
    </svg>
  );
}

export function WorkflowIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <rect x="3" y="3" width="6" height="6" rx="1" />
      <rect x="15" y="15" width="6" height="6" rx="1" />
      <path d="M9 6h3a3 3 0 0 1 3 3v9" />
    </svg>
  );
}

export function CheckIcon(props: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="M20 6 9 17l-5-5" />
    </svg>
  );
}
