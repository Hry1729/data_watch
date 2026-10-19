import { useCallback, useRef, useState } from "react";
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Puzzle, Search, Upload } from "lucide-react";
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
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { CTRL, HEAD_ROW, PAGER, PAGER_BTN, SoftBadge, TABLE_FRAME, TABLE_TEXT, TD, TH, type Tone } from "@/components/DevicePageUi";
import { BUILD_ROLE } from "@/config/build-role";
import { useAuth } from "@/contexts/AuthContext";
import { useAdaptivePageSize } from "@/hooks/useAdaptivePageSize";
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

const SOURCE_DOT: Record<string, string> = {
  official: "bg-brand",
  bundled: "bg-muted-foreground",
  builtin: "bg-muted-foreground",
  installed: "bg-success",
  unregistered: "bg-warning",
  missing: "bg-destructive",
  unknown: "bg-muted-foreground/60",
};

function statusTone(badge: ReturnType<typeof resolveStatusBadge>): Tone {
  if (badge.variant === "destructive") return "bad";
  if (badge.label === "已加载 · 有设备引用") return "ok";
  if (badge.label === "已加载 · 未使用") return "brand";
  return "muted";
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
  const { pageSize, tableViewportRef } = useAdaptivePageSize({ rowHeight: 48 });
  const [busy, setBusy] = useState(false);
  const [uninstallTarget, setUninstallTarget] = useState<DriverListItem | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const load = useCallback(
    async (token: string, signal: AbortSignal) => {
      // 先拉全量 catalog（客户端并集后再统一切片，避免「首页追加」破坏分页）
      const [catalogAll, agentResult] = await Promise.all([
        backendClient.driverList(token, {
          pageNo: 1,
          pageSize: 10000,
          keyword: keyword.trim() || undefined,
        }, signal),
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
        summary: {
          total,
          loaded: mergedList.filter((row) => row.loadStatus === "loaded").length,
          boundDevices: mergedList.reduce((sum, row) => sum + (row.boundDeviceCount ?? 0), 0),
          attention: mergedList.filter((row) => resolveStatusBadge(row).variant === "destructive").length,
        },
      };
    },
    [keyword, page, pageSize],
  );
  const query = useBackendQuery(load);
  const rows = query.data?.list ?? [];
  const totalPages = Math.max(1, Math.ceil((query.data?.total ?? 0) / pageSize));

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
    <div className="flex h-full min-h-0 w-full flex-col gap-4">
      <Card className="flex min-h-0 flex-1 flex-col gap-0 py-0">
        <div className="flex shrink-0 flex-wrap items-center gap-2 px-5 pt-5">
          <div className="mr-2 flex items-baseline gap-2">
            <h2 className="text-[15px] font-semibold leading-none">驱动列表</h2>
            <span className="text-[13px] tabular-nums text-muted-foreground">{query.data ? `${query.data.total} 个` : "—"}</span>
          </div>
          <div className="relative w-[220px]">
            <Search className="absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              className={`${CTRL} pl-8`}
              placeholder="搜索驱动名称"
              value={keyword}
              onChange={(event) => {
                setKeyword(event.target.value);
                setPage(1);
              }}
            />
          </div>
          <div className="ml-auto flex items-center gap-2">
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
              className="h-8 gap-1.5 bg-brand px-3 text-[13px] text-brand-foreground hover:bg-brand/90"
              disabled={busy || !canManage}
              onClick={() => fileRef.current?.click()}
            >
              <Upload className="size-3.5" />
              安装/升级 ZIP
            </Button>
          </div>
        </div>

        <CardContent className="flex min-h-0 flex-1 flex-col px-5 pb-5 pt-4">
          <div className={`flex min-h-0 flex-1 flex-col ${TABLE_FRAME}`}>
            <div ref={tableViewportRef} className="min-h-0 flex-1 overflow-auto">
            <TooltipProvider delayDuration={200}>
              <Table className={`w-full table-fixed ${TABLE_TEXT}`}>
                <colgroup>
                  <col className="w-[14%]" /><col className="w-[15%]" /><col className="w-[9%]" /><col className="w-[6%]" /><col className="w-[5%]" />
                  <col className="w-[16%]" /><col className="w-[8%]" /><col className="w-[15%]" /><col className="w-[12%]" />
                </colgroup>
                <TableHeader>
                  <TableRow className={HEAD_ROW}>
                    <TableHead className={TH}>驱动名称</TableHead>
                    <TableHead className={TH}>说明</TableHead>
                    <TableHead className={TH}>来源</TableHead>
                    <TableHead className={TH}>版本</TableHead>
                    <TableHead className={TH}>读写</TableHead>
                    <TableHead className={TH}>状态</TableHead>
                    <TableHead className={`${TH} text-right`}>关联设备</TableHead>
                    <TableHead className={TH}>更新时间</TableHead>
                    <TableHead className={`${TH} text-center`}>操作</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.loading ? (
                    Array.from({ length: pageSize }, (_, index) => (
                      <TableRow key={index} className="h-12">
                        <TableCell colSpan={9} className={TD}><Skeleton className="h-7 w-full" /></TableCell>
                      </TableRow>
                    ))
                  ) : rows.length ? (
                    rows.map((row) => {
                      const badge = resolveStatusBadge(row);
                      const failed = badge.variant === "destructive" && Boolean(row.error);
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
                          <TableCell className={`${TD} max-w-0 overflow-hidden font-medium`}>
                            <div className="flex min-w-0 items-center gap-2">
                              <span className="grid size-[26px] shrink-0 place-items-center rounded-[7px] bg-muted text-muted-foreground"><Puzzle className="size-3.5" strokeWidth={1.8} /></span>
                              <span className="truncate">{row.driverName || "-"}</span>
                            </div>
                          </TableCell>
                          <TableCell className={`${TD} max-w-0 overflow-hidden text-muted-foreground`}>
                            <Tooltip>
                              <TooltipTrigger asChild>
                                <span className="flex w-full min-w-0 cursor-help items-center gap-1.5">
                                  {failed && <span className="grid size-3.5 shrink-0 place-items-center rounded-full bg-destructive text-[10px] font-bold text-white">!</span>}
                                  <span className="block min-w-0 cursor-help truncate">{row.driverDesc || "-"}</span>
                                </span>
                              </TooltipTrigger>
                              <TooltipContent side="top" className="max-w-sm whitespace-pre-wrap break-words text-left leading-relaxed">
                                {row.error ? `失败原因：${row.error}` : (row.driverDesc || "-")}
                              </TooltipContent>
                            </Tooltip>
                          </TableCell>
                          <TableCell className={TD}>
                            <span className="inline-flex items-center gap-1.5 text-xs">
                              <span className={`size-1.5 rounded-[2px] ${SOURCE_DOT[row.source ?? "unknown"] ?? SOURCE_DOT.unknown}`} />
                              {resolveSourceLabel(row.source)}
                            </span>
                          </TableCell>
                          <TableCell className={`${TD} font-mono text-xs`}>
                            {row.version || "-"}
                          </TableCell>
                          <TableCell className={`${TD} max-w-0 overflow-hidden`}>
                            <span className="block truncate">{row.readWriteType || "-"}</span>
                          </TableCell>
                          <TableCell className={`${TD} max-w-0 overflow-hidden`}>
                            <SoftBadge tone={statusTone(badge)} className="max-w-full"><span className="truncate">{badge.label}</span></SoftBadge>
                          </TableCell>
                          <TableCell className={`${TD} text-right tabular-nums`}>
                            {row.boundDeviceCount ?? 0}
                          </TableCell>
                          <TableCell className={`${TD} max-w-0 overflow-hidden font-mono text-xs text-muted-foreground`}>
                            <span className="block truncate">{row.updateTime || "-"}</span>
                          </TableCell>
                          <TableCell className={`${TD} text-center`}>
                            {row.source === "official" && ["not_installed", "missing", "invalid"].includes(row.installStatus ?? "") ? (
                              <Button variant="outline" size="sm" className="h-7 px-2.5 text-xs" disabled={!canManage || busy} onClick={() => void prepareInstall(undefined, row.driverId || row.driverName)}>恢复安装</Button>
                            ) : showUninstall ? (
                              <Button
                                variant="outline"
                                size="sm"
                                className="h-7 px-2.5 text-xs"
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

            <div className={PAGER}>
              <span>共 {query.data?.total ?? 0} 条驱动</span>
              <span className="flex-1" />
              <span>每页 {pageSize} 行</span>
              <span className="min-w-20 text-center font-medium tabular-nums text-foreground">第 {page} / {totalPages} 页</span>
              <Button variant="outline" size="icon" className={PAGER_BTN} disabled={page <= 1} onClick={() => setPage(1)} aria-label="第一页">
                <ChevronsLeft />
              </Button>
              <Button variant="outline" size="icon" className={PAGER_BTN} disabled={page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))} aria-label="上一页">
                <ChevronLeft />
              </Button>
              <Button variant="outline" size="icon" className={PAGER_BTN} disabled={page >= totalPages} onClick={() => setPage((current) => Math.min(totalPages, current + 1))} aria-label="下一页">
                <ChevronRight />
              </Button>
              <Button variant="outline" size="icon" className={PAGER_BTN} disabled={page >= totalPages} onClick={() => setPage(totalPages)} aria-label="最后一页">
                <ChevronsRight />
              </Button>
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
