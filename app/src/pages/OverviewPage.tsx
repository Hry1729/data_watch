import { useNavigate } from "react-router-dom";
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
import { ModuleSummaryCard } from "@/components/ModuleSummaryCard";
import { TrendChart } from "@/components/TrendChart";
import {
  fallbackStatus,
  useModuleStatus,
  useProcessControl,
} from "@/hooks/useModuleStatus";
import { useModuleRates, rateTrendState } from "@/hooks/useModuleRates";
import { BUILD_ROLE } from "@/config/build-role";
import type { ModuleName } from "@/types";

type OverviewModuleName = Exclude<ModuleName, "uploader">;

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

  return (
    <div className="px-6 py-6 space-y-4" style={{ maxWidth: 1400 }}>
      {/* 顶部：当前构建角色的两个受管模块 */}
      <div className="grid grid-cols-2 gap-4">
        {OVERVIEW_MODULES.map((name) => <ModuleSection key={name} name={name} />)}
      </div>

      {/* 趋势图：两行各一张全宽，接入 module-monitor/rates */}
      <TrendChart
        title={BUILD_ROLE === "distributed-center" ? "汇聚入队趋势" : "采集写入趋势"}
        unit={cRate?.unit ?? "批次/min"}
        state={rateTrendState(cRate, ratesLoading, ratesError)}
        data={cRate?.series ?? []}
        current={cRate?.current ?? null}
      />
      <TrendChart
        title="入库写入趋势"
        unit={wRate?.unit ?? "rows/min"}
        state={rateTrendState(wRate, ratesLoading, ratesError)}
        data={wRate?.series ?? []}
        current={wRate?.current ?? null}
      />
    </div>
  );
}
