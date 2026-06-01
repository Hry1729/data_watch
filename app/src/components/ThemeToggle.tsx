import { Sun, Moon, Monitor } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useTheme } from "@/contexts/ThemeContext";

const NEXT = { light: "dark", dark: "system", system: "light" } as const;
const LABEL = { light: "亮色", dark: "暗色", system: "跟随系统" } as const;

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const Icon = theme === "light" ? Sun : theme === "dark" ? Moon : Monitor;
  return (
    <Button
      variant="ghost"
      size="icon"
      className="h-7 w-7"
      onClick={() => setTheme(NEXT[theme])}
      aria-label={`主题：${LABEL[theme]}（点击切换）`}
      title={`主题：${LABEL[theme]}`}
    >
      <Icon className="h-4 w-4" />
    </Button>
  );
}
