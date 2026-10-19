import { Fragment, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import {
  Activity,
  ChevronDown,
  Download,
  Expand,
  Pause,
  Play,
  Search,
  Settings2,
  Square,
  Wifi,
  WifiOff,
  X,
} from "lucide-react";
import {
  Area,
  AreaChart,
  CartesianGrid,
  XAxis,
  YAxis,
} from "recharts";
import { BUILD_ROLE } from "@/config/build-role";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import {
  ChartContainer,
  ChartTooltip,
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
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { ChartTipCard, CountChip, CTRL, DANGER_BTN, HEAD_ROW, ICON_BTN, LegendPill, OUTLINE_BTN, PRIMARY_BTN, SegTabs, SERIES_COLORS, TABLE_FRAME, TABLE_TEXT, TD, TH } from "@/components/DevicePageUi";
import { useAuth } from "@/contexts/AuthContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { useRealtimeWebSocket } from "@/hooks/useRealtimeWebSocket";
import { monitorClient } from "@/lib/monitor-client";
import { useDevicePicker } from "@/hooks/useDevicePicker";
import { DeviceCatalogPagination } from "@/components/DeviceCatalogPagination";
import { PointValueTile, PointValueTileRow } from "@/components/PointValueTile";
import { saveDownload } from "@/lib/save-download";
import { decimateTimeSeries } from "@/components/charts/decimate-time-series";
import type { RealtimeDataItem } from "@/types/device-monitor";

const STORAGE_KEY = "data-watch:realtime-monitor";
const FREQUENCIES = [
  [100, "0.1秒"],
  [500, "0.5秒"],
  [1000, "1秒"],
  [2000, "2秒"],
  [5000, "5秒"],
  [10000, "10秒"],
  [30000, "30秒"],
] as const;
const REALTIME_WINDOW_MS = 5 * 60_000;
const MAX_REALTIME_SAMPLES = 3000;
const REALTIME_VIEW_WINDOWS = [
  [30_000, "30秒"],
  [60_000, "1分钟"],
  [REALTIME_WINDOW_MS, "5分钟"],
] as const;
const DEFAULT_REALTIME_VIEW_MS = 60_000;

type ChartType = "line" | "area" | "status";
type SampleRow = Record<string, string | number | null>;

function formatSampleTime(timestamp: number) {
  const date = new Date(timestamp);
  const milliseconds = String(date.getMilliseconds()).padStart(3, "0");
  return date.toLocaleTimeString("zh-CN", { hour12: false }) + "." + milliseconds;
}

interface SavedMonitorState {
  deviceId?: number;
  selectedPoints?: number[];
  refreshRate?: number;
  viewWindowMs?: number;
  chartType?: ChartType;
  separateBoolChart?: boolean;
  wasMonitoring?: boolean;
}

function normalizeViewWindow(value?: number): number {
  return REALTIME_VIEW_WINDOWS.some(([duration]) => duration === value)
    ? Number(value)
    : DEFAULT_REALTIME_VIEW_MS;
}

function loadSavedState(): SavedMonitorState {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY) ?? "{}") as SavedMonitorState;
  } catch {
    return {};
  }
}

function normalizePointValue(point: RealtimeDataItem, value: unknown) {
  if (value == null || (typeof value === "string" && !value.trim())) return "--";
  const type = (point.dataType ?? "").toUpperCase();
  if (type === "BOOL") {
    if (typeof value === "boolean") return value;
    const normalized = String(value).trim().toLowerCase();
    if (["1", "true", "yes", "on"].includes(normalized)) return true;
    if (["0", "false", "no", "off"].includes(normalized)) return false;
    return "--";
  }
  if (["INT", "DINT", "WORD", "DWORD", "REAL", "FLOAT", "DOUBLE"].includes(type)) {
    const number = Number(value);
    return Number.isFinite(number) ? number : "--";
  }
  return value as string | number | boolean;
}

function chartValue(value: string | number | boolean) {
  if (typeof value === "boolean") return value ? 1 : 0;
  if (typeof value === "number") return Number.isFinite(value) ? value : null;
  if (!value.trim() || value === "--") return null;
  const number = Number(value);
  return Number.isFinite(number) ? number : null;
}

function formatSyncTime(timestamp: number | null) {
  if (!timestamp) return "--";
  return new Date(timestamp).toLocaleString("zh-CN", { hour12: false });
}

function computeViewDomain(samples: SampleRow[], viewWindowMs: number): [number, number] {
  const latestSampleAt = Number(samples[samples.length - 1]?.sampleAt ?? Date.now());
  const firstSampleAt = Number(samples[0]?.sampleAt ?? latestSampleAt);
  const observedSpan = Math.max(latestSampleAt - firstSampleAt, 1000);
  const rightPadding = Math.min(2000, Math.max(250, observedSpan * 0.04));
  const adaptiveStart = Math.max(firstSampleAt, latestSampleAt - viewWindowMs + rightPadding);
  return [adaptiveStart, latestSampleAt + rightPadding];
}

type ColoredPoint = RealtimeDataItem & { color: string };

