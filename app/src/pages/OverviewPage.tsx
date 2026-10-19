import { useCallback, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { Activity, CloudUpload, Database, GitMerge, Layers } from "lucide-react";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Card, CardContent } from "@/components/ui/card";
import { ModuleSummaryCard } from "@/components/ModuleSummaryCard";
import { TrendChart } from "@/components/TrendChart";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import {
  fallbackStatus,
  useModuleStatus,
  useProcessControl,
} from "@/hooks/useModuleStatus";
import { useModuleMetrics } from "@/hooks/useModuleMetrics";
import { useModuleRates, rateTrendState } from "@/hooks/useModuleRates";
import { backendClient } from "@/lib/backend-client";
import { BUILD_ROLE } from "@/config/build-role";
import type { DistributedNode } from "@/types/distributed";
import type { ModuleRate } from "@/types";
type OverviewModuleName = "collector" | "ingest" | "writer";

const BACKLOG_THRESHOLD = 5000;

function formatNumber(value: number | undefined | null) {
  if (value == null || !Number.isFinite(value)) return "—";
  return value.toLocaleString("zh-CN");
}

function rateStats(rate: ModuleRate | undefined) {
  const values = (rate?.series ?? []).map((point) => point.v).filter((value) => Number.isFinite(value));
  if (values.length === 0) return null;
  return {
    max: Math.max(...values),
    avg: Math.round(values.reduce((sum, value) => sum + value, 0) / values.length),
    min: Math.min(...values),
  };
}

function ConsoleKpi({
  label,
  icon: Icon,
  value,
  unit,
  detail,
  warning = false,
}: {
  label: string;
  icon: typeof Activity;
  value: string;
  unit?: string;
  detail: string;
  warning?: boolean;
}) {
  return (
    <Card size="sm" className="min-h-[132px]">
      <CardContent className="flex flex-col px-4 py-1">
        <div className="flex items-center gap-2 text-[13px] text-muted-foreground">
          <Icon className="size-4 shrink-0" />
          <span className="truncate">{label}</span>
        </div>
        <p
          className={`mt-3 text-2xl font-semibold leading-none tabular-nums ${warning ? "text-warning" : "text-foreground"}`}
        >
          {value}
          {unit && <span className="ml-1 text-xs font-medium text-muted-foreground">{unit}</span>}
        </p>
        <p className="mt-3 truncate text-xs text-muted-foreground" title={detail}>{detail}</p>
      </CardContent>
    </Card>
  );
}

// ─── 确认 Dialog 文案 ─────────────────────────────────────────────────────────

const CONFIRM_TEXT = {
  collector: {
    stop:    { title: "确认停止 Collector",  desc: "停止后将停止数据采集，需手动重新启动。" },
    restart: { title: "确认重启 Collector",  desc: "重启期间采集将短暂中断。" },
  },
  ingest: {
    stop:    { title: "确认停止 Ingest",  desc: "停止后将停止接收边缘上传，需手动重新启动。" },
    restart: { title: "确认重启 Ingest",  desc: "重启期间边缘上传将短暂重试。" },
  },
  writer: {
    stop:    { title: "确认停止 Writer",     desc: "停止后将停止数据入库，需手动重新启动。" },
    restart: { title: "确认重启 Writer",     desc: "重启期间入库将短暂中断。" },
  },
} as const;

// ─── 单模块控制区（复用两次）──────────────────────────────────────────────────

