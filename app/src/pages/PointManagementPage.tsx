import { useCallback, useEffect, useMemo, useRef, useState, type ChangeEvent } from "react";
import { useSearchParams } from "react-router-dom";
import { ChevronDown, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Download, Eye, Lock, Pencil, Plus, Power, ListFilter, Search, Trash2, Upload } from "lucide-react";
import { toast } from "sonner";
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { useAuth } from "@/contexts/AuthContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { backendClient } from "@/lib/backend-client";
import { useDevicePicker } from "@/hooks/useDevicePicker";
import { useAdaptivePageSize } from "@/hooks/useAdaptivePageSize";
import { DeviceCatalogPagination } from "@/components/DeviceCatalogPagination";
import { CTRL, DANGER_BTN, HEAD_ROW, ICON_BTN, PAGER, PAGER_BTN, PRIMARY_BTN, ROW_ICON, ROW_SELECTED, SelectedCount, SoftBadge, TABLE_FRAME, TABLE_TEXT, TAB_LIST, TD, TH } from "@/components/DevicePageUi";
import { parseImportWorkbook } from "@/lib/import-workbook";
import { saveDownload } from "@/lib/save-download";
import { POINT_DISPLAY_DATA_TYPES, type PointDataType } from "@/types/device-monitor";
import type { ImportResult, PointForm, PointListItem } from "@/types/device-monitor";

// varSeq=0 表示"自动分配"：选中设备后取下一个可用序号，后端也会兜底分配。
const EMPTY_FORM: PointForm = { deviceId: 0, deviceCode: "", varSeq: 0, pointName: "", ioAddress: "", dataType: "REAL", driverName: "", isReadonly: true, description: "", scanRate: 1000, isStorage: true };
/** 按驱动给出 IO 地址示例，减少地址格式写错。 */
export function ioAddressHint(driverName: string): string {
  const key = driverName.replace(/[^a-z0-9]/gi, "").toUpperCase();
  if (key.includes("S7") || key.includes("SNAP7")) return "如 MD908、DB13.DBD30、DB1.DBX0.0、I106.0";
  if (key.includes("MODBUS")) return "如 40001、30001、00001";
  if (key.includes("OPCUA")) return "节点 ID，如 ns=2;s=Tag1";
  if (key.includes("OPCDA")) return "项 ID，如 Channel1.Device1.Tag1";
  if (key.includes("SRTP") || key.includes("GE")) return "如 %R100、%I1、%Q1";
  return "按驱动要求填写地址";
}
function errorMessage(reason: unknown, fallback: string): string {
  const message = (reason as { message?: string } | null)?.message;
  return typeof message === "string" && message.trim() ? message : fallback;
}
// 可新建/编辑的物理类型，和后端白名单一致；类型筛选另用显示口径全集。
const DATA_TYPES: PointDataType[] = ["BOOL", "INT", "DINT", "REAL", "STRING", "WORD", "DWORD"];
const DATA_TYPE_COLORS: Record<string, string> = {
  REAL: "var(--brand)",
  BOOL: "var(--success)",
  INT: "var(--warning)",
  DINT: "color-mix(in oklab, var(--warning) 60%, var(--card))",
  WORD: "color-mix(in oklab, var(--brand) 50%, var(--card))",
  DWORD: "color-mix(in oklab, var(--brand) 35%, var(--success))",
};
function dataTypeColor(type: string) {
  return DATA_TYPE_COLORS[type] ?? "var(--muted-foreground)";
}

