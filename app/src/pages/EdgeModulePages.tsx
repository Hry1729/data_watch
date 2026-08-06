import { useCallback, useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { LogPanel } from "@/components/LogPanel";
import { TrendChart } from "@/components/TrendChart";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { useEdgeRates } from "@/hooks/useEdgeRates";
import { useModuleStatus } from "@/hooks/useModuleStatus";
import { rateCardValue, rateTrendState } from "@/hooks/useModuleRates";
import { useWebSocketLog } from "@/hooks/useWebSocketLog";
import {
  agentClient,
  type EdgeLocalDevices,
  type EdgeOutboxStatus,
  type EdgeProcessStatus,
} from "@/lib/agent-client";

type EdgeModuleName = "collector" | "uploader";
type EdgeUploaderSnapshot = EdgeProcessStatus & { outbox: EdgeOutboxStatus };

function useLocalSnapshot<T>(request: () => Promise<T>) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      setData(await request());
      setError(null);
    } catch {
      setError("本机运行数据读取失败");
    }
  }, [request]);

  useEffect(() => {
    void refresh();
    const timer = window.setInterval(() => void refresh(), 3_000);
    return () => window.clearInterval(timer);
  }, [refresh]);

  return { data, error };
}

function formatBytes(value: number): string {
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / 1024 / 1024).toFixed(1)} MB`;
}

function processLabel(status: string): string {
  if (status === "running") return "运行中";
  if (status === "stopped") return "已停止";
  if (status === "crashed") return "异常退出";
  return "未知";
}

function MetricCard({ label, value, warning = false }: { label: string; value: string; warning?: boolean }) {
  return (
    <Card className="rounded-lg border border-border bg-card">
      <CardContent className="px-4 py-3">
        <p className="mb-1 text-xs text-muted-foreground">{label}</p>
        <p className={`truncate text-xl font-bold tabular-nums ${warning ? "text-destructive" : "text-foreground"}`}>{value}</p>
      </CardContent>
    </Card>
  );
}

function ModulePage({
  name,
  metrics,
  children,
}: {
  name: EdgeModuleName;
  metrics: ReactNode;
  children: ReactNode;
}) {
  const [autoScroll, setAutoScroll] = useState(true);
  const status = useModuleStatus(name);
  const logs = useWebSocketLog(name);
  const showError = !status.loading && status.error !== null && status.data === null;
  const processStatus = status.data?.status ?? "unknown";

  return (
    <div className="flex h-full min-h-0 max-w-[1400px] flex-col gap-4 overflow-hidden px-4 py-4 md:px-6">
      {showError && <p className="px-1 text-xs text-destructive" role="alert">{status.error}</p>}
      <div className="grid shrink-0 grid-cols-2 gap-3 lg:grid-cols-5">
        <MetricCard label="运行状态" value={processLabel(processStatus)} warning={processStatus === "crashed"} />
        {metrics}
      </div>
      <div className="grid min-h-0 flex-1 grid-cols-1 items-stretch gap-4 overflow-hidden lg:grid-cols-5">
        <div className="min-h-[420px] overflow-hidden p-px lg:col-span-3 lg:min-h-0">{children}</div>
        <div className="min-h-[320px] overflow-hidden p-px lg:col-span-2 lg:min-h-0">
          <LogPanel
            lines={logs.lines}
            wsStatus={logs.wsStatus}
            autoScroll={autoScroll}
            onAutoScrollChange={setAutoScroll}
            onClear={logs.clear}
          />
        </div>
      </div>
    </div>
  );
}

const requestLocalDevices = () => agentClient.getLocalDevices();
const requestLocalUploader = () => agentClient.getLocalUploader();

export function EdgeCollectorPage() {
  const snapshot = useLocalSnapshot<EdgeLocalDevices>(requestLocalDevices);
  const rates = useEdgeRates();
  const devices = snapshot.data?.devices ?? [];
  const online = devices.filter((device) => device.status === 1).length;
  const scanRates = devices.filter((device) => device.enabled === 1).map((device) => device.scan_rate_ms);
  const fastestRate = scanRates.length > 0 ? `${Math.min(...scanRates)} ms` : "—";

  return (
    <ModulePage
      name="collector"
      metrics={
        <>
          <MetricCard label="已分配设备" value={String(devices.length)} />
          <MetricCard label="在线设备" value={String(online)} />
          <MetricCard label="采集入队" value={rateCardValue(rates.data.collector)} />
          <MetricCard label="最快采集周期" value={fastestRate} />
        </>
      }
    >
      <div className="grid h-full min-h-0 grid-rows-[260px_minmax(0,1fr)] gap-4 overflow-hidden">
        <Card className="flex h-full min-h-0 flex-col gap-0 border border-border py-0">
          <CardHeader className="px-4 py-3">
            <CardTitle>本机设备采集任务</CardTitle>
          </CardHeader>
          <CardContent className="min-h-0 flex-1 overflow-hidden px-4 pb-4">
            {snapshot.error && <p className="pb-2 text-xs text-destructive">{snapshot.error}</p>}
            <div className="h-full overflow-hidden rounded-lg border">
              <Table className="w-full table-fixed">
                <TableHeader className="bg-muted/50">
                  <TableRow className="h-10 hover:bg-transparent">
                    <TableHead className="w-[20%] text-center">设备编码</TableHead>
                    <TableHead className="w-[14%] text-center">协议</TableHead>
                    <TableHead className="w-[30%] text-center">端点</TableHead>
                    <TableHead className="w-[18%] text-center">采集周期</TableHead>
                    <TableHead className="w-[18%] text-center">状态</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {devices.slice(0, 3).map((device) => (
                    <TableRow key={device.device_code} className="h-12">
                      <TableCell className="max-w-0 truncate text-center font-mono text-xs">{device.device_code}</TableCell>
                      <TableCell className="max-w-0 truncate text-center uppercase">{device.protocol}</TableCell>
                      <TableCell className="max-w-0 truncate text-center font-mono text-xs">{device.endpoint}</TableCell>
                      <TableCell className="text-center tabular-nums">{device.scan_rate_ms} ms</TableCell>
                      <TableCell className="text-center">
                        <Badge variant={device.status === 1 ? "outline" : "secondary"}>
                          {device.status === 1 ? "在线" : device.status === null ? "未上报" : "离线"}
                        </Badge>
                      </TableCell>
                    </TableRow>
                  ))}
                  {snapshot.data && devices.length === 0 && (
                    <TableRow><TableCell colSpan={5} className="h-24 text-center text-muted-foreground">暂无已分配设备</TableCell></TableRow>
                  )}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
        <TrendChart
          fillHeight
          title="本地采集入队趋势"
          unit={rates.data.collector.unit}
          state={rateTrendState(rates.data.collector, rates.loading, rates.error)}
          data={rates.data.collector.series}
          current={rates.data.collector.current}
        />
      </div>
    </ModulePage>
  );
}

export function EdgeUploaderPage() {
  const snapshot = useLocalSnapshot<EdgeUploaderSnapshot>(requestLocalUploader);
  const rates = useEdgeRates();
  const outbox = snapshot.data?.outbox;
  const queueRows = [
    { label: "等待上传", value: outbox?.counts.pending ?? 0, detail: "已落盘，等待下一批次" },
    { label: "正在上传", value: outbox?.counts.inflight ?? 0, detail: "当前批次正在等待中心确认" },
    { label: "等待重试", value: outbox?.counts.retry_wait ?? 0, detail: "保持原序号等待重试" },
    { label: "永久失败", value: outbox?.counts.dead ?? 0, detail: "需要现场排查" },
  ];

  return (
    <ModulePage
      name="uploader"
      metrics={
        <>
          <MetricCard label="离线积压" value={String(outbox?.backlog_count ?? 0)} warning={(outbox?.backlog_count ?? 0) > 0} />
          <MetricCard label="中心确认上行" value={rateCardValue(rates.data.uploader)} />
          <MetricCard label="Outbox 文件" value={formatBytes(outbox?.database_bytes ?? 0)} />
          <MetricCard label="磁盘使用" value={`${(outbox?.disk_used_percent ?? 0).toFixed(1)}%`} warning={(outbox?.disk_used_percent ?? 0) >= 85} />
        </>
      }
    >
      <div className="grid h-full min-h-0 grid-rows-[300px_minmax(0,1fr)] gap-4 overflow-hidden">
        <Card className="flex h-full min-h-0 flex-col gap-0 border border-border py-0">
          <CardHeader className="px-4 py-3">
            <CardTitle>上行同步队列</CardTitle>
          </CardHeader>
          <CardContent className="min-h-0 flex-1 space-y-2 overflow-hidden px-4 pb-4">
            {snapshot.error && <p className="text-xs text-destructive">{snapshot.error}</p>}
            <div className="overflow-hidden rounded-lg border">
              <Table className="w-full table-fixed">
                <TableHeader className="bg-muted/50">
                  <TableRow className="h-10 hover:bg-transparent">
                    <TableHead className="w-[28%] text-center">队列状态</TableHead>
                    <TableHead className="w-[20%] text-center">数量</TableHead>
                    <TableHead className="w-[52%] text-center">说明</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {queueRows.map((row) => (
                    <TableRow key={row.label} className="h-10">
                      <TableCell className="text-center font-medium">{row.label}</TableCell>
                      <TableCell className="text-center font-mono tabular-nums">{row.value}</TableCell>
                      <TableCell className="text-center text-xs leading-4 text-muted-foreground">{row.detail}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
            <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
              <Badge variant={outbox?.watermark === "critical" || outbox?.watermark === "overflow" ? "destructive" : "outline"}>水位 {outbox?.watermark ?? "normal"}</Badge>
              <span>本地序号 {outbox?.last_seq ?? 0}</span>
              <span>累计确认 {outbox?.sent_records_total ?? 0}</span>
              <span>累计丢弃 {outbox?.dropped_records ?? 0}</span>
            </div>
          </CardContent>
        </Card>
        <TrendChart
          fillHeight
          title="中心确认上行趋势"
          unit={rates.data.uploader.unit}
          state={rateTrendState(rates.data.uploader, rates.loading, rates.error)}
          data={rates.data.uploader.series}
          current={rates.data.uploader.current}
        />
      </div>
    </ModulePage>
  );
}