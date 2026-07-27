import { useCallback, useMemo, useRef, useState } from "react";
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Search, Upload } from "lucide-react";
import { toast } from "sonner";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { useAuth } from "@/contexts/AuthContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { agentClient } from "@/lib/agent-client";
import { backendClient } from "@/lib/backend-client";
import type { DriverListItem } from "@/types/device-monitor";

function normalizeKey(value: string): string {
  return value.trim().toLowerCase().replace(/[\s-]+/g, "_");
}

/** 为 Agent 独有行生成稳定负 id（仅前端列表 key，不入库） */
function hashText(value: string): number {
  let hash = 0;
  for (let i = 0; i < value.length; i += 1) {
    hash = ((hash << 5) - hash + value.charCodeAt(i)) | 0;
  }
  return hash === 0 ? 1 : hash;
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result || "");
      const comma = result.indexOf(",");
      resolve(comma >= 0 ? result.slice(comma + 1) : result);
    };
    reader.onerror = () => reject(reader.error ?? new Error("读取文件失败"));
    reader.readAsDataURL(file);
  });
}

function resolveStatusBadge(row: DriverListItem): { label: string; variant: "default" | "secondary" | "destructive" | "outline" } {
  // Agent 查询失败：明确「状态未知」，勿伪装成内置可用
  if (row.source === "unknown" || row.loadStatus === "unknown") {
    return { label: "状态未知", variant: "secondary" };
  }
  // 已注册外置包在 Agent 侧缺失或加载失败
  if (row.source === "missing" || row.loadStatus === "missing") {
    return { label: "包缺失/异常", variant: "destructive" };
  }
  // Agent 独有、尚未写入 MySQL catalog
  if (row.source === "unregistered") {
    return { label: "未注册", variant: "destructive" };
  }
  if (row.loadStatus && row.loadStatus !== "loaded" && row.source === "installed") {
    return { label: "异常", variant: "destructive" };
  }
  if (row.status === "disabled") {
    return { label: "不可用", variant: "secondary" };
  }
  if (row.loadStatus === "loaded" || row.source === "builtin") {
    return { label: "可用", variant: "outline" };
  }
  if (row.source === "installed") {
    return { label: "已安装", variant: "secondary" };
  }
  return { label: row.status === "available" ? "可用" : "未使用", variant: "outline" };
}

function resolveSourceLabel(source: DriverListItem["source"]): string {
  if (source === "installed") return "已安装";
  if (source === "unregistered") return "未注册";
  if (source === "unknown") return "状态未知";
  if (source === "missing") return "包缺失";
  return "内置";
}

