import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { ChevronDown, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Eye, Search } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useAdaptivePageSize } from "@/hooks/useAdaptivePageSize";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { monitorClient } from "@/lib/monitor-client";
import { useDevicePicker } from "@/hooks/useDevicePicker";
import { DeviceCatalogPagination } from "@/components/DeviceCatalogPagination";
import { POINT_DISPLAY_DATA_TYPES } from "@/types/device-monitor";
import type { PointListItem } from "@/types/device-monitor";

/**
 * 点位名称：中心/边缘下发真实名称时直接显示；名称为空或旧快照没带名称时
 * 回退为「点位 {varSeq}」（中心在 var_name 为空时返回的是序号本身）。
 */
function displayPointName(row: { pointName: string; varSeq: number }) {
  return row.pointName && row.pointName !== String(row.varSeq)
    ? row.pointName
    : `点位 ${row.varSeq}`;
}

export default function EdgePointMonitorPage() {
  const [searchParams] = useSearchParams();
  const [keyword, setKeyword] = useState("");
  const [deviceCodeFilter, setDeviceCodeFilter] = useState("");
  const [devicePickerOpen, setDevicePickerOpen] = useState(false);
  const [deviceCodeDraft, setDeviceCodeDraft] = useState("");
  const [dataTypeFilter, setDataTypeFilter] = useState("all");
  const [activeType, setActiveType] = useState("all");
  const [connectStatus, setConnectStatus] = useState("all");
  const [page, setPage] = useState(1);
  const [detail, setDetail] = useState<PointListItem | null>(null);
  const { pageSize, tableViewportRef } = useAdaptivePageSize();
  const [deviceId, setDeviceId] = useState(() => {
    const routeDeviceId = Number(searchParams.get("deviceId"));
    return Number.isInteger(routeDeviceId) && routeDeviceId > 0 ? String(routeDeviceId) : "all";
  });

  const loadDevices = useCallback(
    (token: string, signal: AbortSignal) => monitorClient.deviceTypes(token, signal),
    [],
  );
  const deviceCatalog = useBackendQuery(loadDevices);
  const devicePicker = useDevicePicker(monitorClient, deviceCodeDraft, activeType, devicePickerOpen);
  const pickerDevices = devicePicker.data?.list ?? [];
  const loadPoints = useCallback(
    (token: string, signal: AbortSignal) => monitorClient.pointList(token, {
      pageNo: page,
      pageSize,
      keyword: keyword.trim() || undefined,
      deviceType: activeType === "all" ? undefined : activeType,
      connectStatus: connectStatus === "all" ? undefined : connectStatus,
      deviceId: deviceId === "all" ? undefined : Number(deviceId),
      deviceCode: deviceCodeFilter.trim() || undefined,
      dataType: dataTypeFilter === "all" ? undefined : dataTypeFilter,
    }, signal),
    [activeType, connectStatus, dataTypeFilter, deviceCodeFilter, deviceId, keyword, page, pageSize],
  );
  const points = useBackendQuery(loadPoints, { intervalMs: 5_000 });
  const routeDeviceCode = searchParams.get("deviceCode");
  const loadRouteDevice = useCallback((token: string, signal: AbortSignal) => monitorClient.deviceList(token, {
    pageNo: 1, pageSize: 1, deviceCodeExact: routeDeviceCode ?? undefined,
  }, signal), [routeDeviceCode]);
  const routeDevice = useBackendQuery(loadRouteDevice, { enabled: Boolean(routeDeviceCode) && !searchParams.get("deviceId") });

  useEffect(() => {
    const routeDeviceId = Number(searchParams.get("deviceId"));
    const routeDeviceCode = searchParams.get("deviceCode");
    if (Number.isInteger(routeDeviceId) && routeDeviceId > 0) {
      setDeviceId(String(routeDeviceId));
      return;
    }
    const matchedDevice = routeDevice.data?.list.find((device) => device.deviceCode === routeDeviceCode);
    if (matchedDevice) {
      setDeviceId(String(matchedDevice.id));
      setDeviceCodeFilter(matchedDevice.deviceCode);
    }
  }, [routeDevice.data, searchParams]);

  const rows = points.data?.list ?? [];
  const totalPages = Math.max(1, Math.ceil((points.data?.total ?? 0) / pageSize));
  const source = points.data?.source ?? devicePicker.data?.source ?? "center";
  const typeTabs = useMemo(
    () => (deviceCatalog.data ?? []).map((value) => ({ value, label: value })),
    [deviceCatalog.data],
  );

  useEffect(() => {
    setPage((current) => Math.min(current, totalPages));
  }, [totalPages]);

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

  return (
    <div className="flex h-full min-h-0 w-full flex-col">
      <Card className="flex min-h-0 flex-1 flex-col gap-0 py-0">
        <div className="flex flex-wrap items-center gap-2 px-4 pt-4">
          <Tabs value={activeType} onValueChange={(value) => { setActiveType(value); setDeviceId("all"); setDeviceCodeFilter(""); setPage(1); }}>
            <div className="max-w-full overflow-x-auto">
              <TabsList className="w-max">
                <TabsTrigger value="all">汇总</TabsTrigger>
                {typeTabs.map((type) => <TabsTrigger key={type.value} value={type.value}>{type.label}</TabsTrigger>)}
              </TabsList>
            </div>
          </Tabs>
          <Button type="button" variant="outline" className="h-8 w-28 justify-between px-2 font-normal" onClick={openDevicePicker}>
            <span className={deviceCodeFilter ? "truncate text-foreground" : "truncate text-muted-foreground"}>{deviceCodeFilter || "设备号"}</span>
            <ChevronDown className="size-3.5 shrink-0 opacity-50" />
          </Button>
          <Select value={dataTypeFilter} onValueChange={(value) => { setDataTypeFilter(value); setPage(1); }}>
            <SelectTrigger className="!h-8 w-32"><SelectValue /></SelectTrigger>
            <SelectContent><SelectGroup><SelectItem value="all">数据类型（全部）</SelectItem>{POINT_DISPLAY_DATA_TYPES.map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectGroup></SelectContent>
          </Select>
          <div className="relative w-48"><Search className="absolute left-3 top-2 size-4 text-muted-foreground" /><Input className="h-8 pl-9" placeholder="点位名称 / IO地址" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} /></div>
          <Select value={connectStatus} onValueChange={(value) => { setConnectStatus(value); setPage(1); }}>
            <SelectTrigger className="!h-8 w-24"><SelectValue /></SelectTrigger>
            <SelectContent><SelectGroup><SelectItem value="all">全部状态</SelectItem><SelectItem value="online">在线</SelectItem><SelectItem value="unstable">不稳定</SelectItem><SelectItem value="offline">离线</SelectItem></SelectGroup></SelectContent>
          </Select>
          <Badge variant="outline" className={`ml-auto ${source === "local" ? "border-warning/30 bg-warning-soft text-warning" : "border-success/30 bg-success-soft text-success"}`}>{source === "local" ? "本机快照" : "中心数据"}</Badge>
        </div>
        <CardContent className="flex min-h-0 flex-1 flex-col gap-3 pt-3">
          <div ref={tableViewportRef} className="min-h-0 flex-1 overflow-auto rounded-lg border [&_[data-slot=table-container]]:h-full">
            <Table className="h-full w-full table-fixed">
              <TableHeader><TableRow className="bg-muted/50"><TableHead className="w-[15%] text-center">所属设备</TableHead><TableHead className="text-center">点位名称</TableHead><TableHead className="w-[10%] text-center">点位序号</TableHead><TableHead className="w-[17%] text-center">IO地址</TableHead><TableHead className="w-[11%] text-center">数据类型</TableHead><TableHead className="w-[15%] text-center">驱动名称</TableHead><TableHead className="w-[8%] text-center">只读</TableHead><TableHead className="w-20 text-center">操作</TableHead></TableRow></TableHeader>
              <TableBody>{points.loading ? Array.from({ length: 10 }, (_, index) => <TableRow key={index} className="h-10"><TableCell colSpan={8}><Skeleton className="h-6 w-full" /></TableCell></TableRow>) : rows.length ? rows.map((row) => <TableRow key={`${row.id}-${row.deviceId}-${row.varSeq}`} className="h-10"><TableCell className="truncate text-center font-medium">{row.deviceCode}</TableCell><TableCell className="truncate text-center">{displayPointName(row)}</TableCell><TableCell className="text-center">{row.varSeq}</TableCell><TableCell className="truncate text-center font-mono text-xs">{row.ioAddress}</TableCell><TableCell className="text-center"><Badge variant="outline">{row.dataType}</Badge></TableCell><TableCell className="truncate text-center">{row.driverName || "--"}</TableCell><TableCell className="text-center">{row.isReadonly ? "是" : "否"}</TableCell><TableCell><div className="flex justify-center"><Button variant="ghost" size="icon-sm" title="查看详情" onClick={() => setDetail(row)}><Eye className="size-4" /></Button></div></TableCell></TableRow>) : <TableRow><TableCell colSpan={8} className="h-32 text-center text-muted-foreground">{points.error ?? "暂无点位"}</TableCell></TableRow>}</TableBody>
            </Table>
          </div>
          <div className="flex flex-col gap-3 pt-1 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between"><span>共 {points.data?.total ?? 0} 个点位</span><div className="flex flex-wrap items-center gap-2"><span>每页 {pageSize} 行</span><span>第 {page} / {totalPages} 页</span><Button variant="outline" size="icon-sm" title="第一页" disabled={page === 1} onClick={() => setPage(1)}><ChevronsLeft className="size-4" /></Button><Button variant="outline" size="icon-sm" title="上一页" disabled={page === 1} onClick={() => setPage((value) => value - 1)}><ChevronLeft className="size-4" /></Button><Button variant="outline" size="icon-sm" title="下一页" disabled={page === totalPages} onClick={() => setPage((value) => value + 1)}><ChevronRight className="size-4" /></Button><Button variant="outline" size="icon-sm" title="最后一页" disabled={page === totalPages} onClick={() => setPage(totalPages)}><ChevronsRight className="size-4" /></Button></div></div>
        </CardContent>
      </Card>

      <Dialog open={devicePickerOpen} onOpenChange={setDevicePickerOpen}>
        <DialogContent className="flex max-h-[85vh] w-[min(600px,90vw)] max-w-none flex-col gap-3 sm:max-w-none">
          <DialogHeader><DialogTitle>选择设备号</DialogTitle><DialogDescription>{activeType === "all" ? "可手打设备号，或从本机设备中选择。" : `可手打设备号，或从「${activeType}」类型设备中选择。`}</DialogDescription></DialogHeader>
          <div className="flex min-h-0 flex-1 flex-col gap-3"><div className="relative shrink-0"><Search className="absolute left-3 top-2 size-4 text-muted-foreground" /><Input className="h-8 pl-9" placeholder="手打或搜索设备号 / 名称" value={deviceCodeDraft} onChange={(event) => setDeviceCodeDraft(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter") applyDeviceCode(deviceCodeDraft.trim()); }} /></div><div className="h-[300px] max-h-[40vh] overflow-y-auto rounded-md border p-2.5">{devicePicker.loading ? <Skeleton className="h-full w-full" /> : pickerDevices.length ? <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 md:grid-cols-4">{pickerDevices.map((device) => <button key={device.id} type="button" className="flex min-w-0 flex-col gap-0.5 rounded-md border bg-card px-2 py-2 text-left transition-colors hover:bg-muted" onClick={() => applyDeviceCode(device.deviceCode, device.id)}><span className="truncate text-sm font-medium">{device.deviceCode}</span><span className="truncate text-xs text-muted-foreground">{device.deviceName}</span></button>)}</div> : <div className="flex h-full items-center justify-center text-sm text-muted-foreground">{devicePicker.error ?? "没有匹配的设备号"}</div>}</div></div>
          <DeviceCatalogPagination total={devicePicker.data?.total ?? 0} page={devicePicker.page} pageSize={devicePicker.pageSize} loading={devicePicker.loading} onPageChange={devicePicker.setPage} />
          <DialogFooter className="sm:justify-between"><Button type="button" variant="ghost" onClick={() => applyDeviceCode("")}>清空</Button><div className="flex gap-2"><Button type="button" variant="outline" onClick={() => setDevicePickerOpen(false)}>取消</Button><Button type="button" onClick={() => applyDeviceCode(deviceCodeDraft.trim())}>确定</Button></div></DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(detail)} onOpenChange={(open) => !open && setDetail(null)}><DialogContent><DialogHeader><DialogTitle>点位详情</DialogTitle></DialogHeader>{detail && <div className="grid grid-cols-2 gap-x-6 gap-y-4 text-sm">{[["所属设备", detail.deviceCode], ["点位名称", displayPointName(detail)], ["点位序号", detail.varSeq], ["IO地址", detail.ioAddress], ["数据类型", detail.dataType], ["驱动名称", detail.driverName], ["是否只读", detail.isReadonly ? "是" : "否"], ["更新时间", detail.updateTime ?? "--"]].map(([label, value]) => <div key={String(label)}><p className="text-xs text-muted-foreground">{label}</p><p className="mt-1">{value}</p></div>)}</div>}</DialogContent></Dialog>
    </div>
  );
}
