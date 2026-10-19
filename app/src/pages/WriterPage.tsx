import { useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { TrendChart } from "@/components/TrendChart";
import { LogPanel } from "@/components/LogPanel";
import { LogViewToggle, type LogView } from "@/components/LogViewToggle";
import { ModuleDbLogPanel } from "@/components/ModuleDbLogPanel";
import { useModuleStatus } from "@/hooks/useModuleStatus";
import { useWebSocketLog } from "@/hooks/useWebSocketLog";
import { useModuleDbLogs } from "@/hooks/useModuleDbLogs";
import { useModuleMetrics } from "@/hooks/useModuleMetrics";
import { useModuleRates, rateTrendState } from "@/hooks/useModuleRates";
import type { WriterStreamRow } from "@/types";

// ─── 指标卡片 ─────────────────────────────────────────────────────────────────

function MetricCard({
  label,
  value,
  unit,
  detail,
  tone = "normal",
}: {
  label: string;
  value: string;
  unit?: string;
  detail: string;
  tone?: "normal" | "warning";
}) {
  return (
    <Card size="sm">
      <CardContent className="px-4 py-1">
        <p className="text-xs text-muted-foreground">{label}</p>
        <p
          className="mt-2 text-2xl font-semibold leading-none tabular-nums"
          style={{ color: tone === "warning" ? "var(--warning)" : undefined }}
        >
          {value}
          {unit && <span className="ml-1 text-xs font-medium text-muted-foreground">{unit}</span>}
        </p>
        <p className="mt-2 truncate text-xs text-muted-foreground">{detail}</p>
      </CardContent>
    </Card>
  );
}

// ─── Stream 状态 chip ────────────────────────────────────────────────────────────

function StreamStatusChip({ status }: { status: WriterStreamRow["status"] }) {
  const map = {
    normal: { label: "正常", color: "var(--success)" },
    backlog: { label: "积压", color: "var(--warning)" },
  } as const;
  const { label, color } = map[status];
  return <span style={{ color }}>{label}</span>;
}

// ─── 页面 ─────────────────────────────────────────────────────────────────────

export function WriterPage() {
  const [autoScroll, setAutoScroll] = useState(true);
  const [logView, setLogView] = useState<LogView>("realtime");

  // 真实状态轮询
  const { data, loading, error } = useModuleStatus("writer");
  const isInitialLoading = loading && data === null;
  const showError = !isInitialLoading && error !== null && data === null;

  // 一期即时指标轮询（5s）
  const { data: metrics, loading: metricsLoading, error: metricsError } = useModuleMetrics("writer");
  const streams = metrics?.streams ?? [];

  // 二期速率/趋势轮询（5s，与速率卡同源）
  const { data: rates, loading: ratesLoading, error: ratesError } = useModuleRates();
  const wRate = rates?.writer;
  const wLandedRate = rates?.writerLanded;

  // 实时日志 WS（T016）
  const { lines, wsStatus, clear } = useWebSocketLog("writer");

  // 历史日志（DB system_log，阶段3）
  const {
    rows: dbRows,
    loading: dbLoading,
    error: dbError,
    refresh: dbRefresh,
  } = useModuleDbLogs("writer", "storage");

  return (
    <div className="flex h-full min-h-0 max-w-[1400px] flex-col gap-4 overflow-hidden">
      {showError && (
        <p className="text-xs text-destructive mt-1 px-1" role="alert">
          {error}
        </p>
      )}

      <div className="grid shrink-0 grid-cols-2 gap-3 lg:grid-cols-5">
        <MetricCard
          label="Stream Lag"
          value={metrics ? String(metrics.streamLagTotal) : "—"}
          unit="条"
          detail="阈值 5,000"
          tone={(metrics?.streamLagTotal ?? 0) >= 5000 ? "warning" : "normal"}
        />
        <MetricCard
          label="PEL Pending"
          value={metrics ? String(metrics.pelPendingTotal) : "—"}
          unit="条"
          detail="阈值 5,000"
          tone={(metrics?.pelPendingTotal ?? 0) >= 5000 ? "warning" : "normal"}
        />
        <MetricCard
          label="Redis 读取"
          value={wRate?.current != null ? String(wRate.current) : "—"}
          unit={wRate?.unit}
          detail="从 Redis Stream 消费"
        />
        <MetricCard
          label="TDengine 入库"
          value={wLandedRate?.current != null ? String(wLandedRate.current) : "—"}
          unit={wLandedRate?.unit}
          detail="写入 TDengine"
        />
        <MetricCard
          label="DLQ"
          value={metrics ? String(metrics.dlqTotal) : "—"}
          unit="条"
          detail="死信队列"
        />
      </div>

      {/* ⑤ 主体：左侧 Stream 表 + 趋势图 / 右侧日志 */}
      <div className="grid min-h-0 flex-1 overflow-hidden grid-cols-5 items-stretch gap-4">
        {/* 左侧（3/5） */}
        <div className="col-span-3 grid min-h-0 grid-rows-[260px_minmax(0,1fr)] gap-4 overflow-hidden p-px">
          <Card className="flex h-full min-h-0 flex-col">
            <CardHeader>
              <CardTitle>Stream 消费任务</CardTitle>
              <CardDescription>各 Stream 的消费位点与积压状态</CardDescription>
            </CardHeader>
            <CardContent className="flex min-h-0 flex-1 flex-col">
              <div className="h-full overflow-y-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="text-xs">Stream Key</TableHead>
                      <TableHead className="text-xs text-right">Lag</TableHead>
                      <TableHead className="text-xs text-right">PEL</TableHead>
                      <TableHead className="text-xs">状态</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {metricsError && !metrics ? (
                      <TableRow>
                        <TableCell colSpan={4} className="text-center text-xs text-destructive py-6">
                          {metricsError}
                        </TableCell>
                      </TableRow>
                    ) : metricsLoading && !metrics ? (
                      <TableRow>
                        <TableCell colSpan={4} className="text-center text-xs text-muted-foreground/70 py-6">
                          加载中…
                        </TableCell>
                      </TableRow>
                    ) : streams.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={4} className="text-center text-xs text-muted-foreground/70 py-6">
                          暂无数据
                        </TableCell>
                      </TableRow>
                    ) : (
                      streams.map((t) => (
                        <TableRow key={t.streamKey}>
                          <TableCell className="text-xs font-mono">{t.streamKey}</TableCell>
                          <TableCell className="text-xs text-right font-mono">{t.lag}</TableCell>
                          <TableCell className="text-xs text-right font-mono">{t.pel}</TableCell>
                          <TableCell className="text-xs">
                            <StreamStatusChip status={t.status} />
                          </TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>

          <TrendChart
            fillHeight
            title="TDengine 入库趋势"
            unit={wLandedRate?.unit ?? "rows/min"}
            state={rateTrendState(wLandedRate, ratesLoading, ratesError)}
            data={wLandedRate?.series ?? []}
            current={wLandedRate?.current ?? null}
          />
        </div>

        {/* 右侧日志面板（2/5）：实时 WS / 历史 DB 切换，默认实时 */}
        <div className="col-span-2 flex h-full min-h-0 overflow-hidden">
          {logView === "realtime" ? (
            <LogPanel
              lines={lines}
              wsStatus={wsStatus}
              autoScroll={autoScroll}
              onAutoScrollChange={setAutoScroll}
              onClear={clear}
              headerExtra={<LogViewToggle value={logView} onChange={setLogView} />}
            />
          ) : (
            <ModuleDbLogPanel
              rows={dbRows}
              loading={dbLoading}
              error={dbError}
              onRefresh={dbRefresh}
              headerExtra={<LogViewToggle value={logView} onChange={setLogView} />}
            />
          )}
        </div>
      </div>
    </div>
  );
}
