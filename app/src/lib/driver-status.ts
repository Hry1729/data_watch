import type { DriverListItem } from "../types/device-monitor";

export function resolveStatusBadge(row: DriverListItem): { label: string; variant: "default" | "secondary" | "destructive" | "outline" } {
  if (row.installStatus === "not_applicable") return { label: "本机不适用", variant: "secondary" };
  if (row.installStatus === "not_installed") return { label: "未安装", variant: "secondary" };
  if (row.installStatus === "missing" || row.source === "missing" || row.loadStatus === "missing") {
    return { label: "包缺失", variant: "destructive" };
  }
  if (row.installStatus === "invalid") {
    return { label: "包校验失败", variant: "destructive" };
  }
  if (row.source === "unknown" || row.installStatus === "unknown") {
    return { label: "安装状态未知", variant: "secondary" };
  }
  if (row.loadStatus === "collector_stopped") {
    return { label: "已安装 · 采集未运行", variant: "secondary" };
  }
  if (row.loadStatus === "unknown") {
    return { label: row.installStatus === "installed" ? "已安装 · 运行状态未知" : "状态未知", variant: "secondary" };
  }
  if (row.loadStatus === "load_failed") {
    return { label: "已安装 · 加载失败", variant: "destructive" };
  }
  if (row.loadStatus === "not_loaded" || row.loadStatus === "absent") {
    return { label: "已安装 · 未加载", variant: "secondary" };
  }
  if (row.source === "unregistered") {
    return { label: "已安装 · 未注册", variant: "secondary" };
  }
  if (row.status === "disabled") {
    return { label: "不可用", variant: "secondary" };
  }
  if (row.loadStatus === "loaded") {
    return { label: (row.boundDeviceCount ?? 0) > 0 ? "已加载 · 有设备引用" : "已加载 · 未使用", variant: "outline" };
  }
  if (row.installStatus === "installed" || row.loadStatus === "installed") {
    return { label: "已安装", variant: "secondary" };
  }
  return { label: "状态未知", variant: "secondary" };
}
