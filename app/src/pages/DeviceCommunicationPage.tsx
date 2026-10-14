import { useCallback, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { HardDrive, Search } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Progress, type ProgressVariant } from "@/components/ui/progress";
import { Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { backendClient } from "@/lib/backend-client";
import { loadAllDevicePages } from "@/lib/device-catalog";
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
  const match = code.match(/^([A-Za-z]+)?(\d+)$/);
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
  if (deviceStatus === "disabled") return "bg-muted text-foreground hover:bg-muted/80";
  if (connectStatus === "online") return "bg-success text-white hover:bg-success/90";
  if (connectStatus === "unstable") return "bg-warning text-white hover:bg-warning/90";
  return "bg-destructive text-white hover:bg-destructive/90";
}

function RateCard({ title, value, variant }: { title: string; value: number | null; variant: ProgressVariant }) {
  return (
    <Card size="sm" className="bg-card">
      <CardContent className="flex min-h-24 flex-col justify-center py-2">
        <p className="text-xs text-muted-foreground">{title}</p>
        <p className="mt-1 text-2xl font-semibold tabular-nums">{value == null ? "—" : `${value.toFixed(1)}%`}</p>
        <Progress className="mt-2" value={value ?? 0} variant={variant} aria-label={value == null ? `${title} 暂不可用` : `${title} ${value.toFixed(1)}%`} />
      </CardContent>
    </Card>
  );
}

export default function DeviceCommunicationPage() {
  const navigate = useNavigate();
  const [keyword, setKeyword] = useState("");
  const [deviceType, setDeviceType] = useState("all");
  const [status, setStatus] = useState("all");
  const load = useCallback(
    (token: string) => loadAllDevicePages((pageNo, pageSize) => backendClient.deviceList(token, { pageNo, pageSize, keyword: keyword.trim() || undefined })),
    [keyword],
  );
  const query = useBackendQuery(load, { intervalMs: 30_000 });
  const devices = query.data?.list ?? [];

  const filtered = useMemo(
    () => devices.filter((device) => (deviceType === "all" || device.deviceType === deviceType) && (status === "all" || device.connectStatus === status || (status === "disabled" && device.deviceStatus === "disabled"))),
    [deviceType, devices, status],
  );
  const groups = useMemo(() => {
    const grouped = filtered.reduce<Record<string, typeof filtered>>((result, device) => {
      const type = device.deviceType || "其他";
      (result[type] ??= []).push(device);
      return result;
    }, {});
    return Object.entries(grouped);
  }, [filtered]);
  const total = query.data?.total ?? devices.length;
  const online = devices.filter((device) => device.connectStatus === "online" && device.deviceStatus === "enabled").length;
  const offline = devices.filter((device) => device.connectStatus === "offline" && device.deviceStatus === "enabled").length;
  const disabled = devices.filter((device) => device.deviceStatus === "disabled").length;
  const types = [...new Set(devices.map((device) => device.deviceType))];

  return (
    <div className="flex w-full flex-col gap-4 px-4 pb-4 md:px-6 md:pb-6">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Card size="sm" className="bg-card">
          <CardContent className="flex min-h-24 items-center justify-between gap-3 py-2">
            <div>
              <p className="text-xs text-muted-foreground">设备总数</p>
              <p className="mt-1 text-2xl font-semibold tabular-nums">{total}</p>
            </div>
            <div className="flex size-10 shrink-0 items-center justify-center rounded-md bg-primary/10 text-primary">
              <HardDrive className="size-6" />
            </div>
          </CardContent>
        </Card>
        <RateCard title="在线比例" value={query.data?.truncated ? null : total ? online / total * 100 : 0} variant="success" />
        <RateCard title="离线比例" value={query.data?.truncated ? null : total ? offline / total * 100 : 0} variant="destructive" />
        <RateCard title="未用比例" value={query.data?.truncated ? null : total ? disabled / total * 100 : 0} variant="warning" />
      </div>

      {query.data?.truncated && <p className="text-xs text-warning">设备超过选择器安全上限 {devices.length} 台，已加载前 {devices.length} 台；请使用关键字缩小范围。</p>}
      <Card>
        <CardHeader className="flex flex-row items-center gap-3">
          <div className="flex shrink-0 items-center gap-4">
            <CardTitle>设备通讯</CardTitle>
            <div className="flex items-center gap-3 text-xs text-muted-foreground">
              <span className="flex items-center gap-1.5"><span className="size-2 rounded-sm bg-success" />在线</span>
              <span className="flex items-center gap-1.5"><span className="size-2 rounded-sm bg-destructive" />离线</span>
              <span className="flex items-center gap-1.5"><span className="size-2 rounded-sm bg-warning" />异常</span>
              <span className="flex items-center gap-1.5"><span className="size-2 rounded-sm bg-muted-foreground/50" />未启用</span>
            </div>
          </div>
          <div className="ml-auto flex min-w-0 flex-1 items-center justify-end gap-2">
            <div className="relative w-32 shrink-0">
              <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input className="pl-9" placeholder="编号 / 名称" value={keyword} onChange={(event) => setKeyword(event.target.value)} />
            </div>
            <Select value={deviceType} onValueChange={setDeviceType}>
              <SelectTrigger className="w-24"><SelectValue placeholder="全部类型" /></SelectTrigger>
              <SelectContent>
                <SelectGroup>
                  <SelectItem value="all">全部类型</SelectItem>
                  {types.map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}
                </SelectGroup>
              </SelectContent>
            </Select>
            <Select value={status} onValueChange={setStatus}>
              <SelectTrigger className="w-24"><SelectValue placeholder="全部状态" /></SelectTrigger>
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
            <Button variant="outline" size="sm" onClick={() => { setKeyword(""); setDeviceType("all"); setStatus("all"); }}>重置</Button>
          </div>
        </CardHeader>
        <CardContent>

          {query.loading ? (
            <div className="grid w-full grid-cols-[repeat(15,minmax(0,1fr))] gap-2">
              {Array.from({ length: 30 }, (_, index) => <Skeleton key={index} className="aspect-square w-full rounded-md" />)}
            </div>
          ) : query.error ? (
            <p className="py-16 text-center text-sm text-destructive">{query.error}</p>
          ) : groups.length ? (
            <div className="flex flex-col gap-6">
              {groups.map(([type, rows]) => (
                <section key={type}>
                  <div className="mb-3 flex items-center gap-2">
                    <h3 className="text-sm font-medium">{formatDeviceType(type)}</h3>
                    <Badge variant="secondary">{rows.length}</Badge>
                  </div>
                  <div className="grid w-full grid-cols-[repeat(15,minmax(0,1fr))] gap-2">
                    {rows.map((device) => (
                      <button
                        key={device.id}
                        type="button"
                        className={cn(
                          "flex aspect-square w-full items-center justify-center truncate rounded-md p-1 text-center text-xs font-medium tabular-nums transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/40",
                          getDeviceTone(device.connectStatus, device.deviceStatus),
                        )}
                        title={device.deviceName || device.deviceCode}
                        aria-label={`${device.deviceCode} ${device.deviceName}`}
                        onClick={() => navigate(`/device/realtime?deviceId=${device.id}`)}
                      >
                        {formatDeviceCode(device.deviceCode)}
                      </button>
                    ))}
                  </div>
                </section>
              ))}
            </div>
          ) : (
            <p className="py-16 text-center text-sm text-muted-foreground">暂无设备数据</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
