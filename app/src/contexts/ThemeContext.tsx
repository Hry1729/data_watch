import { createContext, useContext, useEffect, useState, type ReactNode } from "react";

type Theme = "light" | "dark" | "system";
type ResolvedTheme = "light" | "dark";

interface ThemeContextValue {
  theme: Theme;
  resolvedTheme: ResolvedTheme;
  setTheme: (t: Theme) => void;
}

const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);

const MEDIA = "(prefers-color-scheme: dark)";

function systemResolved(): ResolvedTheme {
  return window.matchMedia(MEDIA).matches ? "dark" : "light";
}

function readTheme(storageKey: string, fallback: Theme): Theme {
  const value = localStorage.getItem(storageKey);
  return value === "light" || value === "dark" || value === "system" ? value : fallback;
}

export function ThemeProvider({
  children,
  defaultTheme = "system",
  storageKey = "data-watch-theme",
}: {
  children: ReactNode;
  defaultTheme?: Theme;
  storageKey?: string;
}) {
  const [theme, setThemeState] = useState<Theme>(
    () => readTheme(storageKey, defaultTheme),
  );
  const [resolvedTheme, setResolvedTheme] = useState<ResolvedTheme>(() =>
    theme === "system" ? systemResolved() : theme,
  );

  useEffect(() => {
    const resolved = theme === "system" ? systemResolved() : theme;
    setResolvedTheme(resolved);
    document.documentElement.classList.toggle("dark", resolved === "dark");
  }, [theme]);

  useEffect(() => {
    if (theme !== "system") return;
    const mql = window.matchMedia(MEDIA);
    const onChange = () => {
      const resolved = systemResolved();
      setResolvedTheme(resolved);
      document.documentElement.classList.toggle("dark", resolved === "dark");
    };
    mql.addEventListener("change", onChange);
    return () => mql.removeEventListener("change", onChange);
  }, [theme]);

  const setTheme = (t: Theme) => {
    localStorage.setItem(storageKey, t);
    setThemeState(t);
  };

  return (
    <ThemeContext.Provider value={{ theme, resolvedTheme, setTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
}