function TrendChart({
  title,
  points,
  samples,
  chartType,
  viewWindowMs,
  hidden,
  compact = false,
  className = "",
}: {
  title?: string;
  points: ColoredPoint[];
  samples: SampleRow[];
  chartType: ChartType;
  viewWindowMs: number;
  hidden?: number[];
  compact?: boolean;
  className?: string;
}) {
  const config = useMemo(
    () => Object.fromEntries(points.map((point) => [
      `v${point.varSeq}`,
      { label: point.pointName, color: point.color },
    ])) as ChartConfig,
    [points],
  );

  const chartSamples = useMemo(() => decimateTimeSeries(
    samples, 600, points.map((point) => `v${point.varSeq}`),
  ), [samples, points]);

  if (title && points.length === 0) return null;
  if (chartType === "status") {
    const rows = samples.slice(compact ? -3 : -6).reverse();
    return (
      <div className={["min-h-0 space-y-1.5", className].join(" ")}>
        {title && <p className="text-xs font-medium text-muted-foreground">{title}</p>}
        <div className={TABLE_FRAME}>
          <Table className={TABLE_TEXT}>
            <TableHeader><TableRow className={HEAD_ROW}><TableHead className={`${TH} w-[130px]`}>时间</TableHead>{points.map((point) => <TableHead key={point.varSeq} className={TH}><span className="mr-1.5 inline-block size-[7px] rounded-full" style={{ background: point.color }} />{point.pointName}</TableHead>)}</TableRow></TableHeader>
            <TableBody>{rows.map((row, index) => <TableRow key={`${row.time}-${index}`} className={`h-11 ${index === 0 ? "bg-brand-soft/60" : ""}`}><TableCell className={`${TD} font-mono text-xs`}>{row.time}</TableCell>{points.map((point) => { const value = row[`v${point.varSeq}`] ?? "--"; return <TableCell key={point.varSeq} className={TD}><span className={`inline-flex min-w-[52px] justify-center rounded-md px-2 py-0.5 font-mono text-xs font-medium ${typeof value === "number" && value > 0 ? "bg-success-soft text-success" : "bg-muted text-muted-foreground"}`}>{typeof value === "number" && !Number.isInteger(value) ? value.toFixed(2) : value}</span></TableCell>; })}</TableRow>)}</TableBody>
          </Table>
        </div>
      </div>
    );
  }

  const boolOnly = points.every((point) => (point.dataType ?? "").toUpperCase() === "BOOL");
  const viewDomain = computeViewDomain(samples, viewWindowMs);
  const visible = points.filter((point) => !hidden?.includes(point.varSeq));
  const lastIndex = chartSamples.length - 1;
  return (
    <div className={["flex min-h-0 flex-col gap-1.5", className].join(" ")}>
      {title && <p className="text-xs font-medium text-muted-foreground">{title}</p>}
      <ChartContainer config={config} className={[compact ? "h-16 max-h-16" : "h-full min-h-0 flex-1", "w-full aspect-auto"].join(" ")}>
        <AreaChart data={chartSamples} margin={{ top: 10, right: 18, bottom: 0, left: 0 }} accessibilityLayer>
          <defs>
            {visible.map((point) => (
              <linearGradient key={point.varSeq} id={`rt-grad-${point.varSeq}`} x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" style={{ stopColor: point.color, stopOpacity: 0.24 }} />
                <stop offset="100%" style={{ stopColor: point.color, stopOpacity: 0 }} />
              </linearGradient>
            ))}
          </defs>
          <CartesianGrid vertical={false} strokeDasharray="3 4" stroke="var(--border)" />
          <XAxis
            dataKey="sampleAt"
            type="number"
            domain={viewDomain}
            allowDataOverflow
            tickLine={false}
            axisLine={false}
            minTickGap={48}
            tickMargin={8}
            fontSize={11}
            tickFormatter={(value) => formatSampleTime(Number(value)).slice(0, 8)}
          />
          <YAxis
            tickLine={false}
            axisLine={false}
            width={40}
            fontSize={11}
            domain={boolOnly ? [-0.15, 1.15] : undefined}
            ticks={boolOnly ? [0, 1] : undefined}
            tickFormatter={boolOnly ? (value) => Number(value) === 1 ? "ON" : "OFF" : undefined}
          />
          <ChartTooltip
            cursor={{ stroke: "var(--muted-foreground)", strokeOpacity: 0.55, strokeDasharray: "3 3" }}
            isAnimationActive={false}
            content={({ active, payload, label }) => active && payload?.length ? (
              <ChartTipCard
                title={formatSampleTime(Number(label))}
                rows={payload.map((item) => {
                  const point = points.find((candidate) => `v${candidate.varSeq}` === item.dataKey);
                  const value = typeof item.value === "number" ? (boolOnly ? (item.value ? "ON" : "OFF") : Number.isInteger(item.value) ? String(item.value) : item.value.toFixed(2)) : String(item.value ?? "--");
                  return { key: String(item.dataKey), label: point?.pointName ?? String(item.name), color: point?.color ?? "var(--brand)", value };
                })}
              />
            ) : null}
          />
          {visible.map((point) => (
            <Area
              key={point.varSeq}
              type={boolOnly ? "stepAfter" : "monotone"}
              dataKey={`v${point.varSeq}`}
              stroke={point.color}
              fill={chartType === "area" ? `url(#rt-grad-${point.varSeq})` : "transparent"}
              strokeWidth={2}
              isAnimationActive={false}
              activeDot={{ r: 4.5, stroke: "var(--card)", strokeWidth: 2, fill: point.color }}
              dot={(props: { index?: number; cx?: number; cy?: number }) => props.index === lastIndex && props.cx != null && props.cy != null
                ? <g key={`end-${point.varSeq}`}><circle cx={props.cx} cy={props.cy} r={4} fill={point.color} className="animate-ping [transform-box:fill-box] [transform-origin:center]" opacity={0.5} /><circle cx={props.cx} cy={props.cy} r={4} fill={point.color} stroke="var(--card)" strokeWidth={2} /></g>
                : <g key={`d-${point.varSeq}-${props.index}`} />}
            />
          ))}
        </AreaChart>
      </ChartContainer>
    </div>
  );
}

