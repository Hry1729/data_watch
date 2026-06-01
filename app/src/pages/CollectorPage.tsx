import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { TrendChart } from "@/components/TrendChart";
import { LogPanel } from "@/components/LogPanel";
import { ModuleDbLogPanel } from "@/components/ModuleDbLogPanel";
import { useModuleStatus } from "@/hooks/useModuleStatus";
import { useWebSocketLog } from "@/hooks/useWebSocketLog";
import { useModuleDbLogs } from "@/hooks/useModuleDbLogs";
import { useModuleMetrics } from "@/hooks/useModuleMetrics";
import type { CollectorTaskRow } from "@/types";

// ─── 指标卡片 ─────────────────────────────────────────────────────────────────

function MetricCard({ label, value, muted = false }: { label: string; value: string; muted?: boolean }) {
  return (
    <Card style={{ background: "#FFFFFF", border: "1px solid #DDE3ED", borderRadius: 12 }}>
      <CardContent className="px-4 py-4">
        <p className="text-xs text-[#6B7280] mb-1">{label}</p>
        <p className={`text-xl font-bold ${muted ? "text-[#9CA3AF]" : "text-[#111827]"}`}>{value}</p>
      </CardContent>
    </Card>
  );
}

// ─── 连接状态 chip ──────────────────────────────────────────────────────────────

function ConnectStatusChip({ status }: { status: CollectorTaskRow["connectStatus"] }) {
  const map = {
    online: { label: "在线", color: "#16A05D" },
    unstable: { label: "不稳定", color: "#D97706" },
    offline: { label: "离线", color: "#9CA3AF" },
  } as const;
  const { label, color } = map[status];
  return <span style={{ color }}>{label}</span>;
}

// ─── 页面 ─────────────────────────────────────────────────────────────────────

export function CollectorPage() {
  const [autoScroll, setAutoScroll] = useState(true);

  // 进程状态轮询（T015）
  const { data, loading, error } = useModuleStatus("collector");
  const isInitialLoading = loading && data === null;
  const showError = !isInitialLoading && error !== null && data === null;

  // 一期即时指标轮询（5s）
  const { data: metrics, loading: metricsLoading, error: metricsError } = useModuleMetrics("collector");
  const tasks = metrics?.tasks ?? [];

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
    <div className="px-6 py-6 space-y-4" style={{ maxWidth: 1400 }}>
      {showError && (
        <p className="text-xs text-[#E5483F] mt-1 px-1" role="alert">
          {error}
        </p>
      )}

      {/* ④ 指标卡片：3 张接真，速率卡占位 */}
      <div className="grid grid-cols-4 gap-3">
        <MetricCard label="最短周期" value={metrics?.scanRateMs != null ? `${metrics.scanRateMs} ms` : "—"} />
        <MetricCard label="设备连接" value={metrics ? `${metrics.onlineDevices} / ${metrics.totalDevices}` : "—"} />
        <MetricCard label="采集点位" value={metrics ? metrics.pointTotal.toLocaleString() : "—"} />
        <MetricCard label="Redis 写入" value="待接入 · 二期" muted />
      </div>

      {/* ⑤ 主体：左侧任务表 + 趋势图 / 右侧日志 */}
      <div className="grid grid-cols-5 gap-4 items-stretch" style={{ height: 595 }}>
        {/* 左侧（3/5） */}
        <div className="col-span-3 min-h-0 space-y-4">
          <Card style={{ background: "#FFFFFF", border: "1px solid #DDE3ED", borderRadius: 12 }}>
            <CardHeader className="pb-2 pt-4 px-5">
              <CardTitle className="text-sm font-semibold text-[#111827]">采集任务</CardTitle>
            </CardHeader>
            <CardContent className="px-5 pb-4">
              <div className="max-h-48 overflow-y-auto">
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
                        <TableCell colSpan={7} className="text-center text-xs text-[#E5483F] py-6">
                          {metricsError}
                        </TableCell>
                      </TableRow>
                    ) : metricsLoading && !metrics ? (
                      <TableRow>
                        <TableCell colSpan={7} className="text-center text-xs text-[#9CA3AF] py-6">
                          加载中…
                        </TableCell>
                      </TableRow>
                    ) : tasks.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={7} className="text-center text-xs text-[#9CA3AF] py-6">
                          暂无数据
                        </TableCell>
                      </TableRow>
                    ) : (
                      tasks.map((t) => (
                        <TableRow key={t.deviceCode}>
                          <TableCell className="text-xs font-mono">{t.deviceCode}</TableCell>
                          <TableCell className="text-xs text-[#6B7280]">{t.groupName || "—"}</TableCell>
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

          <TrendChart title="采集点趋势" unit="pts/min" state="empty" />
        </div>

        {/* 右侧日志面板（2/5）：实时 WS / 历史 DB 切换，默认实时 */}
        <div className="col-span-2 flex min-h-0 overflow-hidden">
          <Tabs defaultValue="realtime" className="flex h-full w-full flex-col gap-2">
            <TabsList className="w-full">
              <TabsTrigger value="realtime">实时</TabsTrigger>
              <TabsTrigger value="history">历史</TabsTrigger>
            </TabsList>
            <TabsContent value="realtime" className="flex min-h-0 overflow-hidden">
              <LogPanel
                lines={lines}
                wsStatus={wsStatus}
                autoScroll={autoScroll}
                onAutoScrollChange={setAutoScroll}
                onClear={clear}
              />
            </TabsContent>
            <TabsContent value="history" className="flex min-h-0 overflow-hidden">
              <ModuleDbLogPanel
                rows={dbRows}
                loading={dbLoading}
                error={dbError}
                onRefresh={dbRefresh}
              />
            </TabsContent>
          </Tabs>
        </div>
      </div>
    </div>
  );
}
