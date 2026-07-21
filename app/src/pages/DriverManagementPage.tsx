import { useCallback, useState } from "react";
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Search } from "lucide-react";
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
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { backendClient } from "@/lib/backend-client";

export default function DriverManagementPage() {
  const [keyword, setKeyword] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const load = useCallback(
    (token: string) => backendClient.driverList(token, {
      pageNo: page,
      pageSize,
      keyword: keyword.trim() || undefined,
    }),
    [keyword, page, pageSize],
  );
  const query = useBackendQuery(load);
  const rows = query.data?.list ?? [];
  const totalPages = Math.max(1, Math.ceil((query.data?.total ?? 0) / pageSize));

  return (
    <div className="flex h-full min-h-0 w-full flex-col p-4 md:p-6">
      <Card className="flex min-h-0 flex-1 flex-col border border-border">
        <CardHeader>
          <CardTitle>驱动管理</CardTitle>
          <CardAction>
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
          </CardAction>
        </CardHeader>

        <CardContent className="flex min-h-0 flex-1 flex-col gap-3">
          <div className="min-h-0 flex-1 overflow-auto rounded-lg border">
            <TooltipProvider delayDuration={200}>
              <Table className="w-full table-fixed">
                <TableHeader className="bg-muted/50">
                  <TableRow className="h-10 hover:bg-transparent">
                    <TableHead className="w-[18%] text-center">驱动名称</TableHead>
                    <TableHead className="w-[28%] text-center">说明</TableHead>
                    <TableHead className="w-[12%] text-center">读写类型</TableHead>
                    <TableHead className="w-[12%] text-center">状态</TableHead>
                    <TableHead className="w-[12%] text-center">关联设备</TableHead>
                    <TableHead className="w-[18%] text-center">更新时间</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.loading ? (
                    Array.from({ length: 6 }, (_, index) => (
                      <TableRow key={index} className="h-12">
                        <TableCell colSpan={6}><Skeleton className="h-7 w-full" /></TableCell>
                      </TableRow>
                    ))
                  ) : rows.length ? (
                    rows.map((row) => (
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
                              {row.driverDesc || "-"}
                            </TooltipContent>
                          </Tooltip>
                        </TableCell>
                        <TableCell className="max-w-0 overflow-hidden text-center">
                          <span className="block truncate">{row.readWriteType || "-"}</span>
                        </TableCell>
                        <TableCell className="max-w-0 overflow-hidden text-center">
                          <Badge variant={row.status === "disabled" ? "secondary" : "outline"}>
                            {row.status === "disabled" ? "不可用" : "可用"}
                          </Badge>
                        </TableCell>
                        <TableCell className="max-w-0 overflow-hidden text-center tabular-nums">
                          {row.boundDeviceCount ?? 0}
                        </TableCell>
                        <TableCell className="max-w-0 overflow-hidden text-center text-xs tabular-nums text-muted-foreground">
                          <span className="block truncate">{row.updateTime || "-"}</span>
                        </TableCell>
                      </TableRow>
                    ))
                  ) : (
                    <TableRow>
                      <TableCell colSpan={6} className="h-32 text-center text-muted-foreground">
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
    </div>
  );
}