export default function PointManagementPage() {
  const { auth } = useAuth();
  const [searchParams] = useSearchParams();
  const [keyword, setKeyword] = useState("");
  const [deviceCodeFilter, setDeviceCodeFilter] = useState("");
  const [devicePickerOpen, setDevicePickerOpen] = useState(false);
  const [deviceCodeDraft, setDeviceCodeDraft] = useState("");
  const [dataTypeFilter, setDataTypeFilter] = useState("all");
  const [activeType, setActiveType] = useState("all");
  const [connectStatus, setConnectStatus] = useState("all");
  const [page, setPage] = useState(1);
  const { pageSize, tableViewportRef } = useAdaptivePageSize({ rowHeight: 40 });
  const [deviceId, setDeviceId] = useState(() => {
    const routeDeviceId = Number(searchParams.get("deviceId"));
    return Number.isInteger(routeDeviceId) && routeDeviceId > 0 ? String(routeDeviceId) : "all";
  });
  const [dialogOpen, setDialogOpen] = useState(false);
  const [formDeviceSearch, setFormDeviceSearch] = useState("");
  const [editing, setEditing] = useState<PointListItem | null>(null);
  const [detail, setDetail] = useState<PointListItem | null>(null);
  const [deleting, setDeleting] = useState<PointListItem | null>(null);
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [batchDeleteOpen, setBatchDeleteOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [importResult, setImportResult] = useState<ImportResult | null>(null);
  const [form, setForm] = useState<PointForm>(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [transferring, setTransferring] = useState(false);
  const [statusUpdatingId, setStatusUpdatingId] = useState<number | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const loadDeviceCatalog = useCallback(
    (token: string, signal: AbortSignal) => backendClient.deviceTypes(token, {}, signal),
    [],
  );
  const deviceCatalog = useBackendQuery(loadDeviceCatalog);
  const devicePicker = useDevicePicker(backendClient, deviceCodeDraft, activeType, devicePickerOpen);
  const pickerDevices = devicePicker.data?.list ?? [];
  const formDevicePicker = useDevicePicker(backendClient, formDeviceSearch, "all", dialogOpen);
  const devices = formDevicePicker.data?.list ?? [];
  // 列表与导出共用同一组筛选条件，避免导出范围大于当前列表
  const pointFilter = useMemo(() => ({
    keyword: keyword.trim() || undefined,
    deviceType: activeType === "all" ? undefined : activeType,
    connectStatus: connectStatus === "all" ? undefined : connectStatus,
    deviceId: deviceId === "all" ? undefined : Number(deviceId),
    deviceCode: deviceCodeFilter.trim() || undefined,
    dataType: dataTypeFilter === "all" ? undefined : dataTypeFilter,
  }), [activeType, connectStatus, dataTypeFilter, deviceCodeFilter, deviceId, keyword]);
  const loadPoints = useCallback((token: string, signal: AbortSignal) => backendClient.pointList(token, {
    pageNo: page,
    pageSize,
    ...pointFilter,
  }, signal), [page, pageSize, pointFilter]);
  const points = useBackendQuery(loadPoints);
  const routeDeviceCode = searchParams.get("deviceCode");
  const loadRouteDevice = useCallback((token: string, signal: AbortSignal) => backendClient.deviceList(token, {
    pageNo: 1, pageSize: 1, deviceCodeExact: routeDeviceCode ?? undefined,
  }, signal), [routeDeviceCode]);
  const routeDevice = useBackendQuery(loadRouteDevice, { enabled: Boolean(routeDeviceCode) && !searchParams.get("deviceId") });
  useEffect(() => {
    const routeDeviceId = Number(searchParams.get("deviceId"));
    const routeDeviceCode = searchParams.get("deviceCode");
    if (Number.isInteger(routeDeviceId) && routeDeviceId > 0) { setDeviceId(String(routeDeviceId)); return; }
    const matchedDevice = routeDevice.data?.list.find((device) => device.deviceCode === routeDeviceCode);
    if (matchedDevice) {
      setDeviceId(String(matchedDevice.id));
      setDeviceCodeFilter(matchedDevice.deviceCode);
    }
  }, [routeDevice.data, searchParams]);
  const rows = points.data?.list ?? [];
  const totalPages = Math.max(1, Math.ceil((points.data?.total ?? 0) / pageSize));
  const pageRows = rows;
  useEffect(() => {
    setPage((value) => Math.min(value, totalPages));
  }, [totalPages]);
  useEffect(() => {
    setSelectedIds([]);
  }, [activeType, connectStatus, dataTypeFilter, deviceCodeFilter, deviceId, keyword, page, pageSize]);
  const typeTabs = useMemo(() => (deviceCatalog.data ?? []).map((value) => ({ value, label: value })), [deviceCatalog.data]);
  const allSelected = pageRows.length > 0 && pageRows.every((row) => selectedIds.includes(row.id));

  function openDevicePicker() {
    setDeviceCodeDraft(deviceCodeFilter);
    setDevicePickerOpen(true);
  }

  function applyDeviceCode(code: string, id?: number) {
    setDeviceCodeFilter(code);
    setDeviceId(id != null ? String(id) : "all");
    setPage(1);
    setDevicePickerOpen(false);
  }
  function openCreate() { setEditing(null); setFormDeviceSearch(""); setForm({ ...EMPTY_FORM }); setDialogOpen(true); }
  function openEdit(row: PointListItem) { setEditing(row); setFormDeviceSearch(""); setForm({ deviceId: row.deviceId, deviceCode: row.deviceCode, varSeq: row.varSeq, pointName: row.pointName, ioAddress: row.ioAddress, dataType: row.dataType, driverName: row.driverName, isReadonly: row.isReadonly, description: row.description, scanRate: row.scanRate ?? 1000, isStorage: row.isStorage ?? true }); setDialogOpen(true); }
  // 选设备时驱动跟随设备（后端要求一致），新增时顺带取下一个可用序号。
  function selectFormDevice(value: string) {
    const device = devices.find((item) => String(item.id) === value);
    const nextId = Number(value);
    setForm((current) => ({ ...current, deviceId: nextId, deviceCode: device?.deviceCode ?? current.deviceCode, driverName: device?.driverName ?? current.driverName, varSeq: editing ? current.varSeq : 0 }));
    if (editing || !auth?.backendToken || !nextId) return;
    backendClient.nextPointSeq(auth.backendToken, nextId)
      .then(({ varSeq }) => setForm((current) => (current.deviceId === nextId && !current.varSeq ? { ...current, varSeq } : current)))
      .catch(() => undefined);
  }
  function validateForm(): string | null {
    if (!form.deviceId || !form.deviceCode) return "请选择所属设备";
    if (!form.pointName.trim()) return "请填写点位名称";
    if (!form.ioAddress.trim()) return "请填写 IO 地址";
    if (/\s/.test(form.ioAddress.trim())) return "IO 地址不能包含空格";
    if (form.varSeq && (!Number.isInteger(form.varSeq) || form.varSeq <= 0)) return "点位序号必须是正整数";
    if (form.scanRate !== undefined && (!Number.isInteger(form.scanRate) || form.scanRate < 10)) return "采集周期至少 10 ms";
    return null;
  }
  async function save() {
    if (!auth?.backendToken) return;
    const invalid = validateForm();
    if (invalid) { toast.error(invalid); return; }
    const payload: PointForm = { ...form, pointName: form.pointName.trim(), ioAddress: form.ioAddress.trim(), description: form.description?.trim() ?? "" };
    if (!payload.varSeq) delete (payload as Partial<PointForm>).varSeq;
    setSaving(true);
    try { if (editing) await backendClient.updatePoint(auth.backendToken, editing.id, payload); else await backendClient.createPoint(auth.backendToken, payload); toast.success(editing ? "点位已更新" : "点位已新增"); setDialogOpen(false); points.refresh(); deviceCatalog.refresh(); } catch (reason) { toast.error(errorMessage(reason, "保存点位失败")); } finally { setSaving(false); }
  }
  async function confirmDelete() { if (!auth?.backendToken || !deleting) return; try { await backendClient.deletePoint(auth.backendToken, deleting.id); toast.success("点位已删除"); points.refresh(); } catch { toast.error("删除点位失败"); } finally { setDeleting(null); } }
  async function togglePointStatus(row: PointListItem) {
    if (!auth?.backendToken) return;
    const enabled = row.pointStatus !== "enabled";
    setStatusUpdatingId(row.id);
    try {
      await backendClient.updatePointStatus(
        auth.backendToken,
        row.id,
        enabled,
        { deviceId: row.deviceId, varSeq: row.varSeq },
      );
      toast.success(enabled ? "点位已启用" : "点位已停用");
      points.refresh();
    } catch {
      toast.error(enabled ? "启用点位失败" : "停用点位失败");
    } finally {
      setStatusUpdatingId(null);
    }
  }
  function toggleAll(checked: boolean) { setSelectedIds(checked ? pageRows.map((row) => row.id) : []); }
  function toggleRow(id: number, checked: boolean) { setSelectedIds((current) => checked ? [...new Set([...current, id])] : current.filter((value) => value !== id)); }
  async function confirmBatchDelete() {
    if (!auth?.backendToken || !selectedIds.length) return;
    try {
      await backendClient.batchDeletePoints(auth.backendToken, selectedIds);
      toast.success(`已删除 ${selectedIds.length} 个点位`);
      setSelectedIds([]); points.refresh(); deviceCatalog.refresh();
    } catch { toast.error("批量删除点位失败"); } finally { setBatchDeleteOpen(false); }
  }
  async function exportWorkbook(template = false) {
    if (!auth?.backendToken) return;
    setTransferring(true);
    try {
      const file = await backendClient.exportPoints(auth.backendToken, { ...pointFilter }, template);
      saveDownload(file.bytes, file.fileName);
      toast.success(template ? "点位导入模板已下载" : "点位列表已导出");
    } catch { toast.error(template ? "模板下载失败" : "点位导出失败"); } finally { setTransferring(false); }
  }
  async function importWorkbook(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file || !auth?.backendToken) return;
    setTransferring(true); setImportResult(null);
    try {
      const rows = await parseImportWorkbook(file, "point");
      if (!rows.length) throw new Error("文件中没有可导入的点位数据");
      const result = await backendClient.importPoints(auth.backendToken, rows);
      setImportResult(result);
      toast.success(`导入成功 ${result.success} 条` + (result.fail ? `，失败 ${result.fail} 条` : ""));
      points.refresh(); deviceCatalog.refresh();
    } catch (reason) { toast.error(reason instanceof Error ? reason.message : "点位导入失败"); } finally { setTransferring(false); }
  }
  async function importFromTemplate() {
    if (!auth?.backendToken || deviceId === "all") { toast.error("请先选择目标设备"); return; }
    setTransferring(true); setImportResult(null);
    try {
      const result = await backendClient.importPointsFromTemplate(auth.backendToken, Number(deviceId));
      setImportResult(result); toast.success(`从模板导入成功 ${result.success} 条`); points.refresh(); deviceCatalog.refresh();
    } catch { toast.error("从模板导入点位失败"); } finally { setTransferring(false); }
  }

  const activeFilterCount = (dataTypeFilter !== "all" ? 1 : 0) + (connectStatus !== "all" ? 1 : 0);

  return (
    <div className="flex h-full min-h-0 w-full flex-col">
      <Card className="flex min-h-0 flex-1 flex-col gap-0 py-0">
        <TooltipProvider delayDuration={200}>
        <div className="flex shrink-0 flex-nowrap items-center gap-2 whitespace-nowrap px-5 pt-5">
          <Tabs className="min-w-0 shrink" value={activeType} onValueChange={(value) => { setActiveType(value); setDeviceId("all"); setDeviceCodeFilter(""); setPage(1); }}><div className="max-w-full overflow-x-auto overflow-y-hidden [scrollbar-width:none]"><TabsList className={TAB_LIST}><TabsTrigger value="all">汇总</TabsTrigger>{typeTabs.map((type) => <TabsTrigger key={type.value} value={type.value}>{type.label}</TabsTrigger>)}</TabsList></div></Tabs>
          <Button type="button" variant="outline" className="h-8 w-[104px] shrink-0 justify-between px-2.5 text-[13px] font-normal" onClick={openDevicePicker}>
            <span className={`truncate ${deviceCodeFilter ? "font-mono text-[12.5px] text-foreground" : "text-muted-foreground"}`}>{deviceCodeFilter || "设备号"}</span>
            <ChevronDown className="size-3.5 shrink-0 opacity-50" />
          </Button>
          <div className="relative min-w-[140px] max-w-[240px] flex-1"><Search className="absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" /><Input className={`${CTRL} pl-8`} placeholder="点位名称 / IO地址" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} /></div>
          <Popover>
            <PopoverTrigger asChild>
              <Button type="button" variant="outline" className={`h-8 shrink-0 gap-1.5 px-2.5 text-[13px] font-normal ${activeFilterCount ? "border-primary/40 text-primary" : ""}`} aria-label="更多筛选"><ListFilter className="size-3.5" />筛选{activeFilterCount > 0 && <SelectedCount count={activeFilterCount} />}</Button>
            </PopoverTrigger>
            <PopoverContent align="start" className="w-[240px] space-y-3 p-3">
              <div className="space-y-1.5"><Label className="text-xs font-normal text-muted-foreground">数据类型</Label>
                <Select value={dataTypeFilter} onValueChange={(value) => { setDataTypeFilter(value); setPage(1); }}>
                  <SelectTrigger className={`${CTRL} w-full`} title="数据类型"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectGroup>
                      <SelectItem value="all">全部类型</SelectItem>
                      {POINT_DISPLAY_DATA_TYPES.map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}
                    </SelectGroup>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5"><Label className="text-xs font-normal text-muted-foreground">连接状态</Label>
                <Select value={connectStatus} onValueChange={(value) => { setConnectStatus(value); setPage(1); }}><SelectTrigger className={`${CTRL} w-full`}><SelectValue /></SelectTrigger><SelectContent><SelectGroup><SelectItem value="all">全部状态</SelectItem><SelectItem value="online">在线</SelectItem><SelectItem value="unstable">不稳定</SelectItem><SelectItem value="offline">离线</SelectItem></SelectGroup></SelectContent></Select>
              </div>
              {activeFilterCount > 0 && <Button type="button" variant="ghost" className="h-7 w-full text-xs" onClick={() => { setDataTypeFilter("all"); setConnectStatus("all"); setPage(1); }}>清除筛选</Button>}
            </PopoverContent>
          </Popover>
          <div className="ml-auto flex shrink-0 items-center gap-2">
            {selectedIds.length > 0 && <Button variant="destructive" size="sm" className={DANGER_BTN} onClick={() => setBatchDeleteOpen(true)}>
              <Trash2 data-icon="inline-start" />删除
              <SelectedCount count={selectedIds.length} />
            </Button>}
            <Button className={PRIMARY_BTN} onClick={openCreate}><Plus data-icon="inline-start" />新增点位</Button>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button variant="outline" className={ICON_BTN} aria-label="导入点位" onClick={() => { setImportResult(null); setImportOpen(true); }}><Upload /></Button>
              </TooltipTrigger>
              <TooltipContent>导入点位</TooltipContent>
            </Tooltip>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button variant="outline" className={ICON_BTN} aria-label="导出点位" disabled={transferring} onClick={() => exportWorkbook(false)}><Download /></Button>
              </TooltipTrigger>
              <TooltipContent>导出点位</TooltipContent>
            </Tooltip>
          </div>
        </div>
        <CardContent className="flex min-h-0 flex-1 flex-col px-5 pb-5 pt-4">
          <div className={`flex min-h-0 flex-1 flex-col ${TABLE_FRAME}`}>
          <div ref={tableViewportRef} className="min-h-0 flex-1 overflow-auto"><Table className={`w-full table-fixed ${TABLE_TEXT}`}><TableHeader><TableRow className={HEAD_ROW}><TableHead className="h-10 w-12 px-0 text-center"><Checkbox className="mx-auto" aria-label="选择全部点位" checked={allSelected ? true : selectedIds.length ? "indeterminate" : false} onCheckedChange={(checked) => toggleAll(checked === true)} /></TableHead><TableHead className={`${TH} w-[12%]`}>所属设备</TableHead><TableHead className={TH}>点位名称</TableHead><TableHead className={`${TH} w-[9%]`}>点位序号</TableHead><TableHead className={`${TH} w-[20%]`}>IO地址</TableHead><TableHead className={`${TH} w-[10%]`}>数据类型</TableHead><TableHead className={`${TH} w-[12%]`}>驱动名称</TableHead><TableHead className={`${TH} w-[8%]`}>只读</TableHead><TableHead className={`${TH} w-[150px] text-center`}>操作</TableHead></TableRow></TableHeader><TableBody>{points.loading ? Array.from({ length: pageSize }, (_, index) => <TableRow key={index} className="h-10"><TableCell colSpan={9} className={TD}><Skeleton className="h-6 w-full" /></TableCell></TableRow>) : pageRows.length ? pageRows.map((row) => { const off = row.pointStatus !== "enabled"; const muted = off ? "text-muted-foreground" : ""; return <TableRow key={`${row.id}-${row.deviceId}-${row.varSeq}`} className={`h-10 ${ROW_SELECTED}`} data-state={selectedIds.includes(row.id) ? "selected" : undefined}><TableCell className="px-0 text-center"><Checkbox className="mx-auto" aria-label={`选择点位 ${row.pointName}`} checked={selectedIds.includes(row.id)} onCheckedChange={(checked) => toggleRow(row.id, checked === true)} /></TableCell><TableCell className={`${TD} truncate font-mono text-[12.5px] font-medium ${muted}`}>{row.deviceCode}</TableCell><TableCell className={`${TD} max-w-0 ${muted}`}><div className="flex min-w-0 items-center gap-1.5"><span className="truncate">{row.pointName}</span>{off && <SoftBadge tone="muted" dot={false} className="h-[18px] text-[11px]">已停用</SoftBadge>}</div></TableCell><TableCell className={TD}><span className={`inline-grid h-5 min-w-6 place-items-center rounded-md bg-muted px-1.5 text-xs tabular-nums ${muted}`}>{row.varSeq}</span></TableCell><TableCell className={`${TD} truncate font-mono text-xs ${muted}`} title={row.ioAddress}>{row.ioAddress}</TableCell><TableCell className={TD}><span className="inline-flex h-[22px] items-center gap-1.5 rounded-md border px-2 font-mono text-[11.5px] font-medium"><span className="size-1.5 rounded-[2px]" style={{ background: dataTypeColor(row.dataType) }} />{row.dataType}</span></TableCell><TableCell className={`${TD} truncate ${muted}`}>{row.driverName}</TableCell><TableCell className={TD}>{row.isReadonly ? <span className="inline-flex items-center gap-1 text-[12.5px] text-muted-foreground"><Lock className="size-[13px]" />是</span> : <span className="inline-flex items-center gap-1 text-[12.5px] text-brand"><Pencil className="size-[13px]" />否</span>}</TableCell><TableCell className={TD}><div className="flex justify-center gap-0.5"><Button variant="ghost" size="icon" className={ROW_ICON} title="查看详情" onClick={() => setDetail(row)}><Eye className="size-4" /></Button><Button variant="ghost" size="icon" className={ROW_ICON} title={row.pointStatus === "enabled" ? "停用点位" : "启用点位"} disabled={statusUpdatingId === row.id} onClick={() => void togglePointStatus(row)}>{row.pointStatus === "enabled" ? <Power className="size-4 text-warning" /> : <Power className="size-4 text-success" />}</Button><Button variant="ghost" size="icon" className={ROW_ICON} title="编辑点位" onClick={() => openEdit(row)}><Pencil className="size-4" /></Button><Button variant="ghost" size="icon" className={ROW_ICON} title="删除点位" onClick={() => setDeleting(row)}><Trash2 className="size-4 text-destructive" /></Button></div></TableCell></TableRow>; }) : <TableRow><TableCell colSpan={9} className="h-32 text-center text-muted-foreground">{points.error ?? "暂无点位"}</TableCell></TableRow>}</TableBody></Table></div>
          <div className={PAGER}><span>共 {(points.data?.total ?? 0).toLocaleString("en-US")} 个点位</span><span className="flex-1" /><span>每页 {pageSize} 行</span><span className="mx-1 font-medium tabular-nums text-foreground">第 {page} / {totalPages.toLocaleString("en-US")} 页</span><Button variant="outline" size="icon" className={PAGER_BTN} disabled={page === 1} onClick={() => setPage(1)} aria-label="第一页"><ChevronsLeft /></Button><Button variant="outline" size="icon" className={PAGER_BTN} disabled={page === 1} onClick={() => setPage((value) => value - 1)} aria-label="上一页"><ChevronLeft /></Button><Button variant="outline" size="icon" className={PAGER_BTN} disabled={page === totalPages} onClick={() => setPage((value) => value + 1)} aria-label="下一页"><ChevronRight /></Button><Button variant="outline" size="icon" className={PAGER_BTN} disabled={page === totalPages} onClick={() => setPage(totalPages)} aria-label="最后一页"><ChevronsRight /></Button></div>
          </div>
        </CardContent>
        </TooltipProvider>
      </Card>

      <input ref={fileInputRef} className="hidden" type="file" accept=".xlsx" onChange={importWorkbook} />
      <Dialog open={devicePickerOpen} onOpenChange={setDevicePickerOpen}>
        <DialogContent className="flex max-h-[85vh] w-[min(600px,90vw)] max-w-none flex-col gap-3 sm:max-w-none">
          <DialogHeader>
            <DialogTitle>选择设备号</DialogTitle>
            <DialogDescription>
              {activeType === "all" ? "可手打设备号，或从全部设备中选择。" : `可手打设备号，或从「${activeType}」类型设备中选择。`}
            </DialogDescription>
          </DialogHeader>
          <div className="flex min-h-0 flex-1 flex-col gap-3">
            <div className="relative shrink-0">
              <Search className="absolute left-3 top-2 size-4 text-muted-foreground" />
              <Input
                className="h-8 pl-9"
                placeholder="手打或搜索设备号 / 名称，例如 qc100"
                value={deviceCodeDraft}
                onChange={(event) => setDeviceCodeDraft(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter") applyDeviceCode(deviceCodeDraft.trim());
                }}
              />
            </div>
            <div className="h-[300px] max-h-[40vh] overflow-y-auto overflow-x-hidden rounded-lg border p-2.5 [scrollbar-width:thin]">
              {devicePicker.loading ? <Skeleton className="h-full w-full" /> : pickerDevices.length ? (
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 md:grid-cols-4">
                  {pickerDevices.map((device) => (
                    <button
                      key={device.id}
                      type="button"
                      className={`flex min-w-0 flex-col gap-px rounded-lg border px-2.5 py-2 text-left transition-colors hover:bg-muted ${device.deviceCode === deviceCodeFilter ? "border-brand bg-brand-soft" : "bg-card"}`}
                      onClick={() => applyDeviceCode(device.deviceCode, device.id)}
                    >
                      <span className="flex min-w-0 items-center gap-1.5 font-mono text-[13px] font-medium"><span className={`size-1.5 shrink-0 rounded-full ${device.connectStatus === "online" ? "bg-success" : device.connectStatus === "unstable" ? "bg-warning" : "bg-destructive"}`} /><span className="truncate">{device.deviceCode}</span></span>
                      <span className="truncate text-xs text-muted-foreground">{device.deviceName}</span>
                    </button>
                  ))}
                </div>
              ) : (
                <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
                  {devicePicker.loading ? "加载设备中…" : devicePicker.error ?? "没有匹配的设备号"}
                </div>
              )}
            </div>
            <DeviceCatalogPagination total={devicePicker.data?.total ?? 0} page={devicePicker.page} pageSize={devicePicker.pageSize} loading={devicePicker.loading} onPageChange={devicePicker.setPage} />
          </div>
          <DialogFooter className="sm:justify-between">
            <Button type="button" variant="ghost" onClick={() => applyDeviceCode("")}>清空</Button>
            <div className="flex gap-2">
              <Button type="button" variant="outline" onClick={() => setDevicePickerOpen(false)}>取消</Button>
              <Button type="button" onClick={() => applyDeviceCode(deviceCodeDraft.trim())}>确定</Button>
            </div>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog open={importOpen} onOpenChange={setImportOpen}><DialogContent><DialogHeader><DialogTitle>导入点位</DialogTitle><DialogDescription>下载模板后填写点位数据，或选择目标设备后从已有设备类型模板导入。</DialogDescription></DialogHeader>{importResult && <div className="rounded-md border bg-muted/30 p-3 text-sm"><p>成功 {importResult.success} 条，失败 {importResult.fail} 条</p>{importResult.errors?.slice(0, 5).map((item) => <p key={item.rowNumber} className="mt-1 text-xs text-destructive">第 {item.rowNumber} 行：{item.reason}</p>)}</div>}<DialogFooter className="flex-wrap sm:justify-between"><Button variant="ghost" onClick={() => exportWorkbook(true)} disabled={transferring}><Download className="size-4" />下载模板</Button><div className="flex flex-wrap gap-2"><Button variant="outline" onClick={importFromTemplate} disabled={transferring || deviceId === "all"}>从模板导入</Button><Button variant="outline" onClick={() => setImportOpen(false)}>取消</Button><Button onClick={() => fileInputRef.current?.click()} disabled={transferring}><Upload className="size-4" />{transferring ? "处理中…" : "选择文件"}</Button></div></DialogFooter></DialogContent></Dialog>
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}><DialogContent><DialogHeader><DialogTitle>{editing ? "编辑点位" : "新增点位"}</DialogTitle></DialogHeader><div className="grid gap-4 py-2"><div className="space-y-2"><Label>所属设备</Label><Select value={form.deviceId ? String(form.deviceId) : ""} disabled={Boolean(editing)} onValueChange={selectFormDevice}><SelectTrigger><SelectValue placeholder="选择设备" /></SelectTrigger><SelectContent><SelectGroup>{form.deviceId > 0 && !devices.some((device) => device.id === form.deviceId) && <SelectItem value={String(form.deviceId)}>{form.deviceCode}</SelectItem>}{devices.map((device) => <SelectItem key={device.id} value={String(device.id)}>{device.deviceCode} · {device.deviceName}</SelectItem>)}</SelectGroup></SelectContent></Select><Input aria-label="搜索所属设备" placeholder="搜索设备名称或编号" value={formDeviceSearch} onChange={(event) => setFormDeviceSearch(event.target.value)} /><DeviceCatalogPagination total={formDevicePicker.data?.total ?? 0} page={formDevicePicker.page} pageSize={formDevicePicker.pageSize} loading={formDevicePicker.loading} onPageChange={formDevicePicker.setPage} />{formDevicePicker.error && <p role="alert" className="text-sm text-destructive">{formDevicePicker.error}</p>}</div><div className="grid grid-cols-2 gap-3"><div className="space-y-2"><Label>点位名称</Label><Input value={form.pointName} maxLength={50} placeholder="如 大车位置" onChange={(event) => setForm({ ...form, pointName: event.target.value })} /></div><div className="space-y-2"><Label>点位序号</Label><Input type="number" min={1} value={form.varSeq || ""} placeholder="自动分配" disabled={Boolean(editing)} onChange={(event) => setForm({ ...form, varSeq: Number(event.target.value) || 0 })} /></div></div><div className="grid grid-cols-2 gap-3"><div className="space-y-2"><Label>IO地址</Label><Input value={form.ioAddress} maxLength={20} placeholder={ioAddressHint(form.driverName)} onChange={(event) => setForm({ ...form, ioAddress: event.target.value })} /></div><div className="space-y-2"><Label>数据类型</Label><Select value={form.dataType} onValueChange={(value: PointForm["dataType"]) => setForm({ ...form, dataType: value })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{[...new Set([...DATA_TYPES, form.dataType])].map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectContent></Select></div></div><div className="grid grid-cols-2 gap-3"><div className="space-y-2"><Label>驱动名称</Label><Input value={form.driverName} readOnly disabled placeholder="跟随所属设备" title="点位驱动由所属设备决定" /></div><div className="space-y-2"><Label>采集周期 (ms)</Label><Input type="number" min={10} step={100} value={form.scanRate ?? 1000} onChange={(event) => setForm({ ...form, scanRate: Number(event.target.value) || 0 })} /></div></div><div className="flex items-center gap-6"><label className="flex items-center gap-2 text-sm"><Checkbox checked={form.isReadonly} onCheckedChange={(checked) => setForm({ ...form, isReadonly: checked === true })} />只读</label><label className="flex items-center gap-2 text-sm"><Checkbox checked={form.isStorage ?? true} onCheckedChange={(checked) => setForm({ ...form, isStorage: checked === true })} />写入历史库</label></div><div className="space-y-2"><Label>描述说明</Label><Textarea value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></div></div><DialogFooter><Button variant="outline" onClick={() => setDialogOpen(false)}>取消</Button><Button onClick={save} disabled={saving}>{saving ? "保存中…" : "保存"}</Button></DialogFooter></DialogContent></Dialog>
      <Dialog open={Boolean(detail)} onOpenChange={(open) => !open && setDetail(null)}><DialogContent><DialogHeader><DialogTitle>点位详情</DialogTitle></DialogHeader>{detail && <div className="grid grid-cols-2 gap-x-6 gap-y-4 text-sm">{[["所属设备", detail.deviceCode], ["点位名称", detail.pointName], ["点位序号", detail.varSeq], ["IO地址", detail.ioAddress], ["数据类型", detail.dataType], ["驱动名称", detail.driverName], ["是否只读", detail.isReadonly ? "是" : "否"], ["更新时间", detail.updateTime ?? "--"]].map(([label, value]) => <div key={String(label)}><p className="text-xs text-muted-foreground">{label}</p><p className="mt-1">{value}</p></div>)}</div>}</DialogContent></Dialog>
      <AlertDialog open={batchDeleteOpen} onOpenChange={setBatchDeleteOpen}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>批量删除点位</AlertDialogTitle><AlertDialogDescription>确认删除选中的 {selectedIds.length} 个点位吗？该操作不可撤销。</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction onClick={confirmBatchDelete}>删除</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
      <AlertDialog open={Boolean(deleting)} onOpenChange={(open) => !open && setDeleting(null)}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>删除点位</AlertDialogTitle><AlertDialogDescription>确认删除点位“{deleting?.pointName}”吗？</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction onClick={confirmDelete}>删除</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    </div>
  );
}
