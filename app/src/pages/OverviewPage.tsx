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

// ─── 确认 Dialog 文案 ─────────────────────────────────────────────────────────

const CONFIRM_TEXT = {
  collector: {
    stop:    { title: "确认停止 Collector",  desc: "停止后将停止数据采集，需手动重新启动。" },
    restart: { title: "确认重启 Collector",  desc: "重启期间采集将短暂中断。" },
  },
  writer: {
    stop:    { title: "确认停止 Writer",     desc: "停止后将停止数据入库，需手动重新启动。" },
    restart: { title: "确认重启 Writer",     desc: "重启期间入库将短暂中断。" },
  },
} as const;

// ─── 单模块控制区（复用两次）──────────────────────────────────────────────────

function ModuleSection({ name }: { name: "collector" | "writer" }) {
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
    <>
      <ModuleSummaryCard
        name={name}
        status={status}
        loading={isInitialLoading}
        operating={ctl.operating}
        onStart={ctl.onStart}
        onStop={ctl.onStop}
        onRestart={ctl.onRestart}
        onViewLog={() => navigate(`/${name}`, { state: { focusLog: true } })}
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
    </>
  );
}

// ─── 页面 ─────────────────────────────────────────────────────────────────────

export function OverviewPage() {
  // 二期速率/趋势（与详情页同源，总览复用同一份数据）
  const { data: rates, loading: ratesLoading, error: ratesError } = useModuleRates();
  const cRate = rates?.collector;
  const wRate = rates?.writer;

  return (
    <div className="px-6 py-6 space-y-4" style={{ maxWidth: 1400 }}>
      {/* 顶部：Collector / Writer 一行两列 */}
      <div className="grid grid-cols-2 gap-4">
        <ModuleSection name="collector" />
        <ModuleSection name="writer" />
      </div>

      {/* 趋势图：两行各一张全宽，接入 module-monitor/rates */}
      <TrendChart
        title="采集写入趋势"
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
