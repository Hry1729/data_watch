import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Ban,
  Check,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Clipboard,
  Cpu,
  HardDriveDownload,
  KeyRound,
  ListChecks,
  Network,
  RefreshCcw,
  Server,
  Settings2,
  TriangleAlert,
  type LucideIcon,
} from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { useAuth } from "@/contexts/AuthContext";
import { backendClient } from "@/lib/backend-client";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { useDevicePicker } from "@/hooks/useDevicePicker";
import { DeviceCatalogPagination } from "@/components/DeviceCatalogPagination";
import type { AssignmentOccupancy, DistributedNode, RegistrationCodeResult } from "@/types/distributed";

function errorMessage(reason: unknown): string {
  if (reason && typeof reason === "object" && "message" in reason) {
    return String((reason as { message?: string }).message || "请求失败");
  }
  return reason instanceof Error ? reason.message : "请求失败";
}

function isOnline(value: string | null): boolean {
  return Boolean(value && Date.now() - new Date(value).getTime() < 30_000);
}

function formatTime(value: string | null): string {
  return value ? new Date(value).toLocaleString() : "从未";
}

function formatClock(value: string): string {
  return new Date(value).toLocaleTimeString("zh-CN", { hour12: false });
}

function formatAgo(value: string): string {
  const seconds = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 1000));
  if (seconds < 60) return `${seconds} 秒前`;
  if (seconds < 3600) return `${Math.floor(seconds / 60)} 分钟前`;
  if (seconds < 86_400) return `${Math.floor(seconds / 3600)} 小时前`;
  return `${Math.floor(seconds / 86_400)} 天前`;
}

const watermarkRank: Record<DistributedNode["outboxWatermark"], number> = { normal: 0, warning: 1, critical: 2, overflow: 3 };
// 概念稿状态 Badge：浅底 + 同色 28% 描边
const tagCls = "h-5 rounded-md px-2 text-xs font-medium";
const tagTone = {
  brand: "border-brand/30 bg-brand-soft text-brand",
  success: "border-success/30 bg-success-soft text-success",
  warning: "border-warning/30 bg-warning-soft text-warning",
  destructive: "border-destructive/30 bg-destructive-soft text-destructive",
  idle: "border-border bg-muted text-muted-foreground",
};

function processChip(label: string, value: string) {
  const tone = value === "running" ? "text-success" : value === "unknown" ? "text-warning" : "text-muted-foreground";
  const text = value === "running" ? "运行" : value === "unknown" ? "未知" : "停止";
  return (
    <span key={label} className={`inline-flex h-6 items-center gap-1 px-1.5 text-[11px] font-medium whitespace-nowrap [&+&]:shadow-[inset_1px_0_0_var(--card)] ${tone}`}>
      <i className={`size-1.5 rounded-full ${value === "running" || value === "unknown" ? "bg-current" : "shadow-[inset_0_0_0_1.5px_currentColor]"}`} />
      {label} {text}
    </span>
  );
}

function approvalBadge(status: DistributedNode["status"]) {
  if (status === "approved") return <Badge variant="outline" className={`${tagCls} ${tagTone.brand}`}>已批准</Badge>;
  if (status === "revoked") return <Badge variant="outline" className={`${tagCls} ${tagTone.destructive}`}>已吊销</Badge>;
  return <Badge variant="outline" className={`${tagCls} ${tagTone.warning}`}>待审批</Badge>;
}

function onlineBadge(online: boolean) {
  return online
    ? <Badge variant="outline" className={`${tagCls} ${tagTone.success}`}>在线</Badge>
    : <Badge variant="outline" className={`${tagCls} ${tagTone.idle}`}>离线</Badge>;
}

function KpiCard({ icon: Icon, label, value, unit, detail, badge }: { icon: LucideIcon; label: string; value: string; unit?: string; detail: string; badge?: string }) {
  return (
    <Card className="gap-0.5 p-4">
      <div className="flex items-center gap-2 text-[13px] text-muted-foreground">
        <Icon className="size-[15px] shrink-0" />
        <span className="truncate">{label}</span>
        {badge && <span className="ml-auto rounded-full bg-warning-soft px-1.5 py-px text-xs font-medium text-warning">{badge}</span>}
      </div>
      <p className="mt-2 truncate text-[28px] leading-[1.15] font-semibold tracking-[-0.03em] tabular-nums">
        {value}
        {unit && <small className="ml-0.5 text-sm font-medium tracking-normal text-muted-foreground">{unit}</small>}
      </p>
      <p className="truncate text-xs text-muted-foreground" title={detail}>{detail}</p>
    </Card>
  );
}

