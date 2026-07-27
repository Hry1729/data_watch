import { useCallback, useEffect, useMemo, useRef, useState, type ChangeEvent } from "react";
import { useSearchParams } from "react-router-dom";
import { ChevronDown, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Download, Eye, Pencil, Plus, Search, Trash2, Upload } from "lucide-react";
import { toast } from "sonner";
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { useAuth } from "@/contexts/AuthContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { backendClient } from "@/lib/backend-client";
import { useAdaptivePageSize } from "@/hooks/useAdaptivePageSize";
import { parseImportWorkbook } from "@/lib/import-workbook";
import { saveDownload } from "@/lib/save-download";
import type { ImportResult, PointForm, PointListItem } from "@/types/device-monitor";

const EMPTY_FORM: PointForm = { deviceId: 0, deviceCode: "", varSeq: 1, pointName: "", ioAddress: "", dataType: "REAL", driverName: "", isReadonly: true, description: "" };
const DATA_TYPES = ["BOOL", "INT", "DINT", "REAL", "STRING", "WORD", "DWORD"] as const;

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
  const { pageSize, tableViewportRef } = useAdaptivePageSize();
  const [deviceId, setDeviceId] = useState(() => {
    const routeDeviceId = Number(searchParams.get("deviceId"));
    return Number.isInteger(routeDeviceId) && routeDeviceId > 0 ? String(routeDeviceId) : "all";
  });
  const [dialogOpen, setDialogOpen] = useState(false);
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
  const fileInputRef = useRef<HTMLInputElement>(null);
  const loadDeviceCatalog = useCallback((token: string) => backendClient.deviceList(token, { pageNo: 1, pageSize: 1000 }), []);
  const loadDrivers = useCallback((token: string) => backendClient.driverOptions(token), []);
  const deviceCatalog = useBackendQuery(loadDeviceCatalog);
  const drivers = useBackendQuery(loadDrivers);
  const devices = deviceCatalog.data?.list ?? [];
  const activeDeviceIds = useMemo(() => devices.filter((device) => (activeType === "all" || device.deviceType === activeType) && (connectStatus === "all" || device.connectStatus === connectStatus)).map((device) => device.id), [activeType, connectStatus, devices]);
  const hasDeviceFilter = activeType !== "all" || connectStatus !== "all";
  const pointDeviceIds = hasDeviceFilter ? activeDeviceIds : undefined;
  const pointQueryEnabled = !hasDeviceFilter || activeDeviceIds.length > 0;
  const loadPoints = useCallback((token: string) => backendClient.pointList(token, {
    pageNo: page,
    pageSize,
    keyword: keyword.trim() || undefined,
    deviceIds: pointDeviceIds,
    deviceId: deviceId === "all" ? undefined : Number(deviceId),
    deviceCode: deviceCodeFilter.trim() || undefined,
    dataType: dataTypeFilter === "all" ? undefined : dataTypeFilter,
  }), [dataTypeFilter, deviceCodeFilter, deviceId, keyword, page, pageSize, pointDeviceIds]);
  const points = useBackendQuery(loadPoints, { enabled: pointQueryEnabled });
  useEffect(() => {
    const routeDeviceId = Number(searchParams.get("deviceId"));
    const routeDeviceCode = searchParams.get("deviceCode");
    if (Number.isInteger(routeDeviceId) && routeDeviceId > 0) { setDeviceId(String(routeDeviceId)); return; }
    const matchedDevice = routeDeviceCode ? devices.find((device) => device.deviceCode === routeDeviceCode) : undefined;
    if (matchedDevice) {
      setDeviceId(String(matchedDevice.id));
      setDeviceCodeFilter(matchedDevice.deviceCode);
    }
  }, [devices, searchParams]);
  const rows = points.data?.list ?? [];
  const activeDeviceIdSet = useMemo(() => new Set(activeDeviceIds), [activeDeviceIds]);
  const filteredRows = useMemo(() => hasDeviceFilter ? rows.filter((row) => activeDeviceIdSet.has(row.deviceId)) : rows, [activeDeviceIdSet, hasDeviceFilter, rows]);
  const totalPages = Math.max(1, Math.ceil((points.data?.total ?? 0) / pageSize));
  const pageRows = filteredRows;
  useEffect(() => {
    setPage((value) => Math.min(value, totalPages));
  }, [totalPages]);
  useEffect(() => {
    setSelectedIds([]);
  }, [activeType, connectStatus, dataTypeFilter, deviceCodeFilter, deviceId, keyword, page, pageSize]);
  const typeTabs = useMemo(() => Array.from(new Map(devices.map((device) => [device.deviceType, device.deviceType])).entries()).map(([value, label]) => ({ value, label })), [devices]);
  const typeDevices = useMemo(
    () => devices.filter((device) => activeType === "all" || device.deviceType === activeType),
    [activeType, devices],
  );
  const pickerDevices = useMemo(() => {
    const keyword = deviceCodeDraft.trim().toLowerCase();
    if (!keyword) return typeDevices;
    return typeDevices.filter((device) =>
      `${device.deviceCode} ${device.deviceName}`.toLowerCase().includes(keyword),
    );
  }, [deviceCodeDraft, typeDevices]);
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
  function openCreate() { const first = devices[0]; setEditing(null); setForm({ ...EMPTY_FORM, deviceId: first?.id ?? 0, deviceCode: first?.deviceCode ?? "", driverName: drivers.data?.[0]?.driverName ?? "" }); setDialogOpen(true); }
  function openEdit(row: PointListItem) { setEditing(row); setForm({ deviceId: row.deviceId, deviceCode: row.deviceCode, varSeq: row.varSeq, pointName: row.pointName, ioAddress: row.ioAddress, dataType: row.dataType, driverName: row.driverName, isReadonly: row.isReadonly, description: row.description }); setDialogOpen(true); }
  async function save() {
    if (!auth?.backendToken || !form.deviceId || !form.pointName.trim() || !form.ioAddress.trim()) { toast.error("请完整填写点位信息"); return; }
    setSaving(true);
    try { if (editing) await backendClient.updatePoint(auth.backendToken, editing.id, form); else await backendClient.createPoint(auth.backendToken, form); toast.success(editing ? "点位已更新" : "点位已新增"); setDialogOpen(false); points.refresh(); deviceCatalog.refresh(); } catch { toast.error("保存点位失败"); } finally { setSaving(false); }
  }
  async function confirmDelete() { if (!auth?.backendToken || !deleting) return; try { await backendClient.deletePoint(auth.backendToken, deleting.id); toast.success("点位已删除"); points.refresh(); } catch { toast.error("删除点位失败"); } finally { setDeleting(null); } }
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
      const file = await backendClient.exportPoints(auth.backendToken, {
        keyword: keyword.trim() || undefined,
        deviceId: deviceId === "all" ? undefined : Number(deviceId),
        deviceCode: deviceCodeFilter.trim() || undefined,
        dataType: dataTypeFilter === "all" ? undefined : dataTypeFilter,
      }, template);
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

  return (
    <div className="flex h-full min-h-0 w-full flex-col gap-3 p-4 md:p-6">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex min-w-0 flex-wrap items-center gap-2">
          <Tabs value={activeType} onValueChange={(value) => { setActiveType(value); setDeviceId("all"); setDeviceCodeFilter(""); setPage(1); }}><div className="max-w-full overflow-x-auto"><TabsList className="w-max"><TabsTrigger value="all">汇总</TabsTrigger>{typeTabs.map((type) => <TabsTrigger key={type.value} value={type.value}>{type.label}</TabsTrigger>)}</TabsList></div></Tabs>
          <Button type="button" variant="outline" className="h-8 w-28 justify-between px-2 font-normal" onClick={openDevicePicker}>
            <span className={`truncate ${deviceCodeFilter ? "text-foreground" : "text-muted-foreground"}`}>{deviceCodeFilter || "设备号"}</span>
            <ChevronDown className="size-3.5 shrink-0 opacity-50" />
          </Button>
          <Select value={dataTypeFilter} onValueChange={(value) => { setDataTypeFilter(value); setPage(1); }}>
            <SelectTrigger className="!h-8 w-32"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectGroup>
                <SelectItem value="all">数据类型（全部）</SelectItem>
                {DATA_TYPES.map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}
              </SelectGroup>
            </SelectContent>
          </Select>
        </div>
        <div className="flex items-center gap-2"><div className="relative w-48"><Search className="absolute left-3 top-2 size-4 text-muted-foreground" /><Input className="h-8 pl-9" placeholder="点位名称 / IO地址" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} /></div><Select value={connectStatus} onValueChange={(value) => { setConnectStatus(value); setPage(1); }}><SelectTrigger className="!h-8 w-24"><SelectValue /></SelectTrigger><SelectContent><SelectGroup><SelectItem value="all">全部状态</SelectItem><SelectItem value="online">在线</SelectItem><SelectItem value="unstable">不稳定</SelectItem><SelectItem value="offline">离线</SelectItem></SelectGroup></SelectContent></Select></div>
      </div>
      <Card className="flex min-h-0 flex-1 flex-col border border-border">
        <CardHeader><CardTitle>点位管理</CardTitle><CardAction><div className="flex flex-wrap justify-end gap-2"><Button size="sm" onClick={openCreate}><Plus data-icon="inline-start" />新增点位</Button><Button variant="outline" size="sm" onClick={() => { setImportResult(null); setImportOpen(true); }}><Upload data-icon="inline-start" />导入</Button><Button variant="outline" size="sm" onClick={() => exportWorkbook(false)} disabled={transferring}><Download data-icon="inline-start" />导出</Button><Button variant="destructive" size="sm" disabled={!selectedIds.length} onClick={() => setBatchDeleteOpen(true)}><Trash2 data-icon="inline-start" />删除</Button></div></CardAction></CardHeader>
        <CardContent className="flex min-h-0 flex-1 flex-col gap-3">
          <div ref={tableViewportRef} className="min-h-0 flex-1 overflow-auto rounded-lg border [&_[data-slot=table-container]]:h-full"><Table className="h-full w-full table-fixed"><TableHeader><TableRow className="bg-muted/50"><TableHead className="w-12 text-center"><Checkbox aria-label="选择全部点位" checked={allSelected ? true : selectedIds.length ? "indeterminate" : false} onCheckedChange={(checked) => toggleAll(checked === true)} /></TableHead><TableHead className="w-[14%] text-center">所属设备</TableHead><TableHead className="text-center">点位名称</TableHead><TableHead className="w-[10%] text-center">点位序号</TableHead><TableHead className="w-[15%] text-center">IO地址</TableHead><TableHead className="w-[11%] text-center">数据类型</TableHead><TableHead className="w-[14%] text-center">驱动名称</TableHead><TableHead className="w-[8%] text-center">只读</TableHead><TableHead className="w-32 text-center">操作</TableHead></TableRow></TableHeader><TableBody>{points.loading ? Array.from({ length: 10 }, (_, index) => <TableRow key={index} className="h-10"><TableCell colSpan={9}><Skeleton className="h-6 w-full" /></TableCell></TableRow>) : pageRows.length ? pageRows.map((row) => <TableRow key={`${row.id}-${row.deviceId}-${row.varSeq}`} className="h-10" data-state={selectedIds.includes(row.id) ? "selected" : undefined}><TableCell className="text-center"><Checkbox aria-label={`选择点位 ${row.pointName}`} checked={selectedIds.includes(row.id)} onCheckedChange={(checked) => toggleRow(row.id, checked === true)} /></TableCell><TableCell className="truncate text-center font-medium">{row.deviceCode}</TableCell><TableCell className="truncate text-center">{row.pointName}</TableCell><TableCell className="text-center">{row.varSeq}</TableCell><TableCell className="truncate text-center font-mono text-xs">{row.ioAddress}</TableCell><TableCell className="text-center"><Badge variant="outline">{row.dataType}</Badge></TableCell><TableCell className="truncate text-center">{row.driverName}</TableCell><TableCell className="text-center">{row.isReadonly ? "是" : "否"}</TableCell><TableCell><div className="flex justify-center"><Button variant="ghost" size="icon-sm" title="查看详情" onClick={() => setDetail(row)}><Eye className="size-4" /></Button><Button variant="ghost" size="icon-sm" title="编辑点位" onClick={() => openEdit(row)}><Pencil className="size-4" /></Button><Button variant="ghost" size="icon-sm" title="删除点位" onClick={() => setDeleting(row)}><Trash2 className="size-4 text-destructive" /></Button></div></TableCell></TableRow>) : <TableRow><TableCell colSpan={9} className="h-32 text-center text-muted-foreground">{points.error ?? "暂无点位"}</TableCell></TableRow>}</TableBody></Table></div>
          <div className="flex flex-col gap-3 pt-1 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between"><span>共 {points.data?.total ?? 0} 个点位</span><div className="flex flex-wrap items-center gap-2"><span>每页 {pageSize} 行</span><span>第 {page} / {totalPages} 页</span><Button variant="outline" size="icon-sm" disabled={page === 1} onClick={() => setPage(1)}><ChevronsLeft className="size-4" /></Button><Button variant="outline" size="icon-sm" disabled={page === 1} onClick={() => setPage((value) => value - 1)}><ChevronLeft className="size-4" /></Button><Button variant="outline" size="icon-sm" disabled={page === totalPages} onClick={() => setPage((value) => value + 1)}><ChevronRight className="size-4" /></Button><Button variant="outline" size="icon-sm" disabled={page === totalPages} onClick={() => setPage(totalPages)}><ChevronsRight className="size-4" /></Button></div></div>
        </CardContent></Card>

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
            <div className="h-[300px] max-h-[40vh] overflow-y-auto rounded-md border p-2.5">
              {pickerDevices.length ? (
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 md:grid-cols-4">
                  {pickerDevices.map((device) => (
                    <button
                      key={device.id}
                      type="button"
                      className="flex min-w-0 flex-col gap-0.5 rounded-md border bg-card px-2 py-2 text-left transition-colors hover:bg-muted"
                      onClick={() => applyDeviceCode(device.deviceCode, device.id)}
                    >
                      <span className="truncate text-sm font-medium">{device.deviceCode}</span>
                      <span className="truncate text-xs text-muted-foreground">{device.deviceName}</span>
                    </button>
                  ))}
                </div>
              ) : (
                <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
                  {typeDevices.length ? "没有匹配的设备号" : "当前类型下暂无设备"}
                </div>
              )}
            </div>
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
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}><DialogContent><DialogHeader><DialogTitle>{editing ? "编辑点位" : "新增点位"}</DialogTitle></DialogHeader><div className="grid gap-4 py-2"><div className="space-y-2"><Label>所属设备</Label><Select value={form.deviceId ? String(form.deviceId) : ""} onValueChange={(value) => { const device = devices.find((item) => String(item.id) === value); setForm({ ...form, deviceId: Number(value), deviceCode: device?.deviceCode ?? "" }); }}><SelectTrigger><SelectValue placeholder="选择设备" /></SelectTrigger><SelectContent>{devices.map((device) => <SelectItem key={device.id} value={String(device.id)}>{device.deviceCode} · {device.deviceName}</SelectItem>)}</SelectContent></Select></div><div className="grid grid-cols-2 gap-3"><div className="space-y-2"><Label>点位名称</Label><Input value={form.pointName} onChange={(event) => setForm({ ...form, pointName: event.target.value })} /></div><div className="space-y-2"><Label>点位序号</Label><Input type="number" value={form.varSeq} onChange={(event) => setForm({ ...form, varSeq: Number(event.target.value) })} /></div></div><div className="grid grid-cols-2 gap-3"><div className="space-y-2"><Label>IO地址</Label><Input value={form.ioAddress} onChange={(event) => setForm({ ...form, ioAddress: event.target.value })} /></div><div className="space-y-2"><Label>数据类型</Label><Select value={form.dataType} onValueChange={(value: PointForm["dataType"]) => setForm({ ...form, dataType: value })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{DATA_TYPES.map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectContent></Select></div></div><div className="space-y-2"><Label>驱动名称</Label><Select value={form.driverName} onValueChange={(value) => setForm({ ...form, driverName: value })}><SelectTrigger><SelectValue placeholder="选择驱动" /></SelectTrigger><SelectContent>{(drivers.data ?? []).map((driver) => <SelectItem key={driver.driverName} value={driver.driverName}>{driver.driverName}</SelectItem>)}</SelectContent></Select></div><label className="flex items-center gap-2 text-sm"><Checkbox checked={form.isReadonly} onCheckedChange={(checked) => setForm({ ...form, isReadonly: checked === true })} />是否只读</label><div className="space-y-2"><Label>描述说明</Label><Textarea value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></div></div><DialogFooter><Button variant="outline" onClick={() => setDialogOpen(false)}>取消</Button><Button onClick={save} disabled={saving}>{saving ? "保存中…" : "保存"}</Button></DialogFooter></DialogContent></Dialog>
      <Dialog open={Boolean(detail)} onOpenChange={(open) => !open && setDetail(null)}><DialogContent><DialogHeader><DialogTitle>点位详情</DialogTitle></DialogHeader>{detail && <div className="grid grid-cols-2 gap-x-6 gap-y-4 text-sm">{[["所属设备", detail.deviceCode], ["点位名称", detail.pointName], ["点位序号", detail.varSeq], ["IO地址", detail.ioAddress], ["数据类型", detail.dataType], ["驱动名称", detail.driverName], ["是否只读", detail.isReadonly ? "是" : "否"], ["更新时间", detail.updateTime ?? "--"]].map(([label, value]) => <div key={String(label)}><p className="text-xs text-muted-foreground">{label}</p><p className="mt-1">{value}</p></div>)}</div>}</DialogContent></Dialog>
      <AlertDialog open={batchDeleteOpen} onOpenChange={setBatchDeleteOpen}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>批量删除点位</AlertDialogTitle><AlertDialogDescription>确认删除选中的 {selectedIds.length} 个点位吗？该操作不可撤销。</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction onClick={confirmBatchDelete}>删除</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
      <AlertDialog open={Boolean(deleting)} onOpenChange={(open) => !open && setDeleting(null)}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>删除点位</AlertDialogTitle><AlertDialogDescription>确认删除点位“{deleting?.pointName}”吗？</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction onClick={confirmDelete}>删除</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    </div>
  );
}
