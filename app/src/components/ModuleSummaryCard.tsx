import {
  useEffect,
  useState,
  type ComponentType,
  type ReactNode,
} from "react";
import {
  Activity,
  Clock,
  CloudUpload,
  Cpu,
  Database,
  HardDriveDownload,
  MemoryStick,
  Network,
  Play,
  RotateCcw,
  ScrollText,
  Square,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import type { ModuleName, ModuleStatus, ModuleStatusValue } from "@/types";

// ─── 运行时长格式化（HH:MM:SS）────────────────────────────────────────────────

export function formatUptime(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  return [h, m, s].map((v) => String(v).padStart(2, "0")).join(":");
}

/** 运行时长（分钟粒度）：3天 02时 35分，单位为小字 */
export function UptimeValue({ seconds }: { seconds: number }) {
  const safe = Math.max(0, Math.floor(seconds));
  const d = Math.floor(safe / 86400);
  const h = Math.floor((safe % 86400) / 3600);
  const m = Math.floor((safe % 3600) / 60);
  const unit = "mx-px text-xs font-medium tracking-normal text-muted-foreground";
  return (
    <>
      {d > 0 && (
        <>
          {d}
          <small className={unit}>天</small>{" "}
        </>
      )}
      {String(h).padStart(2, "0")}
      <small className={unit}>时</small>{" "}
      {String(m).padStart(2, "0")}
      <small className={unit}>分</small>
    </>
  );
}

// ─── 模块元信息 ───────────────────────────────────────────────────────────────

const MODULE_META: Record<ModuleName, {
  label: string;
  subtitle: string;
  targetLabel: string;
  icon: ComponentType<{ className?: string }>;
}> = {
  collector: {
    label: "Collector 采集模块",
    subtitle: "实时设备数据采集与流式入队",
    targetLabel: "Redis Stream",
    icon: Activity,
  },
  ingest: {
    label: "Ingest 数据汇聚模块",
    subtitle: "边缘数据校验、汇聚与 Redis Stream 入队",
    targetLabel: "Redis Stream",
    icon: Network,
  },
  uploader: {
    label: "Uploader 上行同步模块",
    subtitle: "本地离线队列严格按序同步至中心 Ingest",
    targetLabel: "中心 Ingest",
    icon: CloudUpload,
  },
  storage: {
    label: "Storage 本地存储模块",
    subtitle: "边缘采集帧、实时快照与上行状态单写落盘",
    targetLabel: "SQLite Outbox",
    icon: Database,
  },
  writer: {
    label: "Writer 入库模块",
    subtitle: "Redis Stream 消费与 TDengine 批量写入",
    targetLabel: "TDengine",
    icon: HardDriveDownload,
  },
};

// ─── 状态 Pill（4 态色彩区分）────────────────────────────────────────────────

const STATUS_META: Record<
  ModuleStatusValue,
  { label: string; cls: string; pulse: boolean }
> = {
  running: {
    label: "运行中",
    cls: "bg-success-soft text-success border-success/30",
    pulse: true,
  },
  stopped: {
    label: "已停止",
    cls: "bg-muted text-muted-foreground border-transparent",
    pulse: false,
  },
  crashed: {
    label: "异常退出",
    cls: "bg-destructive-soft text-destructive border-destructive/30",
    pulse: false,
  },
  unknown: {
    label: "未知",
    cls: "bg-muted text-muted-foreground/80 border-transparent",
    pulse: false,
  },
};

function StatusBadge({ status }: { status: ModuleStatusValue }) {
  const meta = STATUS_META[status];
  return (
    <span
      className={cn(
        "inline-flex h-6 shrink-0 items-center gap-[7px] rounded-full border px-2.5 text-xs font-medium",
        meta.cls,
      )}
    >
      <span className="relative size-[7px]">
        {meta.pulse && <span className="absolute inset-0 animate-ping rounded-full bg-current opacity-50 [animation-duration:2.4s]" />}
        <span className="absolute inset-0 rounded-full bg-current" />
      </span>
      {meta.label}
    </span>
  );
}

// ─── Metric 小格（模块卡内嵌 3 格）──────────────────────────────────────────

function MetricCell({
  label,
  icon: Icon,
  value,
  loading,
  narrow = false,
}: {
  label: string;
  icon: ComponentType<{ className?: string }>;
  value: ReactNode;
  loading?: boolean;
  /** 三列卡片内的窄格：数字略小并允许换行 */
  narrow?: boolean;
}) {
  return (
    <div className={cn("relative min-h-20 min-w-0 overflow-hidden rounded-[10px] bg-[color-mix(in_oklab,var(--muted)_55%,var(--card))] px-3.5 py-3", narrow && "px-3")}>
      <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
        <Icon className="size-[13px] shrink-0" />
        <span className="truncate">{label}</span>
      </div>
      {loading ? (
        <Skeleton className="mt-2.5 h-6 w-16" />
      ) : (
        <div className={cn(
          "relative z-[1] mt-2 truncate whitespace-nowrap text-[22px] font-semibold leading-[1.2] tracking-[-0.02em] tabular-nums text-foreground",
          narrow && "whitespace-normal text-lg leading-[1.25]",
        )}>
          {value}
        </div>
      )}
    </div>
  );
}

const UNIT_CLASS = "ml-0.5 text-xs font-medium tracking-normal text-muted-foreground";

// ─── 模块 Header 内容（icon + 标题 + 副标题 + 输出 + Badge + 可选 PID）────────
//
// 抽出为函数：Overview 的 ModuleSummaryCard 与 Detail 页的 ModuleHeaderCard 共用。

function ModuleHeader({
  name,
  status,
  loading,
  showPid,
  compact = false,
}: {
  name: ModuleName;
  status: ModuleStatus;
  loading: boolean;
  showPid: boolean;
  /** 控制台两列卡片：不显示“输出”行 */
  compact?: boolean;
}) {
  const { label, subtitle, targetLabel, icon: Icon } = MODULE_META[name];
  return (
    <div className="flex items-start gap-3">
      <div className="grid size-10 shrink-0 place-items-center rounded-[10px] bg-brand-soft text-brand">
        <Icon className="size-5" />
      </div>
      <div className="min-w-0 flex-1">
        <CardTitle className="truncate text-base font-semibold leading-[1.3] tracking-[-0.01em] text-foreground">{label}</CardTitle>
        <p className="mt-0.5 truncate text-[13px] text-muted-foreground">{subtitle}</p>
        {!compact && (
          <div className="mt-2 inline-flex items-center gap-1.5 text-xs text-muted-foreground">
            输出
            <span className="rounded-full bg-muted px-2 py-px font-medium text-foreground">{targetLabel}</span>
          </div>
        )}
      </div>

      {/* 右：可选 PID chip + 状态 Pill */}
      <div className="flex shrink-0 items-center gap-2">
        {showPid && !loading && status.pid != null && (
          <span className="inline-flex items-center rounded-full bg-muted px-2.5 py-1 font-mono text-xs text-muted-foreground">
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
      className="w-full bg-card"
    >
      <CardContent className="px-5">
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
  /** 两列控制台使用：指标格较宽、不显示“输出”行 */
  compact?: boolean;
  // 回调
  onStart?:   () => void;
  onStop?:    () => void;
  onRestart?: () => void;
  onViewLog?: () => void;
}

const ACTION_BUTTON_CLASS = "h-9 gap-1.5 px-2 text-[13px] font-medium [&_svg]:size-3.5";

// ─── ModuleSummaryCard 主组件 ─────────────────────────────────────────────────

export function ModuleSummaryCard({
  name,
  status,
  operating = false,
  loading = false,
  logButtonLabel = "日志",
  compact = false,
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

    // 运行时长只显示到分钟，30s 校一次即可
    const startedAt = Date.now();
    const timer = setInterval(() => {
      setDisplayUptimeSeconds(status.uptime_seconds + Math.floor((Date.now() - startedAt) / 1000));
    }, 30_000);

    return () => clearInterval(timer);
  }, [loading, status.status, status.uptime_seconds]);

  const hasMemory = status.memory_mb > 0;
  const cpu = status.cpu_percent > 0 ? status.cpu_percent : 0;

  return (
    <Card
      className={cn("w-full gap-4 bg-card p-5", compact && "h-full")}
    >
      <ModuleHeader name={name} status={status} loading={loading} showPid={false} compact={compact} />

      {/* 3 个运行指标格 */}
      <div className="grid grid-cols-3 gap-2.5">
        <MetricCell
          label="运行时长"
          narrow={!compact}
          icon={Clock}
          value={<UptimeValue seconds={displayUptimeSeconds} />}
          loading={loading}
        />
        <MetricCell
          label="内存使用"
          narrow={!compact}
          icon={MemoryStick}
          value={hasMemory ? <>{status.memory_mb.toFixed(0)}<small className={UNIT_CLASS}>MB</small></> : "—"}
          loading={loading}
        />
        <MetricCell
          label="CPU 使用率"
          narrow={!compact}
          icon={Cpu}
          value={<>{cpu.toFixed(1)}<small className={UNIT_CLASS}>%</small></>}
          loading={loading}
        />
      </div>

      {/* 操作按钮 */}
      <div className={cn("grid grid-cols-4 gap-2", compact && "mt-auto")}>
        <Button
          variant="outline"
          className={cn(ACTION_BUTTON_CLASS, "border-brand/30 text-brand hover:bg-brand/10 hover:text-brand")}
          disabled={disabled}
          onClick={onStart}
        >
          <Play data-icon="inline-start" />
          启动
        </Button>
        <Button
          variant="outline"
          className={cn(ACTION_BUTTON_CLASS, "border-destructive/30 text-destructive hover:bg-destructive/10 hover:text-destructive")}
          disabled={disabled}
          onClick={onStop}
        >
          <Square data-icon="inline-start" className="fill-current" />
          停止
        </Button>
        <Button variant="outline" className={ACTION_BUTTON_CLASS} disabled={disabled} onClick={onRestart}>
          <RotateCcw data-icon="inline-start" />
          重启
        </Button>
        <Button variant="outline" className={ACTION_BUTTON_CLASS} disabled={disabled || !onViewLog} onClick={onViewLog}>
          <ScrollText data-icon="inline-start" />
          {logButtonLabel}
        </Button>
      </div>
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
