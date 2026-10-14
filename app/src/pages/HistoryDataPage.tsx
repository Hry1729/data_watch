import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  Brush,
  CartesianGrid,
  Line,
  LineChart,
  Scatter,
  ScatterChart,
  XAxis,
  YAxis,
} from "recharts";
import { Download, RotateCcw, Search } from "lucide-react";
import { BUILD_ROLE } from "@/config/build-role";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart";
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
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { monitorClient } from "@/lib/monitor-client";
import { saveDownload } from "@/lib/save-download";
import type { AppError } from "@/types";
import type { HistoryChartResponse, HistoryRawMatrixResponse } from "@/types/device-monitor";

const COLORS = ["var(--chart-1)", "var(--chart-2)", "var(--chart-3)", "var(--chart-4)", "var(--chart-5)"];
const QUERY_POINT_LIMIT = 10;
const EXPORT_POINT_LIMIT = 20;
const HISTORY_STATE_KEY = "datawatch-history-state";
const INTERVALS = [["auto", "自动"], ["100ms", "100毫秒"], ["500ms", "500毫秒"], ["1s", "1秒"], ["5s", "5秒"], ["30s", "30秒"], ["1m", "1分钟"], ["5m", "5分钟"]] as const;

type ChartType = "line" | "area" | "bar" | "scatter";
type AlignGranularity = "100ms" | "500ms" | "1s";

interface SavedHistoryState {
  deviceId?: number;
  selectedPoints?: number[];
  startTime?: string;
  endTime?: string;
  interval?: string;
  aggregate?: string;
  chartType?: ChartType;
  matrixAlign?: AlignGranularity;
}

function loadSavedHistoryState(): SavedHistoryState {
  try {
    return JSON.parse(localStorage.getItem(HISTORY_STATE_KEY) ?? "{}") as SavedHistoryState;
  } catch {
    return {};
  }
}

function localInput(date: Date) {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}

function apiTime(value: string) {
  return `${value.replace("T", " ")}:00`;
}

function historyErrorMessage(reason: unknown, fallback: string) {
  const error = reason as AppError | undefined;
  const message = error && "message" in error ? error.message.trim() : "";
  return message || fallback;
}

function setRecentRange(hours: number, setStart: (value: string) => void, setEnd: (value: string) => void) {
  const end = new Date();
  setEnd(localInput(end));
  setStart(localInput(new Date(end.getTime() - hours * 60 * 60_000)));
}

function parseHistoryTime(value: string): number {
  const timestamp = new Date(value.includes("T") ? value : value.replace(" ", "T")).getTime();
  return Number.isFinite(timestamp) ? timestamp : 0;
}

function formatHistoryAxisTime(timestamp: number, spanMs: number): string {
  const date = new Date(timestamp);
  if (!Number.isFinite(date.getTime())) return "--";
  if (spanMs >= 6 * 60 * 60_000) {
    return date.toLocaleString("zh-CN", {
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
    });
  }
  return date.toLocaleTimeString("zh-CN", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  });
}