export default function DriverManagementPage() {
  const { auth } = useAuth();
  const canManage =
    auth?.user?.buttons?.includes("device_monitor:driver:manage") ?? false;
  const [keyword, setKeyword] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [busy, setBusy] = useState(false);
  const [uninstallTarget, setUninstallTarget] = useState<DriverListItem | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const load = useCallback(
    async (token: string) => {
      // 先拉全量 catalog（客户端并集后再统一切片，避免「首页追加」破坏分页）
      const [catalogAll, agentResult] = await Promise.all([
        backendClient.driverList(token, {
          pageNo: 1,
          pageSize: 10000,
          keyword: keyword.trim() || undefined,
        }),
        agentClient
          .listDrivers()
          .then((drivers) => ({ ok: true as const, drivers }))
          .catch(() => ({
            ok: false as const,
            drivers: [] as Awaited<ReturnType<typeof agentClient.listDrivers>>,
          })),
      ]);

      const nextMap: Record<string, {
        id?: string;
        name?: string;
        description?: string;
        version?: string;
        sha256?: string;
        status?: string;
        capabilities?: string[];
        error?: string | null;
        readWriteType?: string;
        protocol?: string;
      }> = {};
      for (const item of agentResult.drivers) {
        nextMap[normalizeKey(item.protocol || item.id)] = {
          id: item.id,
          name: item.name,
          description: item.description,
          version: item.version,
          sha256: item.sha256,
          status: item.status,
          capabilities: item.capabilities,
          error: item.error,
          readWriteType: item.readWriteType,
          protocol: item.protocol || item.id,
        };
      }

      const catalogKeySet = new Set(
        catalogAll.list.map((row) => normalizeKey(row.driverName)).filter(Boolean),
      );

      const matchedKeys = new Set<string>();
      const mergedList: DriverListItem[] = catalogAll.list.map((row) => {
        const key = normalizeKey(row.driverName);
        const local = nextMap[key];
        if (!local) {
          // Agent 不可达：勿把未匹配伪装成 builtin
          if (!agentResult.ok) {
            return { ...row, source: "unknown" as const, loadStatus: "unknown" };
          }
          // 已注册外置包在 Agent 缺失：包缺失/异常（不可标 builtin）
          const registeredExternal =
            row.source === "installed" || Boolean(row.driverId) || Boolean(row.installSource);
          if (registeredExternal) {
            return {
              ...row,
              source: "missing" as const,
              loadStatus: "missing",
              error: row.error || "Agent 本机未找到已注册驱动包",
            };
          }
          // 仅 Backend 明确的内置协议可标 builtin
          if (row.source === "builtin") {
            return { ...row, source: "builtin" as const, loadStatus: "builtin" };
          }
          return { ...row, source: "unknown" as const, loadStatus: "unknown" };
        }
        matchedKeys.add(key);
        // Agent 已核对目录/collector：missing / load_failed 直接透出
        const agentMissing = local.status === "missing";
        return {
          ...row,
          source: agentMissing ? ("missing" as const) : ("installed" as const),
          driverId: local.id || row.driverId,
          version: local.version || row.version,
          sha256: local.sha256 || row.sha256,
          loadStatus: local.status,
          capabilities: local.capabilities,
          error: local.error ?? undefined,
          readWriteType: local.readWriteType || row.readWriteType,
        };
      });

      // Agent 独有驱动：并入完整并集，再统一分页
      const kw = keyword.trim().toLowerCase();
      if (agentResult.ok) {
        for (const item of agentResult.drivers) {
          const key = normalizeKey(item.protocol || item.id);
          if (catalogKeySet.has(key) || matchedKeys.has(key)) continue;
          const name = item.protocol || item.id;
          const desc = item.description || item.name || "";
          if (kw && !name.toLowerCase().includes(kw) && !desc.toLowerCase().includes(kw)) {
            continue;
          }
          mergedList.push({
            id: -Math.abs(hashText(key)) || -1,
            driverName: name,
            driverDesc: desc || "Agent 本机已安装，尚未注册到 catalog",
            readWriteType: item.readWriteType || "-",
            updateTime: item.updated_at || "",
            source: "unregistered",
            driverId: item.id,
            version: item.version,
            sha256: item.sha256,
            loadStatus: item.status,
            capabilities: item.capabilities,
            error: item.error ?? undefined,
            boundDeviceCount: 0,
          });
        }
      }

      const total = mergedList.length;
      const start = Math.max(0, (page - 1) * pageSize);
      return {
        ...catalogAll,
        list: mergedList.slice(start, start + pageSize),
        total,
        pageNo: page,
        pageSize,
      };
    },
    [keyword, page, pageSize],
  );
  const query = useBackendQuery(load);
  const rows = query.data?.list ?? [];
  const totalPages = Math.max(1, Math.ceil((query.data?.total ?? 0) / pageSize));

  const warningText = useMemo(
    () => "安装驱动等同于允许其在 collector 进程内执行代码，请仅安装受控来源的内部驱动包。",
    [],
  );

  async function handleInstallFile(file: File) {
    if (!canManage) {
      toast.error("缺少驱动管理权限");
      return;
    }
    if (!auth?.backendToken || !auth.agentToken) {
      toast.error("请先完成登录");
      return;
    }
    if (!file.name.toLowerCase().endsWith(".zip")) {
      toast.error("请选择 ZIP 驱动包");
      return;
    }
    setBusy(true);
    try {
      const b64 = await fileToBase64(file);
      const installed = await agentClient.installDriver(b64);
      const driver = installed.driver;
      await backendClient.registerDriver(auth.backendToken, {
        protocol: driver.protocol || driver.id,
        driverId: driver.id,
        name: driver.name,
        description: driver.description,
        version: driver.version,
        sha256: driver.sha256,
      });
      toast.success(`驱动 ${driver.name || driver.id} 安装成功（${driver.version}）`);
      query.refresh();
    } catch (reason) {
      const message =
        reason && typeof reason === "object" && "message" in reason
          ? String((reason as { message?: string }).message)
          : reason instanceof Error
            ? reason.message
            : "安装失败";
      toast.error(message);
    } finally {
      setBusy(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  }

  async function confirmUninstall() {
    if (!canManage) {
      toast.error("缺少驱动管理权限");
      return;
    }
    if (!uninstallTarget) return;
    if (!auth?.backendToken) {
      toast.error("请先完成登录");
      return;
    }
    const protocol = uninstallTarget.driverName;
    const driverId = uninstallTarget.driverId;
    setBusy(true);
    try {
      await backendClient.uninstallDriver(auth.backendToken, {
        protocol,
        driverId,
      });
      toast.success(`驱动 ${protocol} 已卸载`);
      setUninstallTarget(null);
      query.refresh();
    } catch (reason) {
      const message =
        reason && typeof reason === "object" && "message" in reason
          ? String((reason as { message?: string }).message)
          : reason instanceof Error
            ? reason.message
            : "卸载失败";
      toast.error(message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex h-full min-h-0 w-full flex-col p-4 md:p-6">
      <Card className="flex min-h-0 flex-1 flex-col border border-border">
        <CardHeader>
          <CardTitle>驱动管理</CardTitle>
          <CardAction>
            <div className="flex flex-wrap items-center gap-2">
              <div className="relative w-56">
                <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  className="pl-9"
                  placeholder="搜索驱动名称"
                  value={keyword}
                  onChange={(event) => {
                    setKeyword(event.target.value);
                    setPage(1);
                  }}
                />
              </div>
              <input
                ref={fileRef}
                type="file"
                accept=".zip,application/zip"
                className="hidden"
                onChange={(event) => {
                  const file = event.target.files?.[0];
                  if (file) void handleInstallFile(file);
                }}
              />
              <Button
                size="sm"
                disabled={busy || !canManage}
                onClick={() => fileRef.current?.click()}
              >
                <Upload className="size-4" />
                安装/升级 ZIP
              </Button>
            </div>
          </CardAction>
        </CardHeader>

        <CardContent className="flex min-h-0 flex-1 flex-col gap-3">
          <p className="text-xs text-muted-foreground">{warningText}</p>
          <div className="min-h-0 flex-1 overflow-auto rounded-lg border">
            <TooltipProvider delayDuration={200}>
              <Table className="w-full table-fixed">
                <TableHeader className="bg-muted/50">
                  <TableRow className="h-10 hover:bg-transparent">
                    <TableHead className="w-[14%] text-center">驱动名称</TableHead>
                    <TableHead className="w-[18%] text-center">说明</TableHead>
                    <TableHead className="w-[8%] text-center">来源</TableHead>
                    <TableHead className="w-[8%] text-center">版本</TableHead>
                    <TableHead className="w-[8%] text-center">读写</TableHead>
                    <TableHead className="w-[10%] text-center">状态</TableHead>
                    <TableHead className="w-[8%] text-center">关联设备</TableHead>
                    <TableHead className="w-[12%] text-center">更新时间</TableHead>
                    <TableHead className="w-[14%] text-center">操作</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.loading ? (
                    Array.from({ length: 6 }, (_, index) => (
                      <TableRow key={index} className="h-12">
                        <TableCell colSpan={9}><Skeleton className="h-7 w-full" /></TableCell>
                      </TableRow>
                    ))
                  ) : rows.length ? (
                    rows.map((row) => {
                      const badge = resolveStatusBadge(row);
                      // 外置已安装 / Agent 已有但未注册：均可卸载（内置无按钮）
                      const canUninstall =
                        canManage
                        && (row.source === "installed" || row.source === "unregistered")
                        && (row.boundDeviceCount ?? 0) === 0
                        && !busy
                        && Boolean(row.driverId || row.driverName);
                      const showUninstall =
                        row.source === "installed" || row.source === "unregistered";
                      return (
                        <TableRow key={row.id} className="h-12">
                          <TableCell className="max-w-0 overflow-hidden text-center font-medium">
                            <span className="block truncate">{row.driverName || "-"}</span>
                          </TableCell>
                          <TableCell className="max-w-0 overflow-hidden px-3 text-center text-muted-foreground">
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <span className="block w-full cursor-help truncate">{row.driverDesc || "-"}</span>
                              </TooltipTrigger>
                              <TooltipContent side="top" className="max-w-sm whitespace-pre-wrap break-words text-left leading-relaxed">
                                {row.error ? `失败原因：${row.error}` : (row.driverDesc || "-")}
                              </TooltipContent>
                            </Tooltip>
                          </TableCell>
                          <TableCell className="text-center text-xs">
                            {resolveSourceLabel(row.source)}
                          </TableCell>
                          <TableCell className="text-center text-xs tabular-nums">
                            {row.version || "-"}
                          </TableCell>
                          <TableCell className="max-w-0 overflow-hidden text-center">
                            <span className="block truncate">{row.readWriteType || "-"}</span>
                          </TableCell>
                          <TableCell className="max-w-0 overflow-hidden text-center">
                            <Badge variant={badge.variant}>{badge.label}</Badge>
                          </TableCell>
                          <TableCell className="max-w-0 overflow-hidden text-center tabular-nums">
                            {row.boundDeviceCount ?? 0}
                          </TableCell>
                          <TableCell className="max-w-0 overflow-hidden text-center text-xs tabular-nums text-muted-foreground">
                            <span className="block truncate">{row.updateTime || "-"}</span>
                          </TableCell>
                          <TableCell className="text-center">
                            {showUninstall ? (
                              <Button
                                variant="outline"
                                size="sm"
                                disabled={!canUninstall}
                                title={
                                  (row.boundDeviceCount ?? 0) > 0
                                    ? `仍有 ${row.boundDeviceCount} 台设备绑定`
                                    : row.source === "unregistered"
                                      ? "尚未注册到目录，将仅卸载本机驱动包"
                                      : undefined
                                }
                                onClick={() => setUninstallTarget(row)}
                              >
                                卸载
                              </Button>
                            ) : (
                              <span className="text-xs text-muted-foreground">—</span>
                            )}
                          </TableCell>
                        </TableRow>
                      );
                    })
                  ) : (
                    <TableRow>
                      <TableCell colSpan={9} className="h-32 text-center text-muted-foreground">
                        {query.error ?? "暂无驱动数据"}
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TooltipProvider>
          </div>

          <div className="flex flex-col gap-3 px-1 text-sm sm:flex-row sm:items-center sm:justify-between">
            <span className="text-muted-foreground">共 {query.data?.total ?? 0} 条驱动</span>
            <div className="flex flex-wrap items-center gap-3 sm:justify-end">
              <div className="flex items-center gap-2">
                <span className="text-muted-foreground">每页行数</span>
                <Select
                  value={String(pageSize)}
                  onValueChange={(value) => {
                    setPageSize(Number(value));
                    setPage(1);
                  }}
                >
                  <SelectTrigger className="w-20" size="sm" aria-label="每页行数">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent align="end">
                    <SelectGroup>
                      {[10, 20, 50].map((value) => (
                        <SelectItem key={value} value={String(value)}>{value}</SelectItem>
                      ))}
                    </SelectGroup>
                  </SelectContent>
                </Select>
              </div>
              <span className="min-w-24 text-center font-medium tabular-nums">第 {page} / {totalPages} 页</span>
              <div className="flex items-center gap-2">
                <Button variant="outline" size="icon" className="size-8" disabled={page <= 1} onClick={() => setPage(1)} aria-label="第一页">
                  <ChevronsLeft />
                </Button>
                <Button variant="outline" size="icon" className="size-8" disabled={page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))} aria-label="上一页">
                  <ChevronLeft />
                </Button>
                <Button variant="outline" size="icon" className="size-8" disabled={page >= totalPages} onClick={() => setPage((current) => Math.min(totalPages, current + 1))} aria-label="下一页">
                  <ChevronRight />
                </Button>
                <Button variant="outline" size="icon" className="size-8" disabled={page >= totalPages} onClick={() => setPage(totalPages)} aria-label="最后一页">
                  <ChevronsRight />
                </Button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      <AlertDialog open={Boolean(uninstallTarget)} onOpenChange={(open) => !open && setUninstallTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>卸载驱动</AlertDialogTitle>
            <AlertDialogDescription>
              确认卸载外置驱动“{uninstallTarget?.driverName}”吗？将重启 collector 冷加载，且不可恢复包内容（可重新安装 ZIP）。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy}>取消</AlertDialogCancel>
            <AlertDialogAction disabled={busy} onClick={() => void confirmUninstall()}>确认卸载</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
