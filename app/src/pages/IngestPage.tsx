import { useState } from "react";
import { Card, CardContent } from "@/components/ui/card";
import { LogPanel } from "@/components/LogPanel";
import { formatUptime } from "@/components/ModuleSummaryCard";
import { TrendChart } from "@/components/TrendChart";
import { useModuleStatus } from "@/hooks/useModuleStatus";
import {
  rateTrendState,
  useModuleRates,
} from "@/hooks/useModuleRates";
import { useWebSocketLog } from "@/hooks/useWebSocketLog";

function MetricCard({
  label,
  value,
  unit,
  detail,
  warning = false,
}: {
  label: string;
  value: string;
  unit?: string;
  detail: string;
  warning?: boolean;
}) {
  return (
    <Card size="sm">
      <CardContent className="px-4 py-1">
        <p className="text-xs text-muted-foreground">{label}</p>
        <p className={`mt-2 truncate text-2xl font-semibold leading-none tabular-nums ${warning ? "text-destructive" : "text-foreground"}`}>
          {value}
          {unit && <span className="ml-1 text-xs font-medium text-muted-foreground">{unit}</span>}
        </p>
        <p className="mt-2 truncate text-xs text-muted-foreground">{detail}</p>
      </CardContent>
    </Card>
  );
}

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
    <div className="flex h-full min-h-0 max-w-[1400px] flex-col gap-4 overflow-hidden">
      {showError && <p className="px-1 text-xs text-destructive" role="alert">{error}</p>}

      <div className="grid shrink-0 grid-cols-2 gap-3 lg:grid-cols-5">
        <MetricCard
          label="运行状态"
          value={processLabel(processStatus)}
          detail="Ingest 进程状态"
          warning={processStatus === "crashed"}
        />
        <MetricCard
          label="汇聚入队"
          value={ingestRate?.current != null ? String(ingestRate.current) : "—"}
          unit={ingestRate?.unit}
          detail="写入 Redis Stream"
        />
        <MetricCard
          label="运行时长"
          value={data ? formatUptime(data.uptime_seconds) : "—"}
          detail="自本次启动"
        />
        <MetricCard
          label="内存使用"
          value={data ? data.memory_mb.toFixed(1) : "—"}
          unit="MB"
          detail="当前常驻内存"
        />
        <MetricCard
          label="CPU 使用率"
          value={data ? data.cpu_percent.toFixed(1) : "—"}
          unit="%"
          detail="整机 CPU 占比"
        />
      </div>

      <div className="grid min-h-0 flex-1 grid-cols-1 items-stretch gap-4 overflow-hidden lg:grid-cols-5">
        <div className="min-h-[320px] overflow-hidden p-px lg:col-span-3 lg:min-h-0">
          <TrendChart
            fillHeight
            title="汇聚入队趋势"
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