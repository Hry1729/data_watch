import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { backendClient } from "@/lib/backend-client";
import { DeviceCatalogPagination } from "@/components/DeviceCatalogPagination";
import { cn } from "@/lib/utils";

const DEVICE_TYPE_LABELS: Record<string, string> = {
  qc: "桥吊（QC）",
  "桥吊": "桥吊（QC）",
  asc: "轨道吊（ASC）",
  "轨道吊": "轨道吊（ASC）",
  agv: "自动导引车（AGV）",
  "自动导引车": "自动导引车（AGV）",
};

function formatDeviceType(type: string) {
  const normalizedType = type.trim().toLowerCase();
  return DEVICE_TYPE_LABELS[normalizedType] ?? type;
}

function formatDeviceCode(code: string) {
  if (!code) return "-";
  const match = code.match(/^([A-Za-z]+)?-?(\d+)$/);
  if (!match) return code;
  const prefix = (match[1] || "").toLowerCase();
  const number = match[2];
  if (prefix === "asc" && number.length === 4) {
    const last = number[3];
    if (last === "1") return `${number.slice(0, 3)}L`;
    if (last === "2") return `${number.slice(0, 3)}S`;
  }
  return number;
}

function getDeviceTone(connectStatus?: string, deviceStatus?: string) {
  if (deviceStatus === "disabled") return "bg-muted text-muted-foreground hover:bg-muted/80";
  if (connectStatus === "online") return "bg-[color-mix(in_oklab,var(--success)_14%,var(--card))] text-[color-mix(in_oklab,var(--success)_82%,var(--foreground))] shadow-[inset_0_0_0_1px_color-mix(in_oklab,var(--success)_32%,transparent)]";
  if (connectStatus === "unstable") return "bg-warning text-white dark:text-[#1f1500]";
  return "bg-destructive text-white";
}

/* 色块墙：最小色块边长与间距（px），列数随宽度自适应 */
const TILE_MIN = 46;
const TILE_GAP = 6;
const DEFAULT_PAGE_SIZE = 150;

/* 按色块墙可视区估算一页能放下的设备数（取整到 10，至少 40） */
function estimatePageSize(width: number, height: number) {
  if (width <= 0 || height <= 0) return DEFAULT_PAGE_SIZE;
  const cols = Math.max(1, Math.floor((width + TILE_GAP) / (TILE_MIN + TILE_GAP)));
  const tile = (width - TILE_GAP * (cols - 1)) / cols;
  const rows = Math.max(1, Math.floor((height + TILE_GAP) / (tile + TILE_GAP)));
  return Math.max(40, Math.floor((cols * rows) / 10) * 10);
}

function LegendCount({ dot, label, value }: { dot?: string; label: string; value: number | null }) {
  return (
    <span className="flex items-center gap-1.5">
      {dot && <span className={cn("size-[9px] rounded-[3px]", dot)} />}
      {label}
      <b className="font-semibold tabular-nums text-foreground">{value == null ? "—" : value}</b>
    </span>
  );
}

