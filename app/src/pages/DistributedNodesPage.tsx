import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Ban,
  Check,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Clipboard,
  KeyRound,
  ListChecks,
  RefreshCcw,
  Settings2,
} from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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
import type { DeviceListItem } from "@/types/device-monitor";
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

function processBadge(value: string) {
  if (value === "running") {
    return <Badge variant="outline" className="border-success/30 bg-success-soft text-success">运行中</Badge>;
  }
  if (value === "unknown") {
    return <Badge variant="outline" className="border-warning/30 bg-warning-soft text-warning">未知</Badge>;
  }
  return <Badge variant="outline" className="border-border bg-muted text-muted-foreground">已停止</Badge>;
}

function approvalBadge(status: DistributedNode["status"]) {
  if (status === "approved") {
    return <Badge variant="outline" className="border-primary/30 bg-primary/5 text-primary">已批准</Badge>;
  }
  if (status === "revoked") {
    return <Badge variant="outline" className="border-destructive/30 bg-destructive/5 text-destructive">已吊销</Badge>;
  }
  return <Badge variant="outline" className="border-warning/30 bg-warning-soft text-warning">待审批</Badge>;
}

function onlineBadge(online: boolean) {
  return online
    ? <Badge variant="outline" className="border-success/30 bg-success-soft text-success">在线</Badge>
    : <Badge variant="outline" className="border-border bg-muted text-muted-foreground">离线</Badge>;
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
  const [devices, setDevices] = useState<DeviceListItem[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [assignmentOwners, setAssignmentOwners] = useState<Map<number, AssignmentOccupancy>>(new Map());
  const [assignmentType, setAssignmentType] = useState("all");
  const [assignmentLoading, setAssignmentLoading] = useState(false);
  const [settingsNode, setSettingsNode] = useState<DistributedNode | null>(null);
  const [readTimeoutMs, setReadTimeoutMs] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const loadNodes = useCallback(async () => {
    if (!token) return;
    try {
      setNodes(await backendClient.distributedNodes(token));
    } catch (reason) {
      toast.error(errorMessage(reason));
    } finally {
      setLoading(false);
    }
  }, [token]);

  useEffect(() => {
    void loadNodes();
    const timer = window.setInterval(() => void loadNodes(), 10_000);
    return () => window.clearInterval(timer);
  }, [loadNodes]);

  const totals = useMemo(() => ({
    online: nodes.filter((node) => isOnline(node.lastSeenAt)).length,
    backlog: nodes.reduce((sum, node) => sum + node.outboxBacklog, 0),
  }), [nodes]);
  const totalPages = Math.max(1, Math.ceil(nodes.length / pageSize));
  const pagedNodes = useMemo(
    () => nodes.slice((page - 1) * pageSize, page * pageSize),
    [nodes, page, pageSize],
  );
  const assignmentTypes = useMemo(
    () => Array.from(new Set(devices.map((device) => device.deviceType))),
    [devices],
  );
  const visibleAssignmentDevices = useMemo(
    () => assignmentType === "all"
      ? devices
      : devices.filter((device) => device.deviceType === assignmentType),
    [assignmentType, devices],
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
      await loadNodes();
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
      await loadNodes();
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
      await loadNodes();
    } catch (reason) {
      toast.error(errorMessage(reason));
    } finally {
      setBusy(false);
    }
  }

  async function openAssignments(node: DistributedNode) {
    if (!token) return;
    setAssignmentNode(node);
    setAssignmentType("all");
    setAssignmentLoading(true);
    try {
      const [devicePage, assignments, occupancy] = await Promise.all([
        backendClient.deviceList(token, { pageNo: 1, pageSize: 500 }),
        backendClient.distributedNodeAssignments(token, node.nodeUuid),
        backendClient.distributedAssignmentOccupancy(token),
      ]);
      const assignedIds = new Set(assignments.map((item) => item.deviceId));
      setAssignmentOwners(new Map(occupancy.map((item) => [item.deviceId, item])));
      setDevices(
        devicePage.list.filter(
          (device) => device.deviceStatus === "enabled" || assignedIds.has(device.id),
        ),
      );
      setSelectedIds(assignedIds);
    } catch (reason) {
      toast.error(errorMessage(reason));
      setAssignmentNode(null);
    } finally {
      setAssignmentLoading(false);
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
      await loadNodes();
    } catch (reason) {
      toast.error(errorMessage(reason));
    } finally {
      setBusy(false);
    }
  }

  return (
    <TooltipProvider delayDuration={200}>
    <div className="flex h-full min-h-0 w-full flex-col p-4 md:p-6">
      <Card className="flex min-h-0 flex-1 flex-col border border-border">
        <CardHeader>
          <CardTitle className="flex items-center gap-3">
            边缘节点
            <span className="text-xs font-normal text-muted-foreground">
              {nodes.length} 台 / {totals.online} 台在线 / {totals.backlog} 条积压
            </span>
          </CardTitle>
          <CardAction className="flex items-center gap-2">
            <Tooltip>
              <TooltipTrigger asChild>
                <Button variant="outline" size="icon-sm" onClick={() => void syncDeviceStatuses()} disabled={busy}>
                  <RefreshCcw className="size-4" />
                </Button>
              </TooltipTrigger>
              <TooltipContent>同步设备启停状态</TooltipContent>
            </Tooltip>
            <Button size="sm" onClick={() => { setRegistration(null); setRegistrationOpen(true); }}>
              <KeyRound className="size-4" />
              生成注册码
            </Button>
          </CardAction>
        </CardHeader>
        <CardContent className="min-h-0 flex-1 space-y-3 overflow-auto">
          {loading ? (
            <div className="space-y-2"><Skeleton className="h-10 w-full" /><Skeleton className="h-10 w-full" /></div>
          ) : (
            <div className="overflow-hidden rounded-lg border">
              <Table className="w-full table-fixed">
                <TableHeader className="bg-muted/50">
                  <TableRow className="h-10 hover:bg-transparent">
                    <TableHead className="w-[19%] text-center">节点</TableHead>
                    <TableHead className="w-[14%] text-center">状态</TableHead>
                    <TableHead className="w-[13%] text-center">进程</TableHead>
                    <TableHead className="w-[10%] text-center">配置版本</TableHead>
                    <TableHead className="w-[9%] text-center">积压</TableHead>
                    <TableHead className="w-[7%] text-center">设备</TableHead>
                    <TableHead className="w-[16%] text-center">最近心跳</TableHead>
                    <TableHead className="w-[12%] text-center">操作</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {pagedNodes.map((node) => (
                    <TableRow key={node.nodeUuid} className="h-14">
                      <TableCell className="max-w-0 overflow-hidden text-center">
                        <div className="truncate font-medium">{node.nodeName}</div>
                        <div className="truncate font-mono text-xs text-muted-foreground">{node.nodeUuid}</div>
                      </TableCell>
                      <TableCell className="max-w-0 overflow-hidden text-center">
                        <div className="flex flex-wrap justify-center gap-1">
                          {approvalBadge(node.status)}
                          {onlineBadge(isOnline(node.lastSeenAt))}
                          {node.clockAnomaly && <Badge variant="outline" className="border-destructive/30 bg-destructive/5 text-destructive">时钟异常</Badge>}
                        </div>
                      </TableCell>
                      <TableCell className="max-w-0 overflow-hidden">
                        <div className="flex flex-wrap justify-center gap-1">{processBadge(node.collectorStatus)}{processBadge(node.uploaderStatus)}</div>
                      </TableCell>
                      <TableCell className="text-center"><span className={node.configVersion === node.appliedRevision ? "" : "text-amber-600"}>{node.appliedRevision} / {node.configVersion}</span></TableCell>
                      <TableCell className="text-center"><div className="font-mono">{node.outboxBacklog}</div><Badge variant={node.outboxWatermark === "overflow" || node.outboxWatermark === "critical" ? "destructive" : node.outboxWatermark === "warning" ? "secondary" : "outline"}>{node.outboxDiskUsedPercent.toFixed(1)}%</Badge></TableCell>
                      <TableCell className="text-center">{node.assignmentCount}</TableCell>
                      <TableCell className="max-w-0 overflow-hidden text-center"><span className="block truncate">{formatTime(node.lastSeenAt)}</span></TableCell>
                      <TableCell><div className="flex justify-center gap-1">
                        {node.status !== "approved" && <Tooltip><TooltipTrigger asChild><Button variant="ghost" size="icon-sm" onClick={() => void updateStatus(node, "approved")} disabled={busy}><Check className="size-4" /></Button></TooltipTrigger><TooltipContent>批准节点</TooltipContent></Tooltip>}
                        {node.status !== "revoked" && <Tooltip><TooltipTrigger asChild><Button variant="ghost" size="icon-sm" onClick={() => void updateStatus(node, "revoked")} disabled={busy}><Ban className="size-4" /></Button></TooltipTrigger><TooltipContent>吊销节点</TooltipContent></Tooltip>}
                        <Tooltip><TooltipTrigger asChild><Button variant="ghost" size="icon-sm" onClick={() => openSettings(node)} disabled={node.status === "revoked"}><Settings2 className="size-4" /></Button></TooltipTrigger><TooltipContent>节点设置</TooltipContent></Tooltip>
                        <Tooltip><TooltipTrigger asChild><Button variant="ghost" size="icon-sm" onClick={() => void openAssignments(node)} disabled={node.status === "revoked"}><ListChecks className="size-4" /></Button></TooltipTrigger><TooltipContent>分配设备</TooltipContent></Tooltip>
                      </div></TableCell>
                    </TableRow>
                  ))}
                  {nodes.length === 0 && <TableRow><TableCell colSpan={8} className="h-32 text-center text-muted-foreground">暂无边缘节点</TableCell></TableRow>}
                </TableBody>
              </Table>
            </div>
          )}
          {!loading && (
            <div className="flex flex-col gap-3 px-1 text-sm sm:flex-row sm:items-center sm:justify-between">
              <span className="text-muted-foreground">共 {nodes.length} 台节点</span>
              <div className="flex flex-wrap items-center gap-3 sm:justify-end">
                <div className="flex items-center gap-2">
                  <span className="text-muted-foreground">每页行数</span>
                  <Select value={String(pageSize)} onValueChange={(value) => { setPageSize(Number(value)); setPage(1); }}>
                    <SelectTrigger className="w-20" size="sm" aria-label="每页行数"><SelectValue /></SelectTrigger>
                    <SelectContent align="end">
                      {[5, 10, 20].map((value) => <SelectItem key={value} value={String(value)}>{value}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
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

      <Dialog open={Boolean(assignmentNode)} onOpenChange={(open) => { if (!open) setAssignmentNode(null); }}>
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
          <div className="max-h-80 overflow-auto border border-border">
            {assignmentLoading ? (
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
                        暂无该分类设备
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            )}
          </div>
          <DialogFooter><Button onClick={() => void saveAssignments()} disabled={busy || assignmentLoading}>保存分配</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
    </TooltipProvider>
  );
}