export default function DistributedNodesPage() {
  const { auth } = useAuth();
  const token = auth?.backendToken;
  const [nodes, setNodes] = useState<DistributedNode[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [registrationOpen, setRegistrationOpen] = useState(false);
  const [nodeName, setNodeName] = useState("");
  const [expiresMinutes, setExpiresMinutes] = useState("30");
  const [registration, setRegistration] = useState<RegistrationCodeResult | null>(null);
  const [assignmentNode, setAssignmentNode] = useState<DistributedNode | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [assignmentOwners, setAssignmentOwners] = useState<Map<number, AssignmentOccupancy>>(new Map());
  const [assignmentType, setAssignmentType] = useState("all");
  const [assignmentSearch, setAssignmentSearch] = useState("");
  const [assignmentLoading, setAssignmentLoading] = useState(false);
  const [settingsNode, setSettingsNode] = useState<DistributedNode | null>(null);
  const [readTimeoutMs, setReadTimeoutMs] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const nodesRequestRef = useRef<AbortController | null>(null);
  const assignmentRequestRef = useRef<AbortController | null>(null);
  const devicePicker = useDevicePicker(backendClient, assignmentSearch, assignmentType, Boolean(assignmentNode) && !assignmentLoading);
  const loadDeviceTypes = useCallback((backendToken: string, signal: AbortSignal) => backendClient.deviceTypes(backendToken, {}, signal), []);
  const deviceTypes = useBackendQuery(loadDeviceTypes, { enabled: Boolean(assignmentNode) });

  const loadNodes = useCallback(async (force = false) => {
    if (!token) return;
    if (nodesRequestRef.current && !force) return;
    nodesRequestRef.current?.abort();
    const controller = new AbortController();
    nodesRequestRef.current = controller;
    try {
      const nextNodes = await backendClient.distributedNodes(token, controller.signal);
      if (!controller.signal.aborted) setNodes(nextNodes);
    } catch (reason) {
      if (!controller.signal.aborted) toast.error(errorMessage(reason));
    } finally {
      if (nodesRequestRef.current === controller) nodesRequestRef.current = null;
      if (!controller.signal.aborted) setLoading(false);
    }
  }, [token]);

  useEffect(() => {
    void loadNodes();
    const timer = window.setInterval(() => void loadNodes(), 10_000);
    return () => {
      window.clearInterval(timer);
      nodesRequestRef.current?.abort();
      nodesRequestRef.current = null;
      assignmentRequestRef.current?.abort();
    };
  }, [loadNodes]);

  const totals = useMemo(() => {
    const online = nodes.filter((node) => isOnline(node.lastSeenAt)).length;
    const pending = nodes.filter((node) => node.status === "pending").length;
    const clockAnomalies = nodes.filter((node) => node.clockAnomaly).length;
    const configPending = nodes.filter((node) => node.appliedRevision !== node.configVersion).length;
    return {
      online,
      backlog: nodes.reduce((sum, node) => sum + node.outboxBacklog, 0),
      offline: nodes.length - online,
      pending,
      clockAnomalies,
      configPending,
      attention: nodes.filter((node) => node.clockAnomaly || node.appliedRevision !== node.configVersion || node.status === "pending").length,
      assignedDevices: nodes.reduce((sum, node) => sum + node.assignmentCount, 0),
      assignedNodes: nodes.filter((node) => node.assignmentCount > 0).length,
      worstWatermark: nodes.reduce<DistributedNode["outboxWatermark"]>((worst, node) => (watermarkRank[node.outboxWatermark] > watermarkRank[worst] ? node.outboxWatermark : worst), "normal"),
      topBacklogNode: nodes.reduce<DistributedNode | null>((top, node) => (!top || node.outboxBacklog > top.outboxBacklog ? node : top), null),
    };
  }, [nodes]);
  const totalPages = Math.max(1, Math.ceil(nodes.length / pageSize));
  const pagedNodes = useMemo(
    () => nodes.slice((page - 1) * pageSize, page * pageSize),
    [nodes, page, pageSize],
  );
  const assignmentTypes = deviceTypes.data ?? [];
  const visibleAssignmentDevices = useMemo(
    () => (devicePicker.data?.list ?? []).filter((device) => device.deviceStatus === "enabled" || selectedIds.has(device.id)),
    [devicePicker.data, selectedIds],
  );

  useEffect(() => {
    setPage((current) => Math.min(current, totalPages));
  }, [totalPages]);

  async function createRegistrationCode() {
    if (!token || !nodeName.trim()) return;
    setBusy(true);
    try {
      const result = await backendClient.createNodeRegistrationCode(
        token,
        nodeName.trim(),
        Number(expiresMinutes),
      );
      setRegistration(result);
    } catch (reason) {
      toast.error(errorMessage(reason));
    } finally {
      setBusy(false);
    }
  }

  async function updateStatus(node: DistributedNode, status: "approved" | "revoked") {
    if (!token) return;
    setBusy(true);
    try {
      await backendClient.updateDistributedNodeStatus(token, node.nodeUuid, status);
      toast.success(status === "approved" ? "节点已批准" : "节点已吊销");
      await loadNodes(true);
    } catch (reason) {
      toast.error(errorMessage(reason));
    } finally {
      setBusy(false);
    }
  }

  function openSettings(node: DistributedNode) {
    setSettingsNode(node);
    setReadTimeoutMs(node.readTimeoutMs === null ? "" : String(node.readTimeoutMs));
  }

  async function saveSettings() {
    if (!token || !settingsNode) return;
    const timeout = readTimeoutMs.trim() === "" ? null : Number(readTimeoutMs);
    if (timeout !== null && (!Number.isInteger(timeout) || timeout <= 0)) {
      toast.error("读取超时必须是正整数毫秒值");
      return;
    }
    setBusy(true);
    try {
      await backendClient.updateDistributedNodeSettings(
        token,
        settingsNode.nodeUuid,
        timeout,
      );
      toast.success(timeout === null ? "已恢复协议驱动默认超时" : "节点读取超时已更新");
      setSettingsNode(null);
      await loadNodes(true);
    } catch (reason) {
      toast.error(errorMessage(reason));
    } finally {
      setBusy(false);
    }
  }

  async function syncDeviceStatuses() {
    if (!token) return;
    setBusy(true);
    try {
      const result = await backendClient.syncDistributedNodeDeviceStatuses(token);
      toast.success(`已通知 ${result.updatedNodeCount} 个边缘节点同步设备状态`);
      await loadNodes(true);
    } catch (reason) {
      toast.error(errorMessage(reason));
    } finally {
      setBusy(false);
    }
  }

  async function openAssignments(node: DistributedNode) {
    if (!token) return;
    assignmentRequestRef.current?.abort();
    const controller = new AbortController();
    assignmentRequestRef.current = controller;
    setAssignmentNode(node);
    setAssignmentType("all");
    setAssignmentSearch("");
    setAssignmentLoading(true);
    try {
      const [assignments, occupancy] = await Promise.all([
        backendClient.distributedNodeAssignments(token, node.nodeUuid, controller.signal),
        backendClient.distributedAssignmentOccupancy(token, controller.signal),
      ]);
      if (controller.signal.aborted) return;
      const assignedIds = new Set(assignments.map((item) => item.deviceId));
      setAssignmentOwners(new Map(occupancy.map((item) => [item.deviceId, item])));
      setSelectedIds(assignedIds);
    } catch (reason) {
      if (!controller.signal.aborted) {
        toast.error(errorMessage(reason));
        setAssignmentNode(null);
      }
    } finally {
      if (!controller.signal.aborted) setAssignmentLoading(false);
    }
  }

  async function saveAssignments() {
    if (!token || !assignmentNode) return;
    setBusy(true);
    try {
      const result = await backendClient.assignDistributedNodeDevices(
        token,
        assignmentNode.nodeUuid,
        [...selectedIds],
      );
      const scheduled = result.migratedDeviceIds.length + result.removedDeviceIds.length;
      toast.success(scheduled > 0 ? "已创建 " + scheduled + " 个切换计划" : "设备分配已保存");
      setAssignmentNode(null);
      await loadNodes(true);
    } catch (reason) {
      toast.error(errorMessage(reason));
    } finally {
      setBusy(false);
    }
  }

  return (
    <TooltipProvider delayDuration={200}>
    <div className="flex h-full min-h-0 w-full flex-col gap-4">
      <div className="grid shrink-0 grid-cols-4 gap-4">
        <KpiCard
          icon={Network}
          label="在线节点"
          value={String(totals.online)}
          unit={`/ ${nodes.length} 台`}
          detail={`${totals.offline} 台离线 · ${totals.pending} 台待审批`}
        />
        <KpiCard
          icon={HardDriveDownload}
          label="上行积压"
          value={totals.backlog.toLocaleString()}
          badge={totals.worstWatermark !== "normal" ? `水位 ${totals.worstWatermark}` : undefined}
          detail={totals.topBacklogNode && totals.topBacklogNode.outboxBacklog > 0 ? `条 · 主要来自 ${totals.topBacklogNode.nodeName}` : "条 · 暂无积压"}
        />
        <KpiCard
          icon={Cpu}
          label="已分配设备"
          value={String(totals.assignedDevices)}
          unit="台"
          detail={`分布在 ${totals.assignedNodes} 个边缘节点`}
        />
        <KpiCard
          icon={TriangleAlert}
          label="需关注"
          value={String(totals.attention)}
          detail={`时钟异常 ${totals.clockAnomalies} · 配置未应用 ${totals.configPending} · 待审批 ${totals.pending}`}
        />
      </div>
      <Card className="flex max-h-full min-h-0 flex-col gap-0 py-0">
        <div className="flex shrink-0 items-center gap-3 border-b px-5 pt-5 pb-3.5">
          <div className="min-w-0">
            <p className="text-sm leading-none font-semibold">节点列表</p>
            <p className="mt-1.5 truncate text-[13px] text-muted-foreground">{nodes.length} 台 / {totals.online} 台在线 / {totals.backlog.toLocaleString()} 条积压 · 30 秒内有心跳视为在线</p>
          </div>
          <div className="ml-auto flex items-center gap-2">
            <Tooltip>
              <TooltipTrigger asChild>
                <Button variant="outline" size="icon" className="size-8 bg-background" aria-label="同步设备启停状态" onClick={() => void syncDeviceStatuses()} disabled={busy}>
                  <RefreshCcw className="size-3.5" />
                </Button>
              </TooltipTrigger>
              <TooltipContent>同步设备启停状态</TooltipContent>
            </Tooltip>
            <Button className="h-8 gap-1.5 bg-brand px-3 text-[13px] text-brand-foreground hover:bg-brand/90" onClick={() => { setRegistration(null); setRegistrationOpen(true); }}>
              <KeyRound className="size-3.5" />
              生成注册码
            </Button>
          </div>
        </div>
        <div className="flex min-h-0 flex-1 flex-col px-5 pt-4 pb-5">
          <div className="min-h-0 flex-1 overflow-auto">
          {loading ? (
            <div className="space-y-2"><Skeleton className="h-10 w-full" /><Skeleton className="h-10 w-full" /></div>
          ) : (
            <div className="overflow-hidden rounded-[10px] border">
              <Table className="w-full table-fixed text-[13px]">
                <colgroup><col className="w-[20%]" /><col className="w-[14%]" /><col className="w-[20%]" /><col className="w-[8%]" /><col className="w-[10%]" /><col className="w-[6%]" /><col className="w-[11%]" /><col className="w-[11%]" /></colgroup>
                <TableHeader className="bg-[color-mix(in_oklab,var(--muted)_55%,var(--card))]">
                  <TableRow className="hover:bg-transparent">
                    {["节点", "状态", "进程", "配置版本", "积压", "设备", "最近心跳", "操作"].map((label, index) => (
                      <TableHead key={label} className={`h-10 px-3 text-[13px] font-medium text-muted-foreground ${index === 0 ? "text-left" : "text-center"}`}>{label}</TableHead>
                    ))}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {pagedNodes.map((node) => {
                    const online = isOnline(node.lastSeenAt);
                    const lag = node.configVersion !== node.appliedRevision;
                    const capTone = node.outboxWatermark === "warning" ? "text-warning" : node.outboxWatermark === "normal" ? "text-muted-foreground" : "text-destructive";
                    const hbTone = !node.lastSeenAt ? "text-muted-foreground" : online ? "text-success" : "text-destructive";
                    return (
                    <TableRow key={node.nodeUuid}>
                      <TableCell className="h-[68px] max-w-0 overflow-hidden px-3 py-0">
                        <div className="flex min-w-0 items-center gap-2.5">
                          <span className={`grid size-[34px] shrink-0 place-items-center rounded-[9px] ${node.status === "pending" ? "bg-warning-soft text-warning" : online ? "bg-brand-soft text-brand" : "bg-muted text-muted-foreground"}`}><Server className="size-4" /></span>
                          <div className="min-w-0">
                            <div className="truncate font-medium">{node.nodeName}</div>
                            <div className="truncate font-mono text-[11px] text-muted-foreground">{node.nodeUuid}</div>
                          </div>
                        </div>
                      </TableCell>
                      <TableCell className="max-w-0 overflow-hidden px-3 py-0 text-center whitespace-normal">
                        <div className="flex flex-wrap justify-center gap-1">
                          {approvalBadge(node.status)}
                          {onlineBadge(online)}
                          {node.clockAnomaly && <Badge variant="outline" className={`${tagCls} ${tagTone.destructive}`}>时钟异常</Badge>}
                        </div>
                      </TableCell>
                      <TableCell className="max-w-0 overflow-hidden px-3 py-0 text-center">
                        <div className="inline-flex overflow-hidden rounded-lg bg-[color-mix(in_oklab,var(--muted)_55%,transparent)]">{processChip("C", node.collectorStatus)}{processChip("S", node.storageStatus)}{processChip("U", node.uploaderStatus)}</div>
                      </TableCell>
                      <TableCell className="px-3 py-0 text-center tabular-nums"><span className={lag ? "text-warning" : ""} title={lag ? "已应用版本落后于配置版本" : undefined}>{node.appliedRevision} / {node.configVersion}</span></TableCell>
                      <TableCell className="px-3 py-0 text-center">
                        <div className="flex flex-col items-center gap-0.5">
                          <span className="font-mono text-[13px] font-semibold">{node.outboxBacklog.toLocaleString()}</span>
                          <span className={`text-[11px] tabular-nums ${capTone}`} title="Outbox 磁盘占用">磁盘 {node.outboxDiskUsedPercent.toFixed(1)}%</span>
                        </div>
                      </TableCell>
                      <TableCell className="px-3 py-0 text-center font-medium tabular-nums">{node.assignmentCount}</TableCell>
                      <TableCell className="max-w-0 overflow-hidden px-3 py-0 text-center">
                        <div className="flex flex-col items-center gap-0.5" title={formatTime(node.lastSeenAt)}>
                          <span className={`inline-flex items-center gap-1.5 text-[12.5px] font-medium ${hbTone}`}><i className="size-1.5 rounded-full bg-current" />{node.lastSeenAt ? formatAgo(node.lastSeenAt) : "从未"}</span>
                          {node.lastSeenAt && <span className="text-[11px] whitespace-nowrap text-muted-foreground tabular-nums">{formatClock(node.lastSeenAt)}</span>}
                        </div>
                      </TableCell>
                      <TableCell className="px-3 py-0"><div className="flex justify-center gap-0.5">
                        {node.status !== "approved" && <Tooltip><TooltipTrigger asChild><Button variant="ghost" size="icon" className="size-[30px] text-muted-foreground hover:text-foreground [&_svg:not([class*='size-'])]:size-4 hover:text-success" onClick={() => void updateStatus(node, "approved")} disabled={busy}><Check /></Button></TooltipTrigger><TooltipContent>批准节点</TooltipContent></Tooltip>}
                        {node.status !== "revoked" && <Tooltip><TooltipTrigger asChild><Button variant="ghost" size="icon" className="size-[30px] text-muted-foreground hover:text-foreground [&_svg:not([class*='size-'])]:size-4 hover:text-destructive" onClick={() => void updateStatus(node, "revoked")} disabled={busy}><Ban /></Button></TooltipTrigger><TooltipContent>吊销节点</TooltipContent></Tooltip>}
                        <Tooltip><TooltipTrigger asChild><Button variant="ghost" size="icon" className="size-[30px] text-muted-foreground hover:text-foreground [&_svg:not([class*='size-'])]:size-4" onClick={() => openSettings(node)} disabled={node.status === "revoked"}><Settings2 /></Button></TooltipTrigger><TooltipContent>节点设置</TooltipContent></Tooltip>
                        <Tooltip><TooltipTrigger asChild><Button variant="ghost" size="icon" className="size-[30px] text-muted-foreground hover:text-foreground [&_svg:not([class*='size-'])]:size-4" onClick={() => void openAssignments(node)} disabled={node.status === "revoked"}><ListChecks /></Button></TooltipTrigger><TooltipContent>分配设备</TooltipContent></Tooltip>
                      </div></TableCell>
                    </TableRow>
                    );
                  })}
                  {nodes.length === 0 && <TableRow><TableCell colSpan={8} className="h-32 text-center text-muted-foreground">暂无边缘节点</TableCell></TableRow>}
                </TableBody>
              </Table>
            </div>
          )}
          </div>
          {!loading && (
            <div className="flex shrink-0 items-center gap-3 px-1 pt-3 text-[13px]">
              <span className="text-muted-foreground">共 {nodes.length} 台节点</span>
              <span className="flex-1" />
              <span className="text-muted-foreground">每页行数</span>
              <Select value={String(pageSize)} onValueChange={(value) => { setPageSize(Number(value)); setPage(1); }}>
                <SelectTrigger className="!h-[30px] w-[72px] bg-background text-[13px]" aria-label="每页行数"><SelectValue /></SelectTrigger>
                <SelectContent align="end">
                  {[5, 10, 20].map((value) => <SelectItem key={value} value={String(value)}>{value}</SelectItem>)}
                </SelectContent>
              </Select>
              <span className="min-w-24 text-center font-medium tabular-nums">第 {page} / {totalPages} 页</span>
              <Button variant="outline" size="icon" className="size-8 bg-background" disabled={page <= 1} onClick={() => setPage(1)} aria-label="第一页"><ChevronsLeft className="size-4" /></Button>
              <Button variant="outline" size="icon" className="size-8 bg-background" disabled={page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))} aria-label="上一页"><ChevronLeft className="size-4" /></Button>
              <Button variant="outline" size="icon" className="size-8 bg-background" disabled={page >= totalPages} onClick={() => setPage((current) => Math.min(totalPages, current + 1))} aria-label="下一页"><ChevronRight className="size-4" /></Button>
              <Button variant="outline" size="icon" className="size-8 bg-background" disabled={page >= totalPages} onClick={() => setPage(totalPages)} aria-label="最后一页"><ChevronsRight className="size-4" /></Button>
            </div>
          )}
        </div>
      </Card>

      <Dialog open={registrationOpen} onOpenChange={setRegistrationOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>生成边缘注册码</DialogTitle><DialogDescription>注册码仅可由一个边缘节点使用。</DialogDescription></DialogHeader>
          {registration ? <div className="space-y-3"><Label>注册码</Label><div className="flex gap-2"><Input readOnly className="font-mono" value={registration.registrationCode} /><Button variant="outline" size="icon" onClick={() => { void navigator.clipboard.writeText(registration.registrationCode); toast.success("注册码已复制"); }}><Clipboard className="size-4" /></Button></div><p className="text-xs text-muted-foreground">有效期至 {formatTime(registration.expiresAt)}</p></div> : <div className="space-y-3"><div className="space-y-1.5"><Label htmlFor="node-name">节点名称</Label><Input id="node-name" value={nodeName} onChange={(event) => setNodeName(event.target.value)} /></div><div className="space-y-1.5"><Label htmlFor="expiry">有效分钟数</Label><Input id="expiry" type="number" min={1} max={1440} value={expiresMinutes} onChange={(event) => setExpiresMinutes(event.target.value)} /></div></div>}
          <DialogFooter>{!registration && <Button onClick={() => void createRegistrationCode()} disabled={busy || !nodeName.trim()}>生成</Button>}</DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(settingsNode)} onOpenChange={(open) => { if (!open) setSettingsNode(null); }}>
        <DialogContent>
          <DialogHeader><DialogTitle>节点设置</DialogTitle><DialogDescription>{settingsNode?.nodeName}</DialogDescription></DialogHeader>
          <div className="space-y-1.5">
            <Label htmlFor="read-timeout">读取超时（毫秒）</Label>
            <Input id="read-timeout" type="number" min={1} placeholder="使用协议驱动默认值" value={readTimeoutMs} onChange={(event) => setReadTimeoutMs(event.target.value)} />
            <p className="text-xs text-muted-foreground">允许大于点位采集周期；同设备仍保持单请求在途。</p>
          </div>
          <DialogFooter><Button onClick={() => void saveSettings()} disabled={busy}>保存</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(assignmentNode)} onOpenChange={(open) => { if (!open) { assignmentRequestRef.current?.abort(); setAssignmentNode(null); } }}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <div className="flex items-center justify-between gap-4">
              <DialogTitle className="shrink-0">分配设备</DialogTitle>
              <Tabs value={assignmentType} onValueChange={setAssignmentType} className="min-w-0 max-w-[70%]">
                <div className="overflow-x-auto">
                  <TabsList className="h-8 w-max">
                    <TabsTrigger value="all">汇总</TabsTrigger>
                    {assignmentTypes.map((type) => <TabsTrigger key={type} value={type}>{type}</TabsTrigger>)}
                  </TabsList>
                </div>
              </Tabs>
            </div>
            <DialogDescription>{assignmentNode?.nodeName}</DialogDescription>
          </DialogHeader>
          <Input aria-label="搜索待分配设备" placeholder="搜索设备名称或编号" value={assignmentSearch} onChange={(event) => setAssignmentSearch(event.target.value)} />
          <div className="max-h-80 overflow-auto border border-border">
            {assignmentLoading || devicePicker.loading ? (
              <div className="p-4"><Skeleton className="h-10 w-full" /></div>
            ) : (
              <Table>
                <TableHeader className="sticky top-0 bg-popover">
                  <TableRow>
                    <TableHead className="w-10" />
                    <TableHead>设备编码</TableHead>
                    <TableHead>设备名称</TableHead>
                    <TableHead>驱动</TableHead>
                    <TableHead className="w-20">状态</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {visibleAssignmentDevices.length ? visibleAssignmentDevices.map((device) => {
                    const owner = assignmentOwners.get(device.id);
                    const assignedElsewhere = Boolean(owner && owner.nodeUuid !== assignmentNode?.nodeUuid);
                    const disabled = device.deviceStatus === "disabled";
                    const locked = disabled || assignedElsewhere;
                    return (
                      <TableRow
                        key={device.id}
                        className={locked ? "bg-muted/40 text-muted-foreground" : undefined}
                      >
                        <TableCell>
                          <Checkbox
                            aria-label={`分配设备 ${device.deviceName}`}
                            checked={selectedIds.has(device.id)}
                            disabled={locked}
                            onCheckedChange={(checked) => setSelectedIds((current) => {
                              const next = new Set(current);
                              if (checked) next.add(device.id);
                              else next.delete(device.id);
                              return next;
                            })}
                          />
                        </TableCell>
                        <TableCell className="font-mono">{device.deviceCode}</TableCell>
                        <TableCell>{device.deviceName}</TableCell>
                        <TableCell>{device.driverName}</TableCell>
                        <TableCell>
                          <Badge
                            variant="outline"
                            className={assignedElsewhere ? "border-primary/30 bg-primary/5 text-primary" : disabled ? "border-border bg-muted text-muted-foreground" : "border-success/30 bg-success-soft text-success"}
                            title={assignedElsewhere ? `已分配给 ${owner?.nodeName || owner?.nodeUuid}` : undefined}
                          >
                            {assignedElsewhere ? "已分配" : disabled ? "已停用" : "启用"}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    );
                  }) : (
                    <TableRow>
                      <TableCell colSpan={5} className="h-24 text-center text-muted-foreground">
                        {devicePicker.error ?? "暂无该分类设备"}
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            )}
          </div>
          <DeviceCatalogPagination total={devicePicker.data?.total ?? 0} page={devicePicker.page} pageSize={devicePicker.pageSize} loading={devicePicker.loading || assignmentLoading} onPageChange={devicePicker.setPage} />
          <DialogFooter><Button onClick={() => void saveAssignments()} disabled={busy || assignmentLoading}>保存分配</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
    </TooltipProvider>
  );
}
