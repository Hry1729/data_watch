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
import type { CollectorTaskRow } from "@/types";

// ─── 指标卡片 ─────────────────────────────────────────────────────────────────

function MetricCard({
  label,
  value,
  unit,
  detail,
}: {
  label: string;
  value: string;
  unit?: string;
  detail: string;
}) {
  return (
    <Card size="sm">
      <CardContent className="px-4 py-1">
        <p className="text-xs text-muted-foreground">{label}</p>
        <p className="mt-2 text-2xl font-semibold leading-none tabular-nums">
          {value}
          {unit && <span className="ml-1 text-xs font-medium text-muted-foreground">{unit}</span>}
        </p>
        <p className="mt-2 truncate text-xs text-muted-foreground">{detail}</p>
      </CardContent>
    </Card>
  );
}

// ─── 连接状态 chip ──────────────────────────────────────────────────────────────

function ConnectStatusChip({ status }: { status: CollectorTaskRow["connectStatus"] }) {
  const map = {
    online: { label: "在线", color: "var(--success)" },
    unstable: { label: "不稳定", color: "var(--warning)" },
    offline: { label: "离线", color: "var(--muted-foreground)" },
  } as const;
  const { label, color } = map[status];
  return <span style={{ color }}>{label}</span>;
}

// ─── 页面 ─────────────────────────────────────────────────────────────────────

export function CollectorPage() {
  const [autoScroll, setAutoScroll] = useState(true);
  const [logView, setLogView] = useState<LogView>("realtime");

  // 进程状态轮询（T015）
  const { data, loading, error } = useModuleStatus("collector");
  const isInitialLoading = loading && data === null;
  const showError = !isInitialLoading && error !== null && data === null;

  // 一期即时指标轮询（5s）
  const { data: metrics, loading: metricsLoading, error: metricsError } = useModuleMetrics("collector");
  const tasks = metrics?.tasks ?? [];

  // 二期速率/趋势轮询（5s，与速率卡同源）
  const { data: rates, loading: ratesLoading, error: ratesError } = useModuleRates();
  const cRate = rates?.collector;
  const cPointRate = rates?.collectorPoints;
  const onlineRate =
    metrics && metrics.totalDevices > 0
      ? ((metrics.onlineDevices / metrics.totalDevices) * 100).toFixed(1)
      : null;

  // 实时日志 WS（T016）
  const { lines, wsStatus, clear } = useWebSocketLog("collector");

  // 历史日志（DB system_log）；collector 不锁 log_type，涵盖其多种类型
  const {
    rows: dbRows,
    loading: dbLoading,
    error: dbError,
    refresh: dbRefresh,
  } = useModuleDbLogs("collector");

  return (
    <div className="flex h-full min-h-0 max-w-[1400px] flex-col gap-4 overflow-hidden">
      {showError && (
        <p className="text-xs text-destructive mt-1 px-1" role="alert">
          {error}
        </p>
      )}

      <div className="grid shrink-0 grid-cols-2 gap-3 lg:grid-cols-5">
        <MetricCard
          label="最短周期"
          value={metrics?.scanRateMs != null ? String(metrics.scanRateMs) : "—"}
          unit="ms"
          detail="最快扫描周期"
        />
        <MetricCard
          label="设备连接"
          value={metrics ? String(metrics.onlineDevices) : "—"}
          unit={metrics ? `/ ${metrics.totalDevices}` : undefined}
          detail={onlineRate != null ? `在线率 ${onlineRate}% · ${metrics ? metrics.totalDevices - metrics.onlineDevices : 0} 台未连接` : "—"}
        />
        <MetricCard
          label="采集点位"
          value={metrics ? metrics.pointTotal.toLocaleString() : "—"}
          detail="已配置采集点位"
        />
        <MetricCard
          label="Redis 写入批次"
          value={cRate?.current != null ? String(cRate.current) : "—"}
          unit={cRate?.unit}
          detail="写入 Redis Stream"
        />
        <MetricCard
          label="Redis 有效点数"
          value={cPointRate?.current != null ? String(cPointRate.current) : "—"}
          unit={cPointRate?.unit}
          detail="写入 Redis Stream"
        />
      </div>

      {/* ⑤ 主体：左侧任务表 + 趋势图 / 右侧日志 */}
      <div className="grid min-h-0 flex-1 overflow-hidden grid-cols-5 items-stretch gap-4">
        {/* 左侧（3/5） */}
        <div className="col-span-3 grid min-h-0 grid-rows-[260px_minmax(0,1fr)] gap-4 overflow-hidden p-px">
          <Card className="flex h-full min-h-0 flex-col">
            <CardHeader>
              <CardTitle>采集任务</CardTitle>
              <CardDescription>按设备展示扫描周期、点位与最新采集时间</CardDescription>
            </CardHeader>
            <CardContent className="flex min-h-0 flex-1 flex-col">
              <div className="h-full overflow-y-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="text-xs">设备</TableHead>
                      <TableHead className="text-xs">分组</TableHead>
                      <TableHead className="text-xs">协议</TableHead>
                      <TableHead className="text-xs">周期</TableHead>
                      <TableHead className="text-xs text-right">点位</TableHead>
                      <TableHead className="text-xs">最新时间</TableHead>
                      <TableHead className="text-xs">状态</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {metricsError && !metrics ? (
                      <TableRow>
                        <TableCell colSpan={7} className="text-center text-xs text-destructive py-6">
                          {metricsError}
                        </TableCell>
                      </TableRow>
                    ) : metricsLoading && !metrics ? (
                      <TableRow>
                        <TableCell colSpan={7} className="text-center text-xs text-muted-foreground/70 py-6">
                          加载中…
                        </TableCell>
                      </TableRow>
                    ) : tasks.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={7} className="text-center text-xs text-muted-foreground/70 py-6">
                          暂无数据
                        </TableCell>
                      </TableRow>
                    ) : (
                      tasks.map((t) => (
                        <TableRow key={t.deviceCode}>
                          <TableCell className="text-xs font-mono">{t.deviceCode}</TableCell>
                          <TableCell className="text-xs text-muted-foreground">{t.groupName || "—"}</TableCell>
                          <TableCell className="text-xs">{t.protocol || "—"}</TableCell>
                          <TableCell className="text-xs font-mono">{t.scanRateMs != null ? `${t.scanRateMs}ms` : "—"}</TableCell>
                          <TableCell className="text-xs text-right font-mono">{t.pointCount.toLocaleString()}</TableCell>
                          <TableCell className="text-xs font-mono">{t.lastCollectTime || "—"}</TableCell>
                          <TableCell className="text-xs">
                            <ConnectStatusChip status={t.connectStatus} />
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
            title="采集写入趋势"
            unit={cRate?.unit ?? "批次/min"}
            state={rateTrendState(cRate, ratesLoading, ratesError)}
            data={cRate?.series ?? []}
            current={cRate?.current ?? null}
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
