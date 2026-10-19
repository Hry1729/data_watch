import { useState } from "react";
import { Activity, Clock, Cpu, MemoryStick, Network } from "lucide-react";
import { LogPanel } from "@/components/LogPanel";
import { MonitorKpiCard, formatCount } from "@/components/MonitorKpiCard";
import { UptimeValue } from "@/components/ModuleSummaryCard";
import { TrendChart } from "@/components/TrendChart";
import { useModuleStatus } from "@/hooks/useModuleStatus";
import {
  rateTrendState,
  useModuleRates,
} from "@/hooks/useModuleRates";
import { useWebSocketLog } from "@/hooks/useWebSocketLog";

function processLabel(status: string): string {
  if (status === "running") return "运行中";
  if (status === "stopped") return "已停止";
  if (status === "crashed") return "异常退出";
  return "未知";
}

export function IngestPage() {
  const [autoScroll, setAutoScroll] = useState(true);
  const { data, loading, error } = useModuleStatus("ingest");
  const showError = !loading && error !== null && data === null;
  const processStatus = data?.status ?? "unknown";
  const { data: rates, loading: ratesLoading, error: ratesError } = useModuleRates();
  const ingestRate = rates?.collector;
  const { lines, wsStatus, clear } = useWebSocketLog("ingest");

  return (
    <div className="flex h-full min-h-0 w-full flex-col gap-4 overflow-hidden">
      {showError && <p className="px-1 text-xs text-destructive" role="alert">{error}</p>}

      <div className="grid shrink-0 grid-cols-2 gap-3 lg:grid-cols-5">
        <MonitorKpiCard
          label="运行状态"
          icon={Activity}
          value={data ? processLabel(processStatus) : "—"}
          detail={data?.pid != null ? `PID ${data.pid}` : "Ingest 进程状态"}
          tone={processStatus === "running" ? "success" : processStatus === "crashed" ? "destructive" : "normal"}
        />
        <MonitorKpiCard
          label="汇聚入队"
          icon={Network}
          value={formatCount(ingestRate?.current)}
          unit={ingestRate?.unit ?? "批次/min"}
          detail="写入 Redis Stream"
        />
        <MonitorKpiCard
          label="运行时长"
          icon={Clock}
          value={data ? <UptimeValue seconds={data.uptime_seconds} /> : "—"}
          detail="自上次启动"
        />
        <MonitorKpiCard
          label="内存使用"
          icon={MemoryStick}
          value={data ? data.memory_mb.toFixed(1) : "—"}
          unit="MB"
          detail="Ingest 进程内存"
        />
        <MonitorKpiCard
          label="CPU 使用率"
          icon={Cpu}
          value={data ? data.cpu_percent.toFixed(1) : "—"}
          unit="%"
          detail="Ingest 进程 CPU"
        />
      </div>

      <div className="grid min-h-0 flex-1 grid-cols-1 items-stretch gap-4 overflow-hidden lg:grid-cols-5">
        <div className="min-h-[320px] overflow-hidden p-px lg:col-span-3 lg:min-h-0">
          <TrendChart
            fillHeight
            showStats
            title="汇聚入队趋势"
            description="边缘上传批次经校验后写入 Redis Stream 的速率"
            unit={ingestRate?.unit ?? "批次/min"}
            state={rateTrendState(ingestRate, ratesLoading, ratesError)}
            data={ingestRate?.series ?? []}
            current={ingestRate?.current ?? null}
          />
        </div>
        <div className="min-h-[320px] overflow-hidden p-px lg:col-span-2 lg:min-h-0">
          <LogPanel
            lines={lines}
            wsStatus={wsStatus}
            autoScroll={autoScroll}
            onAutoScrollChange={setAutoScroll}
            onClear={clear}
          />
        </div>
      </div>
    </div>
  );
}