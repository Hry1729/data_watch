import { useCallback, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { Activity, ChartLine, CloudUpload, HardDriveDownload, Network } from "lucide-react";
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
import { KpiStats, MonitorKpiCard, formatShort } from "@/components/MonitorKpiCard";
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
  const wRate = rates?.writer;
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

  const rateDetail = (stats: ReturnType<typeof rateStats>, short = false) => {
    if (!stats) return "近 10 分钟暂无数据";
    const f = short ? formatShort : formatNumber;
    return <KpiStats items={[["峰", f(stats.max)], ["均", f(stats.avg)], ["谷", f(stats.min)]]} />;
  };
  // 集中式：collector 速率即“采集量”，无汇聚模块；分布式中心：同一 Redis 入队速率来自 Ingest，即“汇聚量”
  const collectKpi = isCenter
    ? { value: "—", detail: "采集在边缘节点执行" }
    : { value: formatNumber(cRate?.current), detail: rateDetail(cStats) };
  const ingestKpi = isCenter
    ? { value: formatNumber(cRate?.current), detail: rateDetail(cStats) }
    : { value: "—", detail: "当前部署无汇聚模块" };

  return (
    <div className="flex min-h-full w-full flex-col gap-4">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <MonitorKpiCard
          denseDetail
          label="采集量"
          icon={Activity}
          value={collectKpi.value}
          unit={cRate?.unit ?? "批次/min"}
          detail={collectKpi.detail}
        />
        <MonitorKpiCard
          denseDetail
          label="汇聚量"
          icon={Network}
          value={ingestKpi.value}
          unit={cRate?.unit ?? "批次/min"}
          detail={ingestKpi.detail}
        />
        <MonitorKpiCard
          denseDetail
          label="入库量"
          icon={HardDriveDownload}
          value={formatNumber(wLandedRate?.current)}
          unit={wLandedRate?.unit ?? "rows/min"}
          detail={rateDetail(wLandedStats, true)}
        />
        <MonitorKpiCard
          denseDetail
          label="入库积压 Stream Lag"
          icon={ChartLine}
          value={writerMetrics.data ? formatNumber(writerMetrics.data.streamLagTotal) : "—"}
          unit="条"
          detail={<KpiStats items={[["阈值", formatNumber(BACKLOG_THRESHOLD)], ["Stream", `${writerMetrics.data?.streams.length ?? 0} 个`]]} />}
          tone={(writerMetrics.data?.streamLagTotal ?? 0) >= BACKLOG_THRESHOLD ? "warning" : "normal"}
        />
        <MonitorKpiCard
          denseDetail
          label="边缘上行积压"
          icon={CloudUpload}
          value={isCenter && nodesQuery.data ? formatNumber(edgeStats.backlog) : "—"}
          unit="条"
          detail={edgeDetail}
          tone={edgeStats.watermarkWarnings > 0 ? "warning" : "normal"}
        />
      </div>

      {/* 当前构建角色的两个受管模块 */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        {OVERVIEW_MODULES.map((name) => <ModuleSection key={name} name={name} />)}
      </div>

      {/* 双序列写入趋势：采集（或汇聚）蓝、入库绿，双轴；撑满到内容区底部 */}
      <div className="grid min-h-[320px] flex-1">
        <TrendChart
          fillHeight
          title={isCenter ? "汇聚入队趋势" : "写入趋势"}
          unit={cRate?.unit ?? "批次/min"}
          seriesLabel={isCenter ? "汇聚入队" : "采集写入"}
          state={rateTrendState(cRate, ratesLoading, ratesError)}
          data={cRate?.series ?? []}
          current={cRate?.current ?? null}
          secondary={{
            label: "入库写入",
            unit: wRate?.unit ?? "条/min",
            data: wRate?.series ?? [],
          }}
        />
      </div>
    </div>
  );
}
