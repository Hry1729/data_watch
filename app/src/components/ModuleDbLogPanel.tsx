import type { ReactNode } from "react";
import { RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  LOG_LEVEL_CHIP_CLASS,
  LOG_LEVEL_CLASS,
  LOG_LINE_CLASS,
  LOG_LINES_CLASS,
} from "@/components/LogPanel";
import { cn } from "@/lib/utils";
import type { WriterLogRow } from "@/types";

// ─── 属性 Props ────────────────────────────────────────────────────────────────

export interface ModuleDbLogPanelProps {
  rows: WriterLogRow[];
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
  /** 标题右侧额外控件（如实时/历史切换） */
  headerExtra?: ReactNode;
}

// ─── 组件（DB system_log 历史视图，结构对齐 LogPanel 外壳；collector/writer 通用）──

export function ModuleDbLogPanel({
  rows,
  loading,
  error,
  onRefresh,
  headerExtra,
}: ModuleDbLogPanelProps) {
  return (
    <Card className="h-full min-h-0 w-full gap-0 py-0">
      {/* 面板头部 */}
      <CardHeader className="items-center px-4 pt-4">
        <CardTitle className="self-center text-sm font-semibold leading-[26px]" title="system_log">历史日志</CardTitle>
        <CardAction className="self-center">
          <div className="flex items-center gap-2.5">
            {headerExtra}
            <Button
              variant="ghost"
              size="icon"
              className="size-[26px] rounded-md text-muted-foreground hover:text-foreground"
              onClick={onRefresh}
              disabled={loading}
              aria-label="刷新历史日志"
            >
              <RefreshCw className={cn("size-3.5", loading && "animate-spin")} />
            </Button>
          </div>
        </CardAction>
      </CardHeader>

      <div className="mt-3 border-t border-border" />

      {/* 错误 Banner */}
      {error && (
        <div
          className="flex-shrink-0 border-b border-destructive/40 bg-destructive-soft px-4 py-[7px] text-xs font-medium text-destructive"
        >
          {error}
        </div>
      )}

      {/* 日志滚动区域（DB 返回按时间倒序，最新在上） */}
      <CardContent className={LOG_LINES_CLASS}>
        {rows.length === 0 ? (
          <p className="py-5 text-center font-sans text-xs text-muted-foreground">
            {loading ? "加载中…" : "暂无日志"}
          </p>
        ) : (
          rows.map((row) => (
            <div key={row.id} className={LOG_LINE_CLASS}>
              {/* 时间戳 */}
              <span className="w-[138px] shrink-0 whitespace-nowrap tabular-nums text-muted-foreground opacity-80">{row.time}</span>
              {/* 级别 chip */}
              <span className={cn(LOG_LEVEL_CHIP_CLASS, LOG_LEVEL_CLASS[row.level] ?? LOG_LEVEL_CLASS.INFO)}>
                {row.level}
              </span>
              {/* source（DB 视图保留模块细分） */}
              <span className="shrink-0 text-muted-foreground">{row.source}</span>
              {/* 内容 */}
              <span className="min-w-0 break-words text-foreground">
                {row.content}
              </span>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}
