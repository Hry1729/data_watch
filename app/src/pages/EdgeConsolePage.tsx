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
import { useEdgeRates } from "@/hooks/useEdgeRates";
import { fallbackStatus, useModuleStatus, useProcessControl } from "@/hooks/useModuleStatus";
import { rateTrendState } from "@/hooks/useModuleRates";

type EdgeModuleName = "collector" | "uploader" | "storage";

const CONFIRM_TEXT = {
  collector: {
    stop: { title: "确认停止 Collector", description: "停止后将暂停本机设备采集。" },
    restart: { title: "确认重启 Collector", description: "重启期间本机采集会短暂中断。" },
  },
  uploader: {
    stop: { title: "确认停止 Uploader", description: "停止后数据会继续保存在本地 Outbox。" },
    restart: { title: "确认重启 Uploader", description: "重启期间上行同步会短暂重试。" },
  },
  storage: {
    stop: { title: "确认停止 Storage", description: "停止前会先停止 Collector 和 Uploader，避免产生未落盘状态。" },
    restart: { title: "确认重启 Storage", description: "重启期间采集落盘和上行状态更新会短暂重试。" },
  },
} as const;

function modulePath(name: EdgeModuleName): string {
  if (name === "collector") return "/edge/collector";
  if (name === "uploader") return "/edge/uploader";
  return "/edge/storage";
}

function ModuleSection({ name }: { name: EdgeModuleName }) {
  const navigate = useNavigate();
  const status = useModuleStatus(name);
  const control = useProcessControl(name, status.refresh);
  const pending = control.pendingAction;
  const confirmation = pending ? CONFIRM_TEXT[name][pending] : null;

  return (
    <div className="min-w-0 space-y-1">
      <ModuleSummaryCard
        name={name}
        status={status.data ?? fallbackStatus(name)}
        loading={status.loading && !status.data}
        operating={control.operating}
        logButtonLabel="监控"
        onStart={control.onStart}
        onStop={control.onStop}
        onRestart={control.onRestart}
        onViewLog={() => navigate(modulePath(name))}
      />
      {status.error && !status.data && (
        <p className="px-1 text-xs text-destructive" role="alert">{status.error}</p>
      )}
      <AlertDialog open={pending !== null} onOpenChange={(open) => { if (!open) control.onCancel(); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{confirmation?.title}</AlertDialogTitle>
            <AlertDialogDescription>{confirmation?.description}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={control.onCancel}>取消</AlertDialogCancel>
            <AlertDialogAction onClick={control.onConfirm}>
              {pending === "stop" ? "确认停止" : "确认重启"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

export default function EdgeConsolePage() {
  const rates = useEdgeRates();

  return (
    <div className="mx-auto w-full max-w-[1400px] space-y-4">
      <div className="grid gap-4 lg:grid-cols-3">
        <ModuleSection name="collector" />
        <ModuleSection name="storage" />
        <ModuleSection name="uploader" />
      </div>
      <TrendChart
        title="本地采集入队趋势"
        unit={rates.data.collector.unit}
        state={rateTrendState(rates.data.collector, rates.loading, rates.error)}
        data={rates.data.collector.series}
        current={rates.data.collector.current}
      />
      <TrendChart
        title="中心确认上行趋势"
        unit={rates.data.uploader.unit}
        state={rateTrendState(rates.data.uploader, rates.loading, rates.error)}
        data={rates.data.uploader.series}
        current={rates.data.uploader.current}
      />
    </div>
  );
}