function ModuleSection({ name }: { name: OverviewModuleName }) {
  const navigate  = useNavigate();
  const { data, loading, error, refresh } = useModuleStatus(name);
  const ctl = useProcessControl(name, refresh);
  const status = data ?? fallbackStatus(name);

  // 仅在第一次拉取未完成且尚无数据时显示 skeleton
  const isInitialLoading = loading && data === null;
  // 初次拉取失败（没有任何历史数据）时显示 inline 错误
  const showError = !isInitialLoading && error !== null && data === null;

  const pending = ctl.pendingAction;
  const confirmText = pending ? CONFIRM_TEXT[name][pending] : null;

  return (
    <div className="min-w-0 space-y-1">
      <ModuleSummaryCard
        name={name}
        status={status}
        compact
        loading={isInitialLoading}
        operating={ctl.operating}
        onStart={ctl.onStart}
        onStop={ctl.onStop}
        onRestart={ctl.onRestart}
        onViewLog={() => navigate(`/collection/${name}`, { state: { focusLog: true } })}
      />
      {showError && (
        <p className="text-xs text-destructive mt-1 px-1" role="alert">
          {error}
        </p>
      )}

      {/* 停止 / 重启 确认 Dialog */}
      <AlertDialog
        open={pending !== null}
        onOpenChange={(open) => { if (!open) ctl.onCancel(); }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{confirmText?.title}</AlertDialogTitle>
            <AlertDialogDescription>{confirmText?.desc}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={ctl.onCancel}>取消</AlertDialogCancel>
            <AlertDialogAction onClick={ctl.onConfirm}>
              {pending === "stop" ? "确认停止" : "确认重启"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

const OVERVIEW_MODULES: readonly OverviewModuleName[] = BUILD_ROLE === "distributed-center"
  ? ["ingest", "writer"]
  : ["collector", "writer"];

// ─── 页面 ─────────────────────────────────────────────────────────────────────

export function OverviewPage() {
  // 二期速率/趋势（与详情页同源，总览复用同一份数据）
  const { data: rates, loading: ratesLoading, error: ratesError } = useModuleRates();
  const cRate = rates?.collector;
  const cPointRate = rates?.collectorPoints;
  const wLandedRate = rates?.writerLanded;
  const writerMetrics = useModuleMetrics("writer");

  const loadNodes = useCallback(
    (token: string, signal: AbortSignal) => backendClient.distributedNodes(token, signal),
    [],
  );
  const nodesQuery = useBackendQuery<DistributedNode[]>(loadNodes, {
    enabled: BUILD_ROLE === "distributed-center",
    intervalMs: 10_000,
  });

  const edgeStats = useMemo(() => {
    const nodes = nodesQuery.data ?? [];
    return {
      total: nodes.length,
      online: nodes.filter((node) => Boolean(node.lastSeenAt && Date.now() - new Date(node.lastSeenAt).getTime() < 30_000)).length,
      backlog: nodes.reduce((sum, node) => sum + node.outboxBacklog, 0),
      watermarkWarnings: nodes.filter((node) => node.outboxWatermark !== "normal").length,
    };
  }, [nodesQuery.data]);

  const cPointStats = rateStats(cPointRate);
  const cStats = rateStats(cRate);
  const wLandedStats = rateStats(wLandedRate);
  const isCenter = BUILD_ROLE === "distributed-center";
  const edgeDetail = !isCenter
    ? "当前部署无边缘节点"
    : nodesQuery.error
      ? "无法获取边缘节点数据"
      : edgeStats.total === 0
        ? "无已注册边缘节点"
        : `${edgeStats.online} / ${edgeStats.total} 节点在线${edgeStats.watermarkWarnings > 0 ? ` · ${edgeStats.watermarkWarnings} 台水位告警` : ""}`;

  const rateDetail = (stats: ReturnType<typeof rateStats>) =>
    stats
      ? `峰值 ${formatNumber(stats.max)} · 均值 ${formatNumber(stats.avg)} · 谷值 ${formatNumber(stats.min)}`
      : "近 10 分钟暂无数据";

  return (
    <div className="mx-auto w-full max-w-[1400px] space-y-4">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <ConsoleKpi
          label="采集量"
          icon={Activity}
          value={cPointRate?.current != null ? formatNumber(cPointRate.current) : "—"}
          unit={cPointRate?.unit}
          detail={rateDetail(cPointStats)}
        />
        <ConsoleKpi
          label="汇聚量"
          icon={GitMerge}
          value={cRate?.current != null ? formatNumber(cRate.current) : "—"}
          unit={cRate?.unit}
          detail={rateDetail(cStats)}
        />
        <ConsoleKpi
          label="入库量"
          icon={Database}
          value={wLandedRate?.current != null ? formatNumber(wLandedRate.current) : "—"}
          unit={wLandedRate?.unit}
          detail={rateDetail(wLandedStats)}
        />
        <ConsoleKpi
          label="入库积压 Stream Lag"
          icon={Layers}
          value={writerMetrics.data ? formatNumber(writerMetrics.data.streamLagTotal) : "—"}
          unit="条"
          detail={`阈值 ${formatNumber(BACKLOG_THRESHOLD)} · ${writerMetrics.data?.streams.length ?? 0} 个 Stream`}
          warning={(writerMetrics.data?.streamLagTotal ?? 0) >= BACKLOG_THRESHOLD}
        />
        <ConsoleKpi
          label="边缘上行积压"
          icon={CloudUpload}
          value={isCenter && nodesQuery.data ? formatNumber(edgeStats.backlog) : "—"}
          unit="条"
          detail={edgeDetail}
          warning={edgeStats.watermarkWarnings > 0}
        />
      </div>

      {/* 当前构建角色的两个受管模块 */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        {OVERVIEW_MODULES.map((name) => <ModuleSection key={name} name={name} />)}
      </div>

      {/* 双序列写入趋势：采集蓝、入库绿 */}
      <TrendChart
        title="写入趋势"
        unit={cRate?.unit ?? "批次/min"}
        seriesLabel="采集写入"
        state={rateTrendState(cRate, ratesLoading, ratesError)}
        data={cRate?.series ?? []}
        current={cRate?.current ?? null}
        secondary={{
          label: "入库写入",
          unit: wLandedRate?.unit ?? "rows/min",
          data: wLandedRate?.series ?? [],
        }}
      />
    </div>
  );
}
