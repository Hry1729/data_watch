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
import { BUILD_ROLE } from "@/config/build-role";
import { useAuth } from "@/contexts/AuthContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { agentClient } from "@/lib/agent-client";
import { resolveStatusBadge } from "@/lib/driver-status";
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

function resolveSourceLabel(source: DriverListItem["source"]): string {
  if (source === "official") return "官方预装";
  if (source === "bundled") return "安装包内置";
  if (source === "installed") return "已安装";
  if (source === "unregistered") return "未注册";
  if (source === "unknown") return "状态未知";
  if (source === "missing") return "包缺失";
  return "内置";
}

export default function DriverManagementPage() {
  const { auth } = useAuth();
  const [installTarget, setInstallTarget] = useState<{ file?: File; driverId?: string; name: string; version: string } | null>(null);
  const canManage =
    BUILD_ROLE !== "distributed-center" && (auth?.user?.buttons?.includes("device_monitor:driver:manage") ?? false);
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
        install_status?: string;
        runtime_status?: string;
        capabilities?: string[];
        error?: string | null;
        readWriteType?: string;
        protocol?: string;
        aliases?: string[];
        source?: string;
        protected?: boolean;
      }> = {};
      for (const item of agentResult.drivers) {
        const local = {
          id: item.id,
          name: item.name,
          description: item.description,
          version: item.version,
          sha256: item.sha256,
          status: item.status,
          install_status: item.install_status,
          runtime_status: item.runtime_status,
          capabilities: item.capabilities,
          error: item.error,
          readWriteType: item.readWriteType,
          protocol: item.protocol || item.id,
          aliases: item.aliases,
          source: item.source,
          protected: item.protected,
        };
        const keys = [item.id, item.protocol, ...(item.aliases ?? [])]
          .map((value) => normalizeKey(value || ""))
          .filter(Boolean);
        for (const key of keys) {
          nextMap[key] = local;
        }
      }

      const catalogKeySet = new Set(
        catalogAll.list.map((row) => normalizeKey(row.driverName)).filter(Boolean),
      );

      const matchedKeys = new Set<string>();
      const mergedList: DriverListItem[] = catalogAll.list.map((row) => {
        const key = normalizeKey(row.driverName);
        if (BUILD_ROLE === "distributed-center") return { ...row, installStatus: "not_applicable", loadStatus: "unknown" };
        const local = nextMap[key];
        if (!local) {
          // Agent 不可达：保留保护标记，但运行时状态明确为未知。
          if (!agentResult.ok) {
            return { ...row, source: "unknown" as const, loadStatus: "unknown" };
          }
          // Agent 未报告不能证明 Collector 已停止，也不能证明包缺失
          if (row.source === "bundled" || row.source === "builtin" || row.protected) {
            return {
              ...row,
              source: "bundled" as const,
              protected: true,
              loadStatus: "unknown",
              error: row.error || "Agent 未报告该驱动，无法核验本机状态",
            };
          }
          return { ...row, source: "unknown" as const, loadStatus: "unknown" };
        }
        matchedKeys.add(key);
        // Agent 已核对目录/collector：missing / load_failed 直接透出
        const agentMissing = local.status === "missing";
        const official = local.source === "official";

        return {
          ...row,
          source: official ? ("official" as const) : agentMissing ? ("missing" as const) : ("installed" as const),
          protected: false,
          driverId: local.id || row.driverId,
          version: local.version || row.version,
          sha256: local.sha256 || row.sha256,
          loadStatus: local.runtime_status ?? local.status,
          installStatus: local.install_status ?? (local.status === "missing" ? "missing" : undefined),
          capabilities: local.capabilities,
          error: local.error ?? undefined,
          readWriteType: local.readWriteType || row.readWriteType,
        };
      });

      // Agent 独有驱动：并入完整并集，再统一分页
      const kw = keyword.trim().toLowerCase();
      if (agentResult.ok) {
        for (const item of agentResult.drivers) {
          const keys = [item.id, item.protocol, ...(item.aliases ?? [])]
            .map((value) => normalizeKey(value || ""))
            .filter(Boolean);
          if (keys.some((key) => catalogKeySet.has(key) || matchedKeys.has(key))) continue;
          const key = keys[0] || normalizeKey(item.id);
          const name = item.protocol || item.id;
          const desc = item.description || item.name || "";
          const official = item.source === "official";
          if (kw && !name.toLowerCase().includes(kw) && !desc.toLowerCase().includes(kw)) {
            continue;
          }
          mergedList.push({
            id: -Math.abs(hashText(key)) || -1,
            driverName: name,
            driverDesc: desc || (official ? "官方预装驱动" : "Agent 本机已安装，尚未注册到 catalog"),
            readWriteType: item.readWriteType || "-",
            updateTime: item.updated_at || "",
            source: official ? "official" : "unregistered",
            protected: false,
            driverId: item.id,
            version: item.version,
            sha256: item.sha256,
            loadStatus: item.runtime_status ?? item.status,
            installStatus: item.install_status ?? (item.status === "missing" ? "missing" : undefined),
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
    () => BUILD_ROLE === "distributed-center"
      ? "此处为平台协议目录，中心机不运行采集。驱动安装与运行状态请在目标边缘节点查看。"
      : "当前目标：本机采集节点。安装和卸载影响本节点；只接受官方或内部审核通过的驱动包。",
    [],
  );

  async function prepareInstall(file?: File, driverId?: string) {
    if (!canManage || busy) return;
    setBusy(true);
    try {
      const result = await agentClient.previewDriver(driverId
        ? { restore_id: driverId }
        : { package_base64: await fileToBase64(file!) });
      setInstallTarget({ file, driverId, name: result.driver.name, version: result.driver.version });
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "驱动预检失败");
    } finally {
      setBusy(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  }

  async function handleInstallFile(file: File | undefined, restoreId?: string) {
    if (!canManage) {
      toast.error("缺少驱动管理权限");
      return;
    }
    if (!auth?.backendToken || !auth.agentToken) {
      toast.error("请先完成登录");
      return;
    }
    if (file && !file.name.toLowerCase().endsWith(".zip")) {
      toast.error("请选择 ZIP 驱动包");
      return;
    }
    setBusy(true);
    try {
      const installed = restoreId
        ? await agentClient.restoreDriver(restoreId)
        : await agentClient.installDriver(await fileToBase64(file!));
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
    <div className="flex h-full min-h-0 w-full flex-col px-4 pb-4 md:px-6 md:pb-6">
      <Card className="flex min-h-0 flex-1 flex-col">
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
                  if (file) void prepareInstall(file);
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
                        && (row.source === "official" || row.source === "installed" || row.source === "unregistered")
                        && !row.protected
                        && (row.boundDeviceCount ?? 0) === 0
                        && !busy
                        && Boolean(row.driverId || row.driverName);
                      const showUninstall =
                        row.installStatus !== "not_installed" && !row.protected
                        && (row.source === "official" || row.source === "installed" || row.source === "unregistered");
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
                            {row.source === "official" && ["not_installed", "missing", "invalid"].includes(row.installStatus ?? "") ? (
                              <Button variant="outline" size="sm" disabled={!canManage || busy} onClick={() => void prepareInstall(undefined, row.driverId || row.driverName)}>恢复安装</Button>
                            ) : showUninstall ? (
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

      <AlertDialog open={Boolean(installTarget)} onOpenChange={(open) => { if (!open && !busy) setInstallTarget(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>确认{installTarget?.driverId ? "恢复" : "安装或升级"}驱动</AlertDialogTitle>
            <AlertDialogDescription>
              {installTarget?.name} {installTarget?.version} 已通过兼容性预检，确认后将切换驱动包。
              若采集正在运行，本节点所有设备的采集会暂停后恢复；已停止的采集保持停止。失败时恢复旧包。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busy}>取消</AlertDialogCancel>
            <AlertDialogAction disabled={busy} onClick={() => { if (installTarget) { void handleInstallFile(installTarget.file, installTarget.driverId); setInstallTarget(null); } }}>确认变更</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={Boolean(uninstallTarget)} onOpenChange={(open) => !open && setUninstallTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>卸载驱动</AlertDialogTitle>
            <AlertDialogDescription>
              确认卸载驱动“{uninstallTarget?.driverName}”吗？若采集正在运行，本节点所有设备的采集会暂停后恢复；已停止的采集保持停止。协议目录和历史数据会保留，官方驱动可通过恢复安装重新启用。
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
