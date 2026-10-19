import { Fragment, useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
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
  ReferenceArea,
  Scatter,
  ScatterChart,
  XAxis,
  YAxis,
} from "recharts";
import { CalendarDays, ChevronDown, Download, LineChart as LineChartIcon, RotateCcw, Search } from "lucide-react";
import { BUILD_ROLE } from "@/config/build-role";
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ChartTipCard, CountChip, CTRL, HEAD_ROW, LegendPill, OUTLINE_BTN, PRIMARY_BTN, SegTabs, SERIES_COLORS, SoftBadge, TABLE_FRAME, TABLE_TEXT, TD, TH } from "@/components/DevicePageUi";
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { monitorClient } from "@/lib/monitor-client";
import { saveDownload } from "@/lib/save-download";
import type { AppError } from "@/types";
import type { HistoryChartResponse, HistoryRawMatrixResponse } from "@/types/device-monitor";

const COLORS = SERIES_COLORS;
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

function isBoolSeries(series: { dataType?: string }) {
  return (series.dataType ?? "").toUpperCase() === "BOOL";
}

function shortClock(timestamp: number) {
  const date = new Date(timestamp);
  return Number.isFinite(date.getTime()) ? date.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", hour12: false }) : "--";
}

type ColoredSeries = HistoryChartResponse["series"][number] & { color: string };