function HistoryChart({
  chart,
  chartType,
  rangeStart,
  rangeEnd,
}: {
  chart: HistoryChartResponse;
  chartType: ChartType;
  rangeStart: string;
  rangeEnd: string;
}) {
  const [zoomDomain, setZoomDomain] = useState<[number, number] | null>(null);
  const [brushVersion, setBrushVersion] = useState(0);
  const chartRows = useMemo(() => {
    const rows = new Map<string, Record<string, number | null>>();
    chart.series.forEach((series) => series.data.forEach(([time, value]) => {
      const row = rows.get(time) ?? { time: parseHistoryTime(time) };
      row[`v${series.varSeq}`] = value;
      rows.set(time, row);
    }));
    return [...rows.values()].sort((left, right) => Number(left.time) - Number(right.time));
  }, [chart]);
  const config = useMemo(() => Object.fromEntries(chart.series.map((series, index) => [`v${series.varSeq}`, { label: series.name, color: COLORS[index % COLORS.length] }])) as ChartConfig, [chart]);
  const fullDomain = useMemo<[number, number]>(() => {
    const requestedStart = parseHistoryTime(rangeStart);
    const requestedEnd = parseHistoryTime(rangeEnd);
    if (requestedStart > 0 && requestedEnd > requestedStart) return [requestedStart, requestedEnd];
    const fallbackStart = Number(chartRows[0]?.time ?? Date.now() - 60_000);
    const fallbackEnd = Number(chartRows[chartRows.length - 1]?.time ?? Date.now());
    return fallbackEnd > fallbackStart ? [fallbackStart, fallbackEnd] : [fallbackStart, fallbackStart + 1000];
  }, [chartRows, rangeEnd, rangeStart]);
  const axisDomain = zoomDomain ?? fullDomain;
  const axisSpan = axisDomain[1] - axisDomain[0];

  function handleBrushChange(range: { startIndex?: number; endIndex?: number } | null) {
    if (!range || chartRows.length < 2) return;
    const startIndex = range.startIndex ?? 0;
    const endIndex = range.endIndex ?? chartRows.length - 1;
    if (startIndex <= 0 && endIndex >= chartRows.length - 1) {
      setZoomDomain(null);
      return;
    }
    const startTime = Number(chartRows[startIndex]?.time);
    const endTime = Number(chartRows[endIndex]?.time);
    if (Number.isFinite(startTime) && Number.isFinite(endTime) && endTime > startTime) {
      setZoomDomain([startTime, endTime]);
    }
  }

  function resetZoom() {
    setZoomDomain(null);
    setBrushVersion((current) => current + 1);
  }

  const timeAxis = (
    <XAxis
      dataKey="time"
      type="number"
      scale="time"
      domain={axisDomain}
      allowDataOverflow
      tickLine={false}
      axisLine={false}
      minTickGap={28}
      tickFormatter={(value) => formatHistoryAxisTime(Number(value), axisSpan)}
    />
  );
  const tooltip = (
    <ChartTooltip
      labelFormatter={(value) => new Date(Number(value)).toLocaleString("zh-CN", { hour12: false })}
      content={<ChartTooltipContent indicator="line" />}
    />
  );
  const brush = chartRows.length > 1 ? (
    <Brush
      key={brushVersion}
      dataKey="time"
      height={24}
      travellerWidth={8}
      stroke="var(--border)"
      fill="var(--muted)"
      tickFormatter={(value) => formatHistoryAxisTime(Number(value), fullDomain[1] - fullDomain[0])}
      onChange={handleBrushChange}
    />
  ) : null;
  const common = <><CartesianGrid vertical={false} />{timeAxis}<YAxis tickLine={false} axisLine={false} width={52} />{tooltip}<ChartLegend content={<ChartLegendContent />} />{brush}</>;
  const zoomReset = zoomDomain ? (
    <Button className="absolute right-3 top-2" size="icon-sm" variant="outline" title="重置缩放" aria-label="重置缩放" onClick={resetZoom}>
      <RotateCcw />
    </Button>
  ) : null;

  if (chartType === "scatter") {
    return (
      <div className="relative">
        <ChartContainer config={config} className="h-[420px] w-full aspect-auto">
          <ScatterChart data={chartRows} accessibilityLayer>
            <CartesianGrid />
            {timeAxis}
            <YAxis type="number" dataKey="value" tickLine={false} axisLine={false} width={52} />
            {tooltip}
            <ChartLegend content={<ChartLegendContent />} />
            {chart.series.map((series, index) => <Scatter key={series.varSeq} name={series.name} fill={COLORS[index % COLORS.length]} data={series.data.filter(([, value]) => value != null).map(([time, value]) => ({ time: parseHistoryTime(time), value }))} />)}
            {brush}
          </ScatterChart>
        </ChartContainer>
        {zoomReset}
      </div>
    );
  }
  if (chartType === "bar") {
    return <div className="relative"><ChartContainer config={config} className="h-[420px] w-full aspect-auto"><BarChart data={chartRows} accessibilityLayer>{common}{chart.series.map((series, index) => <Bar key={series.varSeq} dataKey={`v${series.varSeq}`} fill={COLORS[index % COLORS.length]} radius={[3, 3, 0, 0]} isAnimationActive={false} />)}</BarChart></ChartContainer>{zoomReset}</div>;
  }
  if (chartType === "area") {
    return <div className="relative"><ChartContainer config={config} className="h-[420px] w-full aspect-auto"><AreaChart data={chartRows} accessibilityLayer>{common}{chart.series.map((series, index) => <Area key={series.varSeq} type="monotone" dataKey={`v${series.varSeq}`} stroke={COLORS[index % COLORS.length]} fill={COLORS[index % COLORS.length]} fillOpacity={0.16} strokeWidth={2} isAnimationActive={false} />)}</AreaChart></ChartContainer>{zoomReset}</div>;
  }
  return <div className="relative"><ChartContainer config={config} className="h-[420px] w-full aspect-auto"><LineChart data={chartRows} accessibilityLayer>{common}{chart.series.map((series, index) => <Line key={series.varSeq} type="monotone" dataKey={`v${series.varSeq}`} stroke={COLORS[index % COLORS.length]} strokeWidth={2} dot={false} isAnimationActive={false} />)}</LineChart></ChartContainer>{zoomReset}</div>;
}

