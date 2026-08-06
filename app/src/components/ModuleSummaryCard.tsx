import {
  useEffect,
  useState,
} from "react";
import {
  Play,
  RotateCcw,
  ScrollText,
  Square,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import type { ModuleName, ModuleStatus, ModuleStatusValue } from "@/types";

// ─── 运行时长格式化（HH:MM:SS）────────────────────────────────────────────────

export function formatUptime(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  return [h, m, s].map((v) => String(v).padStart(2, "0")).join(":");
}

// ─── 模块元信息 ───────────────────────────────────────────────────────────────

const MODULE_META = {
  collector: {
    label: "Collector 采集模块",
    subtitle: "实时设备数据采集与流式入队",
    targetLabel: "Redis Stream",
  },
  ingest: {
    label: "Ingest 数据汇聚模块",
    subtitle: "边缘数据校验、汇聚与 Redis Stream 入队",
    targetLabel: "Redis Stream",
  },
  uploader: {
    label: "Uploader 上行同步模块",
    subtitle: "本地离线队列严格按序同步至中心 Ingest",
    targetLabel: "中心 Ingest",
  },
  writer: {
    label: "Writer 入库模块",
    subtitle: "Redis Stream 消费与 TDengine 批量写入",
    targetLabel: "TDengine",
  },
} as const;

// ─── 状态 Badge（4 态色彩区分）────────────────────────────────────────────────

const STATUS_META: Record<
  ModuleStatusValue,
  { label: string; cls: string; dot: string }
> = {
  running: {
    label: "运行中",
    cls: "bg-success-soft text-success hover:bg-success-soft border-success/30",
    dot: "bg-success",
  },
  stopped: {
    label: "已停止",
    cls: "bg-muted text-muted-foreground hover:bg-muted",
    dot: "bg-muted-foreground",
  },
  crashed: {
    label: "异常退出",
    cls: "bg-destructive-soft text-destructive hover:bg-destructive-soft border-destructive/30",
    dot: "bg-destructive",
  },
  unknown: {
    label: "未知",
    cls: "bg-muted text-muted-foreground hover:bg-muted",
    dot: "bg-muted-foreground/70",
  },
};

function StatusBadge({ status }: { status: ModuleStatusValue }) {
  const meta = STATUS_META[status];
  return (
    <Badge
      variant="secondary"
      className={`gap-1.5 rounded-full px-3 py-1 ${meta.cls}`}
    >
      <span className={`h-2 w-2 rounded-full ${meta.dot}`} />
      {meta.label}
    </Badge>
  );
}

// ─── Metric 小卡（用于 Overview 的内嵌 3 卡）─────────────────────────────────

function MetricCard({
  label,
  value,
  loading,
}: {
  label: string;
  value: string;
  loading?: boolean;
}) {
  return (
    <div className="rounded-xl border p-4">
      <div className="mb-3 text-sm text-muted-foreground">
        {label}
      </div>
      {loading ? (
        <Skeleton className="h-7 w-24" />
      ) : (
        <div className="text-2xl font-semibold tabular-nums text-foreground">
          {value}
        </div>
      )}
    </div>
  );
}

// ─── 模块 Header 内容（icon + 标题 + 副标题 + Badge + 可选 PID）──────────────
//
// 抽出为函数：Overview 的 ModuleSummaryCard 与 Detail 页的 ModuleHeaderCard 共用。

function ModuleHeader({
  name,
  status,
  loading,
  showPid,
}: {
  name: ModuleName;
  status: ModuleStatus;
  loading: boolean;
  showPid: boolean;
}) {
  const { label, subtitle } = MODULE_META[name];
  return (
    <div className="flex items-start justify-between gap-4">
      {/* 左：标题 + 副标题 */}
      <div className="flex items-center gap-4 min-w-0">
        <div className="min-w-0">
          <CardTitle className="text-xl text-foreground">{label}</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">{subtitle}</p>
        </div>
      </div>

      {/* 右：状态 Badge + 可选 PID chip */}
      <div className="flex items-center gap-2 shrink-0">
        {showPid && !loading && status.pid != null && (
          <span className="inline-flex items-center rounded-full bg-muted px-2.5 py-1 text-xs font-mono text-muted-foreground">
            PID {status.pid}
          </span>
        )}
        {loading ? (
          <Skeleton className="h-6 w-20 rounded-full" />
        ) : (
          <StatusBadge status={status.status} />
        )}
      </div>
    </div>
  );
}

// ─── ModuleHeaderCard（详情页专用，仅 header 内容）─────────────────────────────

export interface ModuleHeaderCardProps {
  name: ModuleName;
  status: ModuleStatus;
  loading?: boolean;
}

export function ModuleHeaderCard({
  name,
  status,
  loading = false,
}: ModuleHeaderCardProps) {
  return (
    <Card
      className="w-full bg-card border border-border"
      style={{
        borderRadius: 12,
      }}
    >
      <CardContent className="px-6 py-5">
        <ModuleHeader name={name} status={status} loading={loading} showPid />
      </CardContent>
    </Card>
  );
}

// ─── ModuleSummaryCard 组件 Props（Overview 专用）─────────────────────────────

export interface ModuleSummaryCardProps {
  /** 模块标识，决定图标和标题 */
  name: ModuleName;
  /** 运行状态快照 */
  status: ModuleStatus;
  /** 操作进行中 — 全部按钮禁用 */
  operating?: boolean;
  /** 初始加载中 — metric 区 skeleton，按钮禁用 */
  loading?: boolean;
  /** "日志"按钮文案 */
  logButtonLabel?: string;
  // 回调
  onStart?:   () => void;
  onStop?:    () => void;
  onRestart?: () => void;
  onViewLog?: () => void;
}

// ─── ModuleSummaryCard 主组件 ─────────────────────────────────────────────────

export function ModuleSummaryCard({
  name,
  status,
  operating = false,
  loading = false,
  logButtonLabel = "日志",
  onStart,
  onStop,
  onRestart,
  onViewLog,
}: ModuleSummaryCardProps) {
  const disabled = operating || loading;
  const [displayUptimeSeconds, setDisplayUptimeSeconds] = useState(status.uptime_seconds);

  useEffect(() => {
    setDisplayUptimeSeconds(status.uptime_seconds);
    if (loading || status.status !== "running") return;

    const timer = setInterval(() => {
      setDisplayUptimeSeconds((value) => value + 1);
    }, 1_000);

    return () => clearInterval(timer);
  }, [loading, status.status, status.uptime_seconds]);

  const uptimeStr = formatUptime(displayUptimeSeconds);
  const memValueStr = status.memory_mb > 0 ? `${status.memory_mb.toFixed(0)} MB` : "—";
  const cpuValueStr = status.cpu_percent > 0 ? `${status.cpu_percent.toFixed(1)}%` : "0.0%";

  return (
    <Card
      className="w-full bg-card border border-border"
      style={{
        borderRadius: 12,
      }}
    >
      <CardHeader className="pb-2">
        <ModuleHeader name={name} status={status} loading={loading} showPid={false} />
      </CardHeader>

      <CardContent className="space-y-5">
        {/* 3 个运行指标卡 */}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <MetricCard
            label="运行时长"
            value={uptimeStr}
            loading={loading}
          />
          <MetricCard
            label="内存使用"
            value={memValueStr}
            loading={loading}
          />
          <MetricCard
            label="CPU 使用率"
            value={cpuValueStr}
            loading={loading}
          />
        </div>

        {/* 操作按钮 */}
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
          <Button
            variant="outline"
            className="h-11 border-brand/30 text-brand hover:bg-brand/10"
            disabled={disabled}
            onClick={onStart}
          >
            <Play className="mr-2 h-4 w-4" />
            启动
          </Button>
          <Button
            variant="outline"
            className="h-11 border-destructive/30 text-destructive hover:bg-destructive/10"
            disabled={disabled}
            onClick={onStop}
          >
            <Square className="mr-2 h-4 w-4 fill-current" />
            停止
          </Button>
          <Button variant="outline" className="h-11" disabled={disabled} onClick={onRestart}>
            <RotateCcw className="mr-2 h-4 w-4" />
            重启
          </Button>
          <Button variant="outline" className="h-11" disabled={disabled || !onViewLog} onClick={onViewLog}>
            <ScrollText className="mr-2 h-4 w-4" />
            {logButtonLabel}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

// ─── 详情页专用：数据库 metric 卡的值映射 ─────────────────────────────────────
//
// 详情页的"数据库"是 4-card 行中的一员（参考采集周期 / 设备连接...格式），
// 这里导出一个工具函数，让 CollectorPage/WriterPage 直接拿到展示值。

export function getDatabaseLabel(name: ModuleName): string {
  return MODULE_META[name].targetLabel;
}