export default function DeviceCommunicationPage() {
  const navigate = useNavigate();
  const [keyword, setKeyword] = useState("");
  const [deviceType, setDeviceType] = useState("all");
  const [status, setStatus] = useState("all");
  const [page, setPage] = useState(1);
  const wallRef = useRef<HTMLDivElement>(null);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  useEffect(() => {
    const node = wallRef.current;
    if (!node) return;
    let current = DEFAULT_PAGE_SIZE;
    const update = () => {
      const next = estimatePageSize(node.clientWidth, node.clientHeight);
      if (next === current) return;
      current = next;
      setPageSize(next);
      setPage(1);
    };
    update();
    const observer = new ResizeObserver(update);
    observer.observe(node);
    return () => observer.disconnect();
  }, []);
  const load = useCallback(
    (token: string, signal: AbortSignal) => backendClient.deviceList(token, {
      pageNo: page, pageSize, keyword: keyword.trim() || undefined,
      deviceTypeExact: deviceType === "all" ? undefined : deviceType,
      connectStatus: status === "all" || status === "disabled" ? undefined : status,
      deviceStatus: status === "disabled" ? "disabled" : undefined,
    }, signal),
    [deviceType, keyword, page, pageSize, status],
  );
  const query = useBackendQuery(load, { intervalMs: 30_000 });
  const devices = query.data?.list ?? [];
  const loadSummary = useCallback((token: string, signal: AbortSignal) => backendClient.deviceSummary(token, { keyword: keyword.trim() || undefined }, signal), [keyword]);
  const summary = useBackendQuery(loadSummary, { intervalMs: 30_000 });
  const loadTypes = useCallback((token: string, signal: AbortSignal) => backendClient.deviceTypes(token, {}, signal), []);
  const typeCatalog = useBackendQuery(loadTypes);
  const groups = useMemo(() => {
    const grouped = devices.reduce<Record<string, typeof devices>>((result, device) => {
      const type = device.deviceType || "其他";
      (result[type] ??= []).push(device);
      return result;
    }, {});
    return Object.entries(grouped);
  }, [devices]);
  const total = summary.data?.total ?? 0;
  const online = summary.data?.enabledOnlineCount ?? 0;
  const offline = summary.data?.enabledOfflineCount ?? 0;
  const disabled = summary.data?.disabledCount ?? 0;
  const unstable = summary.data?.unstableCount ?? 0;
  const types = typeCatalog.data ?? [];
  return (
    <div className="flex h-full min-h-0 w-full flex-col gap-4">
      <Card className="flex min-h-0 flex-1 flex-col">
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2 px-5 pt-1">
          <CardTitle className="text-[15px] font-semibold leading-none">设备通讯</CardTitle>
          <div className="flex flex-wrap items-center gap-x-3.5 gap-y-2 text-xs text-muted-foreground">
            <LegendCount label="总数" value={summary.data ? total : null} />
            <span className="text-border">·</span>
            <LegendCount dot="bg-success" label="在线" value={summary.data ? online : null} />
            <LegendCount dot="bg-destructive" label="离线" value={summary.data ? offline : null} />
            <LegendCount dot="bg-warning" label="异常" value={summary.data ? unstable : null} />
            <LegendCount dot="bg-muted-foreground/50" label="未启用" value={summary.data ? disabled : null} />
          </div>
          <div className="ml-auto flex min-w-0 flex-1 flex-wrap items-center justify-end gap-2">
            <div className="relative w-[150px] shrink-0">
              <Search className="absolute left-[9px] top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
              <Input className="h-8 pl-[30px] text-[13px]" placeholder="编号 / 名称" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} />
            </div>
            <Select value={deviceType} onValueChange={(value) => { setDeviceType(value); setPage(1); }}>
              <SelectTrigger className="w-28"><SelectValue placeholder="全部类型" /></SelectTrigger>
              <SelectContent>
                <SelectGroup>
                  <SelectItem value="all">全部类型</SelectItem>
                  {types.map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}
                </SelectGroup>
              </SelectContent>
            </Select>
            <Select value={status} onValueChange={(value) => { setStatus(value); setPage(1); }}>
              <SelectTrigger className="w-28"><SelectValue placeholder="全部状态" /></SelectTrigger>
              <SelectContent>
                <SelectGroup>
                  <SelectItem value="all">全部状态</SelectItem>
                  <SelectItem value="online">在线</SelectItem>
                  <SelectItem value="unstable">异常</SelectItem>
                  <SelectItem value="offline">离线</SelectItem>
                  <SelectItem value="disabled">未启用</SelectItem>
                </SelectGroup>
              </SelectContent>
            </Select>
            <Button variant="outline" size="sm" onClick={() => { setKeyword(""); setDeviceType("all"); setStatus("all"); setPage(1); }}>重置</Button>
          </div>
        </div>
        <CardContent className="flex min-h-0 flex-1 flex-col gap-0 px-5 pb-1 pt-1">
          <div ref={wallRef} className="min-h-0 flex-1 overflow-auto">
            {query.loading ? (
              <div className="grid w-full grid-cols-[repeat(auto-fill,minmax(46px,1fr))] gap-1.5">
                {Array.from({ length: 40 }, (_, index) => <Skeleton key={index} className="aspect-square w-full rounded-[7px]" />)}
              </div>
            ) : query.error ? (
              <p className="py-16 text-center text-sm text-destructive">{query.error}</p>
            ) : groups.length ? (
              <div className="flex flex-col gap-[18px] pt-1">
                {groups.map(([type, rows]) => {
                  const onlineCount = rows.filter((device) => device.deviceStatus !== "disabled" && device.connectStatus === "online").length;
                  return (
                    <section key={type}>
                      <div className="mb-2.5 flex items-center gap-2.5">
                        <h3 className="text-sm font-semibold">{formatDeviceType(type)}</h3>
                        <Badge className="h-5 rounded-md bg-muted px-2 text-xs font-medium tabular-nums text-muted-foreground">{rows.length}</Badge>
                        <span className="text-xs tabular-nums text-muted-foreground">在线 {onlineCount} / {rows.length}</span>
                      </div>
                      <div className="grid w-full grid-cols-[repeat(auto-fill,minmax(46px,1fr))] gap-1.5">
                        {rows.map((device) => (
                          <button
                            key={device.id}
                            type="button"
                            className={cn(
                              "flex aspect-square w-full items-center justify-center truncate rounded-[7px] p-1 text-center text-[11.5px] font-semibold tabular-nums transition-[transform,box-shadow] hover:z-[2] hover:-translate-y-0.5 hover:shadow-[0_6px_14px_-6px_rgba(0,0,0,0.35)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/40",
                              getDeviceTone(device.connectStatus, device.deviceStatus),
                            )}
                            style={device.deviceStatus === "disabled" ? {
                              background: "repeating-linear-gradient(-45deg, var(--muted) 0 3px, color-mix(in oklab, var(--muted-foreground) 22%, var(--muted)) 3px 6px)",
                            } : undefined}
                            title={device.deviceName || device.deviceCode}
                            aria-label={`${device.deviceCode} ${device.deviceName}`}
                            onClick={() => navigate(`/device/realtime?deviceId=${device.id}`)}
                          >
                            {formatDeviceCode(device.deviceCode)}
                          </button>
                        ))}
                      </div>
                    </section>
                  );
                })}
              </div>
            ) : (
              <p className="py-16 text-center text-sm text-muted-foreground">暂无设备数据</p>
            )}
          </div>
          <div className="mt-5 border-t border-border pb-4 pt-3.5">
            <DeviceCatalogPagination total={query.data?.total ?? 0} page={page} pageSize={pageSize} loading={query.loading} onPageChange={setPage} />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