export default function HistoryDataPage() {
  const savedState = useMemo(loadSavedHistoryState, []);
  const { auth, logout } = useAuth();
  const { refreshVersion } = usePageRefresh();
  const token = auth?.backendToken || (BUILD_ROLE === "edge" ? auth?.agentToken ?? "" : "");
  const [deviceId, setDeviceId] = useState(savedState.deviceId ?? 0);
  const [selectedPoints, setSelectedPoints] = useState<number[]>(savedState.selectedPoints ?? []);
  const [startTime, setStartTime] = useState(() => savedState.startTime ?? localInput(new Date(Date.now() - 24 * 60 * 60_000)));
  const [endTime, setEndTime] = useState(() => savedState.endTime ?? localInput(new Date()));
  const [interval, setIntervalValue] = useState(savedState.interval ?? "auto");
  const [aggregate, setAggregate] = useState(savedState.aggregate ?? "avg");
  const [chartType, setChartType] = useState<ChartType>(savedState.chartType ?? "line");
  const [chart, setChart] = useState<HistoryChartResponse | null>(null);
  const [matrix, setMatrix] = useState<HistoryRawMatrixResponse | null>(null);
  const [matrixAlign, setMatrixAlign] = useState<AlignGranularity>(savedState.matrixAlign ?? "1s");
  const [loading, setLoading] = useState(false);
  const [matrixLoading, setMatrixLoading] = useState(false);
  const [pointDialogOpen, setPointDialogOpen] = useState(false);
  const [pointSearch, setPointSearch] = useState("");
  const [draftPoints, setDraftPoints] = useState<number[]>([]);
  const [deviceDialogOpen, setDeviceDialogOpen] = useState(false);
  const [deviceSearch, setDeviceSearch] = useState("");
  const [draftDeviceType, setDraftDeviceType] = useState("all");
  const [draftDeviceId, setDraftDeviceId] = useState("");
  const [quickRange, setQuickRange] = useState("");
  const [exportDialogOpen, setExportDialogOpen] = useState(false);
  const [exportPoints, setExportPoints] = useState<number[]>([]);
  const [exportStart, setExportStart] = useState(startTime);
  const [exportEnd, setExportEnd] = useState(endTime);
  const [exporting, setExporting] = useState(false);
  const [exportProgress, setExportProgress] = useState(0);
  const lastQueryRef = useRef<{ query: ReturnType<typeof buildQuery> } | null>(null);
  const matrixAlignRef = useRef<AlignGranularity>(matrixAlign);
  const queryVersionRef = useRef(0);
  const matrixVersionRef = useRef(0);
  useEffect(() => {
    ++queryVersionRef.current;
    ++matrixVersionRef.current;
    setLoading(false);
    setMatrixLoading(false);
    return () => {
      ++queryVersionRef.current;
      ++matrixVersionRef.current;
    };
  }, [deviceId, selectedPoints, startTime, endTime, interval, aggregate, token]);

  const loadDevices = useCallback((backendToken: string) => monitorClient.historyDevices(backendToken), []);
  const loadTypes = useCallback((backendToken: string) => monitorClient.historyDeviceTypes(backendToken), []);
  const loadPoints = useCallback((backendToken: string) => monitorClient.historyPoints(backendToken, deviceId), [deviceId]);
  const devices = useBackendQuery(loadDevices);
  const types = useBackendQuery(loadTypes);
  const points = useBackendQuery(loadPoints, { enabled: deviceId > 0 });
  const pointList = points.data ?? [];
  const currentDevice = devices.data?.find((device) => device.id === deviceId);
  const selectedPointItems = pointList.filter((point) => selectedPoints.includes(point.varSeq));
  const filteredPoints = pointList.filter((point) => `${point.pointName} ${point.ioAddress}`.toLowerCase().includes(pointSearch.trim().toLowerCase()));
  const filteredDevices = useMemo(() => {
    const keyword = deviceSearch.trim().toLowerCase();
    return (devices.data ?? []).filter((device) => {
      if (!device.id) return false;
      if (draftDeviceType !== "all" && device.typeCode !== draftDeviceType) return false;
      return !keyword || `${device.deviceCode ?? ""} ${device.deviceName ?? device.name}`.toLowerCase().includes(keyword);
    });
  }, [deviceSearch, devices.data, draftDeviceType]);

  useEffect(() => {
    matrixAlignRef.current = matrixAlign;
  }, [matrixAlign]);

  useEffect(() => {
    localStorage.setItem(HISTORY_STATE_KEY, JSON.stringify({
      deviceId,
      selectedPoints,
      startTime,
      endTime,
      interval,
      aggregate,
      chartType,
      matrixAlign,
    } satisfies SavedHistoryState));
  }, [aggregate, chartType, deviceId, endTime, interval, matrixAlign, selectedPoints, startTime]);

  useEffect(() => {
    const snapshot = lastQueryRef.current;
    if (!token || !snapshot || refreshVersion === 0 || loading) return;
    let active = true;
    const queryVersion = ++queryVersionRef.current;
    const matrixVersion = ++matrixVersionRef.current;
    setLoading(true);
    setMatrixLoading(true);
    Promise.all([
      monitorClient.historyChart(token, snapshot.query),
      monitorClient.historyRawMatrix(token, {
        ...snapshot.query,
        alignGranularity: matrixAlignRef.current,
        pageSize: 200,
      }),
    ])
      .then(([nextChart, nextMatrix]) => {
        if (!active || queryVersion !== queryVersionRef.current) return;
        setChart(nextChart);
        if (matrixVersion === matrixVersionRef.current) setMatrix(nextMatrix);
      })
      .catch((reason) => {
        if (!active || queryVersion !== queryVersionRef.current) return;
        if ((reason as { type?: string })?.type === "unauthorized") logout();
        else toast.error(historyErrorMessage(reason, "历史数据刷新失败"));
      })
      .finally(() => {
        if (!active || queryVersion !== queryVersionRef.current) return;
        setLoading(false);
        if (matrixVersion === matrixVersionRef.current) setMatrixLoading(false);
      });
    return () => { active = false; };
  }, [logout, refreshVersion, token]);

  useEffect(() => {
    if (!deviceId && devices.data?.length) setDeviceId(devices.data.find((item) => item.id)?.id ?? 0);
  }, [deviceId, devices.data]);

  function changeDevice(value: string) {
    ++queryVersionRef.current;
    ++matrixVersionRef.current;
    setDeviceId(Number(value));
    setSelectedPoints([]);
    setChart(null);
    setMatrix(null);
    lastQueryRef.current = null;
  }

  function openDeviceDialog() {
    setDeviceSearch("");
    setDraftDeviceType(currentDevice?.typeCode ?? "all");
    setDraftDeviceId(deviceId ? String(deviceId) : "");
    setDeviceDialogOpen(true);
  }

  function confirmDevice() {
    if (!draftDeviceId) { toast.warning("请选择设备编号"); return; }
    setDeviceDialogOpen(false);
    changeDevice(draftDeviceId);
  }

  function openPointDialog() {
    setDraftPoints(selectedPoints);
    setPointSearch("");
    setPointDialogOpen(true);
  }

  function confirmPoints() {
    ++queryVersionRef.current;
    ++matrixVersionRef.current;
    setSelectedPoints(draftPoints);
    setChart(null);
    setMatrix(null);
    lastQueryRef.current = null;
    setPointDialogOpen(false);
  }

  function buildQuery() {
    return { deviceId, varSeqs: selectedPoints, startTime: apiTime(startTime), endTime: apiTime(endTime), interval, aggregate };
  }

  function validateQuery() {
    if (!deviceId) { toast.warning("请选择设备"); return false; }
    if (!selectedPoints.length) { toast.warning("请选择至少一个点位"); return false; }
    if (selectedPoints.length > QUERY_POINT_LIMIT) { toast.warning(`单次历史查询最多选择 ${QUERY_POINT_LIMIT} 个点位`); return false; }
    const start = new Date(startTime).getTime();
    const end = new Date(endTime).getTime();
    if (!Number.isFinite(start) || !Number.isFinite(end) || start >= end) { toast.warning("请选择正确的时间范围"); return false; }
    if (end - start > 24 * 60 * 60_000) { toast.warning("历史查询时间范围不能超过 24 小时"); return false; }
    return true;
  }

  async function loadMatrix(granularity: AlignGranularity, reset: boolean, cursorTs?: string, query?: ReturnType<typeof buildQuery>) {
    const effectiveQuery = query ?? lastQueryRef.current?.query;
    if (!token || !effectiveQuery?.varSeqs.length) return;
    const matrixVersion = ++matrixVersionRef.current;
    const queryVersion = queryVersionRef.current;
    const isCurrent = () => matrixVersion === matrixVersionRef.current && queryVersion === queryVersionRef.current;
    setMatrixLoading(true);
    try {
      const result = await monitorClient.historyRawMatrix(token, { ...effectiveQuery, alignGranularity: granularity, pageSize: 200, cursorTs });
      if (!isCurrent()) return;
      setMatrix((current) => reset || !current ? result : { ...result, rows: [...current.rows, ...result.rows] });
    } catch (reason) {
      if (!isCurrent()) return;
      if ((reason as { type?: string })?.type === "unauthorized") logout();
      else toast.error(historyErrorMessage(reason, "历史数据矩阵加载失败"));
    } finally {
      if (isCurrent()) setMatrixLoading(false);
    }
  }

  async function queryHistory() {
    if (!token || !validateQuery()) return;
    const query = buildQuery();
    const queryVersion = ++queryVersionRef.current;
    ++matrixVersionRef.current;
    const isCurrent = () => queryVersion === queryVersionRef.current;
    setLoading(true);
    setChart(null);
    setMatrix(null);
    try {
      const result = await monitorClient.historyChart(token, query);
      if (!isCurrent()) return;
      lastQueryRef.current = { query };
      setChart(result);
      await loadMatrix(matrixAlign, true, undefined, query);
    } catch (reason) {
      if (!isCurrent()) return;
      if ((reason as { type?: string })?.type === "unauthorized") logout();
      else toast.error(historyErrorMessage(reason, "历史数据查询失败"));
    } finally {
      if (isCurrent()) setLoading(false);
    }
  }

  function openExportDialog() {
    if (!chart?.series.length) return;
    setExportPoints(selectedPoints);
    setExportStart(startTime);
    setExportEnd(endTime);
    setExportProgress(0);
    setExportDialogOpen(true);
  }

  async function exportHistory() {
    if (!token || !deviceId || !exportPoints.length) return toast.warning("请选择要导出的点位");
    if (exportPoints.length > EXPORT_POINT_LIMIT) return toast.warning(`单次导出最多选择 ${EXPORT_POINT_LIMIT} 个点位`);
    setExporting(true);
    setExportProgress(0);
    try {
      const { taskId } = await monitorClient.historyExport(token, { deviceId, varSeqs: exportPoints, startTime: apiTime(exportStart), endTime: apiTime(exportEnd) });
      let completed = false;
      for (let attempt = 0; attempt < 120; attempt += 1) {
        const status = await monitorClient.historyExportStatus(token, taskId);
        setExportProgress(status.progress ?? 0);
        if (status.status === "completed") { completed = true; break; }
        if (status.status === "failed") throw new Error(status.error || "历史数据导出失败");
        await new Promise((resolve) => window.setTimeout(resolve, 750));
      }
      if (!completed) throw new Error("导出任务等待超时");
      const file = await monitorClient.historyExportDownload(token, taskId);
      saveDownload(file.bytes, file.fileName, "text/csv;charset=utf-8");
      setExportProgress(100);
      toast.success("历史数据已导出");
      setExportDialogOpen(false);
    } catch (reason) {
      if ((reason as { type?: string })?.type === "unauthorized") logout();
      else toast.error(reason instanceof Error ? reason.message : (reason as { message?: string })?.message ?? "历史数据导出失败");
    } finally {
      setExporting(false);
    }
  }

  const pointSummary = selectedPointItems.length ? selectedPointItems.map((point) => point.pointName).join("、") : "点击选择查询点位";
  const submittedQuery = lastQueryRef.current?.query;

  return (
    <div className="mx-auto w-full max-w-[1800px] space-y-4 px-4 pb-4 md:px-6 md:pb-6">
      <div className="flex flex-wrap items-center gap-1">
        <div className="flex items-center gap-1"><span className="text-xs text-muted-foreground">当前设备</span><Button variant="outline" className="max-w-56" onClick={openDeviceDialog}><span className="truncate">{currentDevice ? currentDevice.deviceCode ?? currentDevice.name : "选择设备"}</span></Button></div>
        <Button variant="outline" onClick={openPointDialog} disabled={!deviceId}><Search className="size-3.5" />选择点位{selectedPoints.length > 0 && <Badge variant="secondary" className="ml-1">{selectedPoints.length}</Badge>}</Button>
        <Select value={quickRange} onValueChange={(value) => { setQuickRange(value); setRecentRange(Number(value), setStartTime, setEndTime); }}>
          <SelectTrigger className="h-7 w-[110px]" aria-label="快捷时间范围"><SelectValue placeholder="快捷时间范围" /></SelectTrigger>
          <SelectContent>{[["1", "最近1小时"], ["6", "最近6小时"], ["12", "最近12小时"], ["24", "最近24小时"]].map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}</SelectContent>
        </Select>
        <div className="flex items-center gap-1" aria-label="时间范围"><Input className="h-7 w-[148px]" type="datetime-local" value={startTime} onChange={(event) => setStartTime(event.target.value)} /><Input className="h-7 w-[148px]" type="datetime-local" value={endTime} onChange={(event) => setEndTime(event.target.value)} /></div>
        <Select value={interval} onValueChange={setIntervalValue}><SelectTrigger className="!h-7 w-20" aria-label="时间对齐颗粒度"><SelectValue /></SelectTrigger><SelectContent>{INTERVALS.map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}</SelectContent></Select>
        <Select value={aggregate} onValueChange={setAggregate}><SelectTrigger className="!h-7 w-20" aria-label="值统计"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="avg">平均值</SelectItem><SelectItem value="max">最大值</SelectItem><SelectItem value="min">最小值</SelectItem><SelectItem value="last">最新值</SelectItem></SelectContent></Select>
        {/* Matrix alignment is configured with the data table below. */}
        <div className="ml-auto flex items-center gap-1">
          <Button variant="outline" onClick={openExportDialog} disabled={!chart?.series.length}><Download className="size-4" />导出</Button>
          <Button onClick={() => void queryHistory()} disabled={loading || !deviceId || !selectedPoints.length}><Search className="size-4" />{loading ? "查询中..." : "查询"}</Button>
        </div>
      </div>

      <Card className="gap-0 py-0" title={pointSummary}>
        <CardContent className="p-2">
          {selectedPointItems.length ? <div className="overflow-x-auto"><div className="flex min-w-max gap-2 pb-2 pr-2">{selectedPointItems.map((point) => <div key={point.varSeq} className="w-40 shrink-0 rounded-md border bg-card px-2.5 py-1.5"><p className="truncate text-[11px] font-medium">{point.pointName}</p><p className="mt-0.5 truncate font-mono text-[10px] text-muted-foreground">{point.ioAddress}</p></div>)}</div></div> : <div className="flex h-20 flex-col items-center justify-center gap-1 text-sm text-muted-foreground"><p className="font-medium text-foreground">已选点位</p><div className="flex items-center gap-2"><Search className="size-4 opacity-50" />暂未选择查询点位</div></div>}
        </CardContent>
      </Card>

      <Card className="min-w-0">
        <CardHeader className="!flex items-center justify-between gap-3 border-b px-4 pb-2 pt-4">
          <CardTitle>历史趋势图分析</CardTitle>
          <CardAction>
            <div className="flex items-center gap-1">{(["line", "area", "bar", "scatter"] as ChartType[]).map((type) => <Button key={type} size="sm" variant={chartType === type ? "secondary" : "ghost"} onClick={() => setChartType(type)}>{type === "line" ? "折线图" : type === "area" ? "面积图" : type === "bar" ? "柱状图" : "散点图"}</Button>)}</div>
          </CardAction>
        </CardHeader>
        <CardContent>{loading ? <Skeleton className="h-[420px]" /> : chart?.series.length ? <HistoryChart key={(submittedQuery?.startTime ?? "") + "-" + (submittedQuery?.endTime ?? "")} chart={chart} chartType={chartType} rangeStart={submittedQuery?.startTime ?? apiTime(startTime)} rangeEnd={submittedQuery?.endTime ?? apiTime(endTime)} /> : <div className="flex h-[420px] items-center justify-center text-sm text-muted-foreground">请选择设备、点位和时间范围后查询</div>}</CardContent>
      </Card>

      <Card><CardHeader className="!flex flex-row items-center justify-between space-y-0"><CardTitle className="text-base">历史数据矩阵</CardTitle><CardAction><div className="flex items-center gap-2"><Label className="text-xs text-muted-foreground">对齐粒度</Label><Select value={matrixAlign} onValueChange={(value) => { const next = value as AlignGranularity; setMatrixAlign(next); void loadMatrix(next, true); }}><SelectTrigger className="w-28"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="1s">1秒</SelectItem><SelectItem value="500ms">500毫秒</SelectItem><SelectItem value="100ms">100毫秒</SelectItem></SelectContent></Select></div></CardAction></CardHeader><CardContent className="space-y-3"><div className="max-h-[420px] overflow-auto rounded-md border"><Table><TableHeader className="sticky top-0 z-10 bg-background"><TableRow>{(matrix?.columns ?? [{ prop: "ts", label: "采样时间" }]).map((column) => <TableHead key={column.prop} className={column.prop === "ts" ? "min-w-52" : "min-w-32"}>{column.label}</TableHead>)}</TableRow></TableHeader><TableBody>{matrixLoading && !matrix ? <TableRow><TableCell colSpan={1}><Skeleton className="h-28" /></TableCell></TableRow> : matrix?.rows.length ? matrix.rows.map((row, index) => <TableRow key={`${String(row.ts)}-${index}`}>{matrix.columns.map((column) => <TableCell key={column.prop} className={column.prop === "ts" ? "whitespace-nowrap text-muted-foreground" : "font-mono"}>{String(row[column.prop] ?? "--")}</TableCell>)}</TableRow>) : <TableRow><TableCell colSpan={matrix?.columns.length ?? 1} className="h-28 text-center text-muted-foreground">暂无原始数据</TableCell></TableRow>}</TableBody></Table></div>{matrix?.nextCursor && <div className="flex justify-center"><Button variant="outline" onClick={() => void loadMatrix(matrixAlign, false, matrix.nextCursor ?? undefined)} disabled={matrixLoading}>{matrixLoading ? "加载中..." : "加载更多"}</Button></div>}</CardContent></Card>

      <Dialog open={pointDialogOpen} onOpenChange={setPointDialogOpen}><DialogContent className="sm:max-w-2xl"><DialogHeader><DialogTitle>选择查询点位</DialogTitle><DialogDescription>单次历史查询最多选择 {QUERY_POINT_LIMIT} 个点位。</DialogDescription></DialogHeader><div className="space-y-3"><div className="relative"><Search className="absolute left-3 top-2.5 size-4 text-muted-foreground" /><Input value={pointSearch} onChange={(event) => setPointSearch(event.target.value)} placeholder="搜索点位名称或地址" className="pl-9" /></div><div className="flex items-center justify-between text-xs"><span className="text-muted-foreground">已选择 {draftPoints.length}/{QUERY_POINT_LIMIT}</span><div><Button size="sm" variant="ghost" onClick={() => setDraftPoints(pointList.slice(0, QUERY_POINT_LIMIT).map((point) => point.varSeq))}>全选</Button><Button size="sm" variant="ghost" onClick={() => setDraftPoints([])}>清空</Button></div></div><div className="grid max-h-[360px] grid-cols-2 gap-1 overflow-y-auto rounded-md border p-2">{points.loading ? <Skeleton className="col-span-2 h-48" /> : filteredPoints.length ? filteredPoints.map((point) => { const selected = draftPoints.includes(point.varSeq); const disabled = draftPoints.length >= QUERY_POINT_LIMIT && !selected; return <label key={point.varSeq} className={`flex min-w-0 items-center gap-2 rounded-md px-2 py-2 ${disabled ? "cursor-not-allowed opacity-45" : "cursor-pointer hover:bg-muted"}`}><Checkbox checked={selected} disabled={disabled} onCheckedChange={(checked) => setDraftPoints((current) => checked ? [...new Set([...current, point.varSeq])].slice(0, QUERY_POINT_LIMIT) : current.filter((value) => value !== point.varSeq))} /><span className="min-w-0 flex-1"><span className="block truncate text-sm font-medium">{point.pointName}</span><span className="block truncate font-mono text-xs text-muted-foreground">{point.ioAddress}</span></span></label>; }) : <div className="col-span-2 flex h-32 items-center justify-center text-sm text-muted-foreground">没有匹配的点位</div>}</div></div><DialogFooter><Button variant="outline" onClick={() => setPointDialogOpen(false)}>取消</Button><Button onClick={confirmPoints} disabled={!draftPoints.length}>确认</Button></DialogFooter></DialogContent></Dialog>
      <Dialog open={deviceDialogOpen} onOpenChange={setDeviceDialogOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>选择设备</DialogTitle><DialogDescription>按设备类型、设备编号或名称筛选后确认切换。</DialogDescription></DialogHeader>
          <div className="grid gap-4">
            <div className="space-y-2"><Label>设备类型</Label><Select value={draftDeviceType} onValueChange={(value) => { setDraftDeviceType(value); setDraftDeviceId(""); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">全部类型</SelectItem>{(types.data ?? []).filter((type) => type.typeCode).map((type) => <SelectItem key={type.typeCode} value={type.typeCode}>{type.typeName}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-2"><Label>设备编号</Label><Select value={draftDeviceId} onValueChange={setDraftDeviceId}><SelectTrigger><SelectValue placeholder="选择设备编号" /></SelectTrigger><SelectContent>{filteredDevices.map((device) => <SelectItem key={device.id} value={String(device.id)}>{device.deviceCode ?? device.name} · {device.deviceName ?? device.name}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-2"><Label>搜索</Label><div className="relative"><Search className="absolute left-3 top-2 size-4 text-muted-foreground" /><Input className="h-8 pl-9" value={deviceSearch} onChange={(event) => setDeviceSearch(event.target.value)} placeholder="搜索设备名称或编号" /></div></div>
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setDeviceDialogOpen(false)}>取消</Button><Button onClick={confirmDevice} disabled={!draftDeviceId}>确认</Button></DialogFooter>
        </DialogContent>
      </Dialog>


      <Dialog open={exportDialogOpen} onOpenChange={(open) => !exporting && setExportDialogOpen(open)}><DialogContent className="max-w-lg"><DialogHeader><DialogTitle>导出历史数据</DialogTitle><DialogDescription>可独立选择最多20个导出点位，导出格式为 CSV。</DialogDescription></DialogHeader><div className="space-y-4"><div className="space-y-2"><Label>设备</Label><Input value={currentDevice?.name ?? "未选择"} disabled /></div><div className="space-y-2"><Label>时间范围</Label><div className="grid grid-cols-2 gap-2"><Input type="datetime-local" value={exportStart} onChange={(event) => setExportStart(event.target.value)} /><Input type="datetime-local" value={exportEnd} onChange={(event) => setExportEnd(event.target.value)} /></div></div><div className="space-y-2"><div className="flex items-center justify-between"><Label>导出点位</Label><span className="text-xs text-muted-foreground">{exportPoints.length}/{EXPORT_POINT_LIMIT}</span></div><div className="max-h-52 space-y-1 overflow-y-auto rounded-md border p-2">{pointList.map((point) => { const selected = exportPoints.includes(point.varSeq); const disabled = exportPoints.length >= EXPORT_POINT_LIMIT && !selected; return <label key={point.varSeq} className={`flex items-center gap-2 rounded px-2 py-1.5 text-sm ${disabled ? "cursor-not-allowed opacity-45" : "cursor-pointer hover:bg-muted"}`}><Checkbox checked={selected} disabled={disabled} onCheckedChange={(checked) => setExportPoints((current) => checked ? [...new Set([...current, point.varSeq])].slice(0, EXPORT_POINT_LIMIT) : current.filter((value) => value !== point.varSeq))} />{point.pointName}</label>; })}</div></div><div className="space-y-2"><Label>导出格式</Label><Input value="CSV 文件 (*.csv)" disabled /></div>{exporting && <div className="space-y-2"><div className="flex justify-between text-xs text-primary"><span>正在处理导出任务...</span><span>{exportProgress}%</span></div><div className="h-2 overflow-hidden rounded-full bg-muted"><div className="h-full bg-primary transition-[width]" style={{ width: `${exportProgress}%` }} /></div></div>}</div><DialogFooter><Button variant="outline" onClick={() => setExportDialogOpen(false)} disabled={exporting}>取消</Button><Button onClick={() => void exportHistory()} disabled={exporting || !exportPoints.length}>{exporting ? "导出中..." : "开始导出"}</Button></DialogFooter></DialogContent></Dialog>
    </div>
  );
}
