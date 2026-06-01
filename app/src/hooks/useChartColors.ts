import { useTheme } from "@/contexts/ThemeContext";

export interface ChartColors {
  line: string;
  grid: string;
  axis: string;        // 轴刻度文字 fill
  cursor: string;      // hover 竖线
  tooltipBg: string;
  tooltipBorder: string;
  text: string;        // tooltip 主文字
  muted: string;       // tooltip 次文字
}

const LIGHT: ChartColors = {
  line: "#2563EB",
  grid: "#EEF2F7",
  axis: "#9CA3AF",
  cursor: "#CBD5E1",
  tooltipBg: "#FFFFFF",
  tooltipBorder: "#E5E7EB",
  text: "#111827",
  muted: "#6B7280",
};

const DARK: ChartColors = {
  line: "#4793FF",
  grid: "#243044",
  axis: "#6B7686",
  cursor: "#3A465A",
  tooltipBg: "#1C2331",
  tooltipBorder: "#2D3748",
  text: "#E8EDF5",
  muted: "#9AA6B6",
};

export function useChartColors(): ChartColors {
  const { resolvedTheme } = useTheme();
  return resolvedTheme === "dark" ? DARK : LIGHT;
}
