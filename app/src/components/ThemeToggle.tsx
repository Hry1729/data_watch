import { flushSync } from "react-dom";
import { Sun, Moon, Monitor } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useTheme } from "@/contexts/ThemeContext";

const NEXT = { light: "dark", dark: "system", system: "light" } as const;
const LABEL = { light: "亮色", dark: "暗色", system: "跟随系统" } as const;

type DocWithVT = Document & { startViewTransition?: (cb: () => void) => unknown };

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const Icon = theme === "light" ? Sun : theme === "dark" ? Moon : Monitor;

  function handleToggle(e: React.MouseEvent<HTMLButtonElement>) {
    const next = NEXT[theme];
    const doc = document as DocWithVT;
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    // 不支持 View Transitions 或用户偏好减少动态 → 瞬时切换
    if (!doc.startViewTransition || reduceMotion) {
      setTheme(next);
      return;
    }

    // 以按钮中心为圆心,半径取到最远屏角的距离(铺满全屏)
    const rect = e.currentTarget.getBoundingClientRect();
    const x = rect.left + rect.width / 2;
    const y = rect.top + rect.height / 2;
    const r = Math.hypot(Math.max(x, window.innerWidth - x), Math.max(y, window.innerHeight - y));
    const root = document.documentElement;
    root.style.setProperty("--vt-x", `${x}px`);
    root.style.setProperty("--vt-y", `${y}px`);
    root.style.setProperty("--vt-r", `${r}px`);

    // flushSync 让 React 在截「新」帧前同步提交(新主题图标也进入快照,避免滞后一帧)
    doc.startViewTransition(() => flushSync(() => setTheme(next)));
  }

  return (
    <Button
      variant="ghost"
      size="icon"
      className="h-7 w-7"
      onClick={handleToggle}
      aria-label={`主题：${LABEL[theme]}（点击切换）`}
      title={`主题：${LABEL[theme]}`}
    >
      <Icon className="h-4 w-4" />
    </Button>
  );
}
