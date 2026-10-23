import { useCallback, useEffect, useMemo, useRef, useState, type ChangeEvent } from "react";
import { useNavigate } from "react-router-dom";
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Download, MapPin, Plus, Power, Search, SquarePen, Trash2, Upload } from "lucide-react";
import { toast } from "sonner";
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { CTRL, DANGER_BTN, TAB_LIST, DotText, HEAD_ROW, ICON_BTN, PAGER, PAGER_BTN, PRIMARY_BTN, ROW_ICON, ROW_SELECTED, SelectedCount, SoftBadge, StatusPill, TABLE_FRAME, TABLE_TEXT, TD, TH } from "@/components/DevicePageUi";
import { useAuth } from "@/contexts/AuthContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { backendClient } from "@/lib/backend-client";
import { useAdaptivePageSize } from "@/hooks/useAdaptivePageSize";
import { parseImportWorkbook } from "@/lib/import-workbook";
import { saveDownload } from "@/lib/save-download";
import type { DeviceForm, DeviceListItem, ImportResult } from "@/types/device-monitor";

const EMPTY_FORM: DeviceForm = { deviceType: "", deviceCode: "", deviceName: "", driverName: "", deviceStatus: "enabled", endpoint: "", endpointBackup: "", description: "", rack: "", slot: "", scanCycle: "" };
// 主/备地址：IPv4[:端口]、主机名[:端口]，或 OPC 这类 opc.tcp:// 前缀地址；后端限长 20。
const ENDPOINT_RE = /^(?:[a-z][a-z0-9.+-]*:\/\/)?[A-Za-z0-9.-]+(?::\d{1,5})?(?:\/\S*)?$/;
export function isS7Driver(driverName: string): boolean {
  const key = driverName.replace(/[^a-z0-9]/gi, "").toUpperCase();
  return key.includes("S7") || key.includes("SNAP7");
}
function toOptionalInt(value: string): number | "" {
  if (value.trim() === "") return "";
  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.trunc(parsed) : "";
}

const TYPE_COLORS: Record<string, string> = {
  岸桥: "var(--brand)",
  场桥: "color-mix(in oklab, var(--brand) 60%, var(--card))",
  AGV: "var(--success)",
  PLC: "var(--warning)",
  仪表: "var(--muted-foreground)",
};
const TYPE_FALLBACK = ["var(--brand)", "var(--success)", "var(--warning)", "color-mix(in oklab, var(--brand) 50%, var(--destructive))", "var(--muted-foreground)"];
function typeColor(type: string) {
  if (TYPE_COLORS[type]) return TYPE_COLORS[type];
  let hash = 0;
  for (const char of type) hash = (hash * 31 + char.charCodeAt(0)) | 0;
  return TYPE_FALLBACK[Math.abs(hash) % TYPE_FALLBACK.length];
}

