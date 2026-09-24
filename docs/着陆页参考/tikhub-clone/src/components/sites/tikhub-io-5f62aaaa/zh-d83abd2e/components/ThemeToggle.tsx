"use client";

import { useEffect, useState } from "react";
import { MoonIcon, SunIcon } from "../../shared/icons";

export function ThemeToggle() {
  const [dark, setDark] = useState(false);

  useEffect(() => {
    const isDark = document.documentElement.classList.contains("dark");
    setDark(isDark);
  }, []);

  function toggle() {
    const next = !dark;
    setDark(next);
    document.documentElement.classList.toggle("dark", next);
    try {
      localStorage.setItem("tikhub-theme", next ? "dark" : "light");
    } catch {
      // localStorage may throw in private mode; intentionally ignored
    }
  }

  return (
    <button
      type="button"
      onClick={toggle}
      className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-border bg-surface transition-colors hover:bg-muted"
      aria-label="切换暗黑模式"
    >
      {dark ? <SunIcon className="h-4 w-4" /> : <MoonIcon className="h-4 w-4" />}
    </button>
  );
}