/** Bool 型点位状态泳道：ON 实色胶囊，OFF 斜纹底 */
function BoolLanes({ points, samples, viewWindowMs }: { points: ColoredPoint[]; samples: SampleRow[]; viewWindowMs: number }) {
  const [start, end] = computeViewDomain(samples, viewWindowMs);
  const span = Math.max(end - start, 1);
  const pct = (time: number) => Math.min(100, Math.max(0, ((time - start) / span) * 100));
  const latest = Number(samples[samples.length - 1]?.sampleAt ?? end);
  return (
    <div className="grid grid-cols-[72px_minmax(0,1fr)] items-center gap-y-2 pr-[18px]">
      {points.map((point) => {
        const key = `v${point.varSeq}`;
        const runs: [number, number][] = [];
        let runStart: number | null = null;
        samples.forEach((sample, index) => {
          const time = Number(sample.sampleAt);
          const on = Number(sample[key]) === 1;
          if (on && runStart == null) runStart = time;
          const next = samples[index + 1];
          if (runStart != null && (!on || !next)) {
            runs.push([runStart, on ? time : time]);
            runStart = null;
          }
        });
        const current = Number(samples[samples.length - 1]?.[key]) === 1;
        return (
          <Fragment key={point.varSeq}>
            <span className="truncate text-xs text-foreground">{point.pointName}</span>
            <div className="relative h-[18px] overflow-hidden rounded-full bg-[repeating-linear-gradient(135deg,color-mix(in_oklab,var(--muted-foreground)_35%,transparent)_0_2px,transparent_2px_5px)]">
              {runs.filter(([from, to]) => to >= start && from <= end).map(([from, to]) => (
                <span key={from} className="absolute inset-y-0 rounded-full" style={{ left: `${pct(from)}%`, width: `${Math.max(0.6, pct(to) - pct(from))}%`, background: point.color }} />
              ))}
              <span className="absolute top-1/2 size-2 -translate-x-1/2 -translate-y-1/2 rounded-full" style={{ left: `${pct(latest)}%`, background: current ? point.color : "var(--muted-foreground)" }} />
            </div>
          </Fragment>
        );
      })}
    </div>
  );
}

