import { useCallback, useMemo, useState } from "react";
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
import { MonitorKpiCard } from "@/components/MonitorKpiCard";
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

function KpiCard({ label, value, unit, detail }: { label: string; value: string; unit?: string; detail: string }) {
  return <MonitorKpiCard size="lg" label={label} value={value} unit={unit} detail={detail} />;
}

function formatPercent(value: number | null) {
  return value == null ? "—" : value.toFixed(1);
}

export default function DeviceCommunicationPage() {
  const navigate = useNavigate();
  const [keyword, setKeyword] = useState("");
  const [deviceType, setDeviceType] = useState("all");
  const [status, setStatus] = useState("all");
  const [page, setPage] = useState(1);
  const pageSize = 150;
  const load = useCallback(
    (token: string, signal: AbortSignal) => backendClient.deviceList(token, {
      pageNo: page, pageSize, keyword: keyword.trim() || undefined,
      deviceTypeExact: deviceType === "all" ? undefined : deviceType,
      connectStatus: status === "all" || status === "disabled" ? undefined : status,
      deviceStatus: status === "disabled" ? "disabled" : undefined,
    }, signal),
    [deviceType, keyword, page, status],
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
  const onlinePercent = summary.data ? (total ? online / total * 100 : 0) : null;
  const offlinePercent = summary.data ? (total ? offline / total * 100 : 0) : null;
  const disabledPercent = summary.data ? (total ? disabled / total * 100 : 0) : null;

  return (
    <div className="flex h-full min-h-0 w-full flex-col gap-4">
      <div className="grid shrink-0 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <KpiCard
          label="设备总数"
          value={summary.data ? String(total) : "—"}
          detail={summary.data ? `${online} 在线 · ${unstable} 异常 · ${offline} 离线 · ${disabled} 未启用` : "—"}
        />
        <KpiCard
          label="在线比例"
          value={formatPercent(onlinePercent)}
          unit={onlinePercent == null ? undefined : "%"}
          detail={summary.data ? `${online} / ${total} 台` : "—"}
        />
        <KpiCard
          label="离线比例"
          value={formatPercent(offlinePercent)}
          unit={offlinePercent == null ? undefined : "%"}
          detail={summary.data ? `${offline} / ${total} 台` : "—"}
        />
        <KpiCard
          label="未用比例"
          value={formatPercent(disabledPercent)}
          unit={disabledPercent == null ? undefined : "%"}
          detail={summary.data ? `${disabled} / ${total} 台` : "—"}
        />
      </div>

      <Card className="flex min-h-0 flex-1 flex-col">
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2 px-5 pt-1">
          <CardTitle className="text-[15px] font-semibold leading-none">设备通讯</CardTitle>
          <div className="flex flex-wrap items-center gap-x-3.5 gap-y-2 text-xs text-muted-foreground">
            <span className="flex items-center gap-1.5"><span className="size-[9px] rounded-[3px] bg-success" />在线</span>
            <span className="flex items-center gap-1.5"><span className="size-[9px] rounded-[3px] bg-destructive" />离线</span>
            <span className="flex items-center gap-1.5"><span className="size-[9px] rounded-[3px] bg-warning" />异常</span>
            <span className="flex items-center gap-1.5"><span className="size-[9px] rounded-[3px] bg-muted-foreground/50" />未启用</span>
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
          <div className="min-h-0 flex-1 overflow-auto">
            {query.loading ? (
              <div className="grid w-full grid-cols-[repeat(20,minmax(0,1fr))] gap-1.5">
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
                      <div className="grid w-full grid-cols-[repeat(20,minmax(0,1fr))] gap-1.5">
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
