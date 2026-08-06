import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Activity,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Clock3,
  Database,
  HardDrive,
  KeyRound,
  UploadCloud,
} from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  agentClient,
  type EdgeLocalDevices,
  type EdgeOutboxStatus,
  type EdgeProcessStatus,
} from "@/lib/agent-client";

const PAGE_SIZE = 6;

type EdgeSnapshot = {
  devices: EdgeLocalDevices;
  collector: EdgeProcessStatus;
  uploader: EdgeProcessStatus & { outbox: EdgeOutboxStatus };
};

function processLabel(status: string): string {
  if (status === "running") return "运行中";
  if (status === "stopped") return "已停止";
  if (status === "crashed") return "异常退出";
  return "未知";
}

function formatBytes(value: number): string {
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / 1024 / 1024).toFixed(1)} MB`;
}

function StatCard({
  label,
  value,
  icon: Icon,
  detail,
}: {
  label: string;
  value: string;
  icon: typeof Activity;
  detail: string;
}) {
  return (
    <Card size="sm" className="min-w-0 rounded-lg border border-border bg-card shadow-xs">
      <CardContent className="flex min-h-24 items-center gap-3 py-2">
        <div className="flex size-9 shrink-0 items-center justify-center rounded-md bg-muted text-foreground">
          <Icon className="size-4" aria-hidden="true" />
        </div>
        <div className="min-w-0">
          <p className="text-xs text-muted-foreground">{label}</p>
          <p className="mt-1 truncate text-xl font-semibold text-foreground">{value}</p>
          <p className="mt-0.5 truncate text-xs text-muted-foreground">{detail}</p>
        </div>
      </CardContent>
    </Card>
  );
}

export default function EdgeOperationsPage() {
  const [snapshot, setSnapshot] = useState<EdgeSnapshot | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [rotatingKey, setRotatingKey] = useState(false);
  const [page, setPage] = useState(1);
  const mounted = useRef(true);

  const refresh = useCallback(async () => {
    try {
      const [devices, collector, uploader] = await Promise.all([
        agentClient.getLocalDevices(),
        agentClient.getLocalCollector(),
        agentClient.getLocalUploader(),
      ]);
      if (!mounted.current) return;
      setSnapshot({ devices, collector, uploader });
      setError(null);
    } catch {
      if (mounted.current) setError("本机状态读取失败");
    }
  }, []);

  useEffect(() => {
    mounted.current = true;
    void refresh();
    let timer: ReturnType<typeof setTimeout>;
    const poll = async () => {
      await refresh();
      if (mounted.current) timer = setTimeout(poll, 2000);
    };
    timer = setTimeout(poll, 2000);
    return () => {
      mounted.current = false;
      clearTimeout(timer);
    };
  }, [refresh]);

  async function rotateKey() {
    if (!window.confirm("确认轮换本节点身份密钥？Uploader 将自动重启。")) return;
    setRotatingKey(true);
    try {
      const result = await agentClient.rotateEdgeIdentity();
      if (result.warning) toast.warning(result.warning);
      else toast.success(`身份密钥已轮换至版本 ${result.keyVersion}`);
      await refresh();
    } catch {
      toast.error("身份密钥轮换失败");
    } finally {
      setRotatingKey(false);
    }
  }

  const outbox = snapshot?.uploader.outbox;
  const enabled = snapshot?.devices.devices.filter((device) => device.enabled === 1).length ?? 0;
  const online = snapshot?.devices.devices.filter((device) => device.enabled === 1 && device.status === 1).length ?? 0;
  const total = snapshot?.devices.devices.length ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const pagedDevices = useMemo(
    () => (snapshot?.devices.devices ?? []).slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [snapshot, page],
  );

  useEffect(() => {
    setPage((current) => Math.min(current, totalPages));
  }, [totalPages]);

  return (
    <div className="flex h-full min-h-0 flex-col gap-4 overflow-hidden p-4">
      {error && <p className="text-xs text-destructive">{error}</p>}

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="本机设备" value={`${online} / ${enabled}`} icon={Activity} detail="在线 / 已启用" />
        <StatCard
          label="Collector"
          value={processLabel(snapshot?.collector.status ?? "unknown")}
          icon={Database}
          detail={snapshot?.collector.pid ? `PID ${snapshot.collector.pid}` : "无运行进程"}
        />
        <StatCard
          label="Uploader"
          value={processLabel(snapshot?.uploader.status ?? "unknown")}
          icon={UploadCloud}
          detail={outbox?.upload_interval_ms ? `${outbox.upload_interval_ms} ms 节拍` : "等待有效点位配置"}
        />
        <StatCard
          label="离线积压"
          value={String(outbox?.backlog_count ?? 0)}
          icon={HardDrive}
          detail={`${formatBytes(outbox?.database_bytes ?? 0)} · 磁盘 ${(outbox?.disk_used_percent ?? 0).toFixed(1)}%`}
        />
      </div>

      <Card className="flex min-h-0 flex-1 flex-col rounded-lg">
        <CardHeader className="flex-row items-center justify-between space-y-0">
          <CardTitle className="text-sm font-semibold">本机设备与采集状态</CardTitle>
          <div className="flex items-center gap-3">
            <span className="text-xs text-muted-foreground">
              配置修订 {snapshot?.devices.config?.config_revision ?? "-"}
            </span>
            <Button variant="outline" size="sm" onClick={() => void rotateKey()} disabled={rotatingKey}>
              <KeyRound className="size-4" />
              轮换密钥
            </Button>
          </div>
        </CardHeader>
        <CardContent className="flex min-h-0 flex-1 flex-col gap-3">
          <div className="min-h-0 flex-1 overflow-hidden rounded-lg border">
            <Table className="w-full table-fixed">
              <TableHeader className="bg-muted/50">
                <TableRow className="h-10 hover:bg-transparent">
                  <TableHead className="w-[16%] text-center">设备编码</TableHead>
                  <TableHead className="w-[11%] text-center">类型</TableHead>
                  <TableHead className="w-[11%] text-center">协议</TableHead>
                  <TableHead className="w-[23%] text-center">端点</TableHead>
                  <TableHead className="w-[11%] text-center">采集周期</TableHead>
                  <TableHead className="w-[8%] text-center">版本</TableHead>
                  <TableHead className="w-[10%] text-center">设备状态</TableHead>
                  <TableHead className="w-[10%] text-center">采集状态</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {pagedDevices.map((device) => (
                  <TableRow key={device.device_code} className={device.enabled === 1 ? "h-12" : "h-12 bg-muted/40 text-muted-foreground"}>
                    <TableCell className="max-w-0 truncate text-center font-mono text-xs">{device.device_code}</TableCell>
                    <TableCell className="max-w-0 truncate text-center">{device.device_type}</TableCell>
                    <TableCell className="max-w-0 truncate text-center uppercase">{device.protocol}</TableCell>
                    <TableCell className="max-w-0 truncate text-center font-mono text-xs">{device.endpoint}</TableCell>
                    <TableCell className="text-center tabular-nums">{device.scan_rate_ms} ms</TableCell>
                    <TableCell className="text-center tabular-nums">{device.config_version}</TableCell>
                    <TableCell className="text-center">
                      <Badge variant="outline" className={device.enabled === 1 ? "border-success/30 bg-success-soft text-success" : "border-border bg-muted text-muted-foreground"}>
                        {device.enabled === 1 ? "已启用" : "已停用"}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-center">
                      <Badge variant="outline" className={device.enabled !== 1 ? "border-border bg-muted text-muted-foreground" : device.status === 1 ? "border-success/30 bg-success-soft text-success" : device.status === null ? "border-warning/30 bg-warning-soft text-warning" : "border-destructive/30 bg-destructive/5 text-destructive"}>
                        {device.enabled !== 1 ? "不采集" : device.status === 1 ? "在线" : device.status === null ? "未上报" : "离线"}
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))}
                {!snapshot && !error && (
                  <TableRow><TableCell colSpan={8} className="h-32 text-center text-muted-foreground">正在读取本机状态</TableCell></TableRow>
                )}
                {snapshot && total === 0 && (
                  <TableRow><TableCell colSpan={8} className="h-32 text-center text-muted-foreground">暂无已分配设备</TableCell></TableRow>
                )}
              </TableBody>
            </Table>
          </div>
          {snapshot && (
            <div className="flex items-center justify-between px-1 text-sm">
              <span className="text-muted-foreground">共 {total} 台设备 · 每页 {PAGE_SIZE} 台</span>
              <div className="flex items-center gap-3">
                <span className="min-w-24 text-center font-medium tabular-nums">第 {page} / {totalPages} 页</span>
                <div className="flex items-center gap-2">
                  <Button variant="outline" size="icon" className="size-8" disabled={page <= 1} onClick={() => setPage(1)} aria-label="第一页"><ChevronsLeft className="size-4" /></Button>
                  <Button variant="outline" size="icon" className="size-8" disabled={page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))} aria-label="上一页"><ChevronLeft className="size-4" /></Button>
                  <Button variant="outline" size="icon" className="size-8" disabled={page >= totalPages} onClick={() => setPage((current) => Math.min(totalPages, current + 1))} aria-label="下一页"><ChevronRight className="size-4" /></Button>
                  <Button variant="outline" size="icon" className="size-8" disabled={page >= totalPages} onClick={() => setPage(totalPages)} aria-label="最后一页"><ChevronsRight className="size-4" /></Button>
                </div>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-xs text-muted-foreground">
        <span className="inline-flex items-center gap-1.5"><Clock3 className="size-3.5" />序号 {outbox?.last_seq ?? 0}</span>
        <span>会话 {outbox?.upload_session_id?.slice(0, 8) ?? "-"}</span>
        <span>待上传 {outbox?.counts.pending ?? 0}</span>
        <span>重试 {outbox?.counts.retry_wait ?? 0}</span>
        <span>死信 {outbox?.counts.dead ?? 0}</span>
        <Badge variant={outbox?.watermark === "overflow" || outbox?.watermark === "critical" ? "destructive" : outbox?.watermark === "warning" ? "secondary" : "outline"}>
          磁盘水位 {outbox?.watermark === "overflow" ? "95%+" : outbox?.watermark === "critical" ? "85%+" : outbox?.watermark === "warning" ? "70%+" : "正常"}
        </Badge>
        <span>累计丢弃 {outbox?.dropped_records ?? 0}</span>
      </div>
    </div>
  );
}