export default function RealtimeMonitorPage() {
  const savedState = useMemo(loadSavedState, []);
  const { auth } = useAuth();
  const token = auth?.backendToken || (BUILD_ROLE === "edge" ? auth?.agentToken ?? "" : "");
  const [searchParams, setSearchParams] = useSearchParams();
  const [frequency, setFrequency] = useState(savedState.refreshRate ?? 1000);
  const [viewWindowMs, setViewWindowMs] = useState(() => normalizeViewWindow(savedState.viewWindowMs));
  const [running, setRunning] = useState(savedState.wasMonitoring ?? false);
  const [selectedPoints, setSelectedPoints] = useState<number[]>(savedState.selectedPoints ?? []);
  const [samples, setSamples] = useState<SampleRow[]>([]);
  const [chartType, setChartType] = useState<ChartType>(savedState.chartType ?? "line");
  const [separateBoolChart, setSeparateBoolChart] = useState(savedState.separateBoolChart ?? true);
  const [pointDialogOpen, setPointDialogOpen] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [pointSearch, setPointSearch] = useState("");
  const [draftPoints, setDraftPoints] = useState<number[]>([]);
  const chartCardRef = useRef<HTMLDivElement>(null);
  const [hiddenSeries, setHiddenSeries] = useState<number[]>([]);

  const [deviceDialogOpen, setDeviceDialogOpen] = useState(false);
  const [deviceSearch, setDeviceSearch] = useState("");
  const [draftDeviceType, setDraftDeviceType] = useState("all");
  const [draftDeviceId, setDraftDeviceId] = useState("");
  const requestedDeviceId = Number(searchParams.get("deviceId"));
  const preferredDeviceId = requestedDeviceId || savedState.deviceId || 0;
  const loadSelectedDevice = useCallback((backendToken: string, signal: AbortSignal) => monitorClient.deviceList(backendToken, {
    pageNo: 1, pageSize: 1, deviceIds: preferredDeviceId ? [preferredDeviceId] : undefined,
  }, signal), [preferredDeviceId]);
  const currentDevice = useBackendQuery(loadSelectedDevice);
  const selectedDevice = currentDevice.data?.list.find((device) => !preferredDeviceId || device.id === preferredDeviceId);
  const selectedDeviceId = selectedDevice?.id ?? 0;
  const [selectedGroupCode, setSelectedGroupCode] = useState("g001");
  const loadDeviceTypes = useCallback((backendToken: string, signal: AbortSignal) => monitorClient.deviceTypes(backendToken, signal), []);
  const types = useBackendQuery(loadDeviceTypes, { enabled: deviceDialogOpen });
  const deviceTypes = types.data ?? [];
  const devicePicker = useDevicePicker(monitorClient, deviceSearch, draftDeviceType, deviceDialogOpen);
  const filteredDevices = devicePicker.data?.list ?? [];
  const loadPointGroups = useCallback(
    (backendToken: string, signal: AbortSignal) => selectedDevice?.deviceCode
      ? monitorClient.realtimePointGroups(backendToken, selectedDevice.deviceCode, signal)
      : Promise.resolve([]),
    [selectedDevice?.deviceCode],
  );
  const pointGroups = useBackendQuery(loadPointGroups, { enabled: Boolean(selectedDevice?.deviceCode) });

  useEffect(() => {
    if (!requestedDeviceId && selectedDeviceId) setSearchParams({ deviceId: String(selectedDeviceId) }, { replace: true });
  }, [requestedDeviceId, selectedDeviceId, setSearchParams]);

  useEffect(() => {
    const groups = pointGroups.data ?? [];
    const nextGroupCode = groups[0]?.group_code ?? "g001";
    setSelectedGroupCode((current) => (
      groups.some((group) => group.group_code === current) ? current : nextGroupCode
    ));
  }, [pointGroups.data, selectedDeviceId]);

  const loadRealtime = useCallback(
    (backendToken: string, signal: AbortSignal) => monitorClient.realtimeInit(backendToken, selectedDeviceId, selectedGroupCode, signal),
    [selectedDeviceId, selectedGroupCode],
  );
  const realtime = useBackendQuery(loadRealtime, { enabled: selectedDeviceId > 0 });
  const realtimeSocket = useRealtimeWebSocket({
    enabled: running,
    token,
    deviceCode: selectedDevice?.deviceCode ?? "",
    groupCode: selectedGroupCode,
    flushIntervalMs: frequency,
  });

  const pointList = realtime.data?.data ?? [];
  const resolvedPoints = useMemo(() => pointList.map((point) => {
    const wire = point.pointId == null ? undefined : realtimeSocket.values[String(point.pointId)];
    return {
      ...point,
      value: realtimeSocket.deviceOffline ? "--" : wire ? normalizePointValue(point, wire.v) : point.value,
      quality: realtimeSocket.deviceOffline ? 7 : wire?.q ?? point.quality,
      updateTime: realtimeSocket.deviceOffline ? "" : wire?.t ? new Date(wire.t).toLocaleString("zh-CN", { hour12: false }) : point.updateTime,
    };
  }), [pointList, realtimeSocket.deviceOffline, realtimeSocket.values]);

  const selectedPointItems = useMemo(
    () => resolvedPoints.filter((point) => selectedPoints.includes(point.varSeq)),
    [resolvedPoints, selectedPoints],
  );
  const selectedPointItemsRef = useRef(selectedPointItems);
  const filteredPoints = pointList.filter((point) => `${point.pointName} ${point.ioAddress}`.toLowerCase().includes(pointSearch.trim().toLowerCase()));

  useEffect(() => {
    selectedPointItemsRef.current = selectedPointItems;
  }, [selectedPointItems]);

  useEffect(() => {
    if (!running || realtimeSocket.sampleVersion === 0) return;
    const points = selectedPointItemsRef.current;
    if (!points.length) return;
    const sampleAt = Date.now();
    const row: SampleRow = { time: formatSampleTime(sampleAt), sampleAt };
    points.forEach((point) => { row[`v${point.varSeq}`] = chartValue(point.value); });
    setSamples((current) => [
      ...current
        .filter((sample) => Number(sample.sampleAt ?? 0) >= sampleAt - REALTIME_WINDOW_MS)
        .slice(-(MAX_REALTIME_SAMPLES - 1)),
      row,
    ]);
  }, [realtimeSocket.sampleVersion, running]);
  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({
      deviceId: selectedDeviceId,
      selectedPoints,
      refreshRate: frequency,
      viewWindowMs,
      chartType,
      separateBoolChart,
      wasMonitoring: running,
    } satisfies SavedMonitorState));
  }, [chartType, frequency, running, selectedDeviceId, selectedPoints, separateBoolChart, viewWindowMs]);

  function resetMonitorState() {
    setRunning(false);
    setSelectedPoints([]);
    setSamples([]);
    realtimeSocket.clearValues();
  }

  function changeDevice(deviceId: string) {
    resetMonitorState();
    setSelectedGroupCode("g001");
    setSearchParams({ deviceId });
  }

  function changePointGroup(groupCode: string) {
    if (groupCode === selectedGroupCode) return;
    resetMonitorState();
    setSelectedGroupCode(groupCode);
  }

  function openDeviceSelector() {
    setDeviceSearch("");
    setDraftDeviceType(selectedDevice?.deviceType ?? "all");
    setDraftDeviceId(selectedDeviceId ? String(selectedDeviceId) : "");
    setDeviceDialogOpen(true);
  }

  function confirmDeviceSelection() {
    if (!draftDeviceId) { toast.warning("请选择设备编号"); return; }
    setDeviceDialogOpen(false);
    changeDevice(draftDeviceId);
  }

  function openPointSelector() {
    setDraftPoints(selectedPoints);
    setPointSearch("");
    setPointDialogOpen(true);
  }

  function confirmPointSelection() {
    setSelectedPoints(draftPoints);
    setSamples([]);
    setPointDialogOpen(false);
  }

  function clearSelection() {
    setSelectedPoints([]);
    setSamples([]);
    realtimeSocket.clearValues();
  }

  async function exportRealtimeData() {
    if (!samples.length || !selectedPointItems.length) return toast.warning("暂无数据可导出");
    try {
      const XLSX = await import("xlsx");
      const exportRows = samples.map((sample) => Object.fromEntries([
        ["时间", sample.time],
        ...selectedPointItems.map((point) => [`${point.pointName}(${point.varSeq})`, sample[`v${point.varSeq}`] ?? ""]),
      ]));
      const workbook = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(workbook, XLSX.utils.json_to_sheet(exportRows), "趋势数据");
      const buffer = XLSX.write(workbook, { bookType: "xlsx", type: "array" }) as ArrayBuffer;
      const timestamp = new Date().toISOString().slice(0, 19).replace(/[T:]/g, "-");
      saveDownload(Array.from(new Uint8Array(buffer)), `${selectedDevice?.deviceName ?? "设备"}_实时监控_${timestamp}.xlsx`, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
      toast.success("实时数据已导出");
    } catch {
      toast.error("实时数据导出失败");
    }
  }

  async function toggleFullscreen() {
    try {
      if (!document.fullscreenElement) await chartCardRef.current?.requestFullscreen();
      else await document.exitFullscreen();
    } catch {
      toast.error("无法切换全屏显示");
    }
  }

  const socketConnected = realtimeSocket.status === "connected";
  const hasData = samples.length > 0;
  const coloredPoints: ColoredPoint[] = selectedPointItems.map((point, index) => ({ ...point, color: SERIES_COLORS[index % SERIES_COLORS.length] }));
  const numericColored = coloredPoints.filter((point) => (point.dataType ?? "").toUpperCase() !== "BOOL");
  const boolColored = coloredPoints.filter((point) => (point.dataType ?? "").toUpperCase() === "BOOL");
  const previousSample = samples.length > 1 ? samples[samples.length - 2] : null;
  const windowSamples = samples.length ? samples.filter((sample) => Number(sample.sampleAt) >= Number(samples[samples.length - 1].sampleAt) - viewWindowMs).length : 0;
  const groupLabel = pointGroups.data?.find((group) => group.group_code === selectedGroupCode)?.group_name || selectedGroupCode;
  const splitBool = chartType !== "status" && separateBoolChart;
  const connectionLabel = realtimeSocket.source === "local" ? "本机数据" : socketConnected ? "中心实时连接" : realtimeSocket.status === "connecting" ? "中心连接中" : "中心未连接";

  return (
    <div className="mx-auto flex h-full min-h-0 w-full max-w-[1800px] flex-col gap-3 overflow-hidden">
      <Card className="shrink-0 gap-0 py-0">
      <div className="flex flex-wrap items-center gap-2 whitespace-nowrap border-b px-5 py-3.5">
        <span className="text-xs text-muted-foreground">当前设备</span>
        <Button variant="outline" className={`${OUTLINE_BTN} max-w-56 font-mono text-[12.5px]`} onClick={openDeviceSelector}><span className="truncate">{selectedDevice ? selectedDevice.deviceCode : "选择设备"}</span><ChevronDown className="size-3.5 text-muted-foreground" /></Button>
        <span className="ml-1 text-xs text-muted-foreground">点位组</span>
        <Select value={selectedGroupCode} onValueChange={changePointGroup} disabled={!selectedDeviceId || pointGroups.loading}>
          <SelectTrigger className={`${CTRL} w-[132px]`} aria-label="点位组"><SelectValue /></SelectTrigger>
          <SelectContent>
            {pointGroups.data?.length
              ? pointGroups.data.map((group) => <SelectItem key={group.group_code} value={group.group_code}>{group.group_name || group.group_code}</SelectItem>)
              : <SelectItem value="g001">g001</SelectItem>}
          </SelectContent>
        </Select>
        <Button variant="outline" className={OUTLINE_BTN} onClick={openPointSelector} disabled={!selectedDeviceId}><Search className="size-3.5" />选择点位{selectedPointItems.length > 0 && <CountChip count={selectedPointItems.length} />}</Button>
        <div className="ml-auto flex flex-wrap items-center justify-end gap-2">
          <span className={`inline-flex h-7 items-center gap-2 rounded-full border bg-card px-2.5 text-xs ${socketConnected ? "text-success" : "text-muted-foreground"}`}>
            <span className="relative flex size-2">{socketConnected && <span className="absolute inline-flex size-full animate-ping rounded-full bg-current opacity-50" />}<span className="relative inline-flex size-2 rounded-full bg-current" /></span>
            {connectionLabel}
          </span>
          <Select value={String(frequency)} onValueChange={(value) => setFrequency(Number(value))}>
            <SelectTrigger className={`${CTRL} w-[76px]`} aria-label="时间选择" title="刷新频率"><SelectValue /></SelectTrigger>
            <SelectContent>{FREQUENCIES.map(([value, label]) => <SelectItem key={value} value={String(value)}>{label}</SelectItem>)}</SelectContent>
          </Select>
          <span className="mx-0.5 h-5 w-px bg-border" />
          <Button className={PRIMARY_BTN} onClick={() => setRunning(true)} disabled={running || !selectedDeviceId || selectedPoints.length === 0}><Play className="size-3.5" />开始监控</Button>
          <Button variant="destructive" className={DANGER_BTN} onClick={() => setRunning(false)} disabled={!running}><Square className="size-3.5" />停止监控</Button>
          <Button variant="outline" className={OUTLINE_BTN} onClick={clearSelection} disabled={!selectedPoints.length}><X className="size-3.5" />清空选择</Button>
          <TooltipProvider delayDuration={200}><Tooltip><TooltipTrigger asChild><Button variant="outline" className={ICON_BTN} aria-label="导出" onClick={exportRealtimeData} disabled={!hasData}><Download /></Button></TooltipTrigger><TooltipContent>导出</TooltipContent></Tooltip></TooltipProvider>
        </div>
      </div>
        <CardContent className="p-3">
          <PointValueTileRow empty={<><p className="font-medium text-foreground">实时监测点</p><div className="flex items-center gap-2"><Search className="size-4 opacity-50" />暂未选择监控点位</div></>}>
            {coloredPoints.map((point) => <PointValueTile key={point.varSeq} name={point.pointName} color={point.color} ioAddress={point.ioAddress} dataType={point.dataType} value={point.value} bad={!(point.quality == null || point.quality === 0)} previous={previousSample ? (previousSample[`v${point.varSeq}`] as number | null) ?? null : null} />)}
          </PointValueTileRow>
        </CardContent>
      </Card>

      {running && (realtimeSocket.source === "local" || !socketConnected || realtimeSocket.deviceOffline) && (
        <Alert variant={realtimeSocket.source === "local" ? "default" : "destructive"}>
          <WifiOff className="size-4" />
          <AlertDescription>{realtimeSocket.source === "local" ? "正在显示本机 SQLite 实时值；此模式不需要边缘节点直连 Redis" : !socketConnected ? realtimeSocket.error ?? "中心实时连接已断开，正在重连..." : "设备数据离线，等待采集恢复..."}</AlertDescription>
        </Alert>
      )}

      <Card ref={chartCardRef} className="flex min-h-0 min-w-0 flex-1 flex-col gap-0 overflow-hidden py-0">
        <CardHeader className="!flex shrink-0 items-center gap-3 border-b px-5 py-3.5">
            <div className="min-w-0">
              <p className="text-sm font-semibold leading-none">点位实时趋势</p>
              <p className="mt-1.5 truncate text-[13px] text-muted-foreground">{selectedDevice?.deviceCode ?? "未选择设备"} · {groupLabel} · 数值型与 Bool 型点位分开展示</p>
            </div>
            <div className="ml-auto flex items-center gap-2">
              <Select value={String(viewWindowMs)} onValueChange={(value) => setViewWindowMs(Number(value))}>
                <SelectTrigger className={`${CTRL} w-[92px]`} aria-label="最大显示跨度" title="最大显示跨度"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectGroup>
                    {REALTIME_VIEW_WINDOWS.map(([value, label]) => <SelectItem key={value} value={String(value)}>{label}</SelectItem>)}
                  </SelectGroup>
                </SelectContent>
              </Select>
              <SegTabs<ChartType> value={chartType} options={[["line", "折线图"], ["area", "面积图"], ["status", "状态图"]]} onChange={setChartType} />
              <Button size="icon" variant="ghost" className="size-8 text-muted-foreground" title="全屏" aria-label="全屏" onClick={() => void toggleFullscreen()}><Expand className="size-4" /></Button>
              <Button size="icon" variant="ghost" className="size-8 text-muted-foreground" title="图表设置" aria-label="图表设置" onClick={() => setSettingsOpen(true)}><Settings2 className="size-4" /></Button>
            </div>
        </CardHeader>
        <CardContent className="relative flex min-h-0 flex-1 flex-col px-5 pb-4 pt-4">
          {!running && hasData && <span className="absolute left-1/2 top-2 z-10 inline-flex h-[26px] -translate-x-1/2 items-center gap-1.5 rounded-full bg-warning-soft px-2.5 text-xs font-medium text-warning"><Pause className="size-3.5" />监控已停止，保留最近采样</span>}
          {realtime.loading ? <Skeleton className="h-full min-h-40" /> : !selectedPointItems.length ? <div className="flex h-full min-h-40 flex-col items-center justify-center gap-3 text-sm text-muted-foreground"><Activity className="size-9 opacity-35" /><span>请选择需要监控的点位</span><Button className={PRIMARY_BTN} onClick={openPointSelector}>添加监控点位</Button></div> : !running && !hasData ? <div className="flex h-full min-h-40 flex-col items-center justify-center gap-3 text-sm text-muted-foreground"><Play className="size-9 opacity-35" /><span>点击开始监控以查看实时趋势</span></div> : !hasData ? <div className="flex h-full min-h-40 items-center justify-center text-sm text-muted-foreground">正在连接并等待数据...</div> : chartType === "status" ? <TrendChart points={coloredPoints} samples={samples} chartType={chartType} viewWindowMs={viewWindowMs} className="h-full" /> : (
            <div className="flex min-h-0 flex-1 flex-col">
              {(numericColored.length > 0 || !separateBoolChart) && (
                <>
                  <div className="mb-1.5 flex flex-wrap items-center gap-3">
                    <span className="text-xs font-medium text-muted-foreground">{splitBool ? "数值型点位" : "全部点位"}</span>
                    <div className="flex flex-wrap gap-1.5">{(separateBoolChart ? numericColored : coloredPoints).map((point) => <LegendPill key={point.varSeq} color={point.color} label={point.pointName} off={hiddenSeries.includes(point.varSeq)} onClick={() => setHiddenSeries((current) => current.includes(point.varSeq) ? current.filter((value) => value !== point.varSeq) : [...current, point.varSeq])} />)}</div>
                    <span className="ml-auto flex items-center gap-1.5 text-xs tabular-nums text-muted-foreground"><span className={`size-2 rounded-full ${running ? "bg-success" : "bg-muted-foreground"}`} />采样 {windowSamples} 条 · 窗口内 / 缓存上限 {MAX_REALTIME_SAMPLES}</span>
                  </div>
                  <TrendChart points={separateBoolChart ? numericColored : coloredPoints} samples={samples} chartType={chartType} viewWindowMs={viewWindowMs} hidden={hiddenSeries} className="min-h-[200px] flex-1" />
                </>
              )}
              {splitBool && boolColored.length > 0 && (
                <div className="shrink-0">
                  <div className="mb-2 mt-3.5 flex items-center gap-2 text-xs font-medium text-muted-foreground">Bool 型点位<span className="h-px flex-1 bg-border" /></div>
                  <BoolLanes points={boolColored} samples={samples} viewWindowMs={viewWindowMs} />
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      <div className="grid grid-cols-1 gap-x-6 gap-y-2 px-1.5 pb-0.5 text-xs text-muted-foreground sm:grid-cols-2 xl:grid-cols-4">
          <div className="flex min-w-0 items-center gap-2"><Activity className={`size-3.5 ${realtime.data?.connectStatus === "online" ? "text-emerald-500" : "text-muted-foreground"}`} /><span className="truncate">采集服务{realtime.data?.connectStatus === "online" ? "运行正常" : "等待设备在线"}</span></div>
          <div className="flex min-w-0 items-center gap-2 xl:justify-self-center">{realtimeSocket.source === "local" ? <><Wifi className="size-3.5 text-amber-600" /><span className="truncate">本机 SQLite</span></> : <>{realtimeSocket.tdengineConnected === false ? <WifiOff className="size-3.5 text-destructive" /> : <Wifi className="size-3.5 text-emerald-500" />}<span className="truncate">TDengine {realtimeSocket.tdengineConnected === null ? "检测中" : realtimeSocket.tdengineConnected ? "已连接" : "未连接"}</span></>}</div>
          <div className="flex min-w-0 items-center gap-2 xl:justify-self-center"><Activity className={`size-3.5 ${hasData ? "text-emerald-500" : "text-muted-foreground"}`} /><span className="truncate">图表绘制{hasData ? "更新正常" : "未就绪"}</span></div>
          <div className="flex min-w-0 items-center gap-2 xl:justify-self-end"><Activity className="size-3.5 shrink-0" /><span className="truncate" title={`最后同步时间：${formatSyncTime(realtimeSocket.lastSyncTime)}`}>最后同步时间：{formatSyncTime(realtimeSocket.lastSyncTime)}</span></div>
      </div>

      <Dialog open={deviceDialogOpen} onOpenChange={setDeviceDialogOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader><DialogTitle>选择设备</DialogTitle><DialogDescription>按设备类型、设备编号或名称筛选后确认切换。</DialogDescription></DialogHeader>
          <div className="grid gap-4">
            <div className="space-y-2"><Label>设备类型</Label><Select value={draftDeviceType} onValueChange={(value) => { setDraftDeviceType(value); setDraftDeviceId(""); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectGroup><SelectItem value="all">全部类型</SelectItem>{deviceTypes.map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectGroup></SelectContent></Select></div>
            <div className="flex flex-col gap-2"><Label>设备编号</Label><Select value={draftDeviceId} onValueChange={setDraftDeviceId} disabled={devicePicker.loading}><SelectTrigger className="w-full"><SelectValue placeholder={devicePicker.loading ? "加载设备中…" : "选择设备编号"} /></SelectTrigger><SelectContent position="popper" align="start" className="max-h-64"><SelectGroup>{filteredDevices.map((device) => <SelectItem key={device.id} value={String(device.id)}>{device.deviceCode} · {device.deviceName}</SelectItem>)}</SelectGroup></SelectContent></Select></div>
            <div className="space-y-2"><Label>搜索</Label><div className="relative"><Search className="absolute left-3 top-2 size-4 text-muted-foreground" /><Input className="h-8 pl-9" value={deviceSearch} onChange={(event) => { setDeviceSearch(event.target.value); setDraftDeviceId(""); }} placeholder="搜索设备名称或编号" /></div></div>
            {types.error && <div role="alert" className="flex items-center gap-2 text-sm text-destructive"><span>设备类型加载失败，请检查中心服务版本和日志。</span><Button variant="outline" size="sm" onClick={types.refresh}>重试</Button></div>}
            {devicePicker.error && <div role="alert" className="flex items-center gap-2 text-sm text-destructive"><span>{devicePicker.error}</span><Button variant="outline" size="sm" onClick={devicePicker.refresh}>重试</Button></div>}
            {!devicePicker.loading && !devicePicker.error && !filteredDevices.length && <p role="status" className="text-sm text-muted-foreground">没有匹配的设备，请调整类型或搜索条件。</p>}
            <DeviceCatalogPagination total={devicePicker.data?.total ?? 0} page={devicePicker.page} pageSize={devicePicker.pageSize} loading={devicePicker.loading} onPageChange={(page) => { devicePicker.setPage(page); setDraftDeviceId(""); }} />
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setDeviceDialogOpen(false)}>取消</Button><Button onClick={confirmDeviceSelection} disabled={devicePicker.loading || Boolean(devicePicker.error) || !filteredDevices.some((device) => String(device.id) === draftDeviceId)}>确认</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={pointDialogOpen} onOpenChange={setPointDialogOpen}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader><DialogTitle>选择监控点位</DialogTitle><DialogDescription>{selectedDevice ? selectedDevice.deviceCode : "请先选择设备"}</DialogDescription></DialogHeader>
          <div className="space-y-3">
            <div className="relative"><Search className="absolute left-3 top-2.5 size-4 text-muted-foreground" /><Input value={pointSearch} onChange={(event) => setPointSearch(event.target.value)} placeholder="搜索点位名称或地址" className="pl-9" /></div>
            <div className="flex items-center justify-between text-xs"><span className="text-muted-foreground">已选择 {draftPoints.length} 个点位</span><div className="flex gap-1"><Button size="sm" variant="ghost" onClick={() => setDraftPoints(pointList.map((point) => point.varSeq))}>全选</Button><Button size="sm" variant="ghost" onClick={() => setDraftPoints([])}>清空</Button></div></div>
            <div className="grid max-h-[360px] grid-cols-2 gap-1 overflow-y-auto rounded-md border p-2">
              {realtime.loading ? <Skeleton className="col-span-2 h-48" /> : filteredPoints.length ? filteredPoints.map((point) => <label key={point.varSeq} className="flex min-w-0 cursor-pointer items-center gap-2 rounded-md px-2 py-2 hover:bg-muted"><Checkbox checked={draftPoints.includes(point.varSeq)} onCheckedChange={(checked) => setDraftPoints((current) => checked ? [...new Set([...current, point.varSeq])] : current.filter((value) => value !== point.varSeq))} /><span className="min-w-0 flex-1"><span className="block truncate text-sm font-medium">{point.pointName}</span><span className="block truncate font-mono text-xs text-muted-foreground">{point.ioAddress}</span></span><Badge variant="outline" className="shrink-0">{point.dataType || "--"}</Badge></label>) : <div className="col-span-2 flex h-32 items-center justify-center text-sm text-muted-foreground">没有匹配的点位</div>}
            </div>
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setPointDialogOpen(false)}>取消</Button><Button onClick={confirmPointSelection} disabled={draftPoints.length === 0}>确认</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={settingsOpen} onOpenChange={setSettingsOpen}>
        <DialogContent className="max-w-sm">
          <DialogHeader><DialogTitle>图表设置</DialogTitle><DialogDescription>调整不同数据类型在趋势区域中的展示方式。</DialogDescription></DialogHeader>
          <div className="flex items-center justify-between gap-5 rounded-md border p-4"><div><Label htmlFor="separate-bool">Bool 型变量独立图表</Label><p className="mt-1 text-xs text-muted-foreground">避免 Bool 状态值与连续数值使用同一纵轴。</p></div><Switch id="separate-bool" checked={separateBoolChart} onCheckedChange={setSeparateBoolChart} /></div>
          <DialogFooter><Button onClick={() => setSettingsOpen(false)}>关闭</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
