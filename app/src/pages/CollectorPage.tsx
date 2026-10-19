import { useMemo, useState } from "react";
import { Activity, AlarmClock, Database, MapPin, Plug } from "lucide-react";
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
import type { CollectorTaskRow } from "@/types";

// ─── 连接状态 ──────────────────────────────────────────────────────────────────

function ConnectStatusChip({ status }: { status: CollectorTaskRow["connectStatus"] }) {
  const map = {
    online: { label: "在线", tone: "success" },
    unstable: { label: "不稳定", tone: "warning" },
    offline: { label: "离线", tone: "off" },
  } as const;
  const { label, tone } = map[status];
  return <StatusDot tone={tone} label={label} />;
}

const COUNT_BADGE_CLASS = "h-5 rounded-md px-2 text-xs font-medium";

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

  const taskCounts = useMemo(() => ({
    online: tasks.filter((t) => t.connectStatus === "online").length,
    unstable: tasks.filter((t) => t.connectStatus === "unstable").length,
    offline: tasks.filter((t) => t.connectStatus === "offline").length,
  }), [tasks]);

  const logToggle = <LogViewToggle value={logView} onChange={setLogView} />;

  return (
    <div className="flex h-full min-h-0 max-w-[1400px] flex-col gap-4 overflow-hidden">
      {showError && (
        <p className="text-xs text-destructive mt-1 px-1" role="alert">
          {error}
        </p>
      )}

      <div className="grid shrink-0 grid-cols-2 gap-3 lg:grid-cols-5">
        <MonitorKpiCard
          label="最短周期"
          icon={AlarmClock}
          value={metrics?.scanRateMs != null ? formatCount(metrics.scanRateMs) : "—"}
          unit="ms"
          detail="最快扫描周期"
        />
        <MonitorKpiCard
          label="设备连接"
          icon={Plug}
          value={metrics ? formatCount(metrics.onlineDevices) : "—"}
          unit={metrics ? `/ ${formatCount(metrics.totalDevices)}` : undefined}
          detail={onlineRate != null ? `在线率 ${onlineRate}% · ${metrics ? metrics.totalDevices - metrics.onlineDevices : 0} 台未连接` : "—"}
        />
        <MonitorKpiCard
          label="采集点位"
          icon={MapPin}
          value={metrics ? formatCount(metrics.pointTotal) : "—"}
          detail="已配置采集点位"
        />
        <MonitorKpiCard
          label="Redis 写入批次"
          icon={Database}
          value={formatCount(cRate?.current)}
          unit={cRate?.unit ?? "批次/min"}
          detail="写入 Redis Stream"
        />
        <MonitorKpiCard
          label="Redis 有效点数"
          icon={Activity}
          value={formatCount(cPointRate?.current)}
          unit={cPointRate?.unit ?? "点/min"}
          detail="写入 Redis Stream"
        />
      </div>

      {/* 主体：左侧任务表 + 趋势图 / 右侧日志（上下与左列齐平） */}
      <div className="grid min-h-0 flex-1 overflow-hidden grid-cols-5 items-stretch gap-4">
        {/* 左侧（3/5） */}
        <div className="col-span-3 grid min-h-0 grid-rows-[360px_minmax(0,1fr)] gap-4 overflow-hidden p-px">
          <Card className="flex h-full min-h-0 flex-col gap-0 py-0">
            <div className="flex items-start gap-3 px-5 pt-5">
              <div className="min-w-0">
                <CardTitle className="text-sm font-semibold leading-none">采集任务</CardTitle>
                <CardDescription className="mt-1.5 text-[13px] leading-5">按设备展示扫描周期、点位与最新采集时间</CardDescription>
              </div>
              {tasks.length > 0 && (
                <div className="ml-auto flex shrink-0 gap-2">
                  <Badge className={`${COUNT_BADGE_CLASS} bg-success-soft text-success`}>在线 {taskCounts.online}</Badge>
                  <Badge className={`${COUNT_BADGE_CLASS} bg-warning-soft text-warning`}>不稳定 {taskCounts.unstable}</Badge>
                  <Badge className={`${COUNT_BADGE_CLASS} bg-muted text-muted-foreground`}>离线 {taskCounts.offline}</Badge>
                </div>
              )}
            </div>
            <div className={`mt-3 ${MONITOR_TABLE_SCROLL_CLASS}`}>
              <Table>
                <TableHeader className="[&_tr]:border-b-0">
                  <TableRow className="hover:bg-transparent">
                    <TableHead className={MONITOR_TH_CLASS}>设备</TableHead>
                    <TableHead className={MONITOR_TH_CLASS}>分组</TableHead>
                    <TableHead className={MONITOR_TH_CLASS}>协议</TableHead>
                    <TableHead className={MONITOR_TH_CLASS}>周期</TableHead>
                    <TableHead className={`${MONITOR_TH_CLASS} text-right`}>点位</TableHead>
                    <TableHead className={MONITOR_TH_CLASS}>最新时间</TableHead>
                    <TableHead className={MONITOR_TH_CLASS}>状态</TableHead>
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
                        <TableCell className={`${MONITOR_TD_CLASS} font-mono`}>{t.deviceCode}</TableCell>
                        <TableCell className={`${MONITOR_TD_CLASS} text-muted-foreground`}>{t.groupName || "—"}</TableCell>
                        <TableCell className={MONITOR_TD_CLASS}>
                          {t.protocol ? (
                            <Badge variant="outline" className="h-5 rounded-md bg-transparent px-2 text-xs font-normal dark:bg-transparent">{t.protocol}</Badge>
                          ) : "—"}
                        </TableCell>
                        <TableCell className={`${MONITOR_TD_CLASS} font-mono`}>{t.scanRateMs != null ? `${t.scanRateMs}ms` : "—"}</TableCell>
                        <TableCell className={`${MONITOR_TD_CLASS} text-right font-mono tabular-nums`}>{t.pointCount.toLocaleString("en-US")}</TableCell>
                        <TableCell className={`${MONITOR_TD_CLASS} font-mono tabular-nums ${t.connectStatus === "offline" ? "text-muted-foreground" : ""}`}>{t.lastCollectTime || "—"}</TableCell>
                        <TableCell className={MONITOR_TD_CLASS}>
                          <ConnectStatusChip status={t.connectStatus} />
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
            title="采集写入趋势"
            description="Collector 写入 Redis Stream 的批次速率"
            unit={cRate?.unit ?? "批次/min"}
            state={rateTrendState(cRate, ratesLoading, ratesError)}
            data={cRate?.series ?? []}
            current={cRate?.current ?? null}
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