export default function DeviceManagementPage() {
  const { auth } = useAuth();
  const navigate = useNavigate();
  const [keyword, setKeyword] = useState("");
  const [activeType, setActiveType] = useState("all");
  const [connectStatus, setConnectStatus] = useState("all");
  const [page, setPage] = useState(1);
  const { pageSize, tableViewportRef } = useAdaptivePageSize({ rowHeight: 52 });
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<DeviceListItem | null>(null);
  const [form, setForm] = useState<DeviceForm>(EMPTY_FORM);
  const [deleting, setDeleting] = useState<DeviceListItem | null>(null);
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [batchDeleteOpen, setBatchDeleteOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [importResult, setImportResult] = useState<ImportResult | null>(null);
  const [saving, setSaving] = useState(false);
  const [transferring, setTransferring] = useState(false);
  const [statusUpdatingId, setStatusUpdatingId] = useState<number | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const loadDevices = useCallback(
    (token: string, signal: AbortSignal) => backendClient.deviceList(token, {
      pageNo: page,
      pageSize,
      keyword: keyword.trim() || undefined,
      deviceType: activeType === "all" ? undefined : activeType,
      connectStatus: connectStatus === "all" ? undefined : connectStatus,
    }, signal),
    [activeType, connectStatus, keyword, page, pageSize],
  );
  const loadDeviceCatalog = useCallback(
    (token: string, signal: AbortSignal) => backendClient.deviceTypes(token, {}, signal),
    [],
  );
  const loadDrivers = useCallback((token: string, signal: AbortSignal) => backendClient.driverOptions(token, signal), []);
  const devices = useBackendQuery(loadDevices);
  const deviceCatalog = useBackendQuery(loadDeviceCatalog);
  const typeTabs = useMemo(() => (deviceCatalog.data ?? []).map((value) => ({ value, label: value })), [deviceCatalog.data]);
  const drivers = useBackendQuery(loadDrivers);
  const loadTypeOptions = useCallback((token: string, signal: AbortSignal) => backendClient.deviceTypeOptions(token, signal), []);
  const typeOptions = useBackendQuery(loadTypeOptions);
  const rows = devices.data?.list ?? [];
  const totalPages = Math.max(1, Math.ceil((devices.data?.total ?? 0) / pageSize));

  useEffect(() => {
    setPage((value) => Math.min(value, totalPages));
  }, [totalPages]);
  useEffect(() => {
    setSelectedIds([]);
  }, [activeType, connectStatus, keyword, page, pageSize]);
  const allSelected = rows.length > 0 && rows.every((row) => selectedIds.includes(row.id));

  // 设备变更成功后统一刷新列表、类型目录与汇总 KPI，避免同屏数字矛盾
  function refreshAfterMutation() { devices.refresh(); deviceCatalog.refresh(); }
  function openCreate() { setEditing(null); setForm({ ...EMPTY_FORM }); setDialogOpen(true); }
  function openEdit(row: DeviceListItem) {
    setEditing(row);
    // 列表里的类型是名称（如"轨道吊"），下拉用编码；对不上时保留原值，后端按名称也能识别。
    const option = (typeOptions.data ?? []).find((item) => item.typeCode === row.deviceType || item.typeName === row.deviceType);
    setForm({ deviceType: option?.typeCode ?? row.deviceType, deviceCode: row.deviceCode, deviceName: row.deviceName, driverName: row.driverName, deviceStatus: row.deviceStatus, endpoint: row.endpoint, endpointBackup: row.endpointBackup, description: row.description ?? "", rack: "", slot: "", scanCycle: "" });
    setDialogOpen(true);
    if (!auth?.backendToken) return;
    backendClient.deviceDetail(auth.backendToken, row.id)
      .then((detail) => setForm((current) => (current.deviceCode !== row.deviceCode ? current : {
        ...current,
        rack: detail.rack ?? "",
        slot: detail.slot ?? "",
        scanCycle: detail.scanCycle ?? "",
      })))
      .catch(() => undefined);
  }
  function validateForm(): string | null {
    if (!form.deviceCode.trim()) return "请填写设备编号";
    if (/\s/.test(form.deviceCode.trim())) return "设备编号不能包含空格";
    if (!form.deviceName.trim()) return "请填写设备名称";
    if (!form.deviceType.trim()) return "请选择设备类型";
    if (!form.driverName) return "请选择关联驱动";
    if (!form.endpoint.trim()) return "请填写主地址";
    if (!ENDPOINT_RE.test(form.endpoint.trim())) return "主地址格式不对，例如 192.168.1.10:102";
    if (form.endpointBackup?.trim() && !ENDPOINT_RE.test(form.endpointBackup.trim())) return "备用地址格式不对";
    if (form.rack !== "" && form.rack !== undefined && (form.rack < 0 || form.rack > 7)) return "机架号范围 0~7";
    if (form.slot !== "" && form.slot !== undefined && (form.slot < 0 || form.slot > 31)) return "槽号范围 0~31";
    if (form.scanCycle !== "" && form.scanCycle !== undefined && (form.scanCycle < 10 || form.scanCycle > 600000)) return "采集周期范围 10~600000 ms";
    return null;
  }
  async function save() {
    if (!auth?.backendToken) return;
    const invalid = validateForm();
    if (invalid) { toast.error(invalid); return; }
    const payload: DeviceForm = {
      ...form,
      deviceType: form.deviceType.trim(),
      deviceCode: form.deviceCode.trim(),
      deviceName: form.deviceName.trim(),
      endpoint: form.endpoint.trim(),
      endpointBackup: form.endpointBackup?.trim() ?? "",
      description: form.description?.trim() ?? "",
    };
    setSaving(true);
    try {
      if (editing) await backendClient.updateDevice(auth.backendToken, editing.id, payload); else await backendClient.createDevice(auth.backendToken, payload);
      toast.success(editing ? "设备已更新" : "设备已新增"); setDialogOpen(false); refreshAfterMutation();
    } catch (reason) {
      const message = (reason as { message?: string })?.message;
      toast.error(message || "保存设备失败");
    } finally {
      setSaving(false);
    }
  }
  async function confirmDelete() {
    if (!auth?.backendToken || !deleting) return;
    try { await backendClient.deleteDevice(auth.backendToken, deleting.id); toast.success("设备已删除"); refreshAfterMutation(); } catch { toast.error("删除设备失败"); } finally { setDeleting(null); }
  }
  async function toggleDeviceStatus(row: DeviceListItem) {
    if (!auth?.backendToken) return;
    const enabled = row.deviceStatus !== "enabled";
    setStatusUpdatingId(row.id);
    try {
      await backendClient.updateDeviceStatus(auth.backendToken, row.id, enabled);
      toast.success(enabled ? "设备已启用" : "设备已停用");
      refreshAfterMutation();
    } catch (reason) {
      const message = (reason as { message?: string })?.message;
      toast.error(message || (enabled ? "启用设备失败" : "停用设备失败"));
    } finally {
      setStatusUpdatingId(null);
    }
  }
  function toggleAll(checked: boolean) { setSelectedIds(checked ? rows.map((row) => row.id) : []); }
  function toggleRow(id: number, checked: boolean) { setSelectedIds((current) => checked ? [...new Set([...current, id])] : current.filter((value) => value !== id)); }
  async function confirmBatchDelete() {
    if (!auth?.backendToken || !selectedIds.length) return;
    try {
      await backendClient.batchDeleteDevices(auth.backendToken, selectedIds);
      toast.success(`已删除 ${selectedIds.length} 台设备`);
      setSelectedIds([]); refreshAfterMutation();
    } catch { toast.error("批量删除设备失败"); } finally { setBatchDeleteOpen(false); }
  }
  async function exportWorkbook(template = false) {
    if (!auth?.backendToken) return;
    setTransferring(true);
    try {
      const file = await backendClient.exportDevices(auth.backendToken, { keyword: keyword.trim() || undefined, connectStatus: connectStatus === "all" ? undefined : connectStatus, deviceType: activeType === "all" ? undefined : activeType }, template);
      saveDownload(file.bytes, file.fileName);
      toast.success(template ? "设备导入模板已下载" : "设备列表已导出");
    } catch { toast.error(template ? "模板下载失败" : "设备导出失败"); } finally { setTransferring(false); }
  }
  async function importWorkbook(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file || !auth?.backendToken) return;
    setTransferring(true); setImportResult(null);
    try {
      const rows = await parseImportWorkbook(file, "device");
      if (!rows.length) throw new Error("文件中没有可导入的设备数据");
      const result = await backendClient.importDevices(auth.backendToken, rows);
      setImportResult(result);
      toast.success(`导入成功 ${result.success} 条` + (result.fail ? `，失败 ${result.fail} 条` : ""));
      refreshAfterMutation();
    } catch (reason) { toast.error(reason instanceof Error ? reason.message : "设备导入失败"); } finally { setTransferring(false); }
  }

  return (
    <div className="flex h-full min-h-0 w-full flex-col gap-4">
      <Card className="flex min-h-0 flex-1 flex-col gap-0 py-0">
        <TooltipProvider delayDuration={200}>
        <div className="flex shrink-0 flex-wrap items-center gap-2 px-5 pt-5">
          <Tabs value={activeType} onValueChange={(value) => { setActiveType(value); setPage(1); }}><div className="max-w-full overflow-x-auto overflow-y-hidden [scrollbar-width:none]"><TabsList className={TAB_LIST}><TabsTrigger value="all">汇总</TabsTrigger>{typeTabs.map((type) => <TabsTrigger key={type.value} value={type.value}>{type.label}</TabsTrigger>)}</TabsList></div></Tabs>
          <div className="relative w-44"><Search className="absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" /><Input className={`${CTRL} pl-8`} placeholder="设备名称 / 编号" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} /></div>
          <Select value={connectStatus} onValueChange={(value) => { setConnectStatus(value); setPage(1); }}><SelectTrigger className={`${CTRL} w-24`}><SelectValue /></SelectTrigger><SelectContent><SelectGroup><SelectItem value="all">全部状态</SelectItem><SelectItem value="online">在线</SelectItem><SelectItem value="unstable">不稳定</SelectItem><SelectItem value="offline">离线</SelectItem></SelectGroup></SelectContent></Select>
          <div className="ml-auto flex items-center gap-2">
            <Button className={PRIMARY_BTN} onClick={openCreate}><Plus data-icon="inline-start" />新增设备</Button>
            <Tooltip><TooltipTrigger asChild><Button variant="outline" className={ICON_BTN} aria-label="导入" onClick={() => { setImportResult(null); setImportOpen(true); }}><Upload /></Button></TooltipTrigger><TooltipContent>导入</TooltipContent></Tooltip>
            <Tooltip><TooltipTrigger asChild><Button variant="outline" className={ICON_BTN} aria-label="导出" onClick={() => exportWorkbook(false)} disabled={transferring}><Download /></Button></TooltipTrigger><TooltipContent>导出</TooltipContent></Tooltip>
            <Button variant="destructive" size="sm" className={DANGER_BTN} disabled={!selectedIds.length} onClick={() => setBatchDeleteOpen(true)}><Trash2 data-icon="inline-start" />删除{selectedIds.length > 0 && <SelectedCount count={selectedIds.length} />}</Button>
          </div>
        </div>
        <CardContent className="flex min-h-0 flex-1 flex-col px-5 pb-5 pt-4">
          <div className={`flex min-h-0 flex-1 flex-col ${TABLE_FRAME}`}>
          <div ref={tableViewportRef} className="min-h-0 flex-1 overflow-auto">
            <Table className={`w-full table-fixed ${TABLE_TEXT}`}>
              <TableHeader><TableRow className={HEAD_ROW}><TableHead className="h-10 w-12 px-0 text-center"><Checkbox className="mx-auto" aria-label="选择全部设备" checked={allSelected ? true : selectedIds.length ? "indeterminate" : false} onCheckedChange={(checked) => toggleAll(checked === true)} /></TableHead><TableHead className={`${TH} w-[12%]`}>设备编号</TableHead><TableHead className={TH}>设备名称</TableHead><TableHead className={`${TH} w-[10%]`}>类型</TableHead><TableHead className={`${TH} w-[13%]`}>关联驱动</TableHead><TableHead className={`${TH} w-[12%]`}>通讯状态</TableHead><TableHead className={`${TH} w-[12%]`}>数据状态</TableHead><TableHead className={`${TH} w-40 text-center`}>操作</TableHead></TableRow></TableHeader>
              <TableBody>{devices.loading ? Array.from({ length: pageSize }, (_, index) => <TableRow key={index} className="h-[52px]"><TableCell colSpan={8} className={TD}><Skeleton className="h-6 w-full" /></TableCell></TableRow>) : rows.length ? rows.map((row) => { const off = row.deviceStatus !== "enabled"; return <TableRow key={row.id} className={`h-[52px] ${ROW_SELECTED}`} data-state={selectedIds.includes(row.id) ? "selected" : undefined}><TableCell className="px-0 text-center"><Checkbox className="mx-auto" aria-label={`选择设备 ${row.deviceName}`} checked={selectedIds.includes(row.id)} onCheckedChange={(checked) => toggleRow(row.id, checked === true)} /></TableCell><TableCell className={`${TD} truncate font-mono text-[12.5px] font-medium ${off ? "text-muted-foreground" : ""}`}>{row.deviceCode}</TableCell><TableCell className={`${TD} max-w-0`}><div className="flex min-w-0 items-center gap-2"><span className={`truncate font-medium ${off ? "text-muted-foreground" : ""}`}>{row.deviceName}</span>{off && <SoftBadge tone="muted" dot={false} className="h-[18px] text-[11px]">已停用</SoftBadge>}</div><p className="truncate font-mono text-[11px] leading-[1.3] text-muted-foreground">{row.endpoint || "—"}</p></TableCell><TableCell className={`${TD} max-w-0`}><span className="inline-flex max-w-full items-center gap-1.5"><span className="size-2 shrink-0 rounded-[2px]" style={{ background: typeColor(row.deviceType) }} /><span className="truncate">{row.deviceType}</span></span></TableCell><TableCell className={`${TD} truncate`}>{row.driverName}</TableCell><TableCell className={TD}><StatusPill tone={row.connectStatus === "online" ? "ok" : row.connectStatus === "unstable" ? "warn" : "bad"}>{row.connectStatus === "online" ? "在线" : row.connectStatus === "unstable" ? "不稳定" : "离线"}</StatusPill></TableCell><TableCell className={TD}><DotText tone={row.dataStatus === "normal" ? "ok" : row.dataStatus === "abnormal" ? "warn" : "muted"}>{row.dataStatus === "normal" ? "正常" : row.dataStatus === "abnormal" ? "异常" : "无数据"}</DotText></TableCell><TableCell className={TD}><div className="flex justify-center gap-0.5"><Button variant="ghost" size="icon" className={ROW_ICON} title="查看点位" onClick={() => navigate(`/device/points?deviceId=${row.id}&deviceCode=${encodeURIComponent(row.deviceCode)}`)}><MapPin className="size-4" /></Button><Button variant="ghost" size="icon" className={ROW_ICON} title={row.deviceStatus === "enabled" ? "停用设备" : "启用设备"} disabled={statusUpdatingId === row.id} onClick={() => void toggleDeviceStatus(row)}>{row.deviceStatus === "enabled" ? <Power className="size-4 text-warning" /> : <Power className="size-4 text-success" />}</Button><Button variant="ghost" size="icon" className={ROW_ICON} title="编辑设备" onClick={() => openEdit(row)}><SquarePen className="size-4" /></Button><Button variant="ghost" size="icon" className={ROW_ICON} title="删除设备" onClick={() => setDeleting(row)}><Trash2 className="size-4 text-destructive" /></Button></div></TableCell></TableRow>; }) : <TableRow><TableCell colSpan={8} className="h-32 text-center text-muted-foreground">{devices.error ?? "暂无设备"}</TableCell></TableRow>}</TableBody>
            </Table>
          </div>
          <div className={PAGER}>
            <span>共 {devices.data?.total ?? 0} 台设备</span>
            <span className="flex-1" />
            <span>每页 {pageSize} 行</span><span className="mx-1 font-medium tabular-nums text-foreground">第 {page} / {totalPages} 页</span>
            <Button variant="outline" size="icon" className={PAGER_BTN} disabled={page === 1} onClick={() => setPage(1)} aria-label="第一页"><ChevronsLeft /></Button><Button variant="outline" size="icon" className={PAGER_BTN} disabled={page === 1} onClick={() => setPage((value) => value - 1)} aria-label="上一页"><ChevronLeft /></Button><Button variant="outline" size="icon" className={PAGER_BTN} disabled={page === totalPages} onClick={() => setPage((value) => value + 1)} aria-label="下一页"><ChevronRight /></Button><Button variant="outline" size="icon" className={PAGER_BTN} disabled={page === totalPages} onClick={() => setPage(totalPages)} aria-label="最后一页"><ChevronsRight /></Button>
          </div>
          </div>
        </CardContent>
        </TooltipProvider>
      </Card>

      <input ref={fileInputRef} className="hidden" type="file" accept=".xlsx" onChange={importWorkbook} />
      <Dialog open={importOpen} onOpenChange={setImportOpen}><DialogContent><DialogHeader><DialogTitle>导入设备</DialogTitle><DialogDescription>仅支持 .xlsx 文件。请按模板填写设备类型、设备编号、设备名称、关联驱动和设备状态。</DialogDescription></DialogHeader>{importResult && <div className="rounded-md border bg-muted/30 p-3 text-sm"><p>成功 {importResult.success} 条，失败 {importResult.fail} 条</p>{importResult.errors?.slice(0, 5).map((item) => <p key={item.rowNumber} className="mt-1 text-xs text-destructive">第 {item.rowNumber} 行：{item.reason}</p>)}</div>}<DialogFooter className="sm:justify-between"><Button variant="ghost" onClick={() => exportWorkbook(true)} disabled={transferring}><Download className="size-4" />下载模板</Button><div className="flex gap-2"><Button variant="outline" onClick={() => setImportOpen(false)}>取消</Button><Button onClick={() => fileInputRef.current?.click()} disabled={transferring}><Upload className="size-4" />{transferring ? "处理中…" : "选择文件"}</Button></div></DialogFooter></DialogContent></Dialog>
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}><DialogContent><DialogHeader><DialogTitle>{editing ? "编辑设备" : "新增设备"}</DialogTitle></DialogHeader><div className="grid gap-4 py-2"><div className="grid grid-cols-2 gap-3"><div className="space-y-2"><Label>设备编号</Label><Input value={form.deviceCode} maxLength={20} placeholder="如 asc7181" disabled={Boolean(editing)} onChange={(event) => setForm({ ...form, deviceCode: event.target.value })} /></div><div className="space-y-2"><Label>设备名称</Label><Input value={form.deviceName} maxLength={20} placeholder="如 718L轨道吊" onChange={(event) => setForm({ ...form, deviceName: event.target.value })} /></div></div><div className="grid grid-cols-2 gap-3"><div className="space-y-2"><Label>设备类型</Label><Select value={form.deviceType} onValueChange={(value) => setForm({ ...form, deviceType: value })}><SelectTrigger><SelectValue placeholder="选择设备类型" /></SelectTrigger><SelectContent>{form.deviceType && !(typeOptions.data ?? []).some((item) => item.typeCode === form.deviceType) && <SelectItem value={form.deviceType}>{form.deviceType}</SelectItem>}{(typeOptions.data ?? []).map((item) => <SelectItem key={item.typeCode} value={item.typeCode}>{item.typeName === item.typeCode ? item.typeCode : `${item.typeName} · ${item.typeCode}`}</SelectItem>)}</SelectContent></Select></div><div className="space-y-2"><Label>关联驱动</Label><Select value={form.driverName} onValueChange={(value) => setForm({ ...form, driverName: value })}><SelectTrigger><SelectValue placeholder="选择驱动" /></SelectTrigger><SelectContent>{(drivers.data ?? []).map((driver) => <SelectItem key={driver.driverName} value={driver.driverName}>{driver.driverName}</SelectItem>)}</SelectContent></Select></div></div><div className="grid grid-cols-2 gap-3"><div className="space-y-2"><Label>主地址</Label><Input value={form.endpoint} maxLength={20} placeholder="例如 192.168.1.10:102" onChange={(event) => setForm({ ...form, endpoint: event.target.value })} /></div><div className="space-y-2"><Label>备用地址</Label><Input value={form.endpointBackup ?? ""} maxLength={20} placeholder="选填" onChange={(event) => setForm({ ...form, endpointBackup: event.target.value })} /></div></div><div className="grid grid-cols-3 gap-3"><div className="space-y-2"><Label>机架号{isS7Driver(form.driverName) ? "" : "（S7 用）"}</Label><Input type="number" min={0} max={7} value={form.rack ?? ""} placeholder="默认 0" onChange={(event) => setForm({ ...form, rack: toOptionalInt(event.target.value) })} /></div><div className="space-y-2"><Label>槽号{isS7Driver(form.driverName) ? "" : "（S7 用）"}</Label><Input type="number" min={0} max={31} value={form.slot ?? ""} placeholder={isS7Driver(form.driverName) ? "S7-300 一般为 2" : "默认 1"} onChange={(event) => setForm({ ...form, slot: toOptionalInt(event.target.value) })} /></div><div className="space-y-2"><Label>采集周期 (ms)</Label><Input type="number" min={10} step={100} value={form.scanCycle ?? ""} placeholder="默认 1000" onChange={(event) => setForm({ ...form, scanCycle: toOptionalInt(event.target.value) })} /></div></div><div className="space-y-2"><Label>设备状态</Label><Select value={form.deviceStatus} onValueChange={(value: "enabled" | "disabled") => setForm({ ...form, deviceStatus: value })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="enabled">启用</SelectItem><SelectItem value="disabled">停用</SelectItem></SelectContent></Select></div><div className="space-y-2"><Label>描述说明</Label><Textarea value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></div></div><DialogFooter><Button variant="outline" onClick={() => setDialogOpen(false)}>取消</Button><Button onClick={save} disabled={saving}>{saving ? "保存中…" : "保存"}</Button></DialogFooter></DialogContent></Dialog>
      <AlertDialog open={batchDeleteOpen} onOpenChange={setBatchDeleteOpen}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>批量删除设备</AlertDialogTitle><AlertDialogDescription>确认删除选中的 {selectedIds.length} 台设备吗？关联点位也会被删除。</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction onClick={confirmBatchDelete}>删除</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
      <AlertDialog open={Boolean(deleting)} onOpenChange={(open) => !open && setDeleting(null)}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>删除设备</AlertDialogTitle><AlertDialogDescription>确认删除设备“{deleting?.deviceName}”吗？该操作不可撤销。</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction onClick={confirmDelete}>删除</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    </div>
  );
}