/** Bool 型点位状态泳道（与实时监控一致）：值为 1 的区间画实色段，0 不画，底为斜纹轨道 */
function HistoryBoolLanes({ series, domain }: { series: ColoredSeries[]; domain: [number, number] }) {
  const [start, end] = domain;
  const span = Math.max(end - start, 1);
  const pct = (time: number) => Math.min(100, Math.max(0, ((time - start) / span) * 100));
  return (
    <div className="grid grid-cols-[72px_minmax(0,1fr)] items-center gap-y-2 pl-0 pr-3">
      {series.map((item) => {
        const samples = item.data.map(([time, value]) => [parseHistoryTime(time), value] as const).sort((left, right) => left[0] - right[0]);
        const runs: [number, number][] = [];
        let runStart: number | null = null;
        samples.forEach(([time, value], index) => {
          const on = value != null && value >= 0.5;
          if (on && runStart == null) runStart = time;
          const next = samples[index + 1];
          if (runStart != null && (!on || !next)) {
            runs.push([runStart, time]);
            runStart = null;
          }
        });
        return (
          <Fragment key={item.varSeq}>
            <span className="truncate text-xs text-foreground" title={item.name}>{item.name}</span>
            <div className="relative h-[18px] overflow-hidden rounded-full bg-[repeating-linear-gradient(135deg,color-mix(in_oklab,var(--muted-foreground)_35%,transparent)_0_2px,transparent_2px_5px)]">
              {runs.filter(([from, to]) => to >= start && from <= end).map(([from, to]) => (
                <span key={from} className="absolute inset-y-0 rounded-full" style={{ left: `${pct(from)}%`, width: `${Math.max(0.6, pct(to) - pct(from))}%`, background: item.color }} />
              ))}
            </div>
          </Fragment>
        );
      })}
    </div>
  );
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
  const [hidden, setHidden] = useState<number[]>([]);
  const chartRows = useMemo(() => {
    const rows = new Map<string, Record<string, number | null>>();
    chart.series.forEach((series) => series.data.forEach(([time, value]) => {
      const row = rows.get(time) ?? { time: parseHistoryTime(time) };
      row[`v${series.varSeq}`] = value;
      rows.set(time, row);
    }));
    return [...rows.values()].sort((left, right) => Number(left.time) - Number(right.time));
  }, [chart]);
  const allColored = useMemo<ColoredSeries[]>(() => chart.series.map((series, index) => ({ ...series, color: COLORS[index % COLORS.length] })), [chart]);
  const colored = useMemo(() => allColored.filter((series) => !isBoolSeries(series)), [allColored]);
  const boolSeries = useMemo(() => allColored.filter(isBoolSeries), [allColored]);
  const config = useMemo(() => Object.fromEntries(colored.map((series) => [`v${series.varSeq}`, { label: series.name, color: series.color }])) as ChartConfig, [colored]);
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
  // 缺失区间：所有序列同时为 null 的连续时间段
  const gaps = useMemo(() => {
    const result: [number, number][] = [];
    let gapStart: number | null = null;
    chartRows.forEach((row, index) => {
      const empty = chart.series.every((series) => row[`v${series.varSeq}`] == null);
      if (empty && gapStart == null) gapStart = Number(chartRows[Math.max(0, index - 1)]?.time ?? row.time);
      if (!empty && gapStart != null) { result.push([gapStart, Number(row.time)]); gapStart = null; }
    });
    if (gapStart != null) result.push([gapStart, Number(chartRows[chartRows.length - 1]?.time)]);
    return result;
  }, [chart.series, chartRows]);
  const visibleCount = chartRows.filter((row) => Number(row.time) >= axisDomain[0] && Number(row.time) <= axisDomain[1]).length;

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

  const visible = colored.filter((series) => !hidden.includes(series.varSeq));
  const timeAxis = (
    <XAxis
      dataKey="time"
      type="number"
      scale="time"
      domain={axisDomain}
      allowDataOverflow
      tickLine={false}
      axisLine={false}
      minTickGap={36}
      tickMargin={8}
      fontSize={11}
      tickFormatter={(value) => formatHistoryAxisTime(Number(value), axisSpan)}
    />
  );
  const tooltip = (
    <ChartTooltip
      cursor={chartType === "bar" ? { fill: "var(--muted)", fillOpacity: 0.5 } : { stroke: "var(--muted-foreground)", strokeOpacity: 0.55, strokeDasharray: "3 3" }}
      isAnimationActive={false}
      content={({ active, payload, label }) => active && payload?.length ? (
        <ChartTipCard
          title={new Date(Number(label ?? payload[0]?.payload?.time)).toLocaleString("zh-CN", { hour12: false })}
          rows={payload.filter((item) => item.value != null).map((item) => {
            const series = colored.find((candidate) => `v${candidate.varSeq}` === item.dataKey || candidate.name === item.name);
            const value = typeof item.value === "number" ? `${Number.isInteger(item.value) ? item.value : item.value.toFixed(1)}${series?.unit ? ` ${series.unit}` : ""}` : String(item.value);
            return { key: String(item.dataKey ?? item.name), label: series?.name ?? String(item.name), color: series?.color ?? "var(--brand)", value };
          })}
        />
      ) : null}
    />
  );
  const brush = chartRows.length > 1 ? (
    <Brush
      key={brushVersion}
      dataKey="time"
      height={40}
      travellerWidth={8}
      stroke="var(--brand)"
      fill="var(--card)"
      tickFormatter={(value) => shortClock(Number(value))}
      onChange={handleBrushChange}
    >
      <LineChart data={chartRows}>
        {colored.map((series) => <Line key={series.varSeq} type="monotone" dataKey={`v${series.varSeq}`} stroke={series.color} strokeOpacity={0.45} strokeWidth={1} dot={false} activeDot={false} isAnimationActive={false} />)}
      </LineChart>
    </Brush>
  ) : null;
  const defs = (
    <defs>
      {visible.map((series) => (
        <linearGradient key={series.varSeq} id={`hi-grad-${series.varSeq}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" style={{ stopColor: series.color, stopOpacity: 0.22 }} />
          <stop offset="100%" style={{ stopColor: series.color, stopOpacity: 0 }} />
        </linearGradient>
      ))}
      <pattern id="hi-gap" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
        <line x1="0" y1="0" x2="0" y2="6" style={{ stroke: "var(--muted-foreground)", strokeOpacity: 0.3 }} strokeWidth={2} />
      </pattern>
    </defs>
  );
  const gapAreas = gaps.map(([x1, x2]) => (
    <ReferenceArea key={x1} x1={x1} x2={x2} fill="url(#hi-gap)" fillOpacity={1} ifOverflow="hidden" label={{ value: "无数据", position: "insideTop", fontSize: 11, fill: "var(--muted-foreground)" }} />
  ));
  const common = <>{defs}<CartesianGrid vertical={false} strokeDasharray="3 4" stroke="var(--border)" />{timeAxis}<YAxis tickLine={false} axisLine={false} width={44} fontSize={11} domain={["auto", "auto"]} />{tooltip}{gapAreas}{brush}</>;
  const margin = { top: 8, right: 12, bottom: 0, left: 0 };
  const zoomReset = zoomDomain ? (
    <Button className="absolute right-1.5 top-0.5 z-10 h-7 gap-1.5 px-2.5 text-xs" size="sm" variant="outline" title="重置缩放" aria-label="重置缩放" onClick={resetZoom}>
      <RotateCcw className="size-3.5" />重置缩放
    </Button>
  ) : null;

  let body: ReactNode;
  if (chartType === "scatter") {
    body = (
      <ScatterChart data={chartRows} margin={margin} accessibilityLayer>
        {defs}
        <CartesianGrid strokeDasharray="3 4" stroke="var(--border)" />
        {timeAxis}
        <YAxis type="number" dataKey="value" tickLine={false} axisLine={false} width={44} fontSize={11} domain={["auto", "auto"]} />
        {tooltip}
        {visible.map((series) => <Scatter key={series.varSeq} name={series.name} fill={series.color} fillOpacity={0.75} shape={(props: { cx?: number; cy?: number }) => <circle cx={props.cx} cy={props.cy} r={2.5} fill={series.color} fillOpacity={0.75} />} data={series.data.filter(([, value]) => value != null).map(([time, value]) => ({ time: parseHistoryTime(time), value }))} />)}
        {brush}
      </ScatterChart>
    );
  } else if (chartType === "bar") {
    body = <BarChart data={chartRows} margin={margin} accessibilityLayer>{common}{visible.map((series) => <Bar key={series.varSeq} dataKey={`v${series.varSeq}`} fill={series.color} radius={[3, 3, 0, 0]} isAnimationActive={false} />)}</BarChart>;
  } else {
    body = <AreaChart data={chartRows} margin={margin} accessibilityLayer>{common}{visible.map((series) => <Area key={series.varSeq} type="monotone" dataKey={`v${series.varSeq}`} stroke={series.color} fill={chartType === "area" ? `url(#hi-grad-${series.varSeq})` : "transparent"} strokeWidth={2} dot={false} activeDot={{ r: 4.5, stroke: "var(--card)", strokeWidth: 2, fill: series.color }} isAnimationActive={false} />)}</AreaChart>;
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="mb-2 flex shrink-0 flex-wrap items-center gap-3.5">
        <div className="flex flex-wrap gap-1.5">{colored.map((series) => <LegendPill key={series.varSeq} color={series.color} label={series.name} off={hidden.includes(series.varSeq)} onClick={() => setHidden((current) => current.includes(series.varSeq) ? current.filter((value) => value !== series.varSeq) : [...current, series.varSeq])} />)}</div>
        <div className="ml-auto flex gap-4 text-xs tabular-nums text-muted-foreground">
          <span>显示区间 <b className="font-semibold text-foreground">{shortClock(axisDomain[0])} – {shortClock(axisDomain[1])}</b></span>
          <span>数据点 <b className="font-semibold text-foreground">{visibleCount}</b></span>
          <span>缺失区间 <b className="font-semibold text-foreground">{gaps.length}</b> 段</span>
        </div>
      </div>
      {colored.length > 0 && (
        <div className="relative flex min-h-[200px] flex-1 flex-col">
          <ChartContainer config={config} className="h-full min-h-0 w-full flex-1 aspect-auto">{body}</ChartContainer>
          {zoomReset}
        </div>
      )}
      {boolSeries.length > 0 && (
        <div className={colored.length ? "shrink-0" : "min-h-0 flex-1 overflow-y-auto"}>
          <div className="mb-2 mt-3.5 flex items-center gap-2 text-xs font-medium text-muted-foreground">Bool 型点位<span className="h-px flex-1 bg-border" /></div>
          <HistoryBoolLanes series={boolSeries} domain={axisDomain} />
        </div>
      )}
    </div>
  );
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
  const chartRequestRef = useRef<AbortController | null>(null);
  const matrixRequestRef = useRef<AbortController | null>(null);
  useEffect(() => {
    ++queryVersionRef.current;
    ++matrixVersionRef.current;
    setLoading(false);
    setMatrixLoading(false);
    return () => {
      ++queryVersionRef.current;
      ++matrixVersionRef.current;
      chartRequestRef.current?.abort();
      matrixRequestRef.current?.abort();
    };
  }, [deviceId, selectedPoints, startTime, endTime, interval, aggregate, token]);

  const loadDevices = useCallback((backendToken: string, signal: AbortSignal) => monitorClient.historyDevices(backendToken, signal), []);
  const loadTypes = useCallback((backendToken: string, signal: AbortSignal) => monitorClient.historyDeviceTypes(backendToken, signal), []);
  const loadPoints = useCallback((backendToken: string, signal: AbortSignal) => monitorClient.historyPoints(backendToken, deviceId, signal), [deviceId]);
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
    if (!token || !snapshot || refreshVersion === 0) return;
    let active = true;
    chartRequestRef.current?.abort();
    matrixRequestRef.current?.abort();
    const chartController = new AbortController();
    const matrixController = new AbortController();
    chartRequestRef.current = chartController;
    matrixRequestRef.current = matrixController;
    const queryVersion = ++queryVersionRef.current;
    const matrixVersion = ++matrixVersionRef.current;
    setLoading(true);
    setMatrixLoading(true);
    Promise.all([
      monitorClient.historyChart(token, snapshot.query, chartController.signal).then((nextChart) => {
        if (active && queryVersion === queryVersionRef.current) setChart(nextChart);
      }),
      monitorClient.historyRawMatrix(token, {
        ...snapshot.query,
        alignGranularity: matrixAlignRef.current,
        pageSize: 200,
      }, matrixController.signal).then((nextMatrix) => {
        if (active && queryVersion === queryVersionRef.current && matrixVersion === matrixVersionRef.current) setMatrix(nextMatrix);
      }).catch((reason) => {
        if (!matrixController.signal.aborted) throw reason;
      }),
    ])
      .catch((reason) => {
        if (!active || chartController.signal.aborted || queryVersion !== queryVersionRef.current) return;
        if ((reason as { type?: string })?.type === "unauthorized") logout();
        else toast.error(historyErrorMessage(reason, "历史数据刷新失败"));
      })
      .finally(() => {
        if (!active || queryVersion !== queryVersionRef.current) return;
        setLoading(false);
        if (matrixVersion === matrixVersionRef.current) setMatrixLoading(false);
      });
    return () => {
      active = false;
      chartController.abort();
      matrixController.abort();
    };
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
    matrixRequestRef.current?.abort();
    const controller = new AbortController();
    matrixRequestRef.current = controller;
    const isCurrent = () => !controller.signal.aborted && matrixVersion === matrixVersionRef.current && queryVersion === queryVersionRef.current;
    setMatrixLoading(true);
    try {
      const result = await monitorClient.historyRawMatrix(token, { ...effectiveQuery, alignGranularity: granularity, pageSize: 200, cursorTs }, controller.signal);
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
    chartRequestRef.current?.abort();
    matrixRequestRef.current?.abort();
    const controller = new AbortController();
    chartRequestRef.current = controller;
    const isCurrent = () => !controller.signal.aborted && queryVersion === queryVersionRef.current;
    setLoading(true);
    setChart(null);
    setMatrix(null);
    try {
      const result = await monitorClient.historyChart(token, query, controller.signal);
      if (!isCurrent()) return;
      lastQueryRef.current = { query };
      setChart(result);
      void loadMatrix(matrixAlign, true, undefined, query);
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

  const submittedQuery = lastQueryRef.current?.query;

  const aggregateLabel = ({ avg: "平均值", max: "最大值", min: "最小值", last: "最新值" } as Record<string, string>)[submittedQuery?.aggregate ?? aggregate] ?? aggregate;
  const metaBySeq = new Map((chart?.series ?? []).map((series) => [series.varSeq, { unit: series.unit, dataType: series.dataType }]));
  const matrixColumnColor = (prop: string) => {
    const index = (chart?.series ?? []).findIndex((series) => `v${series.varSeq}` === prop);
    return index >= 0 ? COLORS[index % COLORS.length] : null;
  };

  return (
    <div className="mx-auto flex h-full w-full max-w-[1800px] flex-col gap-3">
      <div className="flex h-full min-h-[560px] shrink-0 flex-col gap-3">
      <Card className="shrink-0 gap-0 py-0">
        <div className="flex flex-wrap items-center gap-1.5 whitespace-nowrap border-b px-5 py-3.5">
        <span className="text-xs text-muted-foreground">当前设备</span>
        <Button variant="outline" className={`${OUTLINE_BTN} max-w-56 font-mono text-[12.5px]`} onClick={openDeviceDialog}><span className="truncate">{currentDevice ? currentDevice.deviceCode ?? currentDevice.name : "选择设备"}</span><ChevronDown className="size-3.5 text-muted-foreground" /></Button>
        <Button variant="outline" className={OUTLINE_BTN} onClick={openPointDialog} disabled={!deviceId}><Search className="size-3.5" />选择点位{selectedPoints.length > 0 && <CountChip count={selectedPoints.length} />}</Button>
        <span className="mx-0.5 h-5 w-px bg-border" />
        <Select value={quickRange} onValueChange={(value) => { setQuickRange(value); setRecentRange(Number(value), setStartTime, setEndTime); }}>
          <SelectTrigger className={`${CTRL} w-[110px] !text-xs`} aria-label="快捷时间范围" title="快捷时间范围"><SelectValue placeholder="快捷时间范围" /></SelectTrigger>
          <SelectContent>{[["1", "最近1小时"], ["6", "最近6小时"], ["12", "最近12小时"], ["24", "最近24小时"]].map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}</SelectContent>
        </Select>
        <div className="inline-flex h-8 items-center gap-1.5 rounded-md border border-input bg-input/20 pl-2.5 pr-1 text-xs text-muted-foreground dark:bg-input/30" aria-label="时间范围"><CalendarDays className="size-3.5 shrink-0" /><input className="w-[148px] bg-transparent text-xs tabular-nums text-foreground outline-none [color-scheme:light] dark:[color-scheme:dark]" type="datetime-local" value={startTime} onChange={(event) => setStartTime(event.target.value)} aria-label="开始时间" /><span>至</span><input className="w-[148px] bg-transparent text-xs tabular-nums text-foreground outline-none [color-scheme:light] dark:[color-scheme:dark]" type="datetime-local" value={endTime} onChange={(event) => setEndTime(event.target.value)} aria-label="结束时间" /></div>
        <Select value={interval} onValueChange={setIntervalValue}><SelectTrigger className={`${CTRL} w-[88px]`} aria-label="时间对齐颗粒度" title="时间对齐颗粒度"><SelectValue /></SelectTrigger><SelectContent>{INTERVALS.map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}</SelectContent></Select>
        <Select value={aggregate} onValueChange={setAggregate}><SelectTrigger className={`${CTRL} w-[84px]`} aria-label="值统计" title="值统计"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="avg">平均值</SelectItem><SelectItem value="max">最大值</SelectItem><SelectItem value="min">最小值</SelectItem><SelectItem value="last">最新值</SelectItem></SelectContent></Select>
        {/* Matrix alignment is configured with the data table below. */}
        <div className="ml-auto flex items-center gap-2">
          <Button variant="outline" className={OUTLINE_BTN} onClick={openExportDialog} disabled={!chart?.series.length}><Download className="size-3.5" />导出</Button>
          <Button className={PRIMARY_BTN} onClick={() => void queryHistory()} disabled={loading || !deviceId || !selectedPoints.length}><Search className="size-3.5" />{loading ? "查询中..." : "查询"}</Button>
        </div>
        </div>
        <CardContent className="p-3">
          {selectedPointItems.length ? <div className="flex gap-2 overflow-x-auto overflow-y-hidden pb-1 [scrollbar-width:thin]">{selectedPointItems.map((point, index) => { const meta = metaBySeq.get(point.varSeq); return <div key={point.varSeq} className="flex h-9 max-w-[260px] shrink-0 items-center gap-2 whitespace-nowrap rounded-[10px] bg-muted/55 px-3"><span className="size-2 shrink-0 rounded-full" style={{ background: COLORS[index % COLORS.length] }} /><span className="truncate text-[12.5px] font-medium">{point.pointName}</span><span className="truncate font-mono text-[10.5px] text-muted-foreground">{[point.ioAddress, meta?.dataType, meta?.unit].filter(Boolean).join(" · ")}</span></div>; })}</div> : <div className="flex h-9 items-center justify-center gap-2 text-[13px] text-muted-foreground"><Search className="size-4 opacity-50" /><span className="font-medium text-foreground">已选点位</span>暂未选择查询点位</div>}
        </CardContent>
      </Card>

      <Card className="flex min-h-0 min-w-0 flex-1 flex-col gap-0 overflow-hidden py-0">
        <CardHeader className="!flex shrink-0 items-center gap-3 border-b px-5 py-3.5">
          <div className="min-w-0">
            <p className="text-sm font-semibold leading-none">历史趋势图分析</p>
            <p className="mt-1.5 truncate text-[13px] text-muted-foreground">{currentDevice?.deviceCode ?? currentDevice?.name ?? "未选择设备"}{submittedQuery ? ` · ${submittedQuery.startTime.slice(0, 16)} ~ ${submittedQuery.endTime.slice(11, 16)} · 间隔 ${chart?.interval ?? submittedQuery.interval} · ${aggregateLabel}` : " · 请选择点位和时间范围后查询"}</p>
          </div>
          <div className="ml-auto"><SegTabs<ChartType> value={chartType} options={[["line", "折线图"], ["area", "面积图"], ["bar", "柱状图"], ["scatter", "散点图"]]} onChange={setChartType} /></div>
        </CardHeader>
        <CardContent className="flex min-h-0 flex-1 flex-col px-5 pb-5 pt-4">{loading ? <Skeleton className="h-full min-h-40" /> : chart?.series.length ? <HistoryChart key={(submittedQuery?.startTime ?? "") + "-" + (submittedQuery?.endTime ?? "")} chart={chart} chartType={chartType} rangeStart={submittedQuery?.startTime ?? apiTime(startTime)} rangeEnd={submittedQuery?.endTime ?? apiTime(endTime)} /> : <div className="flex h-full min-h-40 flex-col items-center justify-center gap-3 text-sm text-muted-foreground"><LineChartIcon className="size-9 opacity-35" />请选择设备、点位和时间范围后查询</div>}</CardContent>
      </Card>
      </div>

      <Card className="shrink-0 gap-0 py-0">
        <div className="flex flex-wrap items-center gap-3 px-5 pt-5">
          <div><p className="text-[15px] font-semibold leading-none">历史数据矩阵</p><p className="mt-1.5 text-[13px] text-muted-foreground">按对齐粒度展开的原始采样，未补值</p></div>
          <div className="ml-auto flex items-center gap-2"><Label className="text-xs font-normal text-muted-foreground">对齐粒度</Label><Select value={matrixAlign} onValueChange={(value) => { const next = value as AlignGranularity; setMatrixAlign(next); void loadMatrix(next, true); }}><SelectTrigger className={`${CTRL} w-28`}><SelectValue /></SelectTrigger><SelectContent><SelectItem value="1s">1秒</SelectItem><SelectItem value="500ms">500毫秒</SelectItem><SelectItem value="100ms">100毫秒</SelectItem></SelectContent></Select></div>
        </div>
        <CardContent className="space-y-3 px-5 pb-5 pt-4">
          <div className="flex items-center gap-2 text-xs text-muted-foreground"><SoftBadge tone="muted" dot={false}>fillMode: {matrix?.meta.fillMode ?? "none"}</SoftBadge><SoftBadge tone="muted" dot={false}>每页 200 行</SoftBadge><span className="ml-auto tabular-nums">已加载 {matrix?.rows.length ?? 0} 行{matrix?.rows.length ? ` · 起始 ${String(matrix.rows[0].ts).replace("T", " ").slice(0, 19)}` : ""}</span></div>
          <div className={`max-h-[420px] overflow-auto ${TABLE_FRAME}`}><Table className={TABLE_TEXT}><TableHeader className="sticky top-0 z-10 bg-card"><TableRow className={HEAD_ROW}>{(matrix?.columns ?? [{ prop: "ts", label: "采样时间" }]).map((column) => { const color = matrixColumnColor(column.prop); return <TableHead key={column.prop} className={`${TH} ${column.prop === "ts" ? "min-w-52" : "min-w-32"}`}>{color && <span className="mr-2 inline-block size-1.5 rounded-full align-[1px]" style={{ background: color }} />}{column.label}{column.unit && <span className="ml-0.5 font-normal opacity-75">({column.unit})</span>}</TableHead>; })}</TableRow></TableHeader><TableBody>{matrixLoading && !matrix ? <TableRow><TableCell colSpan={1} className={TD}><Skeleton className="h-28" /></TableCell></TableRow> : matrix?.rows.length ? matrix.rows.map((row, index) => <TableRow key={`${String(row.ts)}-${index}`} className="h-10">{matrix.columns.map((column) => { const value = row[column.prop]; return <TableCell key={column.prop} className={`${TD} ${column.prop === "ts" ? "whitespace-nowrap font-mono text-xs text-muted-foreground" : value == null ? "font-mono text-xs text-muted-foreground/60" : "font-mono text-xs"}`}>{String(value ?? "--")}</TableCell>; })}</TableRow>) : <TableRow><TableCell colSpan={matrix?.columns.length ?? 1} className="h-28 text-center text-muted-foreground">暂无原始数据</TableCell></TableRow>}</TableBody></Table></div>{matrix?.nextCursor && <div className="flex justify-center"><Button variant="outline" className={OUTLINE_BTN} onClick={() => void loadMatrix(matrixAlign, false, matrix.nextCursor ?? undefined)} disabled={matrixLoading}>{matrixLoading ? "加载中..." : "加载更多"}</Button></div>}</CardContent>
      </Card>

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
