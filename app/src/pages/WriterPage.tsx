import { useMemo, useState } from "react";
import { ChartLine, Clock, Database, HardDriveDownload, TriangleAlert } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Card, CardDescription, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { TrendChart } from "@/components/TrendChart";
import {
  MONITOR_TABLE_SCROLL_CLASS,
  MONITOR_TD_CLASS,
  MONITOR_TH_CLASS,
  MonitorKpiCard,
  StatusDot,
  formatCount,
} from "@/components/MonitorKpiCard";
import { LogPanel } from "@/components/LogPanel";
import { LogViewToggle, type LogView } from "@/components/LogViewToggle";
import { ModuleDbLogPanel } from "@/components/ModuleDbLogPanel";
import { useModuleStatus } from "@/hooks/useModuleStatus";
import { useWebSocketLog } from "@/hooks/useWebSocketLog";
import { useModuleDbLogs } from "@/hooks/useModuleDbLogs";
import { useModuleMetrics } from "@/hooks/useModuleMetrics";
import { useModuleRates, rateTrendState } from "@/hooks/useModuleRates";
import type { WriterStreamRow } from "@/types";

// ─── Stream 状态 chip ────────────────────────────────────────────────────────────

function StreamStatusChip({ status }: { status: WriterStreamRow["status"] }) {
  const map = {
    normal: { label: "正常", tone: "success" },
    backlog: { label: "积压", tone: "warning" },
  } as const;
  const { label, tone } = map[status];
  return <StatusDot tone={tone} label={label} />;
}

const COUNT_BADGE_CLASS = "h-5 rounded-md px-2 text-xs font-medium";
const BACKLOG_THRESHOLD = 5000;

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

  const streamCounts = useMemo(() => ({
    normal: streams.filter((t) => t.status === "normal").length,
    backlog: streams.filter((t) => t.status === "backlog").length,
  }), [streams]);

  const logToggle = <LogViewToggle value={logView} onChange={setLogView} />;

  return (
    <div className="flex h-full min-h-0 w-full flex-col gap-4 overflow-hidden">
      {showError && (
        <p className="text-xs text-destructive mt-1 px-1" role="alert">
          {error}
        </p>
      )}

      <div className="grid shrink-0 grid-cols-2 gap-3 lg:grid-cols-5">
        <MonitorKpiCard
          label="Stream Lag"
          icon={ChartLine}
          value={metrics ? formatCount(metrics.streamLagTotal) : "—"}
          detail={`积压条目 · 阈值 ${formatCount(BACKLOG_THRESHOLD)}`}
          tone={(metrics?.streamLagTotal ?? 0) >= BACKLOG_THRESHOLD ? "warning" : "normal"}
        />
        <MonitorKpiCard
          label="PEL Pending"
          icon={Clock}
          value={metrics ? formatCount(metrics.pelPendingTotal) : "—"}
          detail={`已读未 ACK · 阈值 ${formatCount(BACKLOG_THRESHOLD)}`}
          tone={(metrics?.pelPendingTotal ?? 0) >= BACKLOG_THRESHOLD ? "warning" : "normal"}
        />
        <MonitorKpiCard
          label="Redis 读取"
          icon={Database}
          value={formatCount(wRate?.current)}
          unit={wRate?.unit ?? "条/min"}
          detail="XREADGROUP 消费"
        />
        <MonitorKpiCard
          label="TDengine 入库"
          icon={HardDriveDownload}
          value={formatCount(wLandedRate?.current)}
          unit={wLandedRate?.unit ?? "rows/min"}
          detail="批量写入 TDengine"
        />
        <MonitorKpiCard
          label="DLQ"
          icon={TriangleAlert}
          value={metrics ? formatCount(metrics.dlqTotal) : "—"}
          detail="死信队列待处理"
        />
      </div>

      {/* 主体：左侧 Stream 表 + 趋势图 / 右侧日志（上下与左列齐平） */}
      <div className="grid min-h-0 flex-1 overflow-hidden grid-cols-5 items-stretch gap-4">
        {/* 左侧（3/5） */}
        <div className="col-span-3 grid min-h-0 grid-rows-[360px_minmax(0,1fr)] gap-4 overflow-hidden p-px">
          <Card className="flex h-full min-h-0 flex-col gap-0 py-0">
            <div className="flex items-start gap-3 px-5 pt-5">
              <div className="min-w-0">
                <CardTitle className="text-sm font-semibold leading-none">Stream 消费任务</CardTitle>
                <CardDescription className="mt-1.5 text-[13px] leading-5">各 Stream 的积压（Lag）与待确认（PEL）</CardDescription>
              </div>
              {streams.length > 0 && (
                <div className="ml-auto flex shrink-0 gap-2">
                  <Badge className={`${COUNT_BADGE_CLASS} bg-success-soft text-success`}>正常 <b className="font-semibold">{streamCounts.normal}</b></Badge>
                  <Badge className={`${COUNT_BADGE_CLASS} bg-warning-soft text-warning`}>积压 <b className="font-semibold">{streamCounts.backlog}</b></Badge>
                </div>
              )}
            </div>
            <div className={`mt-3 ${MONITOR_TABLE_SCROLL_CLASS}`}>
              <Table>
                <TableHeader className="[&_tr]:border-b-0">
                  <TableRow className="hover:bg-transparent">
                    <TableHead className={MONITOR_TH_CLASS}>Stream Key</TableHead>
                    <TableHead className={`${MONITOR_TH_CLASS} text-right`}>Lag</TableHead>
                    <TableHead className={`${MONITOR_TH_CLASS} text-right`}>PEL</TableHead>
                    <TableHead className={`${MONITOR_TH_CLASS} w-[22%]`}>状态</TableHead>
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
                        <TableCell className={`${MONITOR_TD_CLASS} font-mono`}>{t.streamKey}</TableCell>
                        <TableCell className={`${MONITOR_TD_CLASS} text-right font-mono tabular-nums ${t.status === "backlog" ? "text-warning" : ""}`}>{t.lag.toLocaleString("en-US")}</TableCell>
                        <TableCell className={`${MONITOR_TD_CLASS} text-right font-mono tabular-nums`}>{t.pel.toLocaleString("en-US")}</TableCell>
                        <TableCell className={MONITOR_TD_CLASS}>
                          <StreamStatusChip status={t.status} />
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          </Card>

          <TrendChart
            fillHeight
            title="TDengine 入库趋势"
            description="批量写入 TDengine 的行速率"
            unit={wLandedRate?.unit ?? "rows/min"}
            state={rateTrendState(wLandedRate, ratesLoading, ratesError)}
            data={wLandedRate?.series ?? []}
            current={wLandedRate?.current ?? null}
          />
        </div>

        {/* 右侧日志面板（2/5）：实时 WS / 历史 DB 切换，默认实时 */}
        <div className="col-span-2 flex h-full min-h-0 overflow-hidden p-px">
          {logView === "realtime" ? (
            <LogPanel
              lines={lines}
              wsStatus={wsStatus}
              autoScroll={autoScroll}
              onAutoScrollChange={setAutoScroll}
              onClear={clear}
              headerExtra={logToggle}
            />
          ) : (
            <ModuleDbLogPanel
              rows={dbRows}
              loading={dbLoading}
              error={dbError}
              onRefresh={dbRefresh}
              headerExtra={logToggle}
            />
          )}
        </div>
      </div>
    </div>
  );
}